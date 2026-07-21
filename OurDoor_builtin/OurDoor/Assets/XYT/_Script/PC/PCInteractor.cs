
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// PC 统一交互器：优先检测屏幕中心的 UI Button，
/// 没有 UI 时再检测摄像机前方的 3D 可交互物体。
/// </summary>
public class PCInteractor : MonoBehaviour
{
    public enum InteractionTargetType
    {
        None,
        UI,
        WorldObject
    }

    [Header("引用")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private PCFirstPersonController playerController;
    [SerializeField] private PCInteractionPrompt interactionPrompt;

    [Header("3D 物体射线")]
    [Min(0.1f)]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Ignore;

    [Header("UI 物理射线")]
    [SerializeField] private LayerMask uiInteractionLayers;
    [SerializeField]
    private QueryTriggerInteraction uiTriggerInteraction =
        QueryTriggerInteraction.Collide;

    [Header("UI")]
    [SerializeField] private string uiButtonPrompt = "按 E 确认";

    [Header("调试")]
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private bool logInteractions;

    [Header("运行时状态（在 Inspector 中观察）")]
    [SerializeField] private bool canInteract;
    [SerializeField] private InteractionTargetType currentTargetType;
    [SerializeField] private GameObject currentTargetObject;

    [SerializeField] private Button currentButton;
    private IPCInteractable currentWorldInteractable;

    /// <summary>当前准星下是否存在可交互 UI 或物体。</summary>
    public bool CanInteract => canInteract;

    /// <summary>当前目标种类：无、UI、3D 物体。</summary>
    public InteractionTargetType CurrentTargetType => currentTargetType;

    /// <summary>当前被准星指向的 GameObject。</summary>
    public GameObject CurrentTargetObject => currentTargetObject;

    public Button CurrentUIButton => currentButton;
    public IPCInteractable CurrentWorldInteractable => currentWorldInteractable;
    public Camera InteractionCamera => interactionCamera;

    private void Awake()
    {
        if (interactionCamera == null)
            interactionCamera = GetComponent<Camera>();

        if (interactionCamera == null)
            interactionCamera = GetComponentInParent<Camera>();

        if (playerController == null)
            playerController = GetComponentInParent<PCFirstPersonController>();

        if (interactionCamera == null)
        {
            Debug.LogError(
                $"{nameof(PCInteractor)}：没有找到交互摄像机。",
                this
            );
        }
    }

    private void Update()
    {
        if (!CanProcessInput())
        {
            ClearTarget();
            return;
        }

        // UI 优先，避免穿透 UI 点击后面的 3D 物体。
        if (TryFindUIButton(out Button button))
        {
            SetUITarget(button);

            if (WasInteractPressed())
                ClickUIButton(button);

            return;
        }

        if (TryFindWorldInteractable(
                out IPCInteractable interactable,
                out GameObject targetObject))
        {
            SetWorldTarget(interactable, targetObject);

            if (WasInteractPressedThisFrame() &&
                interactable.CanInteract(this))
            {
                if (logInteractions)
                    Debug.Log($"PC 物体交互：{targetObject.name}", targetObject);

                interactable.Interact(this);
            }

            return;
        }

        ClearTarget();
    }

    private bool CanProcessInput()
    {
        if (interactionCamera == null ||
            !interactionCamera.isActiveAndEnabled)
        {
            return false;
        }

        if (playerController == null)
            return Cursor.lockState == CursorLockMode.Locked;

        return playerController.IsControlEnabled &&
               playerController.IsCursorLocked;
    }

    //private static bool WasInteractPressed()
    //{
    //    return Keyboard.current != null &&
    //           Keyboard.current.eKey.wasPressedThisFrame;
    //}
    public static bool WasInteractPressedThisFrame()
    {
        return Mouse.current != null &&
               Mouse.current.leftButton.wasPressedThisFrame;
    }
    public static bool WasInteractPressed()
    {
        return Mouse.current != null &&
               Mouse.current.leftButton.isPressed;
    }

    private bool TryFindUIButton(out Button button)
    {
        button = null;

        if (interactionCamera == null)
            return false;

        Ray ray = new Ray(
            interactionCamera.transform.position,
            interactionCamera.transform.forward
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                uiInteractionLayers,
                uiTriggerInteraction))
        {
            return false;
        }

        // Collider 应该挂在 Button 本体或 Button 的子物体上。
        Button candidate =
            hit.collider.GetComponent<Button>(); //InParent

        if (candidate == null)
            return false;

        if (!candidate.isActiveAndEnabled)
            return false;

        if (!candidate.IsInteractable())
            return false;

        button = candidate;
        return true;

        //if (Physics.Raycast(
        //ray,
        //out RaycastHit hit,
        //interactionDistance,
        //uiInteractionLayers,
        //uiTriggerInteraction))
        //{
        //    Debug.Log(
        //        $"UI 射线命中：{hit.collider.name}",
        //        hit.collider
        //    );

        //    Button candidate =
        //        hit.collider.GetComponentInParent<Button>();

        //    if (candidate == null)
        //    {
        //        Debug.LogWarning(
        //            $"命中了 {hit.collider.name}，但向父级找不到 Button",
        //            hit.collider
        //        );

        //        return false;
        //    }

        //    Debug.Log($"找到按钮：{candidate.name}", candidate);

        //    if (!candidate.isActiveAndEnabled ||
        //        !candidate.IsInteractable())
        //    {
        //        Debug.LogWarning(
        //            $"按钮 {candidate.name} 当前不可交互",
        //            candidate
        //        );

        //        return false;
        //    }

        //    button = candidate;
        //    return true;
        //}

        //Debug.Log("UI 射线没有命中任何 Collider");
        //return false;
    }

    private bool TryFindWorldInteractable(
    out IPCInteractable interactable,
    out GameObject targetObject)
    {
        interactable = null;
        targetObject = null;

        Ray ray = new Ray(
            interactionCamera.transform.position,
            interactionCamera.transform.forward
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactionDistance,
            interactionLayers,
            triggerInteraction
        );

        if (hits.Length == 0)
            return false;

        // 按距离排序
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        int count = 2;

        foreach (RaycastHit hit in hits)
        {
            if(count == 0) continue;

            IPCInteractable found = FindInteractable(hit.collider.transform);
            if (found != null && found.CanInteract(this))
            {
                interactable = found;
                targetObject = hit.collider.gameObject;
                return true;
            }
            count--;
        }

        return false;
    }
    private static IPCInteractable FindInteractable(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            MonoBehaviour[] behaviours =
                current.GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPCInteractable interactable)
                    return interactable;
            }

            current = current.parent;
        }

        return null;
    }

    private void SetUITarget(Button button)
    {
        currentButton = button;
        currentWorldInteractable = null;
        canInteract = true;
        currentTargetType = InteractionTargetType.UI;
        currentTargetObject = button.gameObject;
        interactionPrompt?.ShowPrompt(uiButtonPrompt);
    }

    private void SetWorldTarget(
        IPCInteractable interactable,
        GameObject targetObject)
    {
        currentButton = null;
        currentWorldInteractable = interactable;
        canInteract = true;
        currentTargetType = InteractionTargetType.WorldObject;
        currentTargetObject = targetObject;
        //Debug.Log($"interactionPrompt 的值是：{interactable.InteractionPrompt}");
        interactionPrompt?.ShowPrompt(interactable.InteractionPrompt);
    }

    private void ClickUIButton(Button button)
    {
        if (logInteractions)
            Debug.Log($"PC UI 点击：{button.gameObject.name}", button);

        // 直接调用标准 Button 的 onClick，保留项目原有事件配置。
        button.onClick.Invoke();
    }

    private void ClearTarget()
    {
        currentButton = null;
        currentWorldInteractable = null;
        canInteract = false;
        currentTargetType = InteractionTargetType.None;
        currentTargetObject = null;
        interactionPrompt?.HidePrompt();
    }

    private void DrawRuntimeRay()
    {
        if (!drawDebugRay || interactionCamera == null)
            return;

        Debug.DrawRay(
            interactionCamera.transform.position,
            interactionCamera.transform.forward * interactionDistance,
            canInteract ? Color.green : Color.cyan
        );
    }

    private void OnDrawGizmos()
    {
        DrawRuntimeRay();
    }

    private void OnDisable()
    {
        ClearTarget();
    }
}
