using UnityEngine;
using TMPro;

public class S4ResultsUI : MonoBehaviour
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
    [SerializeField] private TMP_Text starsText;

    [Header("Claim")]
    [SerializeField] private ClaimRewards claimRewards;

    public void ShowResults(int servedGuests, int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        WaveRewardsData rewards =
            WaveRewardCalculator.CalculateRewards(guests, satisfaction);

        gameObject.SetActive(true);

        if (guestsText != null)
            guestsText.text = guests + "/12";

        if (satisfactionText != null)
            satisfactionText.text = satisfaction + "%";

        if (statusLine1Text != null)
            statusLine1Text.text = "Rooms & drinks went great!";

        if (statusLine2Text != null)
            statusLine2Text.text = "Food capacity needs improvement";

        if (coinsRewardText != null)
            coinsRewardText.text = "+ " + rewards.coins + " Coins";

        if (energyRewardText != null)
            energyRewardText.text = "+ " + rewards.energy + " Energy";

        if (resortValueRewardText != null)
            resortValueRewardText.text = "+ " + rewards.resortValue + " Resort Value";

        int stars = satisfaction >= 90 ? 5 :
                    satisfaction >= 80 ? 4 :
                    satisfaction >= 70 ? 3 :
                    satisfaction >= 55 ? 2 :
                    satisfaction >= 40 ? 1 : 0;

        if (starsText != null)
            starsText.text = new string('★', stars) + new string('☆', 5 - stars);

        if (claimRewards != null)
            claimRewards.SetRewards(rewards);
    }

    public void HideResults()
    {
        gameObject.SetActive(false);
    }
}