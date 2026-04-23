using UnityEngine;
using System.Collections;

public class DoorOfShop : MonoBehaviour
{
    [Header("开门设置")]
    [Tooltip("开门动画时间（秒）")]
    [SerializeField] private float openDuration = 3f;

    [Header("状态")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isAnimating = false;

    // 缓存原始缩放
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        // 订阅事件
        Level1Manager.Instance.OnLockOpened += OpenDoor;

        // 记录原始缩放（Y轴为1时的完整大小）
        originalScale = transform.localScale;
        targetScale = new Vector3(originalScale.x, 0.1f, originalScale.z);


    }

    private void OnDestroy()
    {
        // 取消订阅（非常重要，防止报错）
        if (Level1Manager.Instance != null)
        {
            Level1Manager.Instance.OnLockOpened -= OpenDoor;
        }
    }
    /// <summary>
    /// 开门（Y轴逐渐缩小到0）
    /// </summary>
    //[ContextMenu("tes")]
    public void OpenDoor()
    {
        // 如果已经开门或者正在动画中，不重复执行
        if (isOpen || isAnimating)
            return;

        StartCoroutine(OpenDoorAnimation());
    }

    private IEnumerator OpenDoorAnimation()
    {
        isAnimating = true;

        float elapsedTime = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = targetScale;

        while (elapsedTime < openDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / openDuration;

            // 使用 SmoothStep 让动画更自然
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        // 确保最终位置精确
        transform.localScale = endScale;

        isOpen = true;
        isAnimating = false;

        Debug.Log($"卷帘门已开启，耗时 {openDuration} 秒");
    }

    /// <summary>
    /// 获取门是否已开启
    /// </summary>
    public bool IsOpen
    {
        get { return isOpen; }
    }

    /// <summary>
    /// 获取是否正在动画中
    /// </summary>
    public bool IsAnimating
    {
        get { return isAnimating; }
    }
}