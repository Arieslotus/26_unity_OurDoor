using UnityEngine;

[RequireComponent(typeof(Light))]
public class PointLightFlicker : MonoBehaviour
{
    [Header("闪烁强度")]
    [Tooltip("亮度上下浮动百分比，例如0.2表示±20%")]
    [Range(0f, 1f)]
    public float intensityVariation = 0.2f;

    [Header("颜色变化")]
    [Tooltip("颜色变化幅度，0表示不变")]
    [Range(0f, 1f)]
    public float colorVariation = 0.05f;

    [Header("闪烁速度")]
    public float flickerSpeed = 15f;

    private Light pointLight;

    private float baseIntensity;
    private Color baseColor;

    private float targetIntensity;
    private Color targetColor;

    void Awake()
    {
        pointLight = GetComponent<Light>();

        baseIntensity = pointLight.intensity;
        baseColor = pointLight.color;

        targetIntensity = baseIntensity;
        targetColor = baseColor;
    }

    void Update()
    {
        // 每帧朝目标插值
        pointLight.intensity = Mathf.Lerp(
            pointLight.intensity,
            targetIntensity,
            Time.deltaTime * flickerSpeed);

        pointLight.color = Color.Lerp(
            pointLight.color,
            targetColor,
            Time.deltaTime * flickerSpeed);

        // 接近目标后生成新的随机目标
        if (Mathf.Abs(pointLight.intensity - targetIntensity) < 0.02f)
        {
            GenerateNextTarget();
        }
    }

    void GenerateNextTarget()
    {
        // 亮度
        targetIntensity = baseIntensity *
            Random.Range(
                1f - intensityVariation,
                1f + intensityVariation);

        // 颜色（轻微偏亮偏暗）
        float t = Random.Range(-colorVariation, colorVariation);

        targetColor = new Color(
            Mathf.Clamp01(baseColor.r + t),
            Mathf.Clamp01(baseColor.g + t),
            Mathf.Clamp01(baseColor.b + t),
            1f);
    }
}