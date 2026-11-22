// Resort Empire Paradise Tycoon – S8 VIP Results (Figma-inspired)
// Put in Assets/Scripts/UI/S8VipFigmaPanel.cs
// In Unity: Tools > Resort Empire > Generate S8 VIP prefab.
// The generated prefab is automatically displayed over the existing S4-based
// VIP result and delegates claims to its existing ClaimRewards component.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class S8VipFigmaPanel : MonoBehaviour
{
    [Header("Generated UI references")]
    public TMP_Text guestHeading;
    public TMP_Text requestsAnswered;
    public TMP_Text stayRating;
    public TMP_Text requirements;
    public TMP_Text[] rewardRows = new TMP_Text[5];
    public Image[] stars = new Image[5];
    public GameObject perfectBanner;
    public GameObject confetti;
    public Button claimButton;
    public TMP_Text previewWatermark;

    private ClaimRewards claimSource;
    private bool preview;
    private bool completedVisitDisplayOnly;
    private static S8VipFigmaPanel previewInstance;
    private bool isRuntimeConnector;
    private float nextCheck;
    private S4FigmaResultsUI activeVip;
    private S8VipFigmaPanel activePanel;

    // Preserve ordinary S4 results and their unclaimed wave rewards while VIP S8 is open.
    private readonly List<GameObject> temporarilyHiddenS4 = new List<GameObject>();
    private float nextS4VisibilityCheck;

    private static readonly Color Purple = new Color32(126, 51, 242, 255);
    private static readonly Color Gold = new Color32(255, 193, 42, 255);
    private static readonly Color MutedStar = new Color32(113, 109, 124, 255);

    private void Awake()
    {
        // Runtime listeners added during prefab generation are not serialized.
        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(OnClaimPressed);
            claimButton.onClick.AddListener(OnClaimPressed);
        }
    }

    private void OnDestroy()
    {
        if (claimButton != null) claimButton.onClick.RemoveListener(OnClaimPressed);
        RestoreOrdinaryWaveResults();
        if (isRuntimeConnector) RemoveActivePanel();
    }

    private void HideOrdinaryWaveResults()
    {
        foreach (var result in FindObjectsByType<S4FigmaResultsUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (result == null || result.IsVipVariant || !result.gameObject.activeInHierarchy)
                continue;

            var ordinaryWindow = result.gameObject;
            if (!temporarilyHiddenS4.Contains(ordinaryWindow))
                temporarilyHiddenS4.Add(ordinaryWindow);

            // Do NOT call HideResults(): it would change S4's result state.
            // Disabling the view does not pay or discard ordinary wave rewards.
            ordinaryWindow.SetActive(false);
        }
    }

    private void RestoreOrdinaryWaveResults()
    {
        foreach (var ordinaryWindow in temporarilyHiddenS4)
            if (ordinaryWindow != null)
                ordinaryWindow.SetActive(true);

        temporarilyHiddenS4.Clear();
    }

    private void LateUpdate()
    {
        // Another script can show a wave result after the VIP receipt opens.
        if (!completedVisitDisplayOnly || Time.unscaledTime < nextS4VisibilityCheck)
            return;

        nextS4VisibilityCheck = Time.unscaledTime + 0.1f;
        HideOrdinaryWaveResults();
    }

    // New S7 path. Rewards have ALREADY been granted and saved by VipGuestController.
    // S8 only displays the receipt and its Claim button closes the overlay.
    public static bool TryShowCompleted(
        VipGuestDefinition guest,
        VipVisitSave visit,
        int satisfactionPercent,
        int rating,
        List<VipReward> grantedRewards)
    {
        if (guest == null || visit == null || grantedRewards == null)
            return false;

        var prefab = Resources.Load<GameObject>("S8VipFigmaPanel");
        if (prefab == null)
        {
            Debug.LogError("S8: Resources/S8VipFigmaPanel.prefab is missing.");
            return false;
        }

        if (prefab.GetComponent<S8VipFigmaPanel>() == null)
        {
            Debug.LogError("S8: The prefab root has no S8VipFigmaPanel component.");
            return false;
        }

        var instance = Instantiate(prefab);
        var panel = instance.GetComponent<S8VipFigmaPanel>();
        panel.DisplayCompletedVisit(guest, visit, satisfactionPercent, rating, grantedRewards);
        panel.HideOrdinaryWaveResults();
        return true;
    }

    public void DisplayCompletedVisit(
        VipGuestDefinition guest,
        VipVisitSave visit,
        int satisfactionPercent,
        int rating,
        List<VipReward> grantedRewards)
    {
        // This mode must NEVER call ClaimRewards or ResourceManager.
        completedVisitDisplayOnly = true;
        preview = false;
        claimSource = null;
        if (previewWatermark != null)
            previewWatermark.gameObject.SetActive(false);

        var completed = new HashSet<string>(visit.completed ?? new List<string>());
        var lines = new List<string>();
        int requiredCompleted = 0;
        int requiredTotal = 0;
        bool bonusCompleted = false;

        foreach (var req in guest.requirements)
        {
            if (req == null) continue;
            bool done = completed.Contains(req.id);
            lines.Add((done ? "[x] " : "[ ] ") +
                (string.IsNullOrWhiteSpace(req.description) ? req.id : req.description));
            if (req.bonus)
                bonusCompleted = done;
            else
            {
                requiredTotal++;
                if (done) requiredCompleted++;
            }
        }

        int coins = 0, energy = 0, materials = 0, resortValue = 0, reputation = 0;
        foreach (var reward in grantedRewards)
        {
            if (reward == null) continue;
            switch (reward.resource)
            {
                case ResourceType.Coins: coins += reward.amount; break;
                case ResourceType.Energy: energy += reward.amount; break;
                case ResourceType.Materials: materials += reward.amount; break;
                case ResourceType.ResortValue: resortValue += reward.amount; break;
                case ResourceType.Reputation: reputation += reward.amount; break;
                default:
                    Debug.LogWarning("S8: Unrepresented reward type: " + reward.resource);
                    break;
            }
        }

        bool perfectStay = requiredTotal > 0 &&
            requiredCompleted == requiredTotal && bonusCompleted;

        Apply(guest.displayName, requiredCompleted, Mathf.Clamp(rating, 1, 5),
            perfectStay, lines, reputation, coins, energy, materials, resortValue);

        if (requestsAnswered != null)
            requestsAnswered.text = requiredCompleted + "/" + requiredTotal + " Served";
        if (stayRating != null)
            stayRating.text = rating + "/5 Stars";
        Debug.Log("[S8] Showing saved VIP result: " + guest.displayName +
            ", satisfaction=" + Mathf.Clamp(satisfactionPercent, 0, 100) +
            "%, stars=" + rating + ". No additional rewards are granted.");
        if (claimButton != null)
            claimButton.interactable = true;
    }

    public void Display(VipStayResult result, ClaimRewards existingClaim)
    {
        if (result == null || !result.IsValid()) return;
        preview = false;
        completedVisitDisplayOnly = false;
        claimSource = existingClaim;
        if (previewWatermark != null) previewWatermark.gameObject.SetActive(false);
        Apply(result.guestName, result.requiredCompleted, result.stars,
            result.perfectStay, result.requirements,
            result.rewards.reputation, result.rewards.coins,
            result.rewards.energy, result.rewards.materials,
            result.rewards.resortValue);
        if (claimButton != null) claimButton.interactable = claimSource != null;
    }

    public void DisplayPreview()
    {
        preview = true;
        completedVisitDisplayOnly = false;
        claimSource = null;
        Apply("Chloe", 3, 5, true,
            new[] { "Required 1: fulfilled", "Required 2: fulfilled", "Required 3: fulfilled", "Bonus: fulfilled" },
            40, 2500, 15, 0, 50);
        if (claimButton != null) claimButton.interactable = false;
        if (previewWatermark != null) previewWatermark.gameObject.SetActive(true);
    }

    public void OnClaimPressed()
    {
        if (preview) return;
        if (completedVisitDisplayOnly)
        {
            // S7 already saved the credit. Just close this receipt.
            completedVisitDisplayOnly = false;
            if (claimButton != null) claimButton.interactable = false;
            RestoreOrdinaryWaveResults();
            Destroy(gameObject);
            return;
        }
        if (claimSource == null)
        {
            Debug.LogError("S8 VIP: Original ClaimRewards was not found. Nothing was claimed.");
            return;
        }
        // The ORIGINAL ClaimRewards handles VIP one-time logic and persistence.
        claimSource.Claim();
    }

    private void Apply(string guest, int answered, int rating, bool perfect,
        IEnumerable<string> requirementLines, int reputation, int coins,
        int energy, int materials, int resortValue)
    {
        if (guestHeading != null)
            guestHeading.text = (string.IsNullOrWhiteSpace(guest) ? "VIP GUEST" : guest.ToUpperInvariant()) + "'S FEEDBACK";
        if (requestsAnswered != null) requestsAnswered.text = answered + "/3 Served";
        // VIP result currently has a star rating, not a separate percentage.
        if (stayRating != null) stayRating.text = rating + "/5 Stars";
        if (requirements != null)
            requirements.text = requirementLines == null ? "" : string.Join("\n", requirementLines);
        if (perfectBanner != null) perfectBanner.SetActive(perfect);
        if (confetti != null) confetti.SetActive(perfect);
        if (stars != null)
            for (int i = 0; i < stars.Length; ++i)
                if (stars[i] != null) stars[i].color = i < rating ? Gold : MutedStar;
        if (rewardRows != null && rewardRows.Length >= 5)
        {
            SetRow(0, "+" + reputation.ToString("N0") + " Reputation");
            SetRow(1, "+" + coins.ToString("N0") + " Coins");
            SetRow(2, "+" + energy.ToString("N0") + " Energy");
            SetRow(3, "+" + materials.ToString("N0") + " Materials");
            SetRow(4, "+" + resortValue.ToString("N0") + " Resort Value");
        }
    }

    private void SetRow(int index, string value)
    {
        if (rewardRows[index] != null) rewardRows[index].text = value;
    }

    private void Update()
    {
        if (!isRuntimeConnector || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        if (GamePersistence.IsRestoring || GamePersistence.IsPaused)
        {
            RemoveActivePanel();
            return;
        }

        S4FigmaResultsUI found = null;
        foreach (var v in FindObjectsByType<S4FigmaResultsUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v != null && v.IsVipVariant && v.gameObject.activeInHierarchy)
            {
                found = v;
                break;
            }
        }
        if (found == null) { RemoveActivePanel(); return; }

        VipGuestController owner = null;
        foreach (var controller in FindObjectsByType<VipGuestController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (controller != null && controller.IsReady && controller.HasPendingResult &&
                controller.PendingResult != null && controller.PendingResult.IsValid())
            {
                owner = controller;
                break;
            }
        }
        if (owner == null) { RemoveActivePanel(); return; }

        if (activePanel == null || found != activeVip)
        {
            RemoveActivePanel();
            var prefab = Resources.Load<S8VipFigmaPanel>("S8VipFigmaPanel");
            if (prefab == null) return; // Existing S4 VIP UI stays available.
            activePanel = Instantiate(prefab);
            activeVip = found;
        }
        // This is the exact ClaimRewards already bound to the VIP controller.
        activePanel.Display(owner.PendingResult, found.S8_GetBoundClaimRewards());
    }

    private void RemoveActivePanel()
    {
        if (activePanel != null) Destroy(activePanel.gameObject);
        activePanel = null;
        activeVip = null;
    }

    // Preview is available only in the Editor.
#if UNITY_EDITOR
    [MenuItem("Tools/Resort Empire/Preview S8 VIP (Play Mode)")]
    private static void Preview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Run Play Mode before opening S8 preview.");
            return;
        }
        if (previewInstance != null)
        {
            Destroy(previewInstance.gameObject);
            previewInstance = null;
            return;
        }
        var prefab = Resources.Load<S8VipFigmaPanel>("S8VipFigmaPanel");
        if (prefab == null)
        {
            Debug.LogError("S8 prefab missing. Run Tools > Resort Empire > Generate S8 VIP prefab.");
            return;
        }
        previewInstance = Instantiate(prefab);
        previewInstance.DisplayPreview();
    }
#endif

    // No polling connector is installed: S7 explicitly opens the S8 receipt
    // after a successful saved transaction via TryShowCompleted().

#if UNITY_EDITOR
    // -----------------------------------------------------------------------
    // Editor generator: creates a real, editable prefab from UI components.
    // Does not edit S4, S6, S7, VipGuestController or ClaimRewards.
    // -----------------------------------------------------------------------
    [MenuItem("Tools/Resort Empire/Generate S8 VIP prefab")]
    private static void GeneratePrefab()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Generate the S8 prefab outside Play Mode.");
            return;
        }
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Art/UI/VIP");
        var rounded = EnsureSprite("Assets/Art/UI/VIP/S8_Generated_Rounded.png", false);
        var star = EnsureSprite("Assets/Art/UI/VIP/S8_Generated_Star.png", true);
        if (rounded == null || star == null)
        {
            Debug.LogError("S8: Could not import generated sprites.");
            return;
        }

        var root = new GameObject("S8_VIPResults", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(S8VipFigmaPanel));
        try
        {
            var view = root.GetComponent<S8VipFigmaPanel>();
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            Stretch((RectTransform)root.transform);

            var background = Picture(root.transform, "Overlay – resort visible underneath",
                Vector2.zero, Vector2.one, new Color(0.01f, .11f, .15f, .73f));
            background.raycastTarget = true; // stop taps reaching old S4 UI
            var stage = Picture(background.transform, "VIP translucent background",
                new Vector2(.037f, .065f), new Vector2(.963f, .94f),
                new Color(.04f, .24f, .28f, .55f), rounded);

            var confettiRoot = new GameObject("Perfect Stay Confetti", typeof(RectTransform));
            confettiRoot.transform.SetParent(stage.transform, false);
            Stretch((RectTransform)confettiRoot.transform);
            var rng = new System.Random(311);
            Color[] confettiColors = { Gold, Purple, new Color32(5,184,157,255),
                new Color32(21,159,226,255), new Color32(242,76,130,255), new Color32(236,97,37,255) };
            for (int i = 0; i < 52; ++i)
            {
                float x = (float)rng.NextDouble();
                float y = (float)rng.NextDouble();
                var piece = Picture(confettiRoot.transform, "Confetti " + i,
                    new Vector2(x, y), new Vector2(x, y), confettiColors[i % confettiColors.Length]);
                var r = piece.rectTransform;
                r.sizeDelta = new Vector2(rng.Next(8, 18), rng.Next(8, 19));
                r.localRotation = Quaternion.Euler(0, 0, rng.Next(0, 180));
            }
            view.confetti = confettiRoot;

            var banner = Picture(stage.transform, "Perfect Stay Banner",
                new Vector2(.327f,.905f), new Vector2(.673f,.975f), Purple, rounded);
            MakeText(banner.transform, "Banner Text", "PERFECT STAY BONUS UNLOCKED",
                Vector2.zero, Vector2.one, 23, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            view.perfectBanner = banner.gameObject;

            MakeText(stage.transform, "VIP VISIT COMPLETE", "VIP VISIT COMPLETE!",
                new Vector2(.24f,.788f), new Vector2(.76f,.91f),
                45, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            for (int i = 0; i < 5; ++i)
            {
                float left = .387f + i * .049f;
                view.stars[i] = Picture(stage.transform, "Star " + (i+1),
                    new Vector2(left, .775f), new Vector2(left+.043f,.839f), Gold, star);
                view.stars[i].preserveAspect = true;
            }

            var feedbackBorder = Picture(stage.transform, "Feedback Violet Border",
                new Vector2(.104f,.235f), new Vector2(.477f,.717f), Purple, rounded);
            var feedback = Picture(feedbackBorder.transform, "Feedback White Card",
                new Vector2(.006f,.009f), new Vector2(.994f,.991f), Color.white, rounded);
            view.guestHeading = MakeText(feedback.transform, "Guest Feedback", "CHLOE'S FEEDBACK",
                new Vector2(.05f,.81f), new Vector2(.96f,.955f),
                31, Purple, TextAlignmentOptions.Left, FontStyles.Bold);
            MakeText(feedback.transform, "Requests Answered label", "Requests Answered",
                new Vector2(.05f,.65f), new Vector2(.72f,.79f),
                24, new Color32(130,132,136,255), TextAlignmentOptions.Left);
            view.requestsAnswered = MakeText(feedback.transform, "Requests Answered value", "3/3 Served",
                new Vector2(.69f,.65f), new Vector2(.95f,.79f),
                26, new Color32(0,184,133,255), TextAlignmentOptions.Right, FontStyles.Bold);
            MakeText(feedback.transform, "VIP Rating label", "VIP Stay Rating",
                new Vector2(.05f,.525f), new Vector2(.73f,.665f),
                24, new Color32(130,132,136,255), TextAlignmentOptions.Left);
            view.stayRating = MakeText(feedback.transform, "VIP Rating value", "5/5 Stars",
                new Vector2(.7f,.525f), new Vector2(.95f,.665f),
                26, new Color32(0,184,133,255), TextAlignmentOptions.Right, FontStyles.Bold);
            view.requirements = MakeText(feedback.transform, "Real VIP requirements", "Required 1: fulfilled\nRequired 2: fulfilled\nRequired 3: fulfilled\nBonus: fulfilled",
                new Vector2(.057f,.07f), new Vector2(.95f,.525f),
                23, new Color32(87,89,91,255), TextAlignmentOptions.TopLeft);

            var rewardsBorder = Picture(stage.transform, "Rewards Violet Border",
                new Vector2(.527f,.197f), new Vector2(.902f,.765f), Purple, rounded);
            var rewards = Picture(rewardsBorder.transform, "Rewards White Card",
                new Vector2(.006f,.009f), new Vector2(.994f,.991f), Color.white, rounded);
            MakeText(rewards.transform, "Premium Rewards Heading", "PREMIUM REWARDS",
                new Vector2(.055f,.82f), new Vector2(.95f,.96f),
                32, Purple, TextAlignmentOptions.Left, FontStyles.Bold);
            Color[] colors = { new Color32(191,72,16,255),new Color32(0,168,32,255),
                new Color32(8,179,211,255),new Color32(136,78,213,255),new Color32(235,163,4,255) };
            string[] symbols = { "R", "$", "E", "M", "V" };
            string[] rows = { "+40 Reputation", "+2,500 Coins", "+15 Energy", "+0 Materials", "+50 Resort Value" };
            for (int i = 0; i < 5; ++i)
            {
                float top = .805f - i * .142f;
                var medal = Picture(rewards.transform, "Reward icon " + i,
                    new Vector2(.058f,top-.117f), new Vector2(.139f,top-.025f), colors[i], rounded);
                MakeText(medal.transform, "Icon letter", symbols[i], Vector2.zero,Vector2.one,
                    25,Color.white,TextAlignmentOptions.Center,FontStyles.Bold);
                view.rewardRows[i] = MakeText(rewards.transform, "Reward value " + i,
                    rows[i],new Vector2(.166f,top-.124f),new Vector2(.972f,top),
                    27,colors[i],TextAlignmentOptions.Left,FontStyles.Bold);
            }

            var whiteOutline = Picture(stage.transform, "Claim Button White Outline",
                new Vector2(.349f,.038f),new Vector2(.651f,.166f),Color.white,rounded);
            var purpleButton = Picture(whiteOutline.transform, "Claim Rewards Button",
                new Vector2(.01f,.06f),new Vector2(.99f,.94f),Purple,rounded);
            purpleButton.raycastTarget = true;
            var button = purpleButton.gameObject.AddComponent<Button>();
            button.targetGraphic = purpleButton;
            button.transition = Selectable.Transition.ColorTint;
            var tint = button.colors;
            tint.normalColor = Color.white;
            tint.highlightedColor = new Color(.94f,.94f,.94f,1);
            tint.pressedColor = new Color(.68f,.68f,.68f,1);
            tint.selectedColor = Color.white;
            tint.disabledColor = new Color(.45f,.45f,.45f,.5f);
            tint.fadeDuration = .05f;
            button.colors = tint;
            view.claimButton = button;
            MakeText(purpleButton.transform, "Claim Label", "CLAIM REWARDS",
                Vector2.zero,Vector2.one,36,Color.white,
                TextAlignmentOptions.Center,FontStyles.Bold);

            view.previewWatermark = MakeText(stage.transform, "Editor preview watermark",
                "PREVIEW ONLY - no rewards are granted",
                new Vector2(.29f,0f),new Vector2(.71f,.042f),
                17,Gold,TextAlignmentOptions.Center);
            view.previewWatermark.gameObject.SetActive(false);

            const string path = "Assets/Resources/S8VipFigmaPanel.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[S8] Created real prefab: " + path + ". No gameplay scripts were modified.");
        }
        finally { DestroyImmediate(root); }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash < 0) return;
        string parent = path.Substring(0, slash);
        string child = path.Substring(slash + 1);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static Sprite EnsureSprite(string path, bool star)
    {
        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (loaded != null) return loaded;
        const int n = 64;
        var texture = new Texture2D(n,n,TextureFormat.RGBA32,false);
        Color[] pixels = new Color[n*n];
        Vector2[] points = null;
        if (star)
        {
            points = new Vector2[10];
            for (int i = 0; i < 10; ++i)
            {
                float a = (90f + 36f * i)*Mathf.Deg2Rad;
                float r = i%2==0 ? 29f : 13f;
                points[i] = new Vector2(32+Mathf.Cos(a)*r,32+Mathf.Sin(a)*r);
            }
        }
        for (int y = 0; y < n; ++y)
        for (int x = 0; x < n; ++x)
        {
            float a;
            if (star)
            {
                bool inside = false;
                for (int i=0,j=9; i<10; j=i++)
                    if ((points[i].y > y)!=(points[j].y > y) &&
                        x < (points[j].x-points[i].x)*(y-points[i].y)/(points[j].y-points[i].y)+points[i].x)
                        inside = !inside;
                a = inside ? 1f : 0f;
            }
            else
            {
                const float rad = 17f;
                float dx=Mathf.Max(rad-(x+.5f),0f,(x+.5f)-(n-rad));
                float dy=Mathf.Max(rad-(y+.5f),0f,(y+.5f)-(n-rad));
                a=Mathf.Clamp01(rad-Mathf.Sqrt(dx*dx+dy*dy)+.5f);
            }
            pixels[y*n+x]=new Color(1f,1f,1f,a);
        }
        texture.SetPixels(pixels); texture.Apply();
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
        DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spriteBorder=star ? Vector4.zero : new Vector4(18,18,18,18);
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.filterMode=FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Image Picture(Transform parent,string name,Vector2 min,Vector2 max,
        Color color,Sprite sprite=null)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));
        go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();
        rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        var img=go.GetComponent<Image>();
        img.sprite=sprite;
        img.color=color;
        img.type=sprite!=null && sprite.border.sqrMagnitude>0f ? Image.Type.Sliced:Image.Type.Simple;
        img.raycastTarget=false;
        return img;
    }

    private static TMP_Text MakeText(Transform parent,string name,string content,
        Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment,
        FontStyles style=FontStyles.Normal)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
        go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();
        rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        var label=go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset!=null) label.font=TMP_Settings.defaultFontAsset;
        label.text=content;
        label.color=color;
        label.alignment=alignment;
        label.fontStyle=style;
        label.fontSize=size;
        label.enableAutoSizing=true;
        label.fontSizeMin=Mathf.Max(13f,size*.58f);
        label.fontSizeMax=size;
        label.raycastTarget=false;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;
        rect.anchorMax=Vector2.one;
        rect.offsetMin=Vector2.zero;
        rect.offsetMax=Vector2.zero;
    }
#endif
}


// Additive access to the existing S4 binding: no changes to existing files.
public partial class S4FigmaResultsUI
{
    public ClaimRewards S8_GetBoundClaimRewards() => claimRewards;
}
