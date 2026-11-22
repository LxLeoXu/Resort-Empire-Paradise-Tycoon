using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

// Edit Mode only. Synthetic objects, no scene writes, no save store or player save access.
public static class Sprint3RegressionTests
{
    private static readonly List<GameObject> objects = new();
    private static int passed;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static void StaticProperty(Type type, string property, object value) =>
        type.GetField("<" + property + ">k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
    private static void Check(string label, bool value)
    {
        if (!value) throw new InvalidOperationException("SPRINT 3 FAIL: " + label);
        passed++;
    }
    private static GameObject New(string name)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false); objects.Add(go); return go;
    }
    // Separate batch process only; creates an unsaved empty scene, never edits SampleScene.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch entry point only.");
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        Run();
    }
    [MenuItem("Tools/Tests/Sprint 3 Integration Regression")]
    public static void RunMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("SPRINT 3: Stop Play Mode before running the tests.");
            return;
        }

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool emptyScene = UnityEngine.SceneManagement.SceneManager.sceneCount == 1 &&
                          activeScene.IsValid() && activeScene.rootCount == 0;
        if (emptyScene && ResourceManager.Instance == null && GamePersistence.Instance == null &&
            UnityEngine.Object.FindObjectsByType<S4FigmaResultsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
        {
            Run();
            return;
        }

        var sceneSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        foreach (var scene in sceneSetup)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("SPRINT 3: An unsaved scene cannot be restored safely. Save it or reopen a saved scene first.");
                return;
            }
        }
        foreach (var scene in sceneSetup)
        {
            if (!scene.isLoaded) continue;
            var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path);
            if (loadedScene.IsValid() && loadedScene.isDirty)
            {
                Debug.LogError("SPRINT 3: Save the current scene (Ctrl+S) before testing. No scene was changed.");
                return;
            }
        }

        try
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Run();
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ResourceManager.Instance != null || GamePersistence.Instance != null ||
            UnityEngine.Object.FindObjectsByType<S4FigmaResultsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Use Edit Mode and an empty temporary scene; no live game singletons or S4 views allowed.");
        passed = 0;
        bool oldDisableSaving = GamePersistence.DisableSaving;
        bool oldRestoring = GamePersistence.IsRestoring;
        bool oldPaused = GamePersistence.IsPaused;
        VipGuestDefinition definition = null;
        try
        {
            StaticProperty(typeof(GamePersistence), "IsRestoring", false);
            StaticProperty(typeof(GamePersistence), "IsPaused", false);
            var manager = New("Isolated resources").AddComponent<ResourceManager>();
            StaticProperty(typeof(ResourceManager), "Instance", manager);
            manager.RestoreResources(manager.CaptureStartingResources());
            int[] satisfaction = { 0, 55, 70, 80, 90 };
            int[] energy = { 0, 1, 2, 3, 5 };
            for (int i = 0; i < satisfaction.Length; i++)
            {
                var reward = WaveRewardCalculator.CalculateRewards(12, satisfaction[i]);
                Check("Stars " + (i + 1), WaveRewardCalculator.GetStarRating(satisfaction[i]) == i + 1);
                Check("Energy table " + (i + 1), reward.energy == energy[i]);
                Check("Shared Coins calculation " + (i + 1), reward.coins == WaveRewardCalculator.CalculateCoins(12, satisfaction[i]));
            }
            foreach (int balance in new[] { 0, 48, 49, 50 })
            {
                manager.RestoreResources(new List<ResourceSave> { new ResourceSave { type = ResourceType.Energy, amount = balance } });
                Check("Energy credit accepted at " + balance, ClaimRewards.TryGrantResources(manager, new WaveRewardsData(0, 5, 0)));
                Check("Energy cap at " + balance, manager.GetResource(ResourceType.Energy) == Math.Min(50, balance + 5));
            }
            var invalid = new RewardSave { prepared = true, waveId = 1, rewards = new WaveRewardsData(0, 0, 0, gems: -1) };
            Check("Negative saved Gems rejected", !invalid.IsValid());
            invalid.rewards.gems = 0; invalid.rewards.firstFiveStarBonus = true;
            Check("Zero milestone rejected", !invalid.IsValid());
            invalid.rewards.gems = 10; Check("Valid milestone accepted", invalid.IsValid());

            manager.RestoreResources(manager.CaptureStartingResources());
            var claim = New("Ordinary claim").AddComponent<ClaimRewards>();
            claim.SetRewardsForWave(1, new WaveRewardsData(100, 5, 10, gems: 10, firstFiveStarBonus: true));
            var view = New("Ordinary S4").AddComponent<S4FigmaResultsUI>();
            var breakdown = New("Coins breakdown").AddComponent<TextMeshProUGUI>();
            var gems = New("Gems row").AddComponent<TextMeshProUGUI>();
            Set(view, "claimRewards", claim); Set(view, "coinsBreakdownText", breakdown); Set(view, "gemsRewardText", gems);
            breakdown.gameObject.SetActive(true); gems.gameObject.SetActive(true);
            string pending = JsonUtility.ToJson(claim.CaptureState());
            int coinsBefore = manager.GetResource(ResourceType.Coins);
            view.ShowVipResults(3, 4, 100, 5, new List<VipReward>());
            Check("VIP fallback hides ordinary rows", !breakdown.gameObject.activeSelf && !gems.gameObject.activeSelf);
            claim.Claim();
            Check("Fallback Claim preserves pending reward", JsonUtility.ToJson(claim.CaptureState()) == pending);
            Check("Fallback Claim does not pay", manager.GetResource(ResourceType.Coins) == coinsBefore && manager.GetResource(ResourceType.Gems) == 0);
            view.ShowWaveResults(1, 12, 12, 90, restoreExistingReward: true);
            Check("Ordinary rows restored", breakdown.gameObject.activeSelf && gems.gameObject.activeSelf);

            // The production overlap filter only hides visible, discoverable S4 views.
            // Synthetic objects created by New() start inactive and HideAndDontSave.
            // Make ONLY this test view discoverable in the temporary test scene.
            view.gameObject.hideFlags = HideFlags.None;
            view.gameObject.SetActive(true);
            if (!view.gameObject.activeInHierarchy ||
                Array.IndexOf(UnityEngine.Object.FindObjectsByType<S4FigmaResultsUI>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None), view) < 0)
                throw new InvalidOperationException("SPRINT 3 TEST SETUP: Synthetic ordinary S4 is not discoverable.");

            var receipt = New("VIP receipt").AddComponent<S8VipFigmaPanel>();
            Invoke(receipt, "HideOrdinaryWaveResults");
            Check("No overlap hides S4", !view.gameObject.activeSelf);
            Check("No overlap keeps reward", JsonUtility.ToJson(claim.CaptureState()) == pending);
            Invoke(receipt, "RestoreOrdinaryWaveResults");
            Check("S4 accessible after receipt", view.gameObject.activeSelf);

            // Save failure path: saving is disabled before a synthetic coordinator is installed.
            GamePersistence.DisableSaving = true;
            var persistence = New("No IO persistence").AddComponent<GamePersistence>();
            typeof(GamePersistence).GetField("<IsReady>k__BackingField", PrivateInstance).SetValue(persistence, true);
            StaticProperty(typeof(GamePersistence), "Instance", persistence);
            claim.Claim();
            Check("Failed save restores ordinary reward", claim.HasPendingReward && !manager.FirstFiveStarRewardClaimed);
            Check("Failed save restores wallet", manager.GetResource(ResourceType.Coins) == coinsBefore && manager.GetResource(ResourceType.Gems) == 0);
            StaticProperty(typeof(GamePersistence), "Instance", null); // No store exists; isolated Claim test mode.
            claim.Claim(); claim.Claim();
            Check("Retry credits once", manager.GetResource(ResourceType.Coins) == coinsBefore + 100 && manager.GetResource(ResourceType.Gems) == 10);
            Check("Next five stars no bonus", manager.GetFirstFiveStarGemReward(2, 5, false) == 0);
            Check("Milestone JSON preserved", JsonUtility.FromJson<FirstFiveStarRewardSave>(JsonUtility.ToJson(manager.CaptureFirstFiveStarReward())).claimed);

            definition = ScriptableObject.CreateInstance<VipGuestDefinition>();
            for (int i = 0; i < 4; i++) definition.requirements.Add(new VipRequirement { id = "r" + i, description = "Request " + i, bonus = i == 3, materials = 10 });
            var vip = New("Isolated VIP").AddComponent<VipGuestController>(); Set(vip, "definition", definition); Set(vip, "ready", true);
            var visit = new VipVisitSave { offered = true, waiting = true, guestId = definition.guestId, afterWave = 3,
                fulfilled = new List<string> { "r0" }, activeTimers = new List<VipRequirementTimerSave> {
                    new VipRequirementTimerSave { requirementId = "r0", remainingSeconds = 4.5f, durationSeconds = 12f } } };
            vip.RestoreSaveJson(JsonUtility.ToJson(visit));
            string timerSave = vip.CaptureSaveJson(); vip.RestoreSaveJson(timerSave);
            Check("Timer survives JSON restart", vip.GetRemainingSeconds("r0") == 4.5f && vip.GetRequirementState(definition.requirements[0]) == VipRequirementState.InProgress);
            var calendar = new DailyRewardCalendar(); calendar.Observe(new DateTime(2026, 11, 22)); calendar.Observe(new DateTime(2026, 11, 25));
            Check("Offline calendar does not advance timer", vip.GetRemainingSeconds("r0") == 4.5f);
            foreach (float remaining in new[] { -1f, 13f })
            {
                visit.activeTimers[0].remainingSeconds = remaining; bool rejected = false;
                try { vip.RestoreSaveJson(JsonUtility.ToJson(visit)); } catch (ArgumentException) { rejected = true; }
                Check("Invalid timer rejected " + remaining, rejected && vip.GetRemainingSeconds("r0") == 4.5f);
            }
            StaticProperty(typeof(GamePersistence), "Instance", persistence);
            vip.RestoreSaveJson("{\"offered\":true,\"waiting\":true,\"guestId\":\"travel-blogger\",\"afterWave\":3,\"fulfilled\":[\"r0\"]}");
            Check("Legacy timer absence means Ready", vip.GetRequirementState(definition.requirements[0]) == VipRequirementState.Ready);
            Check("Failed Serve All save permits retry", !vip.StartServeAll() && vip.GetRequirementState(definition.requirements[0]) == VipRequirementState.Ready);
            vip.RestoreSaveJson(timerSave);
            Invoke(vip, "CompleteFinishedRequirements", new List<string> { "r0" });
            Check("Failed completion retains timer", vip.GetRequirementState(definition.requirements[0]) == VipRequirementState.InProgress && vip.CurrentVisit.completed.Count == 0);
            Debug.Log("SPRINT 3 REGRESSION PASS: " + passed + " checks. No player save IO.");
        }
        finally
        {
            StaticProperty(typeof(GamePersistence), "Instance", null);
            StaticProperty(typeof(ResourceManager), "Instance", null);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            GamePersistence.DisableSaving = oldDisableSaving;
            StaticProperty(typeof(GamePersistence), "IsRestoring", oldRestoring);
            StaticProperty(typeof(GamePersistence), "IsPaused", oldPaused);
        }
    }
}
