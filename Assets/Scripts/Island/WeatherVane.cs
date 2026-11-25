using UnityEngine;

/// <summary>
/// A weathervane's vane, quietly: it swings a little round its rod as the wind veers, with the gusts of the island's
/// Sprite Wind. The sprite is the flat vane seen side on; turned about the vertical it shortens and one end rises in
/// the island's view, a shear this object (the vane's pivot on the rod) gives it as its own rotation and scale and the
/// sprite's rotation. Only in Play mode.
/// </summary>
public class WeatherVane : MonoBehaviour
{
    [Tooltip("The vane's sprite, a child at this object's position.")]
    public Transform vane;
    [Tooltip("Largest turn either way (degrees).")]
    public float swing = 38f;
    [Tooltip("Like Sprite Wind's Sway Speed.")]
    public float speed = 0.9f;

    static readonly int TimeId = Shader.PropertyToID("_IslandTime");
    static readonly int AnimId = Shader.PropertyToID("_IslandAnim");
    float phase;

    void Start()
    {
        Vector3 p = transform.position;
        phase = p.x * 1.7f + p.y * 2.3f;
    }

    void LateUpdate()
    {
        if (vane == null) return;
        float time = Shader.GetGlobalFloat(TimeId);
        float t = time * speed + phase;
        float gust = 0.6f + 0.4f * Mathf.Sin(time * 0.31f + phase * 0.5f);
        float turn = (Mathf.Sin(t) + 0.35f * Mathf.Sin(t * 2.31f + 1.7f)) / 1.35f * swing * gust
                     * Shader.GetGlobalFloat(AnimId) * Mathf.Deg2Rad;
        float a = Mathf.Cos(turn), g = 0.25f * Mathf.Sin(turn);
        float e = (a + 1f) * 0.5f, f = (a - 1f) * 0.5f;
        float q = Mathf.Sqrt(e * e + g * g), r = Mathf.Sqrt(f * f + g * g);
        float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(g, e);
        transform.localRotation = Quaternion.Euler(0f, 0f, (a2 + a1) * 0.5f * Mathf.Rad2Deg);
        transform.localScale = new Vector3(q + r, q - r, 1f);
        vane.localRotation = Quaternion.Euler(0f, 0f, (a2 - a1) * 0.5f * Mathf.Rad2Deg);
    }
}
