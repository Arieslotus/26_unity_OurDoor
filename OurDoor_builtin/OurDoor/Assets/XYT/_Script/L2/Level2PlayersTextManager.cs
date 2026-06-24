using System.Collections;
using UnityEngine;

public class Level2PlayersTextManager : MonoBehaviour
{
    [Header("门外玩家")]
    public PlayerText outerPlayerText;

    [Header("门内玩家")]
    public PlayerText innerPlayerText;

    [Header("对话设置")]
    public float startTalkWaitDuration = 10f; // 开始关卡后多久开始对话
    public float defaultSpeakDuration = 4.5f;  // 默认每句话显示时长
    public float defaultInterval = 1.5f;     // 两句话之间的间隔

    private Coroutine currentDialogueCoroutine;

    private void Start()
    {
        // 订阅事件
        Level2Manager.Instance.OnKeyFound += OnKeyFound;
        Level2Manager.Instance.OnKeyLandWall += OnKeyLand ;
        Level2Manager.Instance.OnBoxBuild += OnBoxBuild;
        Level2Manager.Instance.OnDoorOpen += OnLockOpen;
        StartCoroutine(WaitAndStart());

    }

    private void OnDestroy()
    {
        Level2Manager.Instance.OnKeyFound -= OnKeyFound;
        Level2Manager.Instance.OnKeyLandWall -= OnKeyLand;
        Level2Manager.Instance.OnBoxBuild -= OnBoxBuild;
        Level2Manager.Instance.OnDoorOpen -= OnLockOpen;
    }

    IEnumerator WaitAndStart()
    {
        yield return new WaitForSeconds(startTalkWaitDuration);
        StartSpeakOnStart();
    }
    /// <summary>
    /// 游戏开始时说话
    /// </summary>
    public void StartSpeakOnStart()
    {
        // 停止当前所有对话
        StopAllSpeaking();

        // 开始新对话
        // 门内是校内者，门外是校外者
        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            ("门外", "喂，里面有人吗？"),
            ("门内", "谁啊？"),
            ("门外", "怎么是你！"),
            ("门内", "好耳熟的声音"),
            ("门内", "你怎么在外面？"),

            ("门外", "我正想回来取东西呢"),

            ("门内", "门卫已经走了..."),
            ("门外", "门卫走了啊..."),

            ("门外", "我记得有把备用钥匙"),
            ("门内", "那我找找钥匙"),
            ("门内", "这次换我来帮你！"),
            ("门外", "找找看"),
            ("门外", "我在外面接应你") // 我在外面接应你
        ));
    }

    /// <summary>
    /// 拿到邮箱里的钥匙时说话
    /// </summary>
    public void OnKeyFound()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            ("门外", "太棒了！"),
            ("门内", "找到了！"),
            ("门外", "你找到钥匙了！"),
            ("门内", "我找到钥匙了！"),
            ("门外", "快丢过来扔给我吧！"),
            ("门外", "向上扔，用点力啊"),
            ("门内", "那我丢过去给你"),
            ("门内", "要接住啊")
        ));
    }

    /// <summary>
    /// 钥匙落到墙上
    /// </summary>
    public void OnKeyLand()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            ("门外", "完了！"),
            ("门内", "完蛋了！"),
            ("门外", "钥匙落到墙上了！"),
            ("门内", "钥匙落到墙上了！"),
            ("门内", "好高啊，你够得到吗"),
            ("门外", "太高了，我够不到"),
            ("门外", "该怎么办..."),
            ("门内", "这里有好多纸箱"),
            ("门内", "要不试试用东西垫脚 "),
            ("门外", "我试试用纸箱垫高吧")
        ));
    }

    /// <summary>
    /// 箱子搭好
    /// </summary>
    public void OnBoxBuild()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            ("门外", "搭好了！"),
            ("门内", "太好了！"),
            ("门外", "等我上去拿钥匙开门"),
            ("门内", "拿到钥匙就能开门了"),
            ("门内", "小心点，别摔着")
        ));
    }


    /// <summary>
    /// 门打开
    /// </summary>
    public void OnLockOpen()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            ("门外", "我厉害吧！"),
            ("门内", "又见面了！朋友"),
            ("门外", "今天幸好有你在"),
            ("门内", "我们真有缘"),
            ("门外", "谢了，好兄弟！"),
            ("门内", "还是你厉害！"),
            ("门外", "改天请你吃饭！"),
            ("门内", "改天一起吃饭！")
        ));
    }

    /// <summary>
    /// 自定义对话序列
    /// </summary>
    /// <param name="dialogues">对话数组，每个元素为 (说话者, 内容)</param>
    public void StartCustomDialogue(params (string speaker, string content)[] dialogues)
    {
        StopAllSpeaking();
        currentDialogueCoroutine = StartCoroutine(DialogueSequence(dialogues));
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
    private IEnumerator DialogueSequence(params (string speaker, string content)[] dialogues)
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