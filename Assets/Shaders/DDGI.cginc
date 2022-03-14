#ifndef DDGI_INCLUDED
#define DDGI_INCLUDED

#include "Assets/Shaders/Lighting.cginc"

//including border pixels
uniform int _ProbeIrrTexSize, _ProbeVisTexSize;
uniform int _AtlasX, _AtlasY;

Texture2D _GIIrradiance, _GIVisibility;
SamplerState sampler_GIIrradiance, sampler_GIVisibility;
uniform float2 _GIIrradianceTexSize, _GIVisibilityTexSize;

//https://github.com/RomkoSI/G3D/blob/master/data-files/shader/g3dmath.glsl
float signNotZero(float x) {
	return x >= 0 ? 1.0 : -1.0;
}

float2 signNotZero(float2 v) {
	return float2(signNotZero(v.x), signNotZero(v.y));
}

float2 octCoord(int2 xy, int size) {
	return ((xy - 1) + 0.5) / (size - 2);
}

//https://github.com/RomkoSI/G3D/blob/master/data-files/shader/octahedral.glsl
//flip y and z
float3 octDecode(float2 o) {
	float3 v = float3(o.x, 1.0 - abs(o.x) - abs(o.y), o.y);
	if (v.y < 0.0) {
		v.xz = (1.0 - abs(v.zx)) * signNotZero(v.xz);
	}
	return normalize(v);
}

float2 octEncode(float3 v) {
	float l1norm = abs(v.x) + abs(v.y) + abs(v.z);
	float2 result = v.xz * (1.0 / l1norm);
	if (v.y < 0.0) {
		result = (1.0 - abs(result.yx)) * signNotZero(result.xy);
	}
	return result;
}

int2 GetProbeTexOffset(int i, int size) {
	int probeX = i % _AtlasX;
	int probeY = i / _AtlasX;
	return int2(probeX, probeY) * size;
}

float2 uvFromDirection(float3 dir, int probeIdx, int size, float2 texSize) {
	float2 xy = (octEncode(dir) * 0.5 + 0.5) * (size - 2);
	float2 xyOffset = GetProbeTexOffset(probeIdx, size);
	float2 uv = (xy + xyOffset + float2(1, 1)) / texSize;
	return uv;
}

//https://github.com/xuechao-chen/DDGI/blob/master/data-files/shaders/GIRenderer_ComputeIndirect.pix
float3 DiffuseGI(float3 wpos, float3 viewDir, float3 direction) {

	int idx;
	GINode node;
	if (!TraverseGISVO(wpos, node, idx)) return 0;

	float3 sumIrradiance = 0;
	float sumWeight = 0;
	const float normalBias = 0.02;
	const float viewBias = 0.1;
	float nodeSize = _GIVolumeSize / (1 << node.depth);
	float3 corner = node.center - nodeSize * 0.5;
	float3 alpha = clamp((wpos-corner)/nodeSize, 0, 1);

	for (int i = 0; i < 8; i++) 
	{
		float3 offset = float3(i % 2, i / 4, (i / 2) % 2);
		int probeIdx = node.indices[i];
		float3 probePos = GetProbePosition(probeIdx);

		float3 probeToPoint = wpos - probePos + (direction * normalBias) + (-viewDir * viewBias);
		float3 dir = normalize(-probeToPoint);

		float3 trilinear = max(0.0001, lerp(1.0 - alpha, alpha, offset));
		float trilinearWeight = (trilinear.x * trilinear.y * trilinear.z);

		float weight = 1;

		//backface test
		float3 directionToProbe = (probePos - wpos);
		float backface = (dot(directionToProbe, direction) + 1) * 0.5;
		weight *= Pow2(backface) + 0.2;

		//visibility test
		float2 visProbeUV = uvFromDirection(-dir, probeIdx, _ProbeVisTexSize, _GIVisibilityTexSize);
		float distToProbe = length(probeToPoint);

		float2 filteredDistance = 2 * _GIVisibility.SampleLevel(sampler_GIVisibility, visProbeUV, 0).xy;
		float mean = filteredDistance.x;
		float variance = abs(Pow2(filteredDistance.x) - filteredDistance.y);

		float chebyshevWeight = variance / (variance + Pow2(distToProbe - mean));
		chebyshevWeight = max(Pow3(chebyshevWeight), 0);

		weight *= (distToProbe <= mean) ? 1.0 : max(chebyshevWeight, 0.05);

		weight = max(0.000001, weight);

		const float crushThreshold = 0.2;
		if (weight < crushThreshold) {
			weight *= Pow2(weight) * (1.0 / Pow2(crushThreshold));
		}
		weight *= trilinearWeight;

		float2 probeUV = uvFromDirection(direction, probeIdx, _ProbeIrrTexSize, _GIIrradianceTexSize);
		float3 irradiance = _GIIrradiance.SampleLevel(sampler_GIIrradiance, probeUV, 0).rgb;
		sumIrradiance += irradiance * weight;
		sumWeight += weight;
	}

	return 2 * PI * sumIrradiance / sumWeight * 0.9;
}

#endif 