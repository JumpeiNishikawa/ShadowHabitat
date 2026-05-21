using System.Globalization;
using System.IO;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Writes one CSV row per FixedUpdate-ish tick capturing agent + nearest shadow + state.
    /// Matches spec §4.16 closely. File goes to Application.persistentDataPath/logs/.
    /// </summary>
    public class AgentCsvLogger : MonoBehaviour
    {
        public ShapeAgent agent;
        public ShadowColliderManager shadowManager;
        public string surfaceId = "plane";

        [Tooltip("Logging interval in seconds.")]
        public float logInterval = 0.1f;

        StreamWriter _writer;
        float _accum;

        void OnEnable()
        {
            string dir = Path.Combine(Application.persistentDataPath, "logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"agent_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            _writer = new StreamWriter(path) { NewLine = "\n" };
            _writer.WriteLine("time,surface,agent_id,agent_x,agent_y,agent_state,shadow_area,shadow_x,shadow_y,overlap,event");
            Debug.Log($"[AgentCsvLogger] writing to {path}");
        }

        void OnDisable()
        {
            try { _writer?.Flush(); _writer?.Close(); } catch { }
            _writer = null;
        }

        void Update()
        {
            if (_writer == null || agent == null) return;
            _accum += Time.deltaTime;
            if (_accum < logInterval) return;
            _accum = 0f;

            ShadowColliderTag nearest = null;
            float bestD = float.MaxValue;
            if (shadowManager != null)
            {
                foreach (var kv in shadowManager.ActiveTags)
                {
                    var tag = kv.Value;
                    if (tag == null) continue;
                    float d = Vector2.Distance(agent.transform.position, tag.transform.position);
                    if (d < bestD) { bestD = d; nearest = tag; }
                }
            }

            string ev = agent.State switch
            {
                ShapeAgent.CircleState.AvoidShadow => "ShadowNear",
                ShapeAgent.CircleState.Covered => "ShadowCovering",
                _ => "",
            };

            var ci = CultureInfo.InvariantCulture;
            var ap = agent.transform.position;
            string sa = nearest != null ? nearest.blob.area.ToString("0.######", ci) : "";
            string sx = nearest != null ? nearest.transform.position.x.ToString("0.####", ci) : "";
            string sy = nearest != null ? nearest.transform.position.y.ToString("0.####", ci) : "";
            float overlap = nearest != null
                ? Mathf.Clamp01(1f - bestD / Mathf.Max(0.0001f, nearest.radiusWorld + agent.bodyRadius))
                : 0f;

            _writer.WriteLine(string.Join(",",
                Time.timeAsDouble.ToString("0.####", ci),
                surfaceId,
                "agent_circle",
                ap.x.ToString("0.####", ci),
                ap.y.ToString("0.####", ci),
                agent.State.ToString(),
                sa, sx, sy,
                overlap.ToString("0.####", ci),
                ev));
        }
    }
}
