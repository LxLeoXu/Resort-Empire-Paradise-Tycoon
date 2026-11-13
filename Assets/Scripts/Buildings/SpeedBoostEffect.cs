using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed partial class SpeedBoostEffect : MonoBehaviour
{
    [Tooltip("Objekt efektu (svetové plátno nad strechou). Je zapnutý, len kým zrýchlenie beží.")]
    [SerializeField] private RectTransform effect;
    [Tooltip("Odznak s bleskom nad strechou.")]
    [SerializeField] private RectTransform badge;
    [Tooltip("Kruh okolo odznaku: ukazuje, koľko zrýchlenia ešte ostáva.")]
    [SerializeField] private Image ring;
    [SerializeField] private RectTransform bolt;
    [Tooltip("Iskričky, ktoré stúpajú okolo budovy.")]
    [SerializeField] private Image[] sparks;

    [Header("Rozmery (jednotky plátna efektu)")]
    [Tooltip("Ako hlboko pod odznakom iskričky začínajú.")]
    [SerializeField] private float sparkDepth = 200f;
    [Tooltip("Ako ďaleko od stredu budovy iskričky stúpajú.")]
    [SerializeField] private float sparkSpread = 110f;
    [Tooltip("O koľko sa odznak posunie doprava, keď je na streche aj značka bez obsluhy.")]
    [SerializeField] private float besideMarker = 128f;

    public bool IsPlaying => remaining > 0f;
    public float Remaining => remaining;

    private const float PopOut = 0.25f;
    private const float LastSeconds = 3f;

    private static readonly Color SparkCyan = new Color32(56, 208, 240, 255);

    private NoWorkerMarker marker;
    private Vector2 badgeRest;
    private bool captured;
    private float duration;
    private float remaining;
    private float time;
    private float ending;
    private float clock;
    private float shift;

    public void Play(float seconds)
    {
        Capture();
        duration = Mathf.Max(0.01f, seconds);
        remaining = duration;
        time = 0f;
        ending = 0f;
        shift = marker != null && marker.IsShown ? besideMarker : 0f;
        effect.gameObject.SetActive(true);
        Apply();
    }

    public void Stop()
    {
        remaining = 0f;
    }

    private void Awake()
    {
        Capture();
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        marker = GetComponent<NoWorkerMarker>();
        badgeRest = badge.anchoredPosition;
    }

    private void Advance(float deltaTime)
    {
        if (!effect.gameObject.activeSelf) return;
        clock += deltaTime;
        time += deltaTime;
        if (remaining > 0f)
        {
            remaining = Mathf.Max(0f, remaining - deltaTime);
        }
        else
        {
            ending += deltaTime;
            if (ending >= PopOut)
            {
                effect.gameObject.SetActive(false);
                return;
            }
        }
        shift = Mathf.MoveTowards(shift, marker != null && marker.IsShown ? besideMarker : 0f, deltaTime * 600f);
        Apply();
    }

    private void Apply()
    {
        float shown = EaseOutBack(Span(time, 0f, 0.28f)) * (1f - Smooth(Span(ending, 0f, PopOut)));
        badge.anchoredPosition = badgeRest + Vector2.right * shift;
        badge.localScale = Vector3.one * (shown * (1f + 0.04f * Mathf.Sin(clock * 2f * Mathf.PI * 1.2f)));

        ring.fillAmount = remaining / duration;
        Color ringColor = ring.color;
        ringColor.a = remaining < LastSeconds ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(clock * Mathf.PI * 3f)) : 1f;
        ring.color = ringColor;

        float beat = Mathf.Abs(Mathf.Sin(clock * Mathf.PI * 2.2f));
        bolt.localScale = Vector3.one * (1f + 0.14f * beat);
        bolt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(clock * Mathf.PI * 2.2f) * 5f);

        float glow = Smooth(Span(time, 0.1f, 0.3f)) * (1f - Smooth(Span(ending, 0f, PopOut)));
        for (int i = 0; i < sparks.Length; i++)
        {
            float rise = Mathf.Repeat(clock / 1.5f + (float)i / sparks.Length, 1f);
            float fade = Mathf.Sin(rise * Mathf.PI);
            float slot = Mathf.Lerp(-sparkSpread, sparkSpread, (i + 0.5f) / sparks.Length);
            RectTransform spark = sparks[i].rectTransform;
            spark.anchoredPosition = badgeRest + new Vector2(slot + Mathf.Sin((rise + i * 0.4f) * 2f * Mathf.PI) * 8f, Mathf.Lerp(-sparkDepth, -sparkDepth * 0.15f, rise));
            spark.localScale = Vector3.one * (fade * Mathf.Lerp(0.6f, 1f, Noise(i)));
            Color color = i % 2 == 0 ? SparkCyan : Color.white;
            color.a = fade * glow;
            sparks[i].color = color;
        }
    }

    private static float Span(float value, float start, float length) => Mathf.Clamp01((value - start) / length);

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Noise(int index) => Mathf.Repeat(Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f, 1f);

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    [ContextMenu("Ukážka 15 s (len v Play mode)")]
    private void Preview()
    {
        if (Application.isPlaying) Play(15f);
    }
}
