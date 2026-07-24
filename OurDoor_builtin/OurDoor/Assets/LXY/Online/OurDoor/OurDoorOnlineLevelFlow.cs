/// <summary>
/// 实现功能：监听本关结束剧情完成事件，并向服务端提交本端换关准备。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Services;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class OurDoorOnlineLevelFlow : IDisposable
{
    private readonly LevelFlowService service;
    private readonly NetworkSession session;
    private readonly CancellationToken cancellationToken;
    private UILevelController uiController;
    private bool active;
    private bool readySubmitted;

    public OurDoorOnlineLevelFlow(
        LevelFlowService service,
        NetworkSession session,
        CancellationToken cancellationToken)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.cancellationToken = cancellationToken;
    }

    public void Activate()
    {
        if (active)
            throw new InvalidOperationException("[M4 连续关卡] 关卡流程不能重复启用。");
        if (session.LevelId < 1 || session.LevelId >= 3)
        {
            throw new InvalidOperationException(
                $"[M4 连续关卡] 当前关卡不需要换关监听，levelId={session.LevelId}。");
        }

        uiController = UILevelController.Instance;
        if (uiController == null)
            throw new InvalidOperationException("[M4 连续关卡] 场景中缺少 UILevelController。");
        if (uiController.levelID != session.LevelId)
        {
            throw new InvalidOperationException(
                $"[M4 连续关卡] UI 关卡与会话不一致，" +
                $"ui={uiController.levelID}, session={session.LevelId}。");
        }

        uiController.OnStoryEndFinished += OnStoryEndFinished;
        active = true;
    }

    public Task<ReadyNextLevelResponse> ReadyNowAsync()
    {
        return SubmitReadyAsync("手动验收");
    }

    public void Dispose()
    {
        if (!active)
            return;

        uiController.OnStoryEndFinished -= OnStoryEndFinished;
        uiController = null;
        active = false;
    }

    private async void OnStoryEndFinished()
    {
        if (readySubmitted)
        {
            Debug.Log(
                $"[M4 连续关卡] 本端已通过手动验收入口提交准备，" +
                $"结束剧情不再重复提交，levelId={session.LevelId}。");
            return;
        }

        try
        {
            await SubmitReadyAsync("结束剧情");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[M4 连续关卡] 结束剧情后提交换关准备失败，" +
                $"roomId={session.RoomId}, levelId={session.LevelId}。");
            Debug.LogException(exception);
        }
    }

    private async Task<ReadyNextLevelResponse> SubmitReadyAsync(string source)
    {
        if (!active)
            throw new InvalidOperationException("[M4 连续关卡] 关卡流程尚未启用。");
        if (readySubmitted)
        {
            throw new InvalidOperationException(
                $"[M4 连续关卡] 本端已经提交换关准备，" +
                $"roomId={session.RoomId}, levelId={session.LevelId}。");
        }

        readySubmitted = true;
        try
        {
            ReadyNextLevelResponse response =
                await service.ReadyForNextLevelAsync(cancellationToken);
            Debug.Log(
                $"[M4 连续关卡] 本端换关准备已确认，source={source}, " +
                $"roomId={response.roomId}, levelId={response.levelId}, " +
                $"readyCount={response.readyCount}/2, revision={response.revision}。");
            return response;
        }
        catch
        {
            readySubmitted = false;
            throw;
        }
    }
}
