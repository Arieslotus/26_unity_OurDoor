using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shop : MonoBehaviour
{
    [Header("灯")]
    public Transform lightBulb;

    [Header("风扇")]
    public Fan fan;

    private void Start()
    {
        // 订阅事件

        Level1Manager.Instance.OnPowerOn += TurnOn;
        Level1Manager.Instance.OnPowerOff += TurnOff;

        // init
        if (Level1Manager.Instance.PowerOn)
            TurnOn();
        else
            TurnOff();
    }

    void TurnOn()
    {
        // light
        lightBulb.gameObject.SetActive(true);

        // fan
        if(fan != null)
        {
            fan.StartRotation();
        }
    }
    void TurnOff()
    {
        // light
        lightBulb.gameObject.SetActive(false);

        // fan
        if (fan != null)
        {
            fan.StopRotation();
        }
    }


    private void OnDestroy()
    {
        // 取消订阅（非常重要，防止报错）
        if (Level1Manager.Instance != null)
        {
            Level1Manager.Instance.OnPowerOn -= TurnOn;
            Level1Manager.Instance.OnPowerOff -= TurnOff;
        }
    }

}
