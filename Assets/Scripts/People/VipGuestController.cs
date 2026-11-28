using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public enum VipRequirementState
{
    Missing,
    Ready,
    InProgress,
    Completed
}

[Serializable]
public sealed class VipRequirementTimerSave
{
    public string requirementId;
    public float remainingSeconds;
    public float durationSeconds;
}

[Serializable]
public sealed class VipVisitSave
{
    public bool offered, waiting, rewarded;
    public string guestId;
    public int afterWave;
    public List<string> fulfilled = new();
    public List<string> completed = new();
    public VipStayResult result;
    // Optional in older saves: running VIP work resumes without counting offline time.
    public List<VipRequirementTimerSave> activeTimers = new();
}

public sealed class VipGuestController : MonoBehaviour, IGameSaveParticipant
{
    [SerializeField] private VipGuestDefinition definition;
    [SerializeField, Min(1)] private int arrivalAfterWave = 3;
    [SerializeField, Min(11)] private int vipReputationReward = 25;
    [SerializeField] private UnityEvent onVipArrived = new();

    [Header("VIP Spokojnosť a Hranice hviezdičiek (Inspector)")]
    [Tooltip("Koľko percent z celkovej spokojnosti tvorí splnenie bonusovej požiadavky.")]
    [Range(0, 100)][SerializeField] private int bonusWeightPercent = 25;
    [Range(0, 100)][SerializeField] private int star1Threshold = 0;
    [Range(0, 100)][SerializeField] private int star2Threshold = 30;
    [Range(0, 100)][SerializeField] private int star3Threshold = 50;
    [Range(0, 100)][SerializeField] private int star4Threshold = 70;
    [Range(0, 100)][SerializeField] private int star5Threshold = 90;

    [Header("VIP Strop hviezd pri chýbajúcej role (Inspector)")]
    [Tooltip("Strop hviezd, keď požiadavke chýba špecialista, manažér alebo Spa (jeden spôsob pre všetky tri).")]
    [Range(1, 5)][SerializeField] private int missingRoleStarCap = 3;

    [Header("S8 Výsledok")]
    [SerializeField] private S4FigmaResultsUI resultsUI;

    public event Action<VipGuestDefinition> GuestArrived;
    public event Action<VipGuestDefinition> VisitRestored;
    public event Action Changed;
    public event Action<int, int, int, List<VipReward>> VipCompleted;

    public VipGuestDefinition Definition => definition;

    public VipVisitSave CurrentVisit =>
        JsonUtility.FromJson<VipVisitSave>(CaptureSaveJson());

    public bool IsWaiting => visit.waiting;
    public bool HasPendingResult => visit.result != null && !visit.waiting && !visit.rewarded;
    public VipStayResult PendingResult => HasPendingResult ? JsonUtility.FromJson<VipStayResult>(JsonUtility.ToJson(visit.result)) : null;
    public bool IsReady => ready;
    public int Star3Threshold => star3Threshold;
    public int Star4Threshold => star4Threshold;
    public int Star5Threshold => star5Threshold;
    public int MissingRoleStarCap => missingRoleStarCap;

    public string SaveKey => "vip-guests.v1";
    public string LastError { get; private set; }

    private VipVisitSave visit = new();
    private WaveSession session;
    private WorkerRoster subscribedRoster;
    private bool ready, transaction;
    private float retryAt;
    private float nextRequirementSaveRetryAt;
    private float nextVipFinishRetryAt;
    private float nextTimerCheckpointAt;
    private const float TimerCheckpointInterval = 10f;

    // Aktívne odpočty pre požiadavky v stave IN_PROGRESS
    private readonly Dictionary<string, float> inProgressRemaining = new();
    private readonly Dictionary<string, float> inProgressDuration = new();
    private readonly List<string> finishedBuffer = new();

    private IEnumerator Start()
    {
        while (GamePersistence.Instance == null ||
               !GamePersistence.Instance.IsReady)
        {
            yield return null;
        }

        session = FindFirstObjectByType<WaveManager>()?.Session;

        if (session == null)
            yield break;

        session.WaveEnded += OnWaveEnded;

        subscribedRoster = WorkerRoster.Instance;
        if (subscribedRoster != null)
            subscribedRoster.SpecialistsChanged += OnSpecialistsChanged;

        ready = true;

        if (visit.waiting)
            VisitRestored?.Invoke(definition);

        CheckArrival();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.WaveEnded -= OnWaveEnded;

        if (subscribedRoster != null)
            subscribedRoster.SpecialistsChanged -= OnSpecialistsChanged;
    }

    private void OnSpecialistsChanged()
    {
        if (ready && visit.waiting)
            Changed?.Invoke();
    }

    // Android often suspends the process rather than calling OnApplicationQuit.
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveActiveTimersIfPossible();
    }

    private void OnApplicationQuit() => SaveActiveTimersIfPossible();

    private void SaveActiveTimersIfPossible()
    {
        if (!ready || transaction || !visit.waiting || visit.rewarded ||
            inProgressRemaining.Count == 0 || GamePersistence.IsRestoring ||
            GamePersistence.Instance == null || !GamePersistence.Instance.IsReady)
            return;

        // Checkpoint only: no resources or progress are changed here.
        if (!GamePersistence.Instance.SaveNow())
            Debug.LogWarning("VIP: Timer checkpoint save failed; remaining timers are still active.", this);
    }

    private void OnWaveEnded(int wave)
    {
        CheckArrival();
    }

    private void Update()
    {
        if (!ready) return;

        // 1. Kontrola príchodu VIP
        if (!visit.offered && Time.unscaledTime >= retryAt)
        {
            retryAt = Time.unscaledTime + 1f;
            CheckArrival();
        }

        // 2. Odpočet požiadaviek v stave IN_PROGRESS
        if (inProgressRemaining.Count > 0 && Time.deltaTime > 0f && !GamePersistence.IsPaused && !GamePersistence.IsRestoring)
        {
            finishedBuffer.Clear();
            var keys = new List<string>(inProgressRemaining.Keys);

            foreach (var id in keys)
            {
                inProgressRemaining[id] -= Time.deltaTime;
                if (inProgressRemaining[id] <= 0f)
                {
                    finishedBuffer.Add(id);
                }
            }

            // Avoid retrying a failed save every frame (including after the timer reached zero).
            if (finishedBuffer.Count > 0 && Time.unscaledTime >= nextRequirementSaveRetryAt)
            {
                CompleteFinishedRequirements(finishedBuffer);
            }
        }

        // A periodic checkpoint limits progress loss if Android terminates the app.
        // The saved duration NEVER advances while the player is offline.
        if (inProgressRemaining.Count > 0 &&
            Time.unscaledTime >= nextTimerCheckpointAt &&
            !GamePersistence.IsPaused && !GamePersistence.IsRestoring && !transaction)
        {
            nextTimerCheckpointAt = Time.unscaledTime + TimerCheckpointInterval;
            SaveActiveTimersIfPossible();
        }

        // A failed final save must not leave a completed VIP stuck with no Ready buttons.
        // Also recover an already-completed, still-waiting visit after a restart.
        if (Time.unscaledTime >= nextVipFinishRetryAt &&
            CanAct() &&
            inProgressRemaining.Count == 0 &&
            definition.requirements.Where(r => !r.bonus)
                .All(r => GetRequirementState(r) == VipRequirementState.Completed))
        {
            nextVipFinishRetryAt = Time.unscaledTime + 2f;
            TryFinishAndOpenS8();
        }
    }

    public bool IsRoleAvailable(VipRequirement req)
    {
        if (req == null || req.requiredRole == VipRequiredRole.None)
            return true;

        var roster = subscribedRoster != null ? subscribedRoster : WorkerRoster.Instance;
        return roster != null && roster.IsRoleAvailable(req.requiredRole);
    }

    public int GetRequirementStarCap(VipRequirement req)
    {
        if (req == null || IsRoleAvailable(req))
            return 5;

        int cap = req.missingRoleStarCap > 0 ? req.missingRoleStarCap : missingRoleStarCap;
        return Mathf.Clamp(cap, 1, 5);
    }

    public int GetActiveStayStarCap()
    {
        if (definition == null || definition.requirements == null)
            return 5;

        int lowestCap = 5;
        foreach (var r in definition.requirements)
        {
            if (r == null || r.requiredRole == VipRequiredRole.None)
                continue;

            bool affectsStay = !r.bonus || GetRequirementState(r) == VipRequirementState.Completed;
            if (!affectsStay) continue;

            if (!IsRoleAvailable(r))
                lowestCap = Mathf.Min(lowestCap, GetRequirementStarCap(r));
        }

        return lowestCap;
    }

    public VipRequirementState GetRequirementState(VipRequirement req)
    {
        if (req == null) return VipRequirementState.Missing;

        if (visit.completed != null && visit.completed.Contains(req.id))
            return VipRequirementState.Completed;

        if (inProgressRemaining.ContainsKey(req.id))
            return VipRequirementState.InProgress;

        if (req.startsReady || (visit.fulfilled != null && visit.fulfilled.Contains(req.id)))
            return VipRequirementState.Ready;

        return VipRequirementState.Missing;
    }

    public float GetRemainingSeconds(string requirementId)
    {
        return inProgressRemaining.TryGetValue(requirementId, out float rem) ? Mathf.Max(0f, rem) : 0f;
    }

    public float GetTotalDuration(string requirementId)
    {
        return inProgressDuration.TryGetValue(requirementId, out float dur) ? Mathf.Max(0.1f, dur) : 12f;
    }

    public int GetReadyCount()
    {
        if (definition == null || definition.requirements == null) return 0;
        int count = 0;
        foreach (var r in definition.requirements)
        {
            if (GetRequirementState(r) == VipRequirementState.Ready)
                count++;
        }
        return count;
    }

    public bool CheckArrival()
    {
        if (!ready ||
            transaction ||
            GamePersistence.IsRestoring ||
            visit.offered ||
            definition == null ||
            !definition.IsValid())
            return false;

        int completed = session.State == WaveState.Running
            ? session.WaveNumber - 1
            : session.WaveNumber;

        if (completed < Mathf.Max(1, arrivalAfterWave))
            return false;

        return Commit(
            () =>
            {
                visit = new VipVisitSave
                {
                    offered = true,
                    waiting = true,
                    guestId = definition.guestId,
                    afterWave = completed
                };

                return true;
            },
            () =>
            {
                GuestArrived?.Invoke(definition);
                onVipArrived?.Invoke();
            });
    }

    /// <summary>
    /// Tlačidlo FIX: Minie materiály cez SpendResource a prepne požiadavku na READY.
    /// </summary>
    public bool TryFulfillRequirement(string requirementId)
    {
        if (!CanAct())
            return false;

        var requirement = definition.requirements.Find(r => r.id == requirementId);
        if (requirement == null)
            return false;

        if (GetRequirementState(requirement) != VipRequirementState.Missing)
            return false;

        return Commit(() =>
        {
            if (!ResourceManager.Instance.SpendResource(ResourceType.Materials, requirement.materials))
                return false;

            if (!visit.fulfilled.Contains(requirement.id))
                visit.fulfilled.Add(requirement.id);

            return true;
        });
    }

    /// <summary>
    /// Tlačidlo SERVE ALL: Spustí všetky aktuálne READY požiadavky do stavu IN_PROGRESS.
    /// </summary>
    public bool StartServeAll()
    {
        if (!CanAct()) return false;

        // Stage only READY requirements, preserving any already-running timers.
        var toStart = definition.requirements
            .Where(req => GetRequirementState(req) == VipRequirementState.Ready)
            .ToList();
        if (toStart.Count == 0) return false;

        bool committed = false;
        try
        {
            // Commit saves the new timer snapshot along with all game progress.
            // Its existing Changed event updates S7 after a successful save.
            committed = Commit(() =>
            {
                foreach (var req in toStart)
                {
                    float duration = Mathf.Max(1f, req.serveDurationSeconds);
                    inProgressDuration[req.id] = duration;
                    inProgressRemaining[req.id] = duration;
                }
                return true;
            });
            if (committed)
                nextTimerCheckpointAt = Time.unscaledTime + TimerCheckpointInterval;
            return committed;
        }
        finally
        {
            // Commit already restores visit/resources on a failed save; also
            // revert the new runtime timers so the player can press Serve All again.
            if (!committed)
            {
                foreach (var req in toStart)
                {
                    inProgressRemaining.Remove(req.id);
                    inProgressDuration.Remove(req.id);
                }
            }
        }
    }

    private void CompleteFinishedRequirements(List<string> finishedIds)
    {
        // Keep the finished timers until their Completed state is saved successfully.
        // Otherwise a save failure leaves neither a Ready row nor a running timer to retry.
        if (!Commit(() =>
        {
            visit.completed ??= new List<string>();
            foreach (var id in finishedIds)
            {
                if (!visit.completed.Contains(id))
                    visit.completed.Add(id);
            }
            return true;
        }))
        {
            nextRequirementSaveRetryAt = Time.unscaledTime + 2f;
            Debug.LogWarning("VIP: Could not save completed requirements; retrying safely.", this);
            return;
        }

        nextRequirementSaveRetryAt = 0f;
        foreach (var id in finishedIds)
        {
            inProgressRemaining.Remove(id);
            inProgressDuration.Remove(id);
        }

        // Update() now handles the final save and its retries after all timers are gone.
    }

    public bool TryFinishAndOpenS8()
    {
        if (!CanAct()) return false;

        var grantedRewards = new List<VipReward>();
        int completedTotal = 0;
        int totalCount = definition.requirements.Count;
        int satisfactionPercent = 0;
        int stars = 1;

        bool ok = Commit(() =>
        {
            if (definition.requirements.Any(r => !r.bonus && GetRequirementState(r) != VipRequirementState.Completed))
                return false;

            var rewards = new List<VipReward>(definition.rewards);

            bool bonusDone = definition.requirements
                .Where(r => r.bonus)
                .All(r => GetRequirementState(r) == VipRequirementState.Completed);

            if (bonusDone)
                rewards.AddRange(definition.bonusRewards);

            rewards.Add(new VipReward
            {
                resource = ResourceType.Reputation,
                amount = vipReputationReward
            });

            foreach (var reward in rewards)
            {
                if (!ResourceManager.Instance.AddResource(reward.resource, reward.amount))
                    return false;
            }

            grantedRewards = rewards;
            completedTotal = definition.requirements.Count(r => GetRequirementState(r) == VipRequirementState.Completed);
            int rawSatisfaction = CalculateSatisfactionPercent();
            int stayCap = GetActiveStayStarCap();
            stars = Mathf.Min(CalculateVipStars(rawSatisfaction), stayCap);
            satisfactionPercent = CapSatisfactionForStars(rawSatisfaction, stars);

            visit.waiting = false;
            visit.rewarded = true;
            return true;
        });

        if (ok)
        {
            VipCompleted?.Invoke(completedTotal, totalCount, satisfactionPercent, grantedRewards);

            // S7 has already committed all VIP rewards at this point.
            // Prefer the real purple S8 prefab; do not grant any resources in S8.
            if (!S8VipFigmaPanel.TryShowCompleted(
                    definition, CurrentVisit, satisfactionPercent, stars, grantedRewards))
            {
                Debug.LogWarning("VIP: Purple S8 is unavailable; using the S4 results fallback.");
                if (resultsUI == null)
                    resultsUI = FindFirstObjectByType<S4FigmaResultsUI>(FindObjectsInactive.Include);

                if (resultsUI != null)
                    resultsUI.ShowVipResults(completedTotal, totalCount,
                        satisfactionPercent, stars, grantedRewards);
            }
        }

        return ok;
    }

    public int CurrentSatisfactionPercent =>
        CapSatisfactionForStars(CalculateSatisfactionPercent(), CurrentStarRating);

    public int CurrentStarRating =>
        Mathf.Min(CalculateVipStars(CalculateSatisfactionPercent()), GetActiveStayStarCap());

    public int CalculateSatisfactionPercent()
    {
        if (definition == null || definition.requirements == null || definition.requirements.Count == 0)
            return 0;

        int requiredTotal = 0;
        int requiredDone = 0;
        int bonusTotal = 0;
        int bonusDone = 0;

        foreach (var r in definition.requirements)
        {
            if (r == null) continue;
            bool done = GetRequirementState(r) == VipRequirementState.Completed;
            if (r.bonus)
            {
                bonusTotal++;
                if (done) bonusDone++;
            }
            else
            {
                requiredTotal++;
                if (done) requiredDone++;
            }
        }

        float mainPool = 100f - bonusWeightPercent;
        float mainScore = requiredTotal > 0 ? (mainPool * requiredDone / requiredTotal) : mainPool;
        float bonusScore = bonusTotal > 0 ? ((float)bonusWeightPercent * bonusDone / bonusTotal) : 0f;

        return Mathf.Clamp(Mathf.RoundToInt(mainScore + bonusScore), 0, 100);
    }

    public int CalculateVipStars(int satisfactionPercent)
    {
        if (satisfactionPercent >= star5Threshold) return 5;
        if (satisfactionPercent >= star4Threshold) return 4;
        if (satisfactionPercent >= star3Threshold) return 3;
        if (satisfactionPercent >= star2Threshold) return 2;
        if (satisfactionPercent >= star1Threshold) return 1;
        return 1;
    }

    private int CapSatisfactionForStars(int rawSatisfaction, int starCap)
    {
        int maxAllowed = starCap switch
        {
            <= 1 => Mathf.Max(0, star2Threshold - 1),
            2 => Mathf.Max(0, star3Threshold - 1),
            3 => Mathf.Max(0, star4Threshold - 1),
            4 => Mathf.Max(0, star5Threshold - 1),
            _ => 100
        };

        return Mathf.Clamp(Mathf.Min(rawSatisfaction, maxAllowed), 0, 100);
    }

    // Spätná kompatibilita pre staršie volania
    public bool TryAccept()
    {
        if (!CanAct()) return false;
        foreach (var r in definition.requirements)
        {
            if (!r.bonus && !visit.fulfilled.Contains(r.id)) return false;
            if (visit.fulfilled.Contains(r.id) && !visit.completed.Contains(r.id))
                visit.completed.Add(r.id);
        }
        return TryFinishAndOpenS8();
    }

    public bool TryMaybeLater() => CanAct();

    public bool TryDecline()
    {
        if (!CanAct()) return false;
        bool ok = Commit(() =>
        {
            visit.waiting = false;
            return true;
        });
        if (ok)
        {
            inProgressRemaining.Clear();
            inProgressDuration.Clear();
        }
        return ok;
    }

    private bool CanAct()
    {
        return ready &&
               !transaction &&
               !GamePersistence.IsPaused &&
               !GamePersistence.IsRestoring &&
               visit.waiting &&
               !visit.rewarded &&
               definition != null &&
               definition.IsValid() &&
               visit.guestId == definition.guestId;
    }

    private bool Commit(Func<bool> change, Action afterSave = null)
    {
        if (transaction ||
            ResourceManager.Instance == null ||
            GamePersistence.Instance == null ||
            !GamePersistence.Instance.IsReady)
            return false;

        transaction = true;
        string previous = CaptureSaveJson();
        var resources = ResourceManager.Instance.CaptureResources();
        bool committed = false;

        try
        {
            if (!change() || !GamePersistence.Instance.SaveNow())
            {
                LastError = "VIP action could not be completed or saved.";
                return false;
            }

            committed = true;
            LastError = null;
        }
        finally
        {
            if (!committed)
            {
                visit = JsonUtility.FromJson<VipVisitSave>(previous);
                ResourceManager.Instance.RestoreResources(resources);
            }
            transaction = false;
        }

        Changed?.Invoke();
        afterSave?.Invoke();
        return true;
    }

    public string CaptureSaveJson()
    {
        // Save a copy, never mutate the live visit from a serialization callback.
        var snapshot = JsonUtility.FromJson<VipVisitSave>(JsonUtility.ToJson(visit));
        snapshot.activeTimers = new List<VipRequirementTimerSave>();

        if (visit.waiting && !visit.rewarded)
        {
            foreach (var id in inProgressRemaining.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (visit.completed != null && visit.completed.Contains(id)) continue;
                if (!inProgressDuration.TryGetValue(id, out float duration)) continue;

                snapshot.activeTimers.Add(new VipRequirementTimerSave
                {
                    requirementId = id,
                    remainingSeconds = Mathf.Clamp(inProgressRemaining[id], 0f, duration),
                    durationSeconds = duration
                });
            }
        }

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSaveJson(string json)
    {
        var saved = JsonUtility.FromJson<VipVisitSave>(json);
        if (saved != null)
        {
            saved.fulfilled ??= new List<string>();
            saved.completed ??= new List<string>();
            saved.activeTimers ??= new List<VipRequirementTimerSave>(); // legacy saves
        }

        if (saved == null ||
            saved.afterWave < 0 ||
            saved.fulfilled.Any(string.IsNullOrWhiteSpace) ||
            saved.fulfilled.Distinct().Count() != saved.fulfilled.Count ||
            saved.completed.Any(string.IsNullOrWhiteSpace) ||
            saved.completed.Distinct().Count() != saved.completed.Count ||
            (saved.waiting &&
             (!saved.offered ||
              saved.rewarded ||
              string.IsNullOrWhiteSpace(saved.guestId))))
        {
            throw new ArgumentException("Invalid saved VIP visit.");
        }

        // Validate the full saved timer list before touching runtime state.
        var timerIds = new HashSet<string>();
        foreach (var timer in saved.activeTimers)
        {
            if (timer == null || string.IsNullOrWhiteSpace(timer.requirementId) ||
                !timerIds.Add(timer.requirementId) ||
                float.IsNaN(timer.remainingSeconds) || float.IsInfinity(timer.remainingSeconds) ||
                float.IsNaN(timer.durationSeconds) || float.IsInfinity(timer.durationSeconds) ||
                timer.durationSeconds < 1f || timer.remainingSeconds < 0f ||
                timer.remainingSeconds > timer.durationSeconds ||
                !saved.waiting || saved.rewarded ||
                saved.completed.Contains(timer.requirementId) ||
                definition == null || definition.requirements == null ||
                !definition.requirements.Any(req => req != null &&
                    req.id == timer.requirementId &&
                    (req.startsReady || saved.fulfilled.Contains(req.id))))
                throw new ArgumentException("Invalid saved VIP requirement timer.");
        }

        inProgressRemaining.Clear();
        inProgressDuration.Clear();
        foreach (var timer in saved.activeTimers)
        {
            inProgressRemaining.Add(timer.requirementId, timer.remainingSeconds);
            inProgressDuration.Add(timer.requirementId, timer.durationSeconds);
        }
        visit = saved;
        nextTimerCheckpointAt = Time.unscaledTime + TimerCheckpointInterval;
    }
}