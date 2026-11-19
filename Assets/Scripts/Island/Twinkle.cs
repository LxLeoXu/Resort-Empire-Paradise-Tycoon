using UnityEngine;

/// <summary>
/// A glint that catches the light now and then (the Spa's gold crown): it flashes up turning a little, then is gone
/// until the next one, a few seconds later.
/// </summary>
public class Twinkle : MonoBehaviour
{
    public SpriteRenderer sprite;
    [Tooltip("Seconds between glints (random in this range).")]
    public Vector2 every = new Vector2(3.5f, 6.5f);
    [Tooltip("How long one glint lasts, seconds.")]
    public float flash = 0.55f;
    [Tooltip("Its scale at the brightest moment.")]
    public float size = 1f;

    float next, started = -1f;

    void OnEnable()
    {
        next = Time.time + Random.Range(0.5f, every.y);
        started = -1f;
        Apply(0f);
    }

    void Update()
    {
        if (started < 0f && Time.time >= next)
            started = Time.time;
        if (started < 0f)
            return;
        float u = (Time.time - started) / flash;
        if (u >= 1f)
        {
            started = -1f;
            next = Time.time + Random.Range(every.x, every.y);
            Apply(0f);
            return;
        }
        Apply(Mathf.Sin(u * Mathf.PI));
    }

    void Apply(float k)
    {
        transform.localScale = Vector3.one * (size * k);
        transform.localRotation = Quaternion.Euler(0f, 0f, 40f * k);
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = k;
            sprite.color = c;
        }
    }
}
