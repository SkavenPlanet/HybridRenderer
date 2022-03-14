#if !defined(TESSELLATION_INCLUDED)
#define TESSELLATION_INCLUDED

#include "Tessellation.cginc"
//uniform sampler2D _ParallaxMap;
//uniform float _Parallax;
uniform float _Tess;

struct a2v
{
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD0;
};

//struct v2f
//{
//	float4 pos : POSITION;
//	float4 normal : NORMAL;
//	float2 uv : TEXCOORD0;
//};

struct HS_CONSTANT_OUTPUT
{
	float edge[3]  : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

struct InternalTessInterp_appdata {
	float4 vertex : INTERNALTESSPOS;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD0;
};

InternalTessInterp_appdata tessvert(a2v v) {
	InternalTessInterp_appdata o;
	o.vertex = v.vertex;
	o.tangent = v.tangent;
	o.normal = v.normal;
	o.texcoord = v.texcoord;
	return o;
}

VertexOutputDeferred vert(a2v v) {

	VertexOutputDeferred o;
	UNITY_INITIALIZE_OUTPUT(VertexOutputDeferred, o);
	o.tex = TRANSFORM_TEX(v.texcoord.xy, _MainTex).xyxy;
	float d = tex2Dlod(_ParallaxMap, float4(o.tex.xy, 0, 0)).r * _Parallax;
	v.vertex.xyz += float3(0, d, 0);
	float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.eyeVec = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);
	float3 normalWorld = UnityObjectToWorldNormal(v.normal);
	float4 tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
	float3x3 tangentToWorld = CreateTangentToWorldPerVertex(normalWorld, tangentWorld.xyz, tangentWorld.w);
	o.tangentToWorldAndPackedData[0].xyz = tangentToWorld[0];
	o.tangentToWorldAndPackedData[1].xyz = tangentToWorld[1];
	o.tangentToWorldAndPackedData[2].xyz = tangentToWorld[2];
	return o;

}

float CalcDistanceTessFactor(float4 vertex, float minDist, float maxDist, float tess)
{
	return tess;
}

float4 DistanceBasedTess(float4 v0, float4 v1, float4 v2, float minDist, float maxDist, float tess)
{
	float3 f;
	f.x = CalcDistanceTessFactor(v0, minDist, maxDist, tess);
	f.y = CalcDistanceTessFactor(v1, minDist, maxDist, tess);
	f.z = CalcDistanceTessFactor(v2, minDist, maxDist, tess);

	return UnityCalcTriEdgeTessFactors(f);
}

float4 tessDistance(a2v v0, a2v v1, a2v v2) {
	float minDist = 1;
	float maxDist = 10;
	return DistanceBasedTess(v0.vertex, v1.vertex, v2.vertex, minDist, maxDist, _Tess);
}

// tessellation hull constant shader
HS_CONSTANT_OUTPUT HSConstant(InputPatch<InternalTessInterp_appdata, 3> v) {
	HS_CONSTANT_OUTPUT o;
	float4 tf;
	a2v vi[3];
	vi[0].vertex = v[0].vertex;
	vi[0].tangent = v[0].tangent;
	vi[0].normal = v[0].normal;
	vi[0].texcoord = v[0].texcoord;
	vi[1].vertex = v[1].vertex;
	vi[1].tangent = v[1].tangent;
	vi[1].normal = v[1].normal;
	vi[1].texcoord = v[1].texcoord;
	vi[2].vertex = v[2].vertex;
	vi[2].tangent = v[2].tangent;
	vi[2].normal = v[2].normal;
	vi[2].texcoord = v[2].texcoord;
	tf = tessDistance(vi[0], vi[1], vi[2]);
	o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
	return o;
}

// tessellation hull shader
[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[patchconstantfunc("HSConstant")]
[outputcontrolpoints(3)]
InternalTessInterp_appdata hull(InputPatch<InternalTessInterp_appdata, 3> v, uint id : SV_OutputControlPointID) {
	return v[id];
}

[UNITY_domain("tri")]
VertexOutputDeferred domain(HS_CONSTANT_OUTPUT tessFactors, const OutputPatch<InternalTessInterp_appdata, 3> vi, float3 bary : SV_DomainLocation) {
	a2v v;
	VertexOutputDeferred o;
	v.vertex = vi[0].vertex * bary.x + vi[1].vertex * bary.y + vi[2].vertex * bary.z;
	v.tangent = vi[0].tangent * bary.x + vi[1].tangent * bary.y + vi[2].tangent * bary.z;
	v.normal = vi[0].normal * bary.x + vi[1].normal * bary.y + vi[2].normal * bary.z;
	v.texcoord = vi[0].texcoord * bary.x + vi[1].texcoord * bary.y + vi[2].texcoord * bary.z;
	o = vert(v);

	return o;
}

#endif