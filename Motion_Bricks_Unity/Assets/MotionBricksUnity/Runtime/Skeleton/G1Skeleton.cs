using System;
using UnityEngine;

namespace MotionBricksUnity
{
    [Serializable] public class G1Node { public string name; public int parent, qposIndex; public float[] position, rotation, axis, pivot; }
    [Serializable] public class G1Definition { public G1Node[] nodes; }
    public static class MotionBasis
    {
        // MuJoCo X forward, Y left, Z up -> Unity X right, Y up, Z forward (det B = -1).
        public static Vector3 ToUnity(Vector3 p) => new Vector3(-p.y, p.z, p.x);
        public static Vector3 ToModel(Vector3 p) => new Vector3(p.z, -p.x, p.y);
        public static Quaternion ToUnity(Quaternion q)
        {
            var b = Matrix4x4.zero; b.m01 = -1; b.m12 = 1; b.m20 = 1; b.m33 = 1;
            return (b * Matrix4x4.Rotate(q) * b.transpose).rotation;
        }
        public static Quaternion ToModel(Quaternion q)
        {
            var b=Matrix4x4.zero;b.m01=-1;b.m12=1;b.m20=1;b.m33=1;
            return (b.transpose*Matrix4x4.Rotate(q)*b).rotation;
        }
        public static Vector3 Vec(float[] a) => new Vector3(a[0], a[1], a[2]);
    }
    public sealed class G1Skeleton
    {
        public readonly G1Definition Definition;
        public readonly Vector3[] Positions;
        public readonly Quaternion[] Rotations;
        public G1Skeleton(G1Definition definition) {
            Definition = definition; Positions = new Vector3[definition.nodes.Length]; Rotations = new Quaternion[Positions.Length];
        }
        public void Evaluate(float[] q)
        {
            for (int i = 0; i < Positions.Length; i++) {
                var n = Definition.nodes[i];
                if (n.parent < 0) { Positions[i] = new Vector3(q[0], q[1], q[2]); Rotations[i] = new Quaternion(q[4], q[5], q[6], q[3]); continue; }
                var rest = new Quaternion(n.rotation[1], n.rotation[2], n.rotation[3], n.rotation[0]);
                var hinge = n.qposIndex < 0 ? Quaternion.identity : Quaternion.AngleAxis(q[n.qposIndex] * Mathf.Rad2Deg, MotionBasis.Vec(n.axis));
                var pivot = MotionBasis.Vec(n.pivot);
                var local = MotionBasis.Vec(n.position) + rest * (pivot - hinge * pivot);
                Positions[i] = Positions[n.parent] + Rotations[n.parent] * local;
                Rotations[i] = Rotations[n.parent] * rest * hinge;
            }
        }
    }
}
