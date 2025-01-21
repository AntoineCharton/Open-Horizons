using System;
using CelestialBodies.Terrain;
using UnityEngine;

namespace CelestialBodies
{
    [Serializable]
    struct Ocean
    {
        [SerializeField] internal Surface surface;
        [SerializeField] 
        internal Material oceanMaterial;

        public void SetSurface(Surface surface)
        {
            this.surface = surface;
        }
    }

    static class OceanBuilder
    {
    }
}
