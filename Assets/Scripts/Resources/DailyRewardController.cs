using System;
using System.Collections;
using UnityEngine;

public sealed class DailyRewardController : MonoBehaviour, IGameSaveParticipant
{
    [SerializeField] private DailyRewardWindow rewardWindow;
    [SerializeField] private ResourceType rewardType = ResourceType.Coins;
    [SerializeField, Min(1)] private int rewardAmount = 100;
    [SerializeField] private Sprite rewardIcon;
    private DailyRewardCalendar calendar = new();
    private bool ready, transaction;
    public string SaveKey => "daily-reward.v1";
    public int LatestSeenDay => calendar.latestSeenDay;
    public int LastClaimDay => calendar.lastClaimDay;
    public bool IsReady => ready;
    public bool IsAvailable => ready && calendar.CanClaim(Today());
    public string LastError { get; private set; }
#if UNITY_EDITOR
    public Func<DateTime> DateOverride;
#endif
    private DateTime Today()
    {
#if UNITY_EDITOR
        if (DateOverride != null) return DateOverride().Date;
#endif
        return DateTime.Today;
    }
    private IEnumerator Start()
    {
        while (GamePersistence.Instance == null || !GamePersistence.Instance.IsReady) yield return null;
        ready = true;
        if (rewardWindow != null) rewardWindow.ClaimRequested = TryClaim;
        EvaluateDay();
    }
    private void OnDestroy()
    {
        if (rewardWindow != null) rewardWindow.ClaimRequested = null;
    }
    private void OnApplicationPause(bool paused) { if (!paused && ready) EvaluateDay(); }
    public void EvaluateDay()
    {
        if (!ready || transaction) return;
        if (calendar.Observe(Today())) GamePersistence.Instance.SaveNow();
        if (rewardWindow == null) return;
        if (!calendar.CanClaim(Today())) { if (rewardWindow.IsOpen) rewardWindow.Hide(); return; }
        if (!rewardWindow.IsOpen)
            rewardWindow.Show(rewardIcon, rewardType == ResourceType.Energy ? Mathf.Min(rewardAmount,
                Mathf.Max(0, ResourceManager.MaxEnergy - ResourceManager.Instance.GetResource(ResourceType.Energy))) : rewardAmount, rewardType.ToString());
    }
    public bool TryClaim()
    {
        if (!ready || transaction || GamePersistence.IsRestoring || GamePersistence.IsPaused) return false;
        DateTime today = Today();
        calendar.Observe(today);
        if (!calendar.CanClaim(today)) { LastError = "Daily reward is not available for this date."; EvaluateDay(); return false; }
        if (rewardAmount <= 0 || !Enum.IsDefined(typeof(ResourceType), rewardType)) return false;
        var resources = ResourceManager.Instance;
        if (resources == null) return false;
        var before = resources.CaptureResources();
        int previousClaim = calendar.lastClaimDay;
        transaction = true;
        bool committed = false;
        try
        {
            calendar.lastClaimDay = DailyRewardCalendar.Day(today);
            bool fullEnergy = rewardType == ResourceType.Energy && resources.GetResource(ResourceType.Energy) >= ResourceManager.MaxEnergy;
            if (!fullEnergy && !resources.AddResource(rewardType, rewardAmount)) { LastError = "The reward cannot be added (resource limit reached)."; return false; }
            if (!GamePersistence.Instance.SaveNow())
            {
                LastError = "Could not save the daily reward. Please try again.";
                return false;
            }
            committed = true;
            LastError = null;
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
        finally
        {
            try { if (!committed) { calendar.lastClaimDay = previousClaim; resources.RestoreResources(before); } }
            finally { transaction = false; }
        }
    }
    public string CaptureSaveJson() => JsonUtility.ToJson(calendar);
    public void RestoreSaveJson(string json)
    {
        var saved = JsonUtility.FromJson<DailyRewardCalendar>(json);
        if (saved == null || !saved.IsValid()) throw new ArgumentException("Invalid daily reward calendar.");
        calendar = saved;
    }
}
