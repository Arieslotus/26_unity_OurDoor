/// <summary>
/// 实现功能：复用现有选关按钮；离线时保持原加载流程，联网时仅选择服务端房间关卡。
/// </summary>
using System;
using UnityEngine;

public sealed class OurDoorOnlineLevelSelectAdapter : MonoBehaviour
{
    [SerializeField] private LevelSelectButton originalLevelButton;
    [SerializeField] private OurDoorOnlineController onlineController;

    private void Awake()
    {
        if (originalLevelButton == null)
        {
            throw new InvalidOperationException(
                $"[M2 选关适配] 对象 {gameObject.name} 未配置 Original Level Button。");
        }
        if (onlineController == null)
        {
            throw new InvalidOperationException(
                $"[M2 选关适配] 对象 {gameObject.name} 未配置 Online Controller。");
        }
    }

    public void OnClick()
    {
        if (GameManager.Instance == null)
            throw new InvalidOperationException("[M2 选关适配] 场景中缺少 GameManager。");

        switch (GameManager.Instance.CurrentGameMode)
        {
            case GameManager.GameMode.SimulateTwoPlayer:
                originalLevelButton.OnClick();
                return;
            case GameManager.GameMode.TwoPlayer:
                if (!GameManager.Instance.IsLevelUnlocked(originalLevelButton.levelID))
                {
                    Debug.LogWarning(
                        $"[M2 选关适配] 拒绝选择尚未解锁的关卡，" +
                        $"对象={gameObject.name}, levelId={originalLevelButton.levelID}。");
                    return;
                }
                onlineController.SelectLevel(originalLevelButton.levelID);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    "CurrentGameMode",
                    $"[M2 选关适配] 未知游戏模式：" +
                    $"{GameManager.Instance.CurrentGameMode}。");
        }
    }
}
