// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

Shader "Hidden/VisGIProbes"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite On ZTest LEqual

        Pass
        {
            Stencil {
                    Ref 1
                    Comp Always
                    Pass Replace
                }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "DDGI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                int probeIdx : TEXCOORD1;
            };

            void setup() {}

            uniform float _VisSphereScale;

            //assumes visualization sphere vertices are centered about origin in obj space
            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);

                int instanceId = 0;
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED 
                  instanceId = unity_InstanceID;
                #endif
                
                o.probeIdx = instanceId;
                float3 position = GetProbePosition(o.probeIdx);
                o.vertex = mul(UNITY_MATRIX_VP, float4(v.vertex.xyz * 0.2 + position, 1.0));
                o.normal = v.vertex.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float2 probeUV = uvFromDirection(normal, i.probeIdx, _ProbeIrrTexSize, _GIIrradianceTexSize);
                float3 irradiance = _GIIrradiance.SampleLevel(sampler_GIIrradiance, probeUV, 0).rgb;
                return float4(irradiance, 1);
            }
            ENDCG
        }
    }
}
