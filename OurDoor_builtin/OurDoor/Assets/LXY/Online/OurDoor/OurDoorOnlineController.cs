/// <summary>
/// 实现功能：组合登录、房间与关卡操作服务，并在进入第一关后应用服务端权威快照。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Services;
using OurDoor.LXY.Networking.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OurDoorOnlineController : MonoBehaviour
{
    public static OurDoorOnlineController Instance { get; private set; }

    [SerializeField] private string clientVersion = "0.2.0";

    private CancellationTokenSource cancellation;
    private NetworkManager network;
    private AuthService authService;
    private RoomService roomService;
    private LevelActionService levelActionService;
    private OurDoorLevel1SnapshotSynchronizer level1Synchronizer;
    private RoomReadyDto pendingRoomReady;
    private Exception levelActionFailure;
    private bool destroying;

    public NetworkSession Session { get; private set; }
    public int SelectedLevelId { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            throw new InvalidOperationException(
                $"[M2 在线控制器] 检测到重复组件，对象={gameObject.name}。");
        }
        if (string.IsNullOrWhiteSpace(clientVersion))
            throw new InvalidOperationException("[M2 在线控制器] Client Version 不能为空。");

        network = NetworkManager.Instance;
        if (network == null)
        {
            throw new InvalidOperationException(
                "[M2 在线控制器] 场景中缺少 NetworkManager。" +
                "请在同一常驻对象上手动添加 MainThreadDispatcher 和 NetworkManager。");
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        cancellation = new CancellationTokenSource();
        Session = new NetworkSession();
        authService = new AuthService(network, Session);
        roomService = new RoomService(network, Session);
        levelActionService = new LevelActionService(network, Session);
        roomService.RoomReady += OnRoomReady;
        network.ConnectionLost += OnConnectionLost;
    }

    public async Task ConnectAsync(string host, int port)
    {
        if (Session.State != OnlineSessionState.Disconnected)
        {
            throw new InvalidOperationException(
                $"[M2 在线控制器] 连接要求状态 Disconnected，当前={Session.State}。");
        }

        network.Config.SetEndpoint(host, port);
        await network.ConnectAsync(cancellation.Token);
        Session.MarkConnected();
        Debug.Log($"[M2 在线控制器] 已连接服务端，host={host}, port={port}。");
    }

    public async Task LoginGuestAsync(string guestId, string displayName)
    {
        await authService.LoginGuestAsync(
            guestId,
            displayName,
            clientVersion,
            cancellation.Token);
        Debug.Log(
            $"[M2 在线控制器] 临时登录成功，uid={Session.Uid}, " +
            $"displayName={Session.DisplayName}。");
    }

    public void SelectLevel(int levelId)
    {
        if (levelId < 1 || levelId > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(levelId),
                $"[M2 在线控制器] levelId 必须是 1、2、3，当前={levelId}。");
        }

        SelectedLevelId = levelId;
        Debug.Log(
            $"[M2 在线控制器] 已选择关卡，levelId={levelId}, " +
            $"scene={OurDoorSceneRouter.GetSceneName(levelId)}。");
    }

    public async Task<string> CreateSelectedRoomAsync()
    {
        if (SelectedLevelId == 0)
        {
            throw new InvalidOperationException(
                "[M2 在线控制器] 创建房间前必须通过选关按钮选择 levelId。");
        }

        string roomId =
            await roomService.CreateRoomAsync(SelectedLevelId, cancellation.Token);
        Debug.Log(
            $"[M2 在线控制器] 房间创建成功，roomId={roomId}, " +
            $"levelId={Session.LevelId}, role={Session.Role}, revision={Session.Revision}。");
        return roomId;
    }

    public async Task JoinRoomAsync(string roomId)
    {
        await roomService.JoinRoomAsync(roomId, cancellation.Token);
        Debug.Log(
            $"[M2 在线控制器] 房间加入成功，roomId={Session.RoomId}, " +
            $"levelId={Session.LevelId}, role={Session.Role}, revision={Session.Revision}。");
    }

    public Task<LevelActionResponse> SubmitLevelActionAsync(
        string action,
        bool boolValue,
        string clientActionId = null)
    {
        if (levelActionFailure != null)
        {
            throw new InvalidOperationException(
                "[M3 在线控制器] 之前的关卡操作已经失败，" +
                "必须先处理该错误，当前不再接受后续操作。",
                levelActionFailure);
        }

        return levelActionService.SubmitAsync(
            Session.LevelId,
            action,
            boolValue,
            clientActionId,
            cancellation.Token);
    }

    private void OnRoomReady(RoomReadyDto ready)
    {
        if (pendingRoomReady != null)
        {
            throw new InvalidOperationException(
                $"[M3 在线控制器] 上一个关卡仍在加载，" +
                $"roomId={pendingRoomReady.roomId}, levelId={pendingRoomReady.levelId}。");
        }

        Debug.Log(
            $"[M2 在线控制器] 收到 ROOM_READY，roomId={ready.roomId}, " +
            $"levelId={ready.levelId}, role={ready.role}, revision={ready.revision}。");

        OurDoorRoleAdapter.Apply(ready.role);
        if (OnlineActionBridge.IsOnline)
        {
            throw new InvalidOperationException(
                "[M2 在线控制器] OnlineActionBridge 已被其他发送器占用。");
        }

        OnlineActionBridge.EnterOnline(SubmitOnlineIntent);

        pendingRoomReady = ready;
        SceneManager.sceneLoaded += OnLevelSceneLoaded;
        try
        {
            OurDoorSceneRouter.LoadLevel(ready.levelId);
        }
        catch
        {
            SceneManager.sceneLoaded -= OnLevelSceneLoaded;
            pendingRoomReady = null;
            throw;
        }
    }

    private void OnLevelSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RoomReadyDto ready = pendingRoomReady;
        SceneManager.sceneLoaded -= OnLevelSceneLoaded;
        pendingRoomReady = null;

        if (ready == null)
        {
            throw new InvalidOperationException(
                $"[M3 在线控制器] 收到场景加载完成事件时缺少 ROOM_READY，" +
                $"scene={scene.name}, mode={mode}。");
        }

        string expectedScene = OurDoorSceneRouter.GetSceneName(ready.levelId);
        if (!string.Equals(scene.name, expectedScene, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"[M3 在线控制器] 加载完成的场景与 ROOM_READY 不一致，" +
                $"expected={expectedScene}, actual={scene.name}, " +
                $"roomId={ready.roomId}, levelId={ready.levelId}。");
        }

        if (ready.levelId == 1)
        {
            var synchronizer =
                new OurDoorLevel1SnapshotSynchronizer(Session);
            synchronizer.Activate();
            level1Synchronizer = synchronizer;
        }

        Session.MarkPlaying();
        Debug.Log(
            $"[M3 在线控制器] 已进入联网关卡，roomId={ready.roomId}, " +
            $"scene={OurDoorSceneRouter.GetSceneName(ready.levelId)}, role={ready.role}。");
    }

    private bool SubmitOnlineIntent(OnlineActionIntent intent)
    {
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));
        if (levelActionFailure != null)
        {
            throw new InvalidOperationException(
                "[M3 在线控制器] 之前的关卡操作已经失败，" +
                "当前不再接受后续操作。",
                levelActionFailure);
        }

        levelActionService.ValidateSubmission(intent.LevelId, intent.Action);
        ObserveLevelActionAsync(intent);
        return true;
    }

    private async void ObserveLevelActionAsync(OnlineActionIntent intent)
    {
        try
        {
            LevelActionResponse response =
                await levelActionService.SubmitAsync(
                    intent.LevelId,
                    intent.Action,
                    intent.BoolValue,
                    null,
                    cancellation.Token);
            Debug.Log(
                $"[M3 在线控制器] 关卡操作已确认，" +
                $"action={intent.Action}, clientActionId={response.clientActionId}, " +
                $"revision={response.revision}, changed={response.changed}, " +
                $"duplicate={response.duplicate}。");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Debug.Log(
                $"[M3 在线控制器] 对象销毁时取消关卡操作，" +
                $"action={intent.Action}, object={gameObject.name}。");
        }
        catch (Exception exception) when (destroying)
        {
            Debug.Log(
                $"[M3 在线控制器] 对象销毁期间终止关卡操作，" +
                $"action={intent.Action}, error={exception.Message}。");
        }
        catch (ServerRequestException exception)
        {
            Debug.LogError(
                $"[M3 在线控制器] 服务端拒绝关卡操作，" +
                $"roomId={Session.RoomId}, levelId={intent.LevelId}, " +
                $"action={intent.Action}, boolValue={intent.BoolValue}, " +
                $"code={exception.Code}。修正角色或前置状态后可以重新提交。");
            Debug.LogException(exception);
        }
        catch (Exception exception)
        {
            levelActionFailure = exception;
            Debug.LogError(
                $"[M3 在线控制器] 关卡操作失败，后续操作已停止，" +
                $"roomId={Session.RoomId}, levelId={intent.LevelId}, " +
                $"action={intent.Action}, boolValue={intent.BoolValue}。");
            Debug.LogException(exception);
        }
    }

    private void OnConnectionLost(Exception exception)
    {
        Debug.LogError(
            $"[M2 在线控制器] 网络连接已断开，state={Session.State}, " +
            $"uid={Session.Uid ?? "未登录"}, roomId={Session.RoomId ?? "无"}, " +
            $"error={exception}");
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        destroying = true;
        SceneManager.sceneLoaded -= OnLevelSceneLoaded;
        pendingRoomReady = null;
        network.ConnectionLost -= OnConnectionLost;
        roomService.RoomReady -= OnRoomReady;
        level1Synchronizer?.Dispose();
        roomService.Dispose();
        cancellation.Cancel();
        levelActionService.Dispose();
        cancellation.Dispose();
        if (OnlineActionBridge.IsOnline)
            OnlineActionBridge.ExitOnline();
        Instance = null;
    }
}
