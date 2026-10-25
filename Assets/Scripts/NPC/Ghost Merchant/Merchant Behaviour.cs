using Unity.Cinemachine;
using UnityEngine;
using System.Collections;

public class MerchantBehaviour : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator anim;
    [Header("Toggle Talking")]
    [SerializeField] private bool isTalking = false;
    [Header("Player Raycasting")]
    [SerializeField] private Transform playerCameraTransform;
    public float pickupDistance = 5.0f;
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera mainCam;
    [SerializeField] private CinemachineCamera cutsceneCam;
    private IEnumerator myEnumerator;
    // [Header("UI Overlay")]
    // [SerializeField] private GameObject dialogUI;
    // [SerializeField] private GameObject mainUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        myEnumerator = MySequence();

    }

    // Update is called once per frame
    void Update()
    {
        bool cast = Physics.Raycast(playerCameraTransform.position, playerCameraTransform.forward, out RaycastHit raycasthit, pickupDistance);
        if (cast && !isTalking)
        {
            // Debug.Log("raycast hit " + isTalking);

            if (raycasthit.collider.CompareTag("merchant") && Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("talking mode " + isTalking);
                Talk();
                Debug.Log("after enable talking " + isTalking);

            }

        }
        else if (isTalking && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("talking mode " + isTalking);
            StopTalking();
            Debug.Log("after disable talking " + isTalking);
        }

    }
    void Talk()
    {
        StartCoroutine(myEnumerator);
        isTalking = true;
        //switch camera to telescope camera
        cutsceneCam.Priority = 20;
        mainCam.Priority = 10;
        // if (dialogUI != null)
        // {//enable telescope UI
        //     dialogUI.SetActive(true);
        // }
        // if (mainUI != null)
        // {//enable telescope UI
        //     mainUI.SetActive(false);
        // }
    }
    void StopTalking()
    {
        StopCoroutine(myEnumerator);
        anim.SetBool("yes", false);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", false);
        isTalking = false;
        //switch camera to telescope camera
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
    IEnumerator MySequence()
    {
        yield return new WaitForSeconds(4);
        //nod head
        anim.SetBool("yes", true);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", false);
        //shake head
        yield return new WaitForSeconds(4);
        anim.SetBool("yes", false);
        anim.SetBool("no", true);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", false);
        //thanking bow
        yield return new WaitForSeconds(4);
        anim.SetBool("yes", false);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", true);
        anim.SetBool("celebrate", false);
        //celebration
        yield return new WaitForSeconds(4);
        anim.SetBool("yes", false);
        anim.SetBool("no", false);
        anim.SetBool("thankyou", false);
        anim.SetBool("celebrate", true);

    }
}
