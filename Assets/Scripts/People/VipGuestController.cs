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
}

public sealed class VipGuestController : MonoBehaviour, IGameSaveParticipant
{
    [SerializeField] private VipGuestDefinition definition;
    [SerializeField, Min(1)] private int arrivalAfterWave = 3;
    [SerializeField, Min(11)] private int vipReputationReward = 25;
    [SerializeField] private UnityEvent onVipArrived = new();

    public event Action<VipGuestDefinition> GuestArrived;
    public event Action<VipGuestDefinition> VisitRestored;
    public event Action Changed;

    public VipGuestDefinition Definition => definition;

    public VipVisitSave CurrentVisit =>
        JsonUtility.FromJson<VipVisitSave>(JsonUtility.ToJson(visit));

    public bool IsWaiting => visit.waiting;
    public bool IsReady => ready;

    public string SaveKey => "vip-guests.v1";

    public string LastError { get; private set; }

    private VipVisitSave visit = new();
    private WaveSession session;
    private bool ready, transaction;
    private float retryAt;

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
        if (!ready ||
            visit.offered ||
            Time.unscaledTime < retryAt)
            return;

        retryAt = Time.unscaledTime + 1f;

        CheckArrival();
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

    public bool TryFulfillRequirement(string requirementId)
    {
        if (!CanAct() ||
            visit.fulfilled.Contains(requirementId))
            return false;

        var requirement =
            definition.requirements.Find(r => r.id == requirementId);

        if (requirement == null)
            return false;

        return Commit(() =>
        {
            if (!ResourceManager.Instance.SpendResource(
                ResourceType.Materials,
                requirement.materials))
                return false;

            visit.fulfilled.Add(requirement.id);

            return true;
        });
    }

    public bool TryAccept()
    {
        if (!CanAct() ||
            definition.requirements.Any(
                r => !r.bonus && !visit.fulfilled.Contains(r.id)))
            return false;

        return Commit(() =>
        {
            var rewards = new List<VipReward>(definition.rewards);

            if (definition.requirements
                .Where(r => r.bonus)
                .All(r => visit.fulfilled.Contains(r.id)))
            {
                rewards.AddRange(definition.bonusRewards);
            }

            rewards.Add(new VipReward
            {
                resource = ResourceType.Reputation,
                amount = vipReputationReward
            });

            foreach (var reward in rewards)
            {
                if (!ResourceManager.Instance.AddResource(
                    reward.resource,
                    reward.amount))
                    return false;
            }

            visit.waiting = false;
            visit.rewarded = true;

            return true;
        });
    }

    public bool TryMaybeLater()
    {
        return CanAct();
    }

    public bool TryDecline()
    {
        if (!CanAct())
            return false;

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

    private bool Commit(
        Func<bool> change,
        Action afterSave = null)
    {
        if (transaction ||
            ResourceManager.Instance == null ||
            GamePersistence.Instance == null ||
            !GamePersistence.Instance.IsReady)
            return false;

        transaction = true;

        string previous = CaptureSaveJson();

        var resources =
            ResourceManager.Instance.CaptureResources();

        bool committed = false;

        try
        {
            if (!change() ||
                !GamePersistence.Instance.SaveNow())
            {
                LastError =
                    "VIP action could not be completed or saved.";

                return false;
            }

            committed = true;
            LastError = null;
        }
        finally
        {
            if (!committed)
            {
                visit =
                    JsonUtility.FromJson<VipVisitSave>(previous);

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
        return JsonUtility.ToJson(visit);
    }

    public void RestoreSaveJson(string json)
    {
        var saved = JsonUtility.FromJson<VipVisitSave>(json);

        if (saved == null ||
            saved.afterWave < 0 ||
            saved.fulfilled == null ||
            saved.fulfilled.Any(string.IsNullOrWhiteSpace) ||
            saved.fulfilled.Distinct().Count() != saved.fulfilled.Count ||
            (saved.waiting &&
             (!saved.offered ||
              saved.rewarded ||
              string.IsNullOrWhiteSpace(saved.guestId))))
        {
            throw new ArgumentException("Invalid saved VIP visit.");
        }

        visit = saved;
    }
}