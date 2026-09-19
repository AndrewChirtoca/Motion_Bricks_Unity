using System;
using System.Threading;
using System.Threading.Tasks;

namespace MotionBricksUnity
{
    [Serializable] public class MotionRequest
    {
        public int protocolVersion = 1;
        public string operation = "generate";
        public string modelVersion = MotionConstants.Version;
        public long requestId;
        public double contextTimestamp, timeOrigin;
        public float[] context, movement, facing;
        public int mode, seed = 1234;
    }
    [Serializable] public class MotionResponse
    {
        public int protocolVersion, validFrameCount;
        public long requestId;
        public double contextTimestamp, timeOrigin;
        public string modelVersion, provider, error;
        public float frameRate, inferenceMs;
        public bool ready, targetPoseSupported;
        public float[] qpos;
        public bool MatchesTiming(double origin,double timestamp) =>
            Math.Abs(timeOrigin-origin)<=1e-6 && Math.Abs(contextTimestamp-timestamp)<=1e-6;
        public void Validate()
        {
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            if (protocolVersion != 1 || modelVersion != MotionConstants.Version || frameRate != 30)
                throw new InvalidOperationException("Provider contract mismatch");
            if(double.IsNaN(timeOrigin) || double.IsInfinity(timeOrigin) || double.IsNaN(contextTimestamp) || double.IsInfinity(contextTimestamp))
                throw new InvalidOperationException("Invalid response timing");
            if (validFrameCount < 4 || validFrameCount > 64 || qpos == null || qpos.Length != validFrameCount * 36)
                throw new InvalidOperationException("Invalid motion dimensions");
            foreach (var v in qpos) if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidOperationException("Non-finite pose");
            for (var i = 0; i < validFrameCount; ++i) {
                float norm = 0; for (var j = 3; j < 7; ++j) norm += qpos[i * 36 + j] * qpos[i * 36 + j];
                if (Math.Abs(norm - 1) > .02f) throw new InvalidOperationException("Invalid root quaternion");
            }
        }
    }
    public interface IMotionGenerator : IDisposable
    {
        string ProviderName { get; }
        bool SupportsTargetPose { get; }
        Task InitializeAsync(CancellationToken cancellation);
        Task<MotionResponse> GenerateAsync(MotionRequest request, CancellationToken cancellation);
    }
    public static class MotionConstants
    {
        public const string Version = "gear-sonic-6733128a3d8a523b1418b06bca3cdf61c8b0987f";
        public static float[] Rest() { var q = new float[36]; q[2] = .788740f; q[3] = 1; return q; }
    }
}
