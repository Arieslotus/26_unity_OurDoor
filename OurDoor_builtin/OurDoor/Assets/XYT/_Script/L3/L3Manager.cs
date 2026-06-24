using System;
using UnityEngine;

public class L3Manager : MonoBehaviour
{
    public static L3Manager Instance;

    private void Awake()
    {
        Instance = this;

        // init
        PasswordSuccess = false;
        MetalPieceFound = false;
        MetalPieceReceived = false;
        WireFound = false;
        DoorOpened = false;
    }

    //==================================================
    // 状态
    //==================================================

    /// <summary>
    /// 暗号成功
    /// </summary>
    public bool PasswordSuccess { get; private set; }

    /// <summary>
    /// 找到铁片
    /// </summary>
    public bool MetalPieceFound { get; private set; }

    /// <summary>
    /// 门内拿到铁片
    /// </summary>
    public bool MetalPieceReceived { get; private set; }

    /// <summary>
    /// 找到铁丝
    /// </summary>
    public bool WireFound { get; private set; }

    /// <summary>
    /// 门已打开
    /// </summary>
    public bool DoorOpened { get; private set; }

    //==================================================
    // 事件
    //==================================================

    public event Action OnPasswordSuccess;
    public event Action OnMetalPieceFound;
    public event Action OnMetalPieceReceived;
    public event Action OnWireFound;
    public event Action OnDoorOpened;

    //==================================================
    // Set
    //==================================================

    public void SetPasswordSuccess()
    {
        if (PasswordSuccess)
            return;

        PasswordSuccess = true;

        Debug.Log("暗号验证成功");

        OnPasswordSuccess?.Invoke();
    }

    public void SetMetalPieceFound()
    {
        if (MetalPieceFound)
            return;

        MetalPieceFound = true;

        Debug.Log("找到铁片");

        OnMetalPieceFound?.Invoke();
    }

    public void SetMetalPieceReceived()
    {
        if (MetalPieceReceived)
            return;

        MetalPieceReceived = true;

        Debug.Log("门内拿到铁片");

        OnMetalPieceReceived?.Invoke();
    }

    public void SetWireFound()
    {
        if (WireFound)
            return;

        WireFound = true;

        Debug.Log("找到铁丝");

        OnWireFound?.Invoke();
    }

    public void SetDoorOpened()
    {
        if (DoorOpened)
            return;

        DoorOpened = true;

        Debug.Log("门已打开");

        OnDoorOpened?.Invoke();
    }
}