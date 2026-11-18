using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class ResidentBehaviour : MonoBehaviour
{
    public enum QuestStatus { NotStarted, InProgress, Completed }
    [Header("Animation")]
    [SerializeField] private Animator anim;

    [Header("Quest State & Requirements")]
    public static QuestStatus currentQuestStatus = QuestStatus.NotStarted;
    public int requiredBones = 6;
    public int requiredSlime = 4;
    public int requiredHearts = 1;
    public int rewardKeyFragments = 1;

    [Header("Toggle Talking")]
    [SerializeField] private bool isTalking = false;

    [Header("Player Raycasting")]
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private GameObject playerCapsule;
    public float pickupDistance = 5.0f;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera mainCam;
    [SerializeField] private CinemachineCamera cutsceneCam;

    [Header("Dialogue")]
    [SerializeField] private Dialogue questOfferDialogue;       // Ends with Yes/No buttons
    [SerializeField] private Dialogue questAcceptedDialogue;    // "Greatttt!!! You won't regret it!"
    [SerializeField] private Dialogue questDeclinedDialogue;    // "Oh, I'm sure you'll change your mind..."
    [SerializeField] private Dialogue questIncompleteDialogue;  // "I'm gonna need more parts than that!"
    [SerializeField] private Dialogue questSuccessDialogue;     // "Yesss, parts..." + backstory
    // private IEnumerator myEnumerator;

    // [Header("Dialogue Manager")]
    // [SerializeField] private DialogueTrigger dialogueTrigger;
    // [SerializeField] private DialogueManager dialogueManager;

    [Header("UI Prompt")]
    [SerializeField] private GameObject promptUI;
    // private bool playerInRange = false;
    // [Header("UI Overlay")]
    // [SerializeField] private GameObject dialogUI;
    // [SerializeField] private GameObject mainUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // myEnumerator = MySequence();
        if (promptUI != null) promptUI.SetActive(false);


    }

    // Update is called once per frame
    void Update()
    {
        // bool cast = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward, out RaycastHit raycasthit, pickupDistance);
        // var interactAction = InputSystem.actions.FindAction("Interact");

        // if (cast && !isTalking)
        // {
        //     // Debug.Log("raycast hit " + isTalking);
        //     if (raycasthit.collider.CompareTag("npc"))
        //     {
        //         Debug.Log("cast from resident");
        //         // playerInRange = true;
        //         if (promptUI != null) promptUI.SetActive(true);
        //     }
        //     if (raycasthit.collider.CompareTag("npc") && Input.GetKeyDown(KeyCode.E))
        //     // if (raycasthit.collider.CompareTag("npc") && interactAction != null)
        //     {
        //         Debug.Log("talking mode " + isTalking);
        //         Talk();
        //         Debug.Log("after enable talking " + isTalking);

        //     }

        // }
        // else if (isTalking && Input.GetKeyDown(KeyCode.E))
        // // else if (isTalking && interactAction != null)
        // {
        //     Debug.Log("talking mode " + isTalking);
        //     StopTalking();
        //     Debug.Log("after disable talking " + isTalking);
        // }
        // else if (isTalking && FindFirstObjectByType<DialogueManager>().conversationEnded)
        // {
        //     Debug.Log("conversation ended");
        //     Debug.Log("talking mode " + isTalking);
        //     StopTalking();
        //     Debug.Log("after disable talking " + isTalking);
        // }
        // else
        // {
        //     // playerInRange = false;
        //     if (promptUI != null) promptUI.SetActive(false);

        // }
        // Flowchart: Once quest is completed, Resident Ghost becomes Inactive
        if (currentQuestStatus == QuestStatus.Completed && !isTalking)
        {
            if (promptUI != null) promptUI.SetActive(false);
            return;
        }

        if (!isTalking)
        {
            bool cast = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward, out RaycastHit hit, pickupDistance);
            if (cast && hit.collider.CompareTag("npc"))
            {
                if (promptUI != null) promptUI.SetActive(true);
                if (Input.GetKeyDown(KeyCode.E))
                {
                    StartResidentInteraction();
                }
            }
            else
            {
                if (promptUI != null) promptUI.SetActive(false);
            }
        }
        else if (isTalking && Input.GetKeyDown(KeyCode.E))
        {
            // Skip dialogue
            DialogueManager.Instance.EndDialogue();
        }

    }
    private void StartResidentInteraction()
    {
        if (promptUI != null) promptUI.SetActive(false);
        isTalking = true;

        cutsceneCam.Priority = 20;
        mainCam.Priority = 10;
        playerCapsule.SetActive(false);

        if (currentQuestStatus == QuestStatus.NotStarted)
        {
            DialogueManager.Instance.StartDialogue(
                questOfferDialogue,
                onComplete: null,
                showChoiceOnLastLine: true,
                onChoiceSelected: OnQuestOfferChoice
            );
        }
        else if (currentQuestStatus == QuestStatus.InProgress)
        {
            if (HasEnoughParts())
            {
                CompleteQuestAndReward();
            }
            else
            {
                DialogueManager.Instance.StartDialogue(questIncompleteDialogue, onComplete: StopTalking);
            }
        }
    }
    private void OnQuestOfferChoice(bool accepted)
    {
        if (accepted)
        {
            currentQuestStatus = QuestStatus.InProgress;
            anim.SetBool("yes", true);
            DialogueManager.Instance.StartDialogue(questAcceptedDialogue, onComplete: StopTalking);
        }
        else
        {
            anim.SetBool("no", true);
            DialogueManager.Instance.StartDialogue(questDeclinedDialogue, onComplete: StopTalking);
        }
    }
    private bool HasEnoughParts()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return false;
        return inv.GetCollectibleCount(CollectibleType.Bone) >= requiredBones &&
               inv.GetCollectibleCount(CollectibleType.Slime) >= requiredSlime &&
               inv.GetCollectibleCount(CollectibleType.Heart) >= requiredHearts;
    }
    private void CompleteQuestAndReward()
    {
        var inv = PlayerInventory.Instance;
        inv.ConsumeCollectible(CollectibleType.Bone, requiredBones);
        inv.ConsumeCollectible(CollectibleType.Slime, requiredSlime);
        inv.ConsumeCollectible(CollectibleType.Heart, requiredHearts);

        // Give rewards (spare key fragment + potions)
        inv.AddCollectible(CollectibleType.Slime, rewardKeyFragments);
        inv.AddCollectible(CollectibleType.HealthPotion, 2);
        inv.AddCollectible(CollectibleType.AttackPlusPotion, 1);

        currentQuestStatus = QuestStatus.Completed;
        anim.SetBool("celebrate", true);

        DialogueManager.Instance.StartDialogue(questSuccessDialogue, onComplete: StopTalking);
    }
    // void Talk()
    // {
    //     if (promptUI != null) promptUI.SetActive(false);
    //     dialogueTrigger.TriggerDialogue();
    //     // StartCoroutine(myEnumerator);
    //     isTalking = true;
    //     //switch camera to telescope camera
    //     cutsceneCam.Priority = 20;
    //     mainCam.Priority = 10;
    //     playerCapsule.SetActive(false);

    //     // if (dialogUI != null)
    //     // {//enable telescope UI
    //     //     dialogUI.SetActive(true);
    //     // }
    //     // if (mainUI != null)
    //     // {//enable telescope UI
    //     //     mainUI.SetActive(false);
    //     // }
    // }
    void StopTalking()
    {
        // if (promptUI != null) promptUI.SetActive(false);

        // StopCoroutine(myEnumerator);
        anim.SetBool("yes", false);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", false);
        isTalking = false;
        // FindFirstObjectByType<DialogueManager>().EndDialogue();

        //switch camera to telescope camera
        playerCapsule.SetActive(true);

        cutsceneCam.Priority = 10;
        mainCam.Priority = 20;
        // if (dialogUI != null)
        // {//enable telescope UI
        //     dialogUI.SetActive(false);
        // }
        // if (mainUI != null)
        // {//enable telescope UI
        //     mainUI.SetActive(true);
        // }
    }
    // IEnumerator MySequence()
    // {
    //     yield return new WaitForSeconds(4);
    //     //nod head
    //     anim.SetBool("yes", true);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", false);
    //     //shake head
    //     yield return new WaitForSeconds(4);
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", true);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", false);
    //     //thanking bow
    //     yield return new WaitForSeconds(4);
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", true);
    //     anim.SetBool("celebrate", false);
    //     //celebration
    //     yield return new WaitForSeconds(4);
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", true);

    // }
}
