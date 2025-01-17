using System;
using UnityEngine;

namespace CelestialBodies
{
    [Serializable]
    public struct Fog
    {
        /// <summary>
        /// The radius of the sphere volume.
        /// </summary>
        [Tooltip("The radius of the sphere volume.")]
        public float Radius;

        /// <summary>
        /// The maximum world-y that this fog can occur.
        /// </summary>
        [Tooltip("The maximum world-y that this fog can occur.")]
        public float MaxY;

        /// <summary>
        /// The distance to fade the fog as it nears the maximum world-y.
        /// </summary>
        [Tooltip("The distance to fade the fog as it nears the maximum world-y.")]
        public float YFade;

        /// <summary>
        /// The distance to fade the fog as it nears any edge of the bounding sphere.
        /// </summary>
        [Tooltip("The distance to fade the fog as it nears any edge of the bounding sphere.")]
        public float EdgeFade;

        /// <summary>
        /// Distance from the camera that the fog reaches full intensity.
        /// </summary>
        [Tooltip("Distance from the camera that the fog reaches full intensity.")]
        public float ProximityFade;

        /// <summary>
        /// The density of the fog.
        /// </summary>
        [Tooltip("The density of the fog.")]
        [Range(0.0f, 10.0f)]
        public float FogDensity;

        internal float actualFogDensity;
        /// <summary>
        /// Exponential modifier applied to the primary fog noise map.
        /// </summary>
        [Tooltip("Exponential modifier applied to the primary fog noise map.")]
        [Range(1.0f, 16.0f)]
        public float FogExponent;

        /// <summary>
        /// Exponential modifier applied to the detail fog noise map.
        /// </summary>
        [Tooltip("Exponential modifier applied to the detail fog noise map.")]
        [Range(1.0f, 16.0f)]
        public float DetailFogExponent;

        /// <summary>
        /// Lower bound noise value.
        /// </summary>
        [Tooltip("Lower bound noise value.")]
        [Range(0.0f, 1.0f)]
        public float FogShapeMask;

        /// <summary>
        /// Contribution of primary fog vs detail fog. 0 = all primary fog, 1 = all detail fog.
        /// </summary>
        [Tooltip("Contribution of primary fog vs detail fog. 0 = all primary fog, 1 = all detail fog.")]
        [Range(0.0f, 1.0f)]
        public float FogDetailStrength;

        /// <summary>
        /// Color of the fog facing away from the primary light source. Alpha influence fog density.
        /// </summary>
        [Tooltip("Color of the fog facing away from the primary light source. Alpha influence fog density.")]
        [ColorUsage(true, true)]
        public Color FogColor;

        /// <summary>
        /// Color of the fog facing the primary light source. Alpha influence fog density.
        /// </summary>
        [Tooltip("Color of the fog facing the primary light source. Alpha influence fog density.")]
        [ColorUsage(true, true)]
        public Color DirectionalFogColor;

        /// <summary>
        /// Exponential fall-off factor for the directional light source. Higher the value, faster is falls-off.
        /// </summary>
        [Tooltip("Exponential fall-off factor for the directional light source. Higher the value, faster is falls-off.")]
        [Range(1.0f, 16.0f)]
        public float DirectionalFallOff;

        /// <summary>
        /// How much the fog color is influenced by the primary light source when facing away.
        /// </summary>
        [Tooltip("How much the fog color is influenced by the primary light source when facing away.")]
        [Range(0.0f, 1.0f)]
        public float LightContribution;

        /// <summary>
        /// How much the folow color is influenced by the primary light source when facing toward it.
        /// </summary>
        [Tooltip("How much the folow color is influenced by the primary light source when facing toward it.")]
        [Range(0.0f, 1.0f)]
        public float DirectionalLightContribution;

        /// <summary>
        /// How much does shadows darken the fog.
        /// </summary>
        [Tooltip("How much does shadows darken the fog.")]
        [Range(0.0f, 1.0f)]
        public float ShadowStrength;

        /// <summary>
        /// How much do shadows darken the fog when facing away from the light source.
        /// </summary>
        [Tooltip("How much do shadows darken the fog when facing away from the light source.")]
        [Range(0.0f, 1.0f)]
        public float ShadowReverseStrength;

        /// <summary>
        /// Direction the fog is moving.
        /// </summary>
        [Tooltip("Direction the fog is moving.")]
        public Vector3 FogDirection ;

        /// <summary>
        /// The speed the fog is moving.
        /// </summary>
        [Tooltip("The speed the fog is moving.")]
        public float FogSpeed;

        /// <summary>
        /// Speed multiplier applied to the detail fog.
        /// </summary>
        [Tooltip("Speed multiplier applied to the detail fog.")]
        public float DetailFogSpeedModifier;

        /// <summary>
        /// Tiling for the primary fog noise.
        /// </summary>
        [Tooltip("Tiling for the primary fog noise.")]
        public Vector3 FogTiling;

        /// <summary>
        /// Tiling for the detail fog noise.
        /// </summary>
        [Tooltip("Tiling for the detail fog noise.")]
        public Vector3 DetailFogTiling;

        public float DisableDistance;
        
        internal GameObject gameObject;

        internal int id;

        public static Fog Default()
        {
            Fog newFog = new Fog();
            newFog.Radius = 1e+08f;
            newFog.MaxY = 1e+08f;
            newFog.YFade = 1e+08f;
            newFog.EdgeFade = 50.0f;
            newFog.ProximityFade = 15.0f;
            newFog.FogDensity = 1.2f;
            newFog.actualFogDensity = 0;
            newFog.FogExponent = 1.0f;
            newFog.DetailFogExponent = 1.0f;
            newFog.FogShapeMask = 0.25f;
            newFog.FogDetailStrength = 0.4f;
            newFog.FogColor = Color.white;
            newFog.DirectionalFogColor = Color.white;
            newFog.DirectionalFallOff = 2.0f;
            newFog.LightContribution = 1.0f;
            newFog.DirectionalLightContribution = 1.0f;
            newFog.ShadowStrength = 1.0f;
            newFog.ShadowReverseStrength = 0.3f;
            newFog.FogDirection = new Vector3(1.0f, 0.0f, 0.0f);
            newFog.FogSpeed = 30.0f;
            newFog.DetailFogSpeedModifier = 1.5f;
            newFog.FogTiling = new Vector3(0.0015f, 0.0015f, 0.0015f);
            newFog.DetailFogTiling = new Vector3(0.001f, 0.001f, 0.001f);
            newFog.DisableDistance = 1000;
            return new Fog();
        }
    }

    static class FogBuilder
    {
        public static void Update(this ref Fog fog,float radius)
        {
            var currentFogDensity = fog.actualFogDensity;
            var distance = Vector3.Distance(Camera.main.transform.position, fog.gameObject.transform.position);
            fog.actualFogDensity = Mathf.Max(map(distance, radius, radius + fog.DisableDistance, fog.FogDensity, 0), 0);
            if(Math.Abs(fog.actualFogDensity - currentFogDensity) > 0.001f)
                VolumetricFogPass.UpdateFogVolume(fog);
        }
        
        static float map(float x, float in_min, float in_max, float out_min, float out_max)
        {
            return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
        }
        
        internal static void Start(this ref Fog fog, GameObject gameObject)
        {
            fog.gameObject = gameObject;
            fog.id = VolumetricFogPass.AddFogVolume(fog);
        }

        internal static void UpdateFogSettings(this ref Fog fog)
        {
            VolumetricFogPass.UpdateFogVolume(fog);
        }

        internal static void Destroy(this ref Fog fog)
        {
            VolumetricFogPass.RemoveFogVolume(fog);
        }

        internal static void Apply(this ref Fog fog, MaterialPropertyBlock propertyBlock)
        {
            propertyBlock.SetVector(Properties.BoundingSphere, new Vector4(fog.gameObject.transform.position.x, fog.gameObject.transform.position.y, fog.gameObject.transform.position.z, fog.Radius));
            propertyBlock.SetFloat(Properties.FogMaxY, fog.MaxY);
            propertyBlock.SetFloat(Properties.FogFadeY, fog.YFade);
            propertyBlock.SetFloat(Properties.FogFadeEdge, fog.EdgeFade);
            propertyBlock.SetFloat(Properties.FogProximityFade, fog.ProximityFade);
            propertyBlock.SetFloat(Properties.FogDensity, fog.actualFogDensity);
            propertyBlock.SetFloat(Properties.FogExponent, fog.FogExponent);
            propertyBlock.SetFloat(Properties.DetailFogExponent, fog.DetailFogExponent);
            propertyBlock.SetFloat(Properties.FogCutOff, fog.FogShapeMask);
            propertyBlock.SetFloat(Properties.FogDetailStrength, fog.FogDetailStrength);
            propertyBlock.SetColor(Properties.FogColor, fog.FogColor);
            propertyBlock.SetColor(Properties.DirectionalFogColor, fog.DirectionalFogColor);
            propertyBlock.SetFloat(Properties.DirectionalFallExponent, fog.DirectionalFallOff);
            propertyBlock.SetFloat(Properties.ShadowStrength, fog.ShadowStrength);
            propertyBlock.SetFloat(Properties.ShadowReverseStrength, fog.ShadowReverseStrength);
            propertyBlock.SetFloat(Properties.LightContribution, fog.LightContribution);
            propertyBlock.SetFloat(Properties.DirectionalLightContribution, fog.DirectionalLightContribution);
            propertyBlock.SetVector(Properties.FogTiling, fog.FogTiling);
            propertyBlock.SetVector(Properties.DetailFogTiling, fog.DetailFogTiling);
            propertyBlock.SetVector(Properties.FogSpeed, fog.FogDirection.normalized * fog.FogSpeed);
            propertyBlock.SetFloat(Properties.DetailFogSpeedModifier, fog.DetailFogSpeedModifier);
        }
        
        private static class Properties
        {
            public static readonly int BoundingSphere = Shader.PropertyToID("_BoundingSphere");
            public static readonly int FogMaxY = Shader.PropertyToID("_FogMaxY");
            public static readonly int FogFadeY = Shader.PropertyToID("_FogFadeY");
            public static readonly int FogFadeEdge = Shader.PropertyToID("_FogFadeEdge");
            public static readonly int FogProximityFade = Shader.PropertyToID("_FogProximityFade");
            public static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
            public static readonly int FogExponent = Shader.PropertyToID("_FogExponent");
            public static readonly int DetailFogExponent = Shader.PropertyToID("_DetailFogExponent");
            public static readonly int FogCutOff = Shader.PropertyToID("_FogCutOff");
            public static readonly int FogDetailStrength = Shader.PropertyToID("_FogDetailStrength");
            public static readonly int FogColor = Shader.PropertyToID("_FogColor");
            public static readonly int DirectionalFogColor = Shader.PropertyToID("_DirectionalFogColor");
            public static readonly int DirectionalFallExponent = Shader.PropertyToID("_DirectionalFallExponent");
            public static readonly int ShadowStrength = Shader.PropertyToID("_ShadowStrength");
            public static readonly int ShadowReverseStrength = Shader.PropertyToID("_ShadowReverseStrength");
            public static readonly int LightContribution = Shader.PropertyToID("_LightContribution");
            public static readonly int DirectionalLightContribution = Shader.PropertyToID("_DirectionalLightContribution");
            public static readonly int FogTiling = Shader.PropertyToID("_FogTiling");
            public static readonly int FogSpeed = Shader.PropertyToID("_FogSpeed");
            public static readonly int DetailFogTiling = Shader.PropertyToID("_DetailFogTiling");
            public static readonly int DetailFogSpeedModifier = Shader.PropertyToID("_DetailFogSpeedModifier");
        }
    }
}
