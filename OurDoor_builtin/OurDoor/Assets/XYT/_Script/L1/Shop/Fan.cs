using UnityEngine;

public class Fan : MonoBehaviour
{
    [Header("旋转设置")]
    [Tooltip("旋转角速度（度/秒）")]
    [SerializeField] private float rotationSpeed = 30f;

    [Tooltip("是否默认开启旋转")]
    [SerializeField] private bool startRotating = true;

    [Header("状态")]
    [SerializeField] private bool isRotating = false;

    // 缓存Transform引用
    private Transform cachedTransform;

    void Awake()
    {
        cachedTransform = transform;
    }

    void Start()
    {
        // 根据初始设置决定是否开始旋转
        if (startRotating)
        {
            StartRotation();
        }
    }

    void Update()
    {
        // 如果旋转开关开启，执行旋转
        if (isRotating)
        {
            // 绕Y轴旋转（角速度 * 时间增量 = 旋转角度）
            cachedTransform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>
    /// 开启旋转
    /// </summary>
    public void StartRotation()
    {
        isRotating = true;
        Debug.Log($"物体 {gameObject.name} 开始绕Y轴旋转，速度: {rotationSpeed} 度/秒");
    }

    /// <summary>
    /// 关闭旋转
    /// </summary>
    public void StopRotation()
    {
        isRotating = false;
        Debug.Log($"物体 {gameObject.name} 停止旋转");
    }

    /// <summary>
    /// 切换旋转状态（开/关）
    /// </summary>
    public void ToggleRotation()
    {
        if (isRotating)
            StopRotation();
        else
            StartRotation();
    }



    /// <summary>
    /// 获取是否正在旋转
    /// </summary>
    public bool IsRotating
    {
        get { return isRotating; }
    }

    /// <summary>
    /// 增加旋转速度
    /// </summary>
    /// <param name="delta">增加量（度/秒）</param>
    public void AddRotationSpeed(float delta)
    {
        rotationSpeed += delta;
        Debug.Log($"旋转速度增加 {delta}，当前速度: {rotationSpeed} 度/秒");
    }


}