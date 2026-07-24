/// <summary>
/// 实现功能：提供 M4 第二、三关动作、重复请求和双方换关准备的验收入口。
/// </summary>
using System;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class M4LevelDebugPanel : MonoBehaviour
{
    [SerializeField] private OurDoorOnlineController onlineController;
    [SerializeField] private string duplicateAction = "KEY_FOUND";
    [SerializeField] private bool duplicateBoolValue = true;
    [SerializeField] private string duplicateClientActionId;

    private bool operationRunning;
    private string lastOperation = "尚未操作";

    private void Awake()
    {
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M4 调试面板] 对象 {gameObject.name} 未配置 Online Controller。");
        }
        duplicateClientActionId =
            string.IsNullOrWhiteSpace(duplicateClientActionId)
                ? "M4-DUP-" + Guid.NewGuid().ToString("N")
                : duplicateClientActionId.Trim();
    }

    private void OnGUI()
    {
        const float width = 560f;
        GUILayout.BeginArea(
            new Rect(20f, Screen.height * 0.46f, width, Screen.height * 0.52f),
            GUI.skin.box);
        GUILayout.Label("OurDoor M4 权威同步与连续换关验收");
        GUILayout.Label(
            $"状态：{onlineController.Session.State}  " +
            $"关卡：{onlineController.Session.LevelId}  " +
            $"角色：{onlineController.Session.Role ?? "未分配"}  " +
            $"Revision：{onlineController.Session.Revision}");
        GUILayout.Label($"最近操作：{lastOperation}");
        DrawSnapshot();

        bool playing =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Playing;
        GUI.enabled = playing;

        if (onlineController.Session.LevelId == 2)
        {
            GUILayout.BeginHorizontal();
            DrawActionButton("KEY_FOUND");
            DrawActionButton("KEY_LANDED");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawActionButton("BOX_BUILT");
            DrawActionButton("DOOR_OPENED");
            GUILayout.EndHorizontal();
        }
        else if (onlineController.Session.LevelId == 3)
        {
            GUILayout.BeginHorizontal();
            DrawActionButton("PASSWORD_SUCCESS");
            DrawActionButton("METAL_FOUND");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawActionButton("METAL_RECEIVED");
            DrawActionButton("WIRE_FOUND");
            DrawActionButton("DOOR_OPENED");
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        GUILayout.Label("重复请求");
        duplicateAction = GUILayout.TextField(duplicateAction);
        duplicateBoolValue = GUILayout.Toggle(duplicateBoolValue, "Bool Value");
        duplicateClientActionId = GUILayout.TextField(duplicateClientActionId);
        if (GUILayout.Button("连续发送两次相同 Client Action ID"))
            SubmitDuplicate();

        GUI.enabled = playing && onlineController.Session.LevelId < 3;
        if (GUILayout.Button("本端已准备进入下一关（要求当前关已完成）"))
        {
            RunOperation(
                async () =>
                {
                    ReadyNextLevelResponse response =
                        await onlineController.ReadyForNextLevelAsync();
                    Debug.Log(
                        $"[M4 验收探针] 换关准备已确认，" +
                        $"levelId={response.levelId}, " +
                        $"readyCount={response.readyCount}/2, " +
                        $"revision={response.revision}。");
                },
                "换关准备");
        }

        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void DrawActionButton(string action)
    {
        if (GUILayout.Button(action))
            SubmitOnce(action);
    }

    private void DrawSnapshot()
    {
        RoomSnapshotDto snapshot = onlineController.Session.Snapshot;
        if (snapshot == null)
        {
            GUILayout.Label("快照：无");
            return;
        }
        if (snapshot.levelId == 2 && snapshot.level2 != null)
        {
            GUILayout.Label(
                $"L2：keyFound={snapshot.level2.keyFound}, " +
                $"keyLanded={snapshot.level2.keyLanded}, " +
                $"boxBuilt={snapshot.level2.boxBuilt}, " +
                $"doorOpened={snapshot.level2.doorOpened}");
        }
        else if (snapshot.levelId == 3 && snapshot.level3 != null)
        {
            GUILayout.Label(
                $"L3：passwordSuccess={snapshot.level3.passwordSuccess}, " +
                $"metalFound={snapshot.level3.metalFound}, " +
                $"metalReceived={snapshot.level3.metalReceived}, " +
                $"wireFound={snapshot.level3.wireFound}, " +
                $"doorOpened={snapshot.level3.doorOpened}");
        }
        else
        {
            GUILayout.Label($"快照：levelId={snapshot.levelId}");
        }
    }

    private void SubmitOnce(string action)
    {
        RunOperation(
            async () =>
            {
                LevelActionResponse response =
                    await onlineController.SubmitLevelActionAsync(action, true);
                Debug.Log(
                    $"[M4 验收探针] 操作完成，action={action}, " +
                    $"revision={response.revision}, changed={response.changed}。");
            },
            action);
    }

    private void SubmitDuplicate()
    {
        RunOperation(
            async () =>
            {
                if (string.IsNullOrWhiteSpace(duplicateAction) ||
                    string.IsNullOrWhiteSpace(duplicateClientActionId))
                {
                    throw new InvalidOperationException(
                        "[M4 验收探针] Action 和 Client Action ID 均不能为空。");
                }

                string action = duplicateAction.Trim();
                string actionId = duplicateClientActionId.Trim();
                LevelActionResponse first =
                    await onlineController.SubmitLevelActionAsync(
                        action,
                        duplicateBoolValue,
                        actionId);
                LevelActionResponse second =
                    await onlineController.SubmitLevelActionAsync(
                        action,
                        duplicateBoolValue,
                        actionId);
                if (!second.duplicate || second.changed ||
                    second.revision != first.revision)
                {
                    throw new InvalidOperationException(
                        $"[M4 验收探针] 重复请求结果非法，" +
                        $"duplicate={second.duplicate}, changed={second.changed}, " +
                        $"firstRevision={first.revision}, secondRevision={second.revision}。");
                }
                Debug.Log(
                    $"[M4 验收探针] PASS：重复请求未改变状态，" +
                    $"action={action}, revision={second.revision}。");
            },
            "重复请求");
    }

    private async void RunOperation(Func<Task> operation, string operationName)
    {
        if (operationRunning)
            throw new InvalidOperationException("[M4 调试面板] 已有操作正在执行。");

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
