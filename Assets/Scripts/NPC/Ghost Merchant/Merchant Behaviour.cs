using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class MerchantBehaviour : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator anim;
    [Header("State")]
    [SerializeField] private bool isTalking = false;
    [SerializeField] private bool isShopOpen = false;
    private static bool hasMetMerchantBefore = false;

    [Header("Player Raycasting")]
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private GameObject playerCapsule;
    public float pickupDistance = 5.0f;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera mainCam;
    [SerializeField] private CinemachineCamera cutsceneCam;
    // private IEnumerator myEnumerator;
    [Header("Dialogue")]
    [SerializeField] private Dialogue firstTimeEnterDialogue;
    [SerializeField] private Dialogue repeatEnterDialogue;
    [SerializeField] private Dialogue exitDialogue;

    [Header("Dialogue Manager")]
    [SerializeField] private DialogueTrigger dialogueTrigger;

    [Header("UI References")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private GameObject shopUIOverlay;
    // private bool playerInRange = false;


    // [SerializeField] private DialogueManager dialogueManager;
    // [Header("UI Overlay")]
    // [SerializeField] private GameObject dialogUI;
    // [SerializeField] private GameObject mainUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // myEnumerator = MySequence();
        if (promptUI != null) promptUI.SetActive(false);
        if (shopUIOverlay != null) shopUIOverlay.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        // bool cast = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward, out RaycastHit raycasthit, pickupDistance);
        // var interactAction = InputSystem.actions.FindAction("Interact");
        // if (cast && !isTalking)
        // {
        //     // Debug.Log("raycast hit " + isTalking);
        //     if (raycasthit.collider.CompareTag("merchant"))
        //     {
        //         Debug.Log("cast from merchant");
        //         // playerInRange = true;
        //         if (promptUI != null) promptUI.SetActive(true);
        //     }
        //     if (raycasthit.collider.CompareTag("merchant") && Input.GetKeyDown(KeyCode.E))
        //     // if (raycasthit.collider.CompareTag("merchant") && interactAction != null)

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
        if (!isTalking && !isShopOpen)
        {
            bool cast = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward, out RaycastHit hit, pickupDistance);
            if (cast && hit.collider.CompareTag("merchant"))
            {
                if (promptUI != null) promptUI.SetActive(true);
                if (Input.GetKeyDown(KeyCode.E))
                {
                    StartMerchantInteraction();
                }
            }
            else
            {
                if (promptUI != null) promptUI.SetActive(false);
            }
        }
        else if (isTalking && !isShopOpen && Input.GetKeyDown(KeyCode.E))
        {
            // Flowchart "Skip Dialogue?" -> Yes
            DialogueManager.Instance.EndDialogue(keepCursorUnlocked: true);
        }

    }
    private void StartMerchantInteraction()
    {
        if (promptUI != null) promptUI.SetActive(false);
        isTalking = true;

        cutsceneCam.Priority = 20;
        mainCam.Priority = 10;
        playerCapsule.SetActive(false);

        Dialogue introToPlay = hasMetMerchantBefore ? repeatEnterDialogue : firstTimeEnterDialogue;
        hasMetMerchantBefore = true;

        DialogueManager.Instance.StartDialogue(introToPlay, onComplete: OpenShopOverlay);
    }
    private void OpenShopOverlay()
    {
        isTalking = false;
        isShopOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (shopUIOverlay != null) shopUIOverlay.SetActive(true);
    }
    /// Wire this to the Shop UI's "Close / Exit" button OnClick() event!
    public void OnShopCloseButtonPressed()
    {
        if (shopUIOverlay != null) shopUIOverlay.SetActive(false);
        isShopOpen = false;
        isTalking = true;

        // Flowchart: Close & exit? -> Yes -> Exit Dialogue -> Idle
        DialogueManager.Instance.StartDialogue(exitDialogue, onComplete: FinishMerchantInteraction);
    }
    private void FinishMerchantInteraction()
    {
        anim.SetBool("yes", false);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", false);

        isTalking = false;
        isShopOpen = false;

        playerCapsule.SetActive(true);
        cutsceneCam.Priority = 10;
        mainCam.Priority = 20;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void Talk()
    {
        if (promptUI != null) promptUI.SetActive(false);

        dialogueTrigger.TriggerDialogue();
        // StartCoroutine(myEnumerator);
        isTalking = true;
        //switch camera to telescope camera
        cutsceneCam.Priority = 20;
        mainCam.Priority = 10;
        playerCapsule.SetActive(false);
        // if (dialogUI != null)
        // {//enable telescope UI
        //     dialogUI.SetActive(true);
        // }
        // if (mainUI != null)
        // {//enable telescope UI
        //     mainUI.SetActive(false);
        // }
    }
    // void StopTalking()
    // {
    //     if (promptUI != null) promptUI.SetActive(false);

    //     // StopCoroutine(myEnumerator);
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", false);
    //     isTalking = false;
    //     FindFirstObjectByType<DialogueManager>().EndDialogue();

    //     //switch camera to telescope camera
    //     playerCapsule.SetActive(true);

    //     cutsceneCam.Priority = 10;
    //     mainCam.Priority = 20;

    //     // if (dialogUI != null)
    //     // {//enable telescope UI
    //     //     dialogUI.SetActive(false);
    //     // }
    //     // if (mainUI != null)
    //     // {//enable telescope UI
    //     //     mainUI.SetActive(true);
    //     // }
    // }
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

    // IEnumerator Nod()
    // {
    //     //nod head
    //     anim.SetBool("yes", true);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", false);
    //     yield return null;


    // }
    // IEnumerator ShakeHead()
    // {
    //     //shake head
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", true);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", false);
    //     yield return null;


    // }
    // IEnumerator ThankBow()
    // {
    //     //thanking bow
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", true);
    //     anim.SetBool("celebrate", false);
    //     yield return null;
    // }
    // IEnumerator Celebrate()
    // {
    //     //celebration
    //     anim.SetBool("yes", false);
    //     anim.SetBool("no", false);
    //     anim.SetBool("thankyou", false);
    //     anim.SetBool("celebrate", true);
    //     yield return null;

    // }


}
