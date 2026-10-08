using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nightfall.Editor
{
    public static class NightfallProject
    {
        private const string Scene = "Assets/Scenes/Nightfall.unity";
        [MenuItem("Nightfall/Open game scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Scene);
        }
        [MenuItem("Nightfall/Build desktop game")]
        public static void BuildDesktop()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
            { EditorUtility.DisplayDialog("Choose a desktop platform", "Choose Windows, macOS, or Linux in File > Build Profiles first.", "OK"); return; }
            string folder = EditorUtility.SaveFolderPanel("Choose a build folder", "", "NightfallBuild");
            if (string.IsNullOrEmpty(folder)) return;
            string extension = target == BuildTarget.StandaloneWindows64 ? ".exe" : target == BuildTarget.StandaloneOSX ? ".app" : ".x86_64";
            var report = BuildPipeline.BuildPlayer(new[] { Scene }, System.IO.Path.Combine(folder, "Nightfall" + extension), target, BuildOptions.None);
            Debug.Log("Nightfall build: " + report.summary.result);
        }
    }
}
