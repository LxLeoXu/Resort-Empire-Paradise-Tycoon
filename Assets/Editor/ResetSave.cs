using System.IO;
using UnityEditor;
using UnityEngine;

public static class ResetSave
{
    [MenuItem("Tools/Save Game/Reset Game Save (keeps backup)")]
    private static void Reset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode first, otherwise the game saves again when you stop.");
            return;
        }
        string dir = Application.persistentDataPath;
        string backup = Path.Combine(dir, "save-backup-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backup);
        int moved = 0;
        foreach (string name in new[] { "game-save.json", "game-save.json.bak", "game-save.json.tmp" })
        {
            string file = Path.Combine(dir, name);
            if (!File.Exists(file)) continue;
            File.Move(file, Path.Combine(backup, name));
            moved++;
        }
        Debug.Log("Reset Game Save: " + moved + " file(s) moved to " + backup);
    }

    [MenuItem("Tools/Save Game/Open Save Folder")]
    private static void Open() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}