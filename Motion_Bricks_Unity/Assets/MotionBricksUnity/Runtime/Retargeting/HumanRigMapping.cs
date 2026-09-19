using System;
using UnityEngine;

namespace MotionBricksUnity
{
    [Serializable] public class BoneCorrespondence
    {
        public HumanBodyBones humanBone;
        public string sourceBody, sourceChild;
        public Quaternion sourceRest = Quaternion.identity, targetRest = Quaternion.identity;
    }
    [CreateAssetMenu(menuName = "MotionBricks/Human Rig Mapping")]
    public class HumanRigMapping : ScriptableObject
    {
        public float rootScale = 1;
        public BoneCorrespondence[] bones;
        [TextArea] public string calibrationNotes;
    }
    public sealed class HumanRetargeter
    {
        readonly HumanRigMapping mapping;
        readonly Transform[] targets;
        readonly int[] sources;
        readonly Transform hips;
        public HumanRetargeter(Animator animator, HumanRigMapping mapping, G1Definition definition)
        {
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Valid Humanoid Avatar required");
            this.mapping = mapping; targets = new Transform[mapping.bones.Length]; sources = new int[targets.Length];
            for (int i = 0; i < targets.Length; ++i) {
                targets[i] = animator.GetBoneTransform(mapping.bones[i].humanBone);
                sources[i] = Array.FindIndex(definition.nodes, n => n.name == mapping.bones[i].sourceBody);
                if (targets[i] == null || sources[i] < 0) throw new InvalidOperationException("Incomplete rig mapping");
            }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            animator.applyRootMotion = false; animator.enabled = false;
        }
        // Called only from the demo's LateUpdate; Animator and physics never write the final bones.
        public void Apply(G1Skeleton skeleton, Vector3 origin)
        {
            hips.position = origin + MotionBasis.ToUnity(skeleton.Positions[0]) * mapping.rootScale;
            for (int i = 0; i < targets.Length; ++i) {
                var entry = mapping.bones[i];
                targets[i].rotation = MotionBasis.ToUnity(skeleton.Rotations[sources[i]]) * Quaternion.Inverse(entry.sourceRest) * entry.targetRest;
            }
        }
    }
}
