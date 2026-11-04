using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.Text;

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Animator animator;
    public Animator characterAnimator;
    [Header("Audio")]
    public AudioSource audioSource; // Assign an AudioSource component in the inspector (can be on this GameObject)

    private Queue<DialogueLine> dialogueLines;
    // private Queue<string> sentences;

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
    }

    public void StartDialogue(Dialogue dialogue)
    {
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
        dialogueLines.Clear();

        foreach (DialogueLine line in dialogue.lines)
        {
            dialogueLines.Enqueue(line);
        }
        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {
        characterAnimator.SetBool("thankyou", false);
        characterAnimator.SetBool("yes", false);
        characterAnimator.SetBool("no", false);
        characterAnimator.SetBool("celebrate", false);
        // if (sentences.Count == 0)
        // {
        //     EndDialogue();
        //     return;
        // }
        // string sentence = sentences.Dequeue();
        if (dialogueLines.Count == 0)
        {
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

    void EndDialogue()
    {
        // Restore cursor state when dialogue ends (lock/hide it again for gameplay)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Stop any currently playing sentence audio when dialogue ends
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        animator.SetBool("IsOpen", false);

        Debug.Log("End of conversation");
    }

}
