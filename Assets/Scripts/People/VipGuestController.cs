using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class VipVisitSave
{
    public bool offered, waiting, rewarded;
    public string guestId;
    public int afterWave;
    public List<string> fulfilled = new();
    public VipStayResult result;
}

/// <summary>VIP visit has no expiry and never enters/block the ordinary wave queues.</summary>
public sealed class VipGuestController : MonoBehaviour, IGameSaveParticipant
{
    [SerializeField] private VipGuestDefinition definition;
    [SerializeField, Min(1)] private int arrivalAfterWave = 3;
    [SerializeField] private UnityEvent onVipArrived = new();
    public event Action<VipGuestDefinition> GuestArrived;
    public event Action<VipGuestDefinition> VisitRestored;
    public event Action Changed;
    public VipGuestDefinition Definition => definition;
    public VipVisitSave CurrentVisit => JsonUtility.FromJson<VipVisitSave>(JsonUtility.ToJson(visit));
    public bool IsWaiting => visit.waiting;
    public bool HasPendingResult => visit.result != null && !visit.waiting && !visit.rewarded;
    public VipStayResult PendingResult => HasPendingResult ? JsonUtility.FromJson<VipStayResult>(JsonUtility.ToJson(visit.result)) : null;
    public bool IsReady => ready;
    public string SaveKey => "vip-guests.v1";
    public string LastError { get; private set; }
    private VipVisitSave visit = new();
    private WaveSession session;
    private bool ready, transaction;
    private float retryAt;

    private IEnumerator Start()
    {
        while (GamePersistence.Instance == null || !GamePersistence.Instance.IsReady) yield return null;
        session = FindFirstObjectByType<WaveManager>()?.Session;
        if (session == null) yield break;
        session.WaveEnded += OnWaveEnded;
        ready = true;
        (GetComponent<VipResultsPresenter>() ?? gameObject.AddComponent<VipResultsPresenter>()).Bind(this);
        if (visit.waiting) VisitRestored?.Invoke(definition);
        CheckArrival();
    }
    private void OnDestroy() { if (session != null) session.WaveEnded -= OnWaveEnded; }
    private void OnWaveEnded(int wave) => CheckArrival();
    private void Update()
    {
        if (!ready || visit.offered || Time.unscaledTime < retryAt) return;
        retryAt = Time.unscaledTime + 1f;
        CheckArrival();
    }
    public bool CheckArrival()
    {
        if (!ready || transaction || GamePersistence.IsRestoring || visit.offered || definition == null || !definition.IsValid()) return false;
        int completed = session.State == WaveState.Running ? session.WaveNumber - 1 : session.WaveNumber;
        if (completed < Mathf.Max(1, arrivalAfterWave)) return false;
        return Commit(() =>
        {
            visit = new VipVisitSave { offered = true, waiting = true, guestId = definition.guestId, afterWave = completed };
            return true;
        }, () => { GuestArrived?.Invoke(definition); onVipArrived?.Invoke(); });
    }
    public bool TryFulfillRequirement(string requirementId)
    {
        if (!CanAct() || visit.fulfilled.Contains(requirementId)) return false;
        var requirement = definition.requirements.Find(r => r.id == requirementId);
        if (requirement == null) return false;
        return Commit(() =>
        {
            if (!ResourceManager.Instance.SpendResource(ResourceType.Materials, requirement.materials)) return false;
            visit.fulfilled.Add(requirement.id);
            return true;
        });
    }
    public bool TryAccept()
    {
        if (!CanAct() || definition.requirements.Any(r => !r.bonus && !visit.fulfilled.Contains(r.id))) return false;
        return Commit(() =>
        {
            visit.result = VipStayResult.Create(definition, visit.fulfilled, true);
            visit.waiting = false;
            return true;
        });
    }
    public bool TryDecline()
    {
        if (!CanAct()) return false;
        return Commit(() => { visit.result = VipStayResult.Create(definition, visit.fulfilled, false); visit.waiting = false; return true; });
    }
    public bool TryClaimResult()
    {
        if (!ready || transaction || GamePersistence.IsPaused || GamePersistence.IsRestoring || !HasPendingResult) return false;
        return Commit(() =>
        {
            if (!visit.result.IsValid()) return false;
            visit.rewarded = true; // Reentrant callbacks cannot claim this result again.
            return ClaimRewards.TryGrantResources(ResourceManager.Instance, visit.result.rewards);
        });
    }

    private bool CanAct() => ready && !transaction && !GamePersistence.IsPaused && !GamePersistence.IsRestoring &&
        visit.waiting && !visit.rewarded && definition != null && definition.IsValid() && visit.guestId == definition.guestId;

    private bool Commit(Func<bool> change, Action afterSave = null)
    {
        if (transaction || ResourceManager.Instance == null || GamePersistence.Instance == null || !GamePersistence.Instance.IsReady) return false;
        transaction = true;
        string previous = CaptureSaveJson();
        var resources = ResourceManager.Instance.CaptureResources();
        bool committed = false;
        try
        {
            if (!change() || !GamePersistence.Instance.SaveNow()) { LastError = "VIP action could not be completed or saved."; return false; }
            committed = true; LastError = null;
        }
        finally
        {
            if (!committed) { visit = JsonUtility.FromJson<VipVisitSave>(previous); ResourceManager.Instance.RestoreResources(resources); }
            transaction = false;
        }
        Changed?.Invoke();
        afterSave?.Invoke();
        return true;
    }
    public string CaptureSaveJson() => JsonUtility.ToJson(visit);
    public void RestoreSaveJson(string json)
    {
        var saved = JsonUtility.FromJson<VipVisitSave>(json);
        if (saved == null || saved.afterWave < 0 || saved.fulfilled == null || saved.fulfilled.Any(string.IsNullOrWhiteSpace) ||
            saved.fulfilled.Distinct().Count() != saved.fulfilled.Count || (saved.waiting && (!saved.offered || saved.rewarded || string.IsNullOrWhiteSpace(saved.guestId))))
            throw new ArgumentException("Invalid saved VIP visit.");
        if (saved.result != null &&
    (!saved.result.IsValid() || saved.waiting || !saved.offered))
{
    VipStayResult r = saved.result;

    bool expectedPerfect =
        r.requiredCompleted == 3 && r.bonusCompleted;

    int expectedStars =
        expectedPerfect ? 5 : r.requiredCompleted + 1;

    Debug.LogWarning(
        "[VIP SAVE DIAGNOSTIC]\n" +
        $"Offered: {saved.offered}\n" +
        $"Waiting: {saved.waiting}\n" +
        $"Rewarded: {saved.rewarded}\n" +
        $"Guest name valid: {!string.IsNullOrWhiteSpace(r.guestName)}\n" +
        $"Required completed: {r.requiredCompleted}\n" +
        $"Bonus completed: {r.bonusCompleted}\n" +
        $"Perfect Stay: {r.perfectStay}\n" +
        $"Expected Perfect Stay: {expectedPerfect}\n" +
        $"Stars: {r.stars}\n" +
        $"Expected stars: {expectedStars}\n" +
        $"Requirements count: {r.requirements?.Count ?? -1}\n" +
        $"Rewards present: {r.rewards != null}\n" +
        $"Rewards valid: {r.rewards != null && r.rewards.IsValid()}\n" +
        $"Gems: {(r.rewards != null ? r.rewards.gems : -1)}\n" +
        $"First Five Star Bonus: {r.rewards != null && r.rewards.firstFiveStarBonus}\n" +
        $"Result valid: {r.IsValid()}"
    );

    throw new ArgumentException("Invalid saved VIP result.");
}
        if (definition != null && saved.guestId == definition.guestId &&
            saved.fulfilled.Any(id => !definition.requirements.Exists(r => r.id == id)))
            throw new ArgumentException("Unknown saved VIP requirement.");
        visit = saved;
    }
}
