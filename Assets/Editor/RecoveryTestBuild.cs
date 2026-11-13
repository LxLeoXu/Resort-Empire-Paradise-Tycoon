using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>One-off test APK. The diagnostic panel is excluded from ordinary builds.</summary>
public static class RecoveryTestBuild
{
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play mode / current build first.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Switch active target to Android first.");
        EditorApplication.update -= Build;
        EditorApplication.update += Build;
    }

    private static void Build()
    {
        EditorApplication.update -= Build;
        const string reportPath = "Temp/recovery-test-build.json";
        const string apk = "Builds/Android/ParadiseTycoon2D-RecoveryTest-20261113.apk";
        File.WriteAllText(reportPath, "{\"status\":\"building\"}");
        try
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(apk));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.Android, locationPathName = apk, options = BuildOptions.None,
                extraScriptingDefines = new[] { "SAVE_RECOVERY_TEST" }
            });
            var summary = report.summary;
            File.WriteAllText(reportPath, JsonUtility.ToJson(new Result
            {
                status = summary.result == BuildResult.Succeeded ? "succeeded" : "failed",
                path = Path.GetFullPath(apk), errors = summary.totalErrors, warnings = summary.totalWarnings,
                seconds = summary.totalTime.TotalSeconds
            }, true));
            Debug.Log("RECOVERY TEST BUILD: " + summary.result + " " + Path.GetFullPath(apk));
        }
        catch (Exception error)
        {
            File.WriteAllText(reportPath, JsonUtility.ToJson(new Result { status = "failed", message = error.ToString() }, true));
            Debug.LogException(error);
        }
    }

    [Serializable] private sealed class Result { public string status, path, message; public int errors, warnings; public double seconds; }
}
