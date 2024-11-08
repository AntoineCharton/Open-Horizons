using CelestialBodies.Clouds.Rendering;
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
        [SerializeField] private Cloud cloud;

        private void OnEnable()
        {
            if (sky.atmosphere.Enabled)
                AtmosphereRenderPass.RegisterEffect(this);
            else 
                AtmosphereRenderPass.RemoveEffect(this);
            VolumetricCloudsUrp.VolumetricCloudsPass.RegisterPlanet(transform);
        }

        private void OnValidate()
        {
            if (sky.atmosphere.Enabled)
                AtmosphereRenderPass.RegisterEffect(this);
            else 
                AtmosphereRenderPass.RemoveEffect(this);
            surface.Dirty();
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud);
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

        public bool IsActive()
        {
            return sky.atmosphere.Enabled;
        }

        public GameObject GameObject
        {
            get => gameObject;
        }

    }

}