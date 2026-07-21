using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

//[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]

// L2中校门外可被搭建的物体（纸箱，桌椅）
public class Box : MonoBehaviour
{
    [Header("Manager")]
    BoxManager boxManager;

    [Header("Build")]
    public int boxID = -1; // 用于对应搭建是否正确，非搭建物品是-1

    [Header("Trail")]
    public GameObject trailObject;

    [Header("Throw Detection")]
    public bool canThrow = true;
    public float throwSpeedThreshold = 2f;

    private XRGrabInteractable grab;
    private PCPickupInteractable pick;
    private Rigidbody rb;

    private bool isGrabbed;

    void Awake()
    {
        boxManager = FindObjectOfType<BoxManager>();
        grab = GetComponent<XRGrabInteractable>();
        pick = GetComponent<PCPickupInteractable>();
        rb = GetComponent<Rigidbody>();

        grab?.selectEntered.AddListener(OnGrab);
        grab?.selectExited.AddListener(OnRelease);
        pick?.onPickedUp.AddListener(OnGrab);
        pick?.onDropped.AddListener(OnRelease);


        if (trailObject != null)
            trailObject.SetActive(false);
    }

    void OnDestroy()
    {
        grab?.selectEntered.RemoveListener(OnGrab);
        grab?.selectExited.RemoveListener(OnRelease);
        pick?.onPickedUp.RemoveListener(OnGrab);
        pick?.onDropped.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        OnGrab();
    }
    void OnGrab()
    {
        isGrabbed = true;

        if (boxManager != null)
            boxManager.BeginGrab();

        if (trailObject != null)
            trailObject.SetActive(false);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        OnRelease();
    }
    void OnRelease()
    {
        isGrabbed = false;

        if (boxManager != null)
            boxManager.EndGrab();
    }

    void Update()
    {
        if(canThrow)
            CheckThrowState();
    }

    void CheckThrowState()
    {
        if (trailObject == null)
            return;

        // 正在抓着
        if (isGrabbed)
        {
            trailObject.SetActive(false);
            return;
        }

        // 速度太小
        if (rb.velocity.magnitude < throwSpeedThreshold)
        {
            trailObject.SetActive(false);
            return;
        }

        // 基本静止
        if (rb.IsSleeping())
        {
            trailObject.SetActive(false);
            return;
        }

        // 空中飞行
        if (!IsGrounded())
        {
            trailObject.SetActive(true);
        }
        else
        {
            trailObject.SetActive(false);
        }
    }

    bool IsGrounded()
    {
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            0.2f
        );
    }
}