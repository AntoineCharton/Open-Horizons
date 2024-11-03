using CelestialBodies.Terrain;
using CelestialBodies.Sky;
using UnityEngine;

namespace CelestialBodies
{
    [ExecuteInEditMode]
    public class Planet : MonoBehaviour, IAtmosphereEffect
    {
        [SerializeField] private Surface surface;
        [SerializeField] internal AtmosphereGenerator sky;

        private void OnEnable()
        {
            AtmosphereRenderPass.RegisterEffect(this);
        }

        private void OnValidate()
        {
            surface.Dirty();
        }

        private void Update()
        {
            surface.LazyUpdate(transform);
        }

        private void LateUpdate()
        {
            sky.LazyUpdate(transform, surface.shape.Radius);
        }
        
        private void OnDisable()
        {
            sky.CleanUpAtmosphere(this);
        }

        private void OnDestroy()
        {
            surface.Cleanup();
        }

        public Material GetMaterial(Shader atmosphereShader)
        {
            return sky.GetMaterial(atmosphereShader);
        }

        public bool IsVisible(Plane[] cameraPlanes)
        {
            return sky.IsVisible(cameraPlanes, transform);
        }

        public float DistToAtmosphere(Vector3 pos)
        {
            return sky.DistToAtmosphere(transform, pos);
        }

        public GameObject GameObject
        {
            get => gameObject;
        }

    }

}