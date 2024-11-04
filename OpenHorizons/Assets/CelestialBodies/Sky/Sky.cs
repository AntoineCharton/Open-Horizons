using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CelestialBodies.Sky
{
    static class SkyBuilder
    {
        private static readonly int BakedOpticalDepth = Shader.PropertyToID("_BakedOpticalDepth");
        private static readonly int PlanetCenter = Shader.PropertyToID("_PlanetCenter");
        private static readonly int SunParams = Shader.PropertyToID("_SunParams");
        private static readonly int AtmosphereRadius = Shader.PropertyToID("_AtmosphereRadius");
        private static readonly int PlanetRadius = Shader.PropertyToID("_PlanetRadius");
        private static readonly int OceanRadius = Shader.PropertyToID("_OceanRadius");

        public static bool IsVisible(this AtmosphereGenerator atmosphereGenerator, Plane[] cameraPlanes,
            Transform transform)
        {
            if (atmosphereGenerator.sun == null)
            {
                return false;
            }

            Vector3 pos = transform.position;
            float radius = atmosphereGenerator.AtmosphereSize;

            // Cull spherical bounds, ignoring camera far plane at index 5
            for (int i = 0; i < cameraPlanes.Length - 1; i++)
            {
                float distance = cameraPlanes[i].GetDistanceToPoint(pos);

                if (distance < 0 && Mathf.Abs(distance) > radius)
                {
                    return false;
                }
            }

            return true;
        }

        public static Material GetMaterial(this ref AtmosphereGenerator atmosphereGenerator, Shader atmosphereShader)
        {
            if (atmosphereGenerator.material == null)
            {
                atmosphereGenerator.material = new Material(atmosphereShader);
            }

            return atmosphereGenerator.material;
        }

        public static float DistToAtmosphere(this AtmosphereGenerator atmosphereGenerator, Transform transform,
            Vector3 pos)
        {
            return Math.Abs((pos - transform.position).magnitude - atmosphereGenerator.AtmosphereSize);
        }

        public static void CleanUpAtmosphere(this ref AtmosphereGenerator atmosphereGenerator,
            IAtmosphereEffect atmosphereEffect)
        {
            AtmosphereRenderPass.RemoveEffect(atmosphereEffect);

            if (atmosphereGenerator.opticalDepthTexture != null)
            {
                atmosphereGenerator.opticalDepthTexture.Release();
                Object.DestroyImmediate(atmosphereGenerator.opticalDepthTexture);
            }
        }

        public static void LazyUpdate(this ref AtmosphereGenerator atmosphereGenerator, Transform transform, float radius)
        {
            if (atmosphereGenerator.material == null || atmosphereGenerator.sun == null)
            {
                return;
            }

            atmosphereGenerator.planetRadius = radius;
            atmosphereGenerator.oceanRadius = 1;
            atmosphereGenerator.atmosphere.SetProperties(atmosphereGenerator.material);
            atmosphereGenerator.ValidateOpticalDepth();

            atmosphereGenerator.material.SetTexture(BakedOpticalDepth, atmosphereGenerator.opticalDepthTexture);
            atmosphereGenerator.material.SetVector(PlanetCenter, transform.position);

            if (atmosphereGenerator.directional)
            {
                // For directional sun
                atmosphereGenerator.material.SetVector(SunParams, -atmosphereGenerator.sun.forward);
                atmosphereGenerator.material.EnableKeyword("DIRECTIONAL_SUN");
            }
            else
            {
                // For positional sun
                atmosphereGenerator.material.SetVector(SunParams, atmosphereGenerator.sun.position);
                atmosphereGenerator.material.DisableKeyword("DIRECTIONAL_SUN");
            }

            atmosphereGenerator.material.SetFloat(AtmosphereRadius, atmosphereGenerator.AtmosphereSize);
            atmosphereGenerator.material.SetFloat(PlanetRadius, atmosphereGenerator.planetRadius);
            atmosphereGenerator.material.SetFloat(OceanRadius, atmosphereGenerator.oceanRadius);
        }

        static void ValidateOpticalDepth(this ref AtmosphereGenerator atmosphereGenerator)
        {
            bool upToDate = atmosphereGenerator.atmosphere.IsUpToDate(ref atmosphereGenerator.width,
                ref atmosphereGenerator.points, ref atmosphereGenerator.rayFalloff, ref atmosphereGenerator.mieFalloff,
                ref atmosphereGenerator.hAbsorbtion);
            bool sizeChange = Math.Abs(atmosphereGenerator.size - atmosphereGenerator.planetRadius) > float.MinValue ||
                              Math.Abs(atmosphereGenerator.scale - atmosphereGenerator.atmosphereScale) >
                              float.MinValue;
            bool textureExists = atmosphereGenerator.opticalDepthTexture != null &&
                                 atmosphereGenerator.opticalDepthTexture.IsCreated();

            if (!upToDate || sizeChange || !textureExists)
            {
                if (atmosphereGenerator.computeInstance == null)
                {
                    // Create an instance per effect so multiple effects can bake their optical depth simultaneously
                    atmosphereGenerator.computeInstance =
                        Object.Instantiate(atmosphereGenerator.atmosphere.OpticalDepthCompute);
                }

                atmosphereGenerator.atmosphere.BakeOpticalDepth(ref atmosphereGenerator.opticalDepthTexture,
                    atmosphereGenerator.computeInstance, atmosphereGenerator.planetRadius,
                    atmosphereGenerator.AtmosphereSize);

                atmosphereGenerator.size = atmosphereGenerator.planetRadius;
                atmosphereGenerator.scale = atmosphereGenerator.atmosphereScale;
            }
        }
    }

    [Serializable]
    public struct AtmosphereGenerator
    {
        [SerializeField] internal Atmosphere atmosphere;
        [SerializeField] internal Transform sun;
        [SerializeField] internal bool directional;

        [SerializeField, Min(1f), HideInInspector] internal float planetRadius;
        [SerializeField, Min(1f), HideInInspector] internal float oceanRadius;
        [SerializeField, Min(0.025f)] internal float atmosphereScale;

        public float AtmosphereSize => (1 + atmosphereScale) * planetRadius;

        [SerializeField, HideInInspector] internal Material material;
        [SerializeField, HideInInspector] internal ComputeShader computeInstance;
        [SerializeField, HideInInspector] internal RenderTexture opticalDepthTexture;


        // Values to check if optical depth texture is up to date or not. This method is a little messy but does the job.
        [SerializeField, HideInInspector]
        internal int width;

        [SerializeField, HideInInspector]
        internal int points;

        [SerializeField, HideInInspector]
        internal float size;

        [SerializeField, HideInInspector]
        internal float scale;

        [SerializeField, HideInInspector]
        internal float rayFalloff;

        [SerializeField, HideInInspector]
        internal float mieFalloff;

        [SerializeField, HideInInspector]
        internal float hAbsorbtion;
    }

    [Serializable]
    public struct Atmosphere
    {
        public enum TextureSizes
        {
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512
        }


        [System.Serializable]
        public struct ScatterWavelengths
        {
            public float red;
            public float green;
            public float blue;

            [Min(-1f)] public float
                power; // Minimum value is -1 because negative values around that range can give a nice black-light effect. Any more than that and it'll flashbang you.

            // Scattering wavelengths are inversely proportional to channel^4.
            public readonly Vector3 Wavelengths => new Vector3(
                Mathf.Pow(red, 4),
                Mathf.Pow(green, 4),
                Mathf.Pow(blue, 4)
            ) * power;
        }


        public TextureSizes textureSize;
        [SerializeField] private ComputeShader opticalDepthCompute;
        [SerializeField, Range(1, 30)] private int opticalDepthPoints;

        public ComputeShader OpticalDepthCompute => opticalDepthCompute;


        [SerializeField, Range(3, 30)] private int inScatteringPoints;
        [SerializeField] private float sunIntensity;

        [SerializeField] private ScatterWavelengths rayleighScatter;

        [SerializeField, Range(0, 100)] private float rayleighDensityFalloff;

        [SerializeField] private ScatterWavelengths mieScatter;

        [SerializeField, Range(0, 100)] private float mieDensityFalloff;
        [SerializeField, Range(0, 1)] private float mieG;


        [SerializeField, Range(0, 100)] private float heightAbsorbtion;

        [SerializeField, ColorUsage(false, false)]
        private Color absorbtionColor;


        [SerializeField, ColorUsage(false, false)]
        private Color ambientColor;

        private static readonly int NumInScatteringPoints = Shader.PropertyToID("_NumInScatteringPoints");
        private static readonly int NumOpticalDepthPoints = Shader.PropertyToID("_NumOpticalDepthPoints");
        private static readonly int RayleighScattering = Shader.PropertyToID("_RayleighScattering");
        private static readonly int MieScattering = Shader.PropertyToID("_MieScattering");
        private static readonly int AbsorbtionBeta = Shader.PropertyToID("_AbsorbtionBeta");
        private static readonly int AmbientBeta = Shader.PropertyToID("_AmbientBeta");
        private static readonly int MieG = Shader.PropertyToID("_MieG");
        private static readonly int RayleighFalloff = Shader.PropertyToID("_RayleighFalloff");
        private static readonly int MieFalloff = Shader.PropertyToID("_MieFalloff");
        private static readonly int HeightAbsorbtion = Shader.PropertyToID("_HeightAbsorbtion");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int TextureSize = Shader.PropertyToID("_TextureSize");
        private static readonly int NumOutScatteringSteps = Shader.PropertyToID("_NumOutScatteringSteps");
        private static readonly int PlanetRadius = Shader.PropertyToID("_PlanetRadius");
        private static readonly int AtmosphereRadius = Shader.PropertyToID("_AtmosphereRadius");
        private static readonly int Result = Shader.PropertyToID("_Result");

        public Atmosphere(ComputeShader opticalDepthCompute)
        {
            textureSize = TextureSizes._256;
            this.opticalDepthCompute = opticalDepthCompute;
            inScatteringPoints = 25;
            sunIntensity = 20;
            rayleighScatter = new ScatterWavelengths
                { red = 0.556f, green = 0.7f, blue = 0.84f, power = 2f };
            rayleighDensityFalloff = 15;
            mieScatter = new ScatterWavelengths
                { red = 1.0f, green = 0.95f, blue = 0.8f, power = 0.1f };
            mieDensityFalloff = 15;
            mieG = 0.97f;
            heightAbsorbtion = 0;
            absorbtionColor = Color.black;
            ambientColor = Color.black;
            opticalDepthPoints = 15;
        }

        public void SetProperties(Material material)
        {
            material.SetInteger(NumInScatteringPoints, inScatteringPoints);
            material.SetInteger(NumOpticalDepthPoints, opticalDepthPoints);

            material.SetVector(RayleighScattering, rayleighScatter.Wavelengths);

            material.SetVector(MieScattering, mieScatter.Wavelengths);
            material.SetVector(AbsorbtionBeta, absorbtionColor);
            material.SetVector(AmbientBeta, ambientColor);

            material.SetFloat(MieG, mieG);

            material.SetFloat(RayleighFalloff, rayleighDensityFalloff);
            material.SetFloat(MieFalloff, mieDensityFalloff);
            material.SetFloat(HeightAbsorbtion, heightAbsorbtion);

            material.SetFloat(Intensity, sunIntensity);
        }


        private void SetComputeProperties(ComputeShader shader, float planetRadius, float atmosphereRadius)
        {
            shader.SetInt(TextureSize, (int)textureSize);
            shader.SetInt(NumOutScatteringSteps, opticalDepthPoints);

            shader.SetFloat(PlanetRadius, planetRadius);
            shader.SetFloat(AtmosphereRadius, atmosphereRadius);

            shader.SetFloat(RayleighFalloff, rayleighDensityFalloff);
            shader.SetFloat(MieFalloff, mieDensityFalloff);
            shader.SetFloat(HeightAbsorbtion, heightAbsorbtion);
        }


        public bool IsUpToDate(ref int textureSize, ref int opticalPoints, ref float rayleighFalloff,
            ref float mieFalloff, ref float absorbtion)
        {
            bool upToDate = (int)this.textureSize == textureSize &&
                            opticalDepthPoints == opticalPoints &&
                            Math.Abs(rayleighDensityFalloff - rayleighFalloff) < float.MinValue &&
                            Math.Abs(mieDensityFalloff - mieFalloff) < float.MinValue &&
                            Math.Abs(heightAbsorbtion - absorbtion) < float.MinValue;

            textureSize = (int)this.textureSize;
            opticalPoints = opticalDepthPoints;
            rayleighFalloff = rayleighDensityFalloff;
            mieFalloff = mieDensityFalloff;
            absorbtion = heightAbsorbtion;

            return upToDate;
        }

        public void BakeOpticalDepth(ref RenderTexture opticalDepthTexture, ComputeShader shader, float planetRadius,
            float atmosphereRadius)
        {
            if (shader == null)
            {
                throw new Exception("Compute Shader not provided");
            }

            if (opticalDepthTexture == null || !opticalDepthTexture.IsCreated())
            {
                CreateRenderTexture(ref opticalDepthTexture, (int)textureSize, (int)textureSize, FilterMode.Bilinear,
                    RenderTextureFormat.ARGBHalf);

                shader.SetTexture(0, Result, opticalDepthTexture);

                SetComputeProperties(shader, planetRadius, atmosphereRadius);

                shader.GetKernelThreadGroupSizes(0, out uint x, out uint y, out _);

                int numGroupsX = Mathf.CeilToInt((int)textureSize / (float)x);
                int numGroupsY = Mathf.CeilToInt((int)textureSize / (float)y);
                shader.Dispatch(0, numGroupsX, numGroupsY, 1);
            }
        }


        static void CreateRenderTexture(ref RenderTexture texture, int width, int height,
            FilterMode filterMode = FilterMode.Bilinear, RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height ||
                texture.format != format)
            {
                if (texture != null)
                {
                    texture.Release();
                }

                texture = new RenderTexture(width, height, 0)
                {
                    format = format,
                    enableRandomWrite = true,
                    autoGenerateMips = false
                };

                texture.Create();
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
        }
    }
}