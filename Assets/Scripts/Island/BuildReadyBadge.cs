using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildReadyBadge : MonoBehaviour
{
    private const float PopTime = 0.35f;
    private const float SweepStart = 0.35f, SweepTime = 0.45f;
    private const float SparkStart = 0.2f, SparkTime = 1f;
    private const float ChipStart = 0.5f, ChipTime = 0.2f;
    private const float EventTime = 1.2f;
    private static readonly Color PriceColor = new Color32(0x45, 0x1A, 0x03, 0xFF);
    private static readonly Color ShortColor = new Color32(0xDC, 0x26, 0x26, 0xFF);

    private Image icon, halo, band;
    private RectTransform sweep, chip;
    private RectTransform[] sparks;
    private TMP_Text price;
    private Sprite plainSprite, readySprite;
    private float haloAlpha;
    private int cost = -1, shownCoins = int.MinValue;
    private bool ready, playing;
    private float eventStart;

    public void Init(Image iconImage, Sprite ready, Sprite haloSprite, Sprite flashSprite, Sprite sparkSprite,
                     Sprite chipSprite, Sprite coinSprite, TMP_FontAsset font, int unlockCost)
    {
        icon = iconImage;
        plainSprite = icon.sprite;
        readySprite = ready;
        cost = unlockCost;
        float size = icon.rectTransform.rect.width;

        halo = MakeImage("BuildReadyHalo", transform, haloSprite, size * 1.7f, size * 1.7f);
        halo.transform.SetSiblingIndex(icon.transform.GetSiblingIndex());
        haloAlpha = 0.43f;

        var maskImage = MakeImage("BuildReadySweep", icon.transform, flashSprite, size, size);
        maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        sweep = maskImage.rectTransform;
        band = MakeImage("Band", sweep, null, size * 0.2f, size * 1.6f);
        band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);

        sparks = new RectTransform[6];
        for (int i = 0; i < sparks.Length; i++)
            sparks[i] = MakeImage("BuildReadySpark", transform, sparkSprite, size * 0.22f, size * 0.22f).rectTransform;

        float h = Mathf.Round(size * 0.42f);
        var chipImage = MakeImage("BuildReadyPrice", transform, chipSprite, h * 3f, h);
        chipImage.type = Image.Type.Sliced;
        chipImage.pixelsPerUnitMultiplier = chipSprite.border.y > 0f ? chipSprite.border.y / (h * 0.5f) * (100f / chipSprite.pixelsPerUnit) : 1f;
        chip = chipImage.rectTransform;
        chip.anchoredPosition = new Vector2(0f, -(size * 0.5f + h * 0.5f - size * 0.08f));
        var row = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(Mathf.RoundToInt(h * 0.18f), Mathf.RoundToInt(h * 0.36f), 0, 0);
        row.spacing = Mathf.Round(h * 0.12f);
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var coin = MakeImage("Coin", chip, coinSprite, h * 0.74f, h * 0.74f);
        coin.gameObject.AddComponent<LayoutElement>().preferredWidth = h * 0.74f;
        var textGo = new GameObject("Amount", typeof(RectTransform));
        textGo.transform.SetParent(chip, false);
        price = textGo.AddComponent<TextMeshProUGUI>();
        price.font = font;
        price.fontSize = h * 0.66f;
        price.alignment = TextAlignmentOptions.MidlineLeft;
        price.textWrappingMode = TextWrappingModes.NoWrap;
        price.raycastTarget = false;
        price.rectTransform.sizeDelta = new Vector2(10f, h);

        Apply(0f, false);
    }

    public void SetReady(bool value)
    {
        if (value == ready) return;
        ready = value;
        if (ready && isActiveAndEnabled) Play();
        if (!ready) Apply(0f, false);
    }

    private void OnEnable()
    {
        if (icon != null && ready) Play();
    }

    private void Play()
    {
        playing = true;
        eventStart = Time.unscaledTime;
        icon.sprite = readySprite;
        Apply(0f, true);
    }

    private void Update()
    {
        if (icon == null) return;
        if (ready) RefreshPrice();
        if (!playing) return;
        float t = Time.unscaledTime - eventStart;
        if (t >= EventTime)
        {
            playing = false;
            t = EventTime;
        }
        Apply(t, ready);
    }

    private void RefreshPrice()
    {
        int coins = ResourceManager.Instance != null ? ResourceManager.Instance.GetResource(ResourceType.Coins) : 0;
        if (coins == shownCoins) return;
        shownCoins = coins;
        int missing = cost - coins;
        price.text = missing > 0
            ? "−" + missing.ToString("N0", CultureInfo.InvariantCulture)
            : cost.ToString("N0", CultureInfo.InvariantCulture);
        price.color = missing > 0 ? ShortColor : PriceColor;
    }

    private void Apply(float t, bool on)
    {
        icon.sprite = on ? readySprite : plainSprite;
        halo.enabled = on;
        chip.gameObject.SetActive(on && t >= ChipStart);
        sweep.gameObject.SetActive(on && t >= SweepStart && t <= SweepStart + SweepTime);
        foreach (var s in sparks) s.gameObject.SetActive(false);
        if (!on)
        {
            icon.rectTransform.localScale = Vector3.one;
            return;
        }
        float pop = t < PopTime ? Mathf.Lerp(0.6f, 1f, EaseOutBack(t / PopTime)) : 1f;
        icon.rectTransform.localScale = Vector3.one * pop;
        var c = halo.color;
        c.a = haloAlpha * Mathf.Clamp01(t / PopTime);
        halo.color = c;
        if (sweep.gameObject.activeSelf)
        {
            float u = (t - SweepStart) / SweepTime;
            float w = sweep.rect.width;
            band.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-0.9f, 0.9f, u) * w, 0f);
            band.color = new Color(1f, 1f, 1f, 0.6f * Mathf.Sin(u * Mathf.PI));
        }
        if (t >= SparkStart && t <= SparkStart + SparkTime)
        {
            float u = (t - SparkStart) / SparkTime;
            float size = icon.rectTransform.rect.width;
            for (int i = 0; i < sparks.Length; i++)
            {
                float a = Mathf.Deg2Rad * (-60f + i * 60f);
                float r = size * (0.55f + 0.5f * u);
                sparks[i].gameObject.SetActive(true);
                sparks[i].anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                sparks[i].localScale = Vector3.one * (0.6f + 0.4f * (1f - u));
                sparks[i].GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f - u);
            }
        }
        if (chip.gameObject.activeSelf)
            chip.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, EaseOutBack(Mathf.Clamp01((t - ChipStart) / ChipTime)));
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(w, h);
        return img;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
