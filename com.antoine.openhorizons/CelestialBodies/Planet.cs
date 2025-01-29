using CelestialBodies.Clouds.Rendering;
using CelestialBodies.Terrain;
using CelestialBodies.Sky;
using UnityEngine;

namespace CelestialBodies
{
    [ExecuteAlways]
    public class Planet : MonoBehaviour, IAtmosphereEffect
    {
        [SerializeField] internal Terrain.Terrain terrain = Terrain.Terrain.Default();
        [SerializeField] internal Trees trees = Trees.Default();
        [SerializeField] internal AtmosphereGenerator sky;
        [SerializeField] private Cloud cloud;
        [SerializeField] private Fog fog = Fog.Default();
        [SerializeField] private Ocean ocean;
        [SerializeField] private GameObject target;
        private TerrainGrass _terrainGrasses;
        private Transform _localPosition;

        private void Start()
        {
            _terrainGrasses = GetComponent<TerrainGrass>();
            if (Application.isPlaying)
            {
                if (target == null)
                {
                    target = Camera.main.transform.gameObject;
                }
                trees.Initialize();
                fog.Start(gameObject);
                _localPosition = new GameObject("LocalPosition").transform;
                _localPosition.transform.parent = transform;
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
            terrain.Dirty();
            ocean.Dirty();
            if (Application.isPlaying)
            {
                fog.UpdateFogSettings();
            }
        }

        private void Update()
        {
           
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud, terrain.Surface.shape.Radius);
            terrain.SmartUpdate(transform, ref trees, _terrainGrasses);
            trees.UpdateDetails(transform);
            ocean.SmartUpdate(transform);
            if (Application.isPlaying)
            {
                _localPosition.position = target.transform.position;
                trees.GenerateInteractable(_localPosition.localPosition, transform);
                fog.Update(terrain.Surface.shape.Radius);
                terrain.SwitchToAsyncUpdate();
            }
        }

        private void LateUpdate()
        {
            sky.SmartUpdate(transform, terrain.Surface.shape.Radius);
            terrain.UpdateMeshResolution(target.transform.position);
        }

        private void OnDisable()
        {
            sky.CleanUpAtmosphere(this);
        }

        private void OnDestroy()
        {
            if(Application.isPlaying)
                fog.Destroy();
            terrain.Cleanup();
            trees.CleanUp();
            terrain.SwitchToParrallelUpdate();
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
            if (!cloud.Visible && terrain.Surface.highResolution != 256)
            {
                var newSurface = terrain.Surface;
                newSurface.highResolution = 256;
                newSurface.highResolution = 256;
                newSurface.Dirty = true;
                terrain.SetSurface(newSurface);
            }
        }

        public void OceanLOD(bool isLOD)
        {
            ocean.SetLOD(isLOD);
        }

        public Bounds GetBounds()
        {
            return terrain.Surface.GetBounds(transform);
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