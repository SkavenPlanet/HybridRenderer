#ifndef RT_INCLUDED
#define RT_INCLUDED

uniform float _PixelSpreadAngle;
//https://media.contentapi.ea.com/content/dam/ea/seed/presentations/2019-ray-tracing-gems-chapter-20-akenine-moller-et-al.pdf
//https://github.com/NVIDIAGameWorks/Falcor/blob/5236495554f57a734cc815522d95ae9a7dfe458a/Source/Falcor/Experimental/Scene/Material/TexLODHelpers.slang
struct RayCone {
	float width;
	float spreadAngle;
};

RayCone propagate(RayCone cone, float surfaceSpreadAngle, float hitT) {
	RayCone newCone;
	newCone.width = cone.spreadAngle * hitT + cone.width;
	newCone.spreadAngle = cone.spreadAngle * surfaceSpreadAngle;
	return newCone;
}

float computeScreenSpaceSurfaceSpreadAngle()
{
	//float3 dNdx = ddx(normalW);
	//float3 dNdy = ddy(normalW);
	//float3 dPdx = ddx(positionW);
	//float3 dPdy = ddy(positionW);

	//float beta = sqrt(dot(dNdx, dNdx) + dot(dNdy, dNdy)) * sign(dot(dNdx, dPdx) + dot(dNdy, dPdy));
	return 0.1;
}

RayCone computeRayConeFromGBuffer(float distance)
{
	RayCone rc;
	rc.width = 0; // No width when ray cone starts
	rc.spreadAngle = _PixelSpreadAngle; // Eq. 30
// gbuffer . surfaceSpreadAngle holds a value generated by Eq. 32
	return propagate(rc, computeScreenSpaceSurfaceSpreadAngle(), distance);
}

float computeTextureLOD(float triangleLod, float3 direction, float3 normal, RayCone cone, float texWidth, float texHeight)
{
	float lambda = triangleLod;
	lambda += log2(abs(cone.width));
	lambda += 0.5 * log2(texWidth * texHeight);
	lambda -= log2(abs(dot(direction, normal)));
	return lambda;
}

struct ShadowPayload {
	float atten;
	float hitT;
	RayCone cone;
};

struct GBufferPayload {
	bool hit;
	uint hitKind;
	float4 gBuffer0;
	float4 gBuffer1;
	float4 gBuffer2;
	float4 gBuffer3;
	float3 position;
	RayCone cone;
};

//struct LightingPayload {
//	int bounceIndex;
//	float3 light;
//	float3 refracted;
//	float3 atten;
//	float dist;
//	float kr;
//};



#endif