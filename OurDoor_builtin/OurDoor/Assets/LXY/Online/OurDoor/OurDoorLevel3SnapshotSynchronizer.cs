/// <summary>
/// 实现功能：按 revision 将第三关权威快照应用到 L3Manager 和远端最终表现。
/// </summary>
using System;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class OurDoorLevel3SnapshotSynchronizer : IDisposable
{
    private readonly NetworkSession session;
    private bool active;
    private int appliedRevision = -1;

    public OurDoorLevel3SnapshotSynchronizer(NetworkSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Activate()
    {
        if (active)
            throw new InvalidOperationException("[M4 第三关同步] 同步器不能重复启用。");
        if (session.LevelId != 3)
            throw new InvalidOperationException($"[M4 第三关同步] 当前 levelId={session.LevelId}。");
        if (session.Snapshot == null)
            throw new InvalidOperationException("[M4 第三关同步] 启用时缺少初始快照。");

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
            throw new InvalidOperationException("[M4 第三关同步] 同步器未启用。");
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.levelId != 3 || snapshot.level3 == null)
        {
            throw new InvalidOperationException(
                $"[M4 第三关同步] 快照缺少第三关状态，levelId={snapshot.levelId}。");
        }
        if (snapshot.revision <= appliedRevision)
        {
            throw new InvalidOperationException(
                $"[M4 第三关同步] revision 重复或倒退，" +
                $"已应用={appliedRevision}, 收到={snapshot.revision}。");
        }
        ValidateCausality(snapshot.level3, snapshot.revision);

        L3Manager manager = L3Manager.Instance;
        if (manager == null)
            throw new InvalidOperationException("[M4 第三关同步] 场景中缺少 L3Manager。");
        RejectRollback(manager, snapshot.level3, snapshot.revision);

        if (!manager.PasswordSuccess && snapshot.level3.passwordSuccess)
            Apply(OurDoorLevelActions.PasswordSuccess);
        if (!manager.MetalPieceFound && snapshot.level3.metalFound)
            Apply(OurDoorLevelActions.MetalFound);
        if (!manager.MetalPieceReceived && snapshot.level3.metalReceived)
        {
            Apply(OurDoorLevelActions.MetalReceived);
            if (session.Role == "Inner")
                OurDoorLevel3Presentation.ApplyMetalReceivedForInner();
        }
        if (!manager.WireFound && snapshot.level3.wireFound)
        {
            Apply(OurDoorLevelActions.WireFound);
            if (session.Role == "Outer")
                OurDoorLevel3Presentation.ApplyWireFoundForOuter();
        }
        if (!manager.DoorOpened && snapshot.level3.doorOpened)
        {
            Apply(OurDoorLevelActions.DoorOpened);
            if (session.Role == "Outer")
                OurDoorLevel3Presentation.ApplyDoorOpenedForOuter();
        }

        if (manager.PasswordSuccess != snapshot.level3.passwordSuccess ||
            manager.MetalPieceFound != snapshot.level3.metalFound ||
            manager.MetalPieceReceived != snapshot.level3.metalReceived ||
            manager.WireFound != snapshot.level3.wireFound ||
            manager.DoorOpened != snapshot.level3.doorOpened)
        {
            throw new InvalidOperationException(
                $"[M4 第三关同步] 快照应用后 Manager 状态不一致，revision={snapshot.revision}。");
        }

        appliedRevision = snapshot.revision;
        Debug.Log(
            $"[M4 第三关同步] 已应用快照，revision={snapshot.revision}, " +
            $"passwordSuccess={snapshot.level3.passwordSuccess}, " +
            $"metalFound={snapshot.level3.metalFound}, " +
            $"metalReceived={snapshot.level3.metalReceived}, " +
            $"wireFound={snapshot.level3.wireFound}, " +
            $"doorOpened={snapshot.level3.doorOpened}。");
    }

    private static void Apply(string action)
    {
        OurDoorLevelActionGateway.ApplyAuthoritativeResult(
            new OnlineActionIntent(3, action, true));
    }

    private static void ValidateCausality(Level3StateDto state, int revision)
    {
        if (state.metalFound && !state.passwordSuccess ||
            state.metalReceived && !state.metalFound ||
            state.wireFound && !state.metalReceived ||
            state.doorOpened && !state.wireFound)
        {
            throw new InvalidOperationException(
                $"[M4 第三关同步] 快照因果状态非法，revision={revision}。");
        }
    }

    private static void RejectRollback(
        L3Manager manager,
        Level3StateDto state,
        int revision)
    {
        if (manager.PasswordSuccess && !state.passwordSuccess ||
            manager.MetalPieceFound && !state.metalFound ||
            manager.MetalPieceReceived && !state.metalReceived ||
            manager.WireFound && !state.wireFound ||
            manager.DoorOpened && !state.doorOpened)
        {
            throw new InvalidOperationException(
                $"[M4 第三关同步] 权威状态发生不可逆回退，revision={revision}。");
        }
    }
}
