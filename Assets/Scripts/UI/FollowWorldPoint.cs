using UnityEngine;

/// <summary>
/// Keeps a screen-space UI element (e.g. the "+" of a build spot) over a point in the world, so it stays on its
/// building at any resolution while keeping its constant pixel size. Put it on the element's RectTransform in a
/// Screen Space - Overlay canvas.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class FollowWorldPoint : MonoBehaviour
{
    [Tooltip("Objekt vo svete, nad ktorým má prvok byť (napr. stavenisko).")]
    public Transform target;
    [Tooltip("Posun od cieľa vo svete (napr. nad strechu budovy).")]
    public Vector3 worldOffset;

    private RectTransform rect;

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (target == null || cam == null) return;
        if (rect == null) rect = (RectTransform)transform;
        Vector3 screen = cam.WorldToScreenPoint(target.position + worldOffset);
        screen.z = 0f;
        if ((rect.position - screen).sqrMagnitude > 0.25f)   // (no rewrite for sub-pixel changes)
            rect.position = screen;
    }
}
