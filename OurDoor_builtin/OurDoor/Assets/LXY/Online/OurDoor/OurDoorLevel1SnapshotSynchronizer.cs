/// <summary>
/// 实现功能：将第一关权威快照按 revision 顺序应用到原 Level1Manager，并拒绝非法状态回退。
/// </summary>
using System;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

public sealed class OurDoorLevel1SnapshotSynchronizer : IDisposable
{
    private readonly NetworkSession session;
    private bool active;
    private int appliedRevision = -1;

    public OurDoorLevel1SnapshotSynchronizer(NetworkSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Activate()
    {
        if (active)
            throw new InvalidOperationException("[M3 第一关同步] 同步器不能重复启用。");
        if (session.LevelId != 1)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 只能绑定第一关房间，当前 levelId={session.LevelId}。");
        }
        if (session.Snapshot == null)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 启用时缺少初始快照，roomId={session.RoomId}。");
        }

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
            throw new InvalidOperationException("[M3 第一关同步] 同步器未启用。");
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.levelId != 1)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 收到非第一关快照，levelId={snapshot.levelId}。");
        }
        if (snapshot.revision <= appliedRevision)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 收到重复或倒退快照，" +
                $"已应用={appliedRevision}, 收到={snapshot.revision}。");
        }
        if (snapshot.level1 == null)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 快照缺少 level1 状态，" +
                $"roomId={snapshot.roomId}, revision={snapshot.revision}。");
        }
        if (snapshot.level1.lockOpened && !snapshot.level1.passwordFound)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 快照状态非法：已开锁但尚未发现密码，" +
                $"revision={snapshot.revision}。");
        }

        Level1Manager manager = Level1Manager.Instance;
        if (manager == null)
            throw new InvalidOperationException("[M3 第一关同步] 场景中缺少 Level1Manager。");
        if (manager.PasswordFound && !snapshot.level1.passwordFound)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] PasswordFound 出现权威回退，" +
                $"revision={snapshot.revision}。");
        }
        if (manager.LockOpened && !snapshot.level1.lockOpened)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] LockOpened 出现权威回退，" +
                $"revision={snapshot.revision}。");
        }

        if (manager.PowerOn != snapshot.level1.powerOn)
        {
            OurDoorLevelActionGateway.ApplyAuthoritativeResult(
                new OnlineActionIntent(
                    1,
                    OurDoorLevelActions.SetPower,
                    snapshot.level1.powerOn));
        }
        if (!manager.PasswordFound && snapshot.level1.passwordFound)
        {
            OurDoorLevelActionGateway.ApplyAuthoritativeResult(
                new OnlineActionIntent(
                    1,
                    OurDoorLevelActions.PasswordFound,
                    true));
        }
        if (!manager.LockOpened && snapshot.level1.lockOpened)
        {
            OurDoorLevelActionGateway.ApplyAuthoritativeResult(
                new OnlineActionIntent(
                    1,
                    OurDoorLevelActions.LockOpened,
                    true));
        }

        if (manager.PowerOn != snapshot.level1.powerOn ||
            manager.PasswordFound != snapshot.level1.passwordFound ||
            manager.LockOpened != snapshot.level1.lockOpened)
        {
            throw new InvalidOperationException(
                $"[M3 第一关同步] 权威快照应用后 Manager 状态仍不一致，" +
                $"revision={snapshot.revision}, " +
                $"server=({snapshot.level1.powerOn}," +
                $"{snapshot.level1.passwordFound},{snapshot.level1.lockOpened}), " +
                $"local=({manager.PowerOn},{manager.PasswordFound},{manager.LockOpened})。");
        }

        appliedRevision = snapshot.revision;
        Debug.Log(
            $"[M3 第一关同步] 已应用权威快照，roomId={snapshot.roomId}, " +
            $"revision={snapshot.revision}, powerOn={snapshot.level1.powerOn}, " +
            $"passwordFound={snapshot.level1.passwordFound}, " +
            $"lockOpened={snapshot.level1.lockOpened}。");
    }
}
