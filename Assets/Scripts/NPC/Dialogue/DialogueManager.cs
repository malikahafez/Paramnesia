using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.Text;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    [Header("UI References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Animator animator;

    [Header("Optional Choice Buttons (For Side Quests)")]
    public GameObject choiceButtonPanel;
    public Button yesButton;
    public Button noButton;
    public Button continueButton; // Optional: hide continue button while Yes/No is visible

    [Header("Audio")]
    public AudioSource audioSource; // Assign an AudioSource component in the inspector (can be on this GameObject)
    public bool conversationEnded = true;
    private Animator characterAnimator;
    private Queue<DialogueLine> dialogueLines;
    private Action onDialogueCompleteCallback;
    private Action<bool> onChoiceMadeCallback;
    private bool waitForChoiceAtEnd = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // private Queue<string> sentences;

    // private void Awake()
    // {
    //     if (Instance == null)
    //     {
    //         Instance = this;
    //     }
    //     else
    //     {
    //         Destroy(gameObject);
    //     }
    // }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // sentences = new Queue<string>();
        dialogueLines = new Queue<DialogueLine>();

        // Automatically grab AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        if (choiceButtonPanel != null) choiceButtonPanel.SetActive(false);

        if (yesButton != null)
        {
            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() => HandleChoice(true));
        }
        if (noButton != null)
        {
            noButton.onClick.RemoveAllListeners();
            noButton.onClick.AddListener(() => HandleChoice(false));
        }
        // button.onClick.RemoveAllListeners();
        // button.onClick.AddListener(DisplayNextSentence);
    }
    /// Standard dialogue start (backwards-compatible with DialogueTrigger).

    public void StartDialogue(Dialogue dialogue)
    {
        StartDialogue(dialogue, null, false, null);
    }
    /// Extended dialogue start supporting completion callbacks and Yes/No quest choices.
    public void StartDialogue(Dialogue dialogue, Action onComplete, bool showChoiceOnLastLine = false, Action<bool> onChoiceSelected = null)
    {
        if (dialogue == null)
        {
            Debug.LogError("StartDialogue Error: The 'Dialogue' asset is missing or unassigned on the DialogueTrigger component!", this);
            return;
        }
        if (animator == null)
        {
            Debug.LogError("StartDialogue Error: The 'Animator' reference is missing on the DialogueManager component!", this);
        }
        if (nameText == null)
        {
            Debug.LogError("StartDialogue Error: The 'Name Text' reference is missing on the DialogueManager component!", this);
        }
        conversationEnded = false;
        characterAnimator = dialogue.characterAnimator;
        onDialogueCompleteCallback = onComplete;
        waitForChoiceAtEnd = showChoiceOnLastLine;
        onChoiceMadeCallback = onChoiceSelected;

        if (choiceButtonPanel != null) choiceButtonPanel.SetActive(false);
        if (continueButton != null) continueButton.gameObject.SetActive(true);

        animator.SetBool("IsOpen", true);
        nameText.text = dialogue.name;
        // Unlock and show the mouse cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Starting conversation with " + dialogue.name);
        // sentences.Clear();

        // foreach (string sentence in dialogue.sentences)
        // {
        //     sentences.Enqueue(sentence);
        // }
        if (dialogueLines == null)
        {
            dialogueLines = new Queue<DialogueLine>();
            Debug.Log("dialogueLines were null, created new queue");

        }
        dialogueLines.Clear();
        if (dialogue.lines == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning($"Dialogue warning: '{dialogue.name}' has no lines configured in its array!", this);
        }

        foreach (DialogueLine line in dialogue.lines)
        {
            dialogueLines.Enqueue(line);
            // Debug.Log($"Enqueued sentence: '{line}'");
        }
        Debug.Log("Total lines enqueued: " + dialogueLines.Count);
        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {
        Debug.Log("inside display next sentence");
        if (characterAnimator != null)
        {
            characterAnimator.SetBool("thankyou", false);
            characterAnimator.SetBool("yes", false);
            characterAnimator.SetBool("no", false);
            characterAnimator.SetBool("celebrate", false);
        }
        // if (sentences.Count == 0)
        // {
        //     EndDialogue();
        //     return;
        // }
        // string sentence = sentences.Dequeue();
        if (dialogueLines.Count == 0)
        {
            Debug.Log("no more sentences");
            EndDialogue();
            return;
        }

        DialogueLine currentLine = dialogueLines.Dequeue();
        // 1. Trigger animation if specified
        if (!string.IsNullOrEmpty(currentLine.animationTrigger))
        {
            TriggerAnimation(currentLine.animationTrigger);
        }

        // 2. Play audio clip if specified
        if (currentLine.sentenceAudio != null)
        {
            audioSource.PlayOneShot(currentLine.sentenceAudio, currentLine.audioVolume);
        }
        StopAllCoroutines();//make sure if user skips the old dialogue it will stop typing
        // dialogueText.text = sentence;
        // StartCoroutine(TypeSentence(sentence));
        // Debug.Log(sentence);
        StartCoroutine(TypeSentence(currentLine.sentence));
        Debug.Log(currentLine.sentence);
        // If this was the final line AND we need a Yes/No decision (Resident Ghost quest offer)
        if (dialogueLines.Count == 0 && waitForChoiceAtEnd)
        {
            if (choiceButtonPanel != null) choiceButtonPanel.SetActive(true);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
        }
    }
    private void HandleChoice(bool accepted)
    {
        if (choiceButtonPanel != null) choiceButtonPanel.SetActive(false);
        if (continueButton != null) continueButton.gameObject.SetActive(true);
        waitForChoiceAtEnd = false;

        Action<bool> callback = onChoiceMadeCallback;
        onChoiceMadeCallback = null;
        callback?.Invoke(accepted);
    }

    void TriggerAnimation(string triggerName)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetTrigger(triggerName);
        }
        Debug.Log("Triggered animation/event: " + triggerName);
    }
    IEnumerator TypeSentence(string sentence)
    {
        dialogueText.text = "";
        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.02f);//wait a frame after each letter
        }
    }

    public void EndDialogue(bool keepCursorUnlocked = false)
    {
        // Restore cursor state when dialogue ends (lock/hide it again for gameplay)
        if (!keepCursorUnlocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        // Stop any currently playing sentence audio when dialogue ends
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        if (choiceButtonPanel != null) choiceButtonPanel.SetActive(false);
        animator.SetBool("IsOpen", false);
        conversationEnded = true;
        Debug.Log("End of conversation");
        characterAnimator = null;
        Action callback = onDialogueCompleteCallback;
        onDialogueCompleteCallback = null;
        callback?.Invoke();
    }

}
