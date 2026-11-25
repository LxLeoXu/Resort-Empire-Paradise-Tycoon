using UnityEngine;

public static class ProgressionBootstrap
{
    public static void EnsureExists()
    {
        if (Object.FindFirstObjectByType<DailyRewardController>(FindObjectsInactive.Include) != null) return;
        var prefab = Resources.Load<GameObject>("ProgressionSystems");
        if (prefab != null) Object.Instantiate(prefab).name = "ProgressionSystems";
        else Debug.LogError("ProgressionSystems prefab is missing from Resources.");
    }
}
