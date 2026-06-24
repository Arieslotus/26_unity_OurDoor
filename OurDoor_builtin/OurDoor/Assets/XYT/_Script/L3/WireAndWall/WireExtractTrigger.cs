using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WireExtractTrigger : MonoBehaviour
{
    [Header("铁片")]
    public InsideMetalPiece targetMetalPiece;

    [Header("铁丝")]
    public Transform wireTransform;

    [Header("铁丝起点")]
    public Transform wireStartPoint;

    [Header("铁丝终点")]
    public Transform wireEndPoint;

    [Header("完成时间")]
    public float requiredTime = 10f;

    [Header("晃动阈值")]
    public float shakeSpeedThreshold = 0.2f;

    [Header("完成后掉落")]
    Rigidbody wireRb;

    [Header("完成后开启拾取")]
    XRGrabInteractable wireGrab;

    [SerializeField] float currentProgressTime;

    bool metalInsideTrigger;

    Vector3 lastMetalPos;

    bool completed;

    private void Start()
    {
        wireTransform.position = wireStartPoint.position;

        wireRb = wireTransform.GetComponent<Rigidbody>();
        wireGrab = wireTransform.GetComponent<XRGrabInteractable>();
        if (wireRb != null)
        {
            wireRb.isKinematic = true;
            wireRb.useGravity = false;
        }

        if (wireGrab != null)
        {
            wireGrab.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (completed)
            return;

        if (other.gameObject == targetMetalPiece.gameObject)
        {
            metalInsideTrigger = true;
            lastMetalPos = targetMetalPiece.transform.position;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == targetMetalPiece.gameObject)
        {
            metalInsideTrigger = false;
        }
    }

    private void Update()
    {
        if (completed)
            return;

        if (!metalInsideTrigger)
            return;

        if (!targetMetalPiece.isPicking)
            return;

        Vector3 currentPos = targetMetalPiece.transform.position;

        float speed =
            Vector3.Distance(currentPos, lastMetalPos)
            / Mathf.Max(Time.deltaTime, 0.0001f);

        lastMetalPos = currentPos;

        if (speed > shakeSpeedThreshold)
        {
            currentProgressTime += Time.deltaTime;
        }

        float progress =
            Mathf.Clamp01(currentProgressTime / requiredTime);

        wireTransform.position =
            Vector3.Lerp(
                wireStartPoint.position,
                wireEndPoint.position,
                progress);
        wireTransform.rotation =
            Quaternion.Lerp(
                wireStartPoint.rotation,
                wireEndPoint.rotation,
                progress);

        if (progress >= 1f)
        {
            CompleteExtract();
        }
    }

    void CompleteExtract()
    {
        completed = true;

        Debug.Log("铁丝已勾出");

        // set
        wireTransform.position = wireEndPoint.position;
        wireTransform.rotation = wireEndPoint.rotation;

        // state
        if (wireRb != null)
        {
            wireRb.isKinematic = false;
            wireRb.useGravity = true;
        }

        if (wireGrab != null)
        {
            wireGrab.enabled = true;
        }

        L3Manager.Instance.SetWireFound();
    }
}