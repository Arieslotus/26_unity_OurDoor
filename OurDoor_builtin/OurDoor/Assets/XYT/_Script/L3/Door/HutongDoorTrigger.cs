using System.Collections;
using UnityEngine;

public class HutongDoorTrigger : MonoBehaviour
{
    [Header("锁A")]
    public GameObject lockA;

    [Header("锁B")]
    public GameObject lockB;

    [Header("门控制器")]
    public HutongDoorController doorController;

    bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        // 只允许铁丝触发
        var wire = other.GetComponent<Wire>();
        if (wire == null)
            return;

        triggered = true;

        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        Debug.Log("铁丝进入锁孔");

        // 3秒后锁消失
        yield return new WaitForSeconds(3f);

        if (lockA != null)
            lockA.SetActive(false);

        if (lockB != null)
            lockB.SetActive(false);

        // 再等1秒
        yield return new WaitForSeconds(1f);

        if (doorController != null)
            doorController.OpenDoor();

        L3Manager.Instance.SetDoorOpened();
    }


    public void hideLock()
    {
        if (lockA != null)
            lockA.SetActive(false);

        if (lockB != null)
            lockB.SetActive(false);
    }
}