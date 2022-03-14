using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public static class DeferredRenderer
{
    public static float cloudScatter;
    public static float cloudAbsorb;

    public class GBuffer
    {
        public const RenderTextureFormat albedoFormat = RenderTextureFormat.ARGB32;
        public const RenderTextureFormat specRoughFormat = RenderTextureFormat.ARGB32;
        public const RenderTextureFormat normalFormat = RenderTextureFormat.ARGB2101010;
        public const RenderTextureFormat emissionFormat = RenderTextureFormat.ARGBHalf;
        public const RenderTextureFormat depthFormat = RenderTextureFormat.Depth;
        public const RenderTextureFormat positionFormat = RenderTextureFormat.ARGBFloat;
        public const RenderTextureFormat irradianceFormat = RenderTextureFormat.ARGBHalf;
        public const int depthBits = 32;

        public int width, height;

        public int albedo;
        public int specRough;
        public int normal;
        public int emission;
        public int depthOrPosition;

        public int irradiance;

        public bool allUAV;
        public bool usePositionBuffer;

        public GBuffer(string gBufferName, bool usePositionBuffer = false, bool allUAV = false)
        {
            this.allUAV = allUAV;
            this.usePositionBuffer = usePositionBuffer;
            albedo = Shader.PropertyToID("Albedo_"+ gBufferName);
            specRough = Shader.PropertyToID("SpecRough_"+ gBufferName);
            normal = Shader.PropertyToID("Normal_" + gBufferName);
            emission = Shader.PropertyToID("Emission_" + gBufferName);
            depthOrPosition = Shader.PropertyToID((usePositionBuffer ? "Position_" : "Depth_")+ gBufferName);
            irradiance = Shader.PropertyToID("Irradiance_" + gBufferName);
        }

        public void GetTemporary (int width, int height, CommandBuffer buffer)
        {
            this.width = width;
            this.height = height;

            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height);
            desc.msaaSamples = 1;

            if (allUAV) desc.enableRandomWrite = true;

            desc.colorFormat = albedoFormat;
            desc.sRGB = true;
            buffer.GetTemporaryRT(albedo, desc, FilterMode.Point);
            desc.sRGB = false;

            desc.colorFormat = specRoughFormat;
            buffer.GetTemporaryRT(specRough, desc, FilterMode.Point);

            desc.colorFormat = normalFormat;
            buffer.GetTemporaryRT(normal, desc, FilterMode.Point);

            desc.colorFormat = emissionFormat;
            buffer.GetTemporaryRT(emission, desc, FilterMode.Point);

            if (!usePositionBuffer)
            {
                desc.colorFormat = RenderTextureFormat.Depth;
                desc.depthBufferBits = depthBits;
                desc.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UInt;
            } else
            {
                desc.colorFormat = positionFormat;
            }
            buffer.GetTemporaryRT(depthOrPosition, desc, FilterMode.Point);
            desc.depthBufferBits = 0;

            desc.colorFormat = irradianceFormat;
            desc.enableRandomWrite = true;
            buffer.GetTemporaryRT(irradiance, desc, FilterMode.Point);
        }

        public RenderTargetIdentifier[] GetColorIdentifiers ()
        {
            return new RenderTargetIdentifier[] {
                albedo,
                specRough,
                normal,
                emission
            };
        }

        public void Release (CommandBuffer buffer)
        {
            buffer.ReleaseTemporaryRT(albedo);
            buffer.ReleaseTemporaryRT(specRough);
            buffer.ReleaseTemporaryRT(normal);
            buffer.ReleaseTemporaryRT(emission);
            buffer.ReleaseTemporaryRT(depthOrPosition);
            buffer.ReleaseTemporaryRT(irradiance);
        }
    }

    public static void ExecuteRenderLoop(Camera camera, LightManager lights, 
        GlobalIllumination gi, CullingResults cullResults, ScriptableRenderContext context,
        CommandBuffer buffer)
    {

        int width = camera.scaledPixelWidth;
        int height = camera.scaledPixelHeight;
        int aa = 1;
        RenderTargetIdentifier target = BuiltinRenderTextureType.CameraTarget;

        if (camera.targetTexture)
        {
            width = camera.targetTexture.width;
            height = camera.targetTexture.height;
            target = camera.targetTexture;
            aa = camera.targetTexture.antiAliasing;
        }

        GBuffer primaryGBuffer = new GBuffer("Primary");
        GBuffer reflectionGBuffer = new GBuffer("Reflection", true, true);
        primaryGBuffer.GetTemporary(width, height, buffer);
        reflectionGBuffer.GetTemporary(width, height, buffer);

        int shadowID = Shader.PropertyToID("Shadow");
        int aoID = Shader.PropertyToID("AO");
        int tempShadowID = Shader.PropertyToID("TempShadow"); //used for blurring shadows and AO

        RenderTextureDescriptor shadowDesc = new RenderTextureDescriptor(width, height);
        shadowDesc.msaaSamples = aa;
        shadowDesc.colorFormat = RenderTextureFormat.R8;
        shadowDesc.depthBufferBits = 0;
        shadowDesc.sRGB = false;
        shadowDesc.enableRandomWrite = true;

        buffer.GetTemporaryRT(shadowID, shadowDesc);
        buffer.GetTemporaryRT(aoID, shadowDesc);
        buffer.GetTemporaryRT(tempShadowID, shadowDesc);

        buffer.SetRenderTarget(primaryGBuffer.GetColorIdentifiers(), primaryGBuffer.depthOrPosition);
        buffer.ClearRenderTarget(RTClearFlags.All, Color.clear, 1, 0);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        RenderGBuffer(buffer, camera, cullResults, context);

        //raytracing shaders rely on stencil values
        buffer.SetGlobalTexture("Stencil", primaryGBuffer.depthOrPosition, RenderTextureSubElement.Stencil);

        buffer.SetRayTracingTextureParam(CameraRenderer.rayShadowShader, "PrimaryDepth", primaryGBuffer.depthOrPosition);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayShadowShader, "PrimaryNormal", primaryGBuffer.normal);

        buffer.SetRayTracingTextureParam(CameraRenderer.rayShadowShader, "RenderTarget", shadowID);
        RenderRTShadow(buffer, primaryGBuffer, "DirectionalShadow");

        buffer.SetRayTracingTextureParam(CameraRenderer.rayShadowShader, "RenderTarget", aoID);
        RenderRTShadow(buffer, primaryGBuffer, "AmbientOcclusion");

        //blur shadows and AO
        GaussianBlur(buffer, shadowID, tempShadowID, primaryGBuffer.width, primaryGBuffer.height);
        GaussianBlur(buffer, aoID, tempShadowID, primaryGBuffer.width, primaryGBuffer.height);

        float pixelSpreadAngle = Mathf.Atan2(2 * Mathf.Tan(camera.fieldOfView / 2), height);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "PrimaryDepth", primaryGBuffer.depthOrPosition);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "PrimaryNormal", primaryGBuffer.normal);
        RenderRTGBuffer(buffer, reflectionGBuffer, "ReflectionGBuffer", pixelSpreadAngle);

        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "PrimaryDepth", primaryGBuffer.depthOrPosition);
        RenderRTLighting(buffer, reflectionGBuffer, "ReflectionRayLighting");

        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "Shadow", shadowID);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "AO", aoID);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "SpecularGI", reflectionGBuffer.irradiance);
        RenderRTLighting(buffer, primaryGBuffer, "PrimaryRayLighting");

        //debug gi probes
        if (gi.debugProbes && gi.activeProbeCount > 0)
        {
            SetRenderTarget(buffer, context, primaryGBuffer.irradiance, primaryGBuffer.depthOrPosition);
            RenderGIProbesDebug(buffer, context, gi.activeProbeCount);
        }

        SetRenderTarget(buffer, context, primaryGBuffer.albedo);
        buffer.SetGlobalTexture("_Frame", primaryGBuffer.irradiance);
        RenderTonemapping(buffer, context);

        //GaussianBlur(buffer, primaryGBuffer.albedo, reflectionGBuffer.albedo, primaryGBuffer.width, primaryGBuffer.height);

        SetRenderTarget(buffer, context, primaryGBuffer.albedo, primaryGBuffer.depthOrPosition);
        CameraRenderer.DrawGizmos(camera, context);

        buffer.Blit(primaryGBuffer.albedo, BuiltinRenderTextureType.CameraTarget);

        primaryGBuffer.Release(buffer);
        reflectionGBuffer.Release(buffer);
        gi.giGBuffer.Release(buffer);

        buffer.ReleaseTemporaryRT(shadowID);
        buffer.ReleaseTemporaryRT(aoID);
        buffer.ReleaseTemporaryRT(tempShadowID);
    }

    public static void SetRenderTarget (CommandBuffer buffer, ScriptableRenderContext context, int colorID, int depthID = -1)
    {
        if (depthID < 0)
        {
            buffer.SetRenderTarget(colorID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        } else
        {
            buffer.SetRenderTarget(colorID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthID, RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);
        }
        
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void RenderRTGBuffer (CommandBuffer buffer, GBuffer gBuffer, string rayGenName, float pixelSpreadAngle, 
        int width = -1, int height = -1)
    {
        if (width < 0) width = gBuffer.width;
        if (height < 0) height = gBuffer.height;
        buffer.SetRayTracingFloatParam(CameraRenderer.rayReflectionShader, "_PixelSpreadAngle", pixelSpreadAngle);
        buffer.SetRayTracingShaderPass(CameraRenderer.rayReflectionShader, "RTGBufferPass");
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "Albedo", gBuffer.albedo);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "RoughSpec", gBuffer.specRough);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "Normal", gBuffer.normal);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "Emission", gBuffer.emission);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayReflectionShader, "Position", gBuffer.depthOrPosition);
        buffer.DispatchRays(CameraRenderer.rayReflectionShader, rayGenName, (uint)width, (uint)height, 1);
    } 
    public static void RenderRTLighting (CommandBuffer buffer, GBuffer gBuffer, string rayGenName,
        int width = -1, int height = -1)
    {
        if (width < 0) width = gBuffer.width;
        if (height < 0) height = gBuffer.height;
        buffer.SetRayTracingShaderPass(CameraRenderer.rayDeferredLightingShader, "RTShadowPass");
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "Albedo", gBuffer.albedo);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "SpecRough", gBuffer.specRough);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "Normal", gBuffer.normal);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, 
            gBuffer.usePositionBuffer ? "Position" : "Depth", gBuffer.depthOrPosition);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "Emission", gBuffer.emission);
        buffer.SetRayTracingTextureParam(CameraRenderer.rayDeferredLightingShader, "RenderTarget", gBuffer.irradiance);
        buffer.DispatchRays(CameraRenderer.rayDeferredLightingShader, rayGenName, (uint)width, (uint)height, 1);
    }

    public static void RenderRTShadow(CommandBuffer buffer, GBuffer gBuffer, string rayGenName,
        int width = -1, int height = -1)
    {
        if (width < 0) width = gBuffer.width;
        if (height < 0) height = gBuffer.height;
        buffer.SetRayTracingShaderPass(CameraRenderer.rayShadowShader, "RTShadowPass");
        buffer.DispatchRays(CameraRenderer.rayShadowShader, rayGenName, (uint)width, (uint)height, 1);
    }

    public static void RenderGBuffer (CommandBuffer buffer, Camera camera, CullingResults cullResults, ScriptableRenderContext context)
    {
        CameraRenderer.DrawOpaqueGeometry(camera, cullResults, context, CameraRenderer.gBufferTagId);
    }

    //unused
    //public static void RenderLighting (CommandBuffer buffer, ScriptableRenderContext context, int pass = 0)
    //{
    //    DrawScreenTriangle(buffer, CameraRenderer.lightingMaterial, pass, context);
    //}
    //public static void RenderAtmosphere(CommandBuffer buffer, ScriptableRenderContext context, int pass = -1)
    //{
    //    DrawScreenTriangle(buffer, CameraRenderer.atmosphereMaterial, pass, context);
    //}

    public static void RenderTonemapping(CommandBuffer buffer, ScriptableRenderContext context, int pass = -1)
    {
        DrawScreenTriangle(buffer, CameraRenderer.tonemappingMaterial, pass, context);
    }

    public static void RenderGIProbesDebug(CommandBuffer buffer, ScriptableRenderContext context, int numProbes)
    {
        buffer.DrawMeshInstancedProcedural(
            CameraRenderer.sphereMesh, 0, CameraRenderer.visGiProbesMaterial, 0, numProbes, null
        );
    }

    public static void DrawScreenTriangle(CommandBuffer buffer, Material material, int pass, ScriptableRenderContext context)
    {
        buffer.DrawProcedural(
            Matrix4x4.identity, material, pass,
            MeshTopology.Triangles, 3
        );
    }

    public static void GaussianBlur (CommandBuffer buffer, int mainId, int tempId,
        int width, int height)
    {
        buffer.SetComputeTextureParam(CameraRenderer.gaussianBlur, 0, "source", mainId);
        buffer.SetComputeTextureParam(CameraRenderer.gaussianBlur, 0, "horBlurOutput", tempId);
        buffer.SetComputeTextureParam(CameraRenderer.gaussianBlur, 1, "horBlurOutput", tempId);
        buffer.SetComputeTextureParam(CameraRenderer.gaussianBlur, 1, "verBlurOutput", mainId);
        buffer.DispatchCompute(CameraRenderer.gaussianBlur, 0, Mathf.CeilToInt(width / 64.0f), height, 1);
        buffer.DispatchCompute(CameraRenderer.gaussianBlur, 1, width, Mathf.CeilToInt(height / 64.0f), 1);
    }
}