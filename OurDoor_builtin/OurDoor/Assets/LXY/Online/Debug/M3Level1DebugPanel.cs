/// <summary>
/// 实现功能：提供 M3 第一关合法、错角色、越序和重复 clientActionId 的双客户端验收入口。
/// </summary>
using System;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class M3Level1DebugPanel : MonoBehaviour
{
    [SerializeField] private OurDoorOnlineController onlineController;
    [SerializeField] private string duplicateAction = "SET_POWER";
    [SerializeField] private bool duplicateBoolValue = true;
    [SerializeField] private string duplicateClientActionId;

    private bool operationRunning;
    private string lastOperation = "尚未操作";

    private void Awake()
    {
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M3 调试面板] 对象 {gameObject.name} 未配置 Online Controller。");
        }

        duplicateClientActionId =
            string.IsNullOrWhiteSpace(duplicateClientActionId)
                ? "M3-DUP-" + Guid.NewGuid().ToString("N")
                : duplicateClientActionId.Trim();
    }

    private void OnGUI()
    {
        const float width = 540f;
        GUILayout.BeginArea(
            new Rect(Screen.width - width - 20f, 20f, width, Screen.height - 40f),
            GUI.skin.box);
        GUILayout.Label("OurDoor M3 第一关权威同步验收");
        GUILayout.Label(
            $"状态：{onlineController.Session.State}  " +
            $"角色：{onlineController.Session.Role ?? "未分配"}  " +
            $"Revision：{onlineController.Session.Revision}");
        GUILayout.Label($"最近操作：{lastOperation}");
        DrawSnapshot();

        bool canSubmit =
            !operationRunning &&
            onlineController.Session.State == OnlineSessionState.Playing &&
            onlineController.Session.LevelId == 1;
        GUI.enabled = canSubmit;

        GUILayout.Space(8f);
        GUILayout.Label("单次操作（也用于错角色、越序验收）");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SET_POWER(true)"))
            SubmitOnce("SET_POWER", true);
        if (GUILayout.Button("SET_POWER(false)"))
            SubmitOnce("SET_POWER", false);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("PASSWORD_FOUND"))
            SubmitOnce("PASSWORD_FOUND", true);
        if (GUILayout.Button("LOCK_OPENED"))
            SubmitOnce("LOCK_OPENED", true);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("重复请求验收");
        GUILayout.Label("Action");
        duplicateAction = GUILayout.TextField(duplicateAction);
        duplicateBoolValue =
            GUILayout.Toggle(duplicateBoolValue, "Bool Value");
        GUILayout.Label("固定 Client Action ID");
        duplicateClientActionId =
            GUILayout.TextField(duplicateClientActionId);
        if (GUILayout.Button("连续发送两次相同 Client Action ID"))
            SubmitDuplicate();

        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void DrawSnapshot()
    {
        var snapshot = onlineController.Session.Snapshot;
        if (snapshot == null)
        {
            GUILayout.Label("快照：无");
            return;
        }
        if (snapshot.level1 == null)
        {
            GUILayout.Label(
                $"快照：revision={snapshot.revision}，缺少 level1（M3 配置错误）");
            return;
        }

        GUILayout.Label(
            $"快照：powerOn={snapshot.level1.powerOn}, " +
            $"passwordFound={snapshot.level1.passwordFound}, " +
            $"lockOpened={snapshot.level1.lockOpened}");
    }

    private void SubmitOnce(string action, bool boolValue)
    {
        RunOperation(
            async () =>
            {
                LevelActionResponse response =
                    await onlineController.SubmitLevelActionAsync(
                        action,
                        boolValue);
                Debug.Log(
                    $"[M3 验收探针] 单次操作完成，action={action}, " +
                    $"revision={response.revision}, changed={response.changed}, " +
                    $"duplicate={response.duplicate}。");
            },
            action);
    }

    private void SubmitDuplicate()
    {
        RunOperation(
            async () =>
            {
                if (string.IsNullOrWhiteSpace(duplicateAction))
                    throw new InvalidOperationException("[M3 验收探针] Action 不能为空。");
                if (string.IsNullOrWhiteSpace(duplicateClientActionId))
                {
                    throw new InvalidOperationException(
                        "[M3 验收探针] Client Action ID 不能为空。");
                }

                string actionId = duplicateClientActionId.Trim();
                LevelActionResponse first =
                    await onlineController.SubmitLevelActionAsync(
                        duplicateAction.Trim(),
                        duplicateBoolValue,
                        actionId);
                LevelActionResponse second =
                    await onlineController.SubmitLevelActionAsync(
                        duplicateAction.Trim(),
                        duplicateBoolValue,
                        actionId);

                if (!second.duplicate)
                {
                    throw new InvalidOperationException(
                        $"[M3 验收探针] 第二次请求未被标记为重复，" +
                        $"clientActionId={actionId}。");
                }
                if (second.changed)
                {
                    throw new InvalidOperationException(
                        $"[M3 验收探针] 重复请求错误地改变了状态，" +
                        $"clientActionId={actionId}。");
                }
                if (second.revision != first.revision)
                {
                    throw new InvalidOperationException(
                        $"[M3 验收探针] 重复请求改变了 revision，" +
                        $"first={first.revision}, second={second.revision}。");
                }

                Debug.Log(
                    $"[M3 验收探针] PASS：重复请求未改变状态，" +
                    $"clientActionId={actionId}, revision={second.revision}。");
            },
            "重复 Client Action ID");
    }

    private async void RunOperation(Func<Task> operation, string operationName)
    {
        if (operationRunning)
            throw new InvalidOperationException("[M3 调试面板] 已有操作正在执行。");

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
