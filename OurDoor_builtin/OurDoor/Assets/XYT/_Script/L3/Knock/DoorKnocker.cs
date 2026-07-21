using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

//[RequireComponent(typeof(XRGrabInteractable))]
public class DoorKnocker : MonoBehaviour
{
    [Header("0=×ó 1=ÓÒ")]
    public int knockerID = 0;

    [Header("ÇÃ»÷ÀäÈ´")]
    public float knockCD = 0.5f;

    private XRGrabInteractable grab;
    private PCHingeDoorPullInteractable pick;

    private bool isGrabbed = false;
    private float lastKnockTime = -999f;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        pick = GetComponent<PCHingeDoorPullInteractable>();

        grab?.selectEntered.AddListener(OnGrab);
        grab?.selectExited.AddListener(OnRelease);
        pick?.onPickedUp.AddListener(OnGrab);
        pick?.onDropped.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
        pick?.onDropped.RemoveListener(OnRelease);
        pick?.onPickedUp.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {

    }

    void OnGrab()
    {
        isGrabbed = true;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        OnRelease();
    }
    private void OnRelease()
    {
        if (!isGrabbed)
            return;

        isGrabbed = false;

        if (Time.time - lastKnockTime < knockCD)
            return;

        lastKnockTime = Time.time;

        DoorKnockManager.Instance.OnKnock(knockerID);
    }
}