using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionBricksUnity.Editor
{
    public static class DemoSetup
    {
        const string Folder = "Assets/MotionBricksUnity/Samples";
        [MenuItem("Tools/MotionBricks/Generate demo scenes")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            File.Copy(Path.Combine(EnvironmentValidation.Root,"Artifacts/reference/skeleton.json"),Folder+"/G1Skeleton.json",true);
            File.Copy(Path.Combine(EnvironmentValidation.Root,"Artifacts/reference/motion.json"),Folder+"/ReferenceMotion.json",true);
            AssetDatabase.Refresh();
            Create(false); Create(true); AssetDatabase.SaveAssets();
        }
        static void Create(bool recorded)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name="Ground";ground.transform.localScale=new Vector3(200,1,200);
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(50,-30,0);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(5,3,-6);camera.transform.LookAt(Vector3.up);camera.farClipPlane=300;
            camera.backgroundColor=new Color(.09f,.12f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
            for(int i=0;i<2;i++) {var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Collision wall";wall.layer=8;wall.transform.position=new Vector3(i==0?-8:12,1,4);wall.transform.localScale=new Vector3(.4f,2,10);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SK_Player_01.fbx");
            var human=(GameObject)PrefabUtility.InstantiatePrefab(prefab);human.name="Human";
            var animator=human.GetComponent<Animator>() ?? human.AddComponent<Animator>();
            var definition=JsonUtility.FromJson<G1Definition>(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/G1Skeleton.json").text);
            var mapping=BuildMapping(animator,definition);
            var path=Folder+"/SKPlayerMapping.asset";
            var existing=AssetDatabase.LoadAssetAtPath<HumanRigMapping>(path);
            if(existing==null) AssetDatabase.CreateAsset(mapping,path);else {EditorUtility.CopySerialized(mapping,existing);UnityEngine.Object.DestroyImmediate(mapping);mapping=existing;}
            var renderers=human.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            File.WriteAllLines(Path.Combine(EnvironmentValidation.Root,"Artifacts/character-renderers.txt"),renderers.Select(r=>$"{r.name} active={r.gameObject.activeSelf} bounds={r.bounds.size}"));
            foreach(var lod in human.GetComponentsInChildren<LODGroup>(true)) lod.enabled=false;
            foreach(var renderer in renderers) {
                renderer.enabled=!renderer.name.Contains("_body") || renderer.name=="male_body1_noGloves_LOD0";
                renderer.updateWhenOffscreen=true;
            }
            var demo=new GameObject("MotionBricks runtime").AddComponent<LocomotionDemo>();
            demo.skeletonData=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/G1Skeleton.json");demo.recording=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/ReferenceMotion.json");
            demo.mapping=mapping;demo.character=animator;demo.viewCamera=camera;demo.recordedPlayback=recorded;
            EditorSceneManager.SaveScene(scene,Folder+(recorded?"/SourcePlayback.unity":"/LocomotionDemo.unity"));
        }
        static HumanRigMapping BuildMapping(Animator animator,G1Definition definition)
        {
            var skeleton=new G1Skeleton(definition);skeleton.Evaluate(MotionConstants.Rest());
            var map=ScriptableObject.CreateInstance<HumanRigMapping>();
            map.rootScale=animator.GetBoneTransform(HumanBodyBones.Hips).position.y/.788740f;
            map.calibrationNotes="Generated from valid Humanoid rest pose and official serial-hinge XML. Whole-chain world rotations, automatic swing alignment; fingers retain rest. No IK. Requires visual acceptance.";
            var entries=new List<BoneCorrespondence>();
            void Add(HumanBodyBones bone,string source,HumanBodyBones child=HumanBodyBones.LastBone,string sourceChild=null) {
                var target=animator.GetBoneTransform(bone);if(target==null)return;
                int index=Array.FindIndex(definition.nodes,n=>n.name==source);
                if(index<0)throw new Exception("Missing source body "+source);
                var rotation=target.rotation;
                if(child!=HumanBodyBones.LastBone && sourceChild!=null) {
                    var targetChild=animator.GetBoneTransform(child);int next=Array.FindIndex(definition.nodes,n=>n.name==sourceChild);
                    if(targetChild!=null && next>=0) rotation=Quaternion.FromToRotation(targetChild.position-target.position,MotionBasis.ToUnity(skeleton.Positions[next]-skeleton.Positions[index]))*rotation;
                }
                entries.Add(new BoneCorrespondence{humanBone=bone,sourceBody=source,sourceChild=sourceChild,sourceRest=MotionBasis.ToUnity(skeleton.Rotations[index]),targetRest=rotation});
                // Calibrate in hierarchy order so the hand/foot rest inherits the aligned parent chain.
                target.rotation=rotation;
            }
            Add(HumanBodyBones.Hips,"pelvis");Add(HumanBodyBones.Spine,"waist_roll_link");Add(HumanBodyBones.Chest,"torso_link");
            Add(HumanBodyBones.Neck,"torso_link");Add(HumanBodyBones.Head,"torso_link");
            Add(HumanBodyBones.LeftUpperLeg,"left_hip_yaw_link",HumanBodyBones.LeftLowerLeg,"left_knee_link");
            Add(HumanBodyBones.LeftLowerLeg,"left_knee_link",HumanBodyBones.LeftFoot,"left_ankle_roll_link");Add(HumanBodyBones.LeftFoot,"left_ankle_roll_link");
            Add(HumanBodyBones.RightUpperLeg,"right_hip_yaw_link",HumanBodyBones.RightLowerLeg,"right_knee_link");
            Add(HumanBodyBones.RightLowerLeg,"right_knee_link",HumanBodyBones.RightFoot,"right_ankle_roll_link");Add(HumanBodyBones.RightFoot,"right_ankle_roll_link");
            Add(HumanBodyBones.LeftUpperArm,"left_shoulder_yaw_link",HumanBodyBones.LeftLowerArm,"left_elbow_link");
            Add(HumanBodyBones.LeftLowerArm,"left_elbow_link",HumanBodyBones.LeftHand,"left_wrist_yaw_link");Add(HumanBodyBones.LeftHand,"left_wrist_yaw_link");
            Add(HumanBodyBones.RightUpperArm,"right_shoulder_yaw_link",HumanBodyBones.RightLowerArm,"right_elbow_link");
            Add(HumanBodyBones.RightLowerArm,"right_elbow_link",HumanBodyBones.RightHand,"right_wrist_yaw_link");Add(HumanBodyBones.RightHand,"right_wrist_yaw_link");
            map.bones=entries.ToArray();return map;
        }
        public static void Build()
        {
            Generate();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Folder+"/LocomotionDemo.unity",Folder+"/SourcePlayback.unity"},
                locationPathName=Path.Combine(EnvironmentValidation.Root,"Artifacts/Windows/MotionBricks.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            File.WriteAllText(Path.Combine(EnvironmentValidation.Root,"Artifacts/demo-build.txt"),result.summary.result+"\nErrors: "+result.summary.totalErrors);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Demo build failed");
        }
    }
}
