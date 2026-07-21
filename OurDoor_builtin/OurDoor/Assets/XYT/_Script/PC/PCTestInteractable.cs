using UnityEngine;

/// <summary>
/// 用于验证 PC 射线交互系统的临时测试组件。
/// 确认系统正常后，可以从测试物体上移除。
/// </summary>
public class PCTestInteractable : MonoBehaviour, IPCInteractable
{
    [SerializeField] private string interactionPrompt = "按 E 测试交互";
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool toggleActiveState;
    [SerializeField] private GameObject targetObject;

    public string InteractionPrompt => interactionPrompt;

    public bool CanInteract(PCInteractor interactor)
    {
        return canInteract && isActiveAndEnabled;
    }

    public void Interact(PCInteractor interactor)
    {
        Debug.Log($"PC 交互成功：{gameObject.name}", this);

        if (toggleActiveState && targetObject != null)
            targetObject.SetActive(!targetObject.activeSelf);
    }
}
