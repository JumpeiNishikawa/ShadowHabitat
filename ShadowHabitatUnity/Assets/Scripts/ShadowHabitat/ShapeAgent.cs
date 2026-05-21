using System.Collections.Generic;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Stage 1 circle agent.
    /// Behaviour:
    ///   - Wander gently when no shadow is close.
    ///   - If a shadow is within avoidRadius, steer away (sum of repulsion vectors).
    ///   - If a shadow center overlaps the agent (coverRatio above threshold), shiver / stop.
    /// Coordinates run in Unity world XY (uses Surface for clamping).
    /// </summary>
    public class ShapeAgent : MonoBehaviour
    {
        public Surface surface;
        public ShadowColliderManager shadowManager;

        [Header("Locomotion")]
        public float maxSpeed = 2.5f;
        public float maxAccel = 8f;
        public float bodyRadius = 0.4f;

        [Header("Wander")]
        public float wanderJitter = 1.2f;
        public float wanderTimescale = 1.5f;
        [Tooltip("Re-pick a wander target when the agent gets this close (world units).")]
        public float wanderArriveDistance = 0.5f;
        [Tooltip("Minimum speed while wandering — keeps the agent visibly in motion.")]
        public float wanderMinSpeed = 1.0f;

        [Header("Debug")]
        public bool verboseLogging = true;
        [Tooltip("Throws every Update — set false to silence after diagnostics.")]
        public float diagnosticLogInterval = 1.0f;

        [Header("Shadow avoidance")]
        public float avoidRadius = 2.2f;
        public float avoidStrength = 6f;

        [Header("Cover (paralysis) reaction")]
        [Tooltip("If a shadow center is within this distance of the agent, consider the agent 'covered'.")]
        public float coverDistance = 0.5f;
        public float shiverAmplitude = 0.03f;
        public float shiverFrequency = 30f;

        public enum CircleState { Wander, AvoidShadow, Covered }
        public CircleState State { get; private set; } = CircleState.Wander;

        Vector2 _velocity;
        Vector2 _wanderTarget;
        float _wanderTimer;
        Vector3 _restPosition;
        float _diagAccum;
        int _frameCount;

        void Start()
        {
            _restPosition = transform.position;
            PickNewWanderTarget();
            if (verboseLogging)
            {
                Debug.Log($"[ShapeAgent:{name}] Start. pos={transform.position}  surface={(surface != null ? surface.name : "NULL")}  shadowManager={(shadowManager != null ? shadowManager.name : "NULL")}  firstTarget={_wanderTarget}");
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 pos = transform.position;
            Vector2 desired = Vector2.zero;
            CircleState newState = CircleState.Wander;

            // 1. Avoidance + cover detection
            float coverAccum = 0f;
            if (shadowManager != null)
            {
                foreach (var kv in shadowManager.ActiveTags)
                {
                    var tag = kv.Value;
                    if (tag == null) continue;
                    Vector2 sp = (Vector2)tag.transform.position;
                    Vector2 delta = pos - sp;
                    float dist = delta.magnitude;
                    float influence = avoidRadius + tag.radiusWorld;

                    if (dist < coverDistance + tag.radiusWorld * 0.5f)
                    {
                        coverAccum += 1f;
                    }
                    if (dist < influence && dist > 1e-4f)
                    {
                        float falloff = 1f - (dist / influence);
                        Vector2 away = delta / dist;
                        desired += away * avoidStrength * falloff;
                        newState = CircleState.AvoidShadow;
                    }
                }
            }

            if (coverAccum > 0f)
            {
                newState = CircleState.Covered;
            }

            // 2. Wander when nothing pushes
            if (newState == CircleState.Wander)
            {
                _wanderTimer -= dt;
                Vector2 toWander = _wanderTarget - pos;
                if (_wanderTimer <= 0f || toWander.magnitude < wanderArriveDistance)
                {
                    PickNewWanderTarget();
                    toWander = _wanderTarget - pos;
                }
                Vector2 wanderForce = Vector2.ClampMagnitude(toWander * 2f, maxSpeed);
                if (wanderForce.magnitude < wanderMinSpeed && toWander.sqrMagnitude > 1e-6f)
                {
                    wanderForce = toWander.normalized * wanderMinSpeed;
                }
                desired += wanderForce;
            }

            // 3. Boundary repulsion
            desired += BoundaryRepel(pos);

            // 4. Integrate
            Vector2 targetVel;
            if (newState == CircleState.Covered)
            {
                targetVel = Vector2.zero;
            }
            else
            {
                targetVel = Vector2.ClampMagnitude(desired, maxSpeed);
            }
            Vector2 accel = Vector2.ClampMagnitude((targetVel - _velocity) / Mathf.Max(0.001f, dt), maxAccel);
            _velocity += accel * dt;

            Vector2 next = pos + _velocity * dt;
            next = ClampToSurface(next);

            // 5. Shiver while covered
            if (newState == CircleState.Covered)
            {
                float t = Time.time * shiverFrequency;
                next += new Vector2(Mathf.Sin(t), Mathf.Cos(t * 1.3f)) * shiverAmplitude;
            }

            transform.position = new Vector3(next.x, next.y, transform.position.z);
            State = newState;

            _frameCount++;
            if (verboseLogging && diagnosticLogInterval > 0f)
            {
                _diagAccum += dt;
                if (_diagAccum >= diagnosticLogInterval)
                {
                    _diagAccum = 0f;
                    Debug.Log($"[ShapeAgent:{name}] state={State}  pos={(Vector2)transform.position}  vel={_velocity}  target={_wanderTarget}  framesSinceStart={_frameCount}");
                }
            }
        }

        void PickNewWanderTarget()
        {
            _wanderTimer = wanderTimescale * (0.5f + Random.value);
            if (surface == null)
            {
                _wanderTarget = (Vector2)_restPosition + new Vector2(
                    Random.Range(-wanderJitter, wanderJitter),
                    Random.Range(-wanderJitter, wanderJitter));
                return;
            }
            float nx = Random.Range(0.15f, 0.85f);
            float ny = Random.Range(0.15f, 0.85f);
            _wanderTarget = surface.NormalizedToWorld(nx, ny);
        }

        Vector2 BoundaryRepel(Vector2 pos)
        {
            if (surface == null) return Vector2.zero;
            Vector3 origin = surface.transform.position;
            float left = origin.x;
            float right = origin.x + surface.widthWorld;
            float top = origin.y;
            float bottom = origin.y - surface.heightWorld;

            float margin = 0.6f;
            Vector2 force = Vector2.zero;
            if (pos.x - left < margin) force.x += (margin - (pos.x - left));
            if (right - pos.x < margin) force.x -= (margin - (right - pos.x));
            if (top - pos.y < margin) force.y -= (margin - (top - pos.y));
            if (pos.y - bottom < margin) force.y += (margin - (pos.y - bottom));
            return force * 4f;
        }

        Vector2 ClampToSurface(Vector2 p)
        {
            if (surface == null) return p;
            Vector3 origin = surface.transform.position;
            float left = origin.x + bodyRadius;
            float right = origin.x + surface.widthWorld - bodyRadius;
            float top = origin.y - bodyRadius;
            float bottom = origin.y - surface.heightWorld + bodyRadius;
            return new Vector2(
                Mathf.Clamp(p.x, left, right),
                Mathf.Clamp(p.y, bottom, top));
        }
    }
}
