using System.Collections;
using UnityEngine;

public class L3AutoControl : MonoBehaviour
{
    public bool isAutoControl = false;

    [Header("For Player Inside")]

    [Header("敲门正确")]
    public float t_passwordSuccess = -1f;
    bool auto_PasswordSuccess = false;

    [Header("找到铁片")]
    public float t_findMetalPiece = -1f;
    bool auto_FindMetalPiece = false;

    [Header("递过铁片")]
    public float t_giveMetalPiece = -1f;
    bool auto_GiveMetalPiece = false;

    [Header("For Player Outside")]

    [Header("找到铁丝")]
    public float t_findWire = -1f;
    bool auto_FindWire = false;

    [Header("开门")]
    public float t_openDoor = -1f;
    bool auto_OpenDoor = false;

    private void Start()
    {
        if (OnlineActionBridge.IsOnline)
            isAutoControl = false;

        // which auto
        if (PlayersManager.Instance.currentPlayerType == PlayerType.Outer)
        {
            auto_PasswordSuccess = false;
            auto_FindMetalPiece = false;
            auto_GiveMetalPiece = false;
            auto_FindWire = true;
            auto_OpenDoor = true;
        }
        else if (PlayersManager.Instance.currentPlayerType == PlayerType.Inner)
        {
            auto_PasswordSuccess = true;
            auto_FindMetalPiece = true;
            auto_GiveMetalPiece = true;
            auto_FindWire = false;
            auto_OpenDoor = false;
        }

        if (!isAutoControl)
            return;

        // 敲门正确
        if (auto_PasswordSuccess)
        {
            StartCoroutine(AutoPasswordSuccessRoutine());
        }


        // 找到铁片
        if (auto_FindMetalPiece)
        {
            StartCoroutine(AutoFindMetalPieceRoutine());
        }

        // 递过铁片
        if (auto_GiveMetalPiece)
        {
            StartCoroutine(AutoGiveMetalPieceRoutine());
        }

        // 找到铁丝
        if (auto_FindWire)
        {
            StartCoroutine(AutoFindWireRoutine());
        }

        // 开门
        if (auto_OpenDoor)
        {
            StartCoroutine(AutoOpenDoorRoutine());
        }
    }

    // =========================
    // 敲门正确
    // =========================
    IEnumerator AutoPasswordSuccessRoutine()
    {
        if (t_passwordSuccess > 0)
            yield return new WaitForSeconds(t_passwordSuccess);
        AutoPasswordSuccess();
    }
    [ContextMenu("AutoPasswordSuccess")]
    public void AutoPasswordSuccess()
    {
        Debug.Log("[AutoControl] 自动 敲门正确");
        L3Manager.Instance.SetPasswordSuccess();
    }
    // =========================
    // 找到铁片
    // =========================

    IEnumerator AutoFindMetalPieceRoutine()
    {
        if (t_findMetalPiece > 0)
            yield return new WaitForSeconds(t_findMetalPiece);

        AutoFindMetalPiece();
    }
    [ContextMenu("AutoFindMetalPiece")]
    public void AutoFindMetalPiece()
    {
        Debug.Log("[AutoControl] 自动 找到铁片");
        L3Manager.Instance.SetMetalPieceFound();
    }
    // =========================
    // 递过铁片
    // =========================

    IEnumerator AutoGiveMetalPieceRoutine()
    {
        if (t_giveMetalPiece > 0)
            yield return new WaitForSeconds(t_giveMetalPiece);

        AutoGiveMetalPiece();
    }
    [ContextMenu("AutoGiveMetalPiece")]
    public void AutoGiveMetalPiece()
    {
        var manager = FindObjectOfType<DoorGapReceiver>();
        if (manager != null)
        {
            manager.GiveMetalPiece(null); // *
        }
        Debug.Log("[AutoControl] 自动 递过铁片");
        L3Manager.Instance.SetMetalPieceReceived();
    }

    // =========================
    // 找到铁丝
    // =========================

    IEnumerator AutoFindWireRoutine()
    {
        if (t_findWire > 0)
            yield return new WaitForSeconds(t_findWire);

        AutoFindWire();
    }
    [ContextMenu("AutoFindWire")]
    public void AutoFindWire()
    {
        Debug.Log("[AutoControl] 自动 找到铁丝");
        L3Manager.Instance.SetWireFound();
    }

    // =========================
    // 开门
    // =========================

    IEnumerator AutoOpenDoorRoutine()
    {
        if (t_openDoor > 0)
            yield return new WaitForSeconds(t_openDoor);

        AutoOpenDoor();
    }
    [ContextMenu("AutoOpenDoor")]
    public void AutoOpenDoor()
    {
        var door = FindObjectOfType<HutongDoorController>();
        if (door != null)
        {
            door.OpenDoor(); // 里面最后记得调用 SetDoorOpened()
        }

        var trigger = FindObjectOfType<HutongDoorTrigger>();
        if (trigger != null)
        {
            trigger.hideLock();
        }
        L3Manager.Instance.SetDoorOpened();
        Debug.Log("[AutoControl] 自动 开门");
    }
}
