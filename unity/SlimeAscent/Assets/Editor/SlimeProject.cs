using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeAscent.Editor
{
    public static class SlimeProject
    {
        private static readonly string[] Scenes={"Assets/Scenes/Opening.unity","Assets/Scenes/Lower.unity","Assets/Scenes/Middle.unity","Assets/Scenes/Upper.unity","Assets/Scenes/HumanWorld.unity"};
        [MenuItem("Slime Ascent/Open opening scene")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Scenes[0]);}
        [MenuItem("Slime Ascent/Build desktop game")]
        public static void Build()
        {
            var target=EditorUserBuildSettings.activeBuildTarget;
            if(target!=BuildTarget.StandaloneWindows64&&target!=BuildTarget.StandaloneOSX&&target!=BuildTarget.StandaloneLinux64){EditorUtility.DisplayDialog("Desktop platform required","Choose Windows, macOS or Linux in File > Build Profiles first.","OK");return;}
            string folder=EditorUtility.SaveFolderPanel("Choose output folder","","SlimeAscentBuild");if(string.IsNullOrEmpty(folder))return;
            string suffix=target==BuildTarget.StandaloneWindows64?".exe":target==BuildTarget.StandaloneOSX?".app":".x86_64";
            var result=BuildPipeline.BuildPlayer(Scenes,System.IO.Path.Combine(folder,"SlimeAscent"+suffix),target,BuildOptions.None);
            Debug.Log("Slime Ascent build: "+result.summary.result);
        }
        [MenuItem("Slime Ascent/Clear local checkpoint")]
        public static void ClearCheckpoint()
        {if(EditorUtility.DisplayDialog("Clear Slime Ascent checkpoint?","This removes this game's local floor checkpoint only.","Clear","Cancel")){PlayerPrefs.DeleteKey("SlimeAscent.Checkpoint.v1");PlayerPrefs.Save();}}
    }
}
