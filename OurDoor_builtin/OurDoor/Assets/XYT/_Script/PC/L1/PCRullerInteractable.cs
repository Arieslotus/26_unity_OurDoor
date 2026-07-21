using UnityEngine;

/// <summary>
/// PC 数字锁滚轮适配器。
/// 挂到每个 Ruller 上，通过 PCInteractor 的 E 键调用。
/// </summary>
public class PCRullerInteractable : MonoBehaviour, IPCInteractable
{
    [Header("数字锁")]
    [SerializeField] private int rullerIndex;
    private MoveRuller moveRuller;
    [SerializeField] private float anglePerStep = -36f;

    [Header("提示与反馈")]
    private string interactionPrompt = "点击转动";
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioSource audioSource;

    public string InteractionPrompt => interactionPrompt;

    private void Awake()
    {

        if (moveRuller == null)
            moveRuller = GetComponentInParent<MoveRuller>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public bool CanInteract(PCInteractor interactor)
    {
        if (!isActiveAndEnabled || moveRuller == null)
            return false;

        return Level1Manager.Instance == null ||
               !Level1Manager.Instance.LockOpened;
    }

    public void Interact(PCInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (rullerIndex < 0 ||
            rullerIndex >= moveRuller._numberArray.Length)
        {
            Debug.LogError(
                $"{nameof(PCRullerInteractable)}：滚轮索引 {rullerIndex} 无效。",
                this
            );
            return;
        }

        moveRuller.UpdateNumber(rullerIndex, 1);
        transform.Rotate(anglePerStep, 0f, 0f, Space.Self);

        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }
}
