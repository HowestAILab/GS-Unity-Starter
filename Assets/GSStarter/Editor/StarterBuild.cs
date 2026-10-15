using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Bouwt een APK voor de Quest (Android, Vulkan, IL2CPP). Voor de demo; studenten werken in de Editor.
///   Unity.exe -batchmode -buildTarget Android -projectPath . -executeMethod StarterBuild.BuildQuest -quit
/// </summary>
public static class StarterBuild
{
    const string ApkPath = "Builds/GSStarter.apk";

    [MenuItem("GS Starter/Build voor de Quest (APK)", priority = 60)]
    public static void BuildQuest()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GSStarter.unity" },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[GS Starter] Build {summary.result}: {summary.outputPath} " +
                  $"({summary.totalSize / 1e6:F0} MB, {summary.totalTime.TotalMinutes:F1} min, {summary.totalErrors} fouten)");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
