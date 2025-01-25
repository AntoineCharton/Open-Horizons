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
        [SerializeField] 
        internal Material oceanLodMaterial;

        internal bool isLOD;

        public void SetSurface(Surface surface)
        {
            this.surface = surface;
        }
    }

    public static class OceanBuilder
    {
        internal static void SetLOD(this ref Ocean ocean, bool isLOD)
        {
            if (isLOD && ocean.isLOD != isLOD)
            {
                ocean.isLOD = isLOD;
                for (var i = 0; i < ocean.surface.MeshRenderers.Length; i++)
                {
                    ocean.surface.MeshRenderers[i].material = ocean.oceanLodMaterial;
                }
            }else if (ocean.isLOD != isLOD)
            {
                ocean.isLOD = isLOD;
                for (var i = 0; i < ocean.surface.MeshRenderers.Length; i++)
                {
                    ocean.surface.MeshRenderers[i].material = ocean.oceanMaterial;
                }
            }
        }
    }
}
