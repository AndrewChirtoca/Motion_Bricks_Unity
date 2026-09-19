using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MotionBricksUnity.Tests
{
    public class MotionBoundaryTests
    {
        [Test] public void BasisMapsAxesAndConjugatesRotations() {
            Assert.AreEqual(Vector3.forward,MotionBasis.ToUnity(Vector3.right));
            Assert.AreEqual(Vector3.left,MotionBasis.ToUnity(Vector3.up));
            Assert.AreEqual(Vector3.up,MotionBasis.ToUnity(Vector3.forward));
            for(int i=0;i<50;i++) {
                var p=new Vector3(i*.2f,-.3f,i*.1f);var q=Quaternion.Euler(i*7,i*13,i*-3);
                Assert.Less((p-MotionBasis.ToModel(MotionBasis.ToUnity(p))).magnitude,1e-6);
                Assert.Less((MotionBasis.ToUnity(q*p)-MotionBasis.ToUnity(q)*MotionBasis.ToUnity(p)).magnitude,2e-5);
                Assert.Less(Quaternion.Angle(q,MotionBasis.ToModel(MotionBasis.ToUnity(q))),.06f);
            }
        }
        [Serializable] class FkFixture { public float[] qpos,positions; }
        [Test] public void SerialHingeFkMatchesIndependentScipy() {
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/reference"));
            var definition=JsonUtility.FromJson<G1Definition>(File.ReadAllText(Path.Combine(root,"skeleton.json")));
            var fixture=JsonUtility.FromJson<FkFixture>(File.ReadAllText(Path.Combine(root,"fk-reference.json")));
            var skeleton=new G1Skeleton(definition);skeleton.Evaluate(fixture.qpos);
            for(int i=0;i<skeleton.Positions.Length;i++) Assert.Less((skeleton.Positions[i]-new Vector3(fixture.positions[i*3],fixture.positions[i*3+1],fixture.positions[i*3+2])).magnitude,2e-5,$"body {i}");
        }
        static MotionResponse Response(long id,double at,int frames=30) {
            var q=new float[frames*36];for(int i=0;i<frames;i++){q[i*36]=(float)(at+i/30.0);q[i*36+3]=1;}
            return new MotionResponse{protocolVersion=1,requestId=id,contextTimestamp=at,modelVersion=MotionConstants.Version,frameRate=30,validFrameCount=frames,qpos=q};
        }
        [Test] public void DelayedResponseUsesAbsoluteTimeAndRejectsStale() {
            var b=new MotionBuffer();Assert.True(b.Splice(Response(2,10),10.5,2));var pose=new float[36];Assert.True(b.Sample(10.6,pose));Assert.AreEqual(10.6f,pose[0],1e-5);
            Assert.False(b.Splice(Response(1,10),10.7,2));Assert.False(b.Splice(Response(3,8),10.7,3));
            Assert.LessOrEqual(b.Count,80);
        }
        [Test] public void StarvationHoldsAndRebasePreservesFutureContext() {
            var b=new MotionBuffer();b.Splice(Response(1,0),0,1);var pose=new float[36];Assert.False(b.Sample(2,pose));var x=pose[0];
            b.Rebase(new Vector3(5,2,0));Assert.False(b.Sample(3,pose));Assert.AreEqual(x+5,pose[0]);Assert.AreEqual(2,pose[1]);
        }
        [Test] public void InvalidFramesAndNaNsFailClosed() {
            var r=Response(1,0);r.qpos[7]=float.NaN;Assert.Throws<InvalidOperationException>(()=>r.Validate());
            r=Response(1,0);r.validFrameCount=65;Assert.Throws<InvalidOperationException>(()=>r.Validate());
        }
        [Test] public void TransportTimingAllowsSubMicrosecondRoundoffButNotWrongOrigins() {
            var response=Response(1,7.123456789);response.timeOrigin=.123456789;
            Assert.True(response.MatchesTiming(.123456789+1e-9,7.123456789-1e-9));
            Assert.False(response.MatchesTiming(.124,7.123456789));
            response.timeOrigin=double.NaN;Assert.False(response.MatchesTiming(0,0));Assert.Throws<InvalidOperationException>(()=>response.Validate());
        }
        [Test] public void SmartObjectAnchorTransformsPositionAndHeadingConsistently() {
            var definition=ScriptableObject.CreateInstance<SmartObjectDefinition>();var go=new GameObject("anchor");
            try {
                go.transform.SetPositionAndRotation(new Vector3(2,0,3),Quaternion.Euler(0,90,0));
                var pose=definition.ToWorld(definition.entry,go.transform);
                Assert.Less((MotionBasis.ToUnity(new Vector3(pose[0],pose[1],pose[2]))-new Vector3(2,.788740f,3)).magnitude,1e-5);
                var rotation=MotionBasis.ToUnity(new Quaternion(pose[4],pose[5],pose[6],pose[3]));
                Assert.Less(Quaternion.Angle(rotation,go.transform.rotation),.06f);
            } finally { UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(definition); }
        }
    }
}
