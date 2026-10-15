using UnityEngine;

/// <summary>
/// Moves a boat with the waves drawn by the Island Water shader: it computes the same wave phase at
/// the boat's spot (same noise and settings as the material), so the motion matches the crests you see.
/// A floating boat lifts and tilts as a crest passes under it. A boat pulled up on the beach stays put
/// on the sand; an arriving wave only nudges its end that lies in the water.
/// </summary>
public class FloatingBob : MonoBehaviour
{
    [Tooltip("Island Water material; its wave settings and noise drive the motion.")]
    public Material water;
    [Tooltip("Distance of the boat from the coast in grid cells (set by the island import).")]
    public float coastDistance = 0.5f;
    [Tooltip("Lift on a wave crest, in world units (floating boats).")]
    public float amplitude = 0.02f;
    [Tooltip("Tilt while a wave passes, in degrees.")]
    public float rockAngle = 3f;

    [Header("Pulled up on the beach")]
    public bool beached;
    [Tooltip("Offset from the pivot to the end of the boat that rests on the sand (world units).")]
    public Vector2 beachedEnd;
    [Tooltip("How far an arriving wave lifts the end in the water, in degrees.")]
    public float nudgeAngle = 1.4f;

    Vector3 restPosition;
    Quaternion restRotation;
    Vector2 spot;
    Texture2D noise;
    float waveFrequency = 1.25f;
    float waveSpeed = 0.35f;

    void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        spot = transform.position;
        if (water != null)
        {
            noise = water.GetTexture("_Noise") as Texture2D;
            waveFrequency = water.GetFloat("_WaveFrequency");
            waveSpeed = water.GetFloat("_WaveSpeed");
        }
    }

    void Update()
    {
        // Same as IslandWater.shader: phase = dist * frequency + t * speed + noise * 1.6, crest at frac == 0.5
        float t = Time.timeSinceLevelLoad;
        float n = noise != null && noise.isReadable
            ? noise.GetPixelBilinear(spot.x * 0.09f + t * 0.003f, spot.y * 0.09f + t * 0.003f).b
            : 0.5f;
        float phase = coastDistance * waveFrequency + t * waveSpeed + n * 1.6f;
        float lift = Mathf.Cos((phase - 0.5f) * 2f * Mathf.PI);   // +1 when the crest is under the boat

        if (beached)
        {
            // Only the end in the water rises a little as the wave runs up; the end on the sand stays.
            float push = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, lift));
            float angle = -push * nudgeAngle * Mathf.Sign(beachedEnd.x);
            Quaternion r = Quaternion.Euler(0f, 0f, angle);
            Vector3 end = beachedEnd;
            transform.localPosition = restPosition + (end - r * end);  // rotate around the beached end
            transform.localRotation = restRotation * r;
            return;
        }

        float tilt = Mathf.Sin((phase - 0.5f) * 2f * Mathf.PI);   // leans as the wave front passes
        float swell = Mathf.Sin(t * 0.8f + spot.x) * 0.25f;        // a little idle motion between waves
        transform.localPosition = restPosition + Vector3.up * ((lift * 0.75f + swell) * amplitude);
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, tilt * rockAngle);
    }
}
