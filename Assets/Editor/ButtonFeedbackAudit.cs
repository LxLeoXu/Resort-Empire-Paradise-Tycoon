#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ButtonFeedbackAudit
{
    private static int checkedButtons;
    private static int problems;
    private static int customTransitions;

    [MenuItem("Tools/Resort Empire/Sprint 3/Audit Button Feedback")]
    public static void Audit()
    {
        checkedButtons = 0;
        problems = 0;
        customTransitions = 0;

        Debug.Log("=== BUTTON FEEDBACK AUDIT START ===");

        // 1. Check all loaded scenes.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Button[] buttons =
                    root.GetComponentsInChildren<Button>(true);

                foreach (Button button in buttons)
                {
                    CheckButton(
                        button,
                        "SCENE: " + scene.name
                    );
                }
            }
        }

        // 2. Check prefab assets.
        string[] folders =
        {
            "Assets/Prefabs/Panels",
            "Assets/Prefabs/TopHUD"
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning(
                    "[BUTTON AUDIT] Folder missing: " + folder
                );

                continue;
            }

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { folder }
                );

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                Button[] buttons =
                    prefab.GetComponentsInChildren<Button>(true);

                foreach (Button button in buttons)
                {
                    CheckButton(button, "PREFAB: " + path);
                }
            }
        }

        Debug.Log(
            "=== BUTTON AUDIT FINISHED ===\n" +
            "Checked: " + checkedButtons + "\n" +
            "Problems: " + problems + "\n" +
            "Custom transitions to verify: " +
            customTransitions
        );
    }

    private static void CheckButton(
        Button button,
        string source)
    {
        if (button == null)
            return;

        checkedButtons++;

        string name = button.name;

        if (button.targetGraphic == null)
        {
            Report(
                source,
                name,
                "Missing Target Graphic"
            );

            return;
        }

        if (!button.targetGraphic.enabled)
        {
            Report(
                source,
                name,
                "Target Graphic is disabled"
            );
        }

        if (button.transition == Selectable.Transition.None)
        {
            Report(
                source,
                name,
                "Transition is NONE"
            );

            return;
        }

        if (button.transition !=
            Selectable.Transition.ColorTint)
        {
            customTransitions++;

            Debug.Log(
                "[BUTTON CUSTOM] " +
                source + " / " + name +
                " | " + button.transition
            );

            return;
        }

        ColorBlock colors = button.colors;

        float difference =
            Mathf.Abs(colors.normalColor.r -
                      colors.pressedColor.r) +
            Mathf.Abs(colors.normalColor.g -
                      colors.pressedColor.g) +
            Mathf.Abs(colors.normalColor.b -
                      colors.pressedColor.b) +
            Mathf.Abs(colors.normalColor.a -
                      colors.pressedColor.a);

        if (difference < 0.08f)
        {
            Report(
                source,
                name,
                "Pressed Color too similar to Normal Color"
            );
        }

        if (colors.fadeDuration > 0.1f)
        {
            Report(
                source,
                name,
                "Fade Duration exceeds 0.1 seconds"
            );
        }

        if (button.targetGraphic.color.a < 0.05f)
        {
            Report(
                source,
                name,
                "Target Graphic is transparent"
            );
        }
    }

    private static void Report(
        string source,
        string name,
        string problem)
    {
        problems++;

        Debug.LogWarning(
            "[BUTTON ISSUE] " +
            source + " / " +
            name + " | " +
            problem
        );
    }
}

#endif