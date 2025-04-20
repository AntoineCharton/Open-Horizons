
// @Cyanilux
// Based on https://roystan.net/articles/grass-shader.html

// Structs
struct Attributes {
	float4 positionOS   : POSITION;
	float3 normal		: NORMAL;
	float4 tangent		: TANGENT;
	float4 vertexColor  : COLOR0;
	float2 texcoord     : TEXCOORD0;
};

struct Varyings {
	//float4 positionOS   : SV_POSITION;
	float3 positionWS	: TEXCOORD1;
	float4 vertexColor  : COLOR0;
	float3 positionVS	: TEXCOORD2;
	float3 normal		: TEXCOORD3;
	float4 tangent		: TEXCOORD4;
	float2 texcoord		: TEXCOORD0;
};

struct GeometryOutput {
	float4 positionCS	: SV_POSITION;
	float4 vertexColor  : COLOR0;
	float3 positionWS	: TEXCOORD1;
	float3 normalWS		: TEXCOORD3;
	float2 uv			: TEXCOORD0;
	float bladeSegments : TEXCOORD2;
};

// Methods

float rand(float3 seed) {
	return frac(sin(dot(seed.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
}

float4 permute(float4 x) {
	return fmod(
		x*x*34.0 + x,
		289.0
	);
}

float4 taylorInvSqrt(float4 r) {
	return 1.79284291400159 - 0.85373472095314 * r;
}

float snoise(float3 v)
{
    const float2 C = float2(
        0.166666666666666667, // 1/6
        0.333333333333333333  // 1/3
    );
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);
 
// First corner
    float3 i = floor( v + dot(v, C.yyy) );
    float3 x0 = v - i + dot(i, C.xxx);
 
// Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);
 
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
    float3 x3 = x0 - D.yyy;      // -1.0+3.0*C.x = -0.5 = -D.y
 
// Permutations
    i = fmod(i,289.0);
    float4 p = permute(
        permute(
            permute(
                    i.z + float4(0.0, i1.z, i2.z, 1.0 )
            ) + i.y + float4(0.0, i1.y, i2.y, 1.0 )
        )     + i.x + float4(0.0, i1.x, i2.x, 1.0 )
    );
 
// Gradients: 7x7 points over a square, mapped onto an octahedron.
// The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float n_ = 0.142857142857; // 1/7
    float3 ns = n_ * D.wyz - D.xzx;
 
    float4 j = p - 49.0 * floor(p * ns.z * ns.z); // mod(p,7*7)
 
    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_ ); // mod(j,N)
 
    float4 x = x_ *ns.x + ns.yyyy;
    float4 y = y_ *ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);
 
    float4 b0 = float4( x.xy, y.xy );
    float4 b1 = float4( x.zw, y.zw );
 
    //float4 s0 = float4(lessThan(b0,0.0))*2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1,0.0))*2.0 - 1.0;
    float4 s0 = floor(b0)*2.0 + 1.0;
    float4 s1 = floor(b1)*2.0 + 1.0;
    float4 sh = -step(h, 0.0);
 
    float4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
    float4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;
 
    float3 p0 = float3(a0.xy,h.x);
    float3 p1 = float3(a0.zw,h.y);
    float3 p2 = float3(a1.xy,h.z);
    float3 p3 = float3(a1.zw,h.w);
 
//Normalise gradients
    float4 norm = taylorInvSqrt(float4(
        dot(p0, p0),
        dot(p1, p1),
        dot(p2, p2),
        dot(p3, p3)
    ));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;
 
// Mix final noise value
    float4 m = max(
        0.6 - float4(
            dot(x0, x0),
            dot(x1, x1),
            dot(x2, x2),
            dot(x3, x3)
        ),
        0.0
    );
    m = m * m;
    return 42.0 * dot(
        m*m,
        float4(
            dot(p0, x0),
            dot(p1, x1),
            dot(p2, x2),
            dot(p3, x3)
        )
    );
}

// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
float3x3 AngleAxis3x3(float angle, float3 axis) {
	float c, s;
	sincos(angle, s, c);

	float t = 1 - c;
	float x = axis.x;
	float y = axis.y;
	float z = axis.z;

	return float3x3(
		t * x * x + c, t * x * y - s * z, t * x * z + s * y,
		t * x * y + s * z, t * y * y + c, t * y * z - s * x,
		t * x * z - s * y, t * y * z + s * x, t * z * z + c
	);
}

#ifdef SHADERPASS_SHADOWCASTER
	float3 _LightDirection;

	float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS) {
		float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

	#if UNITY_REVERSED_Z
		positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
	#else
		positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
	#endif

		return positionCS;
	}
#endif

float4 WorldToHClip(float3 positionWS, float3 normalWS){
	#ifdef SHADERPASS_SHADOWCASTER
		return GetShadowPositionHClip(positionWS, normalWS);
	#else
		return TransformWorldToHClip(positionWS);
	#endif
}

// Variables
CBUFFER_START(UnityPerMaterial) // Required to be compatible with SRP Batcher
float4 _Color;
float4 _Color2;
float _Width;
float _RandomWidth;
float _Height;
float _RandomHeight;
float _WindStrength;
float _GrassSupression;
float _TessellationUniform; // Used in CustomTesellation.hlsl
CBUFFER_END

// Vertex, Geometry & Fragment Shaders

Varyings vert (Attributes input) {
	Varyings output = (Varyings)0;

	VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
	// Seems like GetVertexPositionInputs doesn't work with SRP Batcher inside geom function?
	// Had to move it here, in order to obtain positionWS and pass it through the Varyings output.

	// output.positionOS = input.positionOS;
	// object space / model matrix doesn't seem to work in geom shader? Using world instead.
	output.positionWS = vertexInput.positionWS;
	output.positionVS = vertexInput.positionVS;
	output.vertexColor = input.vertexColor;
	output.normal = TransformObjectToWorldNormal(input.normal);
	output.tangent = input.tangent;
	// or maybe
	// output.tangent = float4(TransformObjectToWorldNormal(input.tangent.xyz), input.tangent.w);
	// doesn't seem to make much of a difference though

	output.texcoord = input.texcoord;
	return output;
}

[maxvertexcount(BLADE_SEGMENTS * 2 + 1 + 3)]
void geom(uint primitiveID : SV_PrimitiveID, triangle Varyings input[3], inout TriangleStream<GeometryOutput> triStream) {
	GeometryOutput output = (GeometryOutput) 0;

	//VertexPositionInputs vertexInput = GetVertexPositionInputs(input[0].positionOS.xyz);
	// Note, this works fine without SRP Batcher but seems to break when using it. See vert function above.

	// -----------------------
	// Blade Segment Detail
	// -----------------------
	// (blades closer to camera have more detail, should only really be used for first person camera)
	output.vertexColor = input[0].vertexColor;
	float3 cameraPos = _WorldSpaceCameraPos;
	float3 positionWS = input[1].positionWS;
	float noiseValue = snoise(positionWS);
	positionWS = float3(positionWS.x + (noiseValue * 0.5), positionWS.y + (noiseValue * 0.5), positionWS.z + (noiseValue * 0.5));
	
	#ifdef DISTANCE_DETAIL
		float3 vtcam = cameraPos - positionWS;
		float distSqr = dot(vtcam, vtcam);
		float bladeSegments = lerp(BLADE_SEGMENTS, 0, saturate(distSqr * 0.0005 - 0.1));
		float bladeSegmentsFadeEnd = lerp(BLADE_SEGMENTS, 0, saturate(distSqr * 0.00001));
	#else
		int bladeSegments = BLADE_SEGMENTS;
		int bladeSegmentsFadeEnd =  BLADE_SEGMENTS;
	#endif

	output.bladeSegments = bladeSegments;
	
	
	float fadeValue = saturate(lerp(bladeSegments, bladeSegmentsFadeEnd, noiseValue));
	if (fadeValue <= 0.3){
		// Too far away, don't render grass blades (should only really be used for first person camera)
		return;
	}

	// Only render grass blades infront of camera (nothing behind)
	if (input[0].positionVS.z > 0){
		return;
	}

	// Only render on flat surfaces
	if(input[0].vertexColor[0] > _GrassSupression + 0.05)
		return;
	// -----------------------
	// Construct World -> Tangent Matrix (for aligning grass with mesh normals)
	// -----------------------

	float3 normal = input[0].normal;
	float4 tangent = input[0].tangent;
	float3 binormal = cross(normal, tangent.xyz) * tangent.w;

	float3x3 tangentToLocal = float3x3(
		tangent.x, binormal.x, normal.x,
		tangent.y, binormal.y, normal.y,
		tangent.z, binormal.z, normal.z
	);
	
	// -----------------------
	// Wind
	// -----------------------

	float r = rand(positionWS.xyz);
	float3x3 randRotation = AngleAxis3x3(r * TWO_PI, float3(0,0,1));

	float3x3 windMatrix;
	if (_WindStrength != 0){
		// Wind (based on sin / cos, aka a circular motion, but strength of 0.1 * sine)
		// Could likely be simplified - this was mainly just trial and error to get something that looked nice.
		float2 wind = float2(sin(_Time.y + positionWS.x * 0.5), cos(_Time.y + positionWS.z * 0.5)) * _WindStrength * sin(_Time.y + r) * float2(0.5, 1);
		windMatrix = AngleAxis3x3((wind * PI).y, normalize(float3(wind.x, wind.x, wind.y)));
	} else {
		windMatrix = float3x3(1,0,0,0,1,0,0,0,1);
	}

	// -----------------------
	// Bending, Width & Height
	// -----------------------
	
	//tangentToLocal = float3x3(1,0,0,0,1,0,0,0,1);

	float3x3 transformMatrix = mul(tangentToLocal, randRotation);
	float3x3 transformMatrixWithWind = mul(mul(tangentToLocal, windMatrix), randRotation);
	float bend = rand(positionWS.xyz) - 0.5;
	float width = _Width + _RandomWidth * (rand(positionWS.zyx) - 0.5);
	float height = _Height + _RandomHeight * (rand(positionWS.yxz) - 0.5);
	height = lerp(0 , height, fadeValue);
	width = lerp(_Width * 6, _Width, fadeValue);
	
	// -----------------------
	// Handle Geometry
	// -----------------------

	// Normals for all grass blade vertices is the same
	float3 normalWS = mul(transformMatrix, float3(0, -1, 0));
	output.normalWS = normalWS;

	// Base 2 vertices
	output.positionWS = positionWS + mul(transformMatrix, float3(width, 0, 0));
	output.positionCS = WorldToHClip(output.positionWS, normalWS);
	output.uv = float2(0, 0);
	triStream.Append(output);

	output.positionWS = positionWS + mul(transformMatrix, float3(-width, 0, 0));
	output.positionCS = WorldToHClip(output.positionWS, normalWS);
	output.uv = float2(0, 0);
	triStream.Append(output);

	// Center (2 vertices per BLADE_SEGMENTS)
	for (int i = 1; i < bladeSegments; i++) {
		float t = i / (float)bladeSegments;

		float h = height * t;
		float w = width * (1-t);
		float b = bend * pow(t, 2);

		output.positionWS = positionWS + mul(transformMatrixWithWind, float3(w, b, h));
		output.positionCS = WorldToHClip(output.positionWS, normalWS);
		output.uv = float2(0, t);
		triStream.Append(output);

		output.positionWS = positionWS + mul(transformMatrixWithWind, float3(-w, b, h));
		output.positionCS = WorldToHClip(output.positionWS, normalWS);
		output.uv = float2(0, t);
		triStream.Append(output);
	}

	// Final vertex at top of blade
	output.positionWS = positionWS + mul(transformMatrixWithWind, float3(0, bend, height));
	output.positionCS = WorldToHClip(output.positionWS, normalWS);

	output.uv = float2(0, 1);
	triStream.Append(output);

	triStream.RestartStrip();
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