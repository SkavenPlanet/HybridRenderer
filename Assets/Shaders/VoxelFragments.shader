Shader "VoxelFragments"
{

		SubShader
		{
			//Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
			// ------------------------------------------------------------------
			//  Deferred pass
			Pass
			{
				Cull Off
				ZWrite Off
				//ColorMask Off

				Name "VOXELIZATION"

				CGPROGRAM
				#pragma target 5.0
				#pragma exclude_renderers nomrt


			// -------------------------------------

			// Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
			//#pragma multi_compile _ LOD_FADE_CROSSFADE

			#include "UnityCG.cginc"

			uniform RWTexture3D<float4> VoxelFragments : register(u1);
			uniform float3 _VolumeOffset;
			uniform float4x4 _VoxProjMat[3];

			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			struct a2v
			{
				float4 vertex : POSITION;
			};

			struct v2g {
				float4 vertex : SV_POSITION;
			};

			struct g2f {
				float4 pos : SV_POSITION;
				float3 wpos : TEXCOORD0;
			};

			v2g vert(a2v v) {
				v2g o;
				o.vertex = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}

			//https://www.gamedev.net/forums/topic/638661-voxelization-using-dominant-axis-voxel-cone-tracing/
			[maxvertexcount(3)]
			void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream) {

				float3 p0 = IN[0].vertex.xyz;
				float3 p1 = IN[1].vertex.xyz;
				float3 p2 = IN[2].vertex.xyz;

				float3 absN = abs(normalize(cross((p2 - p0), (p1 - p0))));
				float maxAxis = max(absN.x, max(absN.y, absN.z));

				float4x4 voxProjMat;
				for (int i = 0; i < 3; i++) {
					if (absN[i] == maxAxis) {
						voxProjMat = _VoxProjMat[i];
						break;
					}
				}

				g2f o;
				for (int i = 0; i < 3; i++) {
					o.pos = mul(voxProjMat, IN[i].vertex);
					o.wpos = IN[i].vertex.xyz;
					triStream.Append(o);
				}
				triStream.RestartStrip();
			}

			float4 frag(g2f i) : SV_Target
			{
				int3 position = (int3)floor(i.wpos.xyz + 256);
				int3 offset = position % 2;
				int octant = (offset.x + offset.y * 4 + offset.z * 2);
				int3 fragIdx = position/2;
				VoxelFragments[position] = 1;
				//debug output
				return float4((i.wpos.y+1)/8, 0, 0, 0);
			}

			ENDCG
			}
		}
}