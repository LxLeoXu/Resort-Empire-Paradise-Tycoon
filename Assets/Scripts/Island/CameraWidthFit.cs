using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The island camera always shows at least the width a 20:9 phone shows, so the buildings at the sides (the floating
/// Spa on the left, the bungalow on the right) stay in view on every screen. On narrower screens (18:9, 16:9,
/// tablets) the orthographic size grows until that width fits, and more sea shows above and below; on 20:9 and
/// wider screens the scene's own size stays. It adds itself to the main camera whenever a scene loads (the scene
/// file stays as it is) and refits only when the screen size changes.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraWidthFit : MonoBehaviour
{
    public const float TargetAspect = 20f / 9f;

    Camera cam;
    float baseSize;
    int lastWidth, lastHeight;

    /// <summary>The camera's size as set in the scene (the framing for a 20:9 screen).</summary>
    public float BaseSize => baseSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var main = Camera.main;
        if (main != null && main.orthographic && main.GetComponent<CameraWidthFit>() == null)
            main.gameObject.AddComponent<CameraWidthFit>();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseSize = cam.orthographicSize;
    }

    /// <summary>The size that shows the 20:9 width on a screen of this aspect (width / height).</summary>
    public float SizeFor(float aspect)
    {
        return baseSize * Mathf.Max(1f, TargetAspect / Mathf.Max(0.01f, aspect));
    }

    void LateUpdate()
    {
        if (Screen.width == lastWidth && Screen.height == lastHeight)
            return;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        cam.orthographicSize = SizeFor((float)lastWidth / Mathf.Max(1, lastHeight));
    }
}
