using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 PC 交互准星和提示文字。
/// </summary>
public class PCInteractionPrompt : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Image crosshair;
    [SerializeField] private TMP_Text promptText;

    [Header("提示文字")]
    [SerializeField] private string defaultPrompt = "交互";

    [Header("准星大小")]
    [Tooltip("没有交互目标时的大小")]
    [SerializeField] private float idleScale = 0.5f;

    [Tooltip("发现交互目标时瞬间放大的大小")]
    [SerializeField] private float popScale = 1.2f;

    [Tooltip("弹跳结束后的正常大小")]
    [SerializeField] private float activeScale = 1f;

    [Header("大小动画")]
    [Tooltip("从当前大小放大到 1.2 倍所需时间")]
    [SerializeField] private float popDuration = 0.08f;

    [Tooltip("从 1.2 倍回落到 1 倍所需时间")]
    [SerializeField] private float settleDuration = 0.12f;

    [Tooltip("失去交互目标后缩小的速度")]
    [SerializeField] private float idleScaleSmoothSpeed = 14f;

    [Header("透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float idleAlpha = 0.1f;

    [Range(0f, 1f)]
    [SerializeField] private float activeAlpha = 1f;

    [Tooltip("透明度渐变速度")]
    [SerializeField] private float alphaSmoothSpeed = 8f;

    private PCInteractor interactor;

    private Vector3 originalScale;
    private float targetAlpha;

    private bool wasInteractable;
    private bool isScaleAnimating;

    private string currentPrompt = "";

    private Coroutine scaleCoroutine;

    private void Awake()
    {
        interactor = FindObjectOfType<PCInteractor>();

        if (crosshair != null)
        {
            // 必须在修改准星大小之前保存原始 Scale。
            originalScale = crosshair.rectTransform.localScale;

            crosshair.rectTransform.localScale =
                originalScale * idleScale;

            SetCrosshairAlphaImmediate(idleAlpha);
            targetAlpha = idleAlpha;
        }

        if (promptText != null)
        {
            promptText.text = string.Empty;
            promptText.gameObject.SetActive(false);
        }

        wasInteractable = false;
    }

    private void Update()
    {
        bool canInteract =
            interactor != null &&
            interactor.CanInteract;

        // 只在状态改变的那一刻调用显示或隐藏。
        if (canInteract != wasInteractable)
        {
            wasInteractable = canInteract;

            if (canInteract)
            {
                RefreshPromptText();
                ShowPrompt();
                PlayInteractionPop();
            }
            else
            {
                HidePrompt();
            }
        }
        else if (canInteract)
        {
            // 交互目标可能从一个物体变成另一个物体，
            // 即使一直处于可交互状态，也要更新文字。
            RefreshPromptText();
        }

        UpdateAlpha();
        UpdateIdleScale();
    }

    private void RefreshPromptText()
    {
        if (interactor == null)
            return;

        string newPrompt =
            interactor.CurrentWorldInteractable
                ?.InteractionPrompt;

        if (string.IsNullOrWhiteSpace(newPrompt))
            newPrompt = defaultPrompt;

        if (newPrompt == currentPrompt)
            return;

        currentPrompt = newPrompt;

        if (promptText != null)
            promptText.text = currentPrompt;
    }

    private void ShowPrompt()
    {
        targetAlpha = activeAlpha;

        if (promptText != null)
        {
            promptText.text =
                string.IsNullOrWhiteSpace(currentPrompt)
                    ? defaultPrompt
                    : currentPrompt;

            promptText.gameObject.SetActive(true);
        }
    }

    private void HidePrompt()
    {
        targetAlpha = idleAlpha;
        currentPrompt = "";

        if (promptText != null)
        {
            promptText.text = string.Empty;
            promptText.gameObject.SetActive(false);
        }

        // 如果准星正在播放弹跳，立刻停止，
        // 然后由 UpdateIdleScale 平滑缩小。
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        isScaleAnimating = false;
    }

    private void PlayInteractionPop()
    {
        if (crosshair == null)
            return;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(
            InteractionPopRoutine()
        );
    }

    private IEnumerator InteractionPopRoutine()
    {
        isScaleAnimating = true;

        RectTransform rectTransform =
            crosshair.rectTransform;

        // 第一段：快速放大到 1.2 倍。
        Vector3 startScale =
            rectTransform.localScale;

        Vector3 popTarget =
            originalScale * popScale;

        float timer = 0f;

        while (timer < popDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = popDuration <= 0f
                ? 1f
                : Mathf.Clamp01(timer / popDuration);

            progress = SmoothStep(progress);

            rectTransform.localScale =
                Vector3.Lerp(
                    startScale,
                    popTarget,
                    progress
                );

            yield return null;
        }

        rectTransform.localScale = popTarget;

        // 第二段：从 1.2 倍回落到 1 倍。
        Vector3 activeTarget =
            originalScale * activeScale;

        timer = 0f;

        while (timer < settleDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = settleDuration <= 0f
                ? 1f
                : Mathf.Clamp01(
                    timer / settleDuration
                );

            progress = SmoothStep(progress);

            rectTransform.localScale =
                Vector3.Lerp(
                    popTarget,
                    activeTarget,
                    progress
                );

            yield return null;
        }

        rectTransform.localScale = activeTarget;

        isScaleAnimating = false;
        scaleCoroutine = null;
    }

    private void UpdateAlpha()
    {
        if (crosshair == null)
            return;

        Color color = crosshair.color;

        color.a = Mathf.MoveTowards(
            color.a,
            targetAlpha,
            alphaSmoothSpeed * Time.unscaledDeltaTime
        );

        crosshair.color = color;
    }

    private void UpdateIdleScale()
    {
        if (crosshair == null || isScaleAnimating)
            return;

        float targetScaleMultiplier =
            wasInteractable
                ? activeScale
                : idleScale;

        Vector3 targetScale =
            originalScale * targetScaleMultiplier;

        float smoothFactor =
            1f - Mathf.Exp(
                -idleScaleSmoothSpeed *
                Time.unscaledDeltaTime
            );

        crosshair.rectTransform.localScale =
            Vector3.Lerp(
                crosshair.rectTransform.localScale,
                targetScale,
                smoothFactor
            );
    }

    private void SetCrosshairAlphaImmediate(
        float alpha)
    {
        if (crosshair == null)
            return;

        Color color = crosshair.color;
        color.a = alpha;
        crosshair.color = color;
    }

    private static float SmoothStep(float value)
    {
        return value * value *
               (3f - 2f * value);
    }

    private void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        isScaleAnimating = false;
    }
}