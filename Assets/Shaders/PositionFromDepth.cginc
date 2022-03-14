#ifndef PositionFromDepth_INCLUDED
#define PositionFromDepth_INCLUDED

uniform float4x4 UNITY_MATRIX_I_VP;
float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth, float4x4 invViewProjMatrix)
{
	float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
	float4 hpositionWS = mul(invViewProjMatrix, positionCS);
	return hpositionWS.xyz / hpositionWS.w;
}

// read depth and reconstruct world position
float3 PositionFromDepth(float2 uv, float depth) {
	return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP).xyz;
}

#endif