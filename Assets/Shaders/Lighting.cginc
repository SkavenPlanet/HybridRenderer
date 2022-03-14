#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Assets/Shaders/Atmosphere.cginc"

#ifdef RAYTRACE_SHADER
    #include "Assets/Shaders/RTInclude.cginc"
#endif

struct Light {
	float3 position;
	int lightType;
	float3 color;
	float range;
	float3 direction;
	float spotAngle;
};

struct GI {
	float3 diffuse;
	float3 specular;
};

uniform int _DirLightCount;
uniform int _LightCount;

#define MAX_LIGHTS_PER_NODE 1024

struct LightNode {
    float3 min;
    int numLights;
    int lightOffset;
    uint data;
    int indices[8];
};

#define NODE_IS_LEAF(node)              (node.data & 1)
#define NODE_OCTANT(node)               ((node.data & 0x0000000F)>>1)
#define NODE_DEPTH(node)                ((node.data & 0x000000FF)>>4)
#define NODE_PARENT(node)               (node.data >> 8)

#define NODE_SET_IS_LEAF(node, isLeaf)  (node.data |= isLeaf)
#define NODE_SET_OCTANT(node, octant)   (node.data |= (octant << 1))
#define NODE_SET_DEPTH(node, depth)     (node.data |= (depth << 4))
#define NODE_SET_PARENT(node, parent)   (node.data |= (parent << 8))

uniform StructuredBuffer<Light> _DirLightData;
uniform StructuredBuffer<Light> _LightData;
uniform StructuredBuffer<LightNode> Light_SVO;
uniform StructuredBuffer<uint> CULLED_LIGHTS;
uniform int MAX_LIGHT_SVO_DEPTH;
uniform float _VolumeSize;

struct GINode {
    float3 center;
    int isLeaf;
    int depth;
    int parent;
    int octant;
    int childIsProbe;
    int indices[8];
};

uniform StructuredBuffer<GINode> GI_SVO;
uniform StructuredBuffer<float3> ProbePositions;
uniform StructuredBuffer<float3> NewProbePositions;
uniform float _GIVolumeSize;
uniform int MAX_GI_SVO_DEPTH;

Texture2D Shadow;

float3 GetProbePosition(int i) {
    return ProbePositions[i];
}

#define SPOT 0
#define DIRECTIONAL 1
#define POINT 2
#define AREA 3
#define DISC 4

#define PI 3.1415926535
#define INV_PI (1 / PI)

struct SurfaceData {
    float3  diffuseColor;
    float   occlusion;
    float3  specularColor;
    float   smoothness;
    float3  normalWorld;
    float3  emission;
};

float3 accurateSRGBToLinear(float3 sRGBCol) {

	float3 linearRGBLo = sRGBCol / 12.92;
	float3 linearRGBHi = pow((sRGBCol + 0.055) / 1.055, 2.4);
	float3 linearRGB;
	linearRGB.r = (sRGBCol.r <= 0.04045) ? linearRGBLo.r : linearRGBHi.r;
	linearRGB.g = (sRGBCol.g <= 0.04045) ? linearRGBLo.g : linearRGBHi.g;
	linearRGB.b = (sRGBCol.b <= 0.04045) ? linearRGBLo.b : linearRGBHi.b;
	return linearRGB;

}

float3 accurateLinearToSRGB(float3 linearCol) {

	float3 sRGBLo = linearCol * 12.92;
	float3 sRGBHi = (pow(abs(linearCol), (1.0 / 2.4)) * 1.055) - 0.055;
	float3 sRGB;
	sRGB.r = (linearCol.r <= 0.0031308) ? sRGBLo.r : sRGBHi.r;
	sRGB.g = (linearCol.g <= 0.0031308) ? sRGBLo.g : sRGBHi.g;
	sRGB.b = (linearCol.b <= 0.0031308) ? sRGBLo.b : sRGBHi.b;
	return sRGB;

}

half SpecularStrength(half3 specular)
{
    return max(max(specular.r, specular.g), specular.b);
}

inline float Pow2(float x) {
  return x * x;
}

inline float Pow3(float x) {
  return x * x * x;
}

inline float4 Pow5(float4 x)
{
    return x * x * x * x * x;
}

//most of this is taken from unity builtin shaders
float PerceptualRoughnessToRoughness(float perceptualRoughness)
{
    return perceptualRoughness * perceptualRoughness;
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

inline float GGXTerm(float NdotH, float roughness)
{
    float a2 = roughness * roughness;
    float d = (NdotH * a2 - NdotH) * NdotH + 1.0f;
    return INV_PI * a2 / (d * d + 1e-7f);
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
    return 0.5f / (lambdaV + lambdaL + 1e-4f); // work-around against hlslcc rounding error
#else
    return 0.5f / (lambdaV + lambdaL + 1e-5f);
#endif

#endif
}

half DisneyDiffuse(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
{
    half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
    // Two schlick fresnel term
    half lightScatter = (1 + (fd90 - 1) * Pow5(1 - NdotL));
    half viewScatter = (1 + (fd90 - 1) * Pow5(1 - NdotV));

    return lightScatter * viewScatter;
}

float SmoothnessToPerceptualRoughness(float smoothness)
{
    return (1 - smoothness);
}

float3 BRDF_DIRECT(float3 diffColor, float3 specColor, float oneMinusReflectivity, float smoothness,
    float3 normal, float3 viewDir,
    float3 lightDir, float3 lightColor)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
    float3 halfDir = normalize(float3(lightDir) + viewDir);

    float nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact

    float nl = saturate(dot(normal, lightDir));
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(lightDir, viewDir));
    half lh = saturate(dot(lightDir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

    // Specular term
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    float V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
    float D = GGXTerm(nh, roughness);

    float specularTerm = V * D * PI; // Torrance-Sparrow model, Fresnel is applied later
    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;
//#   ifdef UNITY_COLORSPACE_GAMMA
//    surfaceReduction = 1.0 - 0.28 * roughness * perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
//#   else
    surfaceReduction = 1.0 / (roughness * roughness + 1.0);           // fade \in [0.5;1]
//#   endif

    // To provide true Lambert lighting, we need to be able to kill specular completely.
    specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
    float3 color = diffColor * (lightColor * diffuseTerm)
        + specularTerm * lightColor * FresnelTerm(specColor, lh);

    return color;
}

float3 BRDF_INDIRECT(float3 diffColor, float3 specColor, float oneMinusReflectivity, float smoothness,
    float3 normal, float3 viewDir,
    float3 diffuseGI, float3 specularGI)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);

    float nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;

    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    surfaceReduction = 1.0 / (roughness * roughness + 1.0);           // fade \in [0.5;1]
//#   endif

    half grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
    float3 color = diffColor * diffuseGI
        + surfaceReduction * specularGI * FresnelLerp(specColor, grazingTerm, nv);

    return color;
}

void SurfaceDataToGBuffer(SurfaceData data, 
  out float4 gBuffer0, out float4 gBuffer1, out float4 gBuffer2, out float4 gBuffer3) {
    gBuffer0 = float4(data.diffuseColor, data.occlusion);
    gBuffer1 = float4(data.specularColor, data.smoothness);
    gBuffer2 = float4(data.normalWorld * 0.5 + 0.5, 1);
    gBuffer3 = float4(data.emission, 0);
}

SurfaceData SurfaceDataFromGBuffer(float4 gBuffer0, float4 gBuffer1, float4 gBuffer2, float4 gBuffer3) {
    SurfaceData data;

    data.diffuseColor = gBuffer0.rgb;
    data.occlusion = gBuffer0.a;

    data.specularColor = gBuffer1.rgb;
    data.smoothness = gBuffer1.a;

    data.normalWorld = normalize(gBuffer2.rgb * 2 - 1);
    data.emission = gBuffer3.rgb;

    return data;
}


//https://github.com/RomkoSI/G3D/blob/master/data-files/shader/g3dmath.glsl
float3 sphericalFibonacci(float i, float n) {
  const float b = sqrt(5) * 0.5 + 0.5 - 1;
#   define madfrac(A, B) ((A)*(B)-floor((A)*(B)))
  float phi = 2.0 * PI * frac(i * b);
  float cosTheta = 1.0 - (2.0 * i + 1.0) * (1.0 / n);
  float sinTheta = sqrt(saturate(1.0 - cosTheta * cosTheta));

  return float3(
    cos(phi) * sinTheta,
    cosTheta,
    sin(phi) * sinTheta);

#   undef madfrac
}

//original code below
int GetOctantLight(float3 p, LightNode node)
{
    float nodeSize = _VolumeSize / (1 << NODE_DEPTH(node));
    float3 center = node.min + nodeSize * 0.5;
    int x = p.x > center.x;
    int y = p.y > center.y;
    int z = p.z > center.z;
    return x + y * 4 + z * 2;
}

int GetOctantGI(float3 p, GINode node)
{
    int x = p.x > node.center.x;
    int y = p.y > node.center.y;
    int z = p.z > node.center.z;
    return x + y * 4 + z * 2;
}

bool TraverseLightSVO(float3 p, inout LightNode outNode) {
    int id = 0;
    for (int i = 0; i < MAX_LIGHT_SVO_DEPTH; i++)
    {
        LightNode node = Light_SVO[id];
        if NODE_IS_LEAF(node) {
            outNode = node;
            return true;
        }
        int octant = GetOctantLight(p, node);
        id = node.indices[octant];
        if (id < 0) return false;
    }
    return false;
}

bool TraverseGISVO(float3 p, inout GINode outNode, out int idx) {
    int id = 0;
    for (int i = 0; i < (MAX_GI_SVO_DEPTH+1); i++)
    {
        GINode node = GI_SVO[id];
        if (node.isLeaf) {
            outNode = node;
            idx = id;
            return true;
        }
        int octant = GetOctantGI(p, node);
        id = node.indices[octant];
        if (id < 0) return false;
    }
    idx = 0;
    return false;
}

uint GetLightIdx(uint lightOffset, uint idx) {
    uint data = CULLED_LIGHTS[lightOffset + (idx >> 1)];
    return (idx & 1) ? data >> 16 : (data & 0x0000FFFF);
}

//vpos->view position (camera or ray origin)
float3 LightAccumulation (float3 wpos, float3 vpos, SurfaceData data, GI gi, int2 xy, bool useScreenShadow) {

    float3 eyeVec = -normalize(wpos - vpos);
    half oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);

    float3 irradiance = 0;

    float3 lightDir;
    float3 lightColor;

    //directional light loop
    for (int i = 0; i < _DirLightCount; i++) {
        Light light = _DirLightData[i];
        lightDir = light.direction;
        lightColor = light.color;

        const float r = 6360000;
        float mu = dot(float3(0, 1, 0), lightDir);
        float3 atmosAtten = SunRadiance(r, mu);
        lightColor *= atmosAtten;
        float atten = 1;

        if (i == 0 && useScreenShadow) {
          atten *= Shadow[xy].r;
        } else 
        {
#ifdef RAYTRACE_SHADER
          RayDesc ray;
          ray.Origin = wpos + data.normalWorld * 0.01;
          ray.Direction = lightDir;
          ray.TMin = 0;
          ray.TMax = 100000;

          ShadowPayload payload;
          payload.atten = 1;
          TraceRay(accelerationStructure, 0, 0xFFFFFFF, 0, 1, 0, ray, payload);

          atten *= payload.atten;
          if (atten == 0) continue;
#endif
        }
        lightColor *= atten;

        irradiance += BRDF_DIRECT(data.diffuseColor, data.specularColor, oneMinusReflectivity,
            data.smoothness, data.normalWorld, eyeVec, lightDir, lightColor);
    }

    //punctual light loop
    LightNode node;
    if (TraverseLightSVO(wpos, node)) 
    {
        //irradiance += node.numLights * 5000;
        //light loop
        for (int i = 0; i < node.numLights; i++)
        {
            Light light = _LightData[GetLightIdx(node.lightOffset, i)];
            //for (int i = 0; i < _LightCount; i++) {
            //    Light light = _LightData[i];

            float3 toLight = (light.position - wpos);
            float d = length(toLight);
            toLight = normalize(toLight);

            lightDir = toLight;
            lightColor = light.color;

            float distAtten = saturate((light.range - d) / light.range);
            distAtten *= distAtten;
            float atten = 1;
            switch (light.lightType) {
            case POINT:
                atten = distAtten;
                break;
            case SPOT:
                float angleCutoff = cos(light.spotAngle);
                float angleAtten = saturate((saturate(dot(toLight, light.direction)) - angleCutoff) / (1 - angleCutoff));
                angleAtten *= angleAtten;
                atten *= distAtten * angleAtten;
                break;
            }

            if (atten == 0) continue;

#ifdef RAYTRACE_SHADER
            RayDesc ray;
            ray.Origin = wpos + data.normalWorld * 0.01;
            ray.Direction = lightDir;
            ray.TMin = 0;
            ray.TMax = d;

            ShadowPayload payload;
            payload.atten = 1;
            TraceRay(accelerationStructure, 0, 0xFFFFFFF, 0, 1, 0, ray, payload);

            atten *= payload.atten;
            if (atten == 0) continue;
#endif
            lightColor *= atten;
            irradiance += BRDF_DIRECT(data.diffuseColor, data.specularColor, oneMinusReflectivity,
                data.smoothness, data.normalWorld, eyeVec, lightDir, lightColor);
        }
    }
    irradiance += BRDF_INDIRECT(data.diffuseColor, data.specularColor, oneMinusReflectivity,
        data.smoothness, data.normalWorld, eyeVec, gi.diffuse, gi.specular);

    irradiance += data.emission;
    return irradiance;
}


#endif