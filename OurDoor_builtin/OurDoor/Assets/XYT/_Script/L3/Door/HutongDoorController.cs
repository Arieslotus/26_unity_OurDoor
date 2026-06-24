using System.Collections;
using UnityEngine;

public class HutongDoorController : MonoBehaviour
{
    [Header("门")]
    public Transform doorL;
    public Transform doorR;

    [Header("开门角度")]
    public float leftOpenAngle = -90f;
    public float rightOpenAngle = 90f;

    [Header("开门时间")]
    public float openDuration = 2f;

    [Header("特效父物体")]
    public GameObject ParticleRoot;

    bool isOpening = false;

    Quaternion leftCloseRot;
    Quaternion rightCloseRot;

    private void Awake()
    {
        leftCloseRot = doorL.localRotation;
        rightCloseRot = doorR.localRotation;

        ParticleRoot.SetActive(false);
    }

    [ContextMenu("开门")]
    public void OpenDoor()
    {
        if (isOpening)
            return;

        StartCoroutine(OpenDoorRoutine());
    }

    IEnumerator OpenDoorRoutine()
    {
        isOpening = true;

        // 粒子
        if (ParticleRoot != null)
            ParticleRoot.SetActive(true);

        Quaternion leftTarget =
            leftCloseRot * Quaternion.Euler(0, leftOpenAngle, 0);

        Quaternion rightTarget =
            rightCloseRot * Quaternion.Euler(0, rightOpenAngle, 0);

        float timer = 0f;

        while (timer < openDuration)
        {
            timer += Time.deltaTime;

            float t = timer / openDuration;
            t = Mathf.SmoothStep(0, 1, t);

            if (doorL != null)
                doorL.localRotation =
                    Quaternion.Slerp(leftCloseRot, leftTarget, t);

            if (doorR != null)
                doorR.localRotation =
                    Quaternion.Slerp(rightCloseRot, rightTarget, t);

            yield return null;
        }

        if (doorL != null)
            doorL.localRotation = leftTarget;

        if (doorR != null)
            doorR.localRotation = rightTarget;
    }
}