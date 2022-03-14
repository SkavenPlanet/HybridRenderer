using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Hybrid Render Pipeline")]
public class HybridRenderPipelineAsset : RenderPipelineAsset {

    protected override RenderPipeline CreatePipeline()
    {
        return new HybridRenderPipeline();
    }
}