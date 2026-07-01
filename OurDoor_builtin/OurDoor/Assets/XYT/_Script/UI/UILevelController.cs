using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public struct StoryData
{
    public string text;
    public int voiceID;

    public float stayTime; // not use

    public StoryData(string text, int voiceID, float stayTime = 2f)
    {
        this.text = text;
        this.voiceID = voiceID;
        this.stayTime = stayTime;
    }
}

public class UILevelController : MonoBehaviour
{
    public static UILevelController Instance;

    [Header("游戏黑幕（切场景）")]
    public Image gameBlackImage;

    [Header("菜单黑幕（暂停菜单）")]
    public Image menuBlackImage;

    [Header("菜单Root")]
    public GameObject menuRoot;

    [Header("场景")]
    public string mainMenuScene = "Start_VR";
    public string nextLevelScene = "";

    Coroutine fadeCoroutine;
    Coroutine menuCoroutine;

    [Header("剧情字幕")]
    public TMP_Text storyText;

    public float fadeTime = 0.5f;
    public float stayTime = 2f;

    [Tooltip("01=L1 2=L2 3=L3")]
    public int levelID = 1;

    bool isPlayingStory = false;

    public Action OnStoryBegin;
    public Action OnStoryBeforeFinished, OnStoryEndFinished;

    private void Awake()
    {
        Instance = this;

        // 游戏黑幕
        if (gameBlackImage != null)
        {
            SetImage(gameBlackImage, 1f, false);
        }

        // 菜单黑幕
        if (menuBlackImage != null)
        {
            SetImage(menuBlackImage, 0f, false);
        }

        if (menuRoot != null)
            menuRoot.SetActive(false);

        // 剧情字幕
        storyText.gameObject.SetActive(false);
        storyText.text = "";

        if(levelID == 1)
        {
            PlayStory(true);
        }
        else if(levelID == 2)
        {
            FadeOutGame();
        }
        else if (levelID == 3)
        {
            FadeOutGame();
        }
    }

    private void Start()
    {
        OnStoryBeforeFinished += () =>
        {
            FadeOutGame();
        };

        OnStoryEndFinished += () =>
        {
            UILevelController.Instance.ShowMenu(); // *
        };
    }

    #region 菜单

    public void ShowMenu()
    {
        if (menuRoot == null)
            return;
        if (isPlayingStory)
            return;

        menuRoot.SetActive(true);

        if (menuCoroutine != null)
            StopCoroutine(menuCoroutine);

        menuCoroutine = StartCoroutine(FadeMenu(0f, 0f));
    }

    public void HideMenu()
    {
        if (menuRoot == null)
            return;

        if (menuCoroutine != null)
            StopCoroutine(menuCoroutine);

        menuCoroutine = StartCoroutine(HideMenuRoutine());
    }

    IEnumerator HideMenuRoutine()
    {
        yield return FadeMenu(0f, 1f);

        menuRoot.SetActive(false);
    }

    IEnumerator FadeMenu(float from, float to)
    {
        float t = 0;
        float tt = 1f;
        Color c = menuBlackImage.color;

        menuBlackImage.raycastTarget = true;

        while (t < tt)
        {
            t += Time.deltaTime;

            c.a = Mathf.Lerp(from, to, t / tt);
            menuBlackImage.color = c;

            yield return null;
        }

        c.a = to;
        menuBlackImage.color = c;

        menuBlackImage.raycastTarget = to > 0;
    }

    #endregion

    #region Button

    public void ReturnToStart()
    {
        StartCoroutine(LoadSceneRoutine(mainMenuScene));
    }

    public void NextLevel()
    {
        if (string.IsNullOrEmpty(nextLevelScene))
            return;

        StartCoroutine(LoadSceneRoutine(nextLevelScene));
    }

    #endregion

    #region 剧情字幕
    public void PlayStory(bool isBeforeGame)
    {
        if (isPlayingStory)
            return;

        StartCoroutine(StoryRoutine(isBeforeGame));
    }
    IEnumerator StoryRoutine(bool isBeforeGame)
    {
        isPlayingStory = true;

        OnStoryBegin?.Invoke();

        storyText.gameObject.SetActive(true);

        Color c = storyText.color;
        c.a = 0;
        storyText.color = c;

        StoryData[] lines = GetStoryLines(levelID, isBeforeGame);

        foreach (StoryData line in lines)
        {
            storyText.text = line.text;

            // 播放对应语音
            // AudioManager.Instance.PlayVoice(line.voiceID);

            // 渐显
            float t = 0;

            while (t < fadeTime)
            {
                t += Time.deltaTime;

                c.a = Mathf.Lerp(0, 1, t / fadeTime);
                storyText.color = c;

                yield return null;
            }

            c.a = 1;
            storyText.color = c;

            yield return new WaitForSeconds(stayTime);

            // 渐隐
            t = 0;

            while (t < fadeTime)
            {
                t += Time.deltaTime;

                c.a = Mathf.Lerp(1, 0, t / fadeTime);
                storyText.color = c;

                yield return null;
            }

            c.a = 0;
            storyText.color = c;

            yield return new WaitForSeconds(0.5f);
        }

        storyText.gameObject.SetActive(false);

        isPlayingStory = false;

        if(isBeforeGame)
        {
            OnStoryBeforeFinished?.Invoke();
        }
        else
        {
            OnStoryEndFinished?.Invoke();
        }

    }

    StoryData[] GetStoryLines(int id, bool isBeforeGame)
    {
        switch (id)
        {
            case 1:
                if (isBeforeGame)
                {
                    return new StoryData[]
{
                new StoryData("那是一个再普通不过的晚上。", -1),
                new StoryData("一场突如其来的停电，", -1),
                new StoryData("把两个不相识的少年隔在了门的两边。", -1),
                new StoryData("那是第一次，他们听见彼此的声音。", -1),
};
                }
                else
                {
                    return new StoryData[]
{
                new StoryData("卷帘门后的微光，照亮了那个漫长的夜晚。", -1),
                new StoryData("我们在那个断电的晚上交换了名字。", -1),
                new StoryData("自此很多故事，都从那一天开始。", -1),
                new StoryData("阳光穿透茂密的梧桐，将少年的影子拉得很长，", -1),
                new StoryData("直到延伸到那扇生锈的铁门前。", -1),
};
                }


            case 2:
                if (isBeforeGame)
                {
                    return new StoryData[]
{
                new StoryData("", -1),
};
                }
                else
                {
                    return new StoryData[]
{
                new StoryData("扔过铁门的钥匙还带着彼此指尖的余温，", -1),
                new StoryData("锈迹摩擦的声响，掩盖了并肩奔跑的呼吸。", -1),
                new StoryData("偷钥匙的慌乱在共同的秘密中消解，", -1),
                new StoryData("我们开始习惯在风里捕捉对方的气息。", -1),
                new StoryData("那些熟络后的挂念，穿过门内外的兽首门环，", -1),
                new StoryData("敲响老家旧宅的木门。", -1),
};
                }

            case 3:
                if (isBeforeGame)
                {
                    return new StoryData[]
{
                new StoryData("", -1),
};
                }
                else
                {
                    return new StoryData[]
{
                new StoryData("\"终于开了！快进来！\"", -1),
                new StoryData("\"我脚都站麻了...\"", -1),
                new StoryData("\"哎呀，下次我肯定把钥匙收好。\"", -1),
                new StoryData("\"没事，反正你都会帮我开门的。\"", -1),
                new StoryData("\"那当然！你可是我第一个请来家里玩的朋友！\"", -1),
                new StoryData("\"嘿嘿，走吧！快带我看你家有啥！\"", -1),
                new StoryData("\"来来来！保证你喜欢！\"", -1),

                new StoryData("", -1), // wait

                new StoryData("沉重的木门终于缓缓打开。", -1),
                new StoryData("门后没有奇迹，只有那张早已熟悉的笑脸。", -1),
                new StoryData("原来这世上的门，并不是为了将谁关在门外，", -1),
                new StoryData("而是让原本陌生的人，终于有机会走向彼此。", -1),
                new StoryData("愿未来的每一次遇见,", -1),
                new StoryData("都有人与你并肩同行。", -1),

                };

                }


            default:
                return new StoryData[0];
        }
    }

    #endregion
    IEnumerator LoadSceneRoutine(string sceneName)
    {
        // 先关闭菜单
        if (menuRoot != null)
        {
            yield return HideMenuRoutine();
        }

        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FadeGame(float from, float to, float duration)
    {
        Color c = gameBlackImage.color;

        float t = 0;

        gameBlackImage.raycastTarget = true;

        while (t < duration)
        {
            t += Time.deltaTime;

            c.a = Mathf.Lerp(from, to, t / duration);

            gameBlackImage.color = c;

            yield return null;
        }

        c.a = to;

        gameBlackImage.color = c;

        gameBlackImage.raycastTarget = to > 0;
    }

    /// <summary>
    /// 新场景开始时调用
    /// </summary>
    public void FadeOutGame()
    {
        StartCoroutine(FadeGame(1, 0, 0.8f));
    }
    public void FadeInGame()
    {
        StartCoroutine(FadeGame(0, 1, 1f));
    }
    public void FadeInGameHalf(float time)
    {
        StartCoroutine(FadeGame(0, 0.6f, time));
    }

    void SetImage(Image image, float alpha = 0f, bool raycast = false)
    {
        if (image == null)
            return;

        Color c = image.color;
        c.a = alpha;
        image.color = c;
        image.raycastTarget = raycast;
    }
}