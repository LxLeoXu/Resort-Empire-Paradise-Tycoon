using UnityEngine;

/// <summary>Slowly drifts a cloud across the sky and wraps it around when it leaves the area.</summary>
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Units per second (negative drifts left).")]
    public float speed = 0.12f;
    [Tooltip("Width of the strip the cloud travels along, centred on its start position.")]
    public float wrapWidth = 30f;

    float startX;

    void Awake()
    {
        startX = transform.localPosition.x;
    }

    void Update()
    {
        Vector3 p = transform.localPosition;
        p.x += speed * Time.deltaTime;
        float half = wrapWidth * 0.5f;
        if (p.x > startX + half) p.x -= wrapWidth;
        else if (p.x < startX - half) p.x += wrapWidth;
        transform.localPosition = p;
    }
}
