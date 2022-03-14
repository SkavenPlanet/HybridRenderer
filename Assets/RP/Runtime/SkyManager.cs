using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyManager
{

    [System.Serializable]
    public class Model
    {
        public int modelIndex;
        public Vector3 betaR = Vector3.zero;
        public Vector3 betaM = Vector3.zero;
        public float HR;
        public float HM;
        public float mieG;
        public static int modelDataSize = 9; //number of float params for model data

        public Texture2D transmittance, irradiance;
        public Texture3D inscatter;

        public Model(string modelName)
        {

            transmittance = new Texture2D(TRANSMITTANCE_W, TRANSMITTANCE_H, TextureFormat.RGBAHalf, false);
            transmittance.wrapMode = TextureWrapMode.Clamp;
            transmittance.filterMode = FilterMode.Bilinear;

            irradiance = new Texture2D(SKY_W, SKY_H, TextureFormat.RGBAHalf, false);
            irradiance.wrapMode = TextureWrapMode.Clamp;
            irradiance.filterMode = FilterMode.Bilinear;

            inscatter = new Texture3D(RES_MU_S * RES_NU, RES_MU, RES_R, TextureFormat.RGBAHalf, false);
            inscatter.wrapMode = TextureWrapMode.Clamp;
            inscatter.filterMode = FilterMode.Bilinear;
            LoadModel(modelName);

        }

        public Model(Vector3 betaR, Vector3 betaM, float HR, float HM, float mieG, Texture2D transmittance, Texture2D irradiance, Texture3D inscatter)
        {
            this.betaR = betaR;
            this.betaM = betaM;

            this.HR = HR;
            this.HM = HM;
            this.mieG = mieG;

            this.transmittance = transmittance;
            this.irradiance = irradiance;
            this.inscatter = inscatter;
        }

        void LoadModel(string modelName)
        {

            float[] modelData = new float[modelDataSize];
            string modelPath = Application.dataPath + "/Resources/AtmosphereModels/" + modelName;
            string path;

            path = modelPath + "/modelData.raw";
            LoadRawTexture.LoadRawFile(path, modelDataSize, modelData);

            path = modelPath + "/transmittance.raw";
            LoadRawTexture.WriteIntoTexture2D(transmittance, TRANSMITTANCE_CHANNELS, path);

            path = modelPath + "/irradiance.raw";
            LoadRawTexture.WriteIntoTexture2D(irradiance, IRRADIANCE_CHANNELS, path);

            path = modelPath + "/inscatter.raw";
            LoadRawTexture.WriteIntoTexture3D(inscatter, INSCATTER_CHANNELS, path);

            betaR.x = modelData[0];
            betaR.y = modelData[1];
            betaR.z = modelData[2];

            betaM.x = modelData[3];
            betaM.y = modelData[4];
            betaM.z = modelData[5];

            HR = modelData[6];
            HM = modelData[7];
            mieG = modelData[8];
        }
    }

    public const int TRANSMITTANCE_W = 256;
    public const int TRANSMITTANCE_H = 64;

    public const int SKY_W = 64;
    public const int SKY_H = 16;

    public const int RES_R = 32;
    public const int RES_MU = 128;
    public const int RES_MU_S = 64;
    public const int RES_NU = 8;

    const int TRANSMITTANCE_CHANNELS = 3;
    const int INSCATTER_CHANNELS = 4;
    const int IRRADIANCE_CHANNELS = 3;

    const int volumeSize = 128;
    const int volumeChannels = 1;

    public float lowestPoint;

    const float AVERAGE_GROUND_REFLECTANCE = 0.3f;

    string modelName = "Terran";
    public Model model;

    public float groundRadius = 6360000, atmosRadius = 6420000;
    public Texture3D volumeDensity;

    public Texture2D cumulusMap;
    //r channel - coverage
    //g channel - max height (%)

    public void InitAtmosParams()
    {
        volumeDensity = new Texture3D(volumeSize, volumeSize, volumeSize, TextureFormat.Alpha8, true);
        volumeDensity.wrapMode = TextureWrapMode.Repeat;
        volumeDensity.filterMode = FilterMode.Bilinear;

        LoadRawTexture.WriteIntoTexture3DAlpha(volumeDensity, 1, Application.dataPath + "/Resources/volumetricDensity.raw");

        cumulusMap = Resources.Load("Cloud Map") as Texture2D;

        Shader.SetGlobalTexture("_Transmittance", model.transmittance);
        Shader.SetGlobalTexture("_Irradiance", model.irradiance);
        Shader.SetGlobalTexture("_Inscatter", model.inscatter);

        Shader.SetGlobalTexture("_VolumeDensity", volumeDensity);
        Shader.SetGlobalTexture("_CloudMap", cumulusMap);

        Shader.SetGlobalFloat("Rg", groundRadius);
        Shader.SetGlobalFloat("Rp", groundRadius);
        Shader.SetGlobalFloat("Rt", atmosRadius);
        Shader.SetGlobalFloat("RL", atmosRadius + 1e3f);

        Shader.SetGlobalFloat("RES_R", RES_R);
        Shader.SetGlobalFloat("RES_MU", RES_MU);
        Shader.SetGlobalFloat("RES_MU_S", RES_MU_S);
        Shader.SetGlobalFloat("RES_NU", RES_NU);

        Shader.SetGlobalVector("betaR", model.betaR / 1e3f);
        Shader.SetGlobalFloat("mieG", model.mieG);
        Shader.SetGlobalFloat("HR", model.HR * 1e3f);
        Shader.SetGlobalFloat("HM", model.HM * 1e3f);
        Shader.SetGlobalVector("betaMSca", model.betaM / 1e3f);
        Shader.SetGlobalVector("betaMEx", (model.betaM / 1e3f) / 0.9f);

        //Shader.SetGlobalVector("_PlanetPosition", -Vector3.up * groundRadius);

    }

    public void InitShader(int kernel, ComputeShader shader)
    {

        shader.SetTexture(kernel, "_Transmittance", model.transmittance);
        shader.SetTexture(kernel, "_Irradiance", model.irradiance);
        shader.SetTexture(kernel, "_VolumeDensity", volumeDensity);

        shader.SetFloat("Rg", groundRadius);
        shader.SetFloat("Rp", groundRadius);
        shader.SetFloat("Rt", atmosRadius);
        shader.SetFloat("RL", atmosRadius + 1e3f);

        shader.SetFloat("RES_R", RES_R);
        shader.SetFloat("RES_MU", RES_MU);
        shader.SetFloat("RES_MU_S", RES_MU_S);
        shader.SetFloat("RES_NU", RES_NU);

        shader.SetVector("betaR", model.betaR / 1e3f);
        shader.SetFloat("mieG", model.mieG);
        shader.SetFloat("HR", model.HR * 1e3f);
        shader.SetFloat("HM", model.HM * 1e3f);
        shader.SetVector("betaMSca", model.betaM / 1e3f);
        shader.SetVector("betaMEx", (model.betaM / 1e3f) / 0.9f);
        //shader.SetVector("_PlanetPosition", -Vector3.up * groundRadius);

    }

    //public void UpdateShader(ComputeShader shader, int i, float start)
    //{

    //    shader.SetFloat("_FogExpFalloff", fogAlt);
    //    shader.SetFloat("_FogThickness", fogThickness);
    //    shader.SetFloat("_FogScatter", fogScatter);
    //    shader.SetFloat("_FogExtinction", fogExtinction);
    //    shader.SetFloat("_FogAnisotropy", fogAnisotropy);

    //    shader.SetVector("_VolumeSize", fogCascadeDims[i]);
    //    shader.SetMatrix("_ClipToWorld", clipToView);
    //    shader.SetVector("_LightDir", sun.transform.forward);
    //    shader.SetFloat("_LightIntensity", 1e5f);
    //    shader.SetFloat("_FogZStart", start);
    //    shader.SetFloat("_FogDist", fogCascadeZ[i] - start);
    //    shader.SetInt("_ApplyShadows", fogCascadeShadow[i] ? 1 : 0);

    //    shader.SetTexture(1, "_CloudShadow", cloudShadow);
    //    shader.SetMatrix("_CloudShadowMatrix", shadowWorldToClip);

    //}

}
