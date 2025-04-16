using System;
using System.Collections.Generic;
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
        private ITerrainDetails [] _terrainDetails;
        private Transform _localPosition;

        private void Start()
        {
            _terrainDetails = GetComponents<ITerrainDetails>();
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

        public bool IsInitialized()
        {
            return terrain.IsInitialized();
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud, terrain.Surface.shape.Radius);
            terrain.SmartUpdate(transform, ref trees, _terrainDetails);
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

        public Vector3 GetFirstPointAboveOcean(float offset)
        {
            return terrain.GetFirstPointAboveOcean(ocean, offset);
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
        }

        public void SetNoisePosition(Vector3 position)
        {
            var Noises = terrain.Surface.shape.NoiseSettings;
            for (var i = 0; i < Noises.Length; i++)
            {
                var noiseSettings = Noises[i].NoiseSettings;
                noiseSettings.Center = position;
                Noises[i].NoiseSettings = noiseSettings;
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

        public void SetTint(Gradient gradient)
        {
            terrain.Tint = gradient;
        }
        
        public void SetTexture(Texture2D texture)
        {
            terrain.Texture = texture;
        }
        
        public void SetTextureNormal(Texture2D texture)
        {
            terrain.TextureNormal = texture;
        }

        public void SetTexturBlend(float blend)
        {
            terrain.TextureBlend = blend;
        }

        public void SetStepColor(Color color)
        {
            terrain.StepColor = color;
        }

        public void SetScateringTint(Color color)
        {
            cloud.ScatteringTint = color;
        }

        public void SetSurfaceNoise(NoiseLayer[] noiseLayer)
        {
            var surface = terrain.Surface;
            var shape = surface.shape;
            shape.NoiseSettings = noiseLayer;
            surface.shape = shape;
            terrain.Surface = surface;
        }

        public void AddFlatModifier(Vector3 position, float distance, float easeDistance)
        {
            terrain.AddFlatModifier(position, distance, easeDistance);
        }

        public void AddRemoveTreeModifier(Vector3 position, float distance)
        {
            trees.RemoveModifier(position, distance);
        }

        public void SetReferences(List<Reference> references)
        {
            trees.references = references;
        }
        
        public void OverrideReferences(List<ReferenceOverride> references)
        {
            if (trees.references.Count == references.Count)
            {
                for (int i = 0; i < references.Count; i++)
                {
                    if (references[i].Reference != null)
                    {
                        trees.references[i].reference = references[i].Reference;
                        trees.references[i].interactableReference = references[i].Interactable;
                    }
                }
            }
            else
            {
                Debug.LogWarning("Reference must be the same");
            }
            
        }
        
        public void SetCloud(float startCloud, float endCloud, float densityMultiplier, float shapeFactor, float shapeScale)
        {
            cloud.BottomAltitude = startCloud;
            cloud.AltitudeRange = endCloud;
            cloud.DensityMultipler = densityMultiplier;
            cloud.ShapeFactor = shapeFactor;
            cloud.ShapeScale = shapeScale;
        }
        
        public GameObject GameObject
        {
            get => gameObject;
        }
    }
    
    [Serializable]
    public class ReferenceOverride
    {
        [SerializeField] internal GameObject Reference;
        [SerializeField] internal GameObject Interactable;
    }
}