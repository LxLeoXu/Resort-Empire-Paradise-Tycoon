using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Synthetic objects only. No persistence instance, scene edits or player save IO.
public static class VipGemsTests
{
    private static int passed;
    private static void Check(string name, bool value)
    {
        if (!value) throw new InvalidOperationException("VIP/GEMS FAIL: " + name);
        passed++;
    }
    public static void RunLogic()
    {
        passed = 0;
        var definition = ScriptableObject.CreateInstance<VipGuestDefinition>();
        try
        {
            for (int i = 0; i < 4; i++) definition.requirements.Add(new VipRequirement {
                id = "r" + i, description = "Request " + i, bonus = i == 3, materials = 10 });
            definition.rewards.Add(new VipReward { resource = ResourceType.Coins, amount = 300 });
            definition.rewards.Add(new VipReward { resource = ResourceType.Reputation, amount = 5 });
            definition.bonusRewards.Add(new VipReward { resource = ResourceType.Coins, amount = 100 });
            var done = new List<string>();
            for (int i = 0; i <= 3; i++)
            {
                var result = VipStayResult.Create(definition, done, false);
                Check("Rating " + i, result.IsValid() && result.stars == i + 1 && !result.perfectStay);
                Check("Decline does not pay " + i, result.rewards.coins == 0 && result.rewards.reputation == 0);
                if (i < 3) done.Add("r" + i);
            }
            var normal = VipStayResult.Create(definition, done, true);
            Check("Mandatory rewards", normal.rewards.coins == 300 && normal.rewards.reputation == 5);
            done.Add("r3");
            var perfect = VipStayResult.Create(definition, done, true);
            Check("Perfect without Energy", perfect.IsValid() && perfect.stars == 5 && perfect.perfectStay && perfect.rewards.energy == 0);
            Check("Bonus rewards", perfect.rewards.coins == 400 && perfect.rewards.reputation == 5);
            Check("VIP never grants ordinary Gems", perfect.rewards.gems == 0 && !perfect.rewards.firstFiveStarBonus);
            Check("Four factual requirements", perfect.requirements.Count == 4 && perfect.requirements.TrueForAll(x => x.StartsWith("[x]")));
            done.Remove("r0");
            var partial = VipStayResult.Create(definition, done, false);
            Check("Bonus cannot replace mandatory", partial.stars == 3 && !partial.perfectStay);
            bool rejected = false;
            try { VipStayResult.Create(definition, done, true); } catch (InvalidOperationException) { rejected = true; }
            Check("Incomplete acceptance rejected", rejected);
            perfect.rewards.materials = -1;
            Check("Negative rewards rejected", !perfect.IsValid());
            var reward = new WaveRewardsData(1, 2, 3, 4, 5, 10, true);
            var copy = reward.Copy(); copy.gems = 20;
            Check("Independent reward snapshot", reward.gems == 10 && copy.gems == 20);
            reward.gems = 0; Check("Invalid milestone rejected", !reward.IsValid());
            Check("Legacy constructor", new WaveRewardsData(1, 2, 3, 4).IsValid());
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
        Debug.Log("VIP/GEMS LOGIC PASS: " + passed + " checks.");
    }
#if UNITY_EDITOR
    [MenuItem("Tools/Tests/VIP Results and one-time Gems")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ResourceManager.Instance != null || GamePersistence.Instance != null)
            throw new InvalidOperationException("Run in Edit Mode without a live game session.");
        RunLogic();
        var go = new GameObject("Isolated VIP Gems test") { hideFlags = HideFlags.HideAndDontSave };
        var instance = typeof(ResourceManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            var manager = go.AddComponent<ResourceManager>(); instance.SetValue(null, manager);
            manager.RestoreResources(manager.CaptureStartingResources());
            Check("New game Gems zero", manager.GetResource(ResourceType.Gems) == 0);
            Check("Four stars no Gems", manager.GetFirstFiveStarGemReward(1, 4, false) == 0);
            Check("Preview no Gems", manager.GetFirstFiveStarGemReward(0, 5, false) == 0);
            Check("Pending bonus not duplicated", manager.GetFirstFiveStarGemReward(2, 5, true) == 0);
            int bonus = manager.GetFirstFiveStarGemReward(1, 5, false);
            Check("Configurable positive bonus", bonus > 0);
            var claim = go.AddComponent<ClaimRewards>();
            claim.SetRewardsForWave(1, new WaveRewardsData(0, 0, 0, gems: bonus, firstFiveStarBonus: true));
            var saved = JsonUtility.FromJson<RewardSave>(JsonUtility.ToJson(claim.CaptureState()));
            claim.RestoreState(saved);
            Check("Pending bonus survives JSON", claim.HasPendingFirstFiveStarBonus && manager.GetResource(ResourceType.Gems) == 0);
            int notifications = 0;
            manager.OnResourceChanged += (type, value) => { if (type == ResourceType.Gems) { notifications++; claim.Claim(); } };
            claim.Claim(); claim.Claim();
            Check("Claim once including reentry", manager.GetResource(ResourceType.Gems) == bonus && notifications == 1);
            Check("Flag committed", manager.FirstFiveStarRewardClaimed);
            manager.RestoreFirstFiveStarReward(JsonUtility.FromJson<FirstFiveStarRewardSave>(JsonUtility.ToJson(manager.CaptureFirstFiveStarReward())));
            Check("Flag survives JSON", manager.GetFirstFiveStarGemReward(2, 5, false) == 0);
            claim.RestoreState(saved); claim.Claim();
            Check("Old pending cannot repay milestone", manager.GetResource(ResourceType.Gems) == bonus);
            manager.RestoreFirstFiveStarReward(null);
            Check("Legacy wallet preserved", manager.GetResource(ResourceType.Gems) == bonus && manager.CaptureFirstFiveStarReward().legacyHistoryUnknown);
            Check("Legacy history blocks duplicate", manager.GetFirstFiveStarGemReward(3, 5, false) == 0);
            var old = JsonUtility.FromJson<WaveRewardsData>("{\"coins\":100,\"energy\":0,\"resortValue\":5,\"materials\":2}");
            Check("Legacy reward JSON accepted", old.IsValid() && old.gems == 0 && !old.firstFiveStarBonus);
            manager.RestoreResources(new List<ResourceSave> { new ResourceSave { type = ResourceType.Coins, amount = int.MaxValue } });
            Check("Overflow is atomic", !ClaimRewards.TryGrantResources(manager, new WaveRewardsData(1, 0, 0, reputation: 5)) && manager.GetResource(ResourceType.Reputation) == 0);
            Check("Negative credit rejected", !ClaimRewards.TryGrantResources(manager, new WaveRewardsData(-1, 0, 0)));
            Debug.Log("VIP/GEMS EDITOR PASS: " + passed + " checks. No player saves touched.");
        }
        finally { instance.SetValue(null, null); UnityEngine.Object.DestroyImmediate(go); }
    }
#endif
}
