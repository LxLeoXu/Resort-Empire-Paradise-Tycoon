using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 12f;

    private SpriteRenderer spriteRenderer;
    private int shown = -1;

    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)(Time.time * fps) % frames.Length;
        if (i == shown) return;
        shown = i;
        spriteRenderer.sprite = frames[i];
    }
}
