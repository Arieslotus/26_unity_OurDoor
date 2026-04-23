using System.Collections;
using UnityEngine;

/// <summary>
/// 用于自动控制关键事件触发（调试/演示用）
/// </summary>
public class Level1AutoControl : MonoBehaviour
{
    [Header("恢复电力")]
    public bool isAutoPowerOn = false;
    public float timeUntilAutoPowerOn = -1f;

    [Header("找密码")]
    public bool isAutoFindPassWord = false;
    public float timeUntilAutoFound = -1f;

    [Header("开锁")]
    public bool isAutoLockOpen = false;
    public float timeUntilAutoLockOpen = -1f;

    private void Start()
    {
        // 自动恢复电力
        if (isAutoPowerOn)
        {
            StartCoroutine(AutoPowerOnRoutine());
        }

        // 自动找密码
        if (isAutoFindPassWord)
        {
            StartCoroutine(AutoFindPasswordRoutine());
        }

        // 自动开锁
        if (isAutoLockOpen)
        {
            StartCoroutine(AutoLockOpenRoutine());
        }
    }

    // =========================
    // 自动恢复电力
    // =========================
    IEnumerator AutoPowerOnRoutine()
    {
        // 等待时间（如果 <0 就不等）
        if (timeUntilAutoPowerOn > 0)
            yield return new WaitForSeconds(timeUntilAutoPowerOn);

        // 等待条件（比如：还没通电）
        //yield return new WaitUntil(() => !Level1Manager.Instance.PowerOn);

        TriggerPowerOn();
    }

    void TriggerPowerOn()
    {
        Debug.Log("[AutoControl] 自动恢复电力");
        Level1Manager.Instance.SetPowerOn(true); // *
    }

    // =========================
    // 自动找到密码
    // =========================
    IEnumerator AutoFindPasswordRoutine()
    {
        if (timeUntilAutoFound > 0)
        {
            yield return new WaitForSeconds(timeUntilAutoFound);

        }


        var screen = FindObjectOfType<TVScreen>();
        if (screen != null)
        {
            screen.TurnOnTV();

            screen.NextPicture();
            yield return new WaitForSeconds(1);

            screen.NextPicture();
            yield return new WaitForSeconds(1);

            screen.NextPicture();
            yield return new WaitForSeconds(1);

            screen.NextPicture();
            yield return new WaitForSeconds(1);

            screen.NextPicture();
            yield return new WaitForSeconds(1);

            screen.NextPicture();
            yield return new WaitForSeconds(1);

        }

        Level1Manager.Instance.SetPassWordFound(); // *

    }

    // =========================
    // 自动开锁
    // =========================
    IEnumerator AutoLockOpenRoutine()
    {
        if (timeUntilAutoLockOpen > 0)
            yield return new WaitForSeconds(timeUntilAutoLockOpen);

        // 可选：等电力恢复后再开锁（更符合逻辑）
        //yield return new WaitUntil(() => Level1Manager.Instance.PowerOn);

        // 再等：锁还没开
        //yield return new WaitUntil(() => !Level1Manager.Instance.LockOpened);

        TriggerLockOpen();
    }

    void TriggerLockOpen()
    {
        Debug.Log("[AutoControl] 自动打开密码锁");
        Level1Manager.Instance.SetLockOpened(); // *
    }
}