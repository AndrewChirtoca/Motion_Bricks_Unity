using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MotionBricksUnity
{
    public sealed class PythonMotionGenerator : IMotionGenerator
    {
        const int Limit = 256 * 1024;
        readonly int port;
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        public string ProviderName => "Python ONNX Runtime CPU (loopback)";
        public bool SupportsTargetPose => false;
        public PythonMotionGenerator(int port = 17861) { this.port = port; }
        public async Task InitializeAsync(CancellationToken token)
        {
            var response = await Exchange(new MotionRequest { operation = "ready" }, token);
            if (!response.ready || response.modelVersion != MotionConstants.Version || response.protocolVersion != 1)
                throw new InvalidOperationException(response.error ?? "Provider not ready");
        }
        public async Task<MotionResponse> GenerateAsync(MotionRequest request, CancellationToken token)
        {
            var result = await Exchange(request, token); result.Validate(); return result;
        }
        Task<MotionResponse> Exchange(MotionRequest request, CancellationToken token) => Task.Run(async () => {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            linked.CancelAfter(3000);
            if (!await gate.WaitAsync(0, linked.Token)) throw new InvalidOperationException("A request is already outstanding");
            try {
                using var client = new TcpClient { NoDelay = true };
                using var registration = linked.Token.Register(() => client.Close());
                await client.ConnectAsync("127.0.0.1", port);
                using var stream = client.GetStream();
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
                if (bytes.Length > Limit) throw new InvalidDataException("Request too large");
                var header = BitConverter.GetBytes(bytes.Length);
                await stream.WriteAsync(header, 0, 4, linked.Token);
                await stream.WriteAsync(bytes, 0, bytes.Length, linked.Token);
                await ReadExact(stream, header, linked.Token);
                var length = BitConverter.ToInt32(header, 0);
                if (length <= 0 || length > Limit) throw new InvalidDataException("Response length out of bounds");
                bytes = new byte[length]; await ReadExact(stream, bytes, linked.Token);
                return JsonUtility.FromJson<MotionResponse>(Encoding.UTF8.GetString(bytes));
            } finally { gate.Release(); }
        }, token);
        static async Task ReadExact(Stream stream, byte[] bytes, CancellationToken token)
        {
            for (int read = 0; read < bytes.Length;) {
                var n = await stream.ReadAsync(bytes, read, bytes.Length - read, token);
                if (n == 0) throw new EndOfStreamException(); read += n;
            }
        }
        public void Dispose() { lifetime.Cancel(); }
    }
}
