using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LiANN.MahjongTones.EditorTools
{
    /// <summary>
    /// One-click scene setup and store builds for LiANN Pinyin Mahjong Tones.
    /// Menu: LiANN → Mahjong Tones. Command line (batch mode), for example:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod LiANN.MahjongTones.EditorTools.MahjongTonesBuild.BuildAndroid
    /// Signing is NOT configured here: set the Android keystore (Play App Signing upload key) and the iOS team in
    /// Player Settings on the build machine. No keys or passwords belong in this package.
    /// </summary>
    public static class MahjongTonesBuild
    {
        public const string ScenePath = "Assets/LiANN/MahjongTones.unity";
        public const string CompanyName = "Learning with LiANN";
        public const string ProductName = "LiANN Pinyin Mahjong Tones";
        public const string AppId = "com.learningwithliann.mahjongtones";   // confirm with the owner before the first store upload
        public const string Version = "0.2.0";

        [MenuItem("LiANN/Mahjong Tones/Create play scene", priority = 0)]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The game draws with UI Toolkit; a plain camera only stops the Editor's "No cameras rendering" overlay.
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            new GameObject("LiANN Mahjong Tones").AddComponent<MahjongTonesApp>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Add, don't replace: the project's own scenes stay in Build Settings (builds below pass ScenePath explicitly).
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(sc => sc.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("LiANN Mahjong Tones: created " + ScenePath + " and added it to Build Settings.");
        }

        [MenuItem("LiANN/Mahjong Tones/Build Android (App Bundle)", priority = 20)]
        public static void BuildAndroid()
        {
            ApplyCommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = VersionCode();
            EditorUserBuildSettings.buildAppBundle = true;
            EnsureProguardKeepRule();
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
                Debug.LogWarning("LiANN Mahjong Tones: no Android keystore set; the bundle is debug-signed and can't be uploaded to Google Play.");
            Build(BuildTarget.Android, "Builds/Android/LiANN-Mahjong-Tones-" + Version + ".aab");
        }

        [MenuItem("LiANN/Mahjong Tones/Build iOS (Xcode project)", priority = 21)]
        public static void BuildIOS()
        {
            ApplyCommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AppId);
            PlayerSettings.iOS.buildNumber = VersionCode().ToString();
            Build(BuildTarget.iOS, "Builds/iOS");   // open the Xcode project on a Mac, set the team, archive
        }

        [MenuItem("LiANN/Mahjong Tones/Build Windows", priority = 22)]
        public static void BuildWindows()
        {
            ApplyCommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/LiANN Mahjong Tones.exe");
        }

        const string ProguardFile = "Assets/Plugins/Android/proguard-user.txt";
        const string KeepRule = "-keep class com.learningwithliann.mahjongtones.SecureSessionStore { public static *; }";

        /// <summary>SecureSessionStore is called only by name through AndroidJavaClass, so R8 (Minify in release builds)
        /// could strip or rename it and sign-in would fall back to memory only. A package can't ship a proguard file,
        /// so the Android build adds the keep rule to the project's proguard-user.txt (used when Minify is on).</summary>
        static void EnsureProguardKeepRule()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProguardFile));
            string text = File.Exists(ProguardFile) ? File.ReadAllText(ProguardFile) : "";
            if (text.Contains(KeepRule)) return;
            File.WriteAllText(ProguardFile, text + (text.Length > 0 && !text.EndsWith("\n") ? "\n" : "") + "# LiANN Mahjong Tones: secure session store (called by name)\n" + KeepRule + "\n");
            AssetDatabase.ImportAsset(ProguardFile);
            Debug.Log("LiANN Mahjong Tones: added the SecureSessionStore keep rule to " + ProguardFile + ". Turn on Player Settings > Publishing > Custom Proguard File for it to apply.");
        }

        static void ApplyCommonSettings()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            // Portrait and landscape both supported (spec: layout must work in both).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // Off in the player (battery on phones). Build() puts the project's value back afterwards, because the
            // Editor reads the same setting and would otherwise stop running frames in Play mode when it loses focus.
            if (_runInBackgroundBeforeBuild == null) _runInBackgroundBeforeBuild = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = false;
        }

        static bool? _runInBackgroundBeforeBuild;

        static int VersionCode()
        {
            var p = Version.Split('.');
            return int.Parse(p[0]) * 10000 + int.Parse(p[1]) * 100 + int.Parse(p[2]);
        }

        static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                if (_runInBackgroundBeforeBuild is bool before) PlayerSettings.runInBackground = before;
                _runInBackgroundBeforeBuild = null;
            }
            var s = report.summary;
            Debug.Log($"LiANN Mahjong Tones: {target} build {s.result} → {path} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors).");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
