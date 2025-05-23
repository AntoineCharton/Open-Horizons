﻿
// @Cyanilux
Shader "Unlit/GeoGrass" {
	Properties {
		_Color ("Colour", Color) = (0.2,0.8,0.5,1)
		_Color2 ("Colour2", Color) = (0.5,0.9,0.6,1)
		_Width ("Width", Float) = 0.1
		_Height ("Height", Float) = 0.8
		_RandomWidth ("Random Width", Float) = 0.1
		_RandomHeight ("Random Height", Float) = 0.1
		_WindStrength("Wind Strength", Float) = 0.1
		_GrassSupression ("GrassSupression", Float) = 0.5
		_TessellationUniform("Tessellation Uniform", Range(1, 64)) = 1
		// Note, _TessellationUniform can go higher, but the material preview window causes it to be very laggy.
		// I'd probably just use a manually subdivided plane mesh if higher tessellations are needed.
		// Also, tessellation is uniform across entire plane. Might be good to look into tessellation based on camera distance.

		[Toggle(DISTANCE_DETAIL)] _DistanceDetail ("Toggle Blade Detail based on Camera Distance", Float) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
		Blend Off
		LOD 200

		Cull Off

		Pass {
			Name "ForwardLit"
			Tags {"LightMode" = "UniversalForward"}
			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library, (apparently)
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x gles
			#pragma target 4.5
			
			#pragma vertex vert
			#pragma fragment frag

			#pragma require geometry
			#pragma geometry geom

			#pragma require tessellation
			#pragma hull hull
			#pragma domain domain

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

			#pragma shader_feature_local _ DISTANCE_DETAIL

			// Defines

			#define SHADERPASS_FORWARD
			#define BLADE_SEGMENTS 2

			// Includes

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			#include "Grass.hlsl"
			#include "Tessellation.hlsl"
			
// Fragment
            float4 frag (GeometryOutput input, bool isFrontFace : SV_IsFrontFace) : SV_Target {
				input.normalWS = isFrontFace ? input.normalWS : input.normalWS;
				#if SHADOWS_SCREEN
					float4 clipPos = TransformWorldToHClip(input.positionWS);
					float4 shadowCoord = ComputeScreenPos(clipPos);
				#else
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#endif

				float3 ambient = SampleSH(input.normalWS);
				
				Light mainLight = GetMainLight(shadowCoord);
				float NdotL = saturate(saturate(dot(input.normalWS, mainLight.direction)) + 0.8);
				float up = saturate(dot(float3(0,1,0), mainLight.direction) + 0.5);

				float3 shading = NdotL * up * mainLight.shadowAttenuation * mainLight.color + ambient;

                // Additional Lights 
                int additionalLightsCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalLightsCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    float3 lightVector = input.positionWS - light.direction; // Vector from light to surface
                    float distanceSqr = max(dot(lightVector, lightVector), 0.00001);
                    float3 lightDir = normalize(-lightVector); // Direction from light to surface
                    float NdotL = saturate(dot(input.normalWS, lightDir)) * 0.05;
			
                    // Correct attenuation calculation
                    float attenuation = 1.0 / (1.0 + light.distanceAttenuation * distanceSqr);
					attenuation = 1 - attenuation;
                    // Ensure the light contribution is added correctly
                    shading += NdotL * light.color * attenuation * light.shadowAttenuation;
                }

                return lerp(_Color, _Color2, input.uv.y) * float4(shading, 1);
            }
            ENDHLSL
        }
		
		// Used for rendering shadowmaps
		//UsePass "Universal Render Pipeline/Lit/ShadowCaster"

		Pass {
			Name "ShadowCaster"
			Tags {"LightMode" = "ShadowCaster"}

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library, (apparently)
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x gles
			#pragma target 4.5

			#pragma vertex vert
			#pragma fragment emptyFrag

			#pragma require geometry
			#pragma geometry geom

			#pragma require tessellation
			#pragma hull hull
			#pragma domain domain

			#define BLADE_SEGMENTS 2
			#define SHADERPASS_SHADOWCASTER

			#pragma shader_feature_local _ DISTANCE_DETAIL

			//#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			
			#include "Grass.hlsl"
			#include "Tessellation.hlsl"

			half4 emptyFrag(GeometryOutput input) : SV_TARGET{
				return 0;
			}

			ENDHLSL
		}

		// Used for depth prepass
		// If shadows cascade are enabled we need to perform a depth prepass. 
		// We also need to use a depth prepass in some cases camera require depth texture
		// e.g, MSAA is enabled and we can't resolve with Texture2DMS
		//UsePass "Universal Render Pipeline/Lit/DepthOnly"

		// Note, can't UsePass + SRP Batcher due to UnityPerMaterial CBUFFER having incosistent size between subshaders..
		// Had to comment this out for now so it doesn't break SRP Batcher.
		
		// Instead will do this :
		Pass {
			Name "DepthOnly"
			Tags {"LightMode" = "DepthOnly"}

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library, (apparently)
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x gles
			#pragma target 4.5

			#pragma vertex vert
			#pragma fragment emptyFrag

			#pragma require geometry
			#pragma geometry geom

			#pragma require tessellation
			#pragma hull hull
			#pragma domain domain

			#define BLADE_SEGMENTS 2
			#define SHADERPASS_DEPTHONLY

			#pragma shader_feature_local _ DISTANCE_DETAIL

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Grass.hlsl"
			#include "Tessellation.hlsl"

			half4 emptyFrag(GeometryOutput input) : SV_TARGET{
				return 0;
			}

			ENDHLSL
		}
	}
}

/*MIT License

Copyright (c) 2020 Cyanilux

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/