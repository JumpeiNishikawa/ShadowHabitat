using UnityEngine;

namespace ShadowHabitat
{
    /// <summary>Marker component on each spawned shadow GameObject. Exposes the blob + collider.</summary>
    public class ShadowColliderTag : MonoBehaviour
    {
        public ShadowBlob blob;
        public float radiusWorld;
        public CircleCollider2D collider2d;
        public Transform debugVisual;
    }
}
