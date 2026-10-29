using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A dolphin leaping out of the sea now and then, at one of the spots far from the shore (set by the island
/// import). One leap is a whole sequence of sprites: the shape rising under the surface, the leap with its
/// spray, the dive and the foam settling. Where there is deep water further on it sometimes leaps again,
/// carrying on from where it swam to. It only picks spots whose leap the camera sees (wider screens show
/// more sea beside the island), and never one near where another dolphin is leaping. The sprites leap to
/// the right; leaping left mirrors them. Only in Play mode.
/// </summary>
public class DolphinJump : MonoBehaviour
{
    [System.Serializable]
    public struct Spot
    {
        [Tooltip("The middle of the leap, on the water (parent's local space).")]
        public Vector2 position;
        [Tooltip("+1 leaps to the right, -1 to the left.")]
        public float direction;
        [Tooltip("There is deep water for a second leap further on.")]
        public bool canLeapTwice;
    }

    public SpriteRenderer body;
    [Tooltip("One leap, from the shape rising under the surface to the foam settling (leaping right).")]
    public Sprite[] frames;
    public float framesPerSecond = 16f;
    public Spot[] spots;
    [Tooltip("Seconds between leaps (min, max).")]
    public Vector2 pause = new Vector2(7f, 16f);
    [Range(0f, 1f)] public float secondLeapChance = 0.45f;
    [Tooltip("How far the dolphin swims during one sequence (world units): a second leap starts there.")]
    public float secondLeapDistance = 3.7f;
    [Tooltip("What shows above the water around a spot (x along the leap's direction, y up; world units). " +
             "A spot is used only when this is inside the camera's view.")]
    public Rect visibleArea = new Rect(-0.9f, -0.2f, 1.95f, 0.85f);
    [Tooltip("Another dolphin keeps at least this far away (world units).")]
    public float keepApart = 4.5f;

    static readonly List<Vector2> busy = new List<Vector2>();   // where the dolphins are leaping now
    readonly List<Vector2> mine = new List<Vector2>();
    readonly List<int> choice = new List<int>();
    int lastSpot = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBusy() => busy.Clear();

    void OnEnable()
    {
        body.enabled = false;
        StartCoroutine(Leap());
    }

    void OnDisable()
    {
        // (disabling a component does not stop its coroutines; enabling it again would start a second one)
        StopAllCoroutines();
        body.enabled = false;
        Release();
    }

    void Release()
    {
        foreach (var p in mine) busy.Remove(p);
        mine.Clear();
    }

    Vector2 At(Spot spot, int leap) => spot.position + Vector2.right * (spot.direction * secondLeapDistance * leap);

    bool Seen(Spot spot, int leap)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return true;
        Vector2 p = At(spot, leap);
        Vector3 a = transform.parent.TransformPoint(p.x + spot.direction * visibleArea.xMin, p.y + visibleArea.yMin, 0f);
        Vector3 b = transform.parent.TransformPoint(p.x + spot.direction * visibleArea.xMax, p.y + visibleArea.yMax, 0f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return Mathf.Min(a.x, b.x) > c.x - w && Mathf.Max(a.x, b.x) < c.x + w
            && Mathf.Min(a.y, b.y) > c.y - h && Mathf.Max(a.y, b.y) < c.y + h;
    }

    bool Free(Spot spot, int leaps)
    {
        for (int k = 0; k < leaps; k++)
            foreach (var p in busy)
                if (Vector2.Distance(p, At(spot, k)) < keepApart) return false;
        return true;
    }

    IEnumerator Leap()
    {
        yield return new WaitForSeconds(Random.Range(2f, pause.x));
        while (true)
        {
            choice.Clear();
            for (int i = 0; i < spots.Length; i++)
                if (i != lastSpot && Seen(spots[i], 0) && Free(spots[i], 1)) choice.Add(i);
            if (choice.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            int s = choice[Random.Range(0, choice.Count)];
            lastSpot = s;
            Spot spot = spots[s];
            int leaps = spot.canLeapTwice && Random.value < secondLeapChance && Seen(spot, 1) && Free(spot, 2) ? 2 : 1;
            for (int k = 0; k < leaps; k++)
            {
                mine.Add(At(spot, k));
                busy.Add(At(spot, k));
            }
            for (int k = 0; k < leaps; k++)
            {
                Vector2 p = At(spot, k);
                transform.localPosition = new Vector3(p.x, p.y, transform.localPosition.z);
                body.transform.localScale = new Vector3(spot.direction, 1f, 1f);
                body.enabled = true;
                for (float t = 0f; t * framesPerSecond < frames.Length; t += Time.deltaTime)
                {
                    body.sprite = frames[Mathf.Min((int)(t * framesPerSecond), frames.Length - 1)];
                    yield return null;
                }
                body.enabled = false;
            }
            Release();
            yield return new WaitForSeconds(Random.Range(pause.x, pause.y));
        }
    }
}
