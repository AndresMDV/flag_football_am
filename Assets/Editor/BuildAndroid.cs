using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlagFootball.Editor
{
    public static class BuildAndroid
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string PackageName = "com.andresmdv.flagfootballam.prototype";

        public static void Run()
        {
            if (Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("Use the pinned Unity 6000.3.28f1 Editor.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Start the Editor with -buildTarget Android.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                throw new InvalidOperationException("The Bootstrap scene is missing.");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.productName = "Flag Football Prototype";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();

            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android");
            Directory.CreateDirectory(folder);
            var apk = Path.Combine(folder, "FlagFootballPrototype.apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            var summary = report.summary;
            File.WriteAllText(Path.Combine(folder, "build-report.json"), JsonUtility.ToJson(new Evidence
            {
                result = summary.result.ToString(), unityVersion = Application.unityVersion,
                packageName = PackageName, scene = ScenePath, backend = "IL2CPP",
                architecture = "ARM64", graphicsApi = "OpenGLES3", minApi = 25, targetApi = 36,
                sizeBytes = (long)summary.totalSize, errors = summary.totalErrors,
                warnings = summary.totalWarnings
            }, true));
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0 ||
                !File.Exists(apk) || new FileInfo(apk).Length == 0)
                throw new InvalidOperationException("Android build failed. Inspect the Editor log and build-report.json.");
            Debug.Log("ANDROID_BUILD_SUCCEEDED: Bootstrap development APK; ARM64/IL2CPP; target API 36.");
        }

        [Serializable]
        private sealed class Evidence
        {
            public string result;
            public string unityVersion;
            public string packageName;
            public string scene;
            public string backend;
            public string architecture;
            public string graphicsApi;
            public int minApi;
            public int targetApi;
            public long sizeBytes;
            public long errors;
            public long warnings;
        }
    }
}
