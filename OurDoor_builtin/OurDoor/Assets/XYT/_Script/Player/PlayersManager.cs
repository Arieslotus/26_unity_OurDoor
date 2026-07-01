using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理不同玩家见的显示区别
/// </summary>
public enum PlayerType
{
    Outer = 1,
    Inner = 0,
}

public class PlayersManager : MonoBehaviour
{
    public static PlayersManager Instance;

    [Header("设置")]
    public PlayerType currentPlayerType = PlayerType.Outer;

    public Transform VRParent;

    [Header("门外 玩家")]
    public PlayerText outerPlayerText;
    public Transform outerPlayerPos;

    [Header("门内 玩家")]
    public PlayerText innerPlayerText;
    public Transform innerPlayerPos;

    private void Awake()
    {
        Instance = this;

        // in or out
        if (GameManager.Instance != null) // 运行时根据 GameManager 的设置决定，但编辑器内可以运行关卡场景手动控制
        {
            currentPlayerType = GameManager.Instance.CurrentPlayerRole == GameManager.PlayerRole.Outside ? PlayerType.Outer : PlayerType.Inner;
        }

        if (currentPlayerType == PlayerType.Outer)
        {
            VRParent.position = outerPlayerPos.position;
            VRParent.rotation = outerPlayerPos.rotation;

            outerPlayerText.gameObject.SetActive(true);
            innerPlayerText.gameObject.SetActive(false);
        }
        else if(currentPlayerType == PlayerType.Inner)
        {
            VRParent.position = innerPlayerPos.position;
            VRParent.rotation = innerPlayerPos.rotation;

            outerPlayerText.gameObject.SetActive(false);
            innerPlayerText.gameObject.SetActive(true);
        }
    }

    [ContextMenu("设置VR位置 Out")]
    public void SetOut()
    {
        VRParent.position = outerPlayerPos.position;
        VRParent.rotation = outerPlayerPos.rotation;
    }

    [ContextMenu("设置VR位置 iN")]
    public void SetiN()
    {
        VRParent.position = innerPlayerPos.position;
        VRParent.rotation = innerPlayerPos.rotation;
    }
}
