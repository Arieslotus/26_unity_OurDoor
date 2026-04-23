using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class VRInteract_Ruller : MonoBehaviour
{
    public int rullerIndex;
    public MoveRuller moveRuller;

    public AudioClip clickSound;
    public float hapticAmplitude = 0.5f;
    public float hapticDuration = 0.05f;

    private XRGrabInteractable interactable;
    private AudioSource audioSource;

    void Awake()
    {
        interactable = GetComponent<XRGrabInteractable>();

        // ❗关键：禁用拖拽移动（变成按钮）
        interactable.trackPosition = false;
        interactable.trackRotation = false;

        interactable.selectEntered.AddListener(OnClick);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(OnClick);
    }

    void OnClick(SelectEnterEventArgs args)
    {
        if(Level1Manager.Instance.LockOpened)
        {
            Debug.Log("锁已开，无法点击");
            return;
        }

        // 👉 数字 +1
        if (moveRuller != null)
        {
            moveRuller.UpdateNumber(rullerIndex, 1);
        }

        // 👉 旋转到对应角度（卡点）
        RotateVisual();

        // 👉 反馈
        ProvideFeedback(args);
    }

    void RotateVisual()
    {
        int num = moveRuller._numberArray[rullerIndex];

        float anglePerStep = - 36f;
        float angle = num * anglePerStep;

        //transform.localRotation = Quaternion.Euler(angle, 0, 0);

        transform.Rotate( anglePerStep, 0, 0, Space.Self);

    }

    void ProvideFeedback(SelectEnterEventArgs args)
    {
        // 震动
        var interactor = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (interactor != null)
        {
            interactor.SendHapticImpulse(hapticAmplitude, hapticDuration);
        }

        // 音效
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }
}