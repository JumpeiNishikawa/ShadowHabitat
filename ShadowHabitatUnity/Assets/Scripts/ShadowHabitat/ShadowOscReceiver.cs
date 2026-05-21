using System.Collections.Generic;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Receives shadow blobs from Python via uOSC.
    /// Add a uOSC.uOscServer component to the same GameObject (port matches config.json).
    /// </summary>
    [RequireComponent(typeof(uOSC.uOscServer))]
    public class ShadowOscReceiver : MonoBehaviour
    {
        public string expectedSurfaceId = "plane";

        readonly Dictionary<int, ShadowBlob> _blobs = new();
        readonly List<ShadowBlob> _pending = new();
        int _pendingCount = 0;
        bool _inFrame = false;
        string _frameSurface = "";
        readonly object _lock = new();

        public int CurrentFrame { get; private set; }

        void Awake()
        {
            var srv = GetComponent<uOSC.uOscServer>();
            srv.onDataReceived.AddListener(OnDataReceived);
        }

        void OnDataReceived(uOSC.Message msg)
        {
            switch (msg.address)
            {
                case "/shadow/begin":
                    if (msg.values.Length >= 3)
                    {
                        _frameSurface = msg.values[0].GetString();
                        _pendingCount = (int)msg.values[1].GetInt();
                        _pending.Clear();
                        _inFrame = true;
                    }
                    break;

                case "/shadow/blob":
                    if (_inFrame && msg.values.Length >= 9)
                    {
                        var b = new ShadowBlob
                        {
                            id     = (int)msg.values[0].GetInt(),
                            x      = msg.values[1].GetFloat(),
                            y      = msg.values[2].GetFloat(),
                            area   = msg.values[3].GetFloat(),
                            major  = msg.values[4].GetFloat(),
                            minor  = msg.values[5].GetFloat(),
                            angle  = msg.values[6].GetFloat(),
                            vx     = msg.values[7].GetFloat(),
                            vy     = msg.values[8].GetFloat(),
                            lastUpdateTime = Time.unscaledTime,
                        };
                        _pending.Add(b);
                    }
                    break;

                case "/shadow/end":
                    if (_inFrame && (string.IsNullOrEmpty(expectedSurfaceId) ||
                                     _frameSurface == expectedSurfaceId))
                    {
                        lock (_lock)
                        {
                            _blobs.Clear();
                            foreach (var b in _pending) _blobs[b.id] = b;
                            if (msg.values.Length >= 1) CurrentFrame = (int)msg.values[0].GetInt();
                        }
                    }
                    _inFrame = false;
                    _pending.Clear();
                    break;
            }
        }

        public List<ShadowBlob> SnapshotBlobs()
        {
            lock (_lock)
            {
                var copy = new List<ShadowBlob>(_blobs.Count);
                foreach (var b in _blobs.Values) copy.Add(b);
                return copy;
            }
        }

        public int BlobCount
        {
            get { lock (_lock) { return _blobs.Count; } }
        }
    }

    static class OscValueExt
    {
        public static string GetString(this object o) => o switch
        {
            string s => s,
            _ => o?.ToString() ?? ""
        };

        public static int GetInt(this object o) => o switch
        {
            int i => i,
            long l => (int)l,
            float f => (int)f,
            double d => (int)d,
            _ => 0
        };

        public static float GetFloat(this object o) => o switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            _ => 0f
        };
    }
}
