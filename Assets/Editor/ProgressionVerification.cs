using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ProgressionVerification
{
    private const string Key = "ProgressionVerification.";
    private static double deadline;
    private static int checkedReadyFrames;
    static ProgressionVerification()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        GamePersistence.SaveDirectoryOverride = SessionState.GetString(Key + "directory", "");
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Tests/Daily reward, clock and bottleneck logic")]
    public static void VerifyLogic()
    {
        var today = new DateTime(2026, 11, 19);
        var calendar = new DailyRewardCalendar();
        Require(calendar.Observe(today) && calendar.CanClaim(today), "First day reward available");
        calendar.lastClaimDay = DailyRewardCalendar.Day(today);
        Require(!calendar.CanClaim(today), "Same day cannot claim twice");
        Require(!calendar.CanClaim(today.AddDays(-1)), "Backdating denied");
        calendar.Observe(today.AddDays(7));
        Require(!calendar.CanClaim(today.AddDays(6)), "Seeing a future date blocks earlier days even without claiming");
        calendar.lastClaimDay = DailyRewardCalendar.Day(today.AddDays(7));
        Require(!calendar.CanClaim(today) && !calendar.CanClaim(today.AddDays(7)) && calendar.CanClaim(today.AddDays(8)), "Forward and back lock persists until a new eligible day");
        var loaded = JsonUtility.FromJson<DailyRewardCalendar>(JsonUtility.ToJson(calendar));
        Require(loaded.IsValid() && !loaded.CanClaim(today), "Calendar JSON round trip preserves lock");
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        try
        {
            var wave = new WaveSession(catalog);
            wave.StartWave(90);
            Require(wave.GetRecommendedNeed() == NeedType.None, "No bottleneck means no recommendation");
            wave.UpdateBottleneck(NeedType.Rooms, 6, 1, 3, 2);
            Require(!wave.IsBottleneck(NeedType.Rooms), "Warning delay respected");
            wave.UpdateBottleneck(NeedType.Rooms, 6, 1, 3, 2);
            Require(wave.IsBottleneck(NeedType.Rooms) && wave.GetBottleneckDuration(NeedType.Rooms) == 1, "Bottleneck time starts after delay");
            wave.UpdateBottleneck(NeedType.Food, 6, 4, 3, 0);
            wave.UpdateBottleneck(NeedType.Rooms, 6, 2, 3, 0);
            Require(wave.GetRecommendedNeed() == NeedType.Food, "Longest cumulative bottleneck wins");
            wave.UpdateBottleneck(NeedType.Rooms, 100, 0, 3, 0);
            Require(wave.GetBottleneckDuration(NeedType.Rooms) == 3, "Pause never advances bottleneck timer");
            var snapshot = JsonUtility.FromJson<WaveSave>(JsonUtility.ToJson(wave.CaptureState()));
            var restored = new WaveSession(catalog); restored.RestoreState(snapshot);
            Require(restored.GetRecommendedNeed() == NeedType.Food && restored.GetBottleneckDuration(NeedType.Rooms) == 3, "Saved bottleneck totals restored");
            restored.RestoreState(new WaveSave { number = 1 }); restored.StartWave(90);
            Require(restored.GetRecommendedNeed() == NeedType.None, "New wave clears recommendation");
            restored.UpdateBottleneck(NeedType.Drinks, 7, 5, 3, 0);
            NeedType published = NeedType.None;
            restored.RecommendationReady += need => published = need;
            restored.NeedArrived += need => { restored.BeginService(need.Id); restored.ResolveNeed(need.Id); };
            restored.Tick(90);
            Require(restored.State == WaveState.Result && published == NeedType.Drinks, "Completed wave publishes recommendation for Needs improvement");
            Require(!BottleneckSave.AreValid(new() { new BottleneckSave { type = NeedType.Rooms, duration = float.NaN } }), "Invalid bottleneck save rejected");
            var blogger = AssetDatabase.LoadAssetAtPath<VipGuestDefinition>("Assets/Data/VIP/TravelBlogger.asset");
            Require(blogger != null && blogger.IsValid() && blogger.requirements.Count(r => !r.bonus) == 3 && blogger.requirements.Count(r => r.bonus) == 1 && blogger.requirements.All(r => r.materials > 0), "Travel Blogger is configured with 3 material requirements and 1 bonus");
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
        Debug.Log("PROGRESSION LOGIC PASSED: daily calendar rollback/forward lock, JSON, bottleneck duration, pause, recovery, reset, Travel Blogger definition.");
    }

    [MenuItem("Tools/Tests/Daily reward, S5 and VIP in scene")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        SessionState.SetBool(Key + "active", true);
        SessionState.SetString(Key + "directory", Path.GetFullPath(Path.Combine("Temp", "ProgressionVerification", Guid.NewGuid().ToString("N"))));
        SessionState.SetString(Key + "stage", "daily");
        SessionState.SetInt(Key + "oldInstance", 0);
        GamePersistence.SaveDirectoryOverride = SessionState.GetString(Key + "directory", "");
        deadline = EditorApplication.timeSinceStartup + 120;
        checkedReadyFrames = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Scene test timed out");
            var persistence = GamePersistence.Instance;
            var daily = UnityEngine.Object.FindFirstObjectByType<DailyRewardController>();
            var vip = UnityEngine.Object.FindFirstObjectByType<VipGuestController>();
            if (persistence == null || !persistence.IsReady || daily == null || !daily.IsReady || vip == null || !vip.IsReady) return;
            if (persistence.GetInstanceID() == SessionState.GetInt(Key + "oldInstance", 0)) return;
            if (++checkedReadyFrames < 3) return;
            Time.timeScale = 0;
            GamePersistence.DisableSaving = false;
            var manager = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            var resources = ResourceManager.Instance;
            var window = UnityEngine.Object.FindFirstObjectByType<DailyRewardWindow>(FindObjectsInactive.Include);
            var upgrade = UnityEngine.Object.FindFirstObjectByType<UpgradeModalUI>(FindObjectsInactive.Include);
            var today = DateTime.Today;
            string stage = SessionState.GetString(Key + "stage", "");
            if (stage == "daily")
            {
                Require(window != null && window.IsOpen, "First open shows the existing daily reward prefab");
                daily.DateOverride = () => today;
                int coins = resources.GetResource(ResourceType.Coins), energy = resources.GetResource(ResourceType.Energy);
                window.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
                Require(resources.GetResource(ResourceType.Coins) == coins + 100 && !daily.TryClaim(), "Actual Claim button grants reward once");
                daily.DateOverride = () => today.AddDays(7); daily.EvaluateDay();
                Require(resources.GetResource(ResourceType.Energy) == energy, "Changing phone date never changes energy");
                coins = resources.GetResource(ResourceType.Coins);
                GamePersistence.DisableSaving = true;
                Require(!daily.TryClaim() && resources.GetResource(ResourceType.Coins) == coins, "Failed daily save rolls back credit");
                GamePersistence.DisableSaving = false;
                Require(daily.TryClaim(), "Future date may claim once");
                daily.DateOverride = () => today; daily.EvaluateDay();
                Require(!daily.TryClaim() && !window.IsOpen, "Rollback blocks reward and hides an invalid offer");
                daily.DateOverride = () => today.AddDays(7); Require(!daily.TryClaim(), "Previously claimed future day still blocked");
                daily.DateOverride = () => today.AddDays(8); daily.EvaluateDay(); Require(daily.TryClaim(), "Next unclaimed day available");
                manager.Session.RestoreState(new WaveSave { number = 3, state = WaveState.Result });
                int arrivals = 0; vip.GuestArrived += _ => arrivals++;
                Require(vip.CheckArrival() && vip.IsWaiting && arrivals == 1 && !vip.CheckArrival(), "VIP arrives after configured wave exactly once");
                upgrade.OpenRecommended();
                Require(upgrade.gameObject.activeInHierarchy && upgrade.TargetBuilding == null, "S5 opens with no recommendation when wave has no bottleneck");
                var s5 = new SerializedObject(upgrade);
                var heading = (TMPro.TextMeshProUGUI)s5.FindProperty("windowTitle").objectReferenceValue;
                var nextLabel = (TMPro.TextMeshProUGUI)s5.FindProperty("laterButtonLabel").objectReferenceValue;
                var nextButton = (UnityEngine.UI.Button)s5.FindProperty("laterButton").objectReferenceValue;
                var upgradeAction = (UnityEngine.UI.Button)s5.FindProperty("upgradeButton").objectReferenceValue;
                Require(heading.text == "NO UPGRADE NEEDED" && nextLabel.text == "CONTINUE" && !upgradeAction.gameObject.activeInHierarchy,
                    "Empty S5 has correct title, Continue and no Upgrade button");
                Require(!upgrade.transform.Find("Window/BuildingCard").gameObject.activeSelf && !upgrade.transform.Find("Window/Description").gameObject.activeSelf &&
                    upgrade.transform.Find("Window/NoUpgradeContent").gameObject.activeSelf, "Empty S5 hides all stale card, comparison and cost content");
                Canvas.ForceUpdateCanvases();
                var s5Window = (RectTransform)s5.FindProperty("windowRect").objectReferenceValue;
                var messageRect = (RectTransform)upgrade.transform.Find("Window/NoUpgradeContent");
                var message = messageRect.GetComponent<TMPro.TMP_Text>();
                Require(message.text == "All buildings kept up with demand this wave.", "Smooth wave explains why no upgrade is needed");
                var headerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(s5Window, heading.rectTransform);
                var messageBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(s5Window, messageRect);
                var buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(s5Window, nextButton.transform);
                Require(headerBounds.min.y >= messageBounds.max.y && messageBounds.min.y >= buttonBounds.max.y && buttonBounds.min.y >= s5Window.rect.yMin,
                    $"Empty S5 geometry: header={headerBounds}, message={messageBounds}, button={buttonBounds}, window={s5Window.rect}");
                nextButton.onClick.Invoke();
                Require(!upgrade.gameObject.activeInHierarchy, "Continue closes empty S5");
                manager.Session.RestoreState(new WaveSave { number = 3, state = WaveState.Running });
                manager.Session.UpdateBottleneck(NeedType.Food, 10, 5, 3, 0);
                manager.Session.UpdateBottleneck(NeedType.Rooms, 20, 2, 3, 0);
                var state = manager.Session.CaptureState(); state.state = WaveState.Result; manager.Session.RestoreState(state);
                upgrade.GetComponent<ModalWindow>().Close();
                var claim = UnityEngine.Object.FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
                Require(claim.SetRewardsForWave(3, new WaveRewardsData(1, 0, 0)), "S4 reward prepared");
                claim.Claim();
                Require(upgrade.gameObject.activeInHierarchy && upgrade.TargetBuilding != null && upgrade.TargetBuilding.offeredNeed == NeedType.Food, "Actual S4 Claim automatically opens S5 for the longest bottleneck");
                Require(heading.text == "UPGRADE AVAILABLE!" && nextLabel.text == "LATER" && upgradeAction.gameObject.activeInHierarchy &&
                    upgrade.transform.Find("Window/BuildingCard").gameObject.activeSelf && !messageRect.gameObject.activeSelf,
                    "Normal S5 presentation is restored after an empty result");
                var restaurant = upgrade.TargetBuilding;
                int previousLevel = restaurant.currentLevel;
                restaurant.currentLevel = BuildingInfo.MaxLevel;
                upgrade.OpenRecommended();
                Require(upgrade.TargetBuilding == restaurant && heading.text == "MAX LEVEL REACHED" &&
                    message.gameObject.activeInHierarchy && message.text == $"{restaurant.buildingName} is already at its maximum level." &&
                    !upgradeAction.gameObject.activeInHierarchy && !upgrade.transform.Find("Window/Description").gameObject.activeInHierarchy &&
                    nextLabel.text == "CONTINUE", "Max-level bottleneck shows the building name and Continue without a price or purchase");
                nextButton.onClick.Invoke();
                Require(!upgrade.gameObject.activeInHierarchy, "Continue closes the max-level result");
                restaurant.currentLevel = previousLevel;
                upgrade.OpenRecommended();
                var cost = (TMPro.TMP_Text)s5.FindProperty("costText").objectReferenceValue;
                Require(cost.gameObject.activeInHierarchy && cost.text == restaurant.UpgradeCost.ToString() &&
                    upgradeAction.gameObject.activeInHierarchy && !message.gameObject.activeInHierarchy,
                    "An upgradeable bottleneck shows the actual price after a max-level result");
                var requirement = vip.Definition.requirements[0];
                int materials = resources.GetResource(ResourceType.Materials);
                Require(!vip.TryAccept(), "VIP cannot finish without requirements");
                Require(vip.TryFulfillRequirement(requirement.id) && resources.GetResource(ResourceType.Materials) == materials - requirement.materials, "VIP requirement consumes materials once");
                Require(!vip.TryFulfillRequirement(requirement.id) && !vip.TryFulfillRequirement("unknown"), "Duplicate/unknown VIP requirement rejected");
                SessionState.SetInt(Key + "materials", resources.GetResource(ResourceType.Materials));
                Require(persistence.SaveNow(), "Pending VIP saved with daily calendar and recommendation");
                Next("restored", persistence);
            }
            else if (stage == "restored")
            {
                Require(!daily.IsAvailable && !daily.TryClaim() && daily.LatestSeenDay == DailyRewardCalendar.Day(today.AddDays(8)), "Restart preserves future-date lock");
                Require(vip.IsWaiting && vip.CurrentVisit.fulfilled.Count == 1, "Pending VIP and paid requirement restored");
                Require(manager.Session.GetRecommendedNeed() == NeedType.Food, "Completed-wave recommendation restored");
                Require(manager.Session.CanStartWave, "Waiting VIP does not block the next wave");
                manager.StartWave();
                Require(manager.Session.State == WaveState.Running && vip.IsWaiting, "Ordinary wave starts while VIP waits");
                var remaining = vip.Definition.requirements[1];
                int materials = resources.GetResource(ResourceType.Materials);
                GamePersistence.DisableSaving = true;
                Require(!vip.TryFulfillRequirement(remaining.id) && resources.GetResource(ResourceType.Materials) == materials && vip.CurrentVisit.fulfilled.Count == 1, "Failed VIP save rolls back material spend and progress");
                GamePersistence.DisableSaving = false;
                foreach (var r in vip.Definition.requirements.Where(r => !r.bonus).Skip(1)) Require(vip.TryFulfillRequirement(r.id), "Mandatory VIP requirement fulfilled");
                var bonus = vip.Definition.requirements.Single(r => r.bonus);
                materials = resources.GetResource(ResourceType.Materials);
                Require(!vip.TryFulfillRequirement(bonus.id) && resources.GetResource(ResourceType.Materials) == materials, "Missing materials do not change VIP progress");
                resources.AddResource(ResourceType.Materials, bonus.materials);
                Require(vip.TryFulfillRequirement(bonus.id), "Optional bonus paid");
                int coins = resources.GetResource(ResourceType.Coins);
                Require(vip.TryAccept() && resources.GetResource(ResourceType.Coins) == coins && !vip.IsWaiting && vip.HasPendingResult, "VIP result prepared without credit");
                SessionState.SetInt(Key + "beforeVipClaim", coins);
                Next("vip-result", persistence);
            }
            else if (stage == "vip-result")
            {
                int coins = SessionState.GetInt(Key + "beforeVipClaim", -1);
                Require(vip.HasPendingResult && vip.PendingResult.perfectStay && vip.PendingResult.stars == 5 && resources.GetResource(ResourceType.Coins) == coins, "Unclaimed S8 survives restart without payout");
                GamePersistence.DisableSaving = true;
                Require(!vip.TryClaimResult() && vip.HasPendingResult && resources.GetResource(ResourceType.Coins) == coins, "Failed VIP claim save rolls back rewards");
                GamePersistence.DisableSaving = false;
                Require(vip.TryClaimResult() && resources.GetResource(ResourceType.Coins) == coins + 400, "VIP base and optional bonus rewards claimed");
                Require(!vip.TryClaimResult(), "Repeated VIP Claim rejected");
                Require(!vip.TryAccept() && !vip.CheckArrival(), "VIP rewards/arrival cannot repeat");
                SessionState.SetInt(Key + "coins", resources.GetResource(ResourceType.Coins));
                Next("finished", persistence);
            }
            else if (stage == "finished")
            {
                Require(!vip.IsWaiting && vip.CurrentVisit.rewarded && !vip.CheckArrival() && !vip.TryAccept() && !vip.TryClaimResult(), "Restart never repeats a completed VIP");
                Require(resources.GetResource(ResourceType.Coins) == SessionState.GetInt(Key + "coins", -1), "VIP credit persists exactly once");
                var seeded = vip.CurrentVisit; seeded.waiting = true; seeded.rewarded = false; seeded.fulfilled.Clear(); seeded.result = null;
                vip.RestoreSaveJson(JsonUtility.ToJson(seeded));
                int coins = resources.GetResource(ResourceType.Coins);
                Require(vip.TryDecline() && !vip.IsWaiting && !vip.CheckArrival() && resources.GetResource(ResourceType.Coins) == coins, "Player may decline VIP without expiry or extra reward");
                var store = new GameSaveStore(GamePersistence.SaveDirectoryOverride);
                Require(store.TryLoad(out var saved) && saved.extensions.Any(e => e.key == daily.SaveKey) && saved.extensions.Any(e => e.key == vip.SaveKey), "Shared JSON contains both progression modules");
                Debug.Log("PROGRESSION SCENE PASSED: actual daily prefab/Claim, date rollback, no energy regeneration, save failure rollback, S5 target/empty state, VIP materials/bonus/decline, continued waves, restart persistence.");
                Finish();
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(); }
    }
    private static void Next(string stage, GamePersistence persistence)
    {
        SessionState.SetString(Key + "stage", stage); SessionState.SetInt(Key + "oldInstance", persistence.GetInstanceID());
        checkedReadyFrames = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    private static void Finish()
    {
        GamePersistence.DisableSaving = false;
        SessionState.SetBool(Key + "active", false); EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Progression test failed: " + message); }
}
