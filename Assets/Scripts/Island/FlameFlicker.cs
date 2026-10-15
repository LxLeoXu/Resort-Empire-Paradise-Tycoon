using UnityEngine;

/// <summary>Flickers a torch flame sprite: noisy stretch and brightness around the flame's base.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FlameFlicker : MonoBehaviour
{
    [Tooltip("Height of the flame's base above the sprite pivot, in world units (the flame scales around it).")]
    public float baseHeight = 0.44f;
    public float stretch = 0.14f;
    public float speed = 6f;

    SpriteRenderer sr;
    Vector3 restPosition;
    Vector3 restScale;
    Color restColor;
    float seed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        restColor = sr.color;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        float n = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float m = Mathf.PerlinNoise(seed + 7.3f, t * 1.7f) * 2f - 1f;
        float sy = 1f + n * stretch;
        float sx = 1f - n * stretch * 0.4f + m * 0.03f;
        transform.localScale = new Vector3(restScale.x * sx, restScale.y * sy, restScale.z);
        // keep the flame's base in place while it stretches
        transform.localPosition = restPosition + Vector3.up * (baseHeight * (1f - sy));
        float b = 1f + m * 0.12f;
        sr.color = new Color(restColor.r * b, restColor.g * b, restColor.b * b, restColor.a);
    }
}
