#if UNITY_EDITOR || SAVE_RECOVERY_TEST
using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class RecoveryTestExpectation { public int wave, coins; }

/// <summary>Included only in Editor and builds explicitly compiled with SAVE_RECOVERY_TEST.</summary>
public sealed class SaveRecoveryTestPanel : MonoBehaviour
{
    private GameObject window;
    private UnityEngine.UI.Text status;
    private UnityEngine.UI.Button damageButton, closeButton;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (FindFirstObjectByType<SaveRecoveryTestPanel>() == null)
            new GameObject("SaveRecoveryTestPanel").AddComponent<SaveRecoveryTestPanel>();
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(transform, false);
        Rect area = Screen.safeArea;
        safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        var launcher = Button(safe, "Test uloženia", new Vector2(0, 0), new Vector2(175, 100), new Vector2(310, 90));
        launcher.onClick.AddListener(Open);
        window = new GameObject("RecoveryWindow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        window.transform.SetParent(safe, false);
        var rect = (RectTransform)window.transform;
        rect.anchorMin = new Vector2(.04f, .14f); rect.anchorMax = new Vector2(.96f, .86f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        window.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .065f, .1f, .99f);
        Label("SCRUM-68 / SCRUM-484", new Vector2(.04f, .85f), new Vector2(.96f, .98f), 42);
        status = Label("", new Vector2(.06f, .33f), new Vector2(.94f, .85f), 31);
        damageButton = Button(rect, "Poškodiť hlavné uloženie", new Vector2(.5f, .23f), Vector2.zero, new Vector2(820, 100));
        damageButton.onClick.AddListener(Damage);
        closeButton = Button(rect, "Zavrieť panel", new Vector2(.5f, .075f), Vector2.zero, new Vector2(600, 90));
        closeButton.onClick.AddListener(() => window.SetActive(false));
        window.SetActive(false);
    }

    public void Damage()
    {
        var persistence = GamePersistence.Instance;
        if (persistence == null || !persistence.IsReady) { Refresh(); return; }
        bool success = persistence.CorruptPrimaryForRecoveryTest();
        window.SetActive(true);
        Refresh();
        if (!success) status.text = "Test sa nepodarilo pripraviť:\n" + persistence.LastError + "\n\n" + status.text;
    }

    public void Open() { window.SetActive(true); Refresh(); }

    public void Refresh()
    {
        var persistence = GamePersistence.Instance;
        bool ready = persistence != null && persistence.IsReady;
        bool armed = persistence != null && persistence.RecoveryTestArmed;
        damageButton.interactable = ready && !armed;
        closeButton.interactable = !armed;
        if (!ready) { status.text = "Čakám na načítanie hry. Zatvor a znova otvor panel."; return; }
        if (armed)
        {
            status.text = "Hlavné uloženie je poškodené. Záloha zostala zachovaná.\n\nHra je pozastavená a ďalšie ukladanie je vypnuté.\n\n1. Stlač Domov.\n2. Odstráň hru zo zoznamu posledných aplikácií.\n3. Otvor hru a panel Test uloženia znova.\n\nOčakávaný výsledok: Načítaná záloha.";
            return;
        }
        string source = persistence.LoadedBackup ? "NAČÍTANÁ ZÁLOHA" : "Hra je pripravená na test.";
        string expected = "";
        try
        {
            string path = persistence.SavePath + ".test-expected";
            if (File.Exists(path))
            {
                var saved = JsonUtility.FromJson<RecoveryTestExpectation>(File.ReadAllText(path));
                var current = persistence.CaptureState();
                bool matches = current.wave.number == saved.wave && ResourceManager.Instance.GetResource(ResourceType.Coins) == saved.coins;
                expected = $"\nOčakávaná vlna: {saved.wave}, mince: {saved.coins}.\n" +
                    (persistence.LoadedBackup && matches ? "KONTROLA VLNY A MINCÍ PREŠLA.\n" : "");
            }
        }
        catch (Exception error) { expected = "\nKontrola údajov: " + error.Message + "\n"; }
        var state = persistence.CaptureState();
        status.text = source + $"\nAktuálna vlna: {state.wave.number}, mince: {ResourceManager.Instance.GetResource(ResourceType.Coins)}.\n" + expected +
            "\nTlačidlo vytvorí platnú zálohu a zámerne poškodí hlavný súbor. Potom hru zavri a znova otvor.\n\nIba pre testovanie SCRUM-484.";
    }

    private UnityEngine.UI.Text Label(string text, Vector2 min, Vector2 max, int size)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
        label.transform.SetParent(window.transform, false);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.font = font; label.fontSize = size; label.color = Color.white;
        label.text = text; label.alignment = TextAnchor.UpperLeft; label.raycastTarget = false;
        return label;
    }

    private UnityEngine.UI.Button Button(Transform parent, string caption, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var button = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        button.transform.SetParent(parent, false);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        button.GetComponent<UnityEngine.UI.Image>().color = new Color(.12f, .28f, .38f);
        var text = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
        text.transform.SetParent(button.transform, false);
        var tr = (RectTransform)text.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        text.font = font; text.fontSize = 32; text.text = caption; text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white; text.raycastTarget = false;
        return button.GetComponent<UnityEngine.UI.Button>();
    }
}
#endif
