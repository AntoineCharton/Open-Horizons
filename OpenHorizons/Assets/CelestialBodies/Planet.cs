using CelestialBodies.Clouds.Rendering;
using CelestialBodies.Terrain;
using CelestialBodies.Sky;
using UnityEngine;

namespace CelestialBodies
{
    [ExecuteAlways]
    public class Planet : MonoBehaviour, IAtmosphereEffect
    {
        [SerializeField] internal TerrainSurface terrainSurface = TerrainSurface.Default();
        [SerializeField] internal TerrainDetails terrainDetails = TerrainDetails.Default();
        [SerializeField] internal AtmosphereGenerator sky;
        [SerializeField] private Cloud cloud;
        [SerializeField] private Fog fog = Fog.Default();
        [SerializeField] private Ocean ocean;
        private MeshDetail _meshDetails;

        private void Start()
        {
            _meshDetails = GetComponent<MeshDetail>();
            if (Application.isPlaying)
            {
                terrainDetails.Initialize();
                fog.Start(gameObject);
            }
        }

        private void OnEnable()
        {
            sky.UpdateAtmosphereEffect(this);
            VolumetricCloudsUrp.VolumetricCloudsPass.RegisterPlanet(transform);
        }

        private void OnValidate()
        {
            sky.UpdateAtmosphereEffect(this);
            terrainSurface.Dirty();
            ocean.Dirty();
            if (Application.isPlaying)
            {
                fog.UpdateFogSettings();
            }
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud, terrainSurface.Surface.shape.Radius);
            terrainSurface.SmartUpdate(transform, ref terrainDetails, _meshDetails);
            terrainDetails.UpdateDetails(transform);
            ocean.SmartUpdate(transform);
            if (Application.isPlaying)
            {
                fog.Update(terrainSurface.Surface.shape.Radius);
                terrainSurface.SwitchToAsyncUpdate();
            }
        }

        private void LateUpdate()
        {
            sky.SmartUpdate(transform, terrainSurface.Surface.shape.Radius);
            terrainSurface.UpdateMeshResolution();
        }

        private void OnDisable()
        {
            sky.CleanUpAtmosphere(this);
        }

        private void OnDestroy()
        {
            if(Application.isPlaying)
                fog.Destroy();
            terrainSurface.Cleanup();
            terrainDetails.CleanUp();
            terrainSurface.SwitchToParrallelUpdate();
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
            if (!cloud.Visible && terrainSurface.Surface.highResolution != 256)
            {
                var newSurface = terrainSurface.Surface;
                newSurface.highResolution = 256;
                newSurface.highResolution = 256;
                newSurface.Dirty = true;
                terrainSurface.SetSurface(newSurface);
            }
        }

        public Bounds GetBounds()
        {
            return terrainSurface.Surface.GetBounds(transform);
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