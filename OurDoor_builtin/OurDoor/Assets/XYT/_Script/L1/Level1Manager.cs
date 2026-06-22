using System;
using UnityEngine;

public class Level1Manager : MonoBehaviour
{
    public static Level1Manager Instance;

    private void Awake()
    {
        Instance = this;

        // init
        PowerOn = false;
        LockOpened = false;
    }

    // 状态
    public bool PowerOn { get; private set; }
    public bool LockOpened { get; private set; }

    // 事件
    public event Action OnPowerOn;
    public event Action OnPowerOff;
    public event Action OnPassWordFound;
    public event Action OnLockOpened;

    // ===== 对外接口 =====

    public void SetPowerOn(bool isOn)
    {

        if(isOn)
        {
            if (PowerOn) return;

            PowerOn = true;
            Debug.Log("电力已恢复");

            OnPowerOn?.Invoke();
        }
        else
        {
            if (!PowerOn) return;
            PowerOn = false;
            Debug.Log("电力已停");

            OnPowerOff?.Invoke();
        }

    }

    public void SetLockOpened()
    {
        if (LockOpened) return;

        LockOpened = true;
        Debug.Log("密码锁已打开");

        OnLockOpened?.Invoke();
    }

    public void SetPassWordFound()
    {
        Debug.Log("密码已找到");
        OnPassWordFound?.Invoke();
    }
}