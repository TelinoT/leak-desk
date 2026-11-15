using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>One-click WebGL build for itch.io: Leak Desk > Build WebGL (output: Builds/WebGL, zip-ready).</summary>
public static class LeakDeskBuild
{
    public const string OutputDir = "Builds/WebGL";
    const string StatusFile = "Builds/last_build_status.txt";

    [MenuItem("Leak Desk/Build WebGL")]
    public static void BuildWebGL()
    {
        Directory.CreateDirectory("Builds");
        File.WriteAllText(StatusFile, "running");

        if (!File.Exists("Assets/Game/Resources/LlmConfig.asset"))
        {
            File.WriteAllText(StatusFile, "failed: missing Assets/Game/Resources/LlmConfig.asset (the build would have no LLM)");
            Debug.LogError("[LeakDeskBuild] Missing LlmConfig.asset");
            return;
        }

        PlayerSettings.productName = "Leak Desk";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // itch.io doesn't send Content-Encoding headers
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Game/Scenes/LeakDesk.unity" },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        string result = s.result == BuildResult.Succeeded
            ? $"succeeded: {s.totalSize / (1024f * 1024f):0.0} MB in {s.totalTime.TotalSeconds:0}s -> {OutputDir}"
            : $"failed: {s.result}, {s.totalErrors} errors";
        File.WriteAllText(StatusFile, result);
        Debug.Log("[LeakDeskBuild] " + result);
    }
}
