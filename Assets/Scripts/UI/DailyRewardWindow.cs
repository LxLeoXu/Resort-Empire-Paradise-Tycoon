using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DailyRewardWindow : MonoBehaviour, IPointerClickHandler
{
    [Header("Okno")]
    [Tooltip("Stmavenie hry za oknom.")]
    [SerializeField] private Image dim;
    [Tooltip("Samotné okno: pri otvorení vyskočí, pri zatvorení sa zmenší.")]
    [SerializeField] private RectTransform window;
    [Tooltip("CanvasGroup na okne: ním sa okno plynulo ukáže a skryje.")]
    [SerializeField] private CanvasGroup content;
    [SerializeField] private RectTransform title;

    [Header("Hlavička")]
    [Tooltip("Lúče slnka za darčekom, pomaly sa otáčajú.")]
    [SerializeField] private RectTransform rays;
    [Tooltip("Iskričky na oblohe, blikajú.")]
    [SerializeField] private RectTransform[] sparkles;
    [SerializeField] private RectTransform gift;
    [Tooltip("Veko s mašľou: po dopade darčeka odskočí a vznáša sa nad krabicou.")]
    [SerializeField] private RectTransform lid;
    [Tooltip("Svetlo za darčekom.")]
    [SerializeField] private Image giftGlow;
    [Tooltip("Svetlo, ktoré vyjde z otvorenej krabice.")]
    [SerializeField] private Image boxGlow;
    [Tooltip("Rozsvietený otvor krabice.")]
    [SerializeField] private Image slotGlow;
    [Tooltip("Iskričky, ktoré stúpajú z otvorenej krabice.")]
    [SerializeField] private Image[] motes;

    [Header("Odmena")]
    [SerializeField] private RectTransform card;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardAmount;
    [SerializeField] private TMP_Text rewardName;
    [SerializeField] private CanvasGroup note;
    [SerializeField] private Button claimButton;
    [Tooltip("Iskričky, ktoré vyletia z darčeka a po stlačení tlačidla.")]
    [SerializeField] private Image[] burst;

    [Header("Pohyb")]
    [Tooltip("Ako veľmi sa hra za oknom stmaví (0 až 1).")]
    [Range(0f, 1f)] [SerializeField] private float dimAlpha = 0.6f;
    [Tooltip("Rýchlosť otáčania lúčov v stupňoch za sekundu.")]
    [SerializeField] private float raysSpeed = 7f;

    public event Action Claimed;
    public event Action Closed;

    public bool IsOpen => phase != Phase.Hidden;

    private enum Phase { Hidden, Opening, Idle, Closing }

    private const float DropAt = 0.12f;
    private const float DropTime = 0.55f;
    private const float DropHeight = 200f;
    private const float WiggleAt = 0.72f;
    private const float WiggleTime = 0.4f;
    private const float LidAt = 1.12f;
    private const float LidTime = 0.45f;
    private const float LidLift = 44f;
    private const float BurstAt = 1.2f;
    private const float CardAt = 1.3f;
    private const float CountAt = 1.45f;
    private const float CountTime = 0.6f;
    private const float NoteAt = 1.75f;
    private const float ButtonAt = 1.85f;
    private const float IntroLength = 2.3f;
    private const float OutroHold = 0.26f;
    private const float OutroFade = 0.3f;
    private const float Gravity = 900f;

    private static readonly Color32[] BurstColors =
    {
        new Color32(255, 255, 255, 255), new Color32(255, 221, 128, 255),
        new Color32(255, 107, 94, 255), new Color32(111, 227, 212, 255),
    };

    private Phase phase = Phase.Hidden;
    private float time;
    private float closing;
    private float clock;
    private int amount;
    private int shownAmount;
    private bool captured;
    private Vector2 giftRest;
    private Vector2 lidRest;
    private Vector3[] sparkleRest;
    private Color giftGlowColor;
    private Color boxGlowColor;
    private Color slotGlowColor;
    private Vector2[] moteRest;
    private LayoutElement amountWidth;
    private RectTransform button;
    private float burstTime = -1f;
    private Vector2 burstOrigin;
    private float burstFrom;
    private float burstTo;
    private float burstSpeed;

    public void Show(Sprite icon, int rewardCount, string currencyName)
    {
        gameObject.SetActive(true);
        Capture();
        amount = Mathf.Max(0, rewardCount);
        rewardIcon.sprite = icon;
        rewardName.text = currencyName;
        rewardAmount.text = Format(amount);
        amountWidth.preferredWidth = rewardAmount.preferredWidth;
        shownAmount = -1;
        time = 0f;
        closing = 0f;
        burstTime = -1f;
        phase = Phase.Opening;
        Apply();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (phase == Phase.Opening) time = IntroLength;
    }

    private void Awake()
    {
        Capture();
        claimButton.onClick.AddListener(Claim);
    }

    private void OnDisable()
    {
        phase = Phase.Hidden;
    }

    private void Update()
    {
        Advance(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        giftRest = gift.anchoredPosition;
        lidRest = lid.anchoredPosition;
        sparkleRest = new Vector3[sparkles.Length];
        for (int i = 0; i < sparkles.Length; i++) sparkleRest[i] = sparkles[i].localScale;
        giftGlowColor = giftGlow.color;
        boxGlowColor = boxGlow.color;
        slotGlowColor = slotGlow.color;
        moteRest = new Vector2[motes.Length];
        for (int i = 0; i < motes.Length; i++) moteRest[i] = motes[i].rectTransform.anchoredPosition;
        amountWidth = rewardAmount.GetComponent<LayoutElement>();
        button = (RectTransform)claimButton.transform;
    }

    private void Claim()
    {
        if (phase != Phase.Opening && phase != Phase.Idle) return;
        time = Mathf.Max(time, IntroLength);
        phase = Phase.Closing;
        closing = 0f;
        StartBurst(card.anchoredPosition, 0f, 360f, 620f);
        Claimed?.Invoke();
    }

    private void Advance(float deltaTime)
    {
        if (phase == Phase.Hidden) return;
        clock += deltaTime;
        float before = time;
        time += deltaTime;
        if (before < BurstAt && time >= BurstAt) StartBurst(giftRest, 25f, 155f, 520f);
        if (phase == Phase.Opening && time >= IntroLength) phase = Phase.Idle;
        if (burstTime >= 0f) burstTime += deltaTime;
        if (phase == Phase.Closing)
        {
            closing += deltaTime;
            if (closing >= OutroHold + OutroFade)
            {
                phase = Phase.Hidden;
                gameObject.SetActive(false);
                Closed?.Invoke();
                return;
            }
        }
        Apply();
    }

    private void Apply()
    {
        float open = phase == Phase.Closing ? 1f - Smooth(Span(closing, OutroHold, OutroFade)) : 1f;
        float press = phase == Phase.Closing ? Bump(closing, 0.18f) : 0f;

        dim.color = new Color(0f, 0f, 0f, dimAlpha * Smooth(Span(time, 0f, 0.25f)) * open);
        content.alpha = Span(time, 0f, 0.14f) * open;
        window.localScale = Vector3.one * (Mathf.LerpUnclamped(0.72f, 1f, EaseOutBack(Span(time, 0f, 0.38f))) * Mathf.Lerp(0.86f, 1f, open));
        title.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Span(time, 0.2f, 0.34f)));

        rays.localRotation = Quaternion.Euler(0f, 0f, clock * raysSpeed);
        rays.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, EaseOutCubic(Span(time, 0.05f, 0.7f)));
        for (int i = 0; i < sparkles.Length; i++)
        {
            float twinkle = 0.5f + 0.5f * Mathf.Sin(clock * (1.7f + 0.45f * i) + i * 2.1f);
            sparkles[i].localScale = sparkleRest[i] * (Mathf.Lerp(0.35f, 1f, twinkle) * Smooth(Span(time, 0.3f + 0.08f * i, 0.3f)));
            sparkles[i].localRotation = Quaternion.Euler(0f, 0f, clock * (i % 2 == 0 ? 20f : -20f));
        }

        float drop = Span(time, DropAt, DropTime);
        float land = Mathf.Exp(-Square((drop - 0.3636f) / 0.07f));
        float wiggle = Span(time, WiggleAt, WiggleTime);
        float lift = Span(time, LidAt, LidTime);
        float afloat = Smooth(Span(time, LidAt + LidTime, 0.5f));
        float bob = Mathf.Sin(clock * 2f * Mathf.PI / 2.4f);
        gift.anchoredPosition = giftRest + Vector2.up * (DropHeight * (1f - EaseOutBounce(drop)));
        gift.localScale = drop <= 0f
            ? Vector3.zero
            : new Vector3(1f + 0.12f * land, 1f - 0.14f * land, 1f) * (1f + 0.05f * Mathf.Sin(wiggle * Mathf.PI));
        gift.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(wiggle * 5f * Mathf.PI) * 7f * (1f - wiggle));
        lid.anchoredPosition = lidRest + new Vector2(-8f * EaseOutCubic(lift), LidLift * EaseOutBack(lift) + 4f * bob * afloat);
        lid.localRotation = Quaternion.Euler(0f, 0f, 12f * EaseOutCubic(lift) + 2f * Mathf.Sin(clock * 2f * Mathf.PI / 3.1f) * afloat);

        float shine = Smooth(Span(time, LidAt, 0.12f)) * Mathf.Lerp(1f, 0.78f + 0.12f * bob, Smooth(Span(time, LidAt + 0.15f, 0.5f)));
        boxGlow.color = new Color(boxGlowColor.r, boxGlowColor.g, boxGlowColor.b, boxGlowColor.a * shine);
        boxGlow.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, EaseOutBack(Span(time, LidAt, 0.4f)));
        slotGlow.color = new Color(slotGlowColor.r, slotGlowColor.g, slotGlowColor.b, slotGlowColor.a * shine);
        for (int i = 0; i < motes.Length; i++)
        {
            float rise = Mathf.Repeat(clock / 1.9f + (float)i / motes.Length, 1f);
            float fade = Mathf.Sin(rise * Mathf.PI);
            RectTransform mote = motes[i].rectTransform;
            mote.anchoredPosition = moteRest[i] + new Vector2(Mathf.Sin((rise + i * 0.37f) * 2f * Mathf.PI) * 10f, rise * 78f);
            mote.localScale = Vector3.one * (fade * Mathf.Lerp(0.55f, 1f, Noise(i, 7f)));
            Color moteColor = motes[i].color;
            moteColor.a = fade * afloat;
            motes[i].color = moteColor;
        }
        float halo = Smooth(Span(time, 0.3f, 0.5f)) * (0.78f + 0.22f * Mathf.Sin(clock * 2f * Mathf.PI / 2.8f));
        giftGlow.color = new Color(giftGlowColor.r, giftGlowColor.g, giftGlowColor.b, giftGlowColor.a * halo);

        card.localScale = Vector3.one * (EaseOutBack(Span(time, CardAt, 0.4f)) * (1f + 0.05f * press));
        int counted = Mathf.RoundToInt(amount * EaseOutCubic(Span(time, CountAt, CountTime)));
        if (counted != shownAmount)
        {
            shownAmount = counted;
            rewardAmount.text = Format(counted);
        }
        rewardAmount.rectTransform.localScale = Vector3.one * (1f + 0.2f * Bump(time - (CountAt + CountTime), 0.3f));
        note.alpha = Smooth(Span(time, NoteAt, 0.3f));

        float pulse = phase == Phase.Closing ? 0f : 0.035f * Mathf.Sin(clock * 2f * Mathf.PI / 1.4f) * Smooth(Span(time, ButtonAt + 0.4f, 0.4f));
        button.localScale = Vector3.one * (EaseOutBack(Span(time, ButtonAt, 0.36f)) * (1f + pulse) * (1f - 0.1f * press));
        claimButton.interactable = phase != Phase.Closing && time >= ButtonAt + 0.15f;

        ApplyBurst();
    }

    private void StartBurst(Vector2 origin, float fromAngle, float toAngle, float speed)
    {
        burstOrigin = origin;
        burstFrom = fromAngle;
        burstTo = toAngle;
        burstSpeed = speed;
        burstTime = 0f;
    }

    private void ApplyBurst()
    {
        for (int i = 0; i < burst.Length; i++)
        {
            float life = 0.65f + 0.4f * Noise(i, 1f);
            float age = burstTime / life;
            bool alive = burstTime >= 0f && age < 1f;
            if (burst[i].enabled != alive) burst[i].enabled = alive;
            if (!alive) continue;

            float angle = Mathf.Lerp(burstFrom, burstTo, (i + Noise(i, 2f)) / burst.Length) * Mathf.Deg2Rad;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (burstSpeed * Mathf.Lerp(0.5f, 1f, Noise(i, 3f)));
            RectTransform particle = burst[i].rectTransform;
            particle.anchoredPosition = burstOrigin + velocity * burstTime + Vector2.down * (0.5f * Gravity * burstTime * burstTime);
            particle.localScale = Vector3.one * (Mathf.Lerp(0.45f, 1f, Noise(i, 4f)) * (1f - age * age));
            particle.localRotation = Quaternion.Euler(0f, 0f, (Noise(i, 5f) - 0.5f) * 720f * burstTime);
            Color color = BurstColors[i % BurstColors.Length];
            color.a = 1f - Smooth(Span(age, 0.6f, 0.4f));
            burst[i].color = color;
        }
    }

    private static string Format(int value) => "+" + value.ToString("N0", CultureInfo.InvariantCulture);

    private static float Span(float value, float start, float length) => Mathf.Clamp01((value - start) / length);

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Square(float value) => value * value;

    private static float Bump(float value, float length) => value <= 0f || value >= length ? 0f : Mathf.Sin(value / length * Mathf.PI);

    private static float Noise(int index, float seed) => Mathf.Repeat(Mathf.Sin(index * 12.9898f + seed * 78.233f) * 43758.5453f, 1f);

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    private static float EaseOutBounce(float t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;
        if (t < 1f / d) return n * t * t;
        if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
        if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
        t -= 2.625f / d;
        return n * t * t + 0.984375f;
    }
}
