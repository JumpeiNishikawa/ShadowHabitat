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
        public List<ShapeAgent> agents = new();

        [Tooltip("Sending interval in seconds (0 = every frame).")]
        public float sendInterval = 0f;

        uOSC.uOscClient _client;
        float _accum;

        void Awake()
        {
            _client = GetComponent<uOSC.uOscClient>();
        }

        void Update()
        {
            if (_client == null || surface == null) return;
            _accum += Time.deltaTime;
            if (sendInterval > 0f && _accum < sendInterval) return;
            _accum = 0f;

            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || !agent.isActiveAndEnabled) continue;
                if (!agent.gameObject.activeInHierarchy) continue;

                var n = surface.WorldToNormalized(agent.transform.position);
                float r = surface.WorldRadiusToNormalized(agent.bodyRadius);

                _client.Send("/agent/state",
                    i,
                    surface.surfaceId ?? "plane",
                    n.x,
                    n.y,
                    r);
            }
        }
    }
}
