using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level2Manager : MonoBehaviour
{
    public static Level2Manager Instance;

    private void Awake()
    {
        Instance = this;

        // init
        KeyFound = false;
        KeyLandWall = false;
    }

    // 状态
    public bool KeyFound { get; private set; } // not use
    public bool KeyLandWall { get; private set; }
    bool BoxBuild = false;
    bool DoorOpen = false;

    // 事件
    public event Action OnKeyFound;
    public event Action OnKeyLandWall;
    public event Action OnBoxBuild;
    public event Action OnDoorOpen;


    public void SetKeyFound()
    {
        if (KeyFound) return;

        Debug.Log("钥匙已找到");
        KeyFound = true;
        OnKeyFound?.Invoke();
    }

    public void SetKeyLandOnWall()
    {
        if(KeyLandWall) return;

        Debug.Log("钥匙已落到墙上");
        KeyLandWall = true;
        OnKeyLandWall?.Invoke();
    }

    public void SetBoxBuild()
    {
        if (BoxBuild) return;

        Debug.Log("箱子已搭建好");
        BoxBuild = true;
        OnBoxBuild?.Invoke();
    }

    public void SetDoorOpen()
    {
        if (DoorOpen) return;

        Debug.Log("门已开");
        DoorOpen = true;
        OnDoorOpen?.Invoke();
    }
}
