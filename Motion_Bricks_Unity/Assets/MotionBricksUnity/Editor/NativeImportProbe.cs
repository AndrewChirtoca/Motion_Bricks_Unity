using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;

namespace MotionBricksUnity.Editor
{
    public static class NativeImportProbe
    {
        [Serializable] class Fixture { public float[] context, expected, movement, facing; public int validFrameCount,mode; }
        public static void Run()
        {
            string result;
            try {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.InferenceEngine.Editor.Onnx.ONNXModelConverter")).First(t => t != null);
                var converter = Activator.CreateInstance(type, new object[] { Path.Combine(EnvironmentValidation.Root, "Models/planner_sonic.onnx") });
                var model = (Model)type.GetMethod("Convert", BindingFlags.Instance | BindingFlags.Public).Invoke(converter, null);
                result = "Import succeeded.\n";
                var backend=Environment.GetCommandLineArgs().Contains("-nativeGpu")?BackendType.GPUCompute:BackendType.CPU;
                using var worker=new Worker(model,backend);
                foreach(var fixturePath in Directory.GetFiles(Path.Combine(EnvironmentValidation.Root,"Artifacts/reference"),"native-*.json")) {
                var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(fixturePath));
                var tensors=new List<Tensor>();
                void Float(string name,TensorShape shape,float[] data){var tensor=new Tensor<float>(shape,data);tensors.Add(tensor);worker.SetInput(name,tensor);}
                void Int(string name,TensorShape shape,int[] data){var tensor=new Tensor<int>(shape,data);tensors.Add(tensor);worker.SetInput(name,tensor);}
                try {
                    Float("context_mujoco_qpos",new TensorShape(1,4,36),fixture.context);
                    Float("target_vel",new TensorShape(1),new[]{-1f});Int("mode",new TensorShape(1),new[]{fixture.mode});
                    Float("movement_direction",new TensorShape(1,3),fixture.movement ?? new[]{0f,0f,0f});Float("facing_direction",new TensorShape(1,3),fixture.facing ?? new[]{1f,0f,0f});
                    Int("random_seed",new TensorShape(1),new[]{1234});Int("has_specific_target",new TensorShape(1,1),new[]{0});
                    Float("specific_target_positions",new TensorShape(1,4,3),new float[12]);Float("specific_target_headings",new TensorShape(1,4),new float[4]);
                    Int("allowed_pred_num_tokens",new TensorShape(1,11),new[]{1,1,1,1,1,1,0,0,0,0,0});Float("height",new TensorShape(1),new[]{-1f});
                    var clock=System.Diagnostics.Stopwatch.StartNew();worker.Schedule();
                    using var output=((Tensor<float>)worker.PeekOutput("mujoco_qpos")).ReadbackAndClone();
                    using var count=((Tensor<int>)worker.PeekOutput("num_pred_frames")).ReadbackAndClone();
                    var data=output.DownloadToArray();var n=count.DownloadToArray()[0];float max=0;
                    for(int i=0;i<fixture.expected.Length;++i)max=Mathf.Max(max,Mathf.Abs(data[i]-fixture.expected[i]));
                    result+=$"Fixture: {Path.GetFileName(fixturePath)} Backend: {backend}\nFirst execution+readback ms: {clock.Elapsed.TotalMilliseconds}\nCount: {n} reference: {fixture.validFrameCount}\nMax absolute error: {max}\n";
                    if(n!=fixture.validFrameCount || max>1e-4f)result+="PARITY FAILED at 1e-4 tolerance.\n";
                    else result+="PARITY PASSED at 1e-4 tolerance.\n";
                    for(int repeat=0;repeat<5;repeat++) {
                        clock.Restart();worker.Schedule();var scheduleMs=clock.Elapsed.TotalMilliseconds;
                        using var warm=((Tensor<float>)worker.PeekOutput("mujoco_qpos")).ReadbackAndClone();
                        result+=$"Warm schedule ms: {scheduleMs}; total+readback ms: {clock.Elapsed.TotalMilliseconds}\n";
                    }
                } finally { foreach(var tensor in tensors)tensor.Dispose(); }
                }
            } catch (Exception e) { result = (e.InnerException ?? e).ToString(); }
            File.WriteAllText(Path.Combine(EnvironmentValidation.Root, "Artifacts/native-import.txt"), result);
            File.WriteAllText(Path.Combine(EnvironmentValidation.Root, Environment.GetCommandLineArgs().Contains("-nativeGpu")?"Artifacts/native-gpu.txt":"Artifacts/native-cpu.txt"),result);
            Debug.Log(result);
        }
    }
}
