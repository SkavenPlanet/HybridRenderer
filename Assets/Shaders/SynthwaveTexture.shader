Shader "Hidden/SynthwaveTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            bool inrange(float a, float b, float x) {
              return x > a && x < b;
            }

            //distortion from https://www.shadertoy.com/view/WljfRc

            float rand(float2 co)
            {
              return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            inline float mod(float x, float m) {
              return x % m;
            }

            inline float radians(float a) {
              return a * UNITY_PI / 180;
            }

            float2 distortion(float2 uv) {
              float time = _Time.y * 2;
              float t1 = rand(float2(floor(time), 2.));
              float t2 = rand(float2(floor(time), 4.));

              if (mod(time + t1, 3.) < .1 && uv.y < mod(time, 1));
                uv.x -= .01;
              if (mod(time + t2, 7.) < .06)
                uv.x += .07 * sin(20. * radians(360.) * uv.y + 3. * radians(360.) * time);

              return uv;
            }

            float3 sun(float2 uv) {
                float y = uv.y;
                uv.y /= 3;
                float time = (_Time.y / 5) % 3.14;
                float d = length(uv - float2(0.5, 0));
                float3 purple = float3(255, 29, 255) / 255.0;
                float3 blue = float3(110, 210, 255) / 255.0;
                float3 red = float3(242, 50, 12) / 255.0;
                float l = (0.3 - y) * 2;
                float3 col = lerp(lerp(purple, blue, y + sin(_Time.y)), red, y + sin(_Time.y/2));
                float lines = !(inrange(0.1, 0.2, y) || inrange(0.3, 0.35, y) || inrange(0.4, 0.42, y));
                float sunRadius = 0.29 + sin(_Time.y) * 0.1;
                return lines * col * smoothstep(sunRadius+0.002, sunRadius, d);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 col = sun(distortion(i.uv));
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
