using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClimbController : MonoBehaviour
{
    [Header("UI")]
    public Button climbButton;

    [Header("XR")]
    public Transform xrOrigin;

    [Header("Position")]
    public Transform climbPosition;
    public Transform groundPosition;

    [Header("Fade")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Move")]
    public float waitAfterGetKey = 1f;

    bool isMoving = false;

    void Awake()
    {
        if (climbButton != null)
        {
            climbButton.onClick.AddListener(ClickClimbUp);
        }
    }

    void OnDestroy()
    {
        if (climbButton != null)
        {
            climbButton.onClick.RemoveListener(ClickClimbUp);
        }
    }

    public void ClickClimbUp()
    {
        if (isMoving)
            return;

        StartCoroutine(ClimbUpRoutine());

        // hide ui
        var boxManager = FindObjectOfType<BoxManager>();
        if (boxManager != null)
        {
            boxManager.SetClimbUI(false); // *   
        }
    }

    public void OnGetKey()
    {
        if (isMoving)
            return;

        StartCoroutine(ReturnGroundRoutine());
    }

    IEnumerator ClimbUpRoutine()
    {
        isMoving = true;

        yield return Fade(0, 1);

        xrOrigin.position = climbPosition.position;

        yield return Fade(1, 0);

        isMoving = false;
    }

    IEnumerator ReturnGroundRoutine()
    {
        isMoving = true;

        yield return new WaitForSeconds(waitAfterGetKey);

        yield return Fade(0, 1);

        xrOrigin.position = groundPosition.position;

        yield return Fade(1, 0);

        isMoving = false;
    }

    IEnumerator Fade(float from, float to)
    {
        float timer = 0;

        Color c = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float t = timer / fadeDuration;

            c.a = Mathf.Lerp(from, to, t);

            fadeImage.color = c;

            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }
}