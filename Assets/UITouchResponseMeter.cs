// Resort Empire Paradise Tycoon
// UI Touch Response Meter
// Visible overlay for Unity Editor and Android Development Builds.

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UITouchResponseMeter : MonoBehaviour, IPointerDownHandler
{
    public static bool OverlayVisible { get; private set; } = true;

    public static void SetOverlayVisible(bool visible)
    {
        OverlayVisible = visible;
    }

    private static UITouchResponseMeter host;

    private const double LimitMs = 100.0;
    private const double TimeoutMs = 400.0;
    private const string CsvFile = "UI_Touch_Response.csv";

    private Button trackedButton;
    private Color previousColor;

    private bool hasPreviousColor;
    private bool measuring;

    // On-screen overlay
    private bool showDetails = true;

    private string lastResult = "READY";
    private string lastButton = "Waiting for button press...";
    private string lastChange = "-";

    private double lastMilliseconds;
    private int lastFrames;

    private int passed;
    private int slow;
    private int noChange;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (host != null)
            return;

        GameObject root = new GameObject(
            "TEMP UI Response Meter");

        DontDestroyOnLoad(root);

        host = root.AddComponent<UITouchResponseMeter>();
    }

    private void Awake()
    {
        trackedButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        CaptureColor();
    }

    private IEnumerator Start()
    {
        // Only the persistent host scans the scene.
        if (trackedButton != null)
            yield break;

        Debug.Log(
            "[UI_RESPONSE] Started. CSV: " + GetCsvPath());

        while (true)
        {
            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                if (button.GetComponent<UITouchResponseMeter>() != null)
                    continue;

                // Attach listener without modifying OnClick().
                button.gameObject.AddComponent<UITouchResponseMeter>();
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    private void LateUpdate()
    {
        if (!measuring)
            CaptureColor();
    }

    private void CaptureColor()
    {
        if (trackedButton == null ||
            trackedButton.targetGraphic == null)
            return;

        previousColor =
            trackedButton.targetGraphic.canvasRenderer.GetColor();

        hasPreviousColor = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (trackedButton == null ||
            !trackedButton.IsActive() ||
            !trackedButton.IsInteractable() ||
            trackedButton.targetGraphic == null ||
            !hasPreviousColor ||
            measuring ||
            host == null)
        {
            return;
        }

        measuring = true;

        double startTime =
            Time.realtimeSinceStartupAsDouble;

        int startFrame = Time.frameCount;

        string buttonPath = GetPath(transform);

        host.lastResult = "MEASURING";
        host.lastButton = trackedButton.name;
        host.lastChange = "Waiting for visual change...";

        host.StartCoroutine(
            host.MeasureResponse(
                this,
                trackedButton,
                previousColor,
                startTime,
                startFrame,
                buttonPath));
    }

    private IEnumerator MeasureResponse(
        UITouchResponseMeter listener,
        Button button,
        Color before,
        double startTime,
        int startFrame,
        string buttonPath)
    {
        WaitForEndOfFrame endOfFrame =
            new WaitForEndOfFrame();

        while (true)
        {
            yield return endOfFrame;

            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startTime)
                * 1000.0;

            int frames =
                Time.frameCount - startFrame + 1;

            if (button == null ||
                !button.gameObject.activeInHierarchy)
            {
                Record(
                    buttonPath,
                    elapsedMs,
                    frames,
                    "UI hidden",
                    true);

                break;
            }

            Graphic graphic = button.targetGraphic;

            if (graphic != null)
            {
                Color after =
                    graphic.canvasRenderer.GetColor();

                float difference =
                    Mathf.Abs(after.r - before.r) +
                    Mathf.Abs(after.g - before.g) +
                    Mathf.Abs(after.b - before.b) +
                    Mathf.Abs(after.a - before.a);

                if (difference >= 0.025f)
                {
                    Record(
                        buttonPath,
                        elapsedMs,
                        frames,
                        "Color Tint changed",
                        true);

                    break;
                }
            }

            if (elapsedMs >= TimeoutMs)
            {
                Record(
                    buttonPath,
                    elapsedMs,
                    frames,
                    "No detectable change",
                    false);

                break;
            }
        }

        if (listener != null)
        {
            listener.measuring = false;
            listener.CaptureColor();
        }
    }

    private void Record(
        string buttonPath,
        double milliseconds,
        int frames,
        string change,
        bool detected)
    {
        string result;

        if (!detected)
        {
            result = "NO CHANGE";
            noChange++;
        }
        else if (milliseconds <= LimitMs)
        {
            result = "PASS";
            passed++;
        }
        else
        {
            result = "SLOW";
            slow++;
        }

        // Update the visible overlay.
        lastResult = result;
        lastMilliseconds = milliseconds;
        lastFrames = frames;
        lastChange = change;

        int separator = buttonPath.LastIndexOf('/');

        lastButton = separator >= 0
            ? buttonPath.Substring(separator + 1)
            : buttonPath;

        string message =
            "[UI_RESPONSE] " +
            result + " | " +
            milliseconds.ToString(
                "F2", CultureInfo.InvariantCulture) +
            " ms | " +
            frames + " frames | " +
            buttonPath + " | " +
            change;

        Debug.Log(message);

        // CSV is written AFTER measurement.
        try
        {
            string path = GetCsvPath();

            if (!File.Exists(path))
            {
                File.WriteAllText(
                    path,
                    "utc_time,button,ms,frames,change,result"
                    + Environment.NewLine);
            }

            string safeButton =
                buttonPath.Replace("\"", "\"\"");

            string row =
                DateTime.UtcNow.ToString(
                    "O", CultureInfo.InvariantCulture) +
                ",\"" + safeButton + "\"," +
                milliseconds.ToString(
                    "F2", CultureInfo.InvariantCulture) +
                "," + frames +
                ",\"" + change + "\"," +
                result;

            File.AppendAllText(
                path,
                row + Environment.NewLine);
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "[UI_RESPONSE] CSV error: " +
                error.Message);
        }
    }

    // =====================================================
    // ON-SCREEN TEST PANEL
    // =====================================================

    private void OnGUI()
    {
        // Only the persistent host displays the overlay.
        if (this != host || !OverlayVisible)
            return;

        float scale = Mathf.Max(
            0.85f,
            Mathf.Min(
                Screen.width / 1080f,
                Screen.height / 720f));

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.matrix = Matrix4x4.Scale(
            new Vector3(scale, scale, 1f));

        float virtualWidth = Screen.width / scale;

        float panelWidth = Mathf.Min(
            500f,
            virtualWidth - 20f);

        float x = virtualWidth - panelWidth - 10f;
        float y = 12f;

        if (!showDetails)
        {
            if (GUI.Button(
                new Rect(
                    virtualWidth - 130f,
                    y,
                    120f,
                    36f),
                "UI TEST"))
            {
                showDetails = true;
            }

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            return;
        }

        Rect panel = new Rect(
            x,
            y,
            panelWidth,
            158f);

        GUI.color = new Color(
            0f, 0f, 0f, 0.86f);

        GUI.DrawTexture(
            panel,
            Texture2D.whiteTexture);

        GUI.color = Color.white;

        GUIStyle titleStyle =
            new GUIStyle(GUI.skin.label);

        titleStyle.fontSize = 17;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        GUIStyle resultStyle =
            new GUIStyle(GUI.skin.label);

        resultStyle.fontSize = 21;
        resultStyle.fontStyle = FontStyle.Bold;

        if (lastResult == "PASS")
        {
            resultStyle.normal.textColor =
                new Color(0.4f, 1f, 0.4f);
        }
        else if (lastResult == "SLOW" ||
                 lastResult == "NO CHANGE")
        {
            resultStyle.normal.textColor =
                new Color(1f, 0.45f, 0.45f);
        }
        else
        {
            resultStyle.normal.textColor =
                Color.yellow;
        }

        GUIStyle textStyle =
            new GUIStyle(GUI.skin.label);

        textStyle.fontSize = 15;
        textStyle.normal.textColor = Color.white;

        float left = x + 12f;

        GUI.Label(
            new Rect(left, y + 7f, panelWidth - 110f, 25f),
            "UI RESPONSE TEST (ENGINE)",
            titleStyle);

        if (GUI.Button(
            new Rect(
                x + panelWidth - 82f,
                y + 7f,
                72f,
                28f),
            "HIDE"))
        {
            showDetails = false;
        }

        string resultText =
            lastResult == "READY" ||
            lastResult == "MEASURING"
                ? lastResult
                : lastResult + "  " +
                  lastMilliseconds.ToString("F1") +
                  " ms  |  " +
                  lastFrames + " frames";

        GUI.Label(
            new Rect(left, y + 36f, panelWidth - 20f, 30f),
            resultText,
            resultStyle);

        GUI.Label(
            new Rect(left, y + 69f, panelWidth - 20f, 23f),
            "Button: " + lastButton,
            textStyle);

        GUI.Label(
            new Rect(left, y + 92f, panelWidth - 20f, 23f),
            "Change: " + lastChange,
            textStyle);

        GUI.Label(
            new Rect(left, y + 119f, panelWidth - 20f, 26f),
            "PASS: " + passed +
            "    SLOW: " + slow +
            "    NO CHANGE: " + noChange,
            textStyle);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private static string GetCsvPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            CsvFile);
    }

    private static string GetPath(Transform current)
    {
        string path = current.name;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}

#endif