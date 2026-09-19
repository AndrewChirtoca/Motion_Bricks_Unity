using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MotionBricksUnity.Tests
{
    public class RuntimeTests
    {
        static IEnumerator Load(string name) {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/MotionBricksUnity/Samples/"+name+".unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(name);
#endif
        }
        [UnityTest] public IEnumerator CameraRelativeInputAndRapidReversalsGenerateMotion()
        {
            yield return Load("LocomotionDemo");yield return new WaitForSecondsRealtime(.5f);
            var demo=Object.FindFirstObjectByType<LocomotionDemo>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
#if UNITY_EDITOR
            var previousRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try {
                var start=demo.Skeleton.Positions[0];
                keyboard.MakeCurrent();
                InputState.Change(keyboard,new KeyboardState(Key.W,Key.LeftShift));

                Assert.IsTrue(keyboard.wKey.isPressed,"Injected keyboard state must be readable in PlayMode");
                yield return new WaitForSecondsRealtime(1);
                Assert.Greater((demo.Skeleton.Positions[0]-start).magnitude,.1f,demo.Status);
                for(int i=0;i<6;i++) {
                    InputState.Change(keyboard,new KeyboardState(i%2==0?Key.A:Key.D));

                    yield return new WaitForSecondsRealtime(.12f);
                }
                InputState.Change(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.4f);
                Assert.Greater(demo.Accepted,2,demo.Status);
                foreach(var p in demo.Skeleton.Positions)Assert.IsFalse(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z));
            } finally {
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=previousRouting;
#endif
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=previousBackground;
            }
        }
        [UnityTest] public IEnumerator RecordedSceneAdvancesWithoutLiveGeneration()
        {
            yield return Load("SourcePlayback");yield return new WaitForSecondsRealtime(.1f);
            var demo=Object.FindFirstObjectByType<LocomotionDemo>();Assert.IsTrue(demo.recordedPlayback);
            var start=demo.Skeleton.Positions[0];yield return new WaitForSecondsRealtime(2.5f);
            Assert.AreEqual(0,demo.Accepted);Assert.Greater((demo.Skeleton.Positions[0]-start).magnitude,.1f);
        }
        [UnityTest] public IEnumerator SceneLoadsAndGeneratesOnHuman()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/MotionBricksUnity/Samples/LocomotionDemo.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("LocomotionDemo");
#endif
            var demo=Object.FindFirstObjectByType<LocomotionDemo>();Assert.NotNull(demo);
            yield return new WaitForSecondsRealtime(6);
            Assert.Greater(demo.Accepted,1,demo.Status);Assert.AreEqual(0,demo.Underruns);
            Assert.IsTrue(demo.character.avatar.isValid);Assert.IsTrue(demo.character.avatar.isHuman);
            Assert.IsFalse(demo.character.enabled,"Animator must not compete with retargeting");
            Assert.NotNull(demo.Skeleton);
        }
        [UnityTest] public IEnumerator CancellationDoesNotLeaveProviderBusy()
        {
            using var provider=new PythonMotionGenerator();using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            var task=provider.InitializeAsync(cancelled.Token);while(!task.IsCompleted)yield return null;
            Assert.IsTrue(task.IsCanceled || task.IsFaulted);
            var reconnect=provider.InitializeAsync(CancellationToken.None);while(!reconnect.IsCompleted)yield return null;
            Assert.IsTrue(reconnect.IsCompletedSuccessfully,reconnect.Exception?.ToString());
        }
    }
}
