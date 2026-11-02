using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity 6 Editor-only generator. Does not remove the team's old S4 panel.
/// Menu: Tools > S4 > Generate Figma UI
/// </summary>
public static class S4FigmaUIBuilder
{
    private const string GeneratedSpriteFolder = "Assets/Art/UI/S4";
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private static Sprite roundedSprite;
    private static Sprite starSprite;

    [MenuItem("Tools/S4/Generate Figma UI")]
    public static void Generate()
    {
        Canvas canvas = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null)
            canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("S4", "First create or open a scene with a Canvas.", "OK");
            return;
        }

        if (canvas.transform.Find("S4_FigmaPanel") != null)
        {
            EditorUtility.DisplayDialog("S4", "S4_FigmaPanel already exists. Rename or remove it before generating another copy. Your current panel has NOT been changed.", "OK");
            return;
        }

        string existingPrefabPath = PrefabFolder + "/S4_FigmaPanel.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(existingPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("S4", "A prefab already exists at " + existingPrefabPath +
                ". It has NOT been overwritten. Rename it if you want to regenerate.", "OK");
            return;
        }

        Directory.CreateDirectory(GeneratedSpriteFolder);
        Directory.CreateDirectory(PrefabFolder);
        roundedSprite = GenerateRoundedSprite();
        starSprite = GenerateStarSprite();

        GameObject root = NewUI("S4_FigmaPanel", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0.02f, 0.13f, 0.17f, 0.72f);
        overlay.raycastTarget = true;
        Undo.RegisterCreatedObjectUndo(root, "Generate S4 Figma UI");

        // Confetti is decorative and appears behind the information cards.
        Color[] confetti = { Hex("#F9B72C"), Hex("#19C1E6"), Hex("#F24A70"),
                             Hex("#9EDB3E"), Hex("#B77CF1"), Hex("#FF7131") };
        for (int i = 0; i < 32; i++)
        {
            float x = 0.025f + ((i * 37) % 96) / 100f;
            float y = 0.035f + ((i * 29) % 92) / 100f;
            GameObject piece = NewUI("Confetti_" + (i + 1).ToString("00"), root.transform);
            RectTransform rt = piece.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(7 + (i % 4) * 3, 6 + (i % 3) * 4);
            rt.localRotation = Quaternion.Euler(0, 0, (i * 41) % 180);
            Image img = piece.AddComponent<Image>();
            img.color = confetti[i % confetti.Length];
            img.raycastTarget = false;
        }

        MakeText(root.transform, "TitleText", "WAVE COMPLETE!",
            new Vector2(0, 252), new Vector2(700, 75), 47,
            Color.white, TextAlignmentOptions.Center, true);

        // Image sprites, not Unicode stars: TMP font support is not required.
        Image[] starImages = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject star = NewUI("Star_" + (i + 1), root.transform);
            Place(star.GetComponent<RectTransform>(), new Vector2((i - 2) * 56, 184), new Vector2(45, 45));
            Image image = star.AddComponent<Image>();
            image.sprite = starSprite;
            image.color = i < 4 ? Hex("#FFC235") : Hex("#929CA3");
            image.raycastTarget = false;
            starImages[i] = image;
        }

        GameObject performance = MakeCard(root.transform, "PerformancePanel", new Vector2(-307, -5), new Vector2(580, 307));
        MakeText(performance.transform, "PerformanceTitle", "PERFORMANCE",
            new Vector2(0, 112), new Vector2(505, 46), 27, Hex("#4D291B"), TextAlignmentOptions.Left, true);
        MakeText(performance.transform, "GuestsLabel", "Guests Served",
            new Vector2(-111, 55), new Vector2(280, 37), 23, Hex("#727272"), TextAlignmentOptions.Left);
        TMP_Text guestsValue = MakeText(performance.transform, "GuestsValue", "12/12",
            new Vector2(202, 55), new Vector2(100, 37), 25, Hex("#0DAA72"), TextAlignmentOptions.Right, true);
        MakeText(performance.transform, "SatisfactionLabel", "Avg. Satisfaction",
            new Vector2(-111, 7), new Vector2(280, 37), 23, Hex("#727272"), TextAlignmentOptions.Left);
        TMP_Text satisfactionValue = MakeText(performance.transform, "SatisfactionValue", "88%",
            new Vector2(202, 7), new Vector2(100, 37), 25, Hex("#0DAA72"), TextAlignmentOptions.Right, true);
        TMP_Text status1 = MakeText(performance.transform, "StatusLine1", "OK  Rooms & drinks went great!",
            new Vector2(0, -59), new Vector2(505, 36), 21, Hex("#3A8D65"), TextAlignmentOptions.Left);
        TMP_Text status2 = MakeText(performance.transform, "StatusLine2", "!   Food capacity needs improvement",
            new Vector2(0, -107), new Vector2(505, 36), 21, Hex("#B57926"), TextAlignmentOptions.Left);

        GameObject rewards = MakeCard(root.transform, "RewardsPanel", new Vector2(307, -5), new Vector2(580, 307));
        MakeText(rewards.transform, "RewardsTitle", "REWARDS EARNED",
            new Vector2(0, 112), new Vector2(505, 46), 27, Hex("#4D291B"), TextAlignmentOptions.Left, true);
        TMP_Text coinsValue = MakeText(rewards.transform, "CoinsRewardText", "+120 Coins",
            new Vector2(0, 43), new Vector2(490, 42), 27, Hex("#00A92B"), TextAlignmentOptions.Left, true);
        TMP_Text energyValue = MakeText(rewards.transform, "EnergyRewardText", "+5 Energy",
            new Vector2(0, -20), new Vector2(490, 42), 27, Hex("#05B5D3"), TextAlignmentOptions.Left, true);
        TMP_Text resortValue = MakeText(rewards.transform, "ResortValueRewardText", "+15 Resort Value",
            new Vector2(0, -84), new Vector2(490, 42), 27, Hex("#E4A100"), TextAlignmentOptions.Left, true);

        // Orange claim button with a dark-orange frame.
        MakeRoundedImage(root.transform, "ClaimButtonBorder", new Vector2(0, -251),
            new Vector2(425, 86), Hex("#8B350F"));
        GameObject buttonObject = MakeRoundedImage(root.transform, "ClaimRewardsButton",
            new Vector2(0, -245), new Vector2(413, 74), Hex("#FF7836"));
        Button claimButton = buttonObject.AddComponent<Button>();
        claimButton.targetGraphic = buttonObject.GetComponent<Image>();
        ColorBlock colors = claimButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .94f, .87f);
        colors.pressedColor = new Color(.87f, .81f, .78f);
        colors.selectedColor = Color.white;
        claimButton.colors = colors;
        MakeText(buttonObject.transform, "ButtonLabel", "CLAIM REWARDS", Vector2.zero,
            new Vector2(400, 62), 32, Color.white, TextAlignmentOptions.Center, true);

        // Hook up the existing ClaimRewards.cs without modifying its source code.
        ClaimRewards claim = root.AddComponent<ClaimRewards>();
        SerializedObject claimSO = new SerializedObject(claim);
        SerializedProperty buttonProperty = claimSO.FindProperty("claimButton");
        if (buttonProperty != null)
        {
            buttonProperty.objectReferenceValue = claimButton;
            claimSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("S4: ClaimRewards.cs has no serialized 'claimButton' field. Assign the button manually.");
        }

        S4FigmaResultsUI results = root.AddComponent<S4FigmaResultsUI>();
        results.EditorBind(guestsValue, satisfactionValue, status1, status2,
            coinsValue, energyValue, resortValue, starImages, claim);
        EditorUtility.SetDirty(results);
        EditorUtility.SetDirty(claim);

        // Save as a prefab and keep the instance in the open scene.
        string prefabPath = PrefabFolder + "/S4_FigmaPanel.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("S4 built! Prefab: " + prefabPath + ". Deactivate the OLD S4 panel if it is still visible.");
        EditorUtility.DisplayDialog("S4 ready",
            "Created S4_FigmaPanel with cards, sprites, button and bindings. " +
            "The old S4_ResultPanel was NOT deleted. In Play mode S4 is hidden until ShowResults(...) is called.", "OK");
    }

    [MenuItem("Tools/S4/Test wave 12 guests, 88% (Play mode)")]
    private static void TestWave()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play mode first, then use Tools > S4 > Test wave.");
            return;
        }
        S4FigmaResultsUI ui = UnityEngine.Object.FindFirstObjectByType<S4FigmaResultsUI>(FindObjectsInactive.Include);
        if (ui != null)
            ui.ShowResults(12, 12, 88);
        else
            Debug.LogError("S4FigmaResultsUI was not found in the open scene.");
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static GameObject MakeRoundedImage(Transform parent, string name, Vector2 position,
        Vector2 size, Color color)
    {
        GameObject go = NewUI(name, parent);
        Place(go.GetComponent<RectTransform>(), position, size);
        Image image = go.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return go;
    }

    private static GameObject MakeCard(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject outline = MakeRoundedImage(parent, name + "_Border", position,
            size + new Vector2(7, 7), Hex("#5E301A"));
        GameObject card = MakeRoundedImage(parent, name, position, size, Color.white);
        // Outline is a sibling behind card; keep both centered at the same position.
        outline.transform.SetSiblingIndex(card.transform.GetSiblingIndex() - 1);
        return card;
    }

    private static TMP_Text MakeText(Transform parent, string name, string value,
        Vector2 position, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions alignment, bool bold = false)
    {
        GameObject go = NewUI(name, parent);
        Place(go.GetComponent<RectTransform>(), position, size);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Color Hex(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color result);
        return result;
    }

    private static Sprite GenerateRoundedSprite()
    {
        const string path = GeneratedSpriteFolder + "/S4_RoundedWhite.png";
        if (!File.Exists(path))
        {
            const int size = 64;
            const float radius = 17f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (31.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (31.5f - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + .5f - distance);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        return ImportSprite(path, new Vector4(20, 20, 20, 20));
    }

    private static Sprite GenerateStarSprite()
    {
        const string path = GeneratedSpriteFolder + "/S4_StarWhite.png";
        if (!File.Exists(path))
        {
            const int size = 96;
            Vector2[] vertices = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? 42f : 19f;
                vertices[i] = new Vector2(47.5f + radius * Mathf.Cos(angle),
                    47.5f + radius * Mathf.Sin(angle));
            }
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, InsidePolygon(vertices, x + .5f, y + .5f)
                    ? Color.white : new Color(1, 1, 1, 0));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        return ImportSprite(path, Vector4.zero);
    }

    private static bool InsidePolygon(Vector2[] vertices, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            if (((vertices[i].y > y) != (vertices[j].y > y)) &&
                (x < (vertices[j].x - vertices[i].x) * (y - vertices[i].y) /
                    (vertices[j].y - vertices[i].y) + vertices[i].x))
                inside = !inside;
        }
        return inside;
    }

    private static Sprite ImportSprite(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not import S4 sprite " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
