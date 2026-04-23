using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRInteract_TVButton : MonoBehaviour
{
    [Header("Button Settings")]
    public bool isTurnOnButton = true;

    [SerializeField] private AudioClip pressSound;  // 可选：按下音效
    [SerializeField] private float hapticStrength = 0.5f;  // 可选：震动强度

    TVScreen tvScreen;

    private XRSimpleInteractable interactable;
    private AudioSource audioSource;

    void Awake()
    {
        // 获取组件
        interactable = GetComponent<XRSimpleInteractable>();
        tvScreen = FindObjectOfType<TVScreen>();

        // 如果没有XRSimpleInteractable，尝试获取XRBaseInteractable
        if (interactable == null)
        {
            Debug.LogError($"物体 {gameObject.name} 上没有 XRSimpleInteractable 组件！");
            return;
        }

        // 添加音频源（如果需要音效）
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && pressSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            // 注册按下事件（selectEntered 就是"被按下的瞬间"）
            interactable.selectEntered.AddListener(OnButtonPressed);
            // 可选：注册释放事件
            interactable.selectExited.AddListener(OnButtonReleased);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnButtonPressed);
            interactable.selectExited.RemoveListener(OnButtonReleased);
        }
    }

    // 按钮被按下时触发
    private void OnButtonPressed(SelectEnterEventArgs args)
    {
        Debug.Log($"按钮 {gameObject.name} 被按下了！");

        // 获取按下者的信息（哪个手柄按的）
        var interactor = args.interactorObject;
        Debug.Log($"按下者: {interactor.transform.name}");

        // 触觉反馈
        if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
        {
            if (controllerInteractor.xrController != null)
            {
                controllerInteractor.xrController.SendHapticImpulse(hapticStrength, 0.05f);
            }
        }

        // 播放音效
        if (audioSource != null && pressSound != null)
        {
            audioSource.PlayOneShot(pressSound);
        }

        // 👇 在这里调用你想要的函数
        OnTVButtonPressed();
    }

    private void OnButtonReleased(SelectExitEventArgs args)
    {
        Debug.Log($"按钮 {gameObject.name} 被释放了");
        // 可选：释放时的逻辑
    }

    // 自定义函数：按钮按下后要执行的逻辑
    private void OnTVButtonPressed()
    {
        // TODO: 在这里写你的逻辑
        // 例如：切换电视开关、换台、调音量等

        if (!Level1Manager.Instance.PowerOn)
        {
            Debug.Log("断电中，无法按电视");
            return;
        }

        Debug.Log("🎬 TV按钮被按下，执行相应功能");

        if (tvScreen == null) return;
        if (isTurnOnButton)
        {
            if (tvScreen.IsTVOn)
            {
                tvScreen.NextPicture();
            }
            else
            {
                tvScreen.TurnOnTV();
            }
        }
        else
        {
            // turn off button
            if(tvScreen.IsTVOn)
            {
                tvScreen.TurnOffTV();
            }
        }
        // 示例：切换电视开关
        // TVController.Instance.TogglePower();

        // 示例：播放动画
        // GetComponent<Animator>()?.SetTrigger("Press");

        // 示例：发送事件
        // EventManager.TriggerEvent("TVButtonPressed");
    }
}