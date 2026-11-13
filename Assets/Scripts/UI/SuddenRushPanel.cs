using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SuddenRushPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Samotný panel: pri otvorení vyskočí a zatrasie sa, pri zatvorení sa zmenší.")]
    [SerializeField] private RectTransform panel;
    [Tooltip("CanvasGroup na paneli: ním sa panel plynulo ukáže a skryje.")]
    [SerializeField] private CanvasGroup content;
    [SerializeField] private TMP_Text title;
    [Tooltip("Text nadpisu, {0} je názov budovy.")]
    [SerializeField] private string titleFormat = "Sudden rush at {0}!";
    [Tooltip("Hostia v hlavičke, poskakujú.")]
    [SerializeField] private RectTransform[] guests;
    [Tooltip("Výstražný trojuholník v rohu panela, občas sa zatrasie.")]
    [SerializeField] private RectTransform alert;

    [Header("Tlačidlá")]
    [SerializeField] private Button moveWorkerButton;
    [SerializeField] private Button useEnergyButton;
    [SerializeField] private Button ignoreButton;
    [Tooltip("Cena zrýchlenia v energii na tlačidle Use Energy.")]
    [SerializeField] private TMP_Text energyCost;

    [Header("Ukazovateľ na budovu (keď Show dostane budovu)")]
    [Tooltip("Obrys šípky k budove. Leží pod rámom panela, takže vidno len časť mimo panela.")]
    [SerializeField] private RectTransform tailEdge;
    [Tooltip("Biela výplň šípky. Leží nad rámom panela a otvára jeho obrys tam, kde šípka vychádza.")]
    [SerializeField] private RectTransform tailFill;
    [Tooltip("Časť obrazovky s ostrovom. Krúžok sa na nej orezáva (napravo je panel vlny).")]
    [SerializeField] private RectTransform pointerArea;
    [Tooltip("Krúžok okolo značky „bez obsluhy“ na budove, pulzuje.")]
    [SerializeField] private Image ring;
    [Tooltip("Ozvena krúžku: rozbieha sa od neho a mizne.")]
    [SerializeField] private Image ringEcho;

    [Header("Miesto na obrazovke (keď Show dostane budovu)")]
    [Tooltip("Po aký podiel šírky obrazovky siaha ostrov. Napravo je panel vlny, tam sa panel nepostaví.")]
    [SerializeField] private float islandRight = 0.65f;
    [Tooltip("Voľné miesto hore pre hornú lištu, aj s hosťami, ktorí trčia nad panel (jednotky plátna).")]
    [SerializeField] private float keepTop = 190f;
    [Tooltip("Voľné miesto dole pre tlačidlo vlny (jednotky plátna).")]
    [SerializeField] private float keepBottom = 180f;
    [Tooltip("Odstup od ľavého okraja obrazovky a od panela vlny (jednotky plátna).")]
    [SerializeField] private float keepSides = 30f;
    [Tooltip("Odstup panela od budovy, na ktorú prišiel nával (jednotky plátna).")]
    [SerializeField] private float buildingGap = 40f;

    public event Action MoveWorkerChosen;
    public event Action UseEnergyChosen;
    public event Action Ignored;
    public event Action Closed;

    public bool IsOpen => phase != Phase.Hidden;

    private enum Phase { Hidden, Open, Closing }

    private const float CloseTime = 0.22f;
    private const float PressTime = 0.18f;
    private const int PlaceSteps = 8;
    private const float HeaderHeight = 100f;
    private const float TailBelowHeader = 60f;
    private const float TailAboveBottom = 80f;
    private const float HeaderRoom = 22f;
    private const float CornerRoom = 52f;
    private const float TailHalfWidth = 26f;
    private const float TailMaxTurn = 70f;
    private const float TailHidden = 20f;
    private const float FrameLine = 7f;
    private const float TipRound = 6f;
    private const float TailOverlap = 1.5f;
    private const float RingRadius = 66f;
    private const float BadgeRadius = 34f;
    private const float RingWeight = 8f;

    private static readonly Color32 Outline = new Color32(0x45, 0x1A, 0x03, 255);

    private Phase phase = Phase.Hidden;
    private float time;
    private float closing;
    private float clock;
    private bool captured;
    private Vector2 panelHome;
    private Vector2 panelRest;
    private Vector2[] guestRest;
    private float alertTilt;
    private RectTransform[] buttons;
    private int pressed = -1;
    private float pressedAt;
    private bool pointing;
    private Vector2 badgeAt;
    private Texture2D tailEdgeTexture;
    private Texture2D tailFillTexture;
    private int previewIndex;

    private RectTransform Area => (RectTransform)panel.parent;

    private Vector2 Anchor
    {
        get
        {
            Rect room = Area.rect;
            return room.min + Vector2.Scale(room.size, panel.anchorMin);
        }
    }

    public void Show(string buildingName, int energyPrice) => Open(buildingName, energyPrice, null);

    public void Show(string buildingName, int energyPrice, Transform building) => Open(buildingName, energyPrice, building);

    private void Open(string buildingName, int energyPrice, Transform building)
    {
        gameObject.SetActive(true);
        Capture();
        pointing = building != null && FindBadge(building, out badgeAt);
        panelRest = pointing ? Place(building) : panelHome;
        AimPointer();
        title.text = string.Format(CultureInfo.InvariantCulture, titleFormat, buildingName);
        energyCost.text = energyPrice.ToString(CultureInfo.InvariantCulture);
        time = 0f;
        closing = 0f;
        pressed = -1;
        phase = Phase.Open;
        Apply();
    }

    public void Close()
    {
        if (phase != Phase.Open) return;
        phase = Phase.Closing;
        closing = 0f;
    }

    private void Awake()
    {
        Capture();
        moveWorkerButton.onClick.AddListener(() => Choose(0, MoveWorkerChosen));
        useEnergyButton.onClick.AddListener(() => Choose(1, UseEnergyChosen));
        ignoreButton.onClick.AddListener(() => { Choose(2, Ignored); Close(); });
    }

    private void OnDisable()
    {
        phase = Phase.Hidden;
    }

    private void OnDestroy()
    {
        foreach (var image in new[] { tailEdge, tailFill })
        {
            if (image != null && image.TryGetComponent(out Image graphic) && graphic.sprite != null) Destroy(graphic.sprite);
        }
        if (tailEdgeTexture != null) Destroy(tailEdgeTexture);
        if (tailFillTexture != null) Destroy(tailFillTexture);
    }

    private void Update()
    {
        Advance(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        panelHome = panel.anchoredPosition;
        panelRest = panelHome;
        guestRest = new Vector2[guests.Length];
        for (int i = 0; i < guests.Length; i++) guestRest[i] = guests[i].anchoredPosition;
        alertTilt = alert.localEulerAngles.z;
        buttons = new[]
        {
            (RectTransform)moveWorkerButton.transform, (RectTransform)useEnergyButton.transform, (RectTransform)ignoreButton.transform,
        };
    }

    private void Choose(int index, Action chosen)
    {
        if (phase != Phase.Open) return;
        pressed = index;
        pressedAt = time;
        chosen?.Invoke();
    }

    private void Advance(float deltaTime)
    {
        if (phase == Phase.Hidden) return;
        clock += deltaTime;
        time += deltaTime;
        if (phase == Phase.Closing)
        {
            closing += deltaTime;
            if (closing >= CloseTime)
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
        float open = phase == Phase.Closing ? 1f - Smooth(Span(closing, 0f, CloseTime)) : 1f;
        float shake = Span(time, 0.3f, 0.45f);

        content.alpha = Span(time, 0f, 0.12f) * open;
        panel.localScale = Vector3.one * (Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(Span(time, 0f, 0.34f))) * Mathf.Lerp(0.86f, 1f, open));
        panel.anchoredPosition = panelRest + Vector2.right * (Mathf.Sin(shake * 6f * Mathf.PI) * 12f * (1f - shake));
        title.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Span(time, 0.14f, 0.32f)));

        float ringing = Span(Mathf.Repeat(clock, 1.7f), 0f, 0.55f);
        alert.localScale = Vector3.one * EaseOutBack(Span(time, 0.22f, 0.3f));
        alert.localRotation = Quaternion.Euler(0f, 0f, alertTilt + Mathf.Sin(ringing * 6f * Mathf.PI) * 10f * (1f - ringing));

        for (int i = 0; i < guests.Length; i++)
        {
            float arrive = EaseOutBack(Span(time, 0.08f + 0.06f * i, 0.3f));
            float hop = Mathf.Abs(Mathf.Sin((clock / 0.42f + i * 0.31f) * Mathf.PI));
            guests[i].anchoredPosition = guestRest[i] + Vector2.up * (hop * 22f);
            guests[i].localScale = new Vector3(1f + 0.06f * (1f - hop), 1f - 0.08f * (1f - hop), 1f) * arrive;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            float pop = EaseOutBack(Span(time, 0.26f + 0.09f * i, 0.32f));
            float pulse = i == 0 && phase == Phase.Open ? 0.03f * Mathf.Sin(clock * 2f * Mathf.PI / 1.3f) * Smooth(Span(time, 0.9f, 0.4f)) : 0f;
            float press = i == pressed ? Bump(time - pressedAt, PressTime) : 0f;
            buttons[i].localScale = Vector3.one * (pop * (1f + pulse) * (1f - 0.1f * press));
        }

        if (!pointing) return;
        float ringIn = EaseOutBack(Span(time, 0.3f, 0.32f));
        float beat = 1f + 0.05f * Mathf.Sin(clock * 2f * Mathf.PI / 0.9f);
        ring.rectTransform.localScale = Vector3.one * (ringIn * beat * Mathf.Lerp(0.8f, 1f, open));
        ring.color = new Color(1f, 1f, 1f, Span(time, 0.3f, 0.1f) * open);
        float wave = Mathf.Repeat(clock - 0.5f, 1.1f) / 1.1f;
        ringEcho.rectTransform.localScale = Vector3.one * (1f + 0.6f * Smooth(wave));
        ringEcho.color = new Color(1f, 1f, 1f, 0.7f * (1f - wave) * Span(time, 0.5f, 0.2f) * open);
    }

    private bool FindBadge(Transform building, out Vector2 point)
    {
        point = default;
        if (!Projection(out Camera view, out Camera uiCamera)) return false;
        var marker = building.GetComponent<NoWorkerMarker>();
        Bounds bounds = BuildingBounds(building);
        Vector3 world = marker != null ? marker.BadgePosition : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        if (view.WorldToScreenPoint(world).z < 0f) return false;
        point = ToArea(Area, view, uiCamera, world);
        Rect room = Area.rect;
        return point.x - BadgeRadius >= room.xMin && point.x + BadgeRadius <= room.xMin + room.width * islandRight
            && point.y - BadgeRadius >= room.yMin && point.y + BadgeRadius <= room.yMax;
    }

    private Vector2 Place(Transform building)
    {
        Projection(out Camera view, out Camera uiCamera);
        Bounds bounds = BuildingBounds(building);
        Vector2 a = ToArea(Area, view, uiCamera, bounds.min);
        Vector2 b = ToArea(Area, view, uiCamera, bounds.max);
        Rect avoid = Rect.MinMaxRect(
            Mathf.Min(Mathf.Min(a.x, b.x), badgeAt.x - RingRadius) - buildingGap,
            Mathf.Min(Mathf.Min(a.y, b.y), badgeAt.y - RingRadius) - buildingGap,
            Mathf.Max(Mathf.Max(a.x, b.x), badgeAt.x + RingRadius) + buildingGap,
            Mathf.Max(Mathf.Max(a.y, b.y), badgeAt.y + RingRadius) + buildingGap);

        float ringRoom = RingRadius + buildingGap / 2f;
        var ringBox = new Rect(badgeAt - Vector2.one * ringRoom, Vector2.one * (2f * ringRoom));

        Rect room = Area.rect;
        Vector2 size = panel.rect.size;
        Vector2 anchor = Anchor;
        Vector2 home = anchor + panelHome;
        float x0 = room.xMin + keepSides + size.x / 2f, x1 = room.xMin + room.width * islandRight - keepSides - size.x / 2f;
        float y0 = room.yMin + keepBottom + size.y / 2f, y1 = room.yMax - keepTop - size.y / 2f;
        if (x1 < x0) x0 = x1 = (x0 + x1) / 2f;
        if (y1 < y0) y0 = y1 = (y0 + y1) / 2f;

        Vector2 best = home;
        float bestCover = Cover(home, size, avoid) + RingWeight * Cover(home, size, ringBox);
        float bestReach = (TailBase(home, size) - badgeAt).magnitude;
        float bestDistance = 0f;
        for (int i = 0; i <= PlaceSteps; i++)
        {
            for (int j = 0; j <= PlaceSteps; j++)
            {
                var spot = new Vector2(Mathf.Lerp(x0, x1, (float)i / PlaceSteps), Mathf.Lerp(y0, y1, (float)j / PlaceSteps));
                float cover = Cover(spot, size, avoid) + RingWeight * Cover(spot, size, ringBox);
                float reach = (TailBase(spot, size) - badgeAt).magnitude;
                float distance = (spot - home).sqrMagnitude;
                bool better = cover < bestCover - 1f
                    || (cover < bestCover + 1f && (reach < bestReach - 1f || (reach < bestReach + 1f && distance < bestDistance)));
                if (!better) continue;
                best = spot;
                bestCover = cover;
                bestReach = reach;
                bestDistance = distance;
            }
        }
        return best - anchor;
    }

    private Vector2 TailBase(Vector2 centre, Vector2 size)
    {
        float side = badgeAt.x >= centre.x ? 1f : -1f;
        float low = centre.y - size.y / 2f + TailAboveBottom;
        float high = centre.y + size.y / 2f - HeaderHeight - TailBelowHeader;
        return new Vector2(centre.x + side * size.x / 2f, Mathf.Clamp(badgeAt.y, low, Mathf.Max(low, high)));
    }

    private void AimPointer()
    {
        tailEdge.gameObject.SetActive(pointing);
        tailFill.gameObject.SetActive(pointing);
        pointerArea.gameObject.SetActive(pointing);
        if (!pointing) return;

        Vector2 size = panel.rect.size;
        Vector2 centre = Anchor + panelRest;
        Vector2 start = TailBase(centre, size);
        float side = start.x >= centre.x ? 1f : -1f;
        float turn = Mathf.Abs(TailAngle(start, side) - (side > 0f ? 0f : Mathf.PI));
        float halfWidth = Mathf.Clamp(TailHalfWidth / Mathf.Max(Mathf.Cos(turn), 0.01f), TailHalfWidth, 2.4f * TailHalfWidth);
        float low = centre.y - size.y / 2f + CornerRoom + halfWidth;
        float high = centre.y + size.y / 2f - HeaderHeight - HeaderRoom - halfWidth;
        start.y = Mathf.Clamp(badgeAt.y, low, Mathf.Max(low, high));
        float angle = TailAngle(start, side);
        float length = Mathf.Clamp((badgeAt - start).magnitude - RingRadius - 14f, 44f, 230f);

        Vector2 root = start - centre;
        Vector2 tip = root + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
        Vector2 top = root + Vector2.up * halfWidth;
        Vector2 bottom = root - Vector2.up * halfWidth;
        float hidden = root.x - side * TailHidden;
        float covered = root.x - side * (FrameLine + 3f);
        float density = Mathf.Max(1f, Area.GetComponentInParent<Canvas>().rootCanvas.scaleFactor);
        Vector2 toTop = (top - tip).normalized, toBottom = (bottom - tip).normalized;
        float half = Vector2.Angle(toTop, toBottom) * 0.5f * Mathf.Deg2Rad;
        var cap = (centre: tip + (toTop + toBottom).normalized * (TipRound / Mathf.Sin(half)), radius: TipRound);
        Vector2 capTop = tip + toTop * (TipRound / Mathf.Tan(half));
        Vector2 capBottom = tip + toBottom * (TipRound / Mathf.Tan(half));
        Paint(tailEdge, ref tailEdgeTexture, new[] { new[] { Along(tip, top, hidden), capTop, capBottom, Along(tip, bottom, hidden) } },
            new[] { cap }, FrameLine, Outline, density);
        float seam = root.x + side * TailOverlap;
        Paint(tailFill, ref tailFillTexture, new[]
        {
            new[] { top, capTop, capBottom, bottom },
            new[] { Along(tip, top, covered), Along(tip, top, seam), Along(tip, bottom, seam), Along(tip, bottom, covered) },
        }, new[] { cap }, 0f, Color.white, density);

        pointerArea.anchorMin = Vector2.zero;
        pointerArea.anchorMax = new Vector2(islandRight, 1f);
        pointerArea.offsetMin = pointerArea.offsetMax = Vector2.zero;
        Vector2 local = pointerArea.InverseTransformPoint(Area.TransformPoint(badgeAt));
        ring.rectTransform.anchoredPosition = local;
        ringEcho.rectTransform.anchoredPosition = local;
    }

    private float TailAngle(Vector2 start, float side)
    {
        Vector2 toBadge = badgeAt - start;
        float facing = side > 0f ? 0f : 180f;
        float turn = Mathf.Clamp(Mathf.DeltaAngle(facing, Mathf.Atan2(toBadge.y, toBadge.x) * Mathf.Rad2Deg), -TailMaxTurn, TailMaxTurn);
        return (facing + turn) * Mathf.Deg2Rad;
    }

    private static Vector2 Along(Vector2 from, Vector2 through, float x)
    {
        return from + (through - from) * ((x - from.x) / (through.x - from.x));
    }

    private void Paint(RectTransform target, ref Texture2D texture, Vector2[][] shapes, (Vector2 centre, float radius)[] circles,
        float grow, Color32 colour, float density)
    {
        Vector2 min = shapes[0][0], max = shapes[0][0];
        foreach (var shape in shapes)
        {
            foreach (var point in shape)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        foreach (var circle in circles)
        {
            min = Vector2.Min(min, circle.centre - Vector2.one * circle.radius);
            max = Vector2.Max(max, circle.centre + Vector2.one * circle.radius);
        }
        min -= Vector2.one * (grow + 2f);
        max += Vector2.one * (grow + 2f);
        int width = Mathf.CeilToInt((max.x - min.x) * density), height = Mathf.CeilToInt((max.y - min.y) * density);
        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = target.name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
        }

        var windings = new float[shapes.Length];
        for (int s = 0; s < shapes.Length; s++)
        {
            for (int i = 0; i < shapes[s].Length; i++)
            {
                Vector2 a = shapes[s][i], b = shapes[s][(i + 1) % shapes[s].Length];
                windings[s] += a.x * b.y - b.x * a.y;
            }
        }
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var point = min + new Vector2((x + 0.5f) / density, (y + 0.5f) / density);
                float distance = float.MaxValue;
                for (int s = 0; s < shapes.Length; s++) distance = Mathf.Min(distance, SignedDistance(shapes[s], windings[s], point));
                foreach (var circle in circles) distance = Mathf.Min(distance, (point - circle.centre).magnitude - circle.radius);
                float alpha = Mathf.Clamp01(0.5f - (distance - grow) * density);
                pixels[y * width + x] = new Color32(colour.r, colour.g, colour.b, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);

        var image = target.GetComponent<Image>();
        if (image.sprite != null) Destroy(image.sprite);
        image.sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero, density, 0, SpriteMeshType.FullRect);
        target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = Vector2.zero;
        target.anchoredPosition = min;
        target.sizeDelta = new Vector2(width, height) / density;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static float SignedDistance(Vector2[] shape, float winding, Vector2 point)
    {
        float nearest = float.MaxValue;
        bool inside = true;
        for (int i = 0; i < shape.Length; i++)
        {
            Vector2 a = shape[i], b = shape[(i + 1) % shape.Length];
            Vector2 edge = b - a, offset = point - a;
            float t = Mathf.Clamp01(Vector2.Dot(offset, edge) / edge.sqrMagnitude);
            nearest = Mathf.Min(nearest, (offset - edge * t).magnitude);
            if ((edge.x * offset.y - edge.y * offset.x) * winding < 0f) inside = false;
        }
        return inside ? -nearest : nearest;
    }

    private bool Projection(out Camera view, out Camera uiCamera)
    {
        view = Camera.main;
        uiCamera = null;
        if (view == null) return false;
        Canvas screen = Area.GetComponentInParent<Canvas>().rootCanvas;
        uiCamera = screen.renderMode == RenderMode.ScreenSpaceOverlay ? null : screen.worldCamera;
        return true;
    }

    private static Bounds BuildingBounds(Transform building)
    {
        var bounds = new Bounds(building.position, Vector3.zero);
        bool any = false;
        foreach (var sprite in building.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sprite.enabled || sprite.sprite == null || sprite.sortingLayerName == "Ground") continue;
            if (any) bounds.Encapsulate(sprite.bounds);
            else bounds = sprite.bounds;
            any = true;
        }
        return bounds;
    }

    private static Vector2 ToArea(RectTransform area, Camera view, Camera uiCamera, Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, view.WorldToScreenPoint(world), uiCamera, out Vector2 point);
        return point;
    }

    private static float Cover(Vector2 centre, Vector2 size, Rect avoid)
    {
        float w = Mathf.Min(centre.x + size.x / 2f, avoid.xMax) - Mathf.Max(centre.x - size.x / 2f, avoid.xMin);
        float h = Mathf.Min(centre.y + size.y / 2f, avoid.yMax) - Mathf.Max(centre.y - size.y / 2f, avoid.yMin);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    private static float Span(float value, float start, float length) => Mathf.Clamp01((value - start) / length);

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Bump(float value, float length) => value <= 0f || value >= length ? 0f : Mathf.Sin(value / length * Mathf.PI);

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    [ContextMenu("Ukážka (len v Play mode)")]
    private void Preview()
    {
        if (Application.isPlaying) Show("Beach Bar", 3);
    }

    [ContextMenu("Ukážka pri ďalšej budove (len v Play mode)")]
    private void PreviewAtBuilding()
    {
        if (!Application.isPlaying) return;
        var buildings = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
        if (buildings.Length == 0)
        {
            Show("Beach Bar", 3);
            return;
        }
        Array.Sort(buildings, (x, y) => string.CompareOrdinal(x.name, y.name));
        var building = buildings[previewIndex++ % buildings.Length];
        Show(building.name.Replace("(Clone)", string.Empty), 3, building.transform);
    }
}
