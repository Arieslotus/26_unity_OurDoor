using UnityEngine;

public class FloatText : MonoBehaviour
{
    public float amplitude = 0.03f;

    public float frequency = 1f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        Vector3 p = startPos;

        p.y += Mathf.Sin(Time.time * frequency) * amplitude;

        transform.localPosition = p;
    }
}