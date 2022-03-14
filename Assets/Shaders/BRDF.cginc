#ifndef BRDF_INCLUDED
#define BRDF_INCLUDED

#include "Lighting.cginc"

#define UNITY_PI 3.1415926535
#define UNITY_INV_PI 1 / UNITY_PI

inline float GGXTerm(float NdotH, float roughness)
{
	float a2 = roughness * roughness;
	float d = (NdotH * a2 - NdotH) * NdotH + 1.0f; // 2 mad
	return UNITY_INV_PI * a2 / (d * d + 1e-7f); // This function is not intended to be running on Mobile,
											// therefore epsilon is smaller than what can be represented by half
}

inline half Pow5(half x)
{
	return x * x * x * x * x;
}

inline half2 Pow5(half2 x)
{
	return x * x * x * x * x;
}

inline half3 Pow5(half3 x)
{
	return x * x * x * x * x;
}

inline half4 Pow5(half4 x)
{
	return x * x * x * x * x;
}

inline half3 FresnelTerm(half3 F0, half cosA)
{
	half t = Pow5(1 - cosA);   // ala Schlick interpoliation
	return F0 + (1 - F0) * t;
}
inline half3 FresnelLerp(half3 F0, half3 F90, half cosA)
{
	half t = Pow5(1 - cosA);   // ala Schlick interpoliation
	return lerp(F0, F90, t);
}

// Ref: http://jcgt.org/published/0003/02/03/paper.pdf
inline float SmithJointGGXVisibilityTerm(float NdotL, float NdotV, float roughness)
{
#if 0
	// Original formulation:
	//  lambda_v    = (-1 + sqrt(a2 * (1 - NdotL2) / NdotL2 + 1)) * 0.5f;
	//  lambda_l    = (-1 + sqrt(a2 * (1 - NdotV2) / NdotV2 + 1)) * 0.5f;
	//  G           = 1 / (1 + lambda_v + lambda_l);

	// Reorder code to be more optimal
	half a = roughness;
	half a2 = a * a;

	half lambdaV = NdotL * sqrt((-NdotV * a2 + NdotV) * NdotV + a2);
	half lambdaL = NdotV * sqrt((-NdotL * a2 + NdotL) * NdotL + a2);

	// Simplify visibility term: (2.0f * NdotL * NdotV) /  ((4.0f * NdotL * NdotV) * (lambda_v + lambda_l + 1e-5f));
	return 0.5f / (lambdaV + lambdaL + 1e-5f);  // This function is not intended to be running on Mobile,
												// therefore epsilon is smaller than can be represented by half
#else
	// Approximation of the above formulation (simplify the sqrt, not mathematically correct but close enough)
	float a = roughness;
	float lambdaV = NdotL * (NdotV * (1 - a) + a);
	float lambdaL = NdotV * (NdotL * (1 - a) + a);

#if defined(SHADER_API_SWITCH)
	return 0.5f / (lambdaV + lambdaL + UNITY_HALF_MIN);
#else
	return 0.5f / (lambdaV + lambdaL + 1e-5f);
#endif

#endif
}

inline float3 safeNormalize(float3 inVec)
{
	float dp3 = max(0.001f, dot(inVec, inVec));
	return inVec * rsqrt(dp3);
}

float SmoothnessToPerceptualRoughness(float smoothness)
{
	return (1 - smoothness);
}

float PerceptualRoughnessToRoughness(float perceptualRoughness)
{
	return perceptualRoughness * perceptualRoughness;
}

// Note: Disney diffuse must be multiply by diffuseAlbedo / PI. This is done outside of this function.
half DisneyDiffuse(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
{
	half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
	// Two schlick fresnel term
	half lightScatter = (1 + (fd90 - 1) * Pow5(1 - NdotL));
	half viewScatter = (1 + (fd90 - 1) * Pow5(1 - NdotV));

	return lightScatter * viewScatter;
}

float3 BRDF(float3 diffColor, float3 specColor, float oneMinusReflectivity, float smoothness,
	float3 normal, float3 viewDir, Light light, GI gi) {

	float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
	float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
	float3 halfDir = safeNormalize(light.direction + viewDir);

	float nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact

	float nl = saturate(dot(normal, light.direction));
	float nh = saturate(dot(normal, halfDir));

	float lv = saturate(dot(light.direction, viewDir));
	float lh = saturate(dot(light.direction, halfDir));

	float diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

	roughness = max(roughness, 0.002);
	float V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
	float D = GGXTerm(nh, roughness);

	float specularTerm = V * D * UNITY_PI;
	specularTerm = max(0, specularTerm * nl);

	float surfaceReduction;
	surfaceReduction = 1.0 / (roughness * roughness + 1.0);

	//specularTerm *= any(specColor) ? 1.0 : 0.0;

	float grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
	float3 color = diffColor * (gi.diffuse + light.color * diffuseTerm)
		+ specularTerm * light.color * FresnelTerm(specColor, lh)
		+ surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, nv);

	return color;

}


#endif