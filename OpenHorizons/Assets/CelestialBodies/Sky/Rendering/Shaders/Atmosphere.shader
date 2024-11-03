Shader "Hidden/Atmosphere"
{
    HLSLINCLUDE
    #ifndef GENERAL_INCLUDED
    #define GENERAL_INCLUDED

    // Defines all the defult unity shader variables and includes commonly-used files.

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4x4 unity_PrevObjectToWorldArray;
    float4x4 unity_PrevWorldToObjectArray;
    real4 unity_WorldTransformParams;

    float4x4 unity_MatrixVP;
    float4x4 unity_MatrixV;
    float4x4 unity_PrevMatrixV;
    float4x4 glstate_matrix_projection;

    float4x4 unity_CameraProjection;
    float4x4 unity_CameraInvProjection;
    float4x4 unity_CameraToWorld;

    float4 _ScreenParams;
    float4 _ZBufferParams;
    float4 unity_OrthoParams;
    float4 _WorldSpaceCameraPos;

    #define UNITY_MATRIX_M unity_ObjectToWorld
    #define UNITY_MATRIX_I_M unity_WorldToObject
    #define UNITY_PREV_MATRIX_M unity_PrevObjectToWorldArray
    #define UNITY_PREV_MATRIX_I_M unity_PrevWorldToObjectArray
    #define UNITY_MATRIX_V unity_MatrixV
    #define UNITY_MATRIX_I_V unity_PrevMatrixV
    #define UNITY_MATRIX_VP unity_MatrixVP
    #define UNITY_MATRIX_P glstate_matrix_projection

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

    #endif


    #ifndef COMPOSITE_DEPTH_INCLUDED
    #define COMPOSITE_DEPTH_INCLUDED

    // Include this in a shader to use secondary camera depth


    // Camera depth
    TEXTURE2D(_CameraDepthTexture);
    SAMPLER(sampler_CameraDepthTexture);

    // Previous camera depth
    TEXTURE2D(_PrevCameraDepth);
    SAMPLER(sampler_PrevCameraDepth);

    int _RenderOverlay;

    // Normal, raw depth.
    float SampleDepth(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
    }

    // Previous camera's depth
    float4 SamplePrevDepth(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_PrevCameraDepth, sampler_PrevCameraDepth, uv);
    }


    // Composite depth sample- If the current depth sample goes past the depth buffer's bounds, the secondary depth buffer is sampled insted
    float4 SampleCompositeDepth(float2 uv)
    {
        float rawDepth = SampleDepth(uv);
        float4 compositeDepth = 0;

        if (_RenderOverlay == 1 && rawDepth <= 0.0)
        {
            // If rendering an overlay and end of depth is reached:
            compositeDepth = SamplePrevDepth(uv);
        }
        else
        {
            // Normal scene depth
            compositeDepth.x = rawDepth;
            compositeDepth.y = 0;
            compositeDepth.zw = _ZBufferParams.zw;
        }

        return compositeDepth;
    }


    float CompositeDepthRaw(float2 uv)
    {
        return SampleCompositeDepth(uv).x;
    }


    float CompositeDepth01(float2 uv)
    {
        float4 compositeDepth = SampleCompositeDepth(uv);
        return Linear01Depth(compositeDepth.x, compositeDepth);
    }


    float CompositeDepthEye(float2 uv)
    {
        float4 compositeDepth = SampleCompositeDepth(uv);

        return LinearEyeDepth(compositeDepth.x, compositeDepth);
    }

    // Linear depth scaled by camera view ray distance- useful for finding world position of a fragment or for ray-marching 
    float CompositeDepthScaled(float2 uv, float viewLength, out bool isEndOfDepth)
    {
        float rawDepth = SampleDepth(uv);

        isEndOfDepth = rawDepth <= 0.0;

        if (_RenderOverlay == 1 && isEndOfDepth)
        {
            // If rendering an overlay and end of depth is reached:
            float4 encInfo = SamplePrevDepth(uv);

            isEndOfDepth = encInfo.x <= 0.0;

            return LinearEyeDepth(encInfo.x, encInfo) * encInfo.y;
        }

        return LinearEyeDepth(rawDepth, _ZBufferParams) * viewLength;
    }

    #endif
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Off

        Pass
        {
            Name "RenderAtmosphere"

            HLSLPROGRAM
            #pragma vertex AtmosphereVertex
            #pragma fragment AtmosphereFragment

            #pragma target 4.0


            #define MAX_LOOP_ITERATIONS 30
            #pragma shader_feature DIRECTIONAL_SUN

            TEXTURE2D(_BakedOpticalDepth);
            float4 _BakedOpticalDepth_TexelSize;
            SAMPLER(sampler_BakedOpticalDepth);


            float3 _SunParams;
            float3 _PlanetCenter;

            // Celestial body radii
            float _PlanetRadius;
            float _AtmosphereRadius;
            float _OceanRadius;

            // Scttering steps
            int _NumInScatteringPoints;
            int _NumOpticalDepthPoints;

            // Rayleigh scattering coefficients
            float3 _RayleighScattering;

            // Mie scattering coeficcients
            float3 _MieScattering;
            float _MieG;

            // Ozone absorbtion coefficients
            float3 _AbsorbtionBeta;

            // Ambient atmosphere color
            float3 _AmbientBeta;

            // Density falloffs
            float _RayleighFalloff;
            float _MieFalloff;
            float _HeightAbsorbtion;

            // Light intensity
            float _Intensity;

            #define ATMOSPHERE_MODEL_SIM

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };


            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };


            TEXTURE2D(_Source);
            SAMPLER(sampler_Source);


            v2f AtmosphereVertex(appdata v)
            {
                v2f output;
                output.pos = TransformObjectToHClip(v.vertex.xyz);
                output.uv = v.uv.xy;
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1)).xyz;
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;
                return output;
            }

            float3 DensityAtPoint(float3 position)
            {
                float height = length(position) - _PlanetRadius;
                float height01 = height / (_AtmosphereRadius - _PlanetRadius);

                // Rayleigh and Mie density falloffs are both calculated with the same equation
                float rayleighDensity = exp(-height01 * _RayleighFalloff) * (1 - height01);
                float mieDensity = exp(-height01 * _MieFalloff) * (1 - height01);

                // Absorption density. This is for ozone, which scales together with the rayleigh.
                float denom = (_HeightAbsorbtion + height01);
                float ozoneDensity = (1.0 / (denom * denom + 1.0)) * rayleighDensity;

                return float3(rayleighDensity, mieDensity, ozoneDensity);
            }


            // While slightly more cumbersome, baking optical depth beforehand with a compute shader + render texture provides a significant performance boost
            // Take for example a non-baked fragment sample with 20 steps in the view direction and 10 steps in the sun direction -
            // that's 10*20: 200 marches per pixel, not good due to the frequent use of transcendent functions like exp() and sqrt()

            // When baked, this is reduced to only 1 call to OpticalDepthBaked, reducing the iterations to only 20 (very performant), while keeping near identical visual quality.

            float3 OpticalDepthBaked(float3 rayOrigin, float3 rayDir)
            {
                float rayLen = length(rayOrigin);
                float height = rayLen - _PlanetRadius;

                float height01 = saturate(height / (_AtmosphereRadius - _PlanetRadius));

                float3 normal = rayOrigin / rayLen;

                float uvX = 1 - (dot(normal, rayDir) * 0.5 + 0.5);

                return SAMPLE_TEXTURE2D(_BakedOpticalDepth, sampler_BakedOpticalDepth, float2(uvX, height01));
            }

            // Returns vector (dstToSphere, dstThroughSphere)
            // If ray origin is inside sphere, dstToSphere = 0
            // If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
            float2 RaySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
            {
                float3 offset = rayOrigin - sphereCentre;
                float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - sphereRadius * sphereRadius;
                float d = b * b - 4 * a * c; // Discriminant from quadratic formula
                float MAX_FLOAT = 3.402823466e+38;
                float2 intersection = float2(MAX_FLOAT, 0);

                // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
                if (d > 0)
                {
                    float s = sqrt(d);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar = (-b + s) / (2 * a);
                    // Ignore intersections that occur behind the ray
                    if (dstToSphereFar >= 0)
                    {
                        intersection = float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }


                return intersection;
            }

            float3 CalculateScattering(float3 start, float3 dir, float sceneDepth, float3 sceneColor)
            {
                // add an offset to the camera position, so that the atmosphere is in the correct position
                start -= _PlanetCenter;

                float2 oceanHit = RaySphere(0, _OceanRadius, start, dir);
                sceneDepth = min(sceneDepth, oceanHit.x);

                float2 rayLength = RaySphere(0, _AtmosphereRadius, start, dir);
                rayLength.y = min(rayLength.x + rayLength.y, sceneDepth);

                // Did the ray miss the atmosphere?   
                if (rayLength.x > rayLength.y)
                {
                    return sceneColor;
                }

                // Get camera-relative sun direction
                #if !defined(DIRECTIONAL_SUN)
                float3 sunPos = _SunParams.xyz - _PlanetCenter;
                float3 dirToSun = -normalize(start - sunPos.xyz);
                #else
        float3 dirToSun = _SunParams.xyz;
                #endif


                // Clamp maximum ray length to proper values
                rayLength.y = min(rayLength.y, sceneDepth);
                rayLength.x = max(rayLength.x, 0.0);

                // Frequently used values
                float mu = dot(dir, dirToSun);
                float mumu = mu * mu;
                float gg = _MieG * _MieG;

                // Magic number is (pi * 16)
                float phaseRay = 3.0 / (50.2654824574) * (1.0 + mumu);

                // Magic number is (pi * 8)
                float phaseMie = 3.0 / (25.1327412287) * ((1.0 - gg) * (mumu + 1.0)) / (pow(
                    1.0 + gg - 2.0 * mu * _MieG, 1.5) * (2.0 + gg));

                // Does object block mie glow?
                phaseMie = sceneDepth > rayLength.y ? phaseMie : 0.0;


                float inScatterStepSize = (rayLength.y - rayLength.x) / float(_NumInScatteringPoints);

                float3 inScatterPoint = start + dir * (rayLength.x + inScatterStepSize * 0.5);

                // Scattered light accumulators
                float3 totalRay = 0;
                float3 totalMie = 0;
                float3 opticalDepth = 0;

                // Primary in-scatter loop
                [unroll(MAX_LOOP_ITERATIONS)]
                for (int i = 0; i < _NumInScatteringPoints; i++)
                {
                    // Particle density at sample position
                    float3 density = DensityAtPoint(inScatterPoint) * inScatterStepSize;

                    // Accumulate optical depth
                    opticalDepth += density;

                    // Get sample point relative sun direction
                    #if !defined(DIRECTIONAL_SUN)
                    dirToSun = -normalize(inScatterPoint - sunPos.xyz);
                    #endif

                    // Light ray optical depth - Original optical depth function can be found in OutScattering.compute
                    //float3 lightOpticalDepth = OpticalDepth(inScatterPoint, dirToSun);
                    float3 lightOpticalDepth = OpticalDepthBaked(inScatterPoint, dirToSun);

                    // Attenuation calculation
                    float3 attenuation = exp(
                        -_RayleighScattering * (opticalDepth.x + lightOpticalDepth.x)
                        - _MieScattering * (opticalDepth.y + lightOpticalDepth.y)
                        - _AbsorbtionBeta * (opticalDepth.z + lightOpticalDepth.z)
                    );

                    // Accumulate scttered light
                    totalRay += density.x * attenuation;
                    totalMie += density.y * attenuation;

                    inScatterPoint += dir * inScatterStepSize;
                }


                // Calculate how much light can pass through the atmosphere
                float3 opacity = exp(
                    -(_MieScattering * opticalDepth.y
                        + _RayleighScattering * opticalDepth.x
                        + _AbsorbtionBeta * opticalDepth.z)
                );


                // Calculate final scattering factors
                float3 rayleigh = phaseRay * _RayleighScattering * totalRay;
                float3 mie = phaseMie * _MieScattering * totalMie;

                float3 ambient = opticalDepth.x * _AmbientBeta * 0.00001 /* Fudge factor */;

                // Apply final color
                return (rayleigh + mie + ambient) * _Intensity + sceneColor * opacity;
            }


            float4 AtmosphereFragment(v2f i) : SV_Target
            {
                float4 originalCol = SAMPLE_TEXTURE2D(_Source, sampler_Source, i.uv);

                float viewLength = length(i.viewVector);

                bool isEndOfDepth;
                float sceneDepth = CompositeDepthScaled(i.uv, viewLength, isEndOfDepth);

                float3 color = CalculateScattering(_WorldSpaceCameraPos.xyz, i.viewVector / viewLength, sceneDepth,
                                                   originalCol.xyz);

                return float4(color, originalCol.w);
            }

            #pragma once
            ENDHLSL
        }
    }
}

// ShaderToy by Dimas Leenman under the MIT license: https://www.shadertoy.com/view/wlBXWK
// Modified and ported to HLSL by Kai Angulo
// Adjusted to use baked optical depth based on examples from Sebastian Lague's atmosphere rendering series : https://www.youtube.com/watch?v=DxfEbulyFcY