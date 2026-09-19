using UnityEngine;

namespace MotionBricksUnity
{
    public class SmartObjectAnchor : MonoBehaviour
    {
        public SmartObjectDefinition definition;
        public TextAsset skeletonData;
        void OnDrawGizmos()
        {
            if(definition==null || skeletonData==null)return;
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.yellow;
            Gizmos.DrawWireCube(definition.approachCenter,definition.approachHalfExtents*2);Gizmos.matrix=Matrix4x4.identity;
            var skeleton=new G1Skeleton(JsonUtility.FromJson<G1Definition>(skeletonData.text));
            var poses=new[]{definition.entry,definition.interaction,definition.exit};
            var colors=new[]{Color.green,Color.cyan,Color.magenta};
            for(int k=0;k<poses.Length;k++) {
                skeleton.Evaluate(definition.ToWorld(poses[k],transform));Gizmos.color=colors[k];
                for(int i=1;i<skeleton.Positions.Length;i++)Gizmos.DrawLine(MotionBasis.ToUnity(skeleton.Positions[skeleton.Definition.nodes[i].parent]),MotionBasis.ToUnity(skeleton.Positions[i]));
            }
        }
    }
}
