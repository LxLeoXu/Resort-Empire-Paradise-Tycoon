
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [SerializeField]
    private Color activeStarColor =
        new Color(1f, 0.75f, 0.10f);

    [SerializeField]
    private Color inactiveStarColor =
        new Color(0.62f, 0.65f, 0.68f);

    [Header("Claim")]
    [SerializeField] private ClaimRewards claimRewards;
    [SerializeField] private bool hideOnStart = true;

    private bool resultsShown;

    private void OnEnable()
    {
        if (claimRewards != null)
        {
            claimRewards.OnClaimed -= HideResults;
            claimRewards.OnClaimed += HideResults;
        }
    }

    private void OnDisable()
    {
        if (claimRewards != null)
            claimRewards.OnClaimed -= HideResults;
    }

    private void Start()
    {
        if (hideOnStart && !resultsShown)
            gameObject.SetActive(false);
    }

    // Kompatibilita s povodnym testovacim UI.
    // Wave ID 0 pouzivame iba na nahlad.
    public void ShowResults(
        int servedGuests,
        int satisfactionPercent)
    {
        ShowResults(
            servedGuests,
            -1,
            satisfactionPercent);
    }

    public void ShowResults(
        int servedGuests,
        int totalGuests,
        int satisfactionPercent)
    {
        ShowWaveResults(
            0,
            servedGuests,
            totalGuests,
            satisfactionPercent);
    }

    // Skutocna vlna: waveId je cislo ukoncenej vlny.
    public bool ShowWaveResults(
        int waveId,
        int servedGuests,
        int totalGuests,
        int satisfactionPercent,
        string positiveFeedback = null,
        string negativeFeedback = null,
        bool restoreExistingReward = false)
    {
        if (waveId < 0)
            return false;

        if (claimRewards == null)
        {
            Debug.LogError(
                "S4: ClaimRewards nie je priradeny!",
                this);
            return false;
        }

        ResourceManager manager = ResourceManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "S4: ResourceManager neexistuje!",
                this);
            return false;
        }

        int guests = Mathf.Max(0, servedGuests);

        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        // Vypocitaj odmeny iba raz.
        WaveRewardsData calculated =
            WaveRewardCalculator.CalculateRewards(
                guests,
                satisfaction);

        // Energia moze mat maximum 50.
        int currentEnergy =
            manager.GetResource(ResourceType.Energy);

        int energySpace = Mathf.Max(
            0,
            ResourceManager.MaxEnergy - currentEnergy);

        WaveRewardsData rewards = new WaveRewardsData(
            Mathf.Max(0, calculated.coins),
            Mathf.Clamp(calculated.energy, 0, energySpace),
            Mathf.Max(0, calculated.resortValue));

        // Iba nova vlna moze pripravit novu odmenu.
        bool alreadyPrepared = restoreExistingReward && claimRewards.HasPendingReward && claimRewards.CaptureState().waveId == waveId;
        if (alreadyPrepared) rewards = claimRewards.CaptureState().rewards;
        if (!alreadyPrepared && !claimRewards.SetRewardsForWave(waveId, rewards))
        {
            Debug.LogWarning(
                "S4: Odmena pre tuto vlnu sa " +
                "neda znova pripravit.");
            return false;
        }

        // Zobraz realny pocet hosti.
        if (guestsText != null)
        {
            guestsText.text = totalGuests > 0
                ? guests + "/" + totalGuests
                : guests.ToString();
        }

        if (satisfactionText != null)
            satisfactionText.text = satisfaction + "%";

        // Ziadne vymyslene spravy z Figmy.
        SetFeedback(statusLine1Text, positiveFeedback);
        SetFeedback(statusLine2Text, negativeFeedback);

        // Presne tie odmeny, ktore sme pripravili.
        if (coinsRewardText != null)
            coinsRewardText.text =
                "+" + rewards.coins + " Coins";

        if (energyRewardText != null)
            energyRewardText.text =
                "+" + rewards.energy + " Energy";

        if (resortValueRewardText != null)
            resortValueRewardText.text =
                "+" + rewards.resortValue + " Resort Value";

        UpdateStars(satisfaction);

        resultsShown = true;
        gameObject.SetActive(true);

        Debug.Log(
            $"S4 Wave {waveId}: Guests={guests}, " +
            $"Satisfaction={satisfaction}%, " +
            $"Coins={rewards.coins}, " +
            $"Energy={rewards.energy}, " +
            $"ResortValue={rewards.resortValue}");

        return true;
    }

    private void UpdateStars(int satisfaction)
    {
        int stars = WaveRewardCalculator.GetStarRating(satisfaction);

        if (starImages == null)
            return;

        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] != null)
            {
                starImages[i].color =
                    i < stars
                        ? activeStarColor
                        : inactiveStarColor;
            }
        }
    }

    private void SetFeedback(
        TMP_Text textField,
        string message)
    {
        if (textField == null)
            return;

        bool hasMessage =
            !string.IsNullOrWhiteSpace(message);

        textField.gameObject.SetActive(hasMessage);

        if (hasMessage)
            textField.text = message;
    }

    public void HideResults()
    {
        resultsShown = false;
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void EditorBind(
        TMP_Text guests,
        TMP_Text satisfaction,
        TMP_Text status1,
        TMP_Text status2,
        TMP_Text coins,
        TMP_Text energy,
        TMP_Text resort,
        Image[] stars,
        ClaimRewards claim)
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
