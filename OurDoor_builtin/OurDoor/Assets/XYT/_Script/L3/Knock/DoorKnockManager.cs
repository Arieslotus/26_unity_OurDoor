using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorKnockManager : MonoBehaviour
{
    public static DoorKnockManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    [Header("目标顺序")]
    [Tooltip("左=0 右=1")]
    public List<int> targetSequence = new List<int>()
    {
        0,1,1
    };

    [Header("显示文字")]
    public PlayerText outerPlayerText;

    private List<int> currentSequence = new List<int>();

    private bool isSuccess = false;


    [ContextMenu("test1")]
    public void t1()
    {
        OnKnock(1);
    }
    [ContextMenu("test0")]
    public void t0()
    {
        OnKnock(0);
    }
    /// <summary>
    /// 门环敲击
    /// </summary>
    public void OnKnock(int knockID)
    {
        if (isSuccess)
            return;

        Debug.Log($"敲击: {knockID}");

        int currentIndex = currentSequence.Count;

        // 超出长度
        if (currentIndex >= targetSequence.Count)
        {
            ResetSequence();
            return;
        }

        // 错误
        if (knockID != targetSequence[currentIndex])
        {
            Debug.Log("敲门顺序错误");

            ResetSequence();
            return;
        }

        // 正确
        currentSequence.Add(knockID);

        UpdateKnockText();

        // 完成
        if (currentSequence.Count == targetSequence.Count)
        {
            Success();
        }
    }

    /// <summary>
    /// 更新显示
    /// </summary>
    private void UpdateKnockText()
    {
        if (outerPlayerText == null)
            return;

        string text = "";

        foreach (int id in currentSequence)
        {
            if (id == 0)
                text += "铛——";
            else
                text += "铛！";
        }

        outerPlayerText.SetTextInstant(text);
    }

    /// <summary>
    /// 顺序错误
    /// </summary>
    private void ResetSequence()
    {
        currentSequence.Clear();

        if (outerPlayerText != null)
        {
            outerPlayerText.StopSpeaking();
        }

        Debug.Log("敲门顺序已重置");
    }

    /// <summary>
    /// 暗号成功
    /// </summary>
    private void Success()
    {
        isSuccess = true;

        Debug.Log("暗号成功");

        StartCoroutine(HideTextAfterSuccess());
        OurDoorLevelActionGateway.RequestLevel3PasswordSuccess();
    }

    IEnumerator HideTextAfterSuccess()
    {
        yield return new WaitForSeconds(2f);

        if (outerPlayerText != null)
            outerPlayerText.StopSpeaking();
    }

    /// <summary>
    /// 调试用
    /// </summary>
    [ContextMenu("Reset Knock Sequence")]
    public void ForceReset()
    {
        isSuccess = false;
        ResetSequence();
    }
}
