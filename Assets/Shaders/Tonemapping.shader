Shader "Hidden/Tonemapping"
{
    SubShader
    {
        CGINCLUDE

		#include "TriangleBlit.cginc"

		float AvgLumToEV100(float avgLuminance) {
			const float K = 12.5; //K is a light meter calibration constant
			return log2(avgLuminance * 100 / K);
		}

		float EV100(float aperture, float shutterTime, float ISO) {
			return log2((aperture * aperture) / shutterTime * 100 / ISO);
		}

		float EV100ToExposure(float EV100) {
			float maxLuminance = 1.2 * exp2(EV100);
			return 1.0 / maxLuminance;
		}

		float3 ApplyTonemapping(float3 x)
		{
			float a = 2.51f;
			float b = 0.03f;
			float c = 2.43f;
			float d = 0.59f;
			float e = 0.14f;
			return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
		}

		static const float3x3 ACESInputMat =
		{
			{0.59719, 0.35458, 0.04823},
			{0.07600, 0.90834, 0.01566},
			{0.02840, 0.13383, 0.83777}
		};

		// ODT_SAT => XYZ => D60_2_D65 => sRGB
		static const float3x3 ACESOutputMat =
		{
			{ 1.60475, -0.53108, -0.07367},
			{-0.10208,  1.10813, -0.00605},
			{-0.00327, -0.07276,  1.07602}
		};

		float3 RRTAndODTFit(float3 v)
		{
			float3 a = v * (v + 0.0245786f) - 0.000090537f;
			float3 b = v * (0.983729f * v + 0.4329510f) + 0.238081f;
			return a / b;
		}

		float3 ACESFitted(float3 color)
		{
			color = mul(ACESInputMat, color);

			// Apply RRT and ODT
			color = RRTAndODTFit(color);

			color = mul(ACESOutputMat, color);

			// Clamp to [0, 1]
			color = saturate(color);

			return color;
		}
        ENDCG

		Pass
		{

			ZTest Always
			ZWrite Off
			Cull Off

			CGPROGRAM
			#pragma vertex vert_tri_blit
			#pragma fragment frag
			uniform sampler2D _Frame;

			float4 frag(v2f i) : SV_Target{
				float3 final = tex2D(_Frame, i.uv).rgb;
				float currentEV = EV100(16, 0.01, 100);
				float exposure = EV100ToExposure(currentEV);
				final *= exposure;
				//if(i.uv.x > 1.0/3 && i.uv.x < 2.0/3)
				//	final = ApplyTonemapping(final);
				//if (i.uv.x >= 2.0 / 3)
				final = ACESFitted(final);
				//if(uv.x < 0.5)
				return float4(final, 1.0);
			}
			ENDCG
		}
    }

}
