using System;

namespace ShadowHabitat
{
    /// <summary>
    /// One shadow component received from Python.
    /// Coordinates are normalized in surface space: x,y in 0..1 (image origin: top-left).
    /// Area is fraction of surface (0..1). major/minor are normalized to max(W,H).
    /// </summary>
    [Serializable]
    public struct ShadowBlob
    {
        public int id;
        public float x;
        public float y;
        public float area;
        public float major;
        public float minor;
        public float angle;
        public float vx;
        public float vy;
        public float lastUpdateTime;
    }
}
