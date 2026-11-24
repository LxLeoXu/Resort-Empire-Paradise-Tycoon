using System;
using System.Reflection;
using UnityEngine;

public class DevDebugManager : MonoBehaviour
{
    [Header("Testovacie panely a overlaye (Inspector)")]
    [Tooltip("Zobrazí presúvateľné okno na ovládanie ukladania (Save ZAP/VYP a Zmazať Save) vpravo dole.")]
    [SerializeField] private bool showPersistenceDebug = true;

    [Tooltip("Zobrazí presúvateľný ladiaci panel pre vlny, TimeScale a živú spokojnosť.")]
    [SerializeField] private bool showWaveTuningDebug = true;

    [Tooltip("Zobrazí tlačidlo „Test uloženia“ (SaveRecoveryTestPanel).")]
    [SerializeField] private bool showSaveRecoveryTest = true;

    [Tooltip("Zobrazí horný overlay „UI RESPONSE TEST (ENGINE)“ (UITouchResponseMeter).")]
    [SerializeField] private bool showUiResponseTest = true;

    // Presúvateľné okná
    private Rect persistenceRect = new Rect(Screen.width - 290, Screen.height - 150, 270, 130);
    private Rect tuningButtonRect = new Rect(25, Screen.height - 85, 180, 65);
    private Rect tuningWindowRect = new Rect(25, 30, 850, 720);
    private Rect liveSatisfactionRect = new Rect(Screen.width / 2f - 160, 20, 320, 95);

    private bool isTuningOpen = false;
    private Vector2 scrollPos;

    // Herné ladiace premenné
    private float serviceDuration = 4f;
    private float noWorkerSpeed = 0.33f;
    private float warningWaitTimeThreshold = 3.0f;
    private float warningDelay = 1.5f;
    private float patienceGrace = 10f;
    private float patienceFloorAfter = 10f;
    private float unitFloor = 0.0f;
    private float waveFloor = 0.40f;
    private float qualityBonusScale = 1.0f;
    private float arrivalDuration = 90f;

    // Rýchlosť, Vlna a Škálovanie
    private float timeScale = 1f;
    private int selectedWaveNumber = 1;
    private float waveScaleMultiplier = 1.0f;

    // Držanie tlačidiel (Hold & Accelerate)
    private string activeHoldKey = null;
    private float holdStartTime = 0f;
    private float nextRepeatTime = 0f;
    private Action currentHoldAction = null;

    private SatisfactionTracker tracker;
    private WaveManager manager;
    private WaveNeedsPanel needsPanel;

    private void Awake()
    {
        SyncExternalOverlays();
    }

    private void OnValidate()
    {
        SyncExternalOverlays();
    }

    private void Start()
    {
        persistenceRect = new Rect(Mathf.Max(15, Screen.width - 290), Mathf.Max(15, Screen.height - 150), 270, 130);
        tuningButtonRect = new Rect(25, Screen.height - 85, 180, 65);
        tuningWindowRect = new Rect(30, 30, Mathf.Min(850, Screen.width * 0.8f), Screen.height * 0.90f);
        liveSatisfactionRect = new Rect((Screen.width - 320) / 2f, 15, 320, 95);

        SyncExternalOverlays();
        ReadInitialValues();
    }

    private void SyncExternalOverlays()
    {
#if UNITY_EDITOR || SAVE_RECOVERY_TEST
        SaveRecoveryTestPanel.SetLauncherVisible(showSaveRecoveryTest);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UITouchResponseMeter.SetOverlayVisible(showUiResponseTest);
#endif
    }

    private void Update()
    {
        HandleButtonHoldAcceleration();
    }

    private void HandleButtonHoldAcceleration()
    {
        if (string.IsNullOrEmpty(activeHoldKey) || currentHoldAction == null) return;

        if (!IsPointerDown())
        {
            activeHoldKey = null;
            currentHoldAction = null;
            return;
        }

        float heldDuration = Time.unscaledTime - holdStartTime;

        if (heldDuration > 0.35f && Time.unscaledTime >= nextRepeatTime)
        {
            float interval = heldDuration > 2.5f ? 0.03f : (heldDuration > 1.2f ? 0.06f : 0.12f);
            nextRepeatTime = Time.unscaledTime + interval;

            currentHoldAction.Invoke();
            ApplyValues();
        }
    }

    private bool IsPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed) return true;
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed) return true;
#endif
        try { if (Input.GetMouseButton(0)) return true; } catch { }
        return false;
    }

    private void ReadInitialValues()
    {
        var service = FindFirstObjectByType<BuildingService>();
        if (service != null)
        {
            serviceDuration = GetPrivateField<float>(service, "serviceDuration", 12f);
            noWorkerSpeed = GetPrivateField<float>(service, "noWorkerSpeed", 0.33f);
        }

        tracker = FindFirstObjectByType<SatisfactionTracker>();
        if (tracker != null)
        {
            patienceGrace = tracker.patienceGrace;
            patienceFloorAfter = tracker.patienceFloorAfter;
            unitFloor = tracker.unitFloor;
            waveFloor = tracker.waveFloor;
            qualityBonusScale = tracker.qualityBonusScale;
        }

        manager = FindFirstObjectByType<WaveManager>();
        if (manager != null)
        {
            arrivalDuration = GetPrivateField<float>(manager, "arrivalDuration", 90f);
            waveScaleMultiplier = manager.waveScaleMultiplier;

            if (manager.Session != null)
            {
                selectedWaveNumber = Mathf.Clamp(manager.Session.WaveNumber + 1, 1, 8);
            }
        }

        needsPanel = FindFirstObjectByType<WaveNeedsPanel>();
        if (needsPanel != null)
        {
            warningWaitTimeThreshold = needsPanel.warningWaitTimeThreshold;
            warningDelay = needsPanel.warningDelay;
        }
    }

    private void ApplyValues()
    {
        var services = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
        foreach (var s in services)
        {
            SetPrivateField(s, "serviceDuration", serviceDuration);
            SetPrivateField(s, "noWorkerSpeed", noWorkerSpeed);
        }

        if (tracker == null) tracker = FindFirstObjectByType<SatisfactionTracker>();
        if (tracker != null)
        {
            tracker.patienceGrace = patienceGrace;
            tracker.patienceFloorAfter = patienceFloorAfter;
            tracker.unitFloor = unitFloor;
            tracker.waveFloor = waveFloor;
            tracker.qualityBonusScale = qualityBonusScale;
        }

        if (manager == null) manager = FindFirstObjectByType<WaveManager>();
        if (manager != null)
        {
            SetPrivateField(manager, "arrivalDuration", arrivalDuration);
            manager.waveScaleMultiplier = waveScaleMultiplier;
        }

        if (needsPanel == null) needsPanel = FindFirstObjectByType<WaveNeedsPanel>();
        if (needsPanel != null)
        {
            needsPanel.warningWaitTimeThreshold = warningWaitTimeThreshold;
            needsPanel.warningDelay = warningDelay;
        }

        Time.timeScale = timeScale;
    }

    private void ApplyWaveSelection()
    {
        if (manager == null) manager = FindFirstObjectByType<WaveManager>();
        if (manager == null || manager.Session == null) return;

        SetPrivateField(manager, "useTestWave", true);
        SetPrivateField(manager, "testWaveNumber", selectedWaveNumber);
        manager.waveScaleMultiplier = waveScaleMultiplier;

        if (manager.Session.State != WaveState.Running)
        {
            manager.Session.SetWaveIndex(selectedWaveNumber - 1);
            Debug.Log($"[TUNING] Nastavená vlna {selectedWaveNumber} (Škála: {waveScaleMultiplier:0.00}×)");
        }
    }

    private void OnGUI()
    {
        GUI.skin.button.fontSize = 22;
        GUI.skin.label.fontSize = 22;
        GUI.skin.window.fontSize = 22;

        if (showPersistenceDebug)
        {
            persistenceRect = GUI.Window(901, persistenceRect, DrawPersistenceWindow, "Save System (Drag)");
        }

        if (showWaveTuningDebug)
        {
            if (manager == null) manager = FindFirstObjectByType<WaveManager>();
            if (manager != null && manager.Session != null && manager.Session.State == WaveState.Running)
            {
                liveSatisfactionRect = GUI.Window(902, liveSatisfactionRect, DrawLiveSatisfactionWindow, "Live Satisfaction");
            }

            GUI.color = isTuningOpen ? Color.yellow : new Color(0.2f, 0.8f, 1f, 0.9f);
            if (GUI.Button(tuningButtonRect, isTuningOpen ? "CLOSE" : "TUNING"))
            {
                isTuningOpen = !isTuningOpen;
                // Už sa neresetuje, číta aktuálne uložené hodnoty
                if (isTuningOpen) ReadInitialValues();
            }
            GUI.color = Color.white;

            if (isTuningOpen)
            {
                tuningWindowRect = GUI.Window(903, tuningWindowRect, DrawTuningWindow, "Tuning (Drag Bar)");
            }
        }
    }

    private void DrawPersistenceWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, 1000, 35));

        bool isSavingEnabled = !GamePersistence.DisableSaving;

        GUI.color = isSavingEnabled ? Color.green : Color.red;
        if (GUI.Button(new Rect(10, 35, persistenceRect.width - 20, 38), isSavingEnabled ? "Save: ZAPNUTÉ" : "Save: VYPNUTÉ"))
        {
            GamePersistence.DisableSaving = isSavingEnabled;
            Debug.Log($"Ukladanie je teraz: {(GamePersistence.DisableSaving ? "VYPNUTÉ" : "ZAPNUTÉ")}");
        }

        GUI.color = Color.yellow;
        if (GUI.Button(new Rect(10, 80, persistenceRect.width - 20, 38), "Zmazať Save"))
        {
            GamePersistence.DeleteSaveFiles();
        }
        GUI.color = Color.white;
    }

    private void DrawLiveSatisfactionWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, 1000, 35));

        if (tracker == null) tracker = FindFirstObjectByType<SatisfactionTracker>();
        if (tracker == null) return;

        float liveSat = tracker.LiveSatisfactionPercent;
        int served = tracker.LiveServedCount;

        string stars = liveSat >= 90f ? "5★" : liveSat >= 80f ? "4★" : liveSat >= 70f ? "3★" : liveSat >= 55f ? "2★" : "1★";
        GUI.color = liveSat >= 80f ? Color.green : liveSat >= 65f ? Color.yellow : Color.red;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.Label(new Rect(10, 30, liveSatisfactionRect.width - 20, 30), $"{liveSat:0.0}% ({stars})", style);

        GUI.color = Color.white;
        style.fontSize = 17;
        GUI.Label(new Rect(10, 62, liveSatisfactionRect.width - 20, 25), $"Served Needs: {served}", style);
    }

    private void DrawTuningWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, 1000, 40));

        GUILayout.BeginArea(new Rect(15, 45, tuningWindowRect.width - 30, tuningWindowRect.height - 55));
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        // 1. RÝCHLOSŤ HRY
        GUILayout.Label($"<b>Game Speed (TimeScale): {timeScale:0.0}×</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("- 0.5×", GUILayout.Height(45))) { timeScale = Mathf.Max(0.1f, timeScale - 0.5f); ApplyValues(); }
        if (GUILayout.Button("+ 0.5×", GUILayout.Height(45))) { timeScale += 0.5f; ApplyValues(); }
        if (GUILayout.Button("+ 2.0×", GUILayout.Height(45))) { timeScale += 2.0f; ApplyValues(); }
        if (GUILayout.Button("+ 5.0×", GUILayout.Height(45))) { timeScale += 5.0f; ApplyValues(); }
        if (GUILayout.Button("Reset", GUILayout.Height(45))) { timeScale = 1.0f; ApplyValues(); }
        GUILayout.EndHorizontal();
        GUILayout.Space(15);

        // 2. VÝBER VLNY
        GUILayout.Label($"<b>Target Wave: Wave {selectedWaveNumber} / 8</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("- Wave", GUILayout.Height(45)))
        {
            selectedWaveNumber = Mathf.Clamp(selectedWaveNumber - 1, 1, 8);
            ApplyWaveSelection();
        }
        if (GUILayout.Button("+ Wave", GUILayout.Height(45)))
        {
            selectedWaveNumber = Mathf.Clamp(selectedWaveNumber + 1, 1, 8);
            ApplyWaveSelection();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        // 3. ŠKÁLOVANIE VLNY
        DrawHoldRow("Wave Scale", "scaleMult", () => waveScaleMultiplier, v => waveScaleMultiplier = v, 0.05f, 0.10f);
        GUILayout.Space(15);

        // 4. WORKER MANAGEMENT
        DrawWorkersSection();
        GUILayout.Space(15);

        // 5. PARAMETRE BUDOV A BOTTLENECKU
        DrawHoldRow("Service Time with Worker (s)", "servTime", () => serviceDuration, v => serviceDuration = v, 0.1f, 0.1f);
        DrawHoldRow("Empty Building Speed", "noWorker", () => noWorkerSpeed, v => noWorkerSpeed = v, 0.01f, 0.01f);
        GUILayout.Space(12);

        // BOTTLENECK: Čas spustenia (5s) a Delay (2s)
        DrawHoldRow("Warning Trigger Wait Time (s)", "warnWait", () => warningWaitTimeThreshold, v => warningWaitTimeThreshold = v, 0.5f, 1.0f);
        DrawHoldRow("Warning Delay Filter (s)", "warnDelay", () => warningDelay, v => warningDelay = v, 0.5f, 0.0f);
        GUILayout.Space(12);

        // TRPEZLIVOSŤ A SPOKOJNOSŤ
        DrawHoldRow("Full Patience Grace Period (s)", "patGrace", () => patienceGrace, v => patienceGrace = v, 0.5f, 1.0f);
        DrawHoldRow("Drop to Min Patience After (s)", "patFloor", () => patienceFloorAfter, v => patienceFloorAfter = v, 0.5f, 1f);
        DrawHoldRow("Min Guest Floor", "unitFloor", () => unitFloor, v => unitFloor = v, 0.01f, 0f);
        DrawHoldRow("Min Wave Floor", "waveFloor", () => waveFloor, v => waveFloor = v, 0.01f, 0f);
        DrawHoldRow("Quality Bonus Scale (Lv4+)", "qualScale", () => qualityBonusScale, v => qualityBonusScale = v, 0.10f, 0f);
        GUILayout.Space(12);

        DrawHoldRow("Wave Arrival Duration (s)", "arrDur", () => arrivalDuration, v => arrivalDuration = v, 1.0f, 1f);

        GUILayout.Space(20);
        GUI.color = Color.cyan;
        if (GUILayout.Button("RESET ALL TO BALANCED DEFAULTS", GUILayout.Height(50)))
        {
            serviceDuration = 4f;
            noWorkerSpeed = 0.33f;
            warningWaitTimeThreshold = 3.0f;
            warningDelay = 1.5f;
            patienceGrace = 10f;
            patienceFloorAfter = 10f;
            unitFloor = 0.0f;
            waveFloor = 0.40f;
            qualityBonusScale = 1.0f;
            arrivalDuration = 90f;
            timeScale = 1f;
            waveScaleMultiplier = 1.0f;
            ApplyValues();
        }
        GUI.color = Color.white;

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawWorkersSection()
    {
        GUILayout.Label("<b>Worker Management:</b>");
        var services = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);

        if (services == null || services.Length == 0)
        {
            GUILayout.Label("<i>No active buildings found on island.</i>");
            return;
        }

        foreach (var s in services)
        {
            var info = s.GetComponentInChildren<BuildingInfo>(true);
            if (info != null && !info.isBuilt) continue;
            if (s.MaxWorkers <= 0) continue;

            string bName = info != null && !string.IsNullOrEmpty(info.buildingName) ? info.buildingName : s.Need.ToString();
            int lvl = info != null ? info.currentLevel : 1;
            string color = s.HasWorker ? "green" : "red";

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{bName} (Lv.{lvl}): <color={color}>{s.Workers}/{s.MaxWorkers}</color>", GUILayout.Width(360));

            GUI.enabled = s.Workers > 0;
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("-", GUILayout.Width(62), GUILayout.Height(42)))
            {
                s.SetWorkers(s.Workers - 1);
            }

            GUI.enabled = s.Workers < s.MaxWorkers;
            GUI.color = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("+", GUILayout.Width(62), GUILayout.Height(42)))
            {
                s.SetWorkers(s.Workers + 1);
            }

            GUI.enabled = true;
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
    }

    private void DrawHoldRow(string label, string key, Func<float> getVal, Action<float> setVal, float step, float min)
    {
        float val = getVal();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: <b>{val:0.00}</b>", GUILayout.Width(420));

        if (GUILayout.RepeatButton("-", GUILayout.Width(75), GUILayout.Height(45)))
        {
            if (activeHoldKey != key + "_minus")
            {
                activeHoldKey = key + "_minus";
                holdStartTime = Time.unscaledTime;
                nextRepeatTime = Time.unscaledTime + 0.35f;

                setVal(Mathf.Max(min, val - step));
                ApplyValues();

                currentHoldAction = () => setVal(Mathf.Max(min, getVal() - step));
            }
        }

        if (GUILayout.RepeatButton("+", GUILayout.Width(75), GUILayout.Height(45)))
        {
            if (activeHoldKey != key + "_plus")
            {
                activeHoldKey = key + "_plus";
                holdStartTime = Time.unscaledTime;
                nextRepeatTime = Time.unscaledTime + 0.35f;

                setVal(val + step);
                ApplyValues();

                currentHoldAction = () => setVal(getVal() + step);
            }
        }

        GUILayout.EndHorizontal();
    }

    private T GetPrivateField<T>(object target, string fieldName, T defaultValue)
    {
        if (target == null) return defaultValue;
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) return (T)field.GetValue(target);
        return defaultValue;
    }

    private void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) field.SetValue(target, value);
    }
}