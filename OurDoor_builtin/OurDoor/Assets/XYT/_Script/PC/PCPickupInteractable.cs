using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// PC 可拾取物适配器。
/// 鼠标左键按住拾取，滚轮调整距离，松开左键放下。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PCPickupInteractable : MonoBehaviour, IPCInteractable
{
    [Header("引用")]
    private Rigidbody targetRigidbody;

    [Tooltip("持有时用于定位的点。留空时使用刚体质心。")]
    private Transform holdPoint;

    [Header("持有距离")]
    [Min(0.1f)] [SerializeField] private float minimumHoldDistance = 0.6f;
    [Min(0.1f)] [SerializeField] private float maximumHoldDistance = 4f;
    [Min(0.01f)] [SerializeField] private float scrollDistanceStep = 0.25f;

    [Header("位置跟随")]
    [Min(0f)] [SerializeField] private float positionForce = 80f;
    [Min(0f)] [SerializeField] private float positionDamping = 14f;
    [Min(0f)] [SerializeField] private float maximumAcceleration = 150f;

    [Header("朝向摄像机")]
    [Tooltip("开启后，物体的本地正面会朝向摄像机。")]
    [SerializeField] private bool faceCamera = true;

    [Tooltip("模型正面方向不一致时，用此欧拉角进行修正。")]
    [SerializeField] private Vector3 facingRotationOffset;

    [Min(0f)] [SerializeField] private float rotationFollowSpeed = 12f;

    [Header("拾取状态")]
    [SerializeField] private bool disableGravityWhileHeld = true;
    [SerializeField] private bool ignorePlayerCollisionWhileHeld = true;
    private string interactionPrompt = "拿起";

    [Header("事件")]
    [SerializeField] public UnityEvent onPickedUp;
    [SerializeField] public UnityEvent onDropped;

    [Header("运行时状态")]
    [SerializeField] private bool isHeld;
    [SerializeField] private float currentHoldDistance;

    private Camera holdingCamera;
    private CharacterController playerController;
    private Collider[] objectColliders;

    private bool originalUseGravity;
    private float originalDrag;
    private float originalAngularDrag;
    private RigidbodyInterpolation originalInterpolation;
    private CollisionDetectionMode originalCollisionDetection;

    public string InteractionPrompt => interactionPrompt;
    public bool IsHeld => isHeld;
    public float CurrentHoldDistance => currentHoldDistance;



    private void Awake()
    {
        targetRigidbody = GetComponent<Rigidbody>();
        holdPoint = transform;

        objectColliders = GetComponentsInChildren<Collider>(true);
    }

    public bool CanInteract(PCInteractor interactor)
    {
        return isActiveAndEnabled &&
               targetRigidbody != null &&
               !targetRigidbody.isKinematic &&
               !isHeld;
    }

    public void Interact(PCInteractor interactor)
    {
        // 由 PCInteractor.WasInteractPressed() 在左键按下瞬间调用。
        if (!CanInteract(interactor))
            return;

        holdingCamera = interactor != null
            ? interactor.InteractionCamera
            : Camera.main;

        if (holdingCamera == null)
            return;

        playerController = interactor != null
            ? interactor.GetComponentInParent<CharacterController>()
            : null;

        Vector3 pickupPoint = GetPickupPoint();
        float forwardDistance = Vector3.Dot(
            pickupPoint - holdingCamera.transform.position,
            holdingCamera.transform.forward
        );

        currentHoldDistance = Mathf.Clamp(
            forwardDistance,
            minimumHoldDistance,
            maximumHoldDistance
        );

        SaveRigidbodyState();

        if (disableGravityWhileHeld)
            targetRigidbody.useGravity = false;

        targetRigidbody.drag = Mathf.Max(targetRigidbody.drag, 1f);
        targetRigidbody.angularDrag = Mathf.Max(targetRigidbody.angularDrag, 8f);
        targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        targetRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        targetRigidbody.WakeUp();

        SetPlayerCollisionIgnored(true);
        isHeld = true;
        onPickedUp?.Invoke();
    }

    private void Update()
    {
        if (!isHeld)
            return;

        if (!IsInteractHeld() || holdingCamera == null)
        {
            Drop();
            return;
        }

        if (Mouse.current == null)
            return;

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            currentHoldDistance +=
                Mathf.Sign(scrollY) * scrollDistanceStep;

            currentHoldDistance = Mathf.Clamp(
                currentHoldDistance,
                minimumHoldDistance,
                maximumHoldDistance
            );
        }
    }

    private void FixedUpdate()
    {
        if (!isHeld || holdingCamera == null)
            return;

        Vector3 pickupPoint = GetPickupPoint();
        Vector3 targetPoint =
            holdingCamera.transform.position +
            holdingCamera.transform.forward * currentHoldDistance;

        Vector3 offset = targetPoint - pickupPoint;
        Vector3 acceleration =
            offset * positionForce -
            targetRigidbody.velocity * positionDamping;

        acceleration = Vector3.ClampMagnitude(
            acceleration,
            maximumAcceleration
        );

        targetRigidbody.AddForceAtPosition(
            acceleration,
            pickupPoint,
            ForceMode.Acceleration
        );

        if (faceCamera)
            UpdateFacingRotation();
    }

    private void UpdateFacingRotation()
    {
        // 让物体本地 +Z 正面朝向摄像机。
        Quaternion faceRotation = Quaternion.LookRotation(
            -holdingCamera.transform.forward,
            holdingCamera.transform.up
        );

        Quaternion targetRotation =
            faceRotation * Quaternion.Euler(facingRotationOffset);

        Quaternion nextRotation = Quaternion.Slerp(
            targetRigidbody.rotation,
            targetRotation,
            rotationFollowSpeed * Time.fixedDeltaTime
        );

        targetRigidbody.MoveRotation(nextRotation);
        targetRigidbody.angularVelocity = Vector3.zero;
    }

    private Vector3 GetPickupPoint()
    {
        return holdPoint != null
            ? holdPoint.position
            : targetRigidbody.worldCenterOfMass;
    }

    private static bool IsInteractHeld()
    {
        return PCInteractor.WasInteractPressed();
    }

    private void SaveRigidbodyState()
    {
        originalUseGravity = targetRigidbody.useGravity;
        originalDrag = targetRigidbody.drag;
        originalAngularDrag = targetRigidbody.angularDrag;
        originalInterpolation = targetRigidbody.interpolation;
        originalCollisionDetection = targetRigidbody.collisionDetectionMode;
    }

    private void RestoreRigidbodyState()
    {
        if (targetRigidbody == null)
            return;

        targetRigidbody.useGravity = originalUseGravity;
        targetRigidbody.drag = originalDrag;
        targetRigidbody.angularDrag = originalAngularDrag;
        targetRigidbody.interpolation = originalInterpolation;
        targetRigidbody.collisionDetectionMode = originalCollisionDetection;
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (!ignorePlayerCollisionWhileHeld || playerController == null)
            return;

        foreach (Collider objectCollider in objectColliders)
        {
            if (objectCollider == null)
                continue;

            Physics.IgnoreCollision(
                objectCollider,
                playerController,
                ignored
            );
        }
    }

    public void Drop()
    {
        if (!isHeld)
            return;

        SetPlayerCollisionIgnored(false);
        RestoreRigidbodyState();

        isHeld = false;
        holdingCamera = null;
        playerController = null;

        onDropped?.Invoke();
    }

    private void OnDisable()
    {
        Drop();
    }
}
