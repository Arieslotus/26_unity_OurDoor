/// <summary>
/// 实现功能：提供 M5 主动退出入口，并显示对端离开通知和当前会话状态。
/// </summary>
using System;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class M5ExitDebugPanel : MonoBehaviour
{
    [SerializeField] private OurDoorOnlineController onlineController;

    private bool leaveRunning;
    private string status = "尚未收到退出事件";

    private void Awake()
    {
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M5 调试面板] 对象 {gameObject.name} 未配置 Online Controller。");
        }

        onlineController.PlayerLeft += OnPlayerLeft;
    }

    private void OnDestroy()
    {
        if (onlineController != null)
            onlineController.PlayerLeft -= OnPlayerLeft;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(Screen.width - 500f, 20f, 480f, 190f),
            GUI.skin.box);
        GUILayout.Label("OurDoor M5 退出与清理验收");
        GUILayout.Label(
            $"状态：{onlineController.Session.State}  " +
            $"UID：{onlineController.Session.Uid ?? "无"}");
        GUILayout.Label(
            $"房间：{onlineController.Session.RoomId ?? "无"}  " +
            $"关卡：{onlineController.Session.LevelId}");
        GUILayout.Label($"通知：{status}");

        GUI.enabled =
            !leaveRunning &&
            (onlineController.Session.State == OnlineSessionState.WaitingRoom ||
             onlineController.Session.State == OnlineSessionState.LoadingLevel ||
             onlineController.Session.State == OnlineSessionState.Playing);
        if (GUILayout.Button("主动退出房间并断开连接"))
            LeaveRoom();
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void OnPlayerLeft(PlayerLeftDto playerLeft)
    {
        status =
            $"PLAYER_LEFT room={playerLeft.roomId}, " +
            $"uid={playerLeft.leftUid}, reason={playerLeft.reason}";
    }

    private async void LeaveRoom()
    {
        if (leaveRunning)
            throw new InvalidOperationException("[M5 调试面板] 主动退出已经在执行。");

        leaveRunning = true;
        status = "正在主动退出";
        try
        {
            await onlineController.LeaveRoomAsync();
            status = "主动退出成功，连接已关闭";
        }
        catch (Exception exception)
        {
            status = "主动退出失败：" + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            leaveRunning = false;
        }
    }
}
