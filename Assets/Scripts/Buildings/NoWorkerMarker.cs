using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingService))]
public sealed class NoWorkerMarker : MonoBehaviour
{
    [Tooltip("Objekt značky „bez obsluhy“ (Status_NoWorker). Je zapnutý, len kým postavená budova nemá pracovníka.")]
    [SerializeField] private Transform marker;

    [Header("Animácia")]
    [Tooltip("Ako dlho značka po zobrazení dorastá do plnej veľkosti (sekundy).")]
    [Min(0f)] [SerializeField] private float popDuration = 0.25f;
    [Tooltip("Veľkosť značky v okamihu zobrazenia voči plnej veľkosti.")]
    [Range(0.1f, 1f)] [SerializeField] private float popStartScale = 0.6f;
    [Tooltip("O koľko sa značka vznáša hore a dole (svetové jednotky).")]
    [Min(0f)] [SerializeField] private float bobHeight = 0.025f;
    [Tooltip("Koľko sekúnd trvá jedno zhupnutie.")]
    [Min(0.1f)] [SerializeField] private float bobPeriod = 2.4f;

    private BuildingService service;
    private BuildingInfo info;
    private Vector3 restPosition;
    private Vector3 restScale;
    private float shownAt;
    private float bobPhase;

    public bool IsShown => marker != null && marker.gameObject.activeSelf;

    private void Awake()
    {
        service = GetComponent<BuildingService>();
        info = GetComponent<BuildingInfo>();
        if (marker == null)
        {
            Debug.LogError("NoWorkerMarker: chýba objekt značky.", this);
            enabled = false;
            return;
        }
        restPosition = marker.localPosition;
        restScale = marker.localScale;
        bobPhase = transform.position.x * 0.37f;
    }

    private void OnEnable()
    {
        service.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        service.Changed -= Refresh;
    }

    private void Update()
    {
        Refresh();
        if (IsShown) Animate();
    }

    private void Refresh()
    {
        bool show = !service.HasWorker && (info == null || info.isBuilt);
        if (marker.gameObject.activeSelf == show) return;

        marker.gameObject.SetActive(show);
        if (!show) return;

        shownAt = Time.unscaledTime;
        Animate();
    }

    private void Animate()
    {
        float pop = popDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - shownAt) / popDuration) : 1f;
        marker.localScale = restScale * Mathf.LerpUnclamped(popStartScale, 1f, EaseOutBack(pop));

        float lift = Mathf.Sin((Time.time / bobPeriod + bobPhase) * 2f * Mathf.PI) * bobHeight;
        marker.localPosition = restPosition + Vector3.up * (lift / transform.lossyScale.y);
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }
}
