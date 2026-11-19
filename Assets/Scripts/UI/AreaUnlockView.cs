using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Look and motion of the S9 (Area Unlock) window. AreaUnlockUI fills the texts and decides what the button does;
/// this component picks the area's theme by the preview sprite the spot passed in (the postcard of the pool or of
/// the bungalow), shows the locked / not affordable states and plays the opening.
/// Motion follows the usual conventions: entering elements decelerate (ease-out, a little overshoot), siblings are
/// staggered by 50 ms, the whole intro is under 0.7 s and the button works from the first frame; an event gets one
/// accent (the stamp slams, the photo flashes like a camera); errors shake sideways (the lock, a price the player
/// cannot pay); idle motion is rare and small (one shine across the button every 3.2 s, only when it can be bought).
/// </summary>
public class AreaUnlockView : MonoBehaviour
{
    [Serializable]
    public class Theme
    {
        [Tooltip("The spot's Area Preview (the postcard) that selects this theme")]
        public Sprite photo;
        public Sprite ribbon;
        public Sprite stamp;
        public Sprite needIcon;
        [Tooltip("The postcard's tilt in degrees")]
        public float tilt = -2.5f;
        [Tooltip("The stamp's centre, from the postcard's centre (canvas units)")]
        public Vector2 stampOffset = new Vector2(250f, 170f);
        public float stampTilt = 7f;
        [Tooltip("No worker comes with this building (the Spa): the worker row is hidden, the rows below move up")]
        public bool noWorker;
    }

    [Tooltip("AreaUnlockUI's preview image: the spot puts its postcard there")]
    [SerializeField] private Image previewImage;
    [Tooltip("AreaUnlockUI's unlock button: interactable = it can be bought now")]
    [SerializeField] private Button unlock;
    [SerializeField] private Theme[] themes = new Theme[0];
    [SerializeField] private Sprite lockedRibbon;

    [Header("Parts")]
    [SerializeField] private Image dim;
    [SerializeField] private RectTransform window;
    [SerializeField] private CanvasGroup windowGroup;
    [SerializeField] private RectTransform ribbon;
    [SerializeField] private Image ribbonImage;
    [SerializeField] private RectTransform card;
    [SerializeField] private Image flash;
    [SerializeField] private RectTransform stamp;
    [SerializeField] private Image stampImage;
    [SerializeField] private Image needIcon;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private RectTransform lockBadge;
    [SerializeField] private TMP_Text lockedCaption;
    [SerializeField] private CanvasGroup[] rows = new CanvasGroup[0];
    [SerializeField] private RectTransform priceTag;
    [SerializeField] private RectTransform unlockButton;
    [SerializeField] private Image unlockButtonImage;
    [SerializeField] private RectTransform buttonShine;
    [SerializeField] private Image unlockIcon;
    [SerializeField] private TMP_Text unlockLabel;
    [SerializeField] private TMP_Text priceText;
    [Tooltip("The worker row: a leading \"+1\" is shown in the gain colour")]
    [SerializeField] private TMP_Text workerText;

    [Header("States")]
    [SerializeField] private Sprite buttonOn;
    [SerializeField] private Sprite buttonOff;
    [SerializeField] private Sprite iconUnlock;
    [SerializeField] private Sprite iconLocked;
    [Tooltip("On the button when coins are missing: the thing that is missing")]
    [SerializeField] private Sprite iconCoin;
    [SerializeField] private Color labelOn = Color.white;
    [SerializeField] private Color labelOff = new Color32(0x6B, 0x5B, 0x4B, 0xFF);
    [SerializeField] private Color priceOk = new Color32(0x45, 0x1A, 0x03, 0xFF);
    [SerializeField] private Color priceShort = new Color32(0xB9, 0x1C, 0x1C, 0xFF);
    [SerializeField] private Color gain = new Color32(0x15, 0x80, 0x3D, 0xFF);
    [SerializeField] private string lockedCaptionFormat = "Opens after wave {0}";
    [SerializeField] private float dimAlpha = 0.72f;

    [Tooltip("Stops the clock (for recording frame by frame with Advance)")]
    public bool paused;

    private float clock;
    private Theme theme;
    private BuildingSpot spot;
    private Sprite shownPhoto;
    private Material labelMaterial;
    private Material labelPlainMaterial;
    private Vector2 ribbonRest, cardRest, priceRest;
    private Vector2[] rowRest;
    private Vector2[] rowOffset = new Vector2[0];
    private const int WorkerRow = 2;              // rows: title, demand, worker, satisfaction, price, buttons
    private bool rested, locked, shortOfCoins;

    private const float Slam = 0.46f;          // the stamp lands
    private const float Intro = 0.7f;
    private const float ShineEvery = 3.2f;

    private bool CanBuy => unlock != null && unlock.interactable;

    private void Rest()
    {
        if (rested) return;
        rested = true;
        if (ribbon != null) ribbonRest = ribbon.anchoredPosition;
        if (card != null) cardRest = card.anchoredPosition;
        if (priceTag != null) priceRest = priceTag.anchoredPosition;
        rowRest = new Vector2[rows.Length];
        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null) rowRest[i] = ((RectTransform)rows[i].transform).anchoredPosition;
    }

    private void OnEnable()
    {
        Rest();
        clock = 0f;
        shownPhoto = null;
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
        Pose();
    }

    private void Pose()
    {
        Sprite photo = previewImage != null ? previewImage.sprite : null;
        if (photo != shownPhoto)
            ApplyTheme(photo);
        ApplyState();
        Animate();
    }

    private void ApplyTheme(Sprite photo)
    {
        shownPhoto = photo;
        theme = null;
        foreach (var t in themes)
            if (t != null && t.photo == photo)
                theme = t;
        spot = null;
        foreach (var s in FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.AreaPreview == photo)
                spot = s;
        bool noWorker = theme != null && theme.noWorker && rows.Length > WorkerRow + 1;
        if (rows.Length > WorkerRow && rows[WorkerRow] != null && rows[WorkerRow].gameObject.activeSelf == noWorker)
            rows[WorkerRow].gameObject.SetActive(!noWorker);
        rowOffset = new Vector2[rows.Length];                 // no worker: the two rows round it close up, centred
        if (noWorker)
        {
            rowOffset[WorkerRow - 1] = (rowRest[WorkerRow] - rowRest[WorkerRow - 1]) * 0.5f;
            rowOffset[WorkerRow + 1] = (rowRest[WorkerRow] - rowRest[WorkerRow + 1]) * 0.5f;
        }
        if (theme == null)
            return;
        if (stampImage != null) stampImage.sprite = theme.stamp;
        if (needIcon != null) needIcon.sprite = theme.needIcon;
        if (stamp != null) stamp.anchoredPosition = theme.stampOffset;
    }

    private void ApplyState()
    {
        locked = spot != null && !spot.isOccupied && !spot.IsAvailable;
        int coins = ResourceManager.Instance != null ? ResourceManager.Instance.GetResource(ResourceType.Coins) : 0;
        shortOfCoins = !locked && spot != null && coins < spot.UnlockCost;
        bool canBuy = CanBuy;

        if (ribbonImage != null)
            ribbonImage.sprite = locked || theme == null ? lockedRibbon : theme.ribbon;
        if (lockedOverlay != null && lockedOverlay.activeSelf != locked)
            lockedOverlay.SetActive(locked);
        if (locked && lockedCaption != null && spot != null)
            lockedCaption.text = string.Format(lockedCaptionFormat, spot.RequiredWave);
        if (stamp != null && stamp.gameObject.activeSelf == locked)
            stamp.gameObject.SetActive(!locked);
        if (unlockButtonImage != null)
            unlockButtonImage.sprite = canBuy ? buttonOn : buttonOff;
        if (unlockIcon != null)
        {
            unlockIcon.sprite = locked ? iconLocked : shortOfCoins && iconCoin != null ? iconCoin : iconUnlock;
            unlockIcon.color = Color.white;
        }
        if (unlockLabel != null)
        {
            if (labelMaterial == null) { labelMaterial = unlockLabel.fontSharedMaterial; labelPlainMaterial = unlockLabel.font.material; }
            unlockLabel.color = canBuy ? labelOn : labelOff;
            unlockLabel.fontSharedMaterial = canBuy ? labelMaterial : labelPlainMaterial;
            string s = unlockLabel.text;
            if (shortOfCoins && !s.Contains("<color"))           // "NEED 300 MORE": the missing amount in red
            {
                int a = s.IndexOfAny("0123456789".ToCharArray());
                if (a >= 0)
                {
                    int b = a;
                    while (b < s.Length && (char.IsDigit(s[b]) || s[b] == ',' || s[b] == '.' || s[b] == '\u00A0')) b++;
                    unlockLabel.text = s.Substring(0, a) + "<color=#" + ColorUtility.ToHtmlStringRGB(priceShort) + ">" + s.Substring(a, b - a) + "</color>" + s.Substring(b);
                }
            }
            CentreIconAndLabel();
        }
        if (priceText != null)
            priceText.color = shortOfCoins ? priceShort : priceOk;
        if (workerText != null && workerText.text.StartsWith("+"))       // "+1 Worker": the gain in green
        {
            string s = workerText.text;
            int space = s.IndexOf(' ');
            if (space > 1)
                workerText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(gain) + ">" + s.Substring(0, space) + "</color>" + s.Substring(space);
        }
    }

    /// <summary>Icon and label sit in the button's middle as one group, whatever the label's length.</summary>
    private void CentreIconAndLabel()
    {
        if (unlockIcon == null) return;
        var icon = unlockIcon.rectTransform;
        var label = unlockLabel.rectTransform;
        float iw = icon.rect.width, gap = 12f, tw = unlockLabel.GetPreferredValues(unlockLabel.text).x;
        float left = -(iw + gap + tw) / 2f;
        icon.anchoredPosition = new Vector2(left + iw / 2f, icon.anchoredPosition.y);
        label.anchoredPosition = new Vector2(left + iw + gap + tw / 2f, label.anchoredPosition.y);
    }

    private void Animate()
    {
        float t = clock;
        if (dim != null)
        {
            var c = dim.color;
            c.a = dimAlpha * Smooth(t / 0.2f);
            dim.color = c;
        }
        if (window != null)
            window.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, EaseOutBack(t / 0.26f));
        if (windowGroup != null)
            windowGroup.alpha = Smooth(t / 0.12f);
        if (ribbon != null)                                   // unfurls from its middle, a little late
        {
            float p = Mathf.Clamp01((t - 0.08f) / 0.3f);
            ribbon.localScale = new Vector3(Mathf.LerpUnclamped(0.1f, 1f, EaseOutBack(p, 1.3f)), Mathf.LerpUnclamped(0.7f, 1f, EaseOutCubic(p)), 1f);
            ribbon.anchoredPosition = ribbonRest + new Vector2(0f, 14f * (1f - EaseOutCubic(p)));
        }
        if (card != null)                                     // swings in, then gives a little under the stamp
        {
            float tilt = theme != null ? theme.tilt : 0f;
            float p = Mathf.Clamp01((t - 0.04f) / 0.38f);
            float e = EaseOutBack(p, 1.4f);
            float hit = t < Slam ? 0f : Mathf.Sin(Mathf.Clamp01((t - Slam) / 0.16f) * Mathf.PI);
            card.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(tilt - 7f * Mathf.Sign(tilt == 0 ? 1 : tilt), tilt, e));
            card.localScale = Vector3.one * (Mathf.LerpUnclamped(0.9f, 1f, e) - 0.015f * hit);
            card.anchoredPosition = cardRest - new Vector2(0f, 30f * (1f - EaseOutCubic(p)) + 4f * hit);
        }
        if (flash != null)                                    // the photo is taken: a camera flash as the stamp lands
        {
            float f = locked ? 0f : (t < Slam ? 0f : 1f - Smooth((t - Slam) / 0.32f));
            flash.color = new Color(1f, 1f, 1f, 0.62f * f);
        }
        if (stamp != null)
        {
            float p = Mathf.Clamp01((t - (Slam - 0.14f)) / 0.14f);
            float s = p < 1f ? Mathf.Lerp(1.9f, 1f, p * p) : 1f - 0.07f * Mathf.Sin(Mathf.Clamp01((t - Slam) / 0.14f) * Mathf.PI);
            stamp.localScale = Vector3.one * s;
            stamp.localRotation = Quaternion.Euler(0f, 0f, (theme != null ? theme.stampTilt : 0f) + 12f * (1f - p));
            if (stampImage != null)
                stampImage.color = new Color(1f, 1f, 1f, Smooth(p * 3f));
        }
        if (lockBadge != null)                                // "locked": the padlock shakes once
        {
            float u = Mathf.Clamp01((t - 0.36f) / 0.42f);
            float shake = locked ? Mathf.Sin(u * Mathf.PI * 5f) * (1f - u) : 0f;
            lockBadge.localRotation = Quaternion.Euler(0f, 0f, 12f * shake);
            lockBadge.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01((t - 0.2f) / 0.24f)));
        }
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;
            float p = Mathf.Clamp01((t - 0.16f - 0.05f * i) / 0.22f);
            rows[i].alpha = Smooth(p);
            ((RectTransform)rows[i].transform).anchoredPosition = rowRest[i] + (i < rowOffset.Length ? rowOffset[i] : Vector2.zero)
                                                                  + new Vector2(22f * (1f - EaseOutCubic(p)), 0f);
        }
        if (priceTag != null)                                 // a price that cannot be paid shakes sideways once
        {
            float u = Mathf.Clamp01((t - 0.5f) / 0.4f);
            float shake = shortOfCoins ? Mathf.Sin(u * Mathf.PI * 6f) * (1f - u) : 0f;
            priceTag.anchoredPosition = priceRest + new Vector2(7f * shake, 0f);
        }
        if (unlockButton != null)
        {
            float pop = Mathf.Clamp01((t - 0.38f) / 0.26f);
            unlockButton.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, EaseOutBack(pop));
        }
        if (buttonShine != null)                              // one light sweep, then again every 3.2 s
        {
            bool on = CanBuy && t > Intro;
            float u = on ? ((t - Intro) % ShineEvery) / 0.55f : 1f;
            buttonShine.gameObject.SetActive(on && u < 1f);
            float w = ((RectTransform)buttonShine.parent).rect.width;
            buttonShine.anchoredPosition = new Vector2(Mathf.Lerp(-w / 2f - 60f, w / 2f + 60f, Smooth(u)), 0f);
        }
    }

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float EaseOutCubic(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
    private static float EaseOutBack(float x, float s = 1.70158f) { x = Mathf.Clamp01(x) - 1f; return 1f + (s + 1f) * x * x * x + s * x * x; }
}
