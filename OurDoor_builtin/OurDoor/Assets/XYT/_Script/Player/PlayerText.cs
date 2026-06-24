using System.Collections;
using System.Collections.Generic;
using System.Xml;
using TMPro;
using UnityEngine;

public class PlayerText : MonoBehaviour
{
    [Header("文字设置")]
    //public bool enableBackfaceCulling = true; // 勾选表示开启剔除（只显示正面）

    [Tooltip("要显示的文本内容")]
    [SerializeField] private string displayText = "你好，世界！";

    [Tooltip("字体颜色")]
    [SerializeField] private Color textColor = Color.white;

    [Header("渐显设置")]
    [Tooltip("渐显动画时间（秒）")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("打字机效果设置")]
    [Tooltip("打字机效果每个字符间隔时间（秒）")]
    [SerializeField] private float typewriterInterval = 0.05f;

    [Header("渐隐设置")]
    public float autoHideTime = 4f;
    [Tooltip("渐隐动画时间（秒）")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("状态")]
    [SerializeField] private bool isSpeaking = false;

    // 组件引用
    public TextMeshPro tmpText;
    //private CanvasGroup canvasGroup;

    // 协程引用
    private Coroutine currentCoroutine;

    void Awake()
    {
        // 获取或添加 TMP 组件
        tmpText = GetComponent<TextMeshPro>();
        if (tmpText == null)
        {
            tmpText = gameObject.AddComponent<TextMeshPro>();
        }

        //if (tmpText != null)
        //{
        //    // true = 只显示正面， false = 正反面都显示
        //    tmpText.enableCulling = enableBackfaceCulling;
        //}


        // 获取或添加 CanvasGroup（用于控制透明度）
        //canvasGroup = GetComponent<CanvasGroup>();
        //if (canvasGroup == null)
        //{
        //    canvasGroup = gameObject.AddComponent<CanvasGroup>();
        //}

        // 初始化设置
        tmpText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        tmpText.alpha = 0f;
        tmpText.text = "";
    }

    public void SetTextInstant(string text)
    {
        isSpeaking = true;

        tmpText.text = text;
        tmpText.color = Color.white;
        tmpText.alpha = 1f;
    }

    /// <summary>
    /// 开始说话（渐显 + 打字机效果）
    /// </summary>
    /// <param name="text">要说的文字（如果不传，使用默认displayText）</param>
    [ContextMenu("s t")]
    public void StartSpeakingTest()
    {
        StartSpeaking(false);
    }

    public void StartSpeaking(bool isAutoHide, string text = null, float maintainTime = 3f)
    {
        if (isSpeaking)
        {
            Debug.Log("正在说话中，请先调用 StopSpeaking()");
            return;
        }

        // 如果传入了文字，更新显示文字
        if (!string.IsNullOrEmpty(text))
        {
            displayText = text;
        }

        // 停止当前所有协程
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(SpeakCoroutine(isAutoHide, maintainTime));
    }

    /// <summary>
    /// 结束说话（渐隐）
    /// </summary>
    [ContextMenu("e t")]
    public void StopSpeaking()
    {
        if (!isSpeaking)
        {
            Debug.Log("当前没有在说话");
            return;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    /// <summary>
    /// 说话的完整协程（渐显 -> 打字机 -> 等待完成）
    /// </summary>
    private IEnumerator SpeakCoroutine(bool isAutoHide, float maintainTime)
    {
        isSpeaking = true;

        // 清空文字
        tmpText.text = "";

        // 1. 渐显
        yield return StartCoroutine(FadeInCoroutine());

        // 2. 打字机效果
        yield return StartCoroutine(TypewriterCoroutine());

        // 说话完成，自动渐隐（可选）
        if (isAutoHide)
        {
            yield return new WaitForSeconds(maintainTime);

            yield return StartCoroutine(FadeOutCoroutine());

        }

        //isSpeaking = false;
        currentCoroutine = null;
    }

    /// <summary>
    /// 渐显协程
    /// </summary>
    private IEnumerator FadeInCoroutine()
    {
        float elapsedTime = 0f;
        Color startColor = new Color(textColor.r, textColor.g, textColor.b, 0f);
        Color endColor = new Color(textColor.r, textColor.g, textColor.b, 1f);

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeInDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            tmpText.color = Color.Lerp(startColor, endColor, t);
            tmpText.alpha = t;

            yield return null;
        }

        tmpText.color = endColor;
        tmpText.alpha = 1f;
    }

    /// <summary>
    /// 打字机效果协程
    /// </summary>
    private IEnumerator TypewriterCoroutine()
    {
        tmpText.text = "";

        for (int i = 0; i <= displayText.Length; i++)
        {
            tmpText.text = displayText.Substring(0, i);
            yield return new WaitForSeconds(typewriterInterval);
        }
    }

    /// <summary>
    /// 渐隐协程
    /// </summary>
    private IEnumerator FadeOutCoroutine()
    {
        float elapsedTime = 0f;
        Color startColor = tmpText.color;
        Color endColor = new Color(textColor.r, textColor.g, textColor.b, 0f);

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeOutDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            tmpText.color = Color.Lerp(startColor, endColor, t);
            tmpText.alpha = 1f - t;

            yield return null;
        }

        tmpText.color = endColor;
        tmpText.alpha = 0f;
        tmpText.text = "";

        isSpeaking = false;
        currentCoroutine = null;
    }

    /// <summary>
    /// 立即设置文字（无动画）
    /// </summary>
    /// <param name="text">文字内容</param>
    public void SetTextImmediately(string text)
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        displayText = text;
        tmpText.text = text;
        tmpText.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
        tmpText.alpha = 1f;

        isSpeaking = false;
    }

    /// <summary>
    /// 立即隐藏文字（无动画）
    /// </summary>
    public void HideImmediately()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        tmpText.text = "";
        tmpText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        tmpText.alpha = 0f;

        isSpeaking = false;
    }

    /// <summary>
    /// 设置字体颜色
    /// </summary>
    /// <param name="color">新的颜色</param>
    public void SetTextColor(Color color)
    {
        textColor = color;

        // 如果正在显示，立即更新颜色
        if (isSpeaking)
        {
            Color currentColor = tmpText.color;
            tmpText.color = new Color(color.r, color.g, color.b, currentColor.a);
        }
    }

    /// <summary>
    /// 设置要显示的文字内容
    /// </summary>
    /// <param name="text">文字内容</param>
    public void SetDisplayText(string text)
    {
        displayText = text;
    }

    /// <summary>
    /// 获取是否正在说话
    /// </summary>
    public bool IsSpeaking
    {
        get { return isSpeaking; }
    }

    /// <summary>
    /// 获取当前显示的文本
    /// </summary>
    public string CurrentText
    {
        get { return tmpText.text; }
    }
}