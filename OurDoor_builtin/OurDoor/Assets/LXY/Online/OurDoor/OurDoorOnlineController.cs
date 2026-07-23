/// <summary>
/// 实现功能：组合通用登录与房间服务，在 ROOM_READY 后应用角色并进入对应 OurDoor 场景。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Services;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class OurDoorOnlineController : MonoBehaviour
{
    public static OurDoorOnlineController Instance { get; private set; }

    [SerializeField] private string clientVersion = "0.2.0";

    private CancellationTokenSource cancellation;
    private NetworkManager network;
    private AuthService authService;
    private RoomService roomService;

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

    private void OnRoomReady(RoomReadyDto ready)
    {
        Debug.Log(
            $"[M2 在线控制器] 收到 ROOM_READY，roomId={ready.roomId}, " +
            $"levelId={ready.levelId}, role={ready.role}, revision={ready.revision}。");

        OurDoorRoleAdapter.Apply(ready.role);
        if (OnlineActionBridge.IsOnline)
        {
            throw new InvalidOperationException(
                "[M2 在线控制器] OnlineActionBridge 已被其他发送器占用。");
        }

        OnlineActionBridge.EnterOnline(intent =>
        {
            throw new NotSupportedException(
                $"[M2 在线控制器] LEVEL_ACTION 将在 M3 实现，" +
                $"当前操作不会本地执行：levelId={intent.LevelId}, action={intent.Action}。");
        });

        OurDoorSceneRouter.LoadLevel(ready.levelId);
        Session.MarkPlaying();
        Debug.Log(
            $"[M2 在线控制器] 已进入联网关卡，roomId={ready.roomId}, " +
            $"scene={OurDoorSceneRouter.GetSceneName(ready.levelId)}, role={ready.role}。");
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

        network.ConnectionLost -= OnConnectionLost;
        roomService.RoomReady -= OnRoomReady;
        roomService.Dispose();
        cancellation.Cancel();
        cancellation.Dispose();
        if (OnlineActionBridge.IsOnline)
            OnlineActionBridge.ExitOnline();
        Instance = null;
    }
}
