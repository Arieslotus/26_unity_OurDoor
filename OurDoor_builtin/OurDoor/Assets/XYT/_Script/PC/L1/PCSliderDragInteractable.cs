using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PC 物理滑块拖拽。PCInteractor 负责开始抓取，
/// 持续按住鼠标左键时，抓取目标会完整跟随摄像机的位置与旋转。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PCSliderDragInteractable : MonoBehaviour, IPCInteractable
{
    [Header("引用")]
    private Rigidbody targetRigidbody;
    private Transform grabPoint;

    [Header("拖拽物理")]
    [Min(0f)] [SerializeField] private float pullForce = 60f;
    [Min(0f)] [SerializeField] private float damping = 10f;
    [Min(0.1f)] [SerializeField] private float breakDistance = 3f;
    [Min(0f)] [SerializeField] private float maximumAcceleration = 120f;

    [Header("提示")]
     private string interactionPrompt = "按住拖动";

    [Header("运行时状态")]
    [SerializeField] private bool isDragging;

    private Camera draggingCamera;
    private Vector3 cameraLocalTargetPoint;

    public string InteractionPrompt => interactionPrompt;
    public bool IsDragging => isDragging;

    private void Awake()
    {
            targetRigidbody = GetComponent<Rigidbody>();
        grabPoint = transform;
    }

    public bool CanInteract(PCInteractor interactor)
    {
        return isActiveAndEnabled &&
               targetRigidbody != null &&
               !targetRigidbody.isKinematic &&
               !isDragging;
    }

    public void Interact(PCInteractor interactor)
    {
        // 本方法由 PCInteractor.WasInteractPressed() 在按下瞬间调用。
        if (!CanInteract(interactor))
            return;

        draggingCamera = interactor != null
            ? interactor.InteractionCamera
            : Camera.main;

        if (draggingCamera == null)
            return;

        Vector3 forcePoint = GetForcePoint();

        // 记录抓取瞬间的摄像机局部坐标。
        // 摄像机旋转时 TransformPoint 会旋转这个目标，从而产生对应拉力。
        cameraLocalTargetPoint =
            draggingCamera.transform.InverseTransformPoint(forcePoint);

        isDragging = true;
        targetRigidbody.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!isDragging)
            return;

        if (!IsInteractHeld() || draggingCamera == null)
        {
            StopDragging();
            return;
        }

        Vector3 forcePoint = GetForcePoint();
        Vector3 targetPoint =
            draggingCamera.transform.TransformPoint(cameraLocalTargetPoint);
        Vector3 offset = targetPoint - forcePoint;

        if (offset.magnitude > breakDistance)
        {
            StopDragging();
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

    public void StopDragging()
    {
        isDragging = false;
        draggingCamera = null;
    }

    private void OnDisable()
    {
        StopDragging();
    }
}
