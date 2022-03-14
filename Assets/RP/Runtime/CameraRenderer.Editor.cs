using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    partial void PrepareBuffer(Camera camera);
    partial void PrepareForSceneWindow();
    //public static void DrawGizmos(Camera camera, ScriptableRenderContext context);
    static partial void DrawUnsupportedShaders(Camera camera, CullingResults cullResults, ScriptableRenderContext context);

#if UNITY_EDITOR
    static Material errorMaterial;

    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    static string SampleName { get; set; }

    partial void PrepareBuffer(Camera camera)
    {
        buffer.name = camera.name;
    }

    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

    public static void DrawGizmos(Camera camera, ScriptableRenderContext context)
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    static partial void DrawUnsupportedShaders(Camera camera, CullingResults cullResults, ScriptableRenderContext context)
    {
        if (errorMaterial == null)
        {
            errorMaterial =
                new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(
            legacyShaderTagIds[0], new SortingSettings(camera)
        ) {
            overrideMaterial = errorMaterial
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(
            cullResults, ref drawingSettings, ref filteringSettings
        );
    }
#else

    const string SampleName = bufferName;

#endif
}