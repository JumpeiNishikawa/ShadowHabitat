using System.Collections.Generic;
using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>
    /// Spawns / updates / despawns invisible CircleCollider2D objects, one per shadow blob.
    /// Each spawned GameObject also exposes the source ShadowBlob via ShadowColliderTag so
    /// agents can read radius, velocity, etc.
    /// </summary>
    public class ShadowColliderManager : MonoBehaviour
    {
        public ShadowOscReceiver receiver;
        public Surface surface;

        [Tooltip("Show a semi-transparent disk for each shadow (Editor + Play).")]
        public bool debugVisible = true;

        [Tooltip("Material/color for the debug disk. If null, a default red disk is created.")]
        public Color debugColor = new(1f, 0.2f, 0.2f, 0.35f);

        [Tooltip("Despawn a collider if its blob hasn't updated for this many seconds.")]
        public float blobTimeout = 0.4f;

        [Tooltip("Minimum radius (world units) for collider/visual.")]
        public float minRadius = 0.1f;

        readonly Dictionary<int, ShadowColliderTag> _tags = new();

        void Update()
        {
            if (receiver == null || surface == null) return;

            var blobs = receiver.SnapshotBlobs();
            var seen = new HashSet<int>();

            foreach (var b in blobs)
            {
                seen.Add(b.id);
                if (!_tags.TryGetValue(b.id, out var tag))
                {
                    tag = CreateColliderObject(b.id);
                    _tags[b.id] = tag;
                }
                UpdateTag(tag, b);
            }

            // Remove stale
            var toRemove = new List<int>();
            float now = Time.unscaledTime;
            foreach (var kv in _tags)
            {
                if (!seen.Contains(kv.Key) && now - kv.Value.blob.lastUpdateTime > blobTimeout)
                    toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove)
            {
                if (_tags[id] != null) Destroy(_tags[id].gameObject);
                _tags.Remove(id);
            }
        }

        public IReadOnlyDictionary<int, ShadowColliderTag> ActiveTags => _tags;

        ShadowColliderTag CreateColliderObject(int id)
        {
            var go = new GameObject($"ShadowBlob_{id}");
            go.transform.SetParent(transform, worldPositionStays: true);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var tag = go.AddComponent<ShadowColliderTag>();
            tag.collider2d = col;

            if (debugVisible)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(visual.GetComponent<Collider>());
                visual.name = "debug_visual";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0, 0, 0.01f);
                var mr = visual.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = debugColor;
                mr.sharedMaterial = mat;
                tag.debugVisual = visual.transform;
            }
            return tag;
        }

        void UpdateTag(ShadowColliderTag tag, ShadowBlob b)
        {
            tag.blob = b;
            var pos = surface.NormalizedToWorld(b.x, b.y);
            tag.transform.position = pos;
            float r = Mathf.Max(minRadius, surface.NormalizedRadiusFromArea(b.area));
            tag.radiusWorld = r;
            if (tag.collider2d != null) tag.collider2d.radius = r;
            if (tag.debugVisual != null) tag.debugVisual.localScale = new Vector3(r * 2f, r * 2f, 1f);
        }
    }
}
