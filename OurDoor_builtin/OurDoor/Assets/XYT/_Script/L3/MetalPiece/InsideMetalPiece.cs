using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

//[RequireComponent(typeof(XRGrabInteractable))]
public class InsideMetalPiece : MonoBehaviour
{
    private XRGrabInteractable grab;
    private PCPickupInteractable pick;

    private bool received = false;
    public bool isPicking { get; private set; }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        pick = GetComponent<PCPickupInteractable>();

        pick?.onPickedUp.AddListener(OnGrab);
        grab?.selectEntered.AddListener(OnGrab);
    }

    private void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrab);
        pick?.onPickedUp.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        OnGrab();
    }

    private void OnGrab()
    {
        isPicking = true;
        if (!received)
        {
            received = true;

            Debug.Log("√≈ƒ⁄ƒ√µΩÃ˙∆¨");

            L3Manager.Instance.SetMetalPieceReceived();
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        isPicking = false;
    }
}