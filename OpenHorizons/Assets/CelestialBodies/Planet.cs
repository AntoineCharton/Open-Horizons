using System;
using CelestialBodies.Clouds.Rendering;
using CelestialBodies.Terrain;
using CelestialBodies.Sky;
using UnityEngine;

namespace CelestialBodies
{
    [ExecuteAlways]
    public class Planet : MonoBehaviour, IAtmosphereEffect
    {
        [SerializeField] private Surface surface;
        [SerializeField] internal AtmosphereGenerator sky;
        [SerializeField] private Cloud cloud;
        private TerrainDetails _details;

        private void Start()
        {
            _details = GetComponent<TerrainDetails>();
        }

        private void OnEnable()
        {
            sky.UpdateAtmosphereEffect(this);
            VolumetricCloudsUrp.VolumetricCloudsPass.RegisterPlanet(transform);
        }

        private void OnValidate()
        {
            sky.UpdateAtmosphereEffect(this);
            surface.Dirty();
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud, surface.shape.Radius);
            surface.SmartUpdate(transform, _details);
            if (Application.isPlaying)
                surface.SwitchToAsyncUpdate();
        }

        private void LateUpdate()
        {
            sky.SmartUpdate(transform, surface.shape.Radius);
            surface.UpdateMeshResolution();
        }

        private void OnDisable()
        {
            sky.CleanUpAtmosphere(this);
        }

        private void OnDestroy()
        {
            surface.Cleanup();
            surface.SwitchToParrallelUpdate();
        }

        public void AtmosphereActive(bool isActive)
        {
            if (sky.atmosphere.Visible != isActive)
                sky.atmosphere.Visible = isActive;
            sky.UpdateAtmosphereEffect(this);
        }

        public void CloudsActive(bool isActive)
        {
            cloud.Visible = isActive;
            if (!cloud.Visible && surface.highResolution != 256)
            {
                surface.highResolution = 256;
                surface.Dirty = true;
            }
        }

        public Bounds GetBounds()
        {
            return surface.GetBounds(transform);
        }

        public Material GetMaterial(Shader atmosphereShader)
        {
            return sky.GetMaterial(atmosphereShader);
        }

        public bool IsVisible(Plane[] cameraPlanes)
        {
            return sky.IsVisible(cameraPlanes, transform);
        }

        public GameObject GameObject
        {
            get => gameObject;
        }
    }
}