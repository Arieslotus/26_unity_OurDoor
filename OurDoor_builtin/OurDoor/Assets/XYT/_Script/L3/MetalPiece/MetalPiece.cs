using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 门外的铁片
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class MetalPiece : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;

    private bool hasPickedUp = false;
    public bool isPicking { get; private set; }

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
    private void OnGrab(SelectEnterEventArgs args)
    {
        if (L3Manager.Instance.PasswordSuccess)
        {
            isPicking = true;

            if (hasPickedUp)
                return;

            hasPickedUp = true;

            Debug.Log("拾取到铁片");

            L3Manager.Instance.SetMetalPieceFound();
        }

    }

    private void OnRelease(SelectExitEventArgs args)
    {
        isPicking = false;
    }
}