using System.Collections;
using UnityEngine;

public class L3PlayersTextManager : MonoBehaviour
{
    [Header("门外玩家")]
    public PlayerText outerPlayerText;

    [Header("门内玩家")]
    public PlayerText innerPlayerText;

    [Header("对话设置")]
    public float defaultSpeakDuration = 3f;
    public float defaultInterval = 1f;

    private Coroutine currentDialogueCoroutine;

    private void Start()
    {
        // TODO: 换成你的L3Manager事件

        L3Manager.Instance.OnPasswordSuccess += OnPasswordSuccess;
        L3Manager.Instance.OnMetalPieceFound += OnFindMetalPiece;
        L3Manager.Instance.OnMetalPieceReceived += OnGetMetalPiece;
        L3Manager.Instance.OnWireFound += OnFindWire;
        L3Manager.Instance.OnDoorOpened += OnDoorOpen;

        StartCoroutine(WaitAndStart());
    }

    private void OnDestroy()
    {
        // TODO: 换成你的L3Manager事件

        L3Manager.Instance.OnPasswordSuccess -= OnPasswordSuccess;
        L3Manager.Instance.OnMetalPieceFound -= OnFindMetalPiece;
        L3Manager.Instance.OnMetalPieceReceived -= OnGetMetalPiece;
        L3Manager.Instance.OnWireFound -= OnFindWire;
        L3Manager.Instance.OnDoorOpened -= OnDoorOpen;
    }

    IEnumerator WaitAndStart()
    {
        yield return new WaitForSeconds(10f);

        StartSpeakOnStart();
    }

    //==================================================
    // 开始
    //==================================================

    public void StartSpeakOnStart()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(

            //("门外", "咚咚"),
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门内", "外面有人？"),

            ("门外", "是我！我到门口了！"),

            ("门内", "来来来，对暗号！"),

            ("门外", "暗号？"),

            ("门内", "就是那天约好的"),

            ("门内", "敲门环的顺序"),

            ("门外", "让我想想")
        ));
    }

    //==================================================
    // 对暗号成功
    //==================================================

    public void OnPasswordSuccess()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门内", "对的！"),

            ("门外", "开门吧！"),

            ("门内", "我这就给你开门"),

            ("门内", "等等...钥匙找不到了"),

            ("门外", "啊？你钥匙丢了？"),

            ("门内", "今天没人在家..."),

            ("门内", "得找个东西把锁弄开"),

            ("门外", "那我帮你找找能开门的东西")
        ));
    }

    //==================================================
    // 找到铁片
    //==================================================

    public void OnFindMetalPiece()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门外", "我找到了这个！"),

            ("门外", "我从门底给你"),

            ("门内", "你找到了什么！"),

            ("门内", "从门底给我吧")
        ));
    }

    //==================================================
    // 门内拿到铁片
    //==================================================

    public void OnGetMetalPiece()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门内", "我想想办法..."),

            ("门外", "试试能用它干什么"),

            ("门内", "试试能用它干什么")
        ));
    }

    //==================================================
    // 找到铁丝
    //==================================================

    public void OnFindWire()
    {
        StopAllSpeaking();

        currentDialogueCoroutine = StartCoroutine(DialogueSequence(
            () =>
            {
                Debug.Log("对话结束");
            },
            ("门外", "是铁丝！"),

            ("门内", "这里有个铁丝！"),

            ("门外", "似乎可以撬开锁"),

            ("门内", "等我撬开锁")
        ));
    }

    //==================================================
    // 开门
    //==================================================

    public void OnDoorOpen()
    {
        StopAllSpeaking();

        UILevelController.Instance.PlayStory(false); // *

        WaitAndFadeIn();

        //currentDialogueCoroutine = StartCoroutine(DialogueSequence(
        //    () =>
        //    {
        //        Debug.Log("最终对话结束");

        //        UILevelController.Instance.FadeInGameHalf(); // *
        //    },

        //    //("门外", "终于开了！"),
        //    ("门外", ""),

        //    //("门内", "快进来！"),
        //    ("门内", ""),

        //    //("门外", "我脚都站麻了..."),
        //    ("门外", ""),

        //    //("门内", "哎呀，下次我肯定把钥匙收好。"),
        //    ("门内", ""),

        //    //("门外", "没事，反正你都会帮我开门的。"),
        //    ("门外", ""),

        //    //("门内", "那当然！你可是我第一个请来家里玩的朋友！"),
        //    ("门内", ""),

        //    //("门外", "嘿嘿，走吧！快带我看你家有啥！"),
        //    ("门外", ""),

        //    //("门内", "来来来！保证你喜欢！")
        //    ("门内", "")
        //));
    }

    void WaitAndFadeIn()
    {
        //yield return new WaitForSeconds(25f);
        Debug.Log("最终对话结束");
        UILevelController.Instance.FadeInGameHalf(25f); // *
    }

    //==================================================
    // 通用接口
    //==================================================

    public void StopAllSpeaking()
    {
        if (currentDialogueCoroutine != null)
        {
            StopCoroutine(currentDialogueCoroutine);
            currentDialogueCoroutine = null;
        }

        if (outerPlayerText != null && outerPlayerText.gameObject.activeInHierarchy)
            outerPlayerText.StopSpeaking();

        if (innerPlayerText != null && innerPlayerText.gameObject.activeInHierarchy)
            innerPlayerText.StopSpeaking();
    }

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