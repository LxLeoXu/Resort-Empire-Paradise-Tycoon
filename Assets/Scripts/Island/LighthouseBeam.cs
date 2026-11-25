using UnityEngine;

/// <summary>
/// A lighthouse's lamp, quietly: its glow burns steady while a soft double beam turns round it once every few
/// seconds (drawn as the horizontal beam seen in the island's view), and the lamp brightens for a moment as a beam
/// swings past the camera. Only in Play mode.
/// </summary>
public class LighthouseBeam : MonoBehaviour
{
    [Tooltip("The lamp's glow: steady, brighter for a moment when a beam faces the camera.")]
    public SpriteRenderer glow;
    [Tooltip("A soft glow sprite stretched into the two beams, centred on the lamp.")]
    public SpriteRenderer beam;
    [Tooltip("Seconds for one turn.")]
    public float period = 9f;
    [Tooltip("Length of each beam on the ground (world units).")]
    public float length = 1.3f;
    [Tooltip("Thickness of the beams (world units).")]
    public float width = 0.1f;
    [Range(0f, 1f)] public float beamAlpha = 0.34f;
    [Tooltip("How much brighter the lamp gets when a beam faces the camera.")]
    public float flash = 0.8f;

    static readonly Vector2 towardCamera = new Vector2(-0.7071f, -0.7071f);
    Color glowColor;
    Vector3 glowScale;
    Vector2 spriteSize = Vector2.one;
    float phase;

    void Start()
    {
        if (glow != null)
        {
            glowColor = glow.color;
            glowScale = glow.transform.localScale;
        }
        if (beam != null && beam.sprite != null) spriteSize = beam.sprite.bounds.size;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float a = Time.time / period * Mathf.PI * 2f + phase;
        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));                       // the beam on the ground plane
        var s = new Vector2((d.x - d.y) * 0.5f, (d.x + d.y) * 0.25f);          // as the island's view draws it
        float facing = Mathf.Pow(Mathf.Abs(Vector2.Dot(d, towardCamera)), 16f);  // (either beam)
        if (beam != null)
        {
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg);
            beam.transform.localScale = new Vector3(2f * length * s.magnitude / spriteSize.x, width / spriteSize.y, 1f);
            var c = beam.color;
            c.a = beamAlpha;
            beam.color = c;
        }
        if (glow != null)
        {
            var c = glowColor;
            c.a = Mathf.Clamp01(glowColor.a * (1f + flash * facing));
            glow.color = c;
            glow.transform.localScale = glowScale * (1f + 0.25f * facing);
        }
    }
}
