using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIStartController : MonoBehaviour
{
    [Header("ºÚÄ»")]
    public Image blackImage;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (blackImage != null)
        {
            Color c = blackImage.color;
            c.a = 0;
            blackImage.color = c;
            blackImage.raycastTarget = false;
        }
    }

    public void FadeIn(float duration = 1f, Action onComplete = null)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine(0, 1, duration, onComplete));
    }

    public void FadeOut(float duration = 1f, Action onComplete = null)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine(1, 0, duration, onComplete));
    }

    IEnumerator FadeRoutine(float from, float to, float duration, Action onComplete)
    {
        if (blackImage == null)
            yield break;

        blackImage.raycastTarget = true;

        Color c = blackImage.color;

        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;

            c.a = Mathf.Lerp(from, to, t / duration);
            blackImage.color = c;

            yield return null;
        }

        c.a = to;
        blackImage.color = c;

        blackImage.raycastTarget = (to > 0.99f);

        onComplete?.Invoke();

        fadeCoroutine = null;
    }
}