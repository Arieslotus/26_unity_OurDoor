using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PC 铰链门物理拖拽。摄像机上下左右旋转或移动时，
/// 抓取目标会完整跟随摄像机变换并对门产生相应方向的拉力。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PCHingeDoorPullInteractable : MonoBehaviour, IPCInteractable
{
    [Header("引用")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Transform grabPoint;

    [Header("拖拽物理")]
    [Min(0f)] [SerializeField] private float pullForce = 45f;
    [Min(0f)] [SerializeField] private float damping = 8f;
    [Min(0.1f)] [SerializeField] private float breakDistance = 3f;
    [Min(0f)] [SerializeField] private float maximumAcceleration = 100f;

    [Header("提示")]
    private string interactionPrompt = "按住拉动";

    [Header("运行时状态")]
    [SerializeField] private bool isPulling;

    private Camera pullingCamera;
    private Vector3 cameraLocalTargetPoint;

    public string InteractionPrompt => interactionPrompt;
    public bool IsPulling => isPulling;

    private void Awake()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();
    }

    public bool CanInteract(PCInteractor interactor)
    {
        return isActiveAndEnabled &&
               targetRigidbody != null &&
               !targetRigidbody.isKinematic &&
               !isPulling;
    }

    public void Interact(PCInteractor interactor)
    {
        // 本方法由 PCInteractor.WasInteractPressed() 在按下瞬间调用。
        if (!CanInteract(interactor))
            return;

        pullingCamera = interactor != null
            ? interactor.InteractionCamera
            : Camera.main;

        if (pullingCamera == null)
            return;

        Vector3 forcePoint = GetForcePoint();
        cameraLocalTargetPoint =
            pullingCamera.transform.InverseTransformPoint(forcePoint);

        isPulling = true;
        targetRigidbody.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!isPulling)
            return;

        if (!IsInteractHeld() || pullingCamera == null)
        {
            StopPulling();
            return;
        }

        Vector3 forcePoint = GetForcePoint();
        Vector3 targetPoint =
            pullingCamera.transform.TransformPoint(cameraLocalTargetPoint);
        Vector3 offset = targetPoint - forcePoint;

        if (offset.magnitude > breakDistance)
        {
            StopPulling();
            return;
        }

        Vector3 acceleration =
            offset * pullForce -
            targetRigidbody.GetPointVelocity(forcePoint) * damping;

        acceleration = Vector3.ClampMagnitude(
            acceleration,
            maximumAcceleration
        );

        targetRigidbody.AddForceAtPosition(
            acceleration,
            forcePoint,
            ForceMode.Acceleration
        );
    }

    private Vector3 GetForcePoint()
    {
        return grabPoint != null
            ? grabPoint.position
            : targetRigidbody.worldCenterOfMass;
    }

    private static bool IsInteractHeld()
    {
        // 与当前 PCInteractor.WasInteractPressed() 使用同一个鼠标左键。
        return PCInteractor.WasInteractPressed();
    }

    public void StopPulling()
    {
        isPulling = false;
        pullingCamera = null;
    }

    private void OnDisable()
    {
        StopPulling();
    }
}
