using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SchoolDoorController : MonoBehaviour
{
    [Header("Key")]
    public Transform keyForAnima;

    [Header("Door")]
    public Transform DoorL, DoorR;

    [Header("Door Open")]
    public float doorOpenDuration = 1.5f;

    private bool isOpening = false;

    private Quaternion doorLStartRot;
    private Quaternion doorRStartRot;

    private Quaternion doorLEndRot;
    private Quaternion doorREndRot;

    [Header("Particle")]
    public Transform particles;

    private void Start()
    {
        keyForAnima.gameObject.SetActive(false);
        particles.gameObject.SetActive(false);

        doorLStartRot = DoorL.localRotation;
        doorRStartRot = DoorR.localRotation;
        doorLEndRot = doorLStartRot * Quaternion.Euler(0, -70, 0);
        doorREndRot = doorRStartRot * Quaternion.Euler(0, 70, 0);
    }

    [ContextMenu("自动开门")]
    public void AutoOpenDoor()
    {
        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpening)
            return;

        keyForAnima.gameObject.SetActive(true);

        StartCoroutine(WaitKeyAnimaAndOpenDoor());
    }

    IEnumerator WaitKeyAnimaAndOpenDoor()
    {
        isOpening = true;

        yield return new WaitForSeconds(3.5f); // time

        Level2Manager.Instance.SetDoorOpen(); // * talk
        particles.gameObject.SetActive(true);

        float timer = 0;

        while (timer < doorOpenDuration)
        {
            timer += Time.deltaTime;

            float t = timer / doorOpenDuration;

            // 缓动
            t = Mathf.SmoothStep(0f, 1f, t);

            DoorL.localRotation =
                Quaternion.Lerp(
                    doorLStartRot,
                    doorLEndRot,
                    t);

            DoorR.localRotation =
                Quaternion.Lerp(
                    doorRStartRot,
                    doorREndRot,
                    t);

            yield return null;
        }

        DoorL.localRotation = doorLEndRot;
        DoorR.localRotation = doorREndRot;

        isOpening = false;
    }
}
