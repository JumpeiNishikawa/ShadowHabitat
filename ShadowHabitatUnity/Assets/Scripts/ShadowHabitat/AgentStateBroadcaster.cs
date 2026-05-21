using System.Collections.Generic;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Every frame, sends each registered agent's normalized-surface position
    /// and radius to Python so Python can mask the agent area out of shadow
    /// detection.
    ///
    /// OSC out:
    ///   /agent/state &lt;id:int&gt; &lt;surface:string&gt; &lt;x:float&gt; &lt;y:float&gt; &lt;radius:float&gt;
    ///
    /// Requires a uOSC.uOscClient on the same GameObject targeting Python (port 9001).
    /// Each ShapeAgent referenced here will be auto-registered.
    /// </summary>
    [RequireComponent(typeof(uOSC.uOscClient))]
    public class AgentStateBroadcaster : MonoBehaviour
    {
        public Surface surface;

        [Tooltip("Agents to broadcast. Each gets a stable id derived from its sibling index in this list.")]
        public List<ShapeAgent> agents = new ();

        [Tooltip("Sending interval in seconds (0 = every frame).")]
        public float sendInterval = 0f;

        uOSC.uOscClient _client;
        float _accum;

        [Header("Debug")]
        public bool verboseLogging = true;
        public float heartbeatInterval = 2.0f;

        int _sendsThisHeartbeat;
        int _skipNullAgent;
        int _skipDisabledAgent;
        int _skipInactiveGo;
        int _skipNoClient;
        int _skipNoSurface;
        float _heartbeatAccum;

        void OnEnable()
        {
            _client = GetComponent<uOSC.uOscClient>();
            if (verboseLogging)
            {
                Debug.Log($"[AgentStateBroadcaster:{name}] OnEnable. " +
                          $"client={(_client != null ? "OK" : "NULL")}  " +
                          $"surface={(surface != null ? surface.name : "NULL")}  " +
                          $"agents.Count={agents.Count}");
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    Debug.Log($"  agents[{i}] = " +
                              (a == null ? "NULL"
                               : $"{a.gameObject.name}  active={a.gameObject.activeInHierarchy}  enabled={a.enabled}"));
                }
            }
        }

        void Update()
        {
            if (_client == null) { _skipNoClient++; _accumHeartbeat(); return; }
            if (surface == null) { _skipNoSurface++; _accumHeartbeat(); return; }

            _accum += Time.deltaTime;
            if (sendInterval > 0f && _accum < sendInterval) { _accumHeartbeat(); return; }
            _accum = 0f;

            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null) { _skipNullAgent++; continue; }
                if (!agent.isActiveAndEnabled) { _skipDisabledAgent++; continue; }
                if (!agent.gameObject.activeInHierarchy) { _skipInactiveGo++; continue; }

                var n = surface.WorldToNormalized(agent.transform.position);
                float r = surface.WorldRadiusToNormalized(agent.bodyRadius);

                _client.Send("/agent/state",
                    i,
                    surface.surfaceId ?? "plane",
                    n.x,
                    n.y,
                    r);
                _sendsThisHeartbeat++;
            }
            _accumHeartbeat();
        }

        void _accumHeartbeat()
        {
            if (!verboseLogging || heartbeatInterval <= 0f) return;
            _heartbeatAccum += Time.deltaTime;
            if (_heartbeatAccum < heartbeatInterval) return;
            Debug.Log($"[AgentStateBroadcaster:{name}] HB sends={_sendsThisHeartbeat}  " +
                      $"skipNullAgent={_skipNullAgent}  skipDisabledAgent={_skipDisabledAgent}  " +
                      $"skipInactiveGo={_skipInactiveGo}  skipNoClient={_skipNoClient}  " +
                      $"skipNoSurface={_skipNoSurface}  agents.Count={agents.Count}");
            _heartbeatAccum = 0f;
            _sendsThisHeartbeat = 0;
            _skipNullAgent = _skipDisabledAgent = _skipInactiveGo = _skipNoClient = _skipNoSurface = 0;
        }
    }
}
