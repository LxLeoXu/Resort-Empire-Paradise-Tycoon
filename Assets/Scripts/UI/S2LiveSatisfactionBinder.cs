using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Synchronizes S2 satisfaction UI with the existing SatisfactionTracker.
/// Does not calculate or modify gameplay satisfaction.
/// </summary>
public sealed class S2LiveSatisfactionBinder : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField]
    private SatisfactionTracker satisfactionTracker;

    [Header("Text")]
    [SerializeField]
    private TMP_Text satisfactionPercentText;

    [SerializeField]
    private TMP_Text ratingText;

    [Header("Progress Bar")]
    [SerializeField]
    private Image satisfactionFill;

    [SerializeField]
    private Slider satisfactionSlider;

    [Header("Star Images - Left to Right")]
    [SerializeField]
    private Image[] starImages = new Image[5];

    [Header("Star Colors")]
    [SerializeField]
    private Color activeStarColor =
        new Color(1f, 0.75f, 0.10f, 1f);

    [SerializeField]
    private Color inactiveStarColor =
        new Color(0.55f, 0.55f, 0.55f, 1f);

    private float lastPercent = -1f;

    private void Awake()
    {
        FindTracker();
    }

    private void OnEnable()
    {
        lastPercent = -1f;
        Refresh();
    }

    private void LateUpdate()
    {
        if (satisfactionTracker == null)
        {
            FindTracker();
        }

        Refresh();
    }

    private void FindTracker()
    {
        if (satisfactionTracker != null)
            return;

        satisfactionTracker =
            FindFirstObjectByType<SatisfactionTracker>();
    }

    private void Refresh()
    {
        if (satisfactionTracker == null)
            return;

        float percent = Mathf.Clamp(
            satisfactionTracker.LiveSatisfactionPercent,
            0f,
            100f
        );

        if (float.IsNaN(percent))
            return;

        if (Mathf.Abs(percent - lastPercent) < 0.01f)
            return;

        lastPercent = percent;

        UpdatePercentage(percent);
        UpdateProgressBar(percent);
        UpdateStars(percent);
    }

    private void UpdatePercentage(float percent)
    {
        if (satisfactionPercentText != null)
        {
            satisfactionPercentText.text =
                $"{percent:0.0}%";
        }
    }

    private void UpdateProgressBar(float percent)
    {
        float normalized = percent / 100f;

        // Image-based progress bar.
        if (satisfactionFill != null &&
            satisfactionFill.type == Image.Type.Filled)
        {
            satisfactionFill.fillAmount = normalized;
        }

        // Slider-based progress bar.
        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 1f;

            satisfactionSlider.SetValueWithoutNotify(
                normalized
            );
        }
    }

    private void UpdateStars(float percent)
    {
        // Use the same rating rules as S4 and rewards.
        int stars = WaveRewardCalculator.GetStarRating(
            Mathf.RoundToInt(percent)
        );

        if (ratingText != null)
{
    float rating = percent / 20f;
    ratingText.text = $"{rating:0.0}";
}
        if (starImages == null)
            return;

        for (int i = 0; i < starImages.Length; i++)
        {
            Image star = starImages[i];

            if (star == null)
                continue;

            star.color = i < stars
                ? activeStarColor
                : inactiveStarColor;
        }
    }
}