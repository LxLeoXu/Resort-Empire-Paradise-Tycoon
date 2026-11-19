using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The short, modest celebration after an area is bought in S9, in beats:
/// 1. the new building settles with a small squash, a soft flash, one ring and a few puffs on the ground
///    (dust at the pool, sea spray at the bungalow);
/// 2. a few of the area's particles (drops / paper lanterns with a warm halo), the ribbon "POOL UNLOCKED!" unfurls
///    over it with one soft shine, "+100" resort value rises from the building;
/// 3. everything fades away faster than it came (2.3 s in all). Own overlay canvas, takes no taps, destroys itself.
/// </summary>
public class UnlockCelebration : MonoBehaviour
{
    [Serializable]
    public class Style
    {
        public Sprite ribbon;
        public Sprite particle;
        public Sprite ring;
        public Sprite glow;
        public Sprite spark;
        public Sprite puff;
        public Sprite shine;
        public Sprite valueIcon;
        public TMP_FontAsset font;
        public Material outline;
        public Color glowColor = new Color(1f, 0.93f, 0.7f, 1f);
        public Color ringColor = Color.white;
        public Color puffColor = new Color(0.96f, 0.9f, 0.78f, 1f);
        [Tooltip("{0} = the area's title")]
        public string titleFormat = "{0} UNLOCKED!";
        [Tooltip("Particles rise and sway (lanterns) instead of flying up and falling (drops)")]
        public bool rise;
        public int count = 8;
        public float particleSize = 46f;
        [Tooltip("A warm halo behind each particle (lanterns); alpha 0 = none")]
        public Color haloColor = new Color(1f, 0.7f, 0.35f, 0f);
    }

    private const float RibbonWidth = 640f;
    private const float BannerScale = 0.82f;
    private const float Length = 2.3f;

    [Tooltip("Stops the clock (for recording frame by frame with Advance)")]
    public bool paused;

    private float clock;
    private Style style;
    private Transform target;
    private Vector3 targetScale;
    private Bounds targetBounds;
    private Canvas canvas;
    private RectTransform root, burst, banner, value, shineRt;
    private Image glow, ring, ribbonImage, valueIcon;
    private TMP_Text label, valueText;
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<Image> sparks = new List<Image>();
    private readonly List<Vector2> sparkSpots = new List<Vector2>();
    private readonly List<Image> puffs = new List<Image>();
    private readonly List<Vector2> puffDirs = new List<Vector2>();

    private struct Particle
    {
        public Image image, halo;
        public Vector2 velocity, start;
        public float delay, sway, phase, size;
    }

    /// <summary>Plays the celebration over the given building; `resortValue` = what the purchase added.</summary>
    public static UnlockCelebration Play(Style style, string title, GameObject building, int resortValue = 0)
    {
        if (style == null || building == null) return null;
        var go = new GameObject("UnlockCelebration", typeof(RectTransform));
        var c = go.AddComponent<UnlockCelebration>();
        c.Build(style, title, building, resortValue);
        return c;
    }

    private void Build(Style s, string title, GameObject building, int resortValue)
    {
        style = s;
        target = building.transform;
        targetScale = target.localScale;
        targetBounds = BuildingBounds(building);
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 26;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)transform;
        var rng = new System.Random(title.GetHashCode() & 0x7fffffff);
        float R() => (float)rng.NextDouble();

        burst = Node("Burst", root, Vector2.zero);
        for (int i = 0; i < 6; i++)                                // a few puffs along the ground, out to both sides
        {
            float a = Mathf.Lerp(-20f, 200f, (i + 0.5f) / 6f) * Mathf.Deg2Rad;
            puffDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * Mathf.Lerp(110f, 170f, R()));
            puffs.Add(Img("Puff", burst, s.puff, Mathf.Lerp(70f, 100f, R()), s.puffColor));
        }
        ring = Img("Ring", burst, s.ring, 300f, s.ringColor);
        glow = Img("Flash", burst, s.glow, 320f, s.glowColor);

        for (int i = 0; i < s.count; i++)
        {
            float a = s.rise ? Mathf.Lerp(72f, 108f, R()) : Mathf.Lerp(40f, 140f, R());
            float speed = s.rise ? Mathf.Lerp(95f, 150f, R()) : Mathf.Lerp(430f, 660f, R());
            var p = new Particle
            {
                velocity = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * speed,
                delay = R() * (s.rise ? 0.35f : 0.08f),
                sway = Mathf.Lerp(10f, 20f, R()),
                phase = R() * Mathf.PI * 2f,
                size = s.particleSize * Mathf.Lerp(0.8f, 1.1f, R()),
                start = s.rise ? new Vector2(Mathf.Lerp(-150f, 150f, (i + R()) / s.count), Mathf.Lerp(-30f, 20f, R())) : Vector2.zero,
            };
            if (s.haloColor.a > 0f) p.halo = Img("Halo", burst, s.glow, p.size * 2.4f, s.haloColor);
            p.image = Img("Particle", burst, s.particle, p.size, Color.white);
            particles.Add(p);
        }
        for (int i = 0; i < 4; i++)
        {
            float a = 30f + i * 40f + Mathf.Lerp(-12f, 12f, R());
            float r = Mathf.Lerp(130f, 200f, R());
            sparkSpots.Add(new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * r, Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.7f + 40f));
            sparks.Add(Img("Spark", burst, s.spark, Mathf.Lerp(30f, 44f, R()), Color.white));
        }

        value = Node("Value", root, Vector2.zero);
        valueIcon = Img("Icon", value, s.valueIcon, 44f, Color.white);
        var vt = Node("Text", value, Vector2.zero);
        vt.sizeDelta = new Vector2(240f, 60f);
        valueText = Text(vt, resortValue > 0 ? "+" + resortValue.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "", 38f);
        valueText.alignment = TextAlignmentOptions.MidlineLeft;
        float tw = valueText.GetPreferredValues(valueText.text).x;
        float total = 44f + 8f + tw;
        valueIcon.rectTransform.anchoredPosition = new Vector2(-total / 2f + 22f, 0f);
        vt.anchoredPosition = new Vector2(-total / 2f + 52f + 120f, 2f);
        if (resortValue <= 0) value.gameObject.SetActive(false);

        banner = Node("Banner", root, Vector2.zero);
        ribbonImage = Img("Ribbon", banner, s.ribbon, RibbonWidth, Color.white);
        ribbonImage.type = Image.Type.Sliced;
        ribbonImage.preserveAspect = false;
        ribbonImage.rectTransform.sizeDelta = new Vector2(RibbonWidth, 128f);
        ribbonImage.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var sh = Img("Shine", ribbonImage.transform, s.shine, 128f, new Color(1f, 1f, 1f, 0.32f));
        sh.preserveAspect = false;
        shineRt = sh.rectTransform;
        shineRt.sizeDelta = new Vector2(70f, 220f);
        shineRt.localRotation = Quaternion.Euler(0f, 0f, -22f);
        var t = Node("Title", banner, new Vector2(0f, 9f));
        t.sizeDelta = new Vector2(RibbonWidth - 200f, 80f);
        label = Text(t, string.Format(s.titleFormat, title).ToUpperInvariant(), 40f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 40f;
        label.characterSpacing = 2f;
        Pose();
    }

    private void Update()
    {
        if (!paused)
            Advance(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
    }

    private void Advance(float dt)
    {
        clock += dt;
        if (clock >= Length)
        {
            Destroy(gameObject);
            return;
        }
        Pose();
    }

    private void OnDestroy()
    {
        if (target != null) target.localScale = targetScale;
    }

    private void Pose()
    {
        float t = clock;
        Vector2 at = Anchor();
        burst.anchoredPosition = at;

        if (target != null)                                       // the building settles: a small squash, rebound
        {
            float u = Mathf.Clamp01(t / 0.36f);
            float w = u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI * 2f) * (1f - u);
            target.localScale = new Vector3(targetScale.x * (1f + 0.03f * w), targetScale.y * (1f - 0.045f * w), targetScale.z);
        }

        glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.2f, EaseOutCubic(t / 0.3f));
        SetAlpha(glow, 0.55f * Smooth(t / 0.06f) * (1f - Smooth((t - 0.06f) / 0.3f)));
        float rp = Mathf.Clamp01(t / 0.55f);                      // one ring lying on the iso ground
        ring.rectTransform.localScale = new Vector3(1f, 0.5f, 1f) * Mathf.Lerp(0.3f, 1.7f, EaseOutCubic(rp));
        ring.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        SetAlpha(ring, 0.6f * Mathf.Pow(1f - rp, 1.6f) * Smooth(t / 0.06f));
        for (int i = 0; i < puffs.Count; i++)
        {
            float u = Mathf.Clamp01(t / 0.8f);
            var rt = puffs[i].rectTransform;
            rt.anchoredPosition = puffDirs[i] * EaseOutCubic(u) + new Vector2(0f, -10f + 20f * u);
            rt.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.05f, EaseOutCubic(u));
            SetAlpha(puffs[i], 0.6f * Smooth(t / 0.08f) * (1f - Smooth((u - 0.3f) / 0.7f)));
        }

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            float u = t - p.delay;
            if (u <= 0f) { SetAlpha(p.image, 0f); if (p.halo != null) SetAlpha(p.halo, 0f); continue; }
            var rt = p.image.rectTransform;
            float pop = Mathf.LerpUnclamped(0.3f, 1f, EaseOutBack(Mathf.Clamp01(u / 0.25f)));
            Vector2 pos;
            float alpha;
            if (style.rise)
            {
                pos = p.start + p.velocity * u + new Vector2(Mathf.Sin(u * 2.2f + p.phase) * p.sway, 0f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * 2.2f + p.phase) * 6f);
                rt.localScale = Vector3.one * pop;
                alpha = Smooth(u / 0.2f) * (1f - Smooth((u - 1.2f) / 0.5f));
            }
            else
            {
                var v = p.velocity + new Vector2(0f, -1500f * u);
                pos = p.velocity * u + new Vector2(0f, -750f * u * u);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f);
                float st = 1f + Mathf.Clamp(v.magnitude / 2400f, 0f, 0.3f);     // stretched along the flight
                rt.localScale = new Vector3(pop / Mathf.Sqrt(st), pop * st, 1f);
                alpha = 1f - Smooth((u - 0.6f) / 0.3f);
            }
            rt.anchoredPosition = pos + new Vector2(0f, 20f);
            SetAlpha(p.image, alpha);
            if (p.halo != null)
            {
                p.halo.rectTransform.anchoredPosition = rt.anchoredPosition;
                p.halo.rectTransform.localScale = Vector3.one * pop;
                SetAlpha(p.halo, alpha * style.haloColor.a);
            }
        }
        for (int i = 0; i < sparks.Count; i++)
        {
            float u = (t - 0.1f - i * 0.1f) / 0.45f;
            var rt = sparks[i].rectTransform;
            rt.anchoredPosition = sparkSpots[i];
            float s = u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
            rt.localScale = Vector3.one * s;
            rt.localRotation = Quaternion.Euler(0f, 0f, 60f * u);
            SetAlpha(sparks[i], 0.9f * s);
        }

        float halfW = root.rect.width / 2f, halfH = root.rect.height / 2f;
        float half = RibbonWidth * BannerScale / 2f;
        float bx = Mathf.Clamp(at.x, -halfW + half + 30f, halfW - half - 30f);
        float by = Mathf.Min(at.y + 215f, halfH - 220f);
        float open = Mathf.Clamp01((t - 0.1f) / 0.32f);             // the ribbon unfurls from its middle
        float leave = Mathf.Clamp01((t - 1.8f) / 0.3f);
        banner.anchoredPosition = new Vector2(bx, by + 18f * EaseOutCubic(leave));
        banner.localScale = new Vector3(Mathf.LerpUnclamped(0.1f, 1f, EaseOutBack(open, 1.1f)) * (1f - 0.06f * leave),
                                        Mathf.LerpUnclamped(0.75f, 1f, EaseOutCubic(open)) * (1f - 0.06f * leave), 1f) * BannerScale;
        SetAlpha(ribbonImage, Smooth(open * 4f) * (1f - leave));
        label.alpha = Smooth((t - 0.26f) / 0.16f) * (1f - leave);
        float sweep = Mathf.Clamp01((t - 0.55f) / 0.55f);
        shineRt.anchoredPosition = new Vector2(Mathf.Lerp(-RibbonWidth / 2f - 80f, RibbonWidth / 2f + 80f, sweep * sweep * (3f - 2f * sweep)), 0f);
        shineRt.gameObject.SetActive(sweep > 0f && sweep < 1f);

        float vu = Mathf.Clamp01((t - 0.45f) / 1.25f);              // "+100" rises from the building
        value.anchoredPosition = at + new Vector2(0f, 40f + 80f * EaseOutCubic(vu));
        value.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01((t - 0.45f) / 0.26f)));
        float va = Smooth((t - 0.45f) / 0.12f) * (1f - Smooth((vu - 0.7f) / 0.3f));
        SetAlpha(valueIcon, va);
        valueText.alpha = va;
    }

    private Vector2 Anchor()
    {
        var cam = Camera.main;
        if (cam == null || target == null) return Vector2.zero;
        Vector2 screen = cam.WorldToScreenPoint(targetBounds.center);
        var eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, eventCam, out var local);
        return local;
    }

    private static Bounds BuildingBounds(GameObject building)
    {
        var b = new Bounds(building.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in building.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sortingLayerName == "Ground") continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private TMP_Text Text(RectTransform rt, string s, float size)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = style.font;
        if (style.outline != null) t.fontSharedMaterial = style.outline;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = s;
        return t;
    }

    private static RectTransform Node(string name, Transform parent, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, float size, Color color)
    {
        var rt = Node(name, parent, Vector2.zero);
        rt.sizeDelta = new Vector2(size, size);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = sprite;
        im.color = color;
        im.raycastTarget = false;
        im.preserveAspect = true;
        if (sprite == null) im.enabled = false;
        return im;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = Mathf.Clamp01(a);
        g.color = c;
    }

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float EaseOutCubic(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
    private static float EaseOutBack(float x, float s = 1.70158f) { x = Mathf.Clamp01(x) - 1f; return 1f + (s + 1f) * x * x * x + s * x * x; }
}
