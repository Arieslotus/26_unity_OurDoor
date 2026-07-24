/// <summary>
/// 实现功能：提供临时登录、房间码与 M6 按所选关卡匹配的双客户端验收入口。
/// </summary>
using System;
using System.Threading.Tasks;
using OurDoor.LXY.Networking;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class M2LobbyDebugPanel : MonoBehaviour
{
    [SerializeField] private OurDoorOnlineController onlineController;
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 8888;
    [SerializeField] private string guestId;
    [SerializeField] private string displayName = "Player";
    [SerializeField] private string roomId;

    private bool operationRunning;
    private string lastOperation = "尚未操作";
    private string portInput;

    private void Awake()
    {
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M2 调试面板] 对象 {gameObject.name} 未配置 Online Controller。");
        }
        if (string.IsNullOrWhiteSpace(guestId))
        {
            guestId = Guid.NewGuid().ToString("N");
            Debug.Log($"[M2 调试面板] 已生成本次运行的临时 guestId={guestId}。");
        }
        portInput = port.ToString();
    }

    private void OnGUI()
    {
        const float width = 520f;
        GUILayout.BeginArea(
            new Rect(20f, 20f, width, Screen.height - 40f),
            GUI.skin.box);
        GUILayout.Label("OurDoor M2-M6 联网验收");
        GUILayout.Label($"状态：{onlineController.Session.State}");
        GUILayout.Label(
            $"UID：{onlineController.Session.Uid ?? "未登录"}  " +
            $"房间：{onlineController.Session.RoomId ?? "无"}");
        GUILayout.Label(
            $"关卡：{onlineController.Session.LevelId}  " +
            $"匹配关卡：{onlineController.Session.MatchingLevelId}  " +
            $"角色：{onlineController.Session.Role ?? "未分配"}  " +
            $"Revision：{onlineController.Session.Revision}");
        GUILayout.Label($"最近操作：{lastOperation}");

        GUILayout.Space(8f);
        GUILayout.Label("服务端 Host");
        host = GUILayout.TextField(host);
        GUILayout.Label("服务端 Port");
        portInput = GUILayout.TextField(portInput);

        GUI.enabled =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Disconnected;
        if (GUILayout.Button("1. 连接服务端"))
        {
            RunOperation(async () =>
            {
                if (!int.TryParse(portInput, out port))
                {
                    throw new FormatException(
                        $"[M2 调试面板] Port 不是有效整数：{portInput}。");
                }
                await onlineController.ConnectAsync(host, port);
            }, "连接服务端");
        }

        GUILayout.Space(8f);
        GUILayout.Label("临时 Guest ID");
        guestId = GUILayout.TextField(guestId);
        GUILayout.Label("显示昵称");
        displayName = GUILayout.TextField(displayName);
        GUI.enabled =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Connected;
        if (GUILayout.Button("2. 临时登录"))
        {
            RunOperation(
                () => onlineController.LoginGuestAsync(guestId, displayName),
                "临时登录");
        }
        if (GUILayout.Button("验收探针：未登录创建房间应被拒绝"))
        {
            RunOperation(async () =>
            {
                var response =
                    await NetworkManager.Instance.SendRequestAsync<
                        CreateRoomRequest,
                        CreateRoomResponse>(
                        MessageIds.CreateRoom,
                        new CreateRoomRequest { levelId = 1 });
                if (response.code != ServerErrorCodes.NotLoggedIn)
                {
                    throw new InvalidOperationException(
                        $"[M2 验收探针] 预期错误码 " +
                        $"{ServerErrorCodes.NotLoggedIn}，实际={response.code}, " +
                        $"message={response.message}。");
                }

                Debug.Log(
                    $"[M2 验收探针] PASS：未登录创建房间被服务端拒绝，" +
                    $"code={response.code}, message={response.message}。");
            }, "未登录操作探针");
        }

        GUILayout.Space(8f);
        GUILayout.Label(
            $"已选关卡：{onlineController.SelectedLevelId} " +
            "（正式接入时使用原选关按钮）");
        GUI.enabled =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Lobby;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("调试选择 Level 1"))
            onlineController.SelectLevel(1);
        if (GUILayout.Button("调试选择 Level 2"))
            onlineController.SelectLevel(2);
        if (GUILayout.Button("调试选择 Level 3"))
            onlineController.SelectLevel(3);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("3A. 创建所选关卡房间"))
        {
            RunOperation(async () =>
            {
                roomId = await onlineController.CreateSelectedRoomAsync();
                GUIUtility.systemCopyBuffer = roomId;
                Debug.Log($"[M2 调试面板] 房间码已复制到剪贴板：{roomId}");
            }, "创建房间");
        }

        GUILayout.Space(8f);
        GUILayout.Label("待加入的房间码");
        roomId = GUILayout.TextField(roomId);
        if (GUILayout.Button("3B. 加入房间"))
            RunOperation(() => onlineController.JoinRoomAsync(roomId), "加入房间");

        GUILayout.Space(8f);
        GUI.enabled =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Lobby;
        if (GUILayout.Button("3C. 匹配所选关卡"))
        {
            RunOperation(async () =>
            {
                await onlineController.RequestSelectedMatchAsync();
            }, "请求匹配");
        }

        GUI.enabled =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Matching;
        if (GUILayout.Button("取消匹配"))
        {
            RunOperation(async () =>
            {
                await onlineController.CancelMatchAsync();
            }, "取消匹配");
        }

        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private async void RunOperation(Func<Task> operation, string operationName)
    {
        if (operationRunning)
            throw new InvalidOperationException("[M2 调试面板] 已有操作正在执行。");

        operationRunning = true;
        lastOperation = operationName + "执行中";
        try
        {
            await operation();
            lastOperation = operationName + "成功";
        }
        catch (Exception exception)
        {
            lastOperation = operationName + "失败：" + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            operationRunning = false;
        }
    }
}
