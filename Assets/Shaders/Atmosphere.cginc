/*
 * Proland: a procedural landscape rendering library.
 * Copyright (c) 2008-2011 INRIA
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

/*
 * Proland is distributed under a dual-license scheme.
 * You can obtain a specific license from Inria: proland-licensing@inria.fr.
 */

/*
 * Authors: Eric Bruneton, Antoine Begault, Guillaume Piolat.
 */

/**
 * Precomputed Atmospheric Scattering
 * Copyright (c) 2008 INRIA
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. Neither the name of the copyright holders nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
 * THE POSSIBILITY OF SUCH DAMAGE.
 */

/**
 * Author: Eric Bruneton
 * Modified and ported to Unity by Justin Hawkins 2014
 * Further modified by Jordan Walker 2019
 */


#ifndef Atmosphere_INCLUDED
#define Atmosphere_INCLUDED

//#include "Assets/Shaders/Lighting/LightingCommon.cginc"

// ----------------------------------------------------------------------------
// PHYSICAL MODEL PARAMETERS
// ----------------------------------------------------------------------------

uniform float Rg;
uniform float Rt;
uniform float RL;
uniform float Rp;
uniform float Amplitude;

uniform float AVERAGE_GROUND_REFLECTANCE;

// Rayleigh
uniform float HR;
uniform float3 betaR;

// Mie
uniform float HM;
uniform float3 betaMSca;
uniform float3 betaMEx;
uniform float mieG;

// ----------------------------------------------------------------------------
// PARAMETERIZATION OPTIONS
// ----------------------------------------------------------------------------

uniform float TRANSMITTANCE_W;
uniform float TRANSMITTANCE_H;

uniform float SKY_W;
uniform float SKY_H;

uniform float RES_R;
uniform float RES_MU;
uniform float RES_MU_S;
uniform float RES_NU;

#define TRANSMITTANCE_NON_LINEAR
#define INSCATTER_NON_LINEAR

#define PI 3.1415926535

// ----------------------------------------------------------------------------
// PARAMETERIZATION FUNCTIONS
// ----------------------------------------------------------------------------

Texture2D _Transmittance;
Texture2D _Irradiance;
Texture3D _Inscatter;

SamplerState sampler_Transmittance;
SamplerState sampler_Irradiance;
SamplerState sampler_Inscatter;

static const float EPSILON_ATMOSPHERE = 0.002f;
static const float EPSILON_INSCATTER = 0.004f;

float2 GetTransmittanceUV(float r, float mu) {
    float uR, uMu;
#ifdef TRANSMITTANCE_NON_LINEAR
    uR = sqrt((r - Rg) / (Rt - Rg));
    uMu = atan((mu + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
#else
    uR = (r - Rg) / (Rt - Rg);
    uMu = (mu + 0.15) / (1.0 + 0.15);
#endif
    return float2(uMu, uR);
}

void GetTransmittanceRMu(float2 coord, out float r, out float muS) {
    r = coord.y / TRANSMITTANCE_H;
    muS = coord.x / TRANSMITTANCE_W;
#ifdef TRANSMITTANCE_NON_LINEAR
    r = Rg + (r * r) * (Rt - Rg);
    muS = -0.15 + tan(1.5 * muS) / tan(1.5) * (1.0 + 0.15);
#else
    r = Rg + r * (Rt - Rg);
    muS = -0.15 + muS * (1.0 + 0.15);
#endif
}

float2 GetIrradianceUV(float r, float muS) 
{
    float uR = (r - Rg) / (Rt - Rg);
    float uMuS = (muS + 0.2) / (1.0 + 0.2);
    return float2(uMuS, uR);
}

void GetIrradianceRMuS(float2 coord, out float r, out float muS) 
{
    r = Rg + (coord.y - 0.5) / (SKY_H - 1.0) * (Rt - Rg);
    muS = -0.2 + (coord.x - 0.5) / (SKY_W - 1.0) * (1.0 + 0.2);
}

float4 Texture4D(Texture3D table, SamplerState s, float r, float mu, float muS, float nu)
{
   	float H = sqrt(Rt * Rt - Rg * Rg);
   	float rho = sqrt(r * r - Rg * Rg);

    float rmu = r * mu;
    float delta = rmu * rmu - r * r + Rg * Rg;
    float4 cst = rmu < 0.0 && delta > 0.0 ? float4(1.0, 0.0, 0.0, 0.5 - 0.5 / RES_MU) : float4(-1.0, H * H, H, 0.5 + 0.5 / RES_MU);
    float uR = 0.5 / RES_R + rho / H * (1.0 - 1.0 / RES_R);
    float uMu = cst.w + (rmu * cst.x + sqrt(delta + cst.y)) / (rho + cst.z) * (0.5 - 1.0 / float(RES_MU));
    // paper formula
    //float uMuS = 0.5 / RES_MU_S + max((1.0 - exp(-3.0 * muS - 0.6)) / (1.0 - exp(-3.6)), 0.0) * (1.0 - 1.0 / RES_MU_S);
    // better formula
    float uMuS = 0.5 / RES_MU_S + (atan(max(muS, -0.1975) * tan(1.26 * 1.1)) / 1.1 + (1.0 - 0.26)) * 0.5 * (1.0 - 1.0 / RES_MU_S);

    float lep = (nu + 1.0) / 2.0 * (RES_NU - 1);
    float uNu = floor(lep);
    lep = lep - uNu;

    return table.SampleLevel(s, float3((uNu + uMuS) / RES_NU, uMu, uR), 0) * (1.0 - lep)
    + table.SampleLevel(s, float3((uNu + uMuS + 1.0) / RES_NU, uMu, uR), 0) * lep;

}

// ----------------------------------------------------------------------------
// UTILITY FUNCTIONS
// ----------------------------------------------------------------------------

// nearest intersection of ray r,mu with ground or top atmosphere boundary
// mu=cos(ray zenith angle at ray origin)
float Limit(float r, float mu) 
{
    float dout = -r * mu + sqrt(r * r * (mu * mu - 1.0) + RL * RL);
    float delta2 = r * r * (mu * mu - 1.0) + Rg * Rg;
    if (delta2 >= 0.0) {
        float din = -r * mu - sqrt(delta2);
        if (din >= 0.0) {
            dout = min(dout, din);
        }
    }
    return dout;
}

// optical depth for ray (r,mu) of length d, using analytic formula
// (mu=cos(view zenith angle)), intersections with ground ignored
// H=height scale of exponential density function
float OpticalDepth(float H, float r, float mu, float d) 
{
    float a = sqrt((0.5/H)*r);
    float2 a01 = a*float2(mu, mu + d / r);
    float2 a01s = sign(a01);
    float2 a01sq = a01*a01;
    float x = a01s.y > a01s.x ? exp(a01sq.x) : 0.0;
    float2 y = a01s / (2.3193*abs(a01) + sqrt(1.52*a01sq + 4.0)) * float2(1.0, exp(-d/H*(d/(2.0*r)+mu)));
    return sqrt((6.2831*H)*r) * exp((Rg-r)/H) * (x + dot(y, float2(1.0, -1.0)));
}

//exponential integral
float Ei(float z){

	return 0.5772156649015328606065 + log( 1e-4 + abs(z) ) + z * (1.0 + z * (0.25 + z * ( (1.0/18.0) + z * ( (1.0/96.0) + z *
	(1.0/600.0) ) ) ) ); // For x!=0

}

// transmittance(=transparency) of atmosphere for infinite ray (r,mu)
// (mu=cos(view zenith angle)), intersections with ground ignored
float3 Transmittance(float r, float mu) 
{
    float2 uv = GetTransmittanceUV(r, mu);
    return _Transmittance.SampleLevel(sampler_Transmittance, uv, 0).rgb;
}

// transmittance(=transparency) of atmosphere for ray (r,mu) of length d
// (mu=cos(view zenith angle)), intersections with ground ignored
// uses analytic formula instead of transmittance texture
float3 AnalyticTransmittance(float r, float mu, float d) 
{
    return exp(- betaR * OpticalDepth(HR, r, mu, d) - betaMEx * OpticalDepth(HM, r, mu, d));
}

// transmittance(=transparency) of atmosphere for infinite ray (r,mu)
// (mu=cos(view zenith angle)), or zero if ray intersects ground
float3 TransmittanceWithShadow(float r, float mu) 
{
    return mu < -sqrt(1.0 - (Rg / r) * (Rg / r)) ? float3(0,0,0) : Transmittance(r, mu);
}

// transmittance(=transparency) of atmosphere between x and x0
// assume segment x,x0 not intersecting ground
// r=||x||, mu=cos(zenith angle of [x,x0) ray at x), v=unit direction vector of [x,x0) ray
float3 Transmittance(float r, float mu, float3 v, float3 x0) {
    float3 result;
    float r1 = length(x0);
    float mu1 = dot(x0, v) / r;
    if (mu > 0.0) {
        result = min(Transmittance(r, mu) / Transmittance(r1, mu1), 1.0);
    } else {
        result = min(Transmittance(r1, -mu1) / Transmittance(r, -mu), 1.0);
    }
    return result;
}

// transmittance(=transparency) of atmosphere between x and x0
// assume segment x,x0 not intersecting ground
// d = distance between x and x0, mu=cos(zenith angle of [x,x0) ray at x)
float3 Transmittance(float r, float mu, float d) 
{
    float3 result;
    float r1 = sqrt(r * r + d * d + 2.0 * r * mu * d);
    float mu1 = (r * mu + d) / r1;
    if (mu > 0.0) {
        result = min(Transmittance(r, mu) / Transmittance(r1, mu1), 1.0);
    } else {
        result = min(Transmittance(r1, -mu1) / Transmittance(r, -mu), 1.0);
    }
    return result;
}

float3 Irradiance(float r, float muS) 
{
    float2 uv = GetIrradianceUV(r, muS);
    return _Irradiance.SampleLevel(sampler_Irradiance, uv, 0).rgb;
}

// Rayleigh phase function
float PhaseFunctionR(float mu) 
{
    return (3.0 / (16.0 * PI)) * (1.0 + mu * mu);
}

// Mie phase function
float PhaseFunctionM(float mu) 
{
    return 1.5 * 1.0 / (4.0 * PI) * (1.0 - mieG*mieG) * pow(1.0 + (mieG*mieG) - 2.0*mieG*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + mieG*mieG);
}

// Mie phase function
float PhaseFunctionM(float g, float mu)
{
    return 1.5 * 1.0 / (4.0 * PI) * (1.0 - g*g) * pow(1.0 + (g*g) - 2.0*g*mu, -3.0/2.0) * (1.0 + mu * mu) / (2.0 + g*g);
}

//float PhaseFunctionAmbient() {
//	return 1 / (4 * PI);
//}

//Henyey-Greenstein phase function (Mie approximation)
float PhaseFunctionHG (float g, float cosTheta){

	float g2 = g * g;
	//missing * 4 in denom, hack to improve visual quality and brighhtness
	return (1 - g2) / (PI * pow(1 + g2 - 2 * g * cosTheta, 1.5));

}

float PhaseFunctionI () {

	return 1.0/4.0*PI;

}

// approximated single Mie scattering (cf. approximate Cm in paragraph "Angular precision")
float3 GetMie(float4 rayMie) 
{ 	// rayMie.rgb=C*, rayMie.w=Cm,r
    return rayMie.rgb * rayMie.w / max(rayMie.r, 1e-4) * (betaR.r / betaR);
}

float SQRT(float f, float err) {

    return f >= 0.0 ? sqrt(f) : err;

}

// ----------------------------------------------------------------------------
// PUBLIC FUNCTIONS
// ----------------------------------------------------------------------------

// incident sun light at given position (radiance)
// r=length(x)
// muS=dot(x,s) / r

//float3 SunRadiance(float surfacePosHeight, float musSurfacePos) {
//
//    return TransmittanceWithShadow(surfacePosHeight, musSurfacePos);
//
//}

float3 SkyIrradiance (float surfacePosHeight, float musSurfacePos) { 

	return Irradiance(surfacePosHeight, musSurfacePos);

}

float3 SunRadiance (float r, float mu) {

    float uR = sqrt((r - Rg) / (Rt - Rg));
    float uMu = atan((mu + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
    float2 uv = float2(uMu, uR);
    return mu < -sqrt(1.0 - (Rg / r) * (Rg / r)) ? float3(0,0,0) : _Transmittance.SampleLevel(sampler_Transmittance, uv, 0).rgb;

}

float3 SkyRadiance (float3 camera, float3 lightDir, float3 viewDir, float viewLength, float shadowLength, inout float3 attenuation) {

	float3 result = 0;

	//camera += viewDir * shadowLength;
	float viewAlt = length(camera);
	float pathLength = viewLength;
	float rMu = dot(camera, viewDir);
	float mu = rMu / viewAlt;
	float r0 = viewAlt;
	float mu0 = mu;

    float deltaSq = SQRT(rMu * rMu - viewAlt * viewAlt + Rt*Rt, 1e30);
    float din = max(-rMu - deltaSq, 0.0);
    if (din > 0.0) {
        camera += din * viewDir;
        rMu += din;
        pathLength -= din;
        mu = rMu / Rt;
        viewAlt = Rt;
    }

    if (viewAlt <= Rt) {

    	float3 pos = camera + viewDir * pathLength;
    	if (viewAlt < Rg + 2000.0) {
        	// avoids imprecision problems in aerial perspective near ground
            float f = (Rg + 2000.0) / viewAlt;
            viewAlt *= f;
            //pos *= f;
        }

        float surfacePosHeight = length(pos);

        float nu = dot(viewDir, lightDir);
        float muS = dot(camera, lightDir) / viewAlt;

        float musEnd = dot(pos, lightDir) / surfacePosHeight;
        float muEnd = dot(pos, viewDir) / surfacePosHeight;

        float4 inscatter = 0;
        attenuation *= saturate(AnalyticTransmittance(viewAlt, mu, 1e+11)); //seems to break for really large gas giants

//        if (mu > 0.0) {
//            attenuation *= min(Transmittance(viewAlt, mu) / Transmittance(surfacePosHeight, muEnd), 1.0);
//        } else {
//            attenuation *= min(Transmittance(surfacePosHeight, -muEnd) / Transmittance(viewAlt, -mu), 1.0);
//        }
//
       	float EPS = 0.002;
        float lim = -sqrt(1.0 - (Rg / viewAlt) * (Rg / viewAlt));

        if (abs(mu - lim) < EPS) {
            float a = ((mu - lim) + EPS) / (2.0 * EPS);

            mu = lim - EPS;
            surfacePosHeight = sqrt(viewAlt * viewAlt + pathLength * pathLength + 2.0 * viewAlt * pathLength * mu);
            muEnd = (viewAlt * mu + pathLength) / surfacePosHeight;
            float4 inScatter0 = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, mu, muS, nu);

            float4 inScatterA = inScatter0;

            mu = lim + EPS;
            surfacePosHeight = sqrt(viewAlt * viewAlt + pathLength * pathLength + 2.0 * viewAlt * pathLength * mu);
            muEnd = (viewAlt * mu + pathLength) / surfacePosHeight;
            inScatter0 = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, mu, muS, nu);

            float4 inScatterB = inScatter0;

            inscatter = lerp(inScatterA, inScatterB, a);

        }else{

        	inscatter = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, rMu / viewAlt, muS, nu);

        }

        // avoids imprecision problems in Mie scattering when sun is below horizon
       // inscatter.w *= smoothstep(0.00, 0.02, mu);

        float3 inscatterM = GetMie(inscatter);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM(nu);
        result = (inscatter.rgb * phaseR + inscatterM * phaseM);
      
    } 

    return result;
}

float3 SkySurfaceRadiance (float3 camera, float3 lightDir, float3 viewDir, float viewLength, float shadowLength, inout float3 attenuation) {

	float3 result = 0;

	float viewAlt = length(camera);

	float pathLength = viewLength;
	float rMu = dot(camera, viewDir);
	float mu = rMu / viewAlt;
	float r0 = viewAlt;
	float mu0 = mu;

    float deltaSq = SQRT(rMu * rMu - viewAlt * viewAlt + Rt*Rt, 1e30);
    float din = max(-rMu - deltaSq, 0.0);
    if (din > 0.0) {
        camera += din * viewDir;
        rMu += din;
        pathLength -= din;
        mu = rMu / Rt;
        viewAlt = Rt;
    }

    if (viewAlt <= Rt) {

    	float3 pos = camera + viewDir * pathLength;
    	if (viewAlt < Rg + 2000.0) {
        	// avoids imprecision problems in aerial perspective near ground
            float f = (Rg + 2000.0) / viewAlt;
            viewAlt *= f;
            pos *= f;
        }

        float surfacePosHeight = length(pos - viewDir * shadowLength);
		pathLength -= shadowLength;

        float nu = dot(viewDir, lightDir);
        float muS = dot(camera, lightDir) / viewAlt;

        float musEnd = dot(pos, lightDir) / surfacePosHeight;
        float muEnd = dot(pos, viewDir) / surfacePosHeight;

        float4 inscatter = 0;
        attenuation *= saturate(AnalyticTransmittance(viewAlt, mu, pathLength));

//         if (mu > 0.0) {
//            attenuation *= min(Transmittance(viewAlt, mu) / Transmittance(surfacePosHeight, muEnd), 1.0);
//        } else {
//            attenuation *= min(Transmittance(surfacePosHeight, -muEnd) / Transmittance(viewAlt, -mu), 1.0);
//        }

		//pathLength -= shadowLength;

       	float EPS = 0.004;
        float lim = -sqrt(1.0 - (Rg / viewAlt) * (Rg / viewAlt));

        if (abs(mu - lim) < EPS) {
            float a = ((mu - lim) + EPS) / (2.0 * EPS);

            mu = lim - EPS;
            surfacePosHeight = sqrt(viewAlt * viewAlt + pathLength * pathLength + 2.0 * viewAlt * pathLength * mu);
            muEnd = (viewAlt * mu + pathLength) / surfacePosHeight;
            float4 inScatter0 = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, mu, muS, nu);
            float4 inScatter1 = Texture4D(_Inscatter, sampler_Inscatter, surfacePosHeight, muEnd, musEnd, nu);
            float4 inScatterA = max(inScatter0 - inScatter1 * attenuation.rgbr, 0.0);

            mu = lim + EPS;
            surfacePosHeight = sqrt(viewAlt * viewAlt + pathLength * pathLength + 2.0 * viewAlt * pathLength * mu);
            muEnd = (viewAlt * mu + pathLength) / surfacePosHeight;
            inScatter0 = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, mu, muS, nu);
            inScatter1 = Texture4D(_Inscatter, sampler_Inscatter, surfacePosHeight, muEnd, musEnd, nu);
            float4 inScatterB = max(inScatter0 - inScatter1 * attenuation.rgbr, 0.0);

            inscatter = lerp(inScatterA, inScatterB, a);

        }else{

        	inscatter = Texture4D(_Inscatter, sampler_Inscatter, viewAlt, rMu / viewAlt, muS, nu);
	       	inscatter = max(inscatter - attenuation.rgbr * Texture4D(_Inscatter, sampler_Inscatter, surfacePosHeight, muEnd, musEnd, nu), 0.0);

        }

        // avoids imprecision problems in Mie scattering when sun is below horizon
        //inscatter.w *= smoothstep(0.00, 0.02, mu);

        float3 inscatterM = GetMie(inscatter);
        float phaseR = PhaseFunctionR(nu);
        float phaseM = PhaseFunctionM(nu);
        result = (inscatter.rgb * phaseR + inscatterM * phaseM);

    } 

    return result;
}

#endif