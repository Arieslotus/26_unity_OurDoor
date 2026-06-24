using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level2AutoControl : MonoBehaviour
{
    //public bool isAuto = false;
    //bool isAutoInside = true; // true 为 当前是内部玩家自动外部， flase 为当前是外部自动内部

    [Header("For Player Outside")]
    [Header("找到钥匙")]
    public bool isAutoFindKey = false;
    public float timeUntilFindKey = -1f;

    [Header("丢钥匙")]
    public bool isAutoThrowKey = false;
    public float timeUntilThrowKey = -1f;

    [Header("For Player Inside")]
    [Header("搭好箱子")]
    public bool auto_buldBox = false;
    public float t_buildBoxOver = -1f;

    [Header("开门")]
    public bool auto_OpenDoor = false;
    public float t_openDoor = -1f;


    private void Awake()
    {
        //PlayersManager playersManager = FindObjectOfType<PlayersManager>();
        //if(playersManager!= null)
        //{
        //    playersManager.currentPlayerType
        //}
    }
    private void Start()
    {
        // 找到钥匙
        if (isAutoFindKey)
        {
            StartCoroutine(AutoFindKeyRoutine());
        }

        // 丢钥匙
        if (isAutoThrowKey)
        {
            StartCoroutine(AutoThrowKeyRoutine());
        }

        // 搭好箱子
        if (auto_buldBox)
        {
            StartCoroutine(AutoBuildBoxRoutine());
        }


        // 开门
        if (auto_OpenDoor)
        {
            StartCoroutine(AutoOpenDoorRoutine());
        }
    }

    // =========================
    // 找到钥匙
    // =========================
    IEnumerator AutoFindKeyRoutine()
    {
        if (timeUntilFindKey > 0)
            yield return new WaitForSeconds(timeUntilFindKey);

        Debug.Log("[AutoControl] 自动 找到钥匙");
        Level2Manager.Instance.SetKeyFound(); // *
    }

    // =========================
    // 丢钥匙
    // =========================
    IEnumerator AutoThrowKeyRoutine()
    {
        if (timeUntilThrowKey > 0)
            yield return new WaitForSeconds(timeUntilThrowKey);


        var key = FindObjectOfType<SchoolKey>();
        if (key != null)
        {
            key.AutoThrowKey(); // *  落地后调用 l2 manager 函数
            Debug.Log("[AutoControl] 自动 丢钥匙");
        }
    }

    // =========================
    // 搭好箱子
    // =========================
    IEnumerator AutoBuildBoxRoutine()
    {
        if (t_buildBoxOver > 0)
            yield return new WaitForSeconds(t_buildBoxOver);

        Level2Manager.Instance.SetBoxBuild(); // *
        StartCoroutine(WaitAndHideKey());
        Debug.Log("[AutoControl] 自动 搭好箱子");
    }

    IEnumerator WaitAndHideKey()
    {
        yield return new WaitForSeconds(15f);
        var key = FindObjectOfType<SchoolKey>();
        if (key != null)
        {
            key.gameObject.SetActive(false);
        }
    }

    // =========================
    // 开门
    // =========================
    IEnumerator AutoOpenDoorRoutine()
    {
        if (t_openDoor > 0)
            yield return new WaitForSeconds(t_openDoor);

        var door = FindObjectOfType<SchoolDoorController>();
        if (door != null)
        {
            door.AutoOpenDoor(); // * 内涵 l2 manager 函数
            Debug.Log("[AutoControl] 自动 开门");
        }
    }
}
