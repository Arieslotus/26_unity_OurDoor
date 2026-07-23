using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SchoolKey : MonoBehaviour
{
    [Header("拾取")]
    private XRGrabInteractable grab;
    private PCPickupInteractable pick;
    Rigidbody rb;

    private bool picked = false; // 已经从邮箱中拾取

    [Header("抛出")]
    public Transform targetPoint; // 落点
    public Transform trail; // 尾线
    public Transform particles; // 粒子

    Vector3 lastPos;
    Vector3 velocity;

    public float flyDuration = 1.2f;
    public float arcHeight = 2f;

    bool hasThrown = false; // 已经抛出

    [Header("爬墙拿")]
    bool hasPickedFromWall = false; // 已经从墙上拿下
    ClimbController climb;

    void Awake()
    {
        climb = FindObjectOfType<ClimbController>();
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        pick = GetComponent<PCPickupInteractable>();
        grab?.selectEntered.AddListener(OnGrab);
        grab?.selectExited.AddListener(OnRelease);
        pick?.onPickedUp.AddListener(OnGrab);
        pick?.onDropped.AddListener(OnRelease);
        if (grab == null) Debug.LogError("no XRGrabInteractable on key");

        if(trail != null) trail.gameObject.SetActive(false);
        if(particles != null) particles.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        grab?.selectEntered.RemoveListener(OnGrab);
        grab?.selectExited.RemoveListener(OnRelease);
        pick?.onPickedUp.RemoveListener(OnGrab);
        pick?.onDropped.RemoveListener(OnRelease);
    }

    // grab ---
    void OnGrab(SelectEnterEventArgs args)
    {
        OnGrab();
    }
    void OnGrab()
    {
        if (!picked)
        // in mail box
        {
            picked = true;

            if (particles != null) particles.gameObject.SetActive(true);
            StartCoroutine(Wait1());
        }
        else
        {
            // on wall
            if (hasThrown && !hasPickedFromWall)
            {

                if (climb != null)
                {
                    hasPickedFromWall = true;
                    climb.OnGetKey(); // * 拿到钥匙趴下爬下
                    if (particles != null) particles.gameObject.SetActive(false);
                }
                else
                {
                    climb = FindObjectOfType<ClimbController>();
                }
            }
        }

    }

    IEnumerator Wait1()
    {
        yield return new WaitForSeconds(0.5f);
        OurDoorLevelActionGateway.RequestLevel2KeyFound();
    }

    // throw ----

    [ContextMenu("自动抛出")]
    public void AutoThrowKey()
    {
        StartThrowSequence();
    }

    void Update()
    {
        velocity =
            (transform.position - lastPos)
            / Time.deltaTime;

        lastPos = transform.position;
    }
    void OnRelease(SelectExitEventArgs args)
    {
        OnRelease();
    }
    void OnRelease()
    {
        if (velocity.y > 0.6f) /*edit*/ // 越小在vr中越容易抛出
        {
            StartThrowSequence();
        }
    }

    void StartThrowSequence()
    {
        if (hasThrown) return; // 仅第一此有效

        hasThrown = true;

        if(grab != null)
            grab.enabled = false;

        if (trail != null) trail.gameObject.SetActive(true);
        if (particles != null) particles.gameObject.SetActive(true);

        rb.isKinematic = true;

        StartCoroutine(FlyToTarget());
    }

    IEnumerator FlyToTarget()
    {
        Vector3 start = transform.position;
        Vector3 end = targetPoint.position;

        float time = 0;

        while (time < flyDuration)
        {
            time += Time.deltaTime;

            float t = time / flyDuration;

            Vector3 pos = Vector3.Lerp(start, end, t);

            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = pos;

            yield return null;
        }

        transform.position = end;

        OnLand();
    }
    void OnLand()
    {
        if (grab != null)
            grab.enabled = true;

        rb.isKinematic = true;

        if (trail != null) trail.gameObject.SetActive(false);

        StartCoroutine(Wait0());
    }

    IEnumerator Wait0()
    {
        yield return new WaitForSeconds(2f);
        OurDoorLevelActionGateway.RequestLevel2KeyLanded();

        yield return new WaitForSeconds(10f);

        rb.isKinematic = false;
    }
}
