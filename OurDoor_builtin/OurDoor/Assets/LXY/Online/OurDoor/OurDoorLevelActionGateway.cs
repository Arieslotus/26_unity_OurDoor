using UnityEngine;

/// <summary>
/// 实现功能：统一三关的操作意图提交与服务端权威结果应用，并保留离线直调流程。
/// </summary>
public static class OurDoorLevelActionGateway
{
    public static void RequestLevel1Power(bool isOn)
    {
        RequestOrApply(1, OurDoorLevelActions.SetPower, isOn, delegate
        {
            RequireLevel1Manager().SetPowerOn(isOn);
        });
    }

    public static void RequestLevel1PasswordFound()
    {
        RequestOrApply(1, OurDoorLevelActions.PasswordFound, true, delegate
        {
            RequireLevel1Manager().SetPassWordFound();
        });
    }

    public static void RequestLevel1LockOpened()
    {
        RequestOrApply(1, OurDoorLevelActions.LockOpened, true, delegate
        {
            RequireLevel1Manager().SetLockOpened();
        });
    }

    public static void RequestLevel2KeyFound()
    {
        RequestOrApply(2, OurDoorLevelActions.KeyFound, true, delegate
        {
            RequireLevel2Manager().SetKeyFound();
        });
    }

    public static void RequestLevel2KeyLanded()
    {
        RequestOrApply(2, OurDoorLevelActions.KeyLanded, true, delegate
        {
            RequireLevel2Manager().SetKeyLandOnWall();
        });
    }

    public static void RequestLevel2BoxBuilt()
    {
        RequestOrApply(2, OurDoorLevelActions.BoxBuilt, true, delegate
        {
            RequireLevel2Manager().SetBoxBuild();
        });
    }

    public static void RequestLevel2DoorOpened()
    {
        RequestOrApply(2, OurDoorLevelActions.DoorOpened, true, delegate
        {
            RequireLevel2Manager().SetDoorOpen();
        });
    }

    public static void RequestLevel3PasswordSuccess()
    {
        RequestOrApply(3, OurDoorLevelActions.PasswordSuccess, true, delegate
        {
            RequireLevel3Manager().SetPasswordSuccess();
        });
    }

    public static void RequestLevel3MetalFound()
    {
        RequestOrApply(3, OurDoorLevelActions.MetalFound, true, delegate
        {
            RequireLevel3Manager().SetMetalPieceFound();
        });
    }

    public static void RequestLevel3MetalReceived()
    {
        if (OnlineActionBridge.IsOnline &&
            RequireLevel3Manager().MetalPieceReceived)
        {
            Debug.Log(
                "[M4 关卡同步] METAL_RECEIVED 已由服务端确认，" +
                "Inner 拿起门缝铁片时不重复提交。");
            return;
        }

        RequestOrApply(3, OurDoorLevelActions.MetalReceived, true, delegate
        {
            RequireLevel3Manager().SetMetalPieceReceived();
        });
    }

    public static void RequestLevel3WireFound()
    {
        RequestOrApply(3, OurDoorLevelActions.WireFound, true, delegate
        {
            RequireLevel3Manager().SetWireFound();
        });
    }

    public static void RequestLevel3DoorOpened()
    {
        RequestOrApply(3, OurDoorLevelActions.DoorOpened, true, delegate
        {
            RequireLevel3Manager().SetDoorOpened();
        });
    }

    public static bool ApplyAuthoritativeResult(OnlineActionIntent result)
    {
        if (result == null)
            throw new System.ArgumentNullException("result");

        switch (result.LevelId)
        {
            case 1:
                return ApplyLevel1(result);
            case 2:
                return ApplyLevel2(result);
            case 3:
                return ApplyLevel3(result);
            default:
                throw new System.ArgumentOutOfRangeException(
                    "result",
                    "[关卡同步] 收到未知关卡结果，levelId=" + result.LevelId);
        }
    }

    private static void RequestOrApply(
        int levelId,
        string action,
        bool boolValue,
        System.Action offlineApply)
    {
        if (OnlineActionBridge.IsOnline)
        {
            bool accepted = OnlineActionBridge.TrySubmit(
                new OnlineActionIntent(levelId, action, boolValue));

            if (!accepted)
            {
                throw new System.InvalidOperationException(
                    "[关卡同步] 联网操作发送器拒绝操作，levelId=" + levelId +
                    ", action=" + action);
            }

            return;
        }

        offlineApply();
    }

    private static bool ApplyLevel1(OnlineActionIntent result)
    {
        Level1Manager manager = RequireLevel1Manager();

        switch (result.Action)
        {
            case OurDoorLevelActions.SetPower:
                manager.SetPowerOn(result.BoolValue);
                return true;
            case OurDoorLevelActions.PasswordFound:
                manager.SetPassWordFound();
                return true;
            case OurDoorLevelActions.LockOpened:
                manager.SetLockOpened();
                return true;
            default:
                throw UnknownAction(result);
        }
    }

    private static bool ApplyLevel2(OnlineActionIntent result)
    {
        Level2Manager manager = RequireLevel2Manager();

        switch (result.Action)
        {
            case OurDoorLevelActions.KeyFound:
                manager.SetKeyFound();
                return true;
            case OurDoorLevelActions.KeyLanded:
                manager.SetKeyLandOnWall();
                return true;
            case OurDoorLevelActions.BoxBuilt:
                manager.SetBoxBuild();
                return true;
            case OurDoorLevelActions.DoorOpened:
                manager.SetDoorOpen();
                return true;
            default:
                throw UnknownAction(result);
        }
    }

    private static bool ApplyLevel3(OnlineActionIntent result)
    {
        L3Manager manager = RequireLevel3Manager();

        switch (result.Action)
        {
            case OurDoorLevelActions.PasswordSuccess:
                manager.SetPasswordSuccess();
                return true;
            case OurDoorLevelActions.MetalFound:
                manager.SetMetalPieceFound();
                return true;
            case OurDoorLevelActions.MetalReceived:
                manager.SetMetalPieceReceived();
                return true;
            case OurDoorLevelActions.WireFound:
                manager.SetWireFound();
                return true;
            case OurDoorLevelActions.DoorOpened:
                manager.SetDoorOpened();
                return true;
            default:
                throw UnknownAction(result);
        }
    }

    private static Level1Manager RequireLevel1Manager()
    {
        if (Level1Manager.Instance == null)
            throw new System.InvalidOperationException("[关卡同步] 场景中缺少 Level1Manager。");

        return Level1Manager.Instance;
    }

    private static Level2Manager RequireLevel2Manager()
    {
        if (Level2Manager.Instance == null)
            throw new System.InvalidOperationException("[关卡同步] 场景中缺少 Level2Manager。");

        return Level2Manager.Instance;
    }

    private static L3Manager RequireLevel3Manager()
    {
        if (L3Manager.Instance == null)
            throw new System.InvalidOperationException("[关卡同步] 场景中缺少 L3Manager。");

        return L3Manager.Instance;
    }

    private static System.Exception UnknownAction(OnlineActionIntent result)
    {
        return new System.ArgumentOutOfRangeException(
            "result",
            "[关卡同步] 收到未知操作，levelId=" + result.LevelId +
            ", action=" + result.Action);
    }
}
