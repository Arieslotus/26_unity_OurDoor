using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameMode
    {
        SimulateTwoPlayer,   // 模拟双人
        //TwoPlayer,           // 双人模式
        //SinglePlayer         // 单人模式
    }

    public enum PlayerRole
    {
        Outside,    // 门外
        Inside      // 门内
    }

    [Header("当前游戏设置")]
    public GameMode CurrentGameMode = GameMode.SimulateTwoPlayer;
    public PlayerRole CurrentPlayerRole = PlayerRole.Outside;

    [Header("音量")]
    [Range(0, 1)]
    public float BGMVolume = 1;

    [Range(0, 1)]
    public float SFXVolume = 1;

    [Range(0, 1)]
    public float VoiceVolume = 1;

    [Header("关卡")]
    public const string LEVEL1_KEY = "Level1Unlock";
    public const string LEVEL2_KEY = "Level2Unlock";
    public const string LEVEL3_KEY = "Level3Unlock";


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        if (!PlayerPrefs.HasKey(LEVEL1_KEY))
        {
            PlayerPrefs.SetInt(LEVEL1_KEY, 1);
            PlayerPrefs.SetInt(LEVEL2_KEY, 0);
            PlayerPrefs.SetInt(LEVEL3_KEY, 0);
            PlayerPrefs.Save();
        }
    }

    // settings

    public void SetGameMode(GameMode mode)
    {
        CurrentGameMode = mode;
        Debug.Log("模式：" + mode);
    }

    public void SetPlayerRole(PlayerRole role)
    {
        CurrentPlayerRole = role;
        Debug.Log("玩家：" + role);
    }

    // level save
    [ContextMenu("Clear Save")]
    public void ClearSave()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("存档已删除");
    }
    public bool IsLevelUnlocked(int level)
    {
        switch (level)
        {
            case 1: return PlayerPrefs.GetInt(LEVEL1_KEY, 1) == 1;
            case 2: return PlayerPrefs.GetInt(LEVEL2_KEY, 0) == 1;
            case 3: return PlayerPrefs.GetInt(LEVEL3_KEY, 0) == 1;
        }
        return false;
    }
    public void UnlockLevel(int level)
    {
        switch (level)
        {
            case 2:
                PlayerPrefs.SetInt(LEVEL2_KEY, 1);
                break;

            case 3:
                PlayerPrefs.SetInt(LEVEL3_KEY, 1);
                break;
        }
        PlayerPrefs.Save();
    }

    // level
    public bool IsLevelAutoControl()
    {
        return CurrentGameMode == GameMode.SimulateTwoPlayer;
    }
}