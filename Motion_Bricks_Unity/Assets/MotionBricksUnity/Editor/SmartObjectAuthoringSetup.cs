using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionBricksUnity.Editor
{
    public static class SmartObjectAuthoringSetup
    {
        [MenuItem("Tools/MotionBricks/Generate chair authoring scene (unvalidated interaction)")]
        public static void Generate()
        {
            const string folder="Assets/MotionBricksUnity/Samples";
            var definition=ScriptableObject.CreateInstance<SmartObjectDefinition>();
            definition.name="ChairDefinition";definition.interaction.qpos=MotionConstants.Rest();
            var q=definition.interaction.qpos;q[2]=.450270325f;q[7]=q[13]=-Mathf.PI/2;q[10]=q[16]=Mathf.PI/2;
            definition.validationStatus="INCOMPLETE: released backbone generated sitting in three placements, but 8/12/16-token endpoint tests failed 5 cm at human scale. This is authoring/preview, not a completed generative interaction.";
            var existing=AssetDatabase.LoadAssetAtPath<SmartObjectDefinition>(folder+"/ChairDefinition.asset");
            if(existing==null)AssetDatabase.CreateAsset(definition,folder+"/ChairDefinition.asset");
            else {EditorUtility.CopySerialized(definition,existing);Object.DestroyImmediate(definition);definition=existing;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var poses=new[]{new Vector3(0,0,0),new Vector3(2,0,1),new Vector3(-1,0,2)};
            var headings=new[]{0f,90f,-45f};
            for(int i=0;i<3;i++) {
                var anchor=new GameObject("Chair authoring "+i);anchor.transform.SetPositionAndRotation(poses[i],Quaternion.Euler(0,headings[i],0));
                var smart=anchor.AddComponent<SmartObjectAnchor>();smart.definition=definition;smart.skeletonData=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/G1Skeleton.json");
                var seat=GameObject.CreatePrimitive(PrimitiveType.Cube);seat.name="Source-scale seat preview";seat.transform.SetParent(anchor.transform,false);seat.transform.localPosition=new Vector3(0,.40f,0);seat.transform.localScale=new Vector3(.45f,.06f,.45f);
            }
            EditorSceneManager.SaveScene(scene,folder+"/SmartObjectAuthoring.unity");AssetDatabase.SaveAssets();
        }
    }
}
