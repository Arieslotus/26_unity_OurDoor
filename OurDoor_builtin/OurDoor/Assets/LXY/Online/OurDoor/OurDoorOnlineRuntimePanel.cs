/// <summary>
/// 实现功能：从严格部署配置自动连接，并提供登录、房间、匹配、通知与 Enter 开关的最终联网界面。
/// </summary>
using System;
using System.Collections;
using System.Threading.Tasks;
using OurDoor.LXY.Networking;
using OurDoor.LXY.Networking.Deployment;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class OurDoorOnlineRuntimePanel : MonoBehaviour
{
    private enum LobbyPage
    {
        Root,
        CreateRoom,
        JoinRoom,
        Match
    }

    private const string GuestCharacters = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int GuestIdLength = 4;

    [Header("联网控制器")]
    [SerializeField] private OurDoorOnlineController onlineController;

    [Header("玩家")]
    [SerializeField] private string displayName = "Player";

    [Header("界面")]
    [SerializeField] private float panelWidth = 460f;

    private string guestId;
    private string roomIdInput = string.Empty;
    private string operationStatus = "等待自动连接";
    private string notice;
    private bool panelVisible;
    private bool noticeVisible;
    private bool operationRunning;
    private bool autoConnectFailed;
    private bool configurationLoadFailed;
    private bool initialized;
    private bool intentionalDisconnect;
    private OnlineSessionState previousState;
    private Vector2 scrollPosition;
    private LobbyPage lobbyPage;
    private int draftLevelId;
    private string draftRolePreference;
    private string deploymentConfigPath;
    private DeploymentEndpointConfig deploymentEndpoint;

    private PCFirstPersonController controlledPlayer;
    private bool savedControlEnabled;
    private bool savedPlayerCursorLocked;
    private CursorLockMode savedGlobalCursorLockMode;
    private bool savedGlobalCursorVisible;
    private bool hasGlobalCursorSnapshot;
    private Coroutine scenePlayerBinding;

    private void Awake()
    {
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 对象 {gameObject.name} 未配置 Online Controller。");
        }

        if (OurDoorOnlineController.Instance != null &&
            OurDoorOnlineController.Instance != onlineController)
        {
            Debug.Log(
                $"[M7 联网界面] 检测到随大厅场景重复加载的联网界面，" +
                $"对象={gameObject.name}，等待重复常驻对象销毁。");
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled)
            return;
        if (onlineController.Session == null)
        {
            throw new InvalidOperationException(
                "[M7 联网界面] Online Controller 尚未完成 Awake 初始化。");
        }
        if (panelWidth < 360f)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] Panel Width 不能小于 360，当前={panelWidth}。");
        }

        RequireLegacyDebugPanelsDisabled();
        guestId = GenerateGuestId();
        previousState = onlineController.Session.State;
        onlineController.Session.Changed += OnSessionChanged;
        onlineController.PlayerLeft += OnPlayerLeft;
        SceneManager.sceneLoaded += OnSceneLoaded;
        initialized = true;
        deploymentConfigPath =
            DeploymentEndpointConfigLoader.GetDefaultPath();
        LoadConfigurationAndConnect();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            SetPanelVisible(!panelVisible);
        }

        if (noticeVisible && keyboard.backspaceKey.wasPressedThisFrame)
            noticeVisible = false;
    }

    private void OnGUI()
    {
        if (panelVisible)
            DrawMainPanel();
        if (noticeVisible)
            DrawNotice();
    }

    private void DrawMainPanel()
    {
        float height = Mathf.Max(300f, Screen.height - 40f);
        GUILayout.BeginArea(
            new Rect(20f, 20f, panelWidth, height),
            GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.BeginHorizontal();
        GUILayout.Label("OurDoor 联网");
        GUILayout.FlexibleSpace();
        GUILayout.Label("Enter 关闭");
        GUILayout.EndHorizontal();
        GUILayout.Label($"状态：{GetStateText(onlineController.Session.State)}");
        GUILayout.Label($"操作：{operationStatus}");
        GUILayout.Space(8f);

        DrawStateContent();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawStateContent()
    {
        OnlineSessionState state = onlineController.Session.State;
        switch (state)
        {
            case OnlineSessionState.Disconnected:
                DrawDisconnected();
                return;

            case OnlineSessionState.Connected:
                DrawLogin();
                return;

            case OnlineSessionState.Authenticating:
                GUILayout.Label($"正在登录，临时 ID：{guestId}");
                return;
        }

        DrawLoggedInContent(state);
    }

    private void DrawDisconnected()
    {
        if (configurationLoadFailed)
        {
            GUILayout.Label("联网部署配置无效，已停止自动连接。");
            GUILayout.Label($"配置文件：{deploymentConfigPath}");
        }
        else if (autoConnectFailed)
        {
            GUILayout.Label(
                $"连接失败：{deploymentEndpoint?.ToString() ?? "端点未加载"}");
            GUILayout.Label($"配置文件：{deploymentConfigPath}");
        }
        else
        {
            GUILayout.Label(
                deploymentEndpoint == null
                    ? $"正在读取部署配置：{deploymentConfigPath}"
                    : $"正在连接：{deploymentEndpoint}");
        }

        GUI.enabled =
            (configurationLoadFailed || autoConnectFailed) &&
            !operationRunning;
        if (GUILayout.Button("重新读取配置并连接"))
        {
            LoadConfigurationAndConnect();
        }
        GUI.enabled = true;
    }

    private void LoadConfigurationAndConnect()
    {
        if (operationRunning)
        {
            throw new InvalidOperationException(
                "[M8 联网界面] 网络操作执行中不能重新读取部署配置。");
        }

        configurationLoadFailed = false;
        autoConnectFailed = false;
        deploymentEndpoint = null;
        try
        {
            NetworkManager manager = NetworkManager.Instance;
            if (manager == null)
            {
                throw new InvalidOperationException(
                    "[M8 联网界面] NetworkManager 尚未初始化。");
            }

            deploymentEndpoint =
                DeploymentEndpointConfigLoader.LoadAndApply(
                    manager.Config,
                    deploymentConfigPath);
        }
        catch (DeploymentConfigException exception)
        {
            configurationLoadFailed = true;
            operationStatus = "部署配置读取失败";
            ShowNotice(
                $"联网配置无效，未发起连接。\n" +
                $"配置文件：{deploymentConfigPath}\n" +
                $"原因：{exception.Message}");
            Debug.LogError(
                $"[M8 联网界面] 部署配置读取失败，" +
                $"path={deploymentConfigPath}, error={exception.Message}");
            Debug.LogException(exception);
            return;
        }

        string connectionContext =
            $"配置文件：{deploymentEndpoint.SourcePath}；" +
            $"端点：{deploymentEndpoint.Host}:{deploymentEndpoint.Port}";
        Debug.Log(
            $"[M8 联网界面] 已读取部署配置，{connectionContext}。");
        RunOperation(
            () => onlineController.ConnectAsync(
                deploymentEndpoint.Host,
                deploymentEndpoint.Port),
            "自动连接服务端",
            true,
            false,
            connectionContext);
    }

    private void DrawLogin()
    {
        GUILayout.Label($"本次临时 ID：{guestId}");
        GUILayout.Label("显示昵称");
        displayName = GUILayout.TextField(displayName, 24);

        GUI.enabled = !operationRunning;
        if (GUILayout.Button("登录"))
        {
            RunOperation(
                () => onlineController.LoginGuestAsync(guestId, displayName),
                "临时账号登录",
                true);
        }
        GUI.enabled = true;
    }

    private void DrawLoggedInContent(OnlineSessionState state)
    {
        GUILayout.Label(
            $"玩家：{onlineController.Session.DisplayName}  " +
            $"ID：{guestId}");

        if (state == OnlineSessionState.Lobby)
        {
            DrawLobby();
            return;
        }
        if (state == OnlineSessionState.Matching)
        {
            DrawMatching();
            return;
        }
        if (state == OnlineSessionState.WaitingRoom ||
            state == OnlineSessionState.LoadingLevel ||
            state == OnlineSessionState.Playing)
        {
            DrawRoom();
            return;
        }

        throw new InvalidOperationException(
            $"[M7 联网界面] 登录后遇到未处理状态：{state}。");
    }

    private void DrawLobby()
    {
        GUILayout.Space(8f);
        switch (lobbyPage)
        {
            case LobbyPage.Root:
                DrawLobbyRoot();
                return;
            case LobbyPage.CreateRoom:
                DrawCreateRoomPage();
                return;
            case LobbyPage.JoinRoom:
                DrawJoinRoomPage();
                return;
            case LobbyPage.Match:
                DrawMatchPage();
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(lobbyPage),
                    lobbyPage,
                    "[M7 联网界面] 未知大厅页面。");
        }
    }

    private void DrawLobbyRoot()
    {
        GUILayout.Label("请选择联网方式");
        GUI.enabled = !operationRunning;
        if (GUILayout.Button("创建房间"))
            OpenLobbyPage(LobbyPage.CreateRoom);
        if (GUILayout.Button("加入房间"))
            OpenLobbyPage(LobbyPage.JoinRoom);
        if (GUILayout.Button("匹配"))
            OpenLobbyPage(LobbyPage.Match);
        GUILayout.Space(12f);
        if (GUILayout.Button("断开连接"))
            DisconnectFromLobby();
        GUI.enabled = true;
    }

    private void DrawCreateRoomPage()
    {
        if (DrawSecondaryPageHeader("创建房间"))
            return;

        GUILayout.Label("选择关卡（必选）");
        GUI.enabled = !operationRunning;
        GUILayout.BeginHorizontal();
        DrawDraftLevelButton(1);
        DrawDraftLevelButton(2);
        DrawDraftLevelButton(3);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("选择创建者身份（必选）");
        GUILayout.BeginHorizontal();
        DrawDraftRoleButton(RolePreferences.Outer, "Outer");
        DrawDraftRoleButton(RolePreferences.Inner, "Inner");
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.Label(
            draftLevelId == 0
                ? "已选关卡：未选择"
                : $"已选关卡：第 {draftLevelId} 关");
        GUILayout.Label(
            draftRolePreference == null
                ? "已选身份：未选择"
                : $"已选身份：{GetRoleText(draftRolePreference)}");

        GUI.enabled =
            !operationRunning &&
            draftLevelId >= 1 &&
            draftLevelId <= 3 &&
            (draftRolePreference == RolePreferences.Outer ||
             draftRolePreference == RolePreferences.Inner);
        if (GUILayout.Button("创建房间"))
            StartCreateRoom();
        GUI.enabled = true;
    }

    private void DrawJoinRoomPage()
    {
        if (DrawSecondaryPageHeader("加入房间"))
            return;
        GUILayout.Label("关卡、身份由创建者选择");
        GUILayout.Space(8f);
        GUILayout.Label("房间码");
        GUI.enabled = !operationRunning;
        roomIdInput = GUILayout.TextField(roomIdInput, 12).ToUpperInvariant();
        GUI.enabled =
            !operationRunning && !string.IsNullOrWhiteSpace(roomIdInput);
        if (GUILayout.Button("加入房间"))
        {
            RunOperation(
                () => onlineController.JoinRoomAsync(roomIdInput.Trim()),
                "加入房间",
                true);
        }
        GUI.enabled = true;
    }

    private void DrawMatchPage()
    {
        if (DrawSecondaryPageHeader("匹配"))
            return;

        GUILayout.Label("匹配关卡");
        GUI.enabled = !operationRunning;
        GUILayout.BeginHorizontal();
        DrawDraftLevelButton(0, "任意关卡");
        DrawDraftLevelButton(1);
        DrawDraftLevelButton(2);
        DrawDraftLevelButton(3);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("身份偏好");
        GUILayout.BeginHorizontal();
        DrawDraftRoleButton(RolePreferences.Outer, "Outer");
        DrawDraftRoleButton(RolePreferences.Inner, "Inner");
        DrawDraftRoleButton(RolePreferences.Any, "任意身份");
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.Label(
            draftLevelId == 0
                ? "已选关卡：任意关卡"
                : $"已选关卡：第 {draftLevelId} 关");
        GUILayout.Label($"身份偏好：{GetRoleText(draftRolePreference)}");

        GUI.enabled = !operationRunning;
        if (GUILayout.Button("开始匹配"))
            StartMatch();
        GUI.enabled = true;
    }

    private bool DrawSecondaryPageHeader(string title)
    {
        bool returned = false;
        GUILayout.BeginHorizontal();
        GUILayout.Label(title);
        GUILayout.FlexibleSpace();
        GUI.enabled = !operationRunning;
        if (GUILayout.Button("返回", GUILayout.Width(90f)))
        {
            ReturnToLobbyRoot();
            returned = true;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);
        return returned;
    }

    private void DrawMatching()
    {
        string levelText = onlineController.Session.MatchingLevelId == 0
            ? "任意关卡"
            : $"第 {onlineController.Session.MatchingLevelId} 关";
        GUILayout.Label($"正在匹配：{levelText}");
        GUILayout.Label(
            $"身份偏好：{GetRoleText(onlineController.Session.MatchingRolePreference)}");

        GUI.enabled = !operationRunning;
        if (GUILayout.Button("取消匹配"))
        {
            RunOperation(
                async () => { await onlineController.CancelMatchAsync(); },
                "取消匹配",
                true);
        }
        GUI.enabled = true;
    }

    private void DrawRoom()
    {
        GUILayout.Label($"房间码：{onlineController.Session.RoomId}");
        GUILayout.Label($"关卡：第 {onlineController.Session.LevelId} 关");
        GUILayout.Label($"身份：{GetRoleText(onlineController.Session.Role)}");

        GUI.enabled = !operationRunning;
        if (GUILayout.Button("复制房间码"))
        {
            GUIUtility.systemCopyBuffer = onlineController.Session.RoomId;
            operationStatus = "房间码已复制";
        }
        if (GUILayout.Button("离开房间并断开连接"))
        {
            RunOperation(
                onlineController.LeaveRoomAsync,
                "离开房间",
                true,
                true);
        }
        GUI.enabled = true;
    }

    private void DrawDraftRoleButton(string rolePreference, string label)
    {
        bool selected = string.Equals(
            draftRolePreference,
            rolePreference,
            StringComparison.Ordinal);
        if (GUILayout.Button(selected ? $"【{label}】" : label))
            draftRolePreference = rolePreference;
    }

    private void DrawDraftLevelButton(int levelId, string label = null)
    {
        bool selected = draftLevelId == levelId;
        string buttonLabel = label ?? $"第 {levelId} 关";
        if (GUILayout.Button(selected ? $"【{buttonLabel}】" : buttonLabel))
            draftLevelId = levelId;
    }

    private void DrawNotice()
    {
        const float noticeWidth = 500f;
        const float noticeHeight = 160f;
        float y = Mathf.Max(0f, Screen.height - noticeHeight - 20f);
        Rect area = new Rect(20f, y, noticeWidth, noticeHeight);
        GUI.Box(area, GUIContent.none);

        var noticeStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };
        GUI.Label(
            new Rect(area.x + 12f, area.y + 10f, area.width - 55f, area.height - 20f),
            notice,
            noticeStyle);
        if (GUI.Button(
                new Rect(area.xMax - 38f, area.y + 8f, 28f, 28f),
                "×"))
        {
            noticeVisible = false;
        }
    }

    private async Task CreateRoomAndCopyCodeAsync()
    {
        string createdRoomId = await onlineController.CreateSelectedRoomAsync();
        roomIdInput = createdRoomId;
        GUIUtility.systemCopyBuffer = createdRoomId;
    }

    private void StartCreateRoom()
    {
        if (draftLevelId < 1 || draftLevelId > 3)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 创建房间缺少有效关卡，levelId={draftLevelId}。");
        }
        if (draftRolePreference != RolePreferences.Outer &&
            draftRolePreference != RolePreferences.Inner)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 创建者必须选择 Outer 或 Inner，" +
                $"当前={draftRolePreference ?? "未选择"}。");
        }

        onlineController.SelectLevel(draftLevelId);
        onlineController.SelectRolePreference(draftRolePreference);
        RunOperation(
            CreateRoomAndCopyCodeAsync,
            "创建房间",
            true);
    }

    private void StartMatch()
    {
        if (draftLevelId < 0 || draftLevelId > 3)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 匹配关卡非法，levelId={draftLevelId}。");
        }
        RolePreferences.Validate(
            draftRolePreference,
            nameof(draftRolePreference));

        onlineController.SelectRolePreference(draftRolePreference);
        if (draftLevelId == 0)
        {
            RunOperation(
                async () => { await onlineController.RequestAnyLevelMatchAsync(); },
                "匹配任意关卡",
                true);
            return;
        }

        onlineController.SelectLevel(draftLevelId);
        RunOperation(
            async () => { await onlineController.RequestSelectedMatchAsync(); },
            "匹配所选关卡",
            true);
    }

    private void OpenLobbyPage(LobbyPage page)
    {
        if (page == LobbyPage.Root)
            throw new ArgumentOutOfRangeException(nameof(page), page, "必须打开二级页面。");
        if (operationRunning)
            throw new InvalidOperationException("[M7 联网界面] 操作执行中不能切换大厅页面。");

        lobbyPage = page;
        roomIdInput = string.Empty;
        draftLevelId = 0;
        draftRolePreference =
            page == LobbyPage.Match ? RolePreferences.Any : null;
        scrollPosition = Vector2.zero;
    }

    private void ReturnToLobbyRoot()
    {
        if (operationRunning)
            throw new InvalidOperationException("[M7 联网界面] 操作执行中不能返回一级页面。");

        ClearLobbyNavigation();
    }

    private void ClearLobbyNavigation()
    {
        lobbyPage = LobbyPage.Root;
        roomIdInput = string.Empty;
        draftLevelId = 0;
        draftRolePreference = null;
        scrollPosition = Vector2.zero;
    }

    private void DisconnectFromLobby()
    {
        if (operationRunning)
            throw new InvalidOperationException("[M7 联网界面] 操作执行中不能断开连接。");

        intentionalDisconnect = true;
        operationStatus = "正在断开连接";
        try
        {
            onlineController.DisconnectFromServer();
            operationStatus = "已断开连接";
        }
        catch
        {
            intentionalDisconnect = false;
            throw;
        }
    }

    private async void RunOperation(
        Func<Task> operation,
        string operationName,
        bool notifyFailure,
        bool intentionalLeave = false,
        string failureContext = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (operationRunning)
            throw new InvalidOperationException("[M7 联网界面] 已有操作正在执行。");

        operationRunning = true;
        operationStatus = operationName + "中";
        if (intentionalLeave)
            intentionalDisconnect = true;

        try
        {
            await operation();
            operationStatus = operationName + "成功";
        }
        catch (Exception exception)
        {
            if (intentionalLeave)
                intentionalDisconnect = false;
            if (onlineController.Session.State == OnlineSessionState.Disconnected)
                autoConnectFailed = true;
            operationStatus = operationName + "失败";
            if (notifyFailure)
            {
                ShowNotice(
                    $"{operationName}失败：{exception.Message}" +
                    (string.IsNullOrWhiteSpace(failureContext)
                        ? string.Empty
                        : $"\n{failureContext}"));
            }
            Debug.LogError(
                $"[M7 联网界面] {operationName}失败，" +
                $"state={onlineController.Session.State}, object={gameObject.name}, " +
                $"context={failureContext ?? "无"}。");
            Debug.LogException(exception);
        }
        finally
        {
            operationRunning = false;
        }
    }

    private void OnSessionChanged()
    {
        OnlineSessionState currentState = onlineController.Session.State;
        if (currentState == OnlineSessionState.Lobby &&
            previousState != OnlineSessionState.Lobby)
        {
            ClearLobbyNavigation();
        }
        if (currentState == OnlineSessionState.Disconnected &&
            previousState != OnlineSessionState.Disconnected)
        {
            ClearLobbyNavigation();
            autoConnectFailed = true;
            if (!intentionalDisconnect)
                ShowNotice("与服务器的连接已断开，请打开联网界面重试。");
            intentionalDisconnect = false;
        }

        previousState = currentState;
    }

    private void OnPlayerLeft(PlayerLeftDto playerLeft)
    {
        if (playerLeft == null)
            throw new ArgumentNullException(nameof(playerLeft));

        ShowNotice(
            $"另一名玩家已离开，本局结束。" +
            $"房间={playerLeft.roomId}，原因={playerLeft.reason}");
    }

    private void ShowNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("[M7 联网界面] 通知内容不能为空。", nameof(message));

        notice = message;
        noticeVisible = true;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelVisible == visible)
            return;

        panelVisible = visible;
        if (visible)
            CaptureAndReleasePlayerControl();
        else
            RestorePlayerControl();
    }

    private void CaptureAndReleasePlayerControl()
    {
        if (controlledPlayer != null || hasGlobalCursorSnapshot)
        {
            throw new InvalidOperationException(
                "[M7 联网界面] 打开面板前仍保留旧的玩家控制快照。");
        }

        PCFirstPersonController player = FindSinglePlayerController();
        if (player != null)
        {
            controlledPlayer = player;
            savedControlEnabled = player.IsControlEnabled;
            savedPlayerCursorLocked = player.IsCursorLocked;
            player.SetControlEnabled(false);
            return;
        }

        savedGlobalCursorLockMode = Cursor.lockState;
        savedGlobalCursorVisible = Cursor.visible;
        hasGlobalCursorSnapshot = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestorePlayerControl()
    {
        if (controlledPlayer != null)
        {
            controlledPlayer.SetControlEnabled(savedControlEnabled);
            controlledPlayer.SetCursorLocked(savedPlayerCursorLocked);
            controlledPlayer = null;
        }
        else if (hasGlobalCursorSnapshot)
        {
            Cursor.lockState = savedGlobalCursorLockMode;
            Cursor.visible = savedGlobalCursorVisible;
        }

        hasGlobalCursorSnapshot = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!panelVisible)
            return;
        if (mode != LoadSceneMode.Single)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 联网场景切换预期 Single，实际={mode}, scene={scene.name}。");
        }

        if (scenePlayerBinding != null)
            StopCoroutine(scenePlayerBinding);
        scenePlayerBinding = StartCoroutine(BindScenePlayerAfterStart(scene.name));
    }

    private IEnumerator BindScenePlayerAfterStart(string sceneName)
    {
        yield return null;
        scenePlayerBinding = null;
        if (!panelVisible)
            yield break;

        controlledPlayer = null;
        hasGlobalCursorSnapshot = false;
        CaptureAndReleasePlayerControl();
        Debug.Log(
            $"[M7 联网界面] 场景切换后保持面板控制权，scene={sceneName}, " +
            $"player={(controlledPlayer != null ? controlledPlayer.gameObject.name : "无")}。");
    }

    private static PCFirstPersonController FindSinglePlayerController()
    {
        PCFirstPersonController[] players =
            FindObjectsOfType<PCFirstPersonController>();
        if (players.Length > 1)
        {
            throw new InvalidOperationException(
                $"[M7 联网界面] 场景中存在多个 PCFirstPersonController，" +
                $"数量={players.Length}。");
        }

        return players.Length == 1 ? players[0] : null;
    }

    private static string GenerateGuestId()
    {
        char[] value = new char[GuestIdLength];
        for (int index = 0; index < value.Length; index++)
        {
            value[index] = GuestCharacters[
                UnityEngine.Random.Range(0, GuestCharacters.Length)];
        }
        return new string(value);
    }

    private static string GetStateText(OnlineSessionState state)
    {
        switch (state)
        {
            case OnlineSessionState.Disconnected:
                return "未连接";
            case OnlineSessionState.Connected:
                return "已连接，等待登录";
            case OnlineSessionState.Authenticating:
                return "登录中";
            case OnlineSessionState.Lobby:
                return "大厅";
            case OnlineSessionState.Matching:
                return "匹配中";
            case OnlineSessionState.WaitingRoom:
                return "等待另一名玩家";
            case OnlineSessionState.LoadingLevel:
                return "加载关卡";
            case OnlineSessionState.Playing:
                return "游戏中";
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "未知联网状态。");
        }
    }

    private static string GetRoleText(string role)
    {
        if (role == RolePreferences.Outer)
            return "Outer";
        if (role == RolePreferences.Inner)
            return "Inner";
        if (role == RolePreferences.Any)
            return "任意身份";

        throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "[M7 联网界面] 未知身份或身份偏好。");
    }

    private static void RequireLegacyDebugPanelsDisabled()
    {
        RequireDisabled<M2LobbyDebugPanel>("M2LobbyDebugPanel");
        RequireDisabled<M3Level1DebugPanel>("M3Level1DebugPanel");
        RequireDisabled<M4LevelDebugPanel>("M4LevelDebugPanel");
        RequireDisabled<M5ExitDebugPanel>("M5ExitDebugPanel");
    }

    private static void RequireDisabled<T>(string componentName)
        where T : MonoBehaviour
    {
        T[] components = FindObjectsOfType<T>();
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"[M7 联网界面] {componentName} 仍处于启用状态，" +
                    $"对象={components[index].gameObject.name}。请在 Inspector 手动禁用旧验收面板。");
            }
        }
    }

    private void OnDisable()
    {
        if (panelVisible)
        {
            panelVisible = false;
            RestorePlayerControl();
        }
    }

    private void OnDestroy()
    {
        if (!initialized)
            return;

        if (scenePlayerBinding != null)
            StopCoroutine(scenePlayerBinding);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        onlineController.Session.Changed -= OnSessionChanged;
        onlineController.PlayerLeft -= OnPlayerLeft;
    }
}
