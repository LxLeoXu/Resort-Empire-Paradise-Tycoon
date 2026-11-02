using UnityEngine;

/// <summary>
/// Draws a person (guest, worker) walking: while its transform moves it plays the walk cycle facing the way it
/// goes on screen (four directions, like the paths), and when it stops it shows the standing frame. Moving the
/// person belongs to the guest / worker logic; this only animates the body sprite and its shadow on the ground.
/// </summary>
public class WalkCycle : MonoBehaviour
{
    [System.Serializable]
    public class Direction
    {
        [Tooltip("Snímky chôdze (telo).")]
        public Sprite[] walk;
        [Tooltip("Snímky tieňa k chôdzi, rovnako veľa ako snímok chôdze.")]
        public Sprite[] walkShadow;
        [Tooltip("Postavička stojí.")]
        public Sprite idle;
        [Tooltip("Tieň, keď stojí.")]
        public Sprite idleShadow;
    }

    [Tooltip("Telo (SpriteRenderer na vrstve Default).")]
    public SpriteRenderer body;
    [Tooltip("Tieň (SpriteRenderer na vrstve Ground).")]
    public SpriteRenderer shadow;
    [Tooltip("Ide doprava dole (k hráčovi).")]
    public Direction downRight;
    [Tooltip("Ide doprava hore.")]
    public Direction upRight;
    [Tooltip("Ide doľava hore.")]
    public Direction upLeft;
    [Tooltip("Ide doľava dole.")]
    public Direction downLeft;
    [Tooltip("Snímky chôdze za sekundu.")]
    public float framesPerSecond = 9f;
    [Tooltip("Pod touto rýchlosťou (jednotky za sekundu) postavička stojí.")]
    public float minSpeed = 0.02f;

    Vector3 lastPosition;
    Direction facing;
    float phase;

    /// <summary>True while the person is moving (walk cycle playing).</summary>
    public bool IsWalking { get; private set; }

    void OnEnable()
    {
        lastPosition = transform.position;
        if (facing == null) facing = downRight;
        Show();
    }

    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        IsWalking = speed > minSpeed;
        if (IsWalking)
        {
            facing = Pick(delta);
            phase += Time.deltaTime * framesPerSecond;
        }
        else
        {
            phase = 0f;
        }
        Show();
    }

    /// <summary>Turns a standing person towards a direction on screen (e.g. the building it waits at).</summary>
    public void Face(Vector2 screenDirection)
    {
        facing = Pick(screenDirection);
        Show();
    }

    Direction Pick(Vector2 d)
    {
        if (d.x >= 0f) return d.y <= 0f ? downRight : upRight;
        return d.y > 0f ? upLeft : downLeft;
    }

    void Show()
    {
        if (facing == null) return;
        if (IsWalking && facing.walk != null && facing.walk.Length > 0)
        {
            int f = (int)phase % facing.walk.Length;
            if (body) body.sprite = facing.walk[f];
            if (shadow && facing.walkShadow != null && f < facing.walkShadow.Length) shadow.sprite = facing.walkShadow[f];
        }
        else
        {
            if (body) body.sprite = facing.idle;
            if (shadow) shadow.sprite = facing.idleShadow;
        }
    }
}
