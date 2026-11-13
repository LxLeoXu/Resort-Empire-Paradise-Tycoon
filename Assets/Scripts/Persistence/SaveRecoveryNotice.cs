using System.Collections;
using UnityEngine;

/// <summary>Make recovery visible to the player, including in ordinary builds.</summary>
public sealed class SaveRecoveryNotice : MonoBehaviour
{
    public static void Show(string message)
    {
        var root = new GameObject("SaveRecoveryNotice", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        root.AddComponent<SaveRecoveryNotice>();
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Message", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(root.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(.05f, .72f); rect.anchorMax = new Vector2(.95f, .92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var background = panel.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(.18f, .12f, .025f, .97f); background.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
        label.transform.SetParent(panel.transform, false);
        var textRect = (RectTransform)label.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24, 12); textRect.offsetMax = new Vector2(-24, -12);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 32; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
        label.text = message; label.raycastTarget = false;
    }
    private IEnumerator Start() { yield return new WaitForSecondsRealtime(12); Destroy(gameObject); }
}
