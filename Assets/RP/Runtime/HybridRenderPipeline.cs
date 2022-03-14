using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
public class HybridRenderPipeline : RenderPipeline
{

    SkyManager skyManager = new SkyManager();
    LightManager lightManager = new LightManager();
    GlobalIllumination giManager = new GlobalIllumination();

    CameraRenderer renderer = new CameraRenderer();

    RayTracingAccelerationStructure accelStructure;
    bool accelBuilt = false;

    ComputeBuffer weightsBuffer;
    public HybridRenderPipeline()
    {
        GraphicsSettings.lightsUseLinearIntensity = true;

        var settings = new RayTracingAccelerationStructure.RASSettings();
        settings.layerMask = LayerMask.GetMask("Default");
        settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Manual;
        settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;

        accelStructure = new RayTracingAccelerationStructure(settings);

        float blurRadius = 12;
        float sigma = ((int)blurRadius) / 1.5f;
        weightsBuffer = new ComputeBuffer((int)blurRadius * 2 + 1, sizeof(float));
        float[] blurWeights = OneDimensinalKernel((int)blurRadius, sigma);
        weightsBuffer.SetData(blurWeights);

        Shader.SetGlobalBuffer("gWeights", weightsBuffer);
        Shader.SetGlobalInt("blurRadius", (int)blurRadius);

        skyManager.model = new SkyManager.Model("Terran");
        skyManager.InitAtmosParams();

        lightManager.Init();
        giManager.Init();
    }

    protected override void Dispose (bool disposing)
    {
        lightManager.Cleanup();
        giManager.Cleanup();
        weightsBuffer.Dispose();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        Shader.SetGlobalFloat("_LocalTime", 0);

        foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>() as MeshRenderer[])
        {
            if (renderer.gameObject.layer == LayerMask.NameToLayer("DontRender"))
                continue;

            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            bool[] transparencyFlags = new bool[mesh.subMeshCount];
            bool[] flags = new bool[mesh.subMeshCount];
            uint mask = 0x0F;
            for (int i = 0; i < transparencyFlags.Length; i++)
            {
                bool isTransparent = renderer.sharedMaterials[i].IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");
                bool isAlphaTested = renderer.sharedMaterials[i].IsKeywordEnabled("_ALPHATEST_ON");
                transparencyFlags[i] = isTransparent || isAlphaTested;
                flags[i] = renderer.sharedMaterials[i] != null;
                if (isTransparent)
                {
                    mask = 0xF0;
                }
            }
            accelStructure.AddInstance(renderer, flags, transparencyFlags, true, false, mask);
            accelStructure.UpdateInstanceTransform(renderer);
        }

        foreach (SkinnedMeshRenderer renderer in Object.FindObjectsOfType<SkinnedMeshRenderer>() as SkinnedMeshRenderer[])
        {
            bool[] submesh = new bool[renderer.sharedMesh.subMeshCount];
            uint mask = 0x0F;
            for (int i = 0; i < submesh.Length; i++)
            {
                submesh[i] = true;
            }
            accelStructure.AddInstance(renderer, submesh, null, true, false, mask);
            accelStructure.UpdateInstanceTransform(renderer);
        }

        accelStructure.Build();
        lightManager.SetupLights();
        giManager.primaryCamera = Application.isPlaying ? Camera.main : SceneView.lastActiveSceneView.camera;
        giManager.UpdateGI();
        giManager.debugProbes = false;
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, lightManager, giManager, accelStructure);
        }
    }

    float[] OneDimensinalKernel(int radius, float sigma)
    {
        float[] kernelResult = new float[radius * 2 + 1];
        float sum = 0.0f;
        for (int t = 0; t < radius; t++)
        {
            double newBlurWalue = 0.39894 * Mathf.Exp(-0.5f * t * t / (sigma * sigma)) / sigma;
            kernelResult[radius + t] = (float)newBlurWalue;
            kernelResult[radius - t] = (float)newBlurWalue;
            if (t != 0)
                sum += (float)newBlurWalue * 2.0f;
            else
                sum += (float)newBlurWalue;
        }
        // normalize kernels
        for (int k = 0; k < radius * 2 + 1; k++)
        {
            kernelResult[k] /= sum;
        }
        return kernelResult;
    }
}