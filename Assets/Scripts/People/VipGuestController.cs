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
public sealed class VipVisitSave
{
    public bool offered, waiting, rewarded;
    public string guestId;
    public int afterWave;
    public List<string> fulfilled = new();  // Zaplatené Materiály (READY)
    public List<string> completed = new();  // Dokončené po odpočte (DONE / COMPLETED)
}

public sealed class VipGuestController : MonoBehaviour, IGameSaveParticipant
{
    [SerializeField] private VipGuestDefinition definition;
    [SerializeField, Min(1)] private int arrivalAfterWave = 3;
    [SerializeField, Min(11)] private int vipReputationReward = 25;
    [SerializeField] private UnityEvent onVipArrived = new();

    [Header("VIP Hodnotenie - Hranice hviezdičiek (Inspector)")]
    [Range(0, 100)][SerializeField] private int star3Threshold = 50;
    [Range(0, 100)][SerializeField] private int star4Threshold = 70;
    [Range(0, 100)][SerializeField] private int star5Threshold = 90;

    [Header("S8 Výsledok")]
    [SerializeField] private S4FigmaResultsUI resultsUI;

    public event Action<VipGuestDefinition> GuestArrived;
    public event Action<VipGuestDefinition> VisitRestored;
    public event Action Changed;
    public event Action<int, int, int, List<VipReward>> VipCompleted;

    public VipGuestDefinition Definition => definition;

    public VipVisitSave CurrentVisit =>
        JsonUtility.FromJson<VipVisitSave>(JsonUtility.ToJson(visit));

    public bool IsWaiting => visit.waiting;
    public bool IsReady => ready;
    public int Star3Threshold => star3Threshold;
    public int Star4Threshold => star4Threshold;
    public int Star5Threshold => star5Threshold;

    public string SaveKey => "vip-guests.v1";
    public string LastError { get; private set; }

    private VipVisitSave visit = new();
    private WaveSession session;
    private bool ready, transaction;
    private float retryAt;

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

        ready = true;

        if (visit.waiting)
            VisitRestored?.Invoke(definition);

        CheckArrival();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.WaveEnded -= OnWaveEnded;
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

            if (finishedBuffer.Count > 0)
            {
                CompleteFinishedRequirements(finishedBuffer);
            }
        }
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

        bool startedAny = false;
        foreach (var req in definition.requirements)
        {
            if (GetRequirementState(req) == VipRequirementState.Ready)
            {
                float duration = Mathf.Max(1f, req.serveDurationSeconds);
                inProgressDuration[req.id] = duration;
                inProgressRemaining[req.id] = duration;
                startedAny = true;
            }
        }

        if (startedAny)
            Changed?.Invoke();

        return startedAny;
    }

    private void CompleteFinishedRequirements(List<string> finishedIds)
    {
        foreach (var id in finishedIds)
        {
            inProgressRemaining.Remove(id);
            inProgressDuration.Remove(id);
        }

        Commit(() =>
        {
            visit.completed ??= new List<string>();
            foreach (var id in finishedIds)
            {
                if (!visit.completed.Contains(id))
                    visit.completed.Add(id);
            }
            return true;
        });

        // Ak sú hotové všetky 3 hlavné požiadavky a žiadna ďalšia práve nebeží -> dokončiť VIP a otvoriť S8
        bool allRequiredDone = definition.requirements
            .Where(r => !r.bonus)
            .All(r => GetRequirementState(r) == VipRequirementState.Completed);

        if (allRequiredDone && inProgressRemaining.Count == 0)
        {
            TryFinishAndOpenS8();
        }
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
            satisfactionPercent = Mathf.RoundToInt((float)completedTotal / Mathf.Max(1, totalCount) * 100f);
            stars = CalculateVipStars(satisfactionPercent);

            visit.waiting = false;
            visit.rewarded = true;
            return true;
        });

        if (ok)
        {
            VipCompleted?.Invoke(completedTotal, totalCount, satisfactionPercent, grantedRewards);

            if (resultsUI == null)
                resultsUI = FindFirstObjectByType<S4FigmaResultsUI>(FindObjectsInactive.Include);

            if (resultsUI != null)
            {
                resultsUI.ShowVipResults(completedTotal, totalCount, satisfactionPercent, stars, grantedRewards);
            }
        }

        return ok;
    }

    public int CalculateVipStars(int satisfactionPercent)
    {
        if (satisfactionPercent >= 100) return 5;
        if (satisfactionPercent >= star5Threshold) return 5;
        if (satisfactionPercent >= star4Threshold) return 4;
        if (satisfactionPercent >= star3Threshold) return 3;
        return 2;
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
        inProgressRemaining.Clear();
        inProgressDuration.Clear();
        return Commit(() =>
        {
            visit.waiting = false;
            return true;
        });
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

    public string CaptureSaveJson() => JsonUtility.ToJson(visit);

    public void RestoreSaveJson(string json)
    {
        var saved = JsonUtility.FromJson<VipVisitSave>(json);
        if (saved != null)
        {
            saved.fulfilled ??= new List<string>();
            saved.completed ??= new List<string>();
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

        inProgressRemaining.Clear();
        inProgressDuration.Clear();
        visit = saved;
    }
}