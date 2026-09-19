using System;
using UnityEngine;

namespace MotionBricksUnity
{
    [Serializable] public class SmartObjectPose
    {
        public string label;
        [Tooltip("Object-relative pose in G1 MuJoCo coordinates; 36 values, wxyz quaternion.")]
        public float[] qpos = MotionConstants.Rest();
        [Range(6,16)] public int transitionTokens = 12;
        [Tooltip("The released backbone supports complete frame masks, not arbitrary per-joint masks.")]
        public bool constrainRoot = true, constrainBody = true;
    }
    [CreateAssetMenu(menuName="MotionBricks/Smart Object Definition")]
    public class SmartObjectDefinition : ScriptableObject
    {
        public Vector3 approachCenter = new Vector3(0,0,1);
        public Vector3 approachHalfExtents = new Vector3(.5f,.8f,.6f);
        public SmartObjectPose entry = new SmartObjectPose {label="Entry"};
        public SmartObjectPose interaction = new SmartObjectPose {label="Seated"};
        public SmartObjectPose exit = new SmartObjectPose {label="Exit"};
        public float sourceChairHeight = .45f;
        public float humanScaleTolerance = .05f;
        public bool interruptWithGeneratedRecovery = true;
        [TextArea] public string validationStatus = "Unvalidated; requires a target-pose provider and successful source/human pose checks.";

        public float[] ToWorld(SmartObjectPose keyframe,Transform anchor)
        {
            if(keyframe.qpos==null || keyframe.qpos.Length!=36)throw new InvalidOperationException("Expected a 36-value G1 qpos");
            if((anchor.lossyScale-Vector3.one).sqrMagnitude>.0001f)throw new InvalidOperationException("Smart object constraints require unit scale");
            var q=(float[])keyframe.qpos.Clone();
            var p=MotionBasis.ToModel(anchor.TransformPoint(MotionBasis.ToUnity(new Vector3(q[0],q[1],q[2]))));
            var rotation=MotionBasis.ToModel(anchor.rotation*MotionBasis.ToUnity(new Quaternion(q[4],q[5],q[6],q[3])));
            q[0]=p.x;q[1]=p.y;q[2]=p.z;q[3]=rotation.w;q[4]=rotation.x;q[5]=rotation.y;q[6]=rotation.z;
            return q;
        }
    }
}
