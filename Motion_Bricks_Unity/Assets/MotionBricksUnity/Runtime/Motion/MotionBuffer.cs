using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionBricksUnity
{
    public sealed class MotionBuffer
    {
        readonly SortedList<double, float[]> frames = new SortedList<double, float[]>();
        long lastId = -1;
        public int Count => frames.Count;
        public int Rejected { get; private set; }
        public double End => Count == 0 ? double.NegativeInfinity : frames.Keys[Count - 1];
        public void Clear() { frames.Clear(); lastId = -1; }
        public bool Splice(MotionResponse response, double now, long expectedId)
        {
            response.Validate();
            var end = response.contextTimestamp + (response.validFrameCount - 1) / response.frameRate;
            if (response.requestId != expectedId || response.requestId <= lastId || end <= now ||
                double.IsNaN(response.contextTimestamp) || double.IsInfinity(response.contextTimestamp)) { Rejected++; return false; }
            lastId = response.requestId;
            // Retain already played/history frames; splice at the intended model context time, never rewind the clock.
            double start = Math.Max(response.contextTimestamp, now);
            for (int i = Count - 1; i >= 0 && frames.Keys[i] >= start; --i) frames.RemoveAt(i);
            for (int i = 0; i < response.validFrameCount; ++i) {
                double time = response.contextTimestamp + i / (double)response.frameRate;
                if (time < now - .15) continue;
                var q = new float[36]; Array.Copy(response.qpos, i * 36, q, 0, 36); frames[time] = q;
            }
            while (Count > 80 || Count > 1 && frames.Keys[1] < now - .2) frames.RemoveAt(0);
            return true;
        }
        public bool Sample(double time, float[] result)
        {
            if (Count == 0) return false;
            if (time <= frames.Keys[0]) { Array.Copy(frames.Values[0], result, 36); return true; }
            if (time >= End) { Array.Copy(frames.Values[Count - 1], result, 36); return time <= End + 1e-5; }
            int lo = 0, hi = Count - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (frames.Keys[mid] <= time) lo = mid; else hi = mid; }
            float t = (float)((time - frames.Keys[lo]) / (frames.Keys[hi] - frames.Keys[lo]));
            var a = frames.Values[lo]; var b = frames.Values[hi];
            for (int j = 0; j < 36; ++j) result[j] = Mathf.Lerp(a[j], b[j], t);
            var rot = Quaternion.Slerp(new Quaternion(a[4], a[5], a[6], a[3]), new Quaternion(b[4], b[5], b[6], b[3]), t);
            result[3] = rot.w; result[4] = rot.x; result[5] = rot.y; result[6] = rot.z;
            return true;
        }
        public void Rebase(Vector3 modelOffset) {
            foreach (var q in frames.Values) { q[0] += modelOffset.x; q[1] += modelOffset.y; q[2] += modelOffset.z; }
        }
    }
}
