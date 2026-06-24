using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class DoorKnocker : MonoBehaviour
{
    [Header("0=×ó 1=ÓÒ")]
    public int knockerID = 0;

    [Header("ÇÃ»÷ÀäÈ´")]
    public float knockCD = 0.5f;

    private XRGrabInteractable grab;

    private bool isGrabbed = false;
    private float lastKnockTime = -999f;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
    }

    private void OnRelease(SelectExitEventArgs args)
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