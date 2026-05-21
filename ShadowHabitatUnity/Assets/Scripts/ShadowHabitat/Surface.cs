using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Maps normalized surface coordinates (0..1, image-space, y-down)
    /// to Unity world coordinates (XY plane, y-up).
    /// Place this on an empty GameObject sitting at the top-left corner of
    /// the projected area, with widthWorld/heightWorld matching the projected size.
    /// </summary>
    public class Surface : MonoBehaviour
    {
        public string surfaceId = "plane";

        [Tooltip("Width of the projected area in world units.")]
        public float widthWorld = 10f;

        [Tooltip("Height of the projected area in world units.")]
        public float heightWorld = 5.625f;

        public Vector3 NormalizedToWorld(float nx, float ny)
        {
            // Surface origin (this transform) is the TOP-LEFT corner.
            // Image y grows downward, Unity y grows upward.
            var p = transform.position;
            return new Vector3(
                p.x + nx * widthWorld,
                p.y - ny * heightWorld,
                p.z);
        }

        public Vector2 WorldToNormalized(Vector3 world)
        {
            var p = transform.position;
            float nx = (world.x - p.x) / Mathf.Max(1e-5f, widthWorld);
            float ny = (p.y - world.y) / Mathf.Max(1e-5f, heightWorld);
            return new Vector2(nx, ny);
        }

        public float WorldRadiusToNormalized(float radiusWorld)
        {
            float maxWH = Mathf.Max(widthWorld, heightWorld);
            return radiusWorld / Mathf.Max(1e-5f, maxWH);
        }

        public Vector2 NormalizedSizeToWorld(float majorNorm, float minorNorm)
        {
            // major/minor were normalized by max(W,H) in Python.
            float maxWH = Mathf.Max(widthWorld, heightWorld);
            return new Vector2(majorNorm * maxWH, minorNorm * maxWH);
        }

        public float NormalizedRadiusFromArea(float areaNorm)
        {
            // area = fraction of surface area
            float areaWorld = areaNorm * widthWorld * heightWorld;
            return Mathf.Sqrt(Mathf.Max(0f, areaWorld) / Mathf.PI);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var p = transform.position;
            Vector3 tl = p;
            Vector3 tr = p + new Vector3(widthWorld, 0, 0);
            Vector3 br = p + new Vector3(widthWorld, -heightWorld, 0);
            Vector3 bl = p + new Vector3(0, -heightWorld, 0);
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);
        }
#endif
    }
}
