#ifndef Sky_INCLUDED
#define Sky_INCLUDED

#include "Atmosphere.cginc"
#include "TriangleBlit.cginc"
#include "Lighting.cginc"
#include "PositionFromDepth.cginc"

//uniform float4 _PlanetPosition;
//uniform float4x4 _PlanetRot;
//uniform sampler2D _SpaceMap;

float3 RenderAtmosphere(Light light, float3 rayOrigin, float3 view, float shadowDistance, inout float3 attenuation, bool isSurface) {
		rayOrigin += float3(0, 6360000, 0); //- PlanetPosition;
		float viewLength = length(view);
		float3 viewDir = view / viewLength;

		float3 toLight = light.direction;
		float3 lightColor = light.color;

		if (isSurface) {
				return SkySurfaceRadiance(rayOrigin, toLight, viewDir, viewLength, shadowDistance, attenuation) * lightColor;
		}
		else {
				float lat = atan2(length(viewDir.xy), -viewDir.z) / PI;// *0.5 + 0.5;
				float lng = atan2(viewDir.y, viewDir.x) / PI * 0.5 + 0.5;
				//float3 space = tex2D(_SpaceMap, float2(lng, lat));
				float3 sunDisk = smoothstep(0, 1, (saturate(dot(toLight, viewDir) - 0.9998) / 0.00006));
				float3 spaceElements = (sunDisk * lightColor); // space * (1 - sunDisk) * 10

				float3 scattering = SkyRadiance(rayOrigin, toLight, viewDir, viewLength, shadowDistance, attenuation) * lightColor;
				return scattering + attenuation * spaceElements;
		}
}

#endif