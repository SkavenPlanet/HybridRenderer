using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

//https://catlikecoding.com/unity/tutorials/custom-srp/custom-render-pipeline/
public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;

    public static Camera shadowCam;
    const string bufferName = "Render Camera";

    public static ShaderTagId gBufferTagId = new ShaderTagId("Deferred");
    static ShaderTagId forwardTagId = new ShaderTagId("ForwardBase");

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public static Material standardMaterial = null;
    public static Material visGiProbesMaterial;
    public static Material tonemappingMaterial;
    public static Mesh sphereMesh;
    public static RayTracingShader rayShadowShader = Resources.Load("RTShadow") as RayTracingShader;
    public static RayTracingShader rayReflectionShader = Resources.Load("RTReflection") as RayTracingShader;
    public static RayTracingShader rayDeferredLightingShader = Resources.Load("DeferredLighting") as RayTracingShader;
    public static ComputeShader giUpdateShader = Resources.Load("GIUpdate") as ComputeShader;
    public static ComputeShader aoFilterShader = Resources.Load("FilterAO") as ComputeShader;
    public static ComputeShader gaussianBlur = Resources.Load("GaussianBlur") as ComputeShader;
    public static Texture2D blueNoiseTex;

    public void Render(ScriptableRenderContext context, Camera camera, LightManager lights, GlobalIllumination gi, RayTracingAccelerationStructure accelerationStructure)
    {
        this.camera = camera;
        this.context = context;
        if(standardMaterial == null)
            standardMaterial = new Material(Shader.Find("Standard"));
        PrepareBuffer(camera);
        PrepareForSceneWindow();
        CullingResults cullResults;
        if (!Cull(out cullResults))
        {
            return;
        }

        Setup(buffer, context, camera, accelerationStructure);
        switch (camera.renderingPath)
        {
            case RenderingPath.DeferredShading:
                DeferredRenderer.ExecuteRenderLoop(camera, lights, gi, cullResults, context, buffer);
                break;
            default:
            //    DrawOpaqueGeometry(camera, cullResults, context, forwardTagId);
            //    DrawTransparentGeometry(camera, cullResults, context);
            //    DrawUnsupportedShaders(camera, cullResults, context);
                break;
        }
        Submit(buffer, context);
    }

    bool Cull(out CullingResults cullResults)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //lights culled via separate system
            p.cullingOptions &= ~CullingOptions.NeedsLighting;
            cullResults = context.Cull(ref p);
            return true;
        }
        cullResults = default;
        return false;
    }

    public static void Setup(CommandBuffer buffer, ScriptableRenderContext context, 
        Camera camera, RayTracingAccelerationStructure accelerationStructure)
    {
        if(!sphereMesh)
            sphereMesh = Resources.Load<GameObject>("Sphere").GetComponent<MeshFilter>().sharedMesh;
        if (!visGiProbesMaterial)
        {
            visGiProbesMaterial = new Material(Shader.Find("Hidden/VisGIProbes"));
            visGiProbesMaterial.enableInstancing = true;
        }
        if (!tonemappingMaterial)
            tonemappingMaterial = new Material(Shader.Find("Hidden/Tonemapping"));
        if (!blueNoiseTex)
            blueNoiseTex = Resources.Load<Texture2D>("BlueNoise256");

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );

        Matrix4x4 inverseVP = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false)
            * camera.worldToCameraMatrix).inverse;
        buffer.SetGlobalMatrix("UNITY_MATRIX_I_VP", inverseVP);

        rayReflectionShader.SetMatrix("UNITY_MATRIX_I_VP", inverseVP);
        rayReflectionShader.SetAccelerationStructure("accelerationStructure", accelerationStructure);

        rayShadowShader.SetMatrix("UNITY_MATRIX_I_VP", inverseVP);
        rayShadowShader.SetAccelerationStructure("accelerationStructure", accelerationStructure);
        rayShadowShader.SetTexture("BlueNoise", blueNoiseTex);

        rayDeferredLightingShader.SetAccelerationStructure("accelerationStructure", accelerationStructure);
    }

    public static void Submit(CommandBuffer buffer, ScriptableRenderContext context)
    {
        //buffer.EndSample(SampleName);
        ExecuteBuffer(buffer, context);
        context.Submit();
    }

    public static void DrawOpaqueGeometry(Camera camera, CullingResults cullResults, ScriptableRenderContext context,
        ShaderTagId shaderTagId)
    {
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(
            cullResults, ref drawingSettings, ref filteringSettings
        );
    }

    //public static void DrawTransparentGeometry(Camera camera, CullingResults cullResults, ScriptableRenderContext context)
    //{
    //    //var sortingSettings = new SortingSettings(camera)
    //    //{
    //    //    criteria = SortingCriteria.CommonTransparent
    //    //};
    //    //var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings);
    //    //var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
    //    //context.DrawRenderers(
    //    //    cullResults, ref drawingSettings, ref filteringSettings
    //    //);
    //}

    static void ExecuteBuffer(CommandBuffer buffer, ScriptableRenderContext context)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}