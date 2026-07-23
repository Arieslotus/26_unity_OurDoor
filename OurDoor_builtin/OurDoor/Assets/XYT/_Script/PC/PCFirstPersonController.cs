using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PC 第一人称控制器。
/// 使用 Unity 新 Input System。
/// 仅供 PC 场景使用，不依赖 XR、PICO 或项目现有 InputManager。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PCFirstPersonController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家的第一人称摄像机")]
    [SerializeField] private Camera playerCamera;

    [Header("移动")]
    [Tooltip("普通移动速度")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("按住 Shift 时的移动速度")]
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("重力强度")]
    [SerializeField] private float gravity = -20f;

    [Header("下蹲")]
    [Tooltip("按住 Ctrl 时的移动速度")]
    [SerializeField] private float crouchSpeed = 2f;

    [Tooltip("下蹲后的 CharacterController 高度")]
    [SerializeField] private float crouchHeight = 1.2f;

    [Tooltip("下蹲后的摄像机局部高度")]
    [SerializeField] private float crouchCameraHeight = 1.05f;

    [Tooltip("站立与下蹲切换速度")]
    [SerializeField] private float crouchTransitionSpeed = 8f;

    [Header("鼠标视角")]
    [Tooltip("鼠标灵敏度。新 Input System 推荐从 0.08 开始")]
    [SerializeField] private float mouseSensitivity = 0.08f;

    [Tooltip("摄像机上下旋转限制")]
    [SerializeField] private float verticalLookLimit = 80f;

    [Header("鼠标滚轮缩放")]
    [SerializeField] private float minimumFieldOfView = 30f;
    [SerializeField] private float maximumFieldOfView = 80f;
    [SerializeField] private float zoomStep = 5f;
    [SerializeField] private float zoomSmoothSpeed = 12f;

    [Header("光标")]
    [Tooltip("游戏开始时锁定鼠标")]
    [SerializeField] private bool lockCursorOnStart = true;

    private CharacterController characterController;

    private float verticalVelocity;
    private float cameraPitch;
    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraLocalPosition;
    private float targetFieldOfView;
    private bool isCrouching;

    private readonly Collider[] standCheckResults = new Collider[16];

    private bool cursorLocked;
    private bool controlEnabled = true;
    private bool isTemporarilyReleasingCursor;
    private bool restoreCursorLockAfterTemporaryRelease;

    public bool IsCursorLocked => cursorLocked;
    public bool IsControlEnabled => controlEnabled;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                $"{nameof(PCFirstPersonController)}：没有找到玩家摄像机。",
                this
            );
        }
    }

    private void Start()
    {
        standingHeight = characterController.height;
        standingCenter = characterController.center;

        if (playerCamera != null)
        {
            standingCameraLocalPosition = playerCamera.transform.localPosition;
            targetFieldOfView = Mathf.Clamp(
                playerCamera.fieldOfView,
                minimumFieldOfView,
                maximumFieldOfView
            );
            playerCamera.fieldOfView = targetFieldOfView;
        }

        SetCursorLocked(lockCursorOnStart);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // 切出游戏窗口时释放鼠标。
        if (!hasFocus)
        {
            SetCursorLocked(false);
        }
    }

    private void Update()
    {
        HandleCursor();

        if (!controlEnabled || !cursorLocked)
            return;

        HandleCrouch();
        HandleLook();
        HandleZoom();
        HandleMovement();
    }

    private void HandleCrouch()
    {
        if (Keyboard.current == null || playerCamera == null)
            return;

        bool wantsToCrouch =
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;

        if (!wantsToCrouch && isCrouching && !CanStandUp())
            wantsToCrouch = true;

        isCrouching = wantsToCrouch;

        float targetHeight = isCrouching
            ? Mathf.Min(crouchHeight, standingHeight)
            : standingHeight;

        float standingBottom = standingCenter.y - standingHeight * 0.5f;
        Vector3 targetCenter = standingCenter;
        targetCenter.y = standingBottom + targetHeight * 0.5f;

        characterController.height = Mathf.MoveTowards(
            characterController.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        characterController.center = Vector3.MoveTowards(
            characterController.center,
            targetCenter,
            crouchTransitionSpeed * Time.deltaTime
        );

        Vector3 targetCameraPosition = standingCameraLocalPosition;
        targetCameraPosition.y = isCrouching
            ? crouchCameraHeight
            : standingCameraLocalPosition.y;

        playerCamera.transform.localPosition = Vector3.MoveTowards(
            playerCamera.transform.localPosition,
            targetCameraPosition,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    private bool CanStandUp()
    {
        float radius = characterController.radius * 0.95f;
        Vector3 worldCenter = transform.TransformPoint(standingCenter);
        float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
        Vector3 bottom = worldCenter - transform.up * halfSegment;
        Vector3 top = worldCenter + transform.up * halfSegment;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            standCheckResults,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = standCheckResults[i];

            if (hit == null || hit == characterController)
                continue;

            if (hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    private void HandleLook()
    {
        if (playerCamera == null)
            return;

        if (Mouse.current == null)
            return;

        // 新 Input System 的 delta 是当前帧鼠标移动的像素量。
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        // 左右旋转整个玩家。
        transform.Rotate(Vector3.up * mouseX);

        // 上下只旋转摄像机。
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -verticalLookLimit,
            verticalLookLimit
        );

        playerCamera.transform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        Vector2 moveInput = ReadMovementInput();

        Vector3 inputDirection =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        inputDirection = Vector3.ClampMagnitude(
            inputDirection,
            1f
        );

        bool isRunning =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        float currentSpeed;

        if (isCrouching)
            currentSpeed = crouchSpeed;
        else
            currentSpeed = isRunning ? runSpeed : moveSpeed;

        Vector3 horizontalMovement =
            inputDirection * currentSpeed;

        if (characterController.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMovement = horizontalMovement;
        finalMovement.y = verticalVelocity;

        characterController.Move(
            finalMovement * Time.deltaTime
        );
    }

    private void HandleZoom()
    {
        if (playerCamera == null || Mouse.current == null)
            return;

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            // 向上滚缩小 FOV（拉近），向下滚增大 FOV（拉远）。
            targetFieldOfView -= Mathf.Sign(scrollY) * zoomStep;
            targetFieldOfView = Mathf.Clamp(
                targetFieldOfView,
                minimumFieldOfView,
                maximumFieldOfView
            );
        }

        playerCamera.fieldOfView = Mathf.MoveTowards(
            playerCamera.fieldOfView,
            targetFieldOfView,
            zoomSmoothSpeed * Time.deltaTime
        );
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return input;

        if (keyboard.wKey.isPressed)
            input.y += 1f;

        if (keyboard.sKey.isPressed)
            input.y -= 1f;

        if (keyboard.dKey.isPressed)
            input.x += 1f;

        if (keyboard.aKey.isPressed)
            input.x -= 1f;

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void HandleCursor()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Esc 释放鼠标。
        if (keyboard != null &&
            keyboard.escapeKey.wasPressedThisFrame)
        {
            isTemporarilyReleasingCursor = false;
            restoreCursorLockAfterTemporaryRelease = false;
            SetCursorLocked(false);
            return;
        }

        bool isLeftAltPressed =
            keyboard != null &&
            keyboard.leftAltKey.isPressed;

        // 按住左 Alt 时临时释放鼠标，便于操作界面。
        if (isLeftAltPressed)
        {
            if (!isTemporarilyReleasingCursor)
            {
                isTemporarilyReleasingCursor = true;
                restoreCursorLockAfterTemporaryRelease = cursorLocked;
                SetCursorLocked(false);
            }

            return;
        }

        // 松开左 Alt 后恢复按下前的锁定状态。
        if (isTemporarilyReleasingCursor)
        {
            isTemporarilyReleasingCursor = false;
            bool shouldRestoreCursorLock =
                restoreCursorLockAfterTemporaryRelease;
            restoreCursorLockAfterTemporaryRelease = false;
            SetCursorLocked(shouldRestoreCursorLock);
            return;
        }

        // 鼠标未锁定时，左键点击画面重新锁定。
        if (!cursorLocked &&
            controlEnabled &&
            mouse != null &&
            mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLocked(true);
        }
    }

    /// <summary>
    /// 锁定或释放鼠标。
    /// 后续暂停菜单可以调用此方法。
    /// </summary>
    public void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;

        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }

    /// <summary>
    /// 启用或停用玩家控制。
    /// 剧情播放和暂停菜单可以调用此方法。
    /// </summary>
    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;

        if (!enabled)
        {
            isTemporarilyReleasingCursor = false;
            restoreCursorLockAfterTemporaryRelease = false;
            SetCursorLocked(false);
        }
    }

    /// <summary>
    /// 把玩家传送到指定位置。
    /// </summary>
    public void TeleportTo(
        Vector3 position,
        Quaternion rotation)
    {
        characterController.enabled = false;

        transform.SetPositionAndRotation(
            position,
            rotation
        );

        cameraPitch = 0f;

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation =
                Quaternion.identity;
        }

        verticalVelocity = 0f;
        characterController.enabled = true;
    }
}
