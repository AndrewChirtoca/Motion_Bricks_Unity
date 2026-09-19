using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionBricksUnity.Editor
{
    public static class EnvironmentValidation
    {
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

        public static void Run()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Artifacts"));
            var character = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SK_Player_01.fbx");
            if (character == null) throw new Exception("Character FBX could not be imported.");
            var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/SK_Player_01.fbx").OfType<Avatar>().FirstOrDefault();
            var report = $"Unity: {Application.unityVersion}\nGPU: {SystemInfo.graphicsDeviceName}\nVRAM MiB: {SystemInfo.graphicsMemorySize}\nRAM MiB: {SystemInfo.systemMemorySize}\nAvatar exists: {avatar != null}\nAvatar valid: {avatar != null && avatar.isValid}\nAvatar human: {avatar != null && avatar.isHuman}\nSkinned renderers: {character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}\n";
            File.WriteAllText(Path.Combine(Root, "Artifacts/rig-validation.txt"), report);
            Debug.Log(report);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Assets/MotionBricksUnity/Samples");
            const string scene = "Assets/MotionBricksUnity/Samples/EnvironmentSmoke.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = Path.Combine(Root, "Artifacts/EnvironmentSmoke/EnvironmentSmoke.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText(Path.Combine(Root, "Artifacts/empty-build.txt"), build.summary.result + "\n" + build.summary.totalErrors);
            if (build.summary.result != BuildResult.Succeeded) throw new Exception("Empty Windows build failed.");
        }
    }
}
