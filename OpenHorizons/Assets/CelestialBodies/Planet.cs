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
            if (sky.atmosphere.Enabled && sky.atmosphere.Visible)
                AtmosphereRenderPass.RegisterEffect(this);
            else
            {
                sky.GetLodMesh(transform).gameObject.SetActive(true);
                AtmosphereRenderPass.RemoveEffect(this);
            }
        }

        private void Update()
        {
            VolumetricCloudsUrp.VolumetricCloudsPass.UpdateSettings(cloud);
            surface.LazyUpdate(transform);
        }

        public float GetWidth()
        {
            return surface.shape.Radius * 2;
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

        public void AtmosphereActive(bool isActive)
        {
            if(sky.atmosphere.Visible != isActive)
                sky.atmosphere.Visible = isActive;
            UpdateAtmosphereEffect();
        }

        public void CloudsActive(bool isActive)
        {
            cloud.Visible = isActive;
            if (!cloud.Visible && surface.resolution != 256)
            {
                surface.resolution = 256;
                surface.Dirty = true;
            }
        }

        public Bounds GetBounds()
        {
            Quaternion currentRotation = transform.rotation;
            transform.rotation = Quaternion.Euler(0f,0f,0f);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach(Renderer renderer in surface.MeshRenderers)
            {
                bounds.Encapsulate(renderer.bounds);
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

        public float DistToAtmosphere(Vector3 pos)
        {
            return sky.DistToAtmosphere(transform, pos);
        }

        public bool IsActive()
        {
            return sky.atmosphere.Enabled && sky.atmosphere.Visible;
        }

        public GameObject GameObject
        {
            get => gameObject;
        }

    }

}