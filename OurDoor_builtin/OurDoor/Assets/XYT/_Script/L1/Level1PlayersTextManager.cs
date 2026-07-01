using System.Collections;
using UnityEngine;

public class Level1PlayersTextManager : MonoBehaviour
{
    [Header("门外玩家")]
    public PlayerText outerPlayerText;

    [Header("门内玩家")]
    public PlayerText innerPlayerText;

    [Header("对话设置")]
    public float defaultSpeakDuration = 3f;  // 默认每句话显示时长
    public float defaultInterval = 1f;     // 两句话之间的间隔

    private Coroutine currentDialogueCoroutine;

    private void Start()
    {
        // 订阅事件
        Level1Manager.Instance.OnPowerOn += OnPowerOn;
        Level1Manager.Instance.OnLockOpened += OnLockOpen;
        Level1Manager.Instance.OnPassWordFound += OnPassWordFound;

        UILevelController.Instance.OnStoryBeforeFinished += () =>
        {
            StartCoroutine(WaitAndStart());
        };

    }

    private void OnDestroy()
    {
        Level1Manager.Instance.OnPowerOn -= OnPowerOn;
        Level1Manager.Instance.OnLockOpened -= OnLockOpen;
        Level1Manager.Instance.OnPassWordFound -= OnPassWordFound;
    }

    IEnumerator WaitAndStart()
    {
        yield return new WaitForSeconds(10);
        StartSpeakOnStart();
    }
    /// <summary>
    /// 游戏开始时说话（示例）
    /// </summary>
    public void StartSpeakOnStart()
    {
        // 停止当前所有对话
        StopAllSpeaking();

        // 开始新对话
        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门内", "有人吗？"),
            ("门外", "这里有人？"),
            ("门内", "这停电了"),
            ("门内", "我被困在这里了"),
            ("门外", "别着急，我来帮你。"),
            ("门内", "帮帮我")
        ));
    }

    /// <summary>
    /// 通电时说话
    /// </summary>
    public void OnPowerOn()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门外", "通电了！"),
            ("门内", "通电了！"),
            ("门外", "这里有个密码锁。"),
            ("门内", "密码锁？"),
            ("门外", "你知道密码吗？"),
            ("门内", "我找找办法...")
        ));
    }

    /// <summary>
    /// 锁打开时说话
    /// </summary>
    public void OnPassWordFound()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门内", "找到了！"),
            ("门内", "密码是0412！"),
            ("门外", "你找到密码了！"),
            ("门外", "密码是0412！"),
            ("门外", "等我去开门")
        ));
    }

    /// <summary>
    /// 锁打开时说话
    /// </summary>
    public void OnLockOpen()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");

                UILevelController.Instance.FadeInGame(); // *
                UILevelController.Instance.PlayStory(false); // *
            },
            ("门外", "门开了！出来吧。"),
            ("门内", "太好了！"),
            ("门内", "谢谢你！"),
            ("门外", "小事啦，不用谢")
        ));
    }


    /// <summary>
    /// 停止所有对话
    /// </summary>
    public void StopAllSpeaking()
    {
        if (currentDialogueCoroutine != null)
        {
            StopCoroutine(currentDialogueCoroutine);
            currentDialogueCoroutine = null;
        }

        // 停止两个玩家的说话
        if (outerPlayerText != null && outerPlayerText.gameObject.activeInHierarchy)
            outerPlayerText.StopSpeaking();

        if (innerPlayerText != null && innerPlayerText.gameObject.activeInHierarchy)
            innerPlayerText.StopSpeaking();
    }

    /// <summary>
    /// 对话序列协程
    /// </summary>
    private IEnumerator DialogueSequence(System.Action onFinished = null, params (string speaker, string content)[] dialogues)
    {
        foreach (var dialogue in dialogues)
        {
            // 根据说话者选择对应的 PlayerText
            PlayerText targetText = GetPlayerText(dialogue.speaker);

            if (targetText != null && targetText.gameObject.activeInHierarchy)
            {
                targetText.StartSpeaking(false, dialogue.content);
                yield return new WaitForSeconds(defaultSpeakDuration);
                targetText.StopSpeaking();
                yield return new WaitForSeconds(defaultInterval);
            }
        }
        yield return new WaitForSeconds(3); // 最后一次对话后等待一段时间
        onFinished?.Invoke();
    }

    /// <summary>
    /// 根据名字获取对应的 PlayerText
    /// </summary>
    private PlayerText GetPlayerText(string speaker)
    {
        if (speaker == "门外" || speaker == "outer")
            return outerPlayerText;
        if (speaker == "门内" || speaker == "inner")
            return innerPlayerText;

        Debug.LogWarning($"未知的说话者: {speaker}");
        return null;
    }
}