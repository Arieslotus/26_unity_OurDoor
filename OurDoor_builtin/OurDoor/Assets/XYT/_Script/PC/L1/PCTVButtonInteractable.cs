using UnityEngine;

/// <summary>
/// PC 电视按钮交互适配器。
/// 开机按钮：电视关闭时开机，已开启时切换下一画面。
/// 关机按钮：关闭电视。
/// </summary>
public class PCTVButtonInteractable : MonoBehaviour, IPCInteractable
{
    [Header("按钮类型")]
    [Tooltip("勾选：开机/切换画面按钮；不勾选：关机按钮。")]
    [SerializeField] private bool isTurnOnButton = true;

    [Header("引用")]
    [SerializeField] private TVScreen tvScreen;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressSound;

    [Header("提示")]
    [SerializeField] private string turnOnPrompt = "点击开机 / 切换画面";
    [SerializeField] private string turnOffPrompt = "点击关闭电视";

    public string InteractionPrompt =>
        isTurnOnButton ? turnOnPrompt : turnOffPrompt;

    private void Awake()
    {
        if (tvScreen == null)
            tvScreen = FindObjectOfType<TVScreen>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public bool CanInteract(PCInteractor interactor)
    {
        return isActiveAndEnabled && tvScreen != null;
    }

    public void Interact(PCInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (Level1Manager.Instance == null ||
            !Level1Manager.Instance.PowerOn)
        {
            Debug.Log("断电中，无法使用电视。", this);
            return;
        }

        if (audioSource != null && pressSound != null)
            audioSource.PlayOneShot(pressSound);

        if (isTurnOnButton)
        {
            if (tvScreen.IsTVOn)
                tvScreen.NextPicture();
            else
                tvScreen.TurnOnTV();

            return;
        }

        if (tvScreen.IsTVOn)
            tvScreen.TurnOffTV();
    }
}
