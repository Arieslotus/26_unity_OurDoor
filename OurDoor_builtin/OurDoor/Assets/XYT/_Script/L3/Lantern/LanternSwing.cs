using UnityEngine;

public class LanternSwing : MonoBehaviour
{
    [Header("摆动幅度")]
    public float maxAngle = 5f;

    [Header("摆动速度")]
    public float swingSpeed = 0.8f;

    [Header("随机扰动")]
    public float noiseStrength = 2f;

    [Header("随机变化速度")]
    public float noiseSpeed = 0.3f;

    private Quaternion initialRotation;

    private float randomSeedX;
    private float randomSeedZ;

    private void Start()
    {
        initialRotation = transform.localRotation;

        randomSeedX = Random.Range(0f, 1000f);
        randomSeedZ = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        float t = Time.time;

        // 主摆动
        float swingX =
            Mathf.Sin(t * swingSpeed) * maxAngle;

        float swingZ =
            Mathf.Cos(t * swingSpeed * 0.8f) * maxAngle * 0.6f;

        // Perlin随机风
        float noiseX =
            (Mathf.PerlinNoise(randomSeedX, t * noiseSpeed) - 0.5f)
            * noiseStrength;

        float noiseZ =
            (Mathf.PerlinNoise(randomSeedZ, t * noiseSpeed) - 0.5f)
            * noiseStrength;

        Quaternion targetRotation =
            initialRotation *
            Quaternion.Euler(
                swingX + noiseX,
                0,
                swingZ + noiseZ);

        transform.localRotation = targetRotation;
    }
}