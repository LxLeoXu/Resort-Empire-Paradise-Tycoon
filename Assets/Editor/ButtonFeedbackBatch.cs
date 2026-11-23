using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ButtonFeedbackBatch
{
    [MenuItem("Tools/UI/Apply Feedback To Entire Scene")]
    public static void ApplyToScene()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning(
                "[BUTTON FEEDBACK] Najprv zatvor Prefab Mode a otvor SampleScene.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BUTTON FEEDBACK] Nie je otvorena platna scena.");
            return;
        }

        int found = 0;
        int updated = 0;
        int missingGraphic = 0;
        int specialTransition = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                found++;

                if (button.targetGraphic == null)
                {
                    missingGraphic++;
                    continue;
                }

                if (button.transition == Selectable.Transition.Animation ||
                    button.transition == Selectable.Transition.SpriteSwap)
                {
                    specialTransition++;
                    continue;
                }

                Undo.RecordObject(button, "Apply Button Feedback");

                ColorBlock colors = button.colors;

                colors.normalColor = Color.white;
                colors.highlightedColor =
                    new Color(0.94f, 0.94f, 0.94f, 1f);
                colors.pressedColor =
                    new Color(0.63f, 0.63f, 0.63f, 1f);
                colors.selectedColor =
                    new Color(0.90f, 0.90f, 0.90f, 1f);
                colors.disabledColor =
                    new Color(0.5f, 0.5f, 0.5f, 0.5f);

                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.05f;

                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;

                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                EditorUtility.SetDirty(button);

                updated++;
            }
        }

        if (updated > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            $"[BUTTON FEEDBACK] Found: {found}, " +
            $"Updated: {updated}, " +
            $"Missing Graphic: {missingGraphic}, " +
            $"Special Transitions: {specialTransition}");
    }
}