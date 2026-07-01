using TMPro;
using UnityEngine;

public class LookAtTitle : MonoBehaviour
{
    public Transform xrCamera;

    public float showDot = 0.9f;
    public float hideDot = 0.86f;
    public float moveDistance = 0.3f;

    public float moveSpeed = 5;

    Vector3 showPos;
    Vector3 hidePos;
    bool isShowing = false;

    CanvasGroup group;
    public Transform textParent;
    TMP_Text[] texts;


    void Start()
    {
        xrCamera = Camera.main.transform;

        showPos = transform.localPosition;
        hidePos = showPos + Vector3.down * moveDistance;

        //group = GetComponent<CanvasGroup>();

        transform.localPosition = hidePos;

        if (group != null)
            group.alpha = 0;
        texts = textParent.GetComponentsInChildren<TMP_Text>();

    }

    void Update()
    {
        Vector3 dir =
            (transform.position - xrCamera.position).normalized;

        float dot =
            Vector3.Dot(xrCamera.forward, dir);

        if (!isShowing)
        {
            if (dot > showDot)
                isShowing = true;
        }
        else
        {
            if (dot < hideDot)
                isShowing = false;
        }

        bool show = isShowing;
        //bool show = dot > showDot;

        Vector3 targetPos = show ? showPos : hidePos;

        transform.localPosition =
            Vector3.Lerp(transform.localPosition,
                targetPos,
                Time.deltaTime * moveSpeed);



        if (group != null)
        {
            float target = show ? 1 : 0;
            group.alpha =
                Mathf.Lerp(group.alpha,
                    target,
                    Time.deltaTime * moveSpeed);
        }
        else
        {
            if(textParent != null)
            {
                float targetAlpha = show ? 1 : 0;

                foreach (var t in texts)
                {
                    Color c = t.color;
                    c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * moveSpeed);
                    t.color = c;
                }
            }
        }
    }
}