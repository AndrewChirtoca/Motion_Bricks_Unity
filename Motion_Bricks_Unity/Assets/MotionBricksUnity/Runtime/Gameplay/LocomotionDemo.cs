using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MotionBricksUnity
{
    [Serializable] public class RecordedFrame { public float[] qpos; }
    [Serializable] public class RecordedMotion { public string modelVersion; public float frameRate; public RecordedFrame[] frames; }
    public class LocomotionDemo : MonoBehaviour
    {
        public TextAsset skeletonData, recording;
        public HumanRigMapping mapping;
        public Animator character;
        public Camera viewCamera;
        public bool recordedPlayback;
        public int port = 17861;
        public bool showJointNames;
        public G1Skeleton Skeleton { get; private set; }
        public int Accepted { get; private set; }
        public int Underruns { get; private set; }
        public string Status { get; private set; } = "Starting";
        readonly MotionBuffer buffer = new MotionBuffer();
        readonly float[] pose = MotionConstants.Rest();
        readonly float[] contextSample = new float[36];
        readonly List<float> updateMs = new List<float>();
        readonly List<float> frameMs = new List<float>();
        readonly List<float> responseMs = new List<float>();
        readonly List<float> observableMs = new List<float>();
        float[][] previousPrediction;
        double commandAt, acceptedRequestAt;
        int observedMode=-1;
        Vector3 observedMove,observedFace;
        CancellationTokenSource lifetime;
        IMotionGenerator provider;
        Task<MotionResponse> pending;
        Task initializing;
        HumanRetargeter retargeter;
        RecordedMotion clip;
        Transform[] links;
        long nextId, expectedId;
        int sentMode = -1;
        Vector3 sentMove, sentFace, facing = Vector3.forward, previousRoot;
        double epoch, nextPlan, requestAt, pendingOrigin;
        float latency = .05f;
        bool starving, hasRoot, ready;
        double smokeSeconds;
        string outputFolder;
        int captureIndex;
        double lastCpuMs;
        Vector3 presentationOrigin = new Vector3(1.8f, 0, 0);
        Material diagnosticMaterial;
        RenderTexture smokeTarget;
        UniversalRenderPipeline.SingleCameraRequest renderRequest;
        Transform[] rootAxes;
        Material[] axisMaterials;
        LineRenderer trajectory;
        readonly Vector3[] trail=new Vector3[128];
        double nextTrail;
        Vector3 Present(Vector3 p)=>MotionBasis.ToUnity(p)*mapping.rootScale;

        void Start()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true; QualitySettings.vSyncCount = 0;
            epoch = Time.realtimeSinceStartupAsDouble;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; ++i) if (args[i] == "-motionSmokeSeconds") double.TryParse(args[i+1], out smokeSeconds);
            for (int i = 0; i < args.Length - 1; ++i) if (args[i] == "-motionOutputFolder") outputFolder=args[i+1];
            if(!string.IsNullOrEmpty(outputFolder)) Directory.CreateDirectory(outputFolder);
            if(smokeSeconds>0) {
                // An occluded Windows swapchain may skip rendering. Explicit offscreen render requests keep QA real.
                smokeTarget=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);smokeTarget.Create();
                renderRequest=new UniversalRenderPipeline.SingleCameraRequest { destination=smokeTarget };
                viewCamera.enabled=false;
            }
            Skeleton = new G1Skeleton(JsonUtility.FromJson<G1Definition>(skeletonData.text));
            Skeleton.Evaluate(pose);
            if (character != null) retargeter = new HumanRetargeter(character, mapping, Skeleton.Definition);
            links = new Transform[Skeleton.Positions.Length];
            diagnosticMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); diagnosticMaterial.color = new Color(.1f,.85f,1f);
            for (int i = 1; i < links.Length; ++i) {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = Skeleton.Definition.nodes[i].name;
                Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = diagnosticMaterial; links[i] = go.transform;
            }
            rootAxes=new Transform[3];axisMaterials=new Material[3];
            for(int i=0;i<3;i++) {
                var axis=GameObject.CreatePrimitive(PrimitiveType.Cylinder);axis.name="Source root axis "+i;Destroy(axis.GetComponent<Collider>());
                axisMaterials[i]=new Material(diagnosticMaterial){color=i==0?Color.red:i==1?Color.green:Color.blue};axis.GetComponent<Renderer>().sharedMaterial=axisMaterials[i];rootAxes[i]=axis.transform;
            }
            trajectory=new GameObject("Generated trajectory").AddComponent<LineRenderer>();trajectory.sharedMaterial=diagnosticMaterial;
            trajectory.widthMultiplier=.015f;trajectory.positionCount=trail.Length;
            if (recordedPlayback) { clip = JsonUtility.FromJson<RecordedMotion>(recording.text); Status = "Recorded reference playback — no live inference"; }
            else {
                lifetime = new CancellationTokenSource(); provider = new PythonMotionGenerator(port);
                initializing = provider.InitializeAsync(lifetime.Token); Status = "Connecting to " + provider.ProviderName;
            }
        }

        void Update()
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            double now = Time.realtimeSinceStartupAsDouble - epoch;
            if (recordedPlayback) {
                var f = (float)(now * clip.frameRate) % clip.frames.Length; int a = (int)f, b = Math.Min(a+1,clip.frames.Length-1);
                for (int j=0;j<36;++j) pose[j] = Mathf.Lerp(clip.frames[a].qpos[j],clip.frames[b].qpos[j],f-a);
                var qa=clip.frames[a].qpos; var qb=clip.frames[b].qpos;
                var q=Quaternion.Slerp(new Quaternion(qa[4],qa[5],qa[6],qa[3]),new Quaternion(qb[4],qb[5],qb[6],qb[3]),f-a);
                pose[3]=q.w;pose[4]=q.x;pose[5]=q.y;pose[6]=q.z;
            } else TickLive(now);
            if(!recordedPlayback) CorrectCollision(); Skeleton.Evaluate(pose);
            if (smokeSeconds > 0 && now >= smokeSeconds) { SaveMetrics(); Application.Quit(); smokeSeconds = 0; }
            lastCpuMs=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Application.Quit();
            if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame) showJointNames=!showJointNames;
        }

        void TickLive(double now)
        {
            if (!ready) {
                if (initializing != null && initializing.IsCompleted) {
                    if (initializing.IsCompletedSuccessfully) { ready = true; Status = provider.ProviderName; }
                    else { Status = "Service unavailable: " + initializing.Exception?.GetBaseException().Message; initializing = null; nextPlan = now+1; }
                }
                if (initializing == null && now >= nextPlan) initializing = provider.InitializeAsync(lifetime.Token);
                if (!ready) return;
            }
            ReadCommand(now, out int mode, out Vector3 move, out Vector3 face);
            if(observedMode<0) {observedMode=mode;observedMove=move;observedFace=face;}
            bool changed = mode != sentMode || Vector3.Angle(move,sentMove) > 5 || (move-sentMove).sqrMagnitude > .05f || Vector3.Angle(face,sentFace)>5;
            if(Accepted>0 && (mode!=observedMode || (move-observedMove).sqrMagnitude>.05f || Vector3.Angle(face,observedFace)>5)) {
                commandAt=now;previousPrediction=new float[120][];
                for(int i=0;i<120;i++) {previousPrediction[i]=new float[36];buffer.Sample(now+i/60.0,previousPrediction[i]);}
                observedMode=mode;observedMove=move;observedFace=face;
            }
            if (pending != null && pending.IsCompleted) {
                if (pending.IsCompletedSuccessfully) {
                    var response = pending.Result;
                    latency = Mathf.Lerp(latency,(float)(now-requestAt),.25f);
                    if (!changed && response.MatchesTiming(epoch,pendingOrigin) && buffer.Splice(response, now, expectedId)) {
                        Accepted++; acceptedRequestAt=requestAt;Status = provider.ProviderName;if(responseMs.Count<100000)responseMs.Add((float)(now-requestAt)*1000);
                    }
                } else {
                    Status = "Holding: " + pending.Exception?.GetBaseException().Message; ready=false; initializing=null; nextPlan=now+1;
                }
                pending = null;
            }
            bool available = buffer.Sample(now, pose);
            if(previousPrediction!=null && acceptedRequestAt>=commandAt && now-commandAt<2) {
                var frame=(float)((now-commandAt)*60);var index=Math.Min(119,(int)frame);
                var prior=previousPrediction[index];var next=previousPrediction[Math.Min(119,index+1)];var alpha=frame-index;
                var rootDelta=new Vector3(pose[0]-Mathf.Lerp(prior[0],next[0],alpha),pose[1]-Mathf.Lerp(prior[1],next[1],alpha),pose[2]-Mathf.Lerp(prior[2],next[2],alpha)).magnitude;
                float jointDelta=0;for(int j=7;j<36;j++)jointDelta=Mathf.Max(jointDelta,Mathf.Abs(pose[j]-Mathf.Lerp(prior[j],next[j],alpha)));
                var previousRotation=Quaternion.Slerp(new Quaternion(prior[4],prior[5],prior[6],prior[3]),new Quaternion(next[4],next[5],next[6],next[3]),alpha);
                var rotationDelta=Quaternion.Angle(previousRotation,new Quaternion(pose[4],pose[5],pose[6],pose[3]));
                if(rootDelta>.01f || jointDelta>.02f || rotationDelta>1) {if(observableMs.Count<100000)observableMs.Add((float)(now-commandAt)*1000);previousPrediction=null;}
            }
            if (Accepted > 0 && !available && !starving) Underruns++;
            starving = Accepted > 0 && !available;
            if (pending == null && ready && now >= nextPlan && (changed || buffer.End-now < Math.Max(.3,latency*2+.15))) {
                // The four-frame context ends near expected inference completion. Starting it there
                // would add another 100 ms of already-known motion to every command response.
                var at = now + Math.Min(.15, Math.Max(.035,latency)) - 3.0/30.0;
                var context = new float[144];
                for (int f=0;f<4;++f) {
                    if (buffer.Count == 0) Array.Copy(pose,contextSample,36); else buffer.Sample(at+f/30.0,contextSample);
                    Array.Copy(contextSample,0,context,f*36,36);
                }
                expectedId = ++nextId; requestAt = now; pendingOrigin=at; nextPlan=now+.1;
                var m=MotionBasis.ToModel(move); var facingModel=MotionBasis.ToModel(face);
                pending=provider.GenerateAsync(new MotionRequest { requestId=expectedId, contextTimestamp=at,timeOrigin=epoch,
                    context=context,mode=mode,movement=new[]{m.x,m.y,m.z},facing=new[]{facingModel.x,facingModel.y,facingModel.z} },lifetime.Token);
                sentMode=mode;sentMove=move;sentFace=face;
            }
        }

        void ReadCommand(double now, out int mode, out Vector3 move, out Vector3 face)
        {
            if (smokeSeconds > 0) {
                int phase=(int)(now/4)%6; mode=phase==0 || phase==4 ? 0 : phase==5 ? 3 : 2;
                move=phase==2 ? Vector3.left : phase==3 ? Vector3.right : Vector3.forward;
                if (mode==0) move=Vector3.zero;
                face=move.sqrMagnitude>.1f ? move : Vector3.forward; return;
            }
            var keys=Keyboard.current; Vector2 axis=Vector2.zero;
            if (keys!=null) {
                axis.x=(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0);
                axis.y=(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0);
                if(keys.qKey.isPressed) facing=Quaternion.Euler(0,-90*Time.deltaTime,0)*facing;
                if(keys.eKey.isPressed) facing=Quaternion.Euler(0,90*Time.deltaTime,0)*facing;
            }
            var forward=Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized;
            move=(forward*axis.y+viewCamera.transform.right*axis.x).normalized;
            mode=move.sqrMagnitude<.01f?0:keys!=null && keys.leftShiftKey.isPressed?3:2;
            if (move.sqrMagnitude>.01f && !(keys!=null && keys.leftAltKey.isPressed)) facing=move;
            face=facing;
        }

        void CorrectCollision()
        {
            var root=MotionBasis.ToUnity(new Vector3(pose[0],pose[1],pose[2]))*mapping.rootScale;
            if (hasRoot) {
                var delta=root-previousRoot; delta.y=0;
                var bottom=presentationOrigin+previousRoot; bottom.y=.35f;
                if (delta.magnitude>.00001f && Physics.CapsuleCast(bottom,bottom+Vector3.up*.9f,.24f,delta.normalized,out var hit,delta.magnitude+.02f,1<<8)) {
                    var correction=-delta.normalized*Mathf.Max(0,delta.magnitude-Mathf.Max(0,hit.distance-.02f));
                    var model=MotionBasis.ToModel(correction)/mapping.rootScale;
                    pose[0]+=model.x;pose[1]+=model.y;pose[2]+=model.z;buffer.Rebase(model); expectedId=++nextId;
                    root+=correction; nextPlan=0; sentMode=-1;
                }
            }
            previousRoot=root;hasRoot=true;
        }

        void LateUpdate()
        {
            if (Skeleton==null) return;
            var start=System.Diagnostics.Stopwatch.GetTimestamp();
            retargeter?.Apply(Skeleton,presentationOrigin);
            for(int i=1;i<links.Length;++i) {
                var n=Skeleton.Definition.nodes[i]; var a=Present(Skeleton.Positions[n.parent]); var b=Present(Skeleton.Positions[i]);
                var delta=b-a; links[i].position=(a+b)*.5f; links[i].up=delta.sqrMagnitude>1e-8f?delta:Vector3.up;links[i].localScale=new Vector3(.018f,delta.magnitude*.5f,.018f);
            }
            var focus=Present(Skeleton.Positions[0]);
            for(int i=0;i<3;i++) {
                var direction=MotionBasis.ToUnity(Skeleton.Rotations[0]*(i==0?Vector3.right:i==1?Vector3.up:Vector3.forward));
                rootAxes[i].position=focus+direction*.12f;rootAxes[i].up=direction;rootAxes[i].localScale=new Vector3(.018f,.12f,.018f);
            }
            viewCamera.transform.position=Vector3.Lerp(viewCamera.transform.position,focus+new Vector3(3.5f,2.1f,-4.5f),Time.deltaTime*3);
            viewCamera.transform.LookAt(focus+Vector3.right*.9f+Vector3.up*.3f);
            var now=Time.realtimeSinceStartupAsDouble-epoch;
            if(now>=nextTrail) {Array.Copy(trail,1,trail,0,trail.Length-1);trail[trail.Length-1]=new Vector3(focus.x,.015f,focus.z);trajectory.SetPositions(trail);nextTrail=now+.1;}
            if(now>5 && updateMs.Count<100000) {
                updateMs.Add((float)(lastCpuMs+(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency));
                frameMs.Add(Time.unscaledDeltaTime*1000);
            }
            if(smokeTarget!=null) RenderPipeline.SubmitRenderRequest(viewCamera,renderRequest);
            if(!string.IsNullOrEmpty(outputFolder) && captureIndex<3 && now>=6+captureIndex*4) {
                var previous=RenderTexture.active;RenderTexture.active=smokeTarget;
                var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(outputFolder,$"frame-{captureIndex++}.png"),texture.EncodeToPNG());
                Destroy(texture);RenderTexture.active=previous;
            }
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12,12,650,112),"MotionBricks / GEAR-SONIC prototype");
            GUI.Label(new Rect(24,38,630,25),Status);
            GUI.Label(new Rect(24,62,630,25),$"Generated batches {Accepted}  |  buffer {buffer.Count}  |  underruns {Underruns}  |  rejected {buffer.Rejected}");
            GUI.Label(new Rect(24,86,630,25),"WASD move · Shift run · Alt hold facing · Q/E facing · Esc quit");
            if(showJointNames && Skeleton!=null) for(int i=0;i<Skeleton.Positions.Length;++i) {
                var p=viewCamera.WorldToScreenPoint(Present(Skeleton.Positions[i]));
                if(p.z>0) GUI.Label(new Rect(p.x,Screen.height-p.y,180,18),Skeleton.Definition.nodes[i].name);
            }
        }
        static float Percentile(List<float> list,float p) { if(list.Count==0)return -1;list.Sort();return list[Math.Min(list.Count-1,(int)(p*(list.Count-1)))]; }
        void SaveMetrics() {
            var path=Path.Combine(outputFolder ?? Application.persistentDataPath,"motionbricks-smoke.txt");
            File.WriteAllText(path,$"Provider: {provider?.ProviderName}\nAccepted: {Accepted}\nUnderruns: {Underruns}\nRejected: {buffer.Rejected}\nUpdate CPU p95 ms: {Percentile(updateMs,.95f)}\nFrame p95 ms: {Percentile(frameMs,.95f)}\nRequest completion p95 ms (not observable response): {Percentile(responseMs,.95f)}\nStatus: {Status}\n");
            File.AppendAllText(path,$"Observable source response p95 ms: {Percentile(observableMs,.95f)}\nObservable samples: {observableMs.Count}\nRender mode: {(smokeTarget!=null?"explicit offscreen 1920x1080":"window")}\n");
            Debug.Log("MotionBricks metrics: "+path);
        }
        void OnDestroy() {
            lifetime?.Cancel(); provider?.Dispose(); lifetime?.Dispose();
            if(links!=null) foreach(var link in links) if(link!=null) Destroy(link.gameObject);
            if(diagnosticMaterial!=null) Destroy(diagnosticMaterial);
            if(smokeTarget!=null) {smokeTarget.Release();Destroy(smokeTarget);}
            if(rootAxes!=null)foreach(var axis in rootAxes)if(axis!=null)Destroy(axis.gameObject);
            if(axisMaterials!=null)foreach(var material in axisMaterials)if(material!=null)Destroy(material);
            if(trajectory!=null)Destroy(trajectory.gameObject);
        }
    }
}
