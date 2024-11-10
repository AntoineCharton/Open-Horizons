using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using static Unity.Mathematics.math;

namespace CelestialBodies.Clouds.Rendering
{
    /// <summary>
    /// A renderer feature that adds volumetric clouds support to the URP volume.
    /// </summary>
    [DisallowMultipleRendererFeature("Volumetric Clouds URP")]
    [Tooltip("Add this Renderer Feature to support volumetric clouds in URP Volume.")]
    [HelpURL("https://github.com/jiaozi158/UnityVolumetricCloudsURP/tree/main")]
    public class VolumetricCloudsUrp : ScriptableRendererFeature
    {
        [Header("Setup")]
        [Tooltip("The material of volumetric clouds shader.")]
        [SerializeField] private Material material;
        [Tooltip("Enable this to render volumetric clouds in Rendering Debugger view. \nThis is disabled by default to avoid affecting the individual lighting previews.")]
        [SerializeField] private bool renderingDebugger;

        [Header("Performance")]
        [Tooltip("Specifies if URP renders volumetric clouds in both real-time and baked reflection probes. \nVolumetric clouds in real-time reflection probes may reduce performace.")]
        [SerializeField] private bool reflectionProbe;
        [Range(0.25f, 1.0f), Tooltip("The resolution scale for volumetric clouds rendering.")]
        [SerializeField] private float resolutionScale = 0.5f;
        [Tooltip("Select the method to use for upscaling volumetric clouds.")]
        [SerializeField] private CloudsUpscaleMode upscaleMode = CloudsUpscaleMode.Bilinear;
        [Tooltip("Specifies the preferred texture render mode for volumetric clouds. \nThe Copy Texture mode should be more performant.")]
        [SerializeField] private CloudsRenderMode preferredRenderMode = CloudsRenderMode.CopyTexture;

        [Header("Lighting")]
        [Tooltip("Specifies the volumetric clouds ambient probe update frequency.")]
        [SerializeField] private CloudsAmbientMode ambientProbe = CloudsAmbientMode.Dynamic;
        [Tooltip("Specifies if URP calculates physically based sun attenuation for volumetric clouds.")]
        [SerializeField] private bool sunAttenuation;

        [Header("Wind")]
        [Tooltip("Enable to reset the wind offsets to their initial states when start playing.")]
        [SerializeField] private bool resetOnStart = true;

        [Header("Depth")]
        [Tooltip("Specifies if URP outputs volumetric clouds average depth to a global shader texture named \"_VolumetricCloudsDepthTexture\".")]
        [SerializeField] private bool outputDepth;

        private const string ShaderName = "Hidden/Sky/VolumetricClouds";
        private VolumetricCloudsPass _volumetricCloudsPass;
        private VolumetricCloudsAmbientPass _volumetricCloudsAmbientPass;
        private VolumetricCloudsShadowsPass _volumetricCloudsShadowsPass;

        // Pirnt message only once.
        private bool _isLogPrinted;

        /// <summary>
        /// Gets or sets the material of volumetric clouds shader.
        /// </summary>
        /// <value>
        /// The material of volumetric clouds shader.
        /// </value>
        public Material CloudsMaterial
        {
            get { return material; }
            set { material = (value.shader == Shader.Find(ShaderName)) ? value : material; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to render volumetric clouds in Rendering Debugger view.
        /// </summary>
        /// <value>
        /// <c>true</c> if rendering volumetric clouds in Rendering Debugger view; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// This is disabled by default to avoid affecting the individual lighting previews.
        /// </remarks>
        public bool RenderingDebugger
        {
            get { return renderingDebugger; }
            set { renderingDebugger = value; }
        }

        /// <summary>
        /// Gets or sets the resolution scale for volumetric clouds rendering.
        /// </summary>
        /// <value>
        /// The resolution scale for volumetric clouds rendering, ranging from 0.25 to 1.0.
        /// </value>
        public float ResolutionScale
        {
            get { return resolutionScale; }
            set { resolutionScale = Mathf.Clamp(value, 0.25f, 1.0f); }
        }

        /// <summary>
        /// Gets or sets the preferred texture render mode for volumetric clouds.
        /// </summary>
        /// <value>
        /// The preferred texture render mode for volumetric clouds, either CopyTexture or BlitTexture.
        /// </value>
        /// <remarks>
        /// The CopyTexture mode should be more performant.
        /// </remarks>
        public CloudsRenderMode PreferredRenderMode
        {
            get { return preferredRenderMode; }
            set { preferredRenderMode = value; }
        }

        /// <summary>
        /// Gets or sets the ambient probe update frequency for volumetric clouds.
        /// </summary>
        /// <value>
        /// The ambient probe update frequency for volumetric clouds, either Static or Dynamic.
        /// </value>
        public CloudsAmbientMode AmbientUpdateMode
        {
            get { return ambientProbe; }
            set { ambientProbe = value; }
        }

        /// <summary>
        /// Gets or sets the method used for upscaling volumetric clouds.
        /// </summary>
        /// <value>
        /// The method to use for upscaling volumetric clouds.
        /// </value>
        public CloudsUpscaleMode UpscaleMode
        {
            get { return upscaleMode; }
            set { upscaleMode = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to reset wind offsets for volumetric clouds when entering playmode.
        /// </summary>
        /// <value>
        /// <c>true</c> if resetting wind offsets when entering playmode; otherwise, <c>false</c>.
        /// </value>
        public bool ResetWindOnStart
        {
            get { return resetOnStart; }
            set { resetOnStart = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether URP calculates physically based sun attenuation for volumetric clouds.
        /// </summary>
        /// <value>
        /// <c>true</c> if URP calculates physically based sun attenuation for volumetric clouds; otherwise, <c>false</c>.
        /// </value>
        public bool SunAttenuation
        {
            get { return sunAttenuation; }
            set { sunAttenuation = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether URP outputs volumetric clouds average depth to a global shader texture named "_VolumetricCloudsDepthTexture".
        /// </summary>
        /// <value>
        /// <c>true</c> if URP outputs volumetric clouds average depth; otherwise, <c>false</c>.
        /// </value>
        public bool OutputCloudsDepth
        {
            get { return outputDepth; }
            set { outputDepth = value; }
        }

        public enum CloudsRenderMode
        {
            [Tooltip("Always use Blit() to copy render textures.")]
            BlitTexture = 0,

            [Tooltip("Use CopyTexture() to copy render textures when supported.")]
            CopyTexture = 1
        }

        public enum CloudsAmbientMode
        {
            [Tooltip("Use URP default static ambient probe for volumetric clouds rendering.")]
            Static,

            [Tooltip("Use a fast dynamic ambient probe for volumetric clouds rendering.")]
            Dynamic
        }

        public enum CloudsUpscaleMode
        {
            [Tooltip("Use simple but fast filtering for volumetric clouds upscale.")]
            Bilinear,

            [Tooltip("Use more computationally expensive filtering for volumetric clouds upscale. \nThis blurs the cloud details but reduces the noise that may appear at lower clouds resolutions.")]
            Bilateral
        }

        public override void Create()
        {
            // Check if the volumetric clouds material uses the correct shader.
            if (material != null)
            {
                if (material.shader != Shader.Find(ShaderName))
                {
#if UNITY_EDITOR || DEBUG
                    Debug.LogErrorFormat("Volumetric Clouds URP: Material shader is not {0}.", ShaderName);
#endif
                    return;
                }
            }
            // No material applied.
            else
            {
#if UNITY_EDITOR || DEBUG
                Debug.LogError("Volumetric Clouds URP: Material is empty.");
#endif
                return;
            }

            if (_volumetricCloudsPass == null)
            {
                _volumetricCloudsPass = new(material, resolutionScale);
                _volumetricCloudsPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; // Use camera previous matrix to do reprojection
            }
            else
            {
                // Update every frame to support runtime changes to these properties.
                _volumetricCloudsPass.ResolutionScale = resolutionScale;
                _volumetricCloudsPass.UpscaleMode = upscaleMode;
                _volumetricCloudsPass.DynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
            }

            if (_volumetricCloudsAmbientPass == null)
            {
                _volumetricCloudsAmbientPass = new(material);
                _volumetricCloudsAmbientPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents - 1;
            }

            if (_volumetricCloudsShadowsPass == null)
            {
                _volumetricCloudsShadowsPass = new(material);
                _volumetricCloudsShadowsPass.renderPassEvent = RenderPassEvent.BeforeRendering;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_volumetricCloudsPass != null)
                _volumetricCloudsPass.Dispose();
            if (_volumetricCloudsAmbientPass != null)
                _volumetricCloudsAmbientPass.Dispose();
            if (_volumetricCloudsShadowsPass != null)
                _volumetricCloudsShadowsPass.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null)
            {
#if UNITY_EDITOR || DEBUG
                Debug.LogErrorFormat("Volumetric Clouds URP: Material is empty.");
#endif
                return;
            }

            bool isCloudsActive = VolumetricCloudsPass.Cloud.Enabled && VolumetricCloudsPass.Cloud.Visible;
            
            bool isDebugger = DebugManager.instance.isAnyDebugUIActive;

            bool isProbeCamera = renderingData.cameraData.cameraType == CameraType.Reflection && reflectionProbe;

            if (isCloudsActive && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView || isProbeCamera) && (!isDebugger || renderingDebugger))
            {
                bool dynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
                _volumetricCloudsPass.DynamicAmbientProbe = dynamicAmbientProbe;
                _volumetricCloudsPass.RenderMode = preferredRenderMode;
                _volumetricCloudsPass.ResetWindOnStart = resetOnStart;
                _volumetricCloudsPass.OutputDepth = outputDepth;
                _volumetricCloudsPass.SunAttenuation = sunAttenuation;

#if URP_PBSKY
            PhysicallyBasedSky pbrSky = stack.GetComponent<PhysicallyBasedSky>();
            volumetricCloudsPass.hasAtmosphericScattering = pbrSky != null && pbrSky.IsActive() && pbrSky.atmosphericScattering.value;
#else
                _volumetricCloudsPass.HasAtmosphericScattering = false;
#endif

                renderer.EnqueuePass(_volumetricCloudsPass);

                // No need to render dynamic ambient probe for reflection probes.
                if (dynamicAmbientProbe && !isProbeCamera) { renderer.EnqueuePass(_volumetricCloudsAmbientPass); }

                _isLogPrinted = false;
            }
#if UNITY_EDITOR || DEBUG
            else if (isDebugger && !renderingDebugger && !_isLogPrinted)
            {
                Debug.Log("Volumetric Clouds URP: Disable effect to avoid affecting rendering debugging.");
                _isLogPrinted = true;
            }
#endif
        }

        public class VolumetricCloudsPass : ScriptableRenderPass
        {
            public CloudsRenderMode RenderMode;
            public float ResolutionScale;
            public CloudsUpscaleMode UpscaleMode;
            public bool DynamicAmbientProbe;
            public bool ResetWindOnStart;
            public bool OutputDepth;
            public bool SunAttenuation;
            public bool HasAtmosphericScattering;
            public static Cloud Cloud;

            private bool _denoiseClouds;

            private readonly RenderTargetIdentifier[] _cloudsHandles = new RenderTargetIdentifier[2];

            private RTHandle _cloudsColorHandle;
            private RTHandle _cloudsDepthHandle;
            private RTHandle _accumulateHandle;
            private RTHandle _historyHandle;

            private readonly Material _cloudsMaterial;

            private readonly bool _fastCopy = (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;

            private const string ProfilerTag = "Volumetric Clouds";
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(ProfilerTag);

            private static readonly int NumPrimarySteps = Shader.PropertyToID("_NumPrimarySteps");
            private static readonly int NumLightSteps = Shader.PropertyToID("_NumLightSteps");
            private static readonly int MaxStepSize = Shader.PropertyToID("_MaxStepSize");
            private static readonly int HighestCloudAltitude = Shader.PropertyToID("_HighestCloudAltitude");
            private static readonly int LowestCloudAltitude = Shader.PropertyToID("_LowestCloudAltitude");
            private static readonly int ShapeNoiseOffset = Shader.PropertyToID("_ShapeNoiseOffset");
            private static readonly int VerticalShapeNoiseOffset = Shader.PropertyToID("_VerticalShapeNoiseOffset");
            private static readonly int GlobalOrientation = Shader.PropertyToID("_WindDirection");
            private static readonly int GlobalSpeed = Shader.PropertyToID("_WindVector");
            private static readonly int VerticalShapeDisplacement = Shader.PropertyToID("_VerticalShapeWindDisplacement");
            private static readonly int VerticalErosionDisplacement = Shader.PropertyToID("_VerticalErosionWindDisplacement");
            private static readonly int DensityMultiplier = Shader.PropertyToID("_DensityMultiplier");
            private static readonly int ShapeScale = Shader.PropertyToID("_ShapeScale");
            private static readonly int ShapeFactor = Shader.PropertyToID("_ShapeFactor");
            private static readonly int ErosionScale = Shader.PropertyToID("_ErosionScale");
            private static readonly int ErosionFactor = Shader.PropertyToID("_ErosionFactor");
            private static readonly int MicroErosionScale = Shader.PropertyToID("_MicroErosionScale");
            private static readonly int MicroErosionFactor = Shader.PropertyToID("_MicroErosionFactor");
            private static readonly int ScatteringTint = Shader.PropertyToID("_ScatteringTint");
            private static readonly int AmbientProbeDimmer = Shader.PropertyToID("_AmbientProbeDimmer");
            private static readonly int SunLightDimmer = Shader.PropertyToID("_SunLightDimmer");
            private static readonly int EarthRadius = Shader.PropertyToID("_EarthRadius");
            private static readonly int EarthPosition = Shader.PropertyToID("_EarthPosition");
            //private static readonly int normalizationFactor = Shader.PropertyToID("_NormalizationFactor");
            private static readonly int CloudsCurveLut = Shader.PropertyToID("_CloudCurveTexture");
            private static readonly int CloudnearPlane = Shader.PropertyToID("_CloudNearPlane");
            private static readonly int SunColor = Shader.PropertyToID("_SunColor");

            private static readonly int CameraDepthTexture = Shader.PropertyToID(DepthTexture);
            private static readonly int VolumetricCloudsColorTexture = Shader.PropertyToID(CloudsColorTexture);
            private static readonly int VolumetricCloudsHistoryTexture = Shader.PropertyToID(CloudsHistoryTexture);
            private static readonly int VolumetricCloudsDepthTexture = Shader.PropertyToID(CloudsDepthTexture);
            private static readonly int VolumetricCloudsLightingTexture = Shader.PropertyToID(CloudsLightingTexture); // Same as "_VolumetricCloudsColorTexture"

            private const string LocalClouds = "_LOCAL_VOLUMETRIC_CLOUDS";
            private const string MicroErosion = "_CLOUDS_MICRO_EROSION";
            private const string LowResClouds = "_LOW_RESOLUTION_CLOUDS";
            private const string CloudsAmbientProbe = "_CLOUDS_AMBIENT_PROBE";
            private const string OutputCloudsDepth = "_OUTPUT_CLOUDS_DEPTH";
            private const string PhysicallyBasedSun = "_PHYSICALLY_BASED_SUN";
            private const string PerceptualBlending = "_PERCEPTUAL_BLENDING";

            private const string DepthTexture = "_CameraDepthTexture";
            private const string CloudsColorTexture = "_VolumetricCloudsColorTexture";
            private const string CloudsHistoryTexture = "_VolumetricCloudsHistoryTexture";
            private const string CloudsAccumulationTexture = "_VolumetricCloudsAccumulationTexture";
            private const string CloudsDepthTexture = "_VolumetricCloudsDepthTexture";
            private const string CloudsLightingTexture = "_VolumetricCloudsLightingTexture"; // Same as "_VolumetricCloudsColorTexture"

            private Texture2D _customLutPresetMap;
            private readonly Color[] _customLutColorArray = new Color[CustomLutMapResolution];

            public const float EarthRad = 5500.0f;
            public const float WindNormalizationFactor = 100000.0f; // NOISE_TEXTURE_NORMALIZATION_FACTOR in "VolumetricCloudsUtilities.hlsl"
            public const int CustomLutMapResolution = 64;

            // Wind offsets
            private bool _prevIsPlaying;
            private float _prevTotalTime = -1.0f;
            private float _verticalShapeOffset;
            private float _verticalErosionOffset;
            private Vector2 _windVector = Vector2.zero;
            internal static  Transform _transform;

            public static void RegisterPlanet(Transform transform)
            {
                _transform = transform;
            }

            public static void UpdateSettings(Cloud cloudSettings)
            {
                Cloud = cloudSettings;
            }
        
            private static float Square(float x) => x * x;

            private void UpdateMaterialProperties(Camera camera)
            {
                _cloudsMaterial.EnableKeyword(LocalClouds);

                if (Cloud.MicroErosion) { _cloudsMaterial.EnableKeyword(MicroErosion); }
                else { _cloudsMaterial.DisableKeyword(MicroErosion); }

                if (ResolutionScale < 1.0f && UpscaleMode == CloudsUpscaleMode.Bilateral) { _cloudsMaterial.EnableKeyword(LowResClouds); }
                else { _cloudsMaterial.DisableKeyword(LowResClouds); }

                if (DynamicAmbientProbe) { _cloudsMaterial.EnableKeyword(CloudsAmbientProbe); }
                else { _cloudsMaterial.DisableKeyword(CloudsAmbientProbe); }

                if (OutputDepth) { _cloudsMaterial.EnableKeyword(OutputCloudsDepth); }
                else { _cloudsMaterial.DisableKeyword(OutputCloudsDepth); }

                if (SunAttenuation) { _cloudsMaterial.EnableKeyword(PhysicallyBasedSun); }
                else { _cloudsMaterial.DisableKeyword(PhysicallyBasedSun); }

                _cloudsMaterial.DisableKeyword(PerceptualBlending);

                _cloudsMaterial.SetFloat(NumPrimarySteps, 32);
                _cloudsMaterial.SetFloat(NumLightSteps, 2);
                _cloudsMaterial.SetFloat(MaxStepSize, Cloud.AltitudeRange / 8.0f);
                float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, 0) * (EarthRad  * VolumetricCloudsPass._transform.lossyScale.x);
                float bottomAltitude = (Cloud.BottomAltitude * VolumetricCloudsPass._transform.lossyScale.x) + actualEarthRad;
                float highestAltitude = (bottomAltitude + (Cloud.AltitudeRange * VolumetricCloudsPass._transform.lossyScale.x));
                _cloudsMaterial.SetFloat(HighestCloudAltitude, highestAltitude);
                _cloudsMaterial.SetFloat(LowestCloudAltitude, bottomAltitude);
                _cloudsMaterial.SetVector(ShapeNoiseOffset, new Vector4(Cloud.ShapeOffset.x, Cloud.ShapeOffset.z, 0.0f, 0.0f));
                _cloudsMaterial.SetFloat(VerticalShapeNoiseOffset, Cloud.ShapeOffset.y);

                // Wind animation
                float totalTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
                float deltaTime = totalTime - _prevTotalTime;
                if (Math.Abs(_prevTotalTime - (-1.0f)) < Mathf.Epsilon)
                    deltaTime = 0.0f;

#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPaused)
                    deltaTime = 0.0f;
#endif

                // Conversion from km/h to m/s is the 0.277778f factor
                // We apply a minus to see something moving in the right direction
                deltaTime *= -0.277778f;

                float theta = Cloud.WindOrientation / 180.0f * Mathf.PI;
                Vector2 windDirection = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            
                if (ResetWindOnStart && _prevIsPlaying != Application.isPlaying)
                {
                    _windVector = Vector2.zero;
                    _verticalShapeOffset = 0.0f;
                    _verticalErosionOffset = 0.0f;
                }
                else
                {
                    _windVector += deltaTime * Cloud.WindSpeed * windDirection;
                    // Reset the accumulated wind variables periodically to avoid extreme values.
                    _windVector.x %= WindNormalizationFactor;
                    _windVector.y %= WindNormalizationFactor;
                    _verticalShapeOffset %= WindNormalizationFactor;
                    _verticalErosionOffset %= WindNormalizationFactor;
                }

                // Update previous values
                _prevTotalTime = totalTime;
                _prevIsPlaying = Application.isPlaying;

                // We apply a minus to see something moving in the right direction
                _cloudsMaterial.SetVector(GlobalOrientation, new Vector4(-windDirection.x, -windDirection.y, 0.0f, 0.0f));
                _cloudsMaterial.SetVector(GlobalSpeed, _windVector);
                _cloudsMaterial.SetFloat(VerticalShapeDisplacement, _verticalShapeOffset);
                _cloudsMaterial.SetFloat(VerticalErosionDisplacement, _verticalErosionOffset);
                _cloudsMaterial.SetFloat(DensityMultiplier, (Cloud.DensityMultipler * Cloud.DensityMultipler * 2.0f) * VolumetricCloudsPass._transform.lossyScale.x);
                _cloudsMaterial.SetFloat(ShapeScale, Cloud.ShapeScale / VolumetricCloudsPass._transform.lossyScale.x);
                _cloudsMaterial.SetFloat(ShapeFactor, Cloud.ShapeFactor);
                _cloudsMaterial.SetFloat(ErosionScale, Cloud.ErosionScale);
                _cloudsMaterial.SetFloat(ErosionFactor, Cloud.ErosionFactor);
                _cloudsMaterial.SetFloat(MicroErosionScale, Cloud.MicroErosionScale);
                _cloudsMaterial.SetFloat(MicroErosionFactor, Cloud.MicroErosionFactor);
                _cloudsMaterial.SetColor(ScatteringTint, Color.white - Cloud.ScatteringTint * 0.75f);
                _cloudsMaterial.SetFloat(AmbientProbeDimmer, 1);
                _cloudsMaterial.SetFloat(SunLightDimmer, 1);
                _cloudsMaterial.SetFloat(EarthRadius, actualEarthRad * _transform.lossyScale.x);
                if(_transform == null)
                    _cloudsMaterial.SetVector(EarthPosition, new Vector4(0, 0, 0, 0));
                else
                {
                    var position = _transform.position;
                    _cloudsMaterial.SetVector(EarthPosition, new Vector4(position.x, position.y, position.z, 0));
                }
                Vector3 cameraPosPS = camera.transform.position - new Vector3(0.0f, -actualEarthRad * _transform.lossyScale.x, 0.0f);
                _cloudsMaterial.SetFloat(CloudnearPlane, max(GetCloudNearPlane(cameraPosPS, bottomAltitude, highestAltitude), camera.nearClipPlane));

                // Custom cloud map is not supported yet.
                //float lowerCloudRadius = (bottomAltitude + highestAltitude) * 0.5f - actualEarthRad;
                //cloudsMaterial.SetFloat(normalizationFactor, Mathf.Sqrt((earthRad + lowerCloudRadius) * (earthRad + lowerCloudRadius) - earthRad * actualEarthRad));

                PrepareCustomLutData();
            }

            private void UpdateClouds(Light mainLight, Camera camera)
            {
                // When using PBSky, we already applied the sun attenuation to "_MainLightColor"
                if (SunAttenuation)
                {
                    bool isLinearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    Color mainLightColor = (isLinearColorSpace ? mainLight.color.linear : mainLight.color.gamma) * (mainLight.useColorTemperature ? Mathf.CorrelatedColorTemperatureToRGB(mainLight.colorTemperature) : Color.white) * mainLight.intensity;
                    // Pass the actual main light color to volumetric clouds shader.
                    _cloudsMaterial.SetVector(SunColor, mainLightColor);
                }

                UpdateMaterialProperties(camera);
                _denoiseClouds = true;
            }

            private void PrepareCustomLutData()
            {
                if (_customLutPresetMap == null)
                {
                    _customLutPresetMap = new Texture2D(1, CustomLutMapResolution, GraphicsFormat.R16G16B16A16_SFloat, TextureCreationFlags.None)
                    {
                        name = "Custom LUT Curve",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    _customLutPresetMap.hideFlags = HideFlags.HideAndDontSave;
                }

                var pixels = _customLutColorArray;
            
                Color white = Color.white;
                for (int i = 0; i < CustomLutMapResolution; i++)
                    pixels[i] = white;

                _customLutPresetMap.SetPixels(pixels);
                _customLutPresetMap.Apply();

                _cloudsMaterial.SetTexture(CloudsCurveLut, _customLutPresetMap);
            }

            private static Vector2 IntersectSphere(float sphereRadius, float cosChi,
                float radialDistance, float rcpRadialDistance)
            {
                float d = Square(sphereRadius * rcpRadialDistance) - saturate(1 - cosChi * cosChi);

                // Return the value of 'd' for debugging purposes.
                return (d < 0.0f) ? new Vector2(-1.0f, -1.0f) : (radialDistance * new Vector2(-cosChi - sqrt(d),
                    -cosChi + sqrt(d)));
            }

            private static float GetCloudNearPlane(Vector3 originPS, float lowerBoundPS, float higherBoundPS)
            {
                float radialDistance = length(originPS);
                float rcpRadialDistance = rcp(radialDistance);
                float cosChi = 1.0f;
                Vector2 tInner = IntersectSphere(lowerBoundPS, cosChi, radialDistance, rcpRadialDistance);
                Vector2 tOuter = IntersectSphere(higherBoundPS, -cosChi, radialDistance, rcpRadialDistance);

                if (tInner.x < 0.0f && tInner.y >= 0.0f) // Below the lower bound
                    return tInner.y;
                else // Inside or above the cloud volume
                    return max(tOuter.x, 0.0f);
            }

            public VolumetricCloudsPass(Material material, float resolution)
            {
                _cloudsMaterial = material;
                ResolutionScale = resolution;
            }

            #region Non Render Graph Pass
            private Light GetMainLight(LightData lightData)
            {
                int shadowLightIndex = lightData.mainLightIndex;
                if (shadowLightIndex != -1)
                {
                    VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                    Light light = shadowLight.light;
                    if (light.shadows != LightShadows.None && shadowLight.lightType == LightType.Directional)
                        return light;
                }

                return RenderSettings.sun;
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            
                desc.msaaSamples = 1;
                desc.useMipMap = false;
                desc.depthBufferBits = 0;
#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: CloudsHistoryTexture); // lighting.rgb only
#else
            RenderingUtils.ReAllocateIfNeeded(ref historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsHistoryTexture); // lighting.rgb only
#endif

                desc.colorFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a
#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _accumulateHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: CloudsAccumulationTexture);
#else
            RenderingUtils.ReAllocateIfNeeded(ref accumulateHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsAccumulationTexture);
#endif
            
                desc.width = (int)(desc.width * ResolutionScale);
                desc.height = (int)(desc.height * ResolutionScale);
#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: CloudsLightingTexture);
#else
            RenderingUtils.ReAllocateIfNeeded(ref cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsLightingTexture);
#endif
                _cloudsMaterial.SetTexture(VolumetricCloudsLightingTexture, _cloudsColorHandle);

                desc.colorFormat = RenderTextureFormat.RFloat; // average z-depth
#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: CloudsDepthTexture);
#else
            RenderingUtils.ReAllocateIfNeeded(ref cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsDepthTexture);
#endif

                cmd.SetGlobalTexture(VolumetricCloudsColorTexture, _cloudsColorHandle);
                cmd.SetGlobalTexture(VolumetricCloudsLightingTexture, _cloudsColorHandle); // Same as "_VolumetricCloudsColorTexture"
                cmd.SetGlobalTexture(VolumetricCloudsDepthTexture, _cloudsDepthHandle);

                _cloudsMaterial.SetTexture(VolumetricCloudsHistoryTexture, _historyHandle);
                _cloudsMaterial.SetTexture(VolumetricCloudsDepthTexture, _cloudsDepthHandle);

                ConfigureInput(ScriptableRenderPassInput.Depth);
                ConfigureTarget(_cloudsColorHandle, _cloudsColorHandle);
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                LightData lightData = renderingData.lightData;
                Light mainLight = GetMainLight(lightData);
                if(_transform != null)
                {
                    var position = _transform.position;
                    _cloudsMaterial.SetVector(EarthPosition, new Vector4(position.x, position.y, position.z, 0));
                }

                UpdateClouds(mainLight, renderingData.cameraData.camera);

                RTHandle cameraColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    // Clouds Rendering
                    if (OutputDepth)
                    {
                        _cloudsHandles[0] = _cloudsColorHandle;
                        _cloudsHandles[1] = _cloudsDepthHandle;

                        // RT-1: clouds lighting
                        // RT-2: clouds depth
                        cmd.SetRenderTarget(_cloudsHandles, _cloudsDepthHandle);
                        Blitter.BlitTexture(cmd, cameraColorHandle, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), _cloudsMaterial, pass: 0);
                    }
                    else
                    {
                        Blitter.BlitCameraTexture(cmd, _cloudsColorHandle, _cloudsColorHandle, _cloudsMaterial, pass: 0);
                    }

                    // Clouds Upscale & Combine
                    Blitter.BlitCameraTexture(cmd, cameraColorHandle, cameraColorHandle, _cloudsMaterial, pass: HasAtmosphericScattering ? 7 : 1);
                
                    if (_denoiseClouds)
                    {
                        // Prepare Temporal Reprojection (copy source buffer: colorHandle.rgb + cloudsColorHandle.a)
                        Blitter.BlitCameraTexture(cmd, cameraColorHandle, _accumulateHandle, _cloudsMaterial, pass: 2);

                        // Temporal Reprojection
                        Blitter.BlitCameraTexture(cmd, _accumulateHandle, cameraColorHandle, _cloudsMaterial, pass: 3);

                        // Update history texture for temporal reprojection
                        bool canCopy = cameraColorHandle.rt.format == _historyHandle.rt.format && cameraColorHandle.rt.antiAliasing == 1 && _fastCopy;
                        if (canCopy && RenderMode == CloudsRenderMode.CopyTexture) { cmd.CopyTexture(cameraColorHandle, _historyHandle); }
                        else { Blitter.BlitCameraTexture(cmd, cameraColorHandle, _historyHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, _cloudsMaterial, pass: 2); }
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
            #endregion

#if UNITY_6000_0_OR_NEWER
            #region Render Graph Pass
            private Light GetMainLight(UniversalLightData lightData)
            {
                int shadowLightIndex = lightData.mainLightIndex;
                if (shadowLightIndex != -1)
                {
                    VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                    Light light = shadowLight.light;
                    if (light.shadows != LightShadows.None && shadowLight.lightType == LightType.Directional)
                        return light;
                }

                return RenderSettings.sun;
            }

            // This class stores the data needed by the pass, passed as parameter to the delegate function that executes the pass
            private class PassData
            {
                internal Material CloudsMaterial;
                internal bool CanCopy;
                internal bool DenoiseClouds;
                internal bool OutputDepth;
                internal bool HasAtmosphericScattering;

                internal RenderTargetIdentifier[] CloudsHandles;

                internal TextureHandle CameraColorHandle;
                internal TextureHandle CameraDepthHandle;
                internal TextureHandle CloudsColorHandle;
                internal TextureHandle CloudsDepthHandle;
                internal TextureHandle AccumulateHandle;
                internal TextureHandle HistoryHandle;
            }

            // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
            static void ExecutePass(PassData data, UnsafeGraphContext context)
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                data.CloudsMaterial.SetTexture(CameraDepthTexture, data.CameraDepthHandle);

                // Clouds Rendering
                if (data.OutputDepth)
                {
                    data.CloudsHandles[0] = data.CloudsColorHandle;
                    data.CloudsHandles[1] = data.CloudsDepthHandle;

                    // RT-1: clouds lighting
                    // RT-2: clouds depth
                    context.cmd.SetRenderTarget(data.CloudsHandles, data.CloudsDepthHandle);
                    Blitter.BlitTexture(cmd, data.CameraColorHandle, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), data.CloudsMaterial, pass: 0);
                }
                else
                {
                    Blitter.BlitCameraTexture(cmd, data.CameraColorHandle, data.CloudsColorHandle, data.CloudsMaterial, pass: 0);
                }

                // Clouds Upscale & Combine
                Blitter.BlitCameraTexture(cmd, data.CloudsColorHandle, data.CameraColorHandle, data.CloudsMaterial, pass: data.HasAtmosphericScattering ? 7 : 1);

                if (data.DenoiseClouds)
                {
                    // Prepare Temporal Reprojection (copy source buffer: colorHandle.rgb + cloudsHandle.a)
                    Blitter.BlitCameraTexture(cmd, data.CameraColorHandle, data.AccumulateHandle, data.CloudsMaterial, pass: 2);

                    // Temporal Reprojection
                    Blitter.BlitCameraTexture(cmd, data.AccumulateHandle, data.CameraColorHandle, data.CloudsMaterial, pass: 3);

                    // Update history texture for temporal reprojection
                    if (data.CanCopy)
                        cmd.CopyTexture(data.CameraColorHandle, data.HistoryHandle);
                    else
                        Blitter.BlitCameraTexture(cmd, data.CameraColorHandle, data.HistoryHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, data.CloudsMaterial, pass: 2);

                    data.CloudsMaterial.SetTexture(VolumetricCloudsHistoryTexture, data.HistoryHandle);
                }
            }

            // This is where the renderGraph handle can be accessed.
            // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // add a raster render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
                using (var builder = renderGraph.AddUnsafePass<PassData>(ProfilerTag, out var passData))
                {
                    // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
                    // The active color and depth textures are the main color and depth buffers that the camera renders into
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();

                    Light mainLight = GetMainLight(lightData);
                    UpdateClouds(mainLight, cameraData.camera);

                    // Get the active color texture through the frame data, and set it as the source texture for the blit
                    passData.CameraColorHandle = resourceData.activeColorTexture;
                    passData.CameraDepthHandle = resourceData.cameraDepthTexture;

                    RenderTextureFormat cloudsHandleFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a
                    RenderTextureFormat cloudsDepthHandleFormat = RenderTextureFormat.RFloat; // average z-depth

                    RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                    var textureDesc = passData.CameraColorHandle.GetDescriptor(renderGraph);
                    desc.height = textureDesc.height;
                    desc.width = textureDesc.width;

                    desc.msaaSamples = 1;
                    desc.useMipMap = false;
                    desc.depthBufferBits = 0;
                    desc.colorFormat = cloudsHandleFormat;

                    TextureHandle accumulateHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, name: CloudsAccumulationTexture, false);
                    TextureHandle historyHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, name: CloudsHistoryTexture, false);

                    desc.width = (int)(desc.width * ResolutionScale);
                    desc.height = (int)(desc.height * ResolutionScale);
                    RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: CloudsLightingTexture);
                    _cloudsMaterial.SetTexture(VolumetricCloudsLightingTexture, _cloudsColorHandle);
                    TextureHandle cloudsTextureHandle = renderGraph.ImportTexture(_cloudsColorHandle);

                    builder.SetGlobalTextureAfterPass(cloudsTextureHandle, VolumetricCloudsColorTexture);
                    builder.SetGlobalTextureAfterPass(cloudsTextureHandle, VolumetricCloudsLightingTexture); // Same as "_VolumetricCloudsColorTexture"

                    if (OutputDepth)
                    {
                        desc.colorFormat = cloudsDepthHandleFormat;

                        RenderingUtils.ReAllocateHandleIfNeeded(ref _cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: CloudsDepthTexture);
                        _cloudsMaterial.SetTexture(VolumetricCloudsDepthTexture, _cloudsDepthHandle);
                        TextureHandle cloudsDepthTextureHandle = renderGraph.ImportTexture(_cloudsDepthHandle);
                        passData.CloudsDepthHandle = cloudsDepthTextureHandle;
                        builder.UseTexture(passData.CloudsDepthHandle, AccessFlags.Write); // change to "AccessFlags.ReadWrite" if you need to access it in opaque object's shader
                        builder.SetGlobalTextureAfterPass(cloudsDepthTextureHandle, VolumetricCloudsDepthTexture);
                    }

                    // Fill up the passData with the data needed by the pass
                    passData.CloudsMaterial = _cloudsMaterial;
                    passData.CanCopy = cameraData.cameraTargetDescriptor.colorFormat == cloudsHandleFormat && cameraData.cameraTargetDescriptor.msaaSamples == 1 && _fastCopy;
                    passData.DenoiseClouds = _denoiseClouds;
                    passData.OutputDepth = OutputDepth;
                    passData.HasAtmosphericScattering = HasAtmosphericScattering;

                    passData.CloudsHandles = _cloudsHandles;
                    passData.CloudsColorHandle = cloudsTextureHandle;
                    passData.AccumulateHandle = accumulateHandle;
                    passData.HistoryHandle = historyHandle;

                    ConfigureInput(ScriptableRenderPassInput.Depth);

                    // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                    builder.UseTexture(passData.CameraColorHandle, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.CameraDepthHandle);
                    builder.UseTexture(passData.CloudsColorHandle, AccessFlags.Write);
                    builder.UseTexture(passData.AccumulateHandle, AccessFlags.Write);
                    builder.UseTexture(passData.HistoryHandle, AccessFlags.ReadWrite);

                    // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
                }
            }
            #endregion
#endif

            #region Shared
            public void Dispose()
            {
                _cloudsColorHandle?.Release();
                _cloudsDepthHandle?.Release();
                _historyHandle?.Release();
                _accumulateHandle?.Release();
            }
            #endregion
        }
        public class VolumetricCloudsAmbientPass : ScriptableRenderPass
        {
            private const string ProfilerTag = "Volumetric Clouds Ambient Probe";
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(ProfilerTag);

            private readonly Material _cloudsMaterial;
            private RTHandle _probeColorHandle;

            private const string VolumetricCloudsAmbientProbe = "_VolumetricCloudsAmbientProbe";
            private const string WorldSpaceCameraPos = "_WorldSpaceCameraPos";
            private const string DisableSunDisk = "_DisableSunDisk";
            private const string UnityMatrixVp = "unity_MatrixVP";
            private const string UnityMatrixInvVp = "unity_MatrixInvVP";
            private const string ScaledScreenParams = "_ScaledScreenParams";

            private static readonly int CloudsAmbientProbe = Shader.PropertyToID(VolumetricCloudsAmbientProbe);

            // left, right, up, down, back, front
            private readonly Vector3[] _cubemapDirs = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            private readonly Vector3[] _cubemapUps = new Vector3[] { Vector3.down, Vector3.down, Vector3.back, Vector3.forward, Vector3.left, Vector3.left };

#if UNITY_6000_0_OR_NEWER
            private readonly RendererListHandle[] _rendererListHandles = new RendererListHandle[6];
            private readonly Matrix4x4[] _skyViewMatrices = new Matrix4x4[6];
#endif

            private static readonly Matrix4x4 SkyProjectionMatrix = Matrix4x4.Perspective(90.0f, 1.0f, 0.1f, 10.0f);
            private static readonly Vector4 SkyViewScreenParams = new Vector4(16.0f, 16.0f, 1.0f + rcp(16.0f), 1.0f + rcp(16.0f));

            public VolumetricCloudsAmbientPass(Material material)
            {
                _cloudsMaterial = material;
            }

            #region Non Render Graph Pass
#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.useMipMap = true;
                desc.autoGenerateMips = true;
                desc.width = 16;
                desc.height = 16;
                desc.dimension = TextureDimension.Cube;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.depthBufferBits = 0;

#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: VolumetricCloudsAmbientProbe);
#else
            RenderingUtils.ReAllocateIfNeeded(ref probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsAmbientProbe);
#endif
                _cloudsMaterial.SetTexture(CloudsAmbientProbe, _probeColorHandle);

                ConfigureTarget(_probeColorHandle, _probeColorHandle);
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // UpdateEnvironment() is another way to update ambient lighting but it's really slow.
                //DynamicGI.UpdateEnvironment();

                Camera camera = renderingData.cameraData.camera;

                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                float2 cameraResolution = float2(desc.width, desc.height);
                Vector4 cameraScreenParams = new Vector4(cameraResolution.x, cameraResolution.y, 1.0f + rcp(cameraResolution.x), 1.0f + rcp(cameraResolution.y));

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    cmd.SetGlobalVector(WorldSpaceCameraPos, Vector3.zero);
                    cmd.SetGlobalFloat(DisableSunDisk, 1.0f);

                    for (int i = 0; i < 6; i++)
                    {
                        CoreUtils.SetRenderTarget(cmd, _probeColorHandle, ClearFlag.None, 0, (CubemapFace)i);

                        Matrix4x4 viewMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(_cubemapDirs[i], _cubemapUps[i]), Vector3.one);
                        viewMatrix.SetColumn(2, -viewMatrix.GetColumn(2));
                        if (i == 3) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.down * 180.0f)); }
                        else if (i == 4) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.left * -90.0f)); }
                        else if (i == 5) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.right * -90.0f)); }

                        // Set the Near & Far Plane to 0.1 and 10
                        Matrix4x4 skyMatrixVp = GL.GetGPUProjectionMatrix(SkyProjectionMatrix, true) * viewMatrix;

                        // Camera matrices for skybox rendering
                        cmd.SetViewMatrix(viewMatrix);
                        cmd.SetGlobalMatrix(UnityMatrixVp, skyMatrixVp);
                        cmd.SetGlobalMatrix(UnityMatrixInvVp, skyMatrixVp.inverse);
                        cmd.SetGlobalVector(ScaledScreenParams, SkyViewScreenParams);

                        // Can we exclude the sun disk in ambient probe?
                        RendererList rendererList = context.CreateSkyboxRendererList(camera, SkyProjectionMatrix, viewMatrix);
                        cmd.DrawRendererList(rendererList);
                    }
                }

                cmd.SetGlobalVector(WorldSpaceCameraPos, camera.transform.position);
                cmd.SetGlobalFloat(DisableSunDisk, 0.0f);

                Matrix4x4 worldToCameraMatrix;
                Matrix4x4 matrixVp = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * (worldToCameraMatrix = camera.worldToCameraMatrix);

                // Camera matrices for objects rendering
                cmd.SetViewMatrix(worldToCameraMatrix);
                cmd.SetGlobalMatrix(UnityMatrixVp, matrixVp);
                cmd.SetGlobalMatrix(UnityMatrixInvVp, matrixVp.inverse);
                cmd.SetGlobalVector(ScaledScreenParams, cameraScreenParams);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            
                CommandBufferPool.Release(cmd);
            }
            #endregion

#if UNITY_6000_0_OR_NEWER
            #region Render Graph Pass
            private class PassData
            {
                internal Material CloudsMaterial;

                internal TextureHandle ProbeColorHandle;

                internal Vector3 CameraPositionWs;
                internal Vector4 CameraScreenParams;
                internal Matrix4x4 WorldToCameraMatrix;
                internal Matrix4x4 ProjectionMatrix;

                internal RendererListHandle[] RendererListHandles;
                internal Matrix4x4[] SkyViewMatrices;
                internal Matrix4x4 SkyProjection;
            }

            // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
            static void ExecutePass(PassData data, UnsafeGraphContext context)
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                context.cmd.SetGlobalVector(WorldSpaceCameraPos, Vector3.zero);
                context.cmd.SetGlobalFloat(DisableSunDisk, 1.0f);

                for (int i = 0; i < 6; i++)
                {
                    CoreUtils.SetRenderTarget(cmd, data.ProbeColorHandle, ClearFlag.None, 0, (CubemapFace)i);

                    Matrix4x4 skyMatrixVp = GL.GetGPUProjectionMatrix(data.SkyProjection, true) * data.SkyViewMatrices[i];
                
                    // Camera matrices for skybox rendering
                    cmd.SetViewMatrix(data.SkyViewMatrices[i]);
                    context.cmd.SetGlobalMatrix(UnityMatrixVp, skyMatrixVp);
                    context.cmd.SetGlobalMatrix(UnityMatrixInvVp, skyMatrixVp.inverse);
                    context.cmd.SetGlobalVector(ScaledScreenParams, SkyViewScreenParams);

                    context.cmd.DrawRendererList(data.RendererListHandles[i]);
                }

                data.CloudsMaterial.SetTexture(CloudsAmbientProbe, data.ProbeColorHandle);

                context.cmd.SetGlobalVector(WorldSpaceCameraPos, data.CameraPositionWs);
                context.cmd.SetGlobalFloat(DisableSunDisk, 0.0f);

                Matrix4x4 matrixVp = GL.GetGPUProjectionMatrix(data.ProjectionMatrix, true) * data.WorldToCameraMatrix;

                // Camera matrices for objects rendering
                cmd.SetViewMatrix(data.WorldToCameraMatrix);
                context.cmd.SetGlobalMatrix(UnityMatrixVp, matrixVp);
                context.cmd.SetGlobalMatrix(UnityMatrixInvVp, matrixVp.inverse);
                context.cmd.SetGlobalVector(ScaledScreenParams, data.CameraScreenParams);
            }

            // This is where the renderGraph handle can be accessed.
            // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // add an unsafe render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
                using (var builder = renderGraph.AddUnsafePass<PassData>(ProfilerTag, out var passData))
                {
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                    float2 cameraResolution = float2(desc.width, desc.height);
                
                    desc.msaaSamples = 1;
                    desc.useMipMap = true;
                    desc.autoGenerateMips = true;
                    desc.width = 16;
                    desc.height = 16;
                    desc.dimension = TextureDimension.Cube;
                    desc.depthBufferBits = 0;
                    RenderingUtils.ReAllocateHandleIfNeeded(ref _probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: VolumetricCloudsAmbientProbe);
                    TextureHandle probeColorTextureHandle = renderGraph.ImportTexture(_probeColorHandle);
                    passData.ProbeColorHandle = probeColorTextureHandle;
                    passData.CloudsMaterial = _cloudsMaterial;

                    // Set the Near & Far Plane to 0.1 and 10
                    for (int i = 0; i < 6; i++)
                    {
                        Matrix4x4 viewMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(_cubemapDirs[i], _cubemapUps[i]), Vector3.one);
                        viewMatrix.SetColumn(2, -viewMatrix.GetColumn(2));
                        if (i == 3) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.down * 180.0f)); }
                        else if (i == 4) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.left * -90.0f)); }
                        else if (i == 5) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.right * -90.0f)); }

                        _skyViewMatrices[i] = viewMatrix;
                        _rendererListHandles[i] = renderGraph.CreateSkyboxRendererList(cameraData.camera, SkyProjectionMatrix, viewMatrix);
                        builder.UseRendererList(_rendererListHandles[i]);
                    }

                    // Fill up the passData with the data needed by the pass
                    passData.RendererListHandles = _rendererListHandles;
                    passData.SkyViewMatrices = _skyViewMatrices;
                    passData.SkyProjection = SkyProjectionMatrix;
                    passData.CloudsMaterial = _cloudsMaterial;
                    passData.CameraPositionWs = cameraData.camera.transform.position;
                    passData.CameraScreenParams = new Vector4(cameraResolution.x, cameraResolution.y, 1.0f + rcp(cameraResolution.x), 1.0f + rcp(cameraResolution.y));
                    passData.WorldToCameraMatrix = cameraData.camera.worldToCameraMatrix;
                    passData.ProjectionMatrix = cameraData.camera.projectionMatrix;
                
                    // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                    builder.UseTexture(passData.ProbeColorHandle, AccessFlags.Write);

                    // Disable pass culling because the ambient probe is not used by other pass
                    builder.AllowPassCulling(false);

                    // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
                }
            }
            #endregion
#endif

            #region Shared
            public void Dispose()
            {
                _probeColorHandle?.Release();
            }
            #endregion
        }
        public class VolumetricCloudsShadowsPass : ScriptableRenderPass
        {
            private const string ProfilerTag = "Volumetric Clouds Shadows";
            private readonly ProfilingSampler _mProfilingSampler = new ProfilingSampler(ProfilerTag);
            private readonly Material _cloudsMaterial;

            private RTHandle _shadowTextureHandle;
            private RTHandle _intermediateShadowTextureHandle;

            private readonly Vector3[] _frustumCorners = new Vector3[4];

            private Light _targetLight;
        
            private static readonly int CloudShadowSunOrigin = Shader.PropertyToID("_CloudShadowSunOrigin");
            private static readonly int CloudShadowSunRight = Shader.PropertyToID("_CloudShadowSunRight");
            private static readonly int CloudShadowSunUp = Shader.PropertyToID("_CloudShadowSunUp");
            private static readonly int CloudShadowSunForward = Shader.PropertyToID("_CloudShadowSunForward");
            private static readonly int CameraPositionPS = Shader.PropertyToID("_CameraPositionPS");
            private static readonly int VolumetricCloudsShadowOriginToggle = Shader.PropertyToID("_VolumetricCloudsShadowOriginToggle");
            private static readonly int VolumetricCloudsShadowScale = Shader.PropertyToID("_VolumetricCloudsShadowScale");
            //private static readonly int shadowPlaneOffset = Shader.PropertyToID("_ShadowPlaneOffset");

            private const string VolumetricCloudsShadowTexture = "_VolumetricCloudsShadowTexture";
            private const string VolumetricCloudsShadowTempTexture = "_VolumetricCloudsShadowTempTexture";

            private const string LightCookies = "_LIGHT_COOKIES";

            private static readonly Matrix4x4 DirLightProj = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f);

            private static readonly int MainLightTexture = Shader.PropertyToID("_MainLightCookieTexture");
            private static readonly int MainLightWorldToLight = Shader.PropertyToID("_MainLightWorldToLight");
            private static readonly int MainLightCookieTextureFormat = Shader.PropertyToID("_MainLightCookieTextureFormat");

            public VolumetricCloudsShadowsPass(Material material)
            {
                _cloudsMaterial = material;
            }

            #region Non Render Graph Pass
            private Light GetMainLight(LightData lightData)
            {
                int shadowLightIndex = lightData.mainLightIndex;
                if (shadowLightIndex != -1)
                {
                    VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                    Light light = shadowLight.light;
                    if (light.shadows != LightShadows.None && shadowLight.lightType == LightType.Directional)
                        return light;
                }

                return RenderSettings.sun;
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Should we support colored shadows?
                GraphicsFormat cookieFormat = GraphicsFormat.R16_UNorm; //option 2: R8_UNorm
#if UNITY_2023_2_OR_NEWER
                bool useSingleChannel = SystemInfo.IsFormatSupported(cookieFormat, GraphicsFormatUsage.Render);
#else
            bool useSingleChannel = SystemInfo.IsFormatSupported(cookieFormat, FormatUsage.Render);
#endif
                cookieFormat = useSingleChannel ? cookieFormat : GraphicsFormat.B10G11R11_UFloatPack32;
            
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.useMipMap = false;
                desc.graphicsFormat = cookieFormat;
            
#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: VolumetricCloudsShadowTexture);
#else
            RenderingUtils.ReAllocateIfNeeded(ref shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTexture);
#endif

#if UNITY_6000_0_OR_NEWER
                RenderingUtils.ReAllocateHandleIfNeeded(ref _intermediateShadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: VolumetricCloudsShadowTempTexture);
#else
            RenderingUtils.ReAllocateIfNeeded(ref intermediateShadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTempTexture);
#endif

                ConfigureTarget(_shadowTextureHandle, _shadowTextureHandle);
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CameraData cameraData = renderingData.cameraData;
                Camera camera = cameraData.camera;
                LightData lightData = renderingData.lightData;

                // Get and update the main light
                Light light = GetMainLight(lightData);
                if (_targetLight != light)
                {
                    ResetShadowCookie();
                    _targetLight = light;
                }

                // Check if we need shadow cookie
                bool hasVolumetricCloudsShadows = _targetLight != null && _targetLight.isActiveAndEnabled && _targetLight.intensity != 0.0f;
                if (!hasVolumetricCloudsShadows)
                {
                    ResetShadowCookie();
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                var targetLightTransform = _targetLight.transform;
                Matrix4x4 wsToLsMat = targetLightTransform.worldToLocalMatrix;
                using (new ProfilingScope(cmd, _mProfilingSampler))
                {
                    Matrix4x4 lsToWsMat = targetLightTransform.localToWorldMatrix;

                    float3 cameraPos = camera.transform.position;

                    camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _frustumCorners);

                    // Generate the light space bounds of the camera frustum
                    Bounds lightSpaceBounds = new Bounds();
                    lightSpaceBounds.SetMinMax(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue));
                    lightSpaceBounds.Encapsulate(wsToLsMat.MultiplyPoint(cameraPos));
                    for (int cornerIdx = 0; cornerIdx < 4; ++cornerIdx)
                    {
                        Vector3 corner = _frustumCorners[cornerIdx];
                        Vector3 posLightSpace = wsToLsMat.MultiplyPoint(new float3(corner) + cameraPos);
                        lightSpaceBounds.Encapsulate(posLightSpace);
                        posLightSpace = wsToLsMat.MultiplyPoint(new float3(-corner) + cameraPos);
                        lightSpaceBounds.Encapsulate(posLightSpace);
                    }

                    // Compute the four corners we need
                    float3 c0 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                    float3 c1 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                    float3 c2 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));

                    float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, 0) * VolumetricCloudsPass.EarthRad;
                    float3 planetCenterPos = float3(0.0f, -actualEarthRad * VolumetricCloudsPass._transform.lossyScale.x, 0.0f);

                    float3 dirX = c1 - c0;
                    float3 dirY = c2 - c0;

                    // The shadow cookie size
                    float2 regionSize = float2(length(dirX), length(dirY));

                    // Update material properties
                    //cloudsMaterial.SetFloat(shadowPlaneOffset, cloudsVolume.shadowPlaneHeightOffset.value);
                    _cloudsMaterial.SetVector(CloudShadowSunOrigin, float4(c0 - planetCenterPos, 1.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunRight, float4(dirX, 0.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunUp, float4(dirY, 0.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunForward, float4(-_targetLight.transform.forward, 0.0f));
                    _cloudsMaterial.SetVector(CameraPositionPS, float4(cameraPos - planetCenterPos, 0.0f));
                    cmd.SetGlobalVector(VolumetricCloudsShadowOriginToggle, float4(c0, 0.0f));
                    cmd.SetGlobalVector(VolumetricCloudsShadowScale, float4(regionSize, 0.0f, 0.0f)); // Used in physically based sky

                    // Apply light cookie settings
                    _targetLight.cookie = null;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieSize = Vector2.one;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieOffset = Vector2.zero;

                    Vector2 uvScale = 1 / regionSize;
                    float minHalfValue = Unity.Mathematics.half.MinValue;
                    if (Mathf.Abs(uvScale.x) < minHalfValue)
                        uvScale.x = Mathf.Sign(uvScale.x) * minHalfValue;
                    if (Mathf.Abs(uvScale.y) < minHalfValue)
                        uvScale.y = Mathf.Sign(uvScale.y) * minHalfValue;

                    Matrix4x4 cookieUVTransform = Matrix4x4.Scale(new Vector3(uvScale.x, uvScale.y, 1));
                    lsToWsMat.SetColumn(3, float4(cameraPos, 1));
                    Matrix4x4 cookieMatrix = DirLightProj * cookieUVTransform * lsToWsMat.inverse;

                    float cookieFormat = (float)GetLightCookieShaderFormat(_shadowTextureHandle.rt.graphicsFormat);

                    cmd.SetGlobalTexture(MainLightTexture, _shadowTextureHandle);
                    cmd.SetGlobalMatrix(MainLightWorldToLight, cookieMatrix);
                    cmd.SetGlobalFloat(MainLightCookieTextureFormat, cookieFormat);
                    cmd.EnableShaderKeyword(LightCookies);

                    // Render shadow cookie texture
                    Blitter.BlitCameraTexture(cmd, _shadowTextureHandle, _shadowTextureHandle, _cloudsMaterial, pass: 4);

                    // Given the low number of steps available and the absence of noise in the integration, we try to reduce the artifacts by doing two consecutive 3x3 blur passes.
                    Blitter.BlitCameraTexture(cmd, _shadowTextureHandle, _intermediateShadowTextureHandle, _cloudsMaterial, pass: 5);
                    Blitter.BlitCameraTexture(cmd, _intermediateShadowTextureHandle, _shadowTextureHandle, _cloudsMaterial, pass: 5);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
            #endregion

#if UNITY_6000_0_OR_NEWER
            #region Render Graph Pass
            private Light GetMainLight(UniversalLightData lightData)
            {
                int shadowLightIndex = lightData.mainLightIndex;
                if (shadowLightIndex != -1)
                {
                    VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                    Light light = shadowLight.light;
                    if (light.shadows != LightShadows.None && shadowLight.lightType == LightType.Directional)
                        return light;
                }

                return RenderSettings.sun;
            }

            private class PassData
            {
                internal Material CloudsMaterial;

                internal TextureHandle IntermediateShadowTexture;
                internal TextureHandle ShadowTexture;

                internal Matrix4x4 LightWorldToLight;
                internal float LightCookieTextureFormat;

                internal Vector4 ShadowOriginToggle;
                internal Vector4 ShadowScale;
            }

            // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
            static void ExecutePass(PassData data, UnsafeGraphContext context)
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                // Render shadow cookie texture
                Blitter.BlitCameraTexture(cmd, data.ShadowTexture, data.ShadowTexture, data.CloudsMaterial, pass: 4);

                // Given the low number of steps available and the absence of noise in the integration, we try to reduce the artifacts by doing two consecutive 3x3 blur passes.
                Blitter.BlitCameraTexture(cmd, data.ShadowTexture, data.IntermediateShadowTexture, data.CloudsMaterial, pass: 5);
                Blitter.BlitCameraTexture(cmd, data.IntermediateShadowTexture, data.ShadowTexture, data.CloudsMaterial, pass: 5);

                cmd.SetGlobalVector(VolumetricCloudsShadowOriginToggle, data.ShadowOriginToggle);
                cmd.SetGlobalVector(VolumetricCloudsShadowScale, data.ShadowScale); // Used in physically based sky

                cmd.SetGlobalTexture(MainLightTexture, data.ShadowTexture);
                cmd.SetGlobalMatrix(MainLightWorldToLight, data.LightWorldToLight);
                cmd.SetGlobalFloat(MainLightCookieTextureFormat, data.LightCookieTextureFormat);
                cmd.EnableShaderKeyword(LightCookies);
            }

            // This is where the renderGraph handle can be accessed.
            // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // Get and update the main light
                Light light = GetMainLight(lightData);
                if (_targetLight != light)
                {
                    ResetShadowCookie();
                    _targetLight = light;
                }

                // Check if we need shadow cookie
                bool hasVolumetricCloudsShadows = _targetLight != null && _targetLight.isActiveAndEnabled && _targetLight.intensity != 0.0f;
                if (!hasVolumetricCloudsShadows)
                {
                    ResetShadowCookie();
                    return;
                }

                var camera = cameraData.camera;

                // add an unsafe render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
                Matrix4x4 wsToLsMat = _targetLight.transform.worldToLocalMatrix;
                using (var builder = renderGraph.AddUnsafePass<PassData>(ProfilerTag, out var passData))
                {
                    // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
                    // The active color and depth textures are the main color and depth buffers that the camera renders into

                    Matrix4x4 lsToWsMat = _targetLight.transform.localToWorldMatrix;

                    float3 cameraPos = camera.transform.position;

                    camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _frustumCorners);

                    // Generate the light space bounds of the camera frustum
                    Bounds lightSpaceBounds = new Bounds();
                    lightSpaceBounds.SetMinMax(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue));
                    lightSpaceBounds.Encapsulate(wsToLsMat.MultiplyPoint(cameraPos));
                    for (int cornerIdx = 0; cornerIdx < 4; ++cornerIdx)
                    {
                        Vector3 corner = _frustumCorners[cornerIdx];
                        Vector3 posLightSpace = wsToLsMat.MultiplyPoint(float3(corner) + cameraPos);
                        lightSpaceBounds.Encapsulate(posLightSpace);

                        posLightSpace = wsToLsMat.MultiplyPoint(float3(-corner) + cameraPos);
                        lightSpaceBounds.Encapsulate(posLightSpace);
                    }
                
                    // Compute the four corners we need
                    float3 c0 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                    float3 c1 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                    float3 c2 = lsToWsMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));

                    float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, 0) * VolumetricCloudsPass.EarthRad;
                    float3 planetCenterPos = float3(0.0f, -actualEarthRad  * VolumetricCloudsPass._transform.lossyScale.x, 0.0f);

                    float3 dirX = c1 - c0;
                    float3 dirY = c2 - c0;
                 
                    // The shadow cookie size
                    float2 regionSize = float2(length(dirX), length(dirY));

                    // Should we support colored shadows?
                    GraphicsFormat cookieTextureFormat = GraphicsFormat.R16_UNorm; //option 2: R8_UNorm
                    bool useSingleChannel = SystemInfo.IsFormatSupported(cookieTextureFormat, GraphicsFormatUsage.Render);
                    cookieTextureFormat = useSingleChannel ? cookieTextureFormat : GraphicsFormat.B10G11R11_UFloatPack32;
                
                    RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                    desc.msaaSamples = 1;
                    desc.depthBufferBits = 0;
                    desc.useMipMap = false;
                    desc.graphicsFormat = cookieTextureFormat;
                    RenderingUtils.ReAllocateHandleIfNeeded(ref _shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: VolumetricCloudsShadowTexture);
                    TextureHandle shadowTexture = renderGraph.ImportTexture(_shadowTextureHandle);
                    _cloudsMaterial.SetVector(CloudShadowSunOrigin, float4(c0 - planetCenterPos, 1.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunRight, float4(dirX, 0.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunUp, float4(dirY, 0.0f));
                    _cloudsMaterial.SetVector(CloudShadowSunForward, float4(-_targetLight.transform.forward, 0.0f));
                    _cloudsMaterial.SetVector(CameraPositionPS, float4(cameraPos - planetCenterPos, 0.0f));
                    _cloudsMaterial.SetVector(VolumetricCloudsShadowOriginToggle, float4(c0, 0.0f));

                    // Apply light cookie settings
                    _targetLight.cookie = null;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieSize = Vector2.one;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieOffset = Vector2.zero;

                    // Apply shadow cookie
                    Vector2 uvScale = 1 / regionSize;
                    float minHalfValue = Unity.Mathematics.half.MinValue;
                    if (Mathf.Abs(uvScale.x) < minHalfValue)
                        uvScale.x = Mathf.Sign(uvScale.x) * minHalfValue;
                    if (Mathf.Abs(uvScale.y) < minHalfValue)
                        uvScale.y = Mathf.Sign(uvScale.y) * minHalfValue;

                    Matrix4x4 cookieUVTransform = Matrix4x4.Scale(new Vector3(uvScale.x, uvScale.y, 1));
                    //cookieUVTransform.SetColumn(3, new Vector4(uvScale.x, uvScale.y, 0, 1));
                    lsToWsMat.SetColumn(3, float4(cameraPos, 1));
                    Matrix4x4 cookieMatrix = DirLightProj * cookieUVTransform * lsToWsMat.inverse;

                    float cookieFormat = (float)GetLightCookieShaderFormat(cookieTextureFormat);

                    // Fill up the passData with the data needed by the pass
                    passData.CloudsMaterial = _cloudsMaterial;
                    passData.ShadowTexture = shadowTexture;
                    passData.LightWorldToLight = cookieMatrix;
                    passData.LightCookieTextureFormat = cookieFormat;
                    passData.ShadowOriginToggle = float4(c0, 0.0f);
                    passData.ShadowScale = float4(regionSize, 0.0f, 0.0f);

                    // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                    builder.UseTexture(passData.ShadowTexture, AccessFlags.Write);
                    builder.UseTexture(passData.IntermediateShadowTexture, AccessFlags.Write); // We always write to it before reading

                    // Shader keyword changes (_LIGHT_COOKIES) are considered as global state modifications
                    builder.AllowGlobalStateModification(true);
                    // Disable pass culling because the cookie texture is not used by other pass
                    builder.AllowPassCulling(false);

                    // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
                }
            }
            #endregion
#endif

            #region Shared
            private enum LightCookieShaderFormat
            {
                RGB = 0,
                Alpha = 1,
                Red = 2
            }

            private LightCookieShaderFormat GetLightCookieShaderFormat(GraphicsFormat cookieFormat)
            {
                // TODO: convert this to use GraphicsFormatUtility
                switch (cookieFormat)
                {
                    default:
                        return LightCookieShaderFormat.RGB;
                    // A8, A16 GraphicsFormat does not expose yet.
                    case (GraphicsFormat)54:
                    case (GraphicsFormat)55:
                        return LightCookieShaderFormat.Alpha;
                    case GraphicsFormat.R8_SRGB:
                    case GraphicsFormat.R8_UNorm:
                    case GraphicsFormat.R8_UInt:
                    case GraphicsFormat.R8_SNorm:
                    case GraphicsFormat.R8_SInt:
                    case GraphicsFormat.R16_UNorm:
                    case GraphicsFormat.R16_UInt:
                    case GraphicsFormat.R16_SNorm:
                    case GraphicsFormat.R16_SInt:
                    case GraphicsFormat.R16_SFloat:
                    case GraphicsFormat.R32_UInt:
                    case GraphicsFormat.R32_SInt:
                    case GraphicsFormat.R32_SFloat:
                    case GraphicsFormat.R_BC4_SNorm:
                    case GraphicsFormat.R_BC4_UNorm:
                    case GraphicsFormat.R_EAC_SNorm:
                    case GraphicsFormat.R_EAC_UNorm:
                        return LightCookieShaderFormat.Red;
                }
            }

            private void ResetShadowCookie()
            {
                if (_targetLight != null)
                {
                    _targetLight.cookie = null;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieSize = Vector2.one;
                    _targetLight.GetComponent<UniversalAdditionalLightData>().lightCookieOffset = Vector2.zero;
                }
            }

            public void Dispose()
            {
                ResetShadowCookie();
                _shadowTextureHandle?.Release();
                _intermediateShadowTextureHandle?.Release();
            }
            #endregion
        }
    }
}

//MIT License
//
//Copyright (c) 2024 jiaozi158
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//    of this software and associated documentation files (the "Software"), to deal
//    in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//    furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//    copies or substantial portions of the Software.
//
//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.