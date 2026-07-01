using UnityEngine;

[RequireComponent(typeof(Light))]
public class PointLightPulse : MonoBehaviour
{
    [Header("频率(每秒闪烁次数)")]
    public float speed = 2f;

    [Header("亮度幅度")]
    public float intensityAmplitude = 1f;

    [Header("颜色幅度")]
    [Range(0, 1)]
    public float colorAmplitude = 0.2f;

    private Light pointLight;

    private float baseIntensity;
    private Color baseColor;

    void Awake()
    {
        pointLight = GetComponent<Light>();

        baseIntensity = pointLight.intensity;
        baseColor = pointLight.color;
    }

    void Update()
    {
        // 0~1
        float t = (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) + 1f) * 0.5f;

        // 亮度
        pointLight.intensity = baseIntensity + intensityAmplitude * t;

        // 颜色（越亮越接近白色）
        pointLight.color = Color.Lerp(
            baseColor,
            Color.white,
            t * colorAmplitude);
    }
}