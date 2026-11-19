using System.Collections;
using UnityEngine;

/// <summary>
/// The Spa's boat ferries a VIP guest between the VIP pontoon at the island's pier and the spa: it waits at the
/// pontoon, the VIP gets on, it sails to the spa's landing, the VIP gets off and stays a while, gets back on, and the
/// boat takes them back. At each end it turns before it moors, so it always leaves bow first. Smooth motion: the
/// speed eases into and out of a mooring and drops in tight turns, the heading turns gradually (no flicker), and
/// it shows the sprite rendered nearest to that heading (N headings from Blender, with and without the VIP aboard).
/// </summary>
public class SpaBoatShuttle : MonoBehaviour
{
    [Tooltip("The hull with the boatman (on a child with FloatingBob, so it bobs while it moves).")]
    public SpriteRenderer hull;
    [Tooltip("The same boat with the VIP aboard, drawn over the hull and faded in / out when the VIP gets on / off.")]
    public SpriteRenderer passenger;
    [Tooltip("Its shadow on the water.")]
    public SpriteRenderer shadow;
    [Tooltip("Hull sprites by heading on the ground, evenly round from 0 degrees.")]
    public Sprite[] hulls = new Sprite[8];
    [Tooltip("The boat with the VIP aboard, by heading as hulls.")]
    public Sprite[] passengerHulls = new Sprite[8];
    [Tooltip("Shadow sprites by heading, as hulls.")]
    public Sprite[] shadows = new Sprite[8];
    [Tooltip("Shadows with the VIP aboard.")]
    public Sprite[] passengerShadows = new Sprite[8];
    [Tooltip("From the pier to the spa's landing (local points; the first is the pier berth, the last the landing).")]
    public Vector2[] toSpa = new Vector2[0];
    [Tooltip("From the spa's landing to the pier (local points; the first is the landing, the last the pier berth).")]
    public Vector2[] toPier = new Vector2[0];
    [Tooltip("Cruising speed, world units per second.")]
    public float speed = 0.45f;
    [Tooltip("How far before / after a mooring it slows down / speeds up (world units).")]
    public float easeDistance = 0.9f;
    [Tooltip("Speed in the tightest turns, as a part of the cruising speed.")]
    public float turnSpeed = 0.55f;
    [Tooltip("How quickly the bow follows the course (seconds).")]
    public float headingLag = 0.25f;
    [Tooltip("Its heading while it lies at the spa's landing, where it starts (ground degrees).")]
    public float spaHeading = 315f;
    [Tooltip("How long the VIP stays at the spa (seconds).")]
    public Vector2 spaTime = new Vector2(10f, 16f);
    [Tooltip("How long it waits at the pier for the next VIP (seconds).")]
    public Vector2 pierTime = new Vector2(6f, 10f);
    [Tooltip("Getting on or off (seconds).")]
    public float boardTime = 0.5f;
    [Tooltip("Foam it leaves behind while it moves.")]
    public ParticleSystem wake;
    public float wakeRate = 30f;

    int shownIndex = -1;
    bool aboard;
    float heading, headingVelocity;

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        SetWake(0f);
    }

    IEnumerator Run()
    {
        // it starts at the spa's landing, the VIP inside, part way through the stay
        Place(toPier.Length > 0 ? toPier[0] : (Vector2)transform.localPosition);
        SetAboard(0f);
        heading = spaHeading;
        ShowHeading(true);
        SetWake(0f);
        yield return new WaitForSeconds(Random.Range(1f, spaTime.y));
        while (true)
        {
            yield return Board(true);
            yield return Travel(toPier);
            yield return Board(false);
            yield return new WaitForSeconds(Random.Range(pierTime.x, pierTime.y));
            yield return Board(true);
            yield return Travel(toSpa);
            yield return Board(false);
            yield return new WaitForSeconds(Random.Range(spaTime.x, spaTime.y));
        }
    }

    IEnumerator Board(bool on)
    {
        yield return new WaitForSeconds(0.6f);
        for (float t = 0f; t < boardTime; t += Time.deltaTime)
        {
            SetAboard(on ? t / boardTime : 1f - t / boardTime);
            yield return null;
        }
        SetAboard(on ? 1f : 0f);
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator Travel(Vector2[] pts)
    {
        if (pts == null || pts.Length < 2)
            yield break;
        // the way: a cubic B-spline over the points (it rounds every corner evenly, so the turning rate has no kinks;
        // the ends are clamped: it leaves from the first point and stops on the last, along the last leg)
        var q = new Vector2[pts.Length + 4];
        q[0] = q[1] = pts[0];
        for (int i = 0; i < pts.Length; i++) q[i + 2] = pts[i];
        q[pts.Length + 2] = q[pts.Length + 3] = pts[pts.Length - 1];
        const int perSegment = 32;
        int segments = q.Length - 3;
        int n = segments * perSegment + 1;
        var path = new Vector2[n];
        var dist = new float[n];
        for (int i = 0; i < n; i++)
        {
            int seg = Mathf.Min(i / perSegment, segments - 1);
            float s = i == n - 1 ? 1f : (i - seg * perSegment) / (float)perSegment;
            float s2 = s * s, s3 = s2 * s, r = 1f - s;
            path[i] = (r * r * r * q[seg] + (3f * s3 - 6f * s2 + 4f) * q[seg + 1] +
                       (-3f * s3 + 3f * s2 + 3f * s + 1f) * q[seg + 2] + s3 * q[seg + 3]) / 6f;
            dist[i] = i == 0 ? 0f : dist[i - 1] + Vector2.Distance(path[i - 1], path[i]);
        }
        // how sharply it turns along the way (on the ground: degrees per unit), smoothed, as a speed factor
        var turn = new float[n];
        for (int i = 1; i < n - 1; i++)
        {
            float a0 = GroundAngle(path[i] - path[i - 1]), a1 = GroundAngle(path[i + 1] - path[i]);
            float ds = (dist[i + 1] - dist[i - 1]) * 0.5f;
            turn[i] = ds < 1e-3f ? 0f : Mathf.Abs(Mathf.DeltaAngle(a0, a1)) / ds;   // (the clamped ends bunch up)
        }
        var slow = new float[n];
        const float window = 0.35f;                         // (world units each way)
        for (int i = 0; i < n; i++)
        {
            float sum = 0f; int cnt = 0;
            for (int j = i; j >= 0 && dist[i] - dist[j] < window; j--) { sum += turn[j]; cnt++; }
            for (int j = i + 1; j < n && dist[j] - dist[i] < window; j++) { sum += turn[j]; cnt++; }
            float rate = sum / Mathf.Max(1, cnt);           // 360 deg per unit ~ a turn of radius 0.16
            slow[i] = Mathf.Lerp(1f, turnSpeed, Mathf.Clamp01(rate / 300f));
        }
        float length = dist[n - 1], d = 0f;
        while (d < length)
        {
            float ease = Mathf.Min(Mathf.Sqrt(Mathf.Clamp01((length - d) / easeDistance)),
                                   Mathf.Sqrt(Mathf.Clamp01(d / easeDistance)));
            float k = Mathf.Max(Mathf.Min(ease, Sample(slow, dist, d)), 0.12f);
            d = Mathf.Min(length, d + speed * k * Time.deltaTime);
            Vector2 p = At(path, dist, d);
            Vector2 tangent = At(path, dist, Mathf.Min(length, d + 0.03f)) - At(path, dist, Mathf.Max(0f, d - 0.03f));
            Place(p);
            if (tangent.sqrMagnitude > 1e-8f)
                heading = Mathf.SmoothDampAngle(heading, GroundAngle(tangent), ref headingVelocity, headingLag);
            ShowHeading(false);
            SetWake(Mathf.Min(ease, 1f));
            yield return null;
        }
        // the last stretch runs straight along the mooring heading: settle on it
        headingVelocity = 0f;
        SetWake(0f);
    }

    static Vector2 At(Vector2[] path, float[] dist, float d)
    {
        int i = Index(dist, d);
        float span = dist[i] - dist[i - 1];
        float f = span > 1e-6f ? (d - dist[i - 1]) / span : 0f;
        return Vector2.Lerp(path[i - 1], path[i], Mathf.Clamp01(f));
    }

    static float Sample(float[] values, float[] dist, float d)
    {
        int i = Index(dist, d);
        float span = dist[i] - dist[i - 1];
        float f = span > 1e-6f ? (d - dist[i - 1]) / span : 0f;
        return Mathf.Lerp(values[i - 1], values[i], Mathf.Clamp01(f));
    }

    static int Index(float[] dist, float d)
    {
        int lo = 1, hi = dist.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (dist[mid] < d) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    // A screen (Unity) direction as a heading on the iso ground: Unity x = (gx - gy) / 2, y = (gx + gy) / 4.
    static float GroundAngle(Vector2 v)
    {
        return Mathf.Atan2(2f * v.y - v.x, 2f * v.y + v.x) * Mathf.Rad2Deg;
    }

    // The sprite rendered nearest to the current heading (a little hysteresis: no flicker on a boundary).
    void ShowHeading(bool force)
    {
        int count = hulls != null && hulls.Length > 0 ? hulls.Length : 8;
        float step = 360f / count;
        float a = Mathf.Repeat(heading, 360f);
        if (!force && shownIndex >= 0 && Mathf.Abs(Mathf.DeltaAngle(a, shownIndex * step)) < step * 0.6f)
            return;
        int index = Mathf.RoundToInt(a / step) % count;
        if (index == shownIndex) return;
        shownIndex = index;
        if (hull != null && hulls[index] != null) hull.sprite = hulls[index];
        if (passenger != null && index < passengerHulls.Length && passengerHulls[index] != null)
            passenger.sprite = passengerHulls[index];
        ShowShadow();
    }

    // The VIP aboard: 0 (not there) .. 1; the shadow follows once they are half way on or off.
    void SetAboard(float k)
    {
        if (passenger != null)
        {
            var c = passenger.color;
            c.a = k;
            passenger.color = c;
            passenger.enabled = k > 0f;
        }
        bool on = k >= 0.5f;
        if (on != aboard)
        {
            aboard = on;
            ShowShadow();
        }
    }

    void ShowShadow()
    {
        if (shadow == null || shownIndex < 0) return;
        var set = aboard && passengerShadows != null && shownIndex < passengerShadows.Length &&
                  passengerShadows[shownIndex] != null ? passengerShadows : shadows;
        if (shownIndex < set.Length && set[shownIndex] != null) shadow.sprite = set[shownIndex];
    }

    void Place(Vector2 local)
    {
        transform.localPosition = new Vector3(local.x, local.y, transform.localPosition.z);
    }

    void SetWake(float k)
    {
        if (wake == null) return;
        var emission = wake.emission;
        emission.rateOverTime = wakeRate * k * k;
    }
}
