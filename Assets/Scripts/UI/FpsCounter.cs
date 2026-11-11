using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class FpsCounter : MonoBehaviour
{
    [Header("UI Text (voliteľné)")]
    [SerializeField] private TextMeshProUGUI fpsText;

    [Header("Pozícia jednoduchého textu (ak nepoužiješ UI Text)")]
    [SerializeField] private Vector2 screenPosition = new Vector2(60, 60);
    [SerializeField] private int fontSize = 38;

    [Header("Nastavenia")]
    [Tooltip("Ak zaškrtneš, FPS naskočí hneď pri štarte hry (ideálne pre testovacie APK)")]
    [SerializeField] private bool showOnStart = false; 
    [SerializeField] private float updateInterval = 0.5f;

    private float accumTime = 0f;
    private int frames = 0;
    private float currentFps = 0f;
    private bool isVisible = false;

    // Kliknutia
    private int tapCount = 0;
    private float lastTapTime = 0f;

    private void Awake()
    {
        // Vynútiť aspoň 60 FPS target na Androide
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        SetVisible(showOnStart);
    }

    private void Update()
    {
        CheckInput();

        if (!isVisible) return;

        accumTime += Time.unscaledDeltaTime;
        frames++;

        if (accumTime >= updateInterval)
        {
            currentFps = frames / accumTime;
            accumTime = 0.0f;
            frames = 0;

            if (fpsText != null)
            {
                fpsText.text = $"{currentFps:0.} FPS";
                fpsText.color = currentFps < 30f ? Color.red : Color.green;
            }
        }
    }

    private void CheckInput()
    {
        // 1. Klávesa F na PC
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleVisibility();
            return;
        }

        // 2. MOBILNÁ SKRATKA: Dotyk tromi prstami naraz kdekoľvek na obrazovku
        if (Touchscreen.current != null)
        {
            var touches = Touchscreen.current.touches;
            int activeTouches = 0;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].isInProgress) activeTouches++;
            }

            if (activeTouches >= 3)
            {
                if (Time.unscaledTime - lastTapTime > 0.5f) // ochrana pred viacnásobným prepnutím
                {
                    ToggleVisibility();
                    lastTapTime = Time.unscaledTime;
                    return;
                }
            }
        }

        // 3. 3x rýchly klik / ťuknutie do ľavého horného rohu (myš aj prst)
        bool clicked = false;
        Vector2 clickPos = Vector2.zero;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clicked = true;
            clickPos = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            clicked = true;
            clickPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (clicked)
        {
            // Zväčšená zóna: ľavá štvrtina šírky (25%) a horná štvrtina výšky (25%)
            if (clickPos.x < Screen.width * 0.25f && clickPos.y > Screen.height * 0.75f)
            {
                if (Time.unscaledTime - lastTapTime < 0.6f)
                {
                    tapCount++;
                    if (tapCount >= 3)
                    {
                        ToggleVisibility();
                        tapCount = 0;
                    }
                }
                else
                {
                    tapCount = 1;
                }

                lastTapTime = Time.unscaledTime;
            }
        }
    }

    public void ToggleVisibility()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool state)
    {
        isVisible = state;
        if (fpsText != null)
        {
            fpsText.enabled = isVisible;
        }
    }

    private void OnGUI()
    {
        if (!isVisible || fpsText != null) return;

        GUI.color = currentFps < 30f ? Color.red : Color.green;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        GUI.Label(new Rect(screenPosition.x, screenPosition.y, 350, 100), $"{currentFps:0.} FPS", style);
    }
}