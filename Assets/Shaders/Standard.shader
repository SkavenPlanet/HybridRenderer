Shader "CustomStandard"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		_GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
		[Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

		_BumpScale("Scale", Float) = 1.0
		[Normal] _BumpMap("Normal Map", 2D) = "bump" {}

		_Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
		_ParallaxMap("Height Map", 2D) = "black" {}

		_OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
		_OcclusionMap("Occlusion", 2D) = "white" {}

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}

		_DetailMask("Detail Mask", 2D) = "white" {}

		_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
		_DetailNormalMapScale("Scale", Float) = 1.0
		[Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

		_Tess("Tessellation Amount", Range(0, 10)) = 0.1
		_Displacement("Tessellation Displacement", Range(0.0, 2.0)) = 1
		_DisplacementMap("Displacement Map", 2D) = "white" {}

		[Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0


			// Blending state
			[HideInInspector] _Mode("__mode", Float) = 0.0
			[HideInInspector] _SrcBlend("__src", Float) = 1.0
			[HideInInspector] _DstBlend("__dst", Float) = 0.0
			[HideInInspector] _ZWrite("__zw", Float) = 1.0
	}

		CGINCLUDE
		#include "Assets/Shaders/Lighting.cginc"

		#pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
		#pragma shader_feature_local _TRANSPARENCY
		#pragma shader_feature _EMISSION

		uniform float _Cutoff;

		Texture2D _MainTex, _BumpMap, _EmissionMap;
		SamplerState sampler_MainTex, sampler_BumpMap, sampler_EmissionMap;
		float3 _EmissionColor;

		uniform float3 _Color;
		uniform float _Glossiness, _Metallic;
		uniform float4 _MainTex_ST;

		#define colorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04)
		#define TRANSFORM_TEX(tex,name) (tex.xy * name##_ST.xy + name##_ST.zw)

		inline float OneMinusReflectivityFromMetallic(float metallic)
		{
				float oneMinusDielectricSpec = colorSpaceDielectricSpec.a;
				return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
		}

		inline float4 UnpackNormal(float4 packednormal, float scale)
		{
				packednormal.x *= packednormal.wy;

				float4 normal;
				normal.xy = packednormal.xy * 2 - 1;
				normal.xy *= scale;
				normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
				return normal;
		}

		float3x3 TangentToWorld(float3 vNormal, float4 vTangent, float3x3 objectToWorld) {
				float3 normal = normalize(mul(objectToWorld, vNormal));
				float3 tangent = normalize(mul(objectToWorld, vTangent.xyz));
				float tangentSign = vTangent.w;
				float3 bitangent = cross(normal, tangent) * tangentSign;
				return float3x3(tangent, bitangent, normal);
		}

		SurfaceData GetSurfaceData(float4 albedoMap, float4 normalMap, float4 emissionMap, float3x3 tangentToWorld) {
				normalMap = UnpackNormal(normalMap, 1.0);
				float3 albedo = _Color * albedoMap.rgb;
				float metallic = _Metallic;
				float oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
				float3 diffuseColor = albedo * oneMinusReflectivity;
				float3 specularColor = lerp(colorSpaceDielectricSpec.rgb, albedo, metallic);

				float3 tangent = tangentToWorld[0];
				float3 bitangent = tangentToWorld[1];
				float3 normal = tangentToWorld[2];

				normal = normalize(normal);
				//orthonormalize
				tangent = normalize(tangent - normal * dot(tangent, normal));
				bitangent = -cross(normal, tangent);

				SurfaceData s = (SurfaceData)0;
				s.diffuseColor = diffuseColor;
				s.normalWorld = normalize(tangent * normalMap.x + bitangent * normalMap.y + normal * normalMap.z);
				s.specularColor = specularColor;
				s.smoothness = _Glossiness;
				s.occlusion = 0;
				#ifdef _EMISSION
					s.emission = _EmissionColor * emissionMap.rgb;
				#endif

				return s;
		}

		ENDCG

		SubShader
		{
			Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
			LOD 300
			// ------------------------------------------------------------------
			//  Deferred pass
			Pass
			{
				//used later to indicate that geometry is present
				Stencil {
					Ref 1
					Comp Always
					Pass Replace
				}

				Name "DEFERRED"
				Tags { "LightMode" = "Deferred" }

				CGPROGRAM
				#pragma target 3.0
				#pragma exclude_renderers nomrt


			// -------------------------------------

			#pragma multi_compile_instancing
			// Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
			//#pragma multi_compile _ LOD_FADE_CROSSFADE

			#pragma vertex vert
			#pragma fragment frag

			struct a2v
			{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					float3 normal : NORMAL;
					float4 tangent : TANGENT;
			};

			struct v2f
			{
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					float3x3 tangentToWorld : TEXCOORD1;
					//float3 wpos : TEXCOORD2;
			};


			v2f vert(a2v v)
			{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					o.tangentToWorld = TangentToWorld(v.normal, v.tangent, (float3x3)unity_ObjectToWorld);
					//o.wpos = mul(unity_ObjectToWorld, v.vertex);

					return o;
			}


			void frag (
				v2f i,
				out float4 outGBuffer0 : SV_Target0,
				out float4 outGBuffer1 : SV_Target1,
				out float4 outGBuffer2 : SV_Target2,
				out float4 outGBuffer3 : SV_Target3
			)
			{
					float4 albedoMap = _MainTex.Sample(sampler_MainTex, i.uv);
					float4 normalMap = _BumpMap.Sample(sampler_BumpMap, i.uv);
					float4 emissionMap = _EmissionMap.Sample(sampler_EmissionMap, i.uv);
					SurfaceData data = GetSurfaceData(albedoMap, normalMap, emissionMap, i.tangentToWorld);
					SurfaceDataToGBuffer(data, outGBuffer0, outGBuffer1, outGBuffer2, outGBuffer3);
			}

			ENDCG
		}

		Pass
		{
			Name "RTShadowPass"

			CGPROGRAM

			#pragma raytracing MyHitShader
			#include "RaytraceHelper.cginc"
			#include "RTInclude.cginc"

#ifdef _ALPHATEST_ON
				#pragma raytracing AnyHitShader
				[shader("anyhit")]
				void AnyHitShader(inout ShadowPayload payload : SV_RayPayload,
					AttributeData attributeData : SV_IntersectionAttributes)
				{
					IVertex lerpV;
					GetIntersectionVertex(attributeData, lerpV);

					float2 uv = lerpV.texCoord0.xy;
					float alpha = _MainTex.SampleLevel(sampler_MainTex, uv, 1).a;

					if ((alpha - _Cutoff) < 0) {
						IgnoreHit();
					}
					else {
						AcceptHitAndEndSearch();
					}

				}
#endif

				[shader("closesthit")]
				void MyHitShader(inout ShadowPayload payload : SV_RayPayload,
					AttributeData attributeData : SV_IntersectionAttributes)
				{
					payload.atten = 0;
					payload.hitT = RayTCurrent();
				}

				ENDCG
			}

			Pass
			{
				Name "RTGBufferPass"

				CGPROGRAM

					#pragma raytracing MyHitShader
					#include "RaytraceHelper.cginc"
					#include "RTInclude.cginc"

					float4 Sample2DTrilinear(Texture2D t, SamplerState s, float2 uv, float lambda_t) {
							uint w, h;
							t.GetDimensions(w, h);
							float lod = 0.5 * log2(w * h) + lambda_t;
							return t.SampleLevel(s, uv, lod);
					}

					float4 Sample2DBilinear(Texture2D t, SamplerState s, float2 uv) {
							return t.SampleLevel(s, uv, 0);
					}

					//necessary for alpha cutout
#ifdef _ALPHATEST_ON
					#pragma raytracing AnyHitShader
					[shader("anyhit")]
					void AnyHitShader(inout GBufferPayload payload : SV_RayPayload,
						AttributeData attributeData : SV_IntersectionAttributes)
					{
						IVertex lerpV;
						GetIntersectionVertex(attributeData, lerpV);

						float2 uv = lerpV.texCoord0;
						float alpha = Sample2DBilinear(_MainTex, sampler_MainTex, uv).a;

						if ((alpha - _Cutoff) < 0) {
							IgnoreHit();
						}
					}
#endif

					[shader("closesthit")]
					void MyHitShader(inout GBufferPayload payload : SV_RayPayload,
						AttributeData attributeData : SV_IntersectionAttributes)
					{
						IVertex lerpV;
						GetIntersectionVertex2(attributeData, lerpV);

						float hitT = RayTCurrent();

						RayCone cone = propagate(payload.cone, 0, hitT);
						float triangleLod = 0.5 * log2(lerpV.texCoord0Area / lerpV.triangleArea);

						float3 rayOrigin = WorldRayOrigin();
						float3 rayDir = WorldRayDirection();

						float3x3 tangentToWorld = TangentToWorld(lerpV.normalOS.xyz, lerpV.tangentOS, (float3x3)ObjectToWorld3x4());
						float3 normal = tangentToWorld[2];

						float2 uv = TRANSFORM_TEX(lerpV.texCoord0, _MainTex);
						float lambda_t = triangleLod + log2(abs(cone.width / dot(rayDir, normal)));

						float4 albedoMap = Sample2DTrilinear(_MainTex, sampler_MainTex, uv, lambda_t);
						float4 normalMap = Sample2DTrilinear(_BumpMap, sampler_BumpMap, uv, lambda_t);
						float4 emissionMap = Sample2DBilinear(_EmissionMap, sampler_EmissionMap, uv);
						//srgb->linear conversion
						albedoMap.rgb = pow(albedoMap.rgb, 1.0 / 2.2);

						SurfaceData data = GetSurfaceData(albedoMap, normalMap, emissionMap, tangentToWorld);
						SurfaceDataToGBuffer(data, payload.gBuffer0, payload.gBuffer1, payload.gBuffer2, payload.gBuffer3);

						payload.hit = true;
						payload.hitKind = HitKind();
						payload.position = rayOrigin + RayTCurrent() * rayDir;
					}

					ENDCG
				}
		}

			FallBack "VertexLit"
			CustomEditor "StandardShaderGUI"
}