using UnityEngine;

/// <summary>Flickers a soft glow sprite (a lantern's flame, a string light's bulb): its brightness and size wander
/// with noise around their rest values.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GlowFlicker : MonoBehaviour
{
    [Tooltip("How far the brightness wanders, as a share of the rest alpha.")]
    public float amount = 0.25f;
    [Tooltip("How far the size wanders, as a share of the rest scale.")]
    public float sizeAmount = 0.06f;
    public float speed = 2f;

    SpriteRenderer sr;
    Color restColor;
    Vector3 restScale;
    float seed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        restColor = sr.color;
        restScale = transform.localScale;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        float n = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float m = Mathf.PerlinNoise(seed + 3.1f, t * 0.6f) * 2f - 1f;
        Color c = restColor;
        c.a = Mathf.Clamp01(restColor.a * (1f + n * amount));
        sr.color = c;
        transform.localScale = restScale * (1f + m * sizeAmount);
    }
}
