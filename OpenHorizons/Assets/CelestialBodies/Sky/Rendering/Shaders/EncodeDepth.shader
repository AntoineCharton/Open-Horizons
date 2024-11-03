// Encodes relevant camera depth information into a four-channel texture.
// Channel R stores raw depth
// Channel G stores fragment camera view length for raymarched post-processing effects
// Channels B-W store Z-Buffer parameters needed to decode and linearize raw depth

Shader "Hidden/EncodeDepth"
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
float4 SampleCompositeDepth(float2 uv) {
	float rawDepth = SampleDepth(uv);
	float4 compositeDepth = 0;

	if (_RenderOverlay == 1 && rawDepth <= 0.0) {
		// If rendering an overlay and end of depth is reached:
		compositeDepth = SamplePrevDepth(uv);
	} else {
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

	if (_RenderOverlay == 1 && isEndOfDepth) {
		// If rendering an overlay and end of depth is reached:
		float4 encInfo = SamplePrevDepth(uv);

		isEndOfDepth = encInfo.x <= 0.0;

		return LinearEyeDepth(encInfo.x, encInfo) * encInfo.y;
	}

	return LinearEyeDepth(rawDepth, _ZBufferParams) * viewLength;
}

#endif

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


v2f EncodeDepthVertex(appdata v) 
{
	v2f output;
	output.pos = TransformObjectToHClip(v.vertex.xyz);
	output.uv = v.uv.xy;

	// Get view vector for fragment shader - do not normalize here as interpolation will mess it up, only normalize in fragment shader
	float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1)).xyz;
	output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;
	return output;
}


float4 EncodeDepthFragment(v2f i) : SV_Target 
{
    // x: raw depth
	// y: view length
	// z-w: z-buffer parameters
	float4 encodedInfo = (float4)0;

	encodedInfo.x = SampleDepth(i.uv);
	encodedInfo.y = length(i.viewVector);
	encodedInfo.z = _ZBufferParams.z;
	encodedInfo.w = _ZBufferParams.w;

    return encodedInfo;
}

ENDHLSL

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "EncodeDepth"

            HLSLPROGRAM
            #pragma vertex EncodeDepthVertex
            #pragma fragment EncodeDepthFragment

            ENDHLSL
        }
    }
}

//MIT License
//Copyright (c) 2023 Kai Angulo
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.