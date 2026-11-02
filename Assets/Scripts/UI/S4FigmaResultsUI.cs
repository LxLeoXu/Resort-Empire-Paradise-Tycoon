using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime controller for the S4 screen created by S4FigmaUIBuilder.
/// Requires the team's WaveRewardCalculator.CalculateRewards and ClaimRewards.SetRewards.
/// </summary>
public class S4FigmaResultsUI : MonoBehaviour
{
    [Header("Performance")]
    [SerializeField] private TMP_Text guestsText;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private TMP_Text statusLine1Text;
    [SerializeField] private TMP_Text statusLine2Text;

    [Header("Rewards")]
    [SerializeField] private TMP_Text coinsRewardText;
    [SerializeField] private TMP_Text energyRewardText;
    [SerializeField] private TMP_Text resortValueRewardText;

    [Header("Stars")]
    [SerializeField] private Image[] starImages;
    [SerializeField] private Color activeStarColor = new Color(1f, 0.75f, 0.10f);
    [SerializeField] private Color inactiveStarColor = new Color(0.62f, 0.65f, 0.68f);

    [Header("Claim")]
    [SerializeField] private ClaimRewards claimRewards;
    [SerializeField] private bool hideOnStart = true;

    private bool resultsShown;

    private void Start()
    {
        // Keep S4 out of the way until the wave actually ends.
        if (hideOnStart && !resultsShown)
            gameObject.SetActive(false);
    }

    // Compatible with callers providing only the number of served guests.
    public void ShowResults(int servedGuests, int satisfactionPercent)
    {
        ShowResults(servedGuests, -1, satisfactionPercent);
    }

    // Use totalGuests when available for the "12/12" presentation in Figma.
    public void ShowResults(int servedGuests, int totalGuests, int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        // Uses the existing game calculator; these formulas are NOT defined by this UI.
        WaveRewardsData rewards = WaveRewardCalculator.CalculateRewards(guests, satisfaction);

        if (guestsText != null)
            guestsText.text = totalGuests > 0 ? guests + "/" + totalGuests : guests.ToString();
        if (satisfactionText != null)
            satisfactionText.text = satisfaction + "%";

        // Figma mockup copy: replace after your team supplies real capacity feedback.
        if (statusLine1Text != null)
            statusLine1Text.text = "Rooms & drinks went great!";
        if (statusLine2Text != null)
            statusLine2Text.text = "Food capacity needs improvement";

        if (coinsRewardText != null)
            coinsRewardText.text = "+" + rewards.coins + " Coins";
        if (energyRewardText != null)
            energyRewardText.text = "+" + rewards.energy + " Energy";
        if (resortValueRewardText != null)
            resortValueRewardText.text = "+" + rewards.resortValue + " Resort Value";

        int stars = satisfaction >= 90 ? 5 :
                    satisfaction >= 80 ? 4 :
                    satisfaction >= 70 ? 3 :
                    satisfaction >= 55 ? 2 :
                    satisfaction >= 40 ? 1 : 0;
        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                    starImages[i].color = i < stars ? activeStarColor : inactiveStarColor;
            }
        }

        // Prepare; never credit resources merely by opening S4.
        if (claimRewards != null)
            claimRewards.SetRewards(rewards);
        else
            Debug.LogError("S4: ClaimRewards component not assigned.", this);

        resultsShown = true;
        gameObject.SetActive(true);
    }

    public void HideResults()
    {
        resultsShown = false;
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    // Used only by the editor generator to connect all created UI elements.
    public void EditorBind(
        TMP_Text guests, TMP_Text satisfaction, TMP_Text status1, TMP_Text status2,
        TMP_Text coins, TMP_Text energy, TMP_Text resort,
        Image[] stars, ClaimRewards claim)
    {
        guestsText = guests;
        satisfactionText = satisfaction;
        statusLine1Text = status1;
        statusLine2Text = status2;
        coinsRewardText = coins;
        energyRewardText = energy;
        resortValueRewardText = resort;
        starImages = stars;
        claimRewards = claim;
    }
#endif
}
