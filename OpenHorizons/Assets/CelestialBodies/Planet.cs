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

        private void Awake()
        {
            surface.CleanUpMeshes();
            surface.InitializeSurface(transform);
            surface.SmartUpdate(transform);
        }

        private void OnEnable()
        {
            UpdateAtmosphereEffect();
            VolumetricCloudsUrp.VolumetricCloudsPass.RegisterPlanet(transform);
        }

        private void OnValidate()
        {
            UpdateAtmosphereEffect();
            surface.Dirty();
        }

        void UpdateAtmosphereEffect()
        {
            var LODMesh = sky.GetLodMesh(transform).gameObject;
            if (sky.atmosphere.Enabled && sky.atmosphere.Visible)
            {
                if(LODMesh.activeInHierarchy)
                    LODMesh.SetActive(false);
                AtmosphereRenderPass.RegisterEffect(this);
            }
            else
            {
                if(!LODMesh.activeInHierarchy)
                    LODMesh.SetActive(true);
                AtmosphereRenderPass.RemoveEffect(this);
            }
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud, surface.shape.Radius);
            surface.SmartUpdate(transform);
        }

        private void LateUpdate()
        {
            sky.SmartUpdate(transform, surface.shape.Radius);
            if(surface.allFacesGenerated == false)
                return;
            
            var closestFace = 0;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < surface.TerrainFaces.Length; i++)
            {
                var currentDistance = Vector3.Distance(Camera.main.transform.position, surface.MeshRenderers[i].bounds.center);
                if (currentDistance < closestDistance)
                {
                    closestDistance = currentDistance;
                    closestFace = i;
                }
            }

            if (surface.closestFace != closestFace)
            {
                surface.previousClosestFace = surface.closestFace;
                surface.closestFace = closestFace;
                surface.DirtySurface();
            }
        }

        private void OnDisable()
        {
            sky.CleanUpAtmosphere(this);
        }

        private void OnDestroy()
        {
            surface.Cleanup();
        }

        public void AtmosphereActive(bool isActive)
        {
            if(sky.atmosphere.Visible != isActive)
                sky.atmosphere.Visible = isActive;
            UpdateAtmosphereEffect();
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
            Quaternion currentRotation = transform.rotation;
            transform.rotation = Quaternion.Euler(0f,0f,0f);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            if (surface.MeshRenderers != null)
            {
                foreach (Renderer renderer in surface.MeshRenderers)
                {
                    if (renderer != null)
                        bounds.Encapsulate(renderer.bounds);
                }
            }

            Vector3 localCenter = bounds.center - this.transform.position;
            bounds.center = localCenter;
            transform.rotation = currentRotation;

            return bounds;
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