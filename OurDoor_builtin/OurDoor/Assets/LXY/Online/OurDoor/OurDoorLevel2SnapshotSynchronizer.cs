/// <summary>
/// 实现功能：按 revision 将第二关权威快照应用到 Level2Manager 和远端最终表现。
/// </summary>
using System;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class OurDoorLevel2SnapshotSynchronizer : IDisposable
{
    private readonly NetworkSession session;
    private bool active;
    private int appliedRevision = -1;

    public OurDoorLevel2SnapshotSynchronizer(NetworkSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Activate()
    {
        if (active)
            throw new InvalidOperationException("[M4 第二关同步] 同步器不能重复启用。");
        if (session.LevelId != 2)
            throw new InvalidOperationException($"[M4 第二关同步] 当前 levelId={session.LevelId}。");
        if (session.Snapshot == null)
            throw new InvalidOperationException("[M4 第二关同步] 启用时缺少初始快照。");

        session.SnapshotChanged += OnSnapshotChanged;
        active = true;
        try
        {
            ApplySnapshot(session.Snapshot);
        }
        catch
        {
            session.SnapshotChanged -= OnSnapshotChanged;
            active = false;
            throw;
        }
    }

    public void Dispose()
    {
        if (!active)
            return;
        session.SnapshotChanged -= OnSnapshotChanged;
        active = false;
    }

    private void OnSnapshotChanged(RoomSnapshotDto snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(RoomSnapshotDto snapshot)
    {
        if (!active)
            throw new InvalidOperationException("[M4 第二关同步] 同步器未启用。");
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.levelId != 2 || snapshot.level2 == null)
        {
            throw new InvalidOperationException(
                $"[M4 第二关同步] 快照缺少第二关状态，levelId={snapshot.levelId}。");
        }
        if (snapshot.revision <= appliedRevision)
        {
            throw new InvalidOperationException(
                $"[M4 第二关同步] revision 重复或倒退，" +
                $"已应用={appliedRevision}, 收到={snapshot.revision}。");
        }
        ValidateCausality(snapshot.level2, snapshot.revision);

        Level2Manager manager = Level2Manager.Instance;
        if (manager == null)
            throw new InvalidOperationException("[M4 第二关同步] 场景中缺少 Level2Manager。");
        RejectRollback(manager, snapshot.level2, snapshot.revision);

        if (!manager.KeyFound && snapshot.level2.keyFound)
            Apply(OurDoorLevelActions.KeyFound);
        if (!manager.KeyLandWall && snapshot.level2.keyLanded)
        {
            Apply(OurDoorLevelActions.KeyLanded);
            if (session.Role == "Outer")
                OurDoorLevel2Presentation.ApplyKeyLandedForOuter();
        }
        if (!manager.BoxBuild && snapshot.level2.boxBuilt)
        {
            Apply(OurDoorLevelActions.BoxBuilt);
            if (session.Role == "Inner")
                OurDoorLevel2Presentation.ApplyBoxBuiltForInner();
        }
        if (!manager.DoorOpen && snapshot.level2.doorOpened)
        {
            Apply(OurDoorLevelActions.DoorOpened);
            if (session.Role == "Inner")
                OurDoorLevel2Presentation.ApplyDoorOpenedForInner();
        }

        if (manager.KeyFound != snapshot.level2.keyFound ||
            manager.KeyLandWall != snapshot.level2.keyLanded ||
            manager.BoxBuild != snapshot.level2.boxBuilt ||
            manager.DoorOpen != snapshot.level2.doorOpened)
        {
            throw new InvalidOperationException(
                $"[M4 第二关同步] 快照应用后 Manager 状态不一致，revision={snapshot.revision}。");
        }

        appliedRevision = snapshot.revision;
        Debug.Log(
            $"[M4 第二关同步] 已应用快照，revision={snapshot.revision}, " +
            $"keyFound={snapshot.level2.keyFound}, keyLanded={snapshot.level2.keyLanded}, " +
            $"boxBuilt={snapshot.level2.boxBuilt}, doorOpened={snapshot.level2.doorOpened}。");
    }

    private static void Apply(string action)
    {
        OurDoorLevelActionGateway.ApplyAuthoritativeResult(
            new OnlineActionIntent(2, action, true));
    }

    private static void ValidateCausality(Level2StateDto state, int revision)
    {
        if (state.keyLanded && !state.keyFound ||
            state.boxBuilt && !state.keyLanded ||
            state.doorOpened && (!state.keyLanded || !state.boxBuilt))
        {
            throw new InvalidOperationException(
                $"[M4 第二关同步] 快照因果状态非法，revision={revision}。");
        }
    }

    private static void RejectRollback(
        Level2Manager manager,
        Level2StateDto state,
        int revision)
    {
        if (manager.KeyFound && !state.keyFound ||
            manager.KeyLandWall && !state.keyLanded ||
            manager.BoxBuild && !state.boxBuilt ||
            manager.DoorOpen && !state.doorOpened)
        {
            throw new InvalidOperationException(
                $"[M4 第二关同步] 权威状态发生不可逆回退，revision={revision}。");
        }
    }
}
