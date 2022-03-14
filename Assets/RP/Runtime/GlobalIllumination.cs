using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GlobalIllumination
{
    const string bufferName = "GI";

    public bool diffuseGI = true;
    public bool specularGI = true;
    public bool infiniteBounces = true;
    public bool debugProbes = true;
    public const int numRaysPerPerProbe = 1024;
    public const int probeColorTexSize = 8;
    public const int probeVisibilityTexSize = 16;
    public const int atlasProbesX = 256;
    public const int atlasProbesY = 32;
    public const int maxNumProbes = atlasProbesX * atlasProbesY; //max for now is 8192

    public int activeProbeCount;

    public int colorWidth, colorHeight;
    public int visWidth, visHeight;

    public bool updateVoxelFragments = true;

    struct SVONodeStruct
    {
        public Vector3 center;
        public int isLeaf;
        public int depth;
        public int parent;
        public int octant;
        public int childIsProbe;
        public int i0, i1, i2, i3, i4, i5, i6, i7;
        public int this[int idx]
        {
            get
            {
                switch (idx)
                {
                    case 0:
                        return i0;
                    case 1:
                        return i1;
                    case 2:
                        return i2;
                    case 3:
                        return i3;
                    case 4:
                        return i4;
                    case 5:
                        return i5;
                    case 6:
                        return i6;
                    case 7:
                        return i7;
                    default:
                        return 0;
                }
            }

            set
            {
                switch (idx)
                {
                    case 0:
                        i0 = value; return;
                    case 1:
                        i1 = value; return;
                    case 2:
                        i2 = value; return;
                    case 3:
                        i3 = value; return;
                    case 4:
                        i4 = value; return;
                    case 5:
                        i5 = value; return;
                    case 6:
                        i6 = value; return;
                    case 7:
                        i7 = value; return;
                    default:
                        return;
                }
            }
        }
    }
    //size of struct in bytes
    const int svoStride = sizeof(float) * 3 + sizeof(int) * (5 + 8);

    struct VoxelFragment
    {
        public int x, y, z;
    }

    public Vector3 p;
    float volumeRootSize = 512; //size of gi volume in world units
    int sceneVoxelSize = 512; // number of voxels in voxel fragment render
    public int maxDepth = 9;
    int maxNodes = (int)1e6;

    Material voxelFragsMat = new Material(Shader.Find("VoxelFragments"));
    ComputeBuffer tempSvo, finalSVO, tempNodes, voxelFragments,
        idxStack, leafNodes, probePositions, leafProbeIndices;
    ComputeBuffer newProbePositions;
    ComputeBuffer final, count, args;
    ComputeShader gisvo;

    public RenderTexture voxels, voxelDebug;

    public static bool rebuildSVO;
    bool recalculateLock = false;
    bool updateLock = false;
    bool updateSVO = false;

    bool firstFrame = true;

    public Camera primaryCamera;

    Matrix4x4[] voxelProjMatrix = new Matrix4x4[3];

    public RenderTexture probeIrradiance;
    public RenderTexture probeVisibility;
    public DeferredRenderer.GBuffer giGBuffer;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public void Init ()
    {
        colorWidth = atlasProbesX * probeColorTexSize;
        colorHeight = atlasProbesY * probeColorTexSize;

        visWidth = atlasProbesX * probeVisibilityTexSize;
        visHeight = atlasProbesY * probeVisibilityTexSize;

        RenderTextureDescriptor desc = new RenderTextureDescriptor();
        desc.dimension = TextureDimension.Tex2D;
        desc.volumeDepth = 1;
        desc.msaaSamples = 1;
        desc.enableRandomWrite = true;
        desc.colorFormat = RenderTextureFormat.ARGBHalf;
        desc.sRGB = false;

        desc.width = colorWidth;
        desc.height = colorHeight;
        probeIrradiance = new RenderTexture(desc);
        probeIrradiance.filterMode = FilterMode.Bilinear;
        probeIrradiance.Create();

        desc.width = visWidth;
        desc.height = visHeight;
        desc.colorFormat = RenderTextureFormat.RGHalf;

        probeVisibility = new RenderTexture(desc);
        probeVisibility.filterMode = FilterMode.Bilinear;
        probeVisibility.Create();

        Shader.SetGlobalInt("_AtlasX", atlasProbesX);
        Shader.SetGlobalInt("_AtlasY", atlasProbesY);

        Shader.SetGlobalInt("_ProbeIrrTexSize", probeColorTexSize);
        Shader.SetGlobalInt("_ProbeVisTexSize", probeVisibilityTexSize);
        Shader.SetGlobalVector("_GIIrradianceTexSize", new Vector2(colorWidth, colorHeight));
        Shader.SetGlobalVector("_GIVisibilityTexSize", new Vector2(visWidth, visHeight));
        Shader.SetGlobalTexture("_GIIrradiance", probeIrradiance);
        Shader.SetGlobalTexture("_GIVisibility", probeVisibility);

        giGBuffer = new DeferredRenderer.GBuffer("GI", true, true);

        InitGISVO();
    }

    void InitGISVO()
    {
        gisvo = Resources.Load<ComputeShader>("SceneGISVO");

        tempSvo = new ComputeBuffer(maxNodes, svoStride,
                ComputeBufferType.Counter);
        finalSVO = new ComputeBuffer(maxNodes, svoStride,
                ComputeBufferType.Counter);
        leafNodes = new ComputeBuffer(maxNodes, svoStride,
            ComputeBufferType.Append);
        tempNodes = new ComputeBuffer(maxNodes, svoStride,
            ComputeBufferType.Append);

        probePositions = new ComputeBuffer(maxNumProbes, sizeof(float) * 3);
        leafProbeIndices = new ComputeBuffer(maxNodes, sizeof(int) * 9);

        //needed for infinite bounces
        newProbePositions = new ComputeBuffer(maxNumProbes, sizeof(float) * 3);

        //0 -> num nodes at previous depth, 1 -> numTempNodes, 2 -> numNewNodesAtDepth,
        //3, 4, 5-> dispatch args (0), 6, 7, 8 -> dispatch args (1)
        args = new ComputeBuffer(9, sizeof(int), ComputeBufferType.IndirectArguments);
        count = new ComputeBuffer(3, sizeof(int));

        voxels = new RenderTexture(sceneVoxelSize, sceneVoxelSize, 0, RenderTextureFormat.R8);
        voxels.dimension = TextureDimension.Tex3D;
        voxels.volumeDepth = sceneVoxelSize;
        voxels.enableRandomWrite = true;
        voxels.Create();

        voxelDebug = new RenderTexture(sceneVoxelSize, sceneVoxelSize, 0, RenderTextureFormat.ARGB32);
        voxelDebug.Create();

        //generate projection matrices for voxel fragment rendering
        Quaternion[] rot = new Quaternion[]
        {
            Quaternion.Euler(0, -90, 0),
            Quaternion.Euler(90, 0, 0),
            Quaternion.Euler(0, 180, 0)
        };
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = Vector3.zero;
            p[i] = 1;
            Matrix4x4 view = Matrix4x4.TRS(p * 100, rot[i], Vector3.one).inverse;
            view.m20 *= -1f;
            view.m21 *= -1f;
            view.m22 *= -1f;
            view.m23 *= -1f;
            voxelProjMatrix[i] = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-512, 512, -256, 256, -256, 256), true) * view;
        }
        voxelFragsMat.SetMatrixArray("_VoxProjMat", voxelProjMatrix);

        Shader.SetGlobalInt("MAX_GI_SVO_DEPTH", maxDepth);
        Shader.SetGlobalBuffer("GI_SVO", finalSVO);
        Shader.SetGlobalBuffer("ProbePositions", probePositions);
        Shader.SetGlobalBuffer("NewProbePositions", newProbePositions);
    }

    public void UpdateGI ()
    {
        if (rebuildSVO)
        {
            Shader.SetGlobalFloat("_GIVolumeSize", volumeRootSize);
            RecalculateGISVO();
            UpdateGISVO();
        }
        UpdateGIProbes();
    }

    void RecalculateGISVO()
    {
        if (recalculateLock) return;

        if (firstFrame || updateVoxelFragments)
        {
            voxelFragsMat.SetVector("_VolumeOffset", -Vector3.one * volumeRootSize * 0.5f);

            //clear voxel fragment texture
            buffer.SetComputeTextureParam(gisvo, 0, "VoxelFragments", voxels);
            buffer.DispatchCompute(gisvo, 0, sceneVoxelSize / 8, sceneVoxelSize / 8, sceneVoxelSize / 8);

            buffer.SetRenderTarget(voxelDebug);
            buffer.SetRandomWriteTarget(1, voxels);
            buffer.SetViewport(new Rect(0, 0, sceneVoxelSize, sceneVoxelSize));
            var renderers = Object.FindObjectsOfType<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                int submeshCount = renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                for (int i = renderer.subMeshStartIndex; i < submeshCount; i++)
                    buffer.DrawRenderer(renderer, voxelFragsMat, i);
            }
            var skinnedRenderers = Object.FindObjectsOfType<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                buffer.DrawRenderer(renderer, voxelFragsMat);
            }
        }

        SVONodeStruct root = new SVONodeStruct();
        root.center = Vector3.zero;

        buffer.SetBufferCounterValue(leafNodes, 0);

        buffer.SetBufferData(tempSvo, new SVONodeStruct[] { root });
        buffer.SetBufferCounterValue(tempSvo, 1);

        buffer.SetBufferData(args, new uint[] { 1, 0, 0, 1, 1, 1, 1, 1, 1 });
        buffer.SetBufferData(count, new uint[] { 0 });

        buffer.SetComputeBufferParam(gisvo, 1, "COUNT", count);
        buffer.SetComputeBufferParam(gisvo, 1, "SVO", tempSvo);
        buffer.SetComputeBufferParam(gisvo, 1, "TempNodes_Append", tempNodes);
        buffer.SetComputeBufferParam(gisvo, 1, "ARGS", args);

        buffer.SetComputeBufferParam(gisvo, 2, "SVO", tempSvo);
        buffer.SetComputeBufferParam(gisvo, 2, "ARGS", args);
        buffer.SetComputeBufferParam(gisvo, 2, "TempNodes", tempNodes);
        buffer.SetComputeBufferParam(gisvo, 2, "LeafNodes", leafNodes);
        buffer.SetComputeTextureParam(gisvo, 2, "VoxelFragments", voxels);

        buffer.SetComputeBufferParam(gisvo, 3, "ARGS", args);

        buffer.SetComputeBufferParam(gisvo, 4, "COUNT", count);
        buffer.SetComputeBufferParam(gisvo, 4, "ARGS", args);

        buffer.SetComputeVectorParam(gisvo, "_CameraPos", primaryCamera.transform.position);

        for (int i = 1; i < maxDepth; i++)
        {
            buffer.SetBufferCounterValue(tempNodes, 0);
            float nodeSize = (volumeRootSize / (1 << i));
            buffer.SetComputeFloatParam(gisvo, "_NodeSize", nodeSize);
            buffer.SetComputeIntParam(gisvo, "_Depth", i);
            buffer.SetComputeIntParam(gisvo, "_ReachedMaxDepth", i == (maxDepth - 1) ? 1 : 0);

            buffer.DispatchCompute(gisvo, 1, args, sizeof(int) * 3);
            buffer.CopyCounterValue(tempNodes, args, sizeof(int) * 1);
            buffer.DispatchCompute(gisvo, 3, 1, 1, 1);
            buffer.DispatchCompute(gisvo, 2, args, sizeof(int) * 6);
            buffer.CopyCounterValue(tempSvo, args, sizeof(int) * 2);
            buffer.DispatchCompute(gisvo, 4, 1, 1, 1);
        }
        buffer.CopyCounterValue(leafNodes, args, sizeof(int) * 1);
        buffer.CopyCounterValue(tempSvo, count, 0);
        buffer.CopyCounterValue(tempSvo, args, 0);

        System.Action<AsyncGPUReadbackRequest> argsCallback = delegate (AsyncGPUReadbackRequest request) { ArgsReadback(request); };
        buffer.RequestAsyncReadback(args, argsCallback);
        if(firstFrame)
            buffer.WaitAllAsyncReadbackRequests();

        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        recalculateLock = true;

    }

    SVONodeStruct[] leafData;
    int[] countData;
    //get number of leaf nodes
    void ArgsReadback(AsyncGPUReadbackRequest request)
    {
        countData = request.GetData<int>().ToArray();
        System.Action<AsyncGPUReadbackRequest> leafCallback = delegate (AsyncGPUReadbackRequest request) { LeavesReadback(request); };
        AsyncGPUReadbackRequest request2 = AsyncGPUReadback.Request(leafNodes, countData[1] * svoStride, 0, leafCallback);
        if (firstFrame)
            request2.WaitForCompletion();
    }

    void LeavesReadback(AsyncGPUReadbackRequest request)
    {
        leafData = request.GetData<SVONodeStruct>().ToArray();
        updateLock = false;
    }

    List<Vector3> probePositionsData = new List<Vector3>();
    void UpdateGISVO ()
    {
        if (updateLock) return;
        if (leafData != null)
        {
            Dictionary<Vector3, int> idMap = new Dictionary<Vector3, int>();
            List<int> nodeProbeIndices = new List<int>();
            //each node adds its own index + the indices for the probes
            for (int i = 0; i < leafData.Length; i++)
            {
                float nodeSize = (volumeRootSize / (1 << leafData[i].depth));
                Vector3 center = leafData[i].center;
                //leafData[i][0] is node self index
                nodeProbeIndices.Add(leafData[i][0]);

                for (int n = 0; n < 8; n++)
                {
                    Vector3 offset = new Vector3(n % 2, n / 4, (n / 2) % 2) - Vector3.one * 0.5f;
                    Vector3 probePos = center + offset * nodeSize;

                    int probeId;
                    if(idMap.ContainsKey(probePos))
                    {
                        probeId = idMap[probePos];
                    }
                    else
                    {
                        probeId = idMap.Count;
                        idMap.Add(probePos, probeId);
                    }

                    nodeProbeIndices.Add(probeId);
                    leafData[i][n] = probeId;
                }
            }

            activeProbeCount = idMap.Count;

            probePositionsData = new List<Vector3>(idMap.Keys);

            buffer.SetBufferData(leafProbeIndices, nodeProbeIndices);
            buffer.SetBufferData(newProbePositions, probePositionsData);

            buffer.SetComputeBufferParam(gisvo, 5, "ARGS", args);
            buffer.SetComputeBufferParam(gisvo, 5, "LeafProbeIndices", leafProbeIndices);
            buffer.SetComputeBufferParam(gisvo, 5, "SVO", tempSvo);
            buffer.DispatchCompute(gisvo, 5, Mathf.CeilToInt(countData[1] / 64.0f), 1, 1);

            buffer.SetGlobalInt("_NumProbes", activeProbeCount);

            Graphics.ExecuteCommandBuffer(buffer);
            buffer.Clear();

            updateSVO = true;
            if (firstFrame)
                UpdateSVO();
        }

        updateLock = true;
    }

    //copy temp svo to final svo
    void UpdateSVO ()
    {
        buffer.SetBufferData(probePositions, probePositionsData);

        buffer.SetComputeBufferParam(gisvo, 6, "ARGS", args);
        buffer.SetComputeBufferParam(gisvo, 6, "SVO", tempSvo);
        buffer.SetComputeBufferParam(gisvo, 6, "FINAL_SVO", finalSVO);
        buffer.DispatchCompute(gisvo, 6, Mathf.CeilToInt(countData[0] / 64.0f), 1, 1);
        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        recalculateLock = false;
        updateSVO = false;
        firstFrame = false;
    }

    void UpdateGIProbes ()
    {
        int raysBufferWidth = activeProbeCount;
        int raysBufferHeight = numRaysPerPerProbe;

        if (raysBufferWidth == 0 || raysBufferHeight == 0) return;

        giGBuffer.GetTemporary(raysBufferWidth, raysBufferHeight, buffer);

        Shader.SetGlobalInt("_NumRaysPerProbe", numRaysPerPerProbe);
        buffer.SetRayTracingIntParam(CameraRenderer.rayReflectionShader, "_Offset", 0);

        DeferredRenderer.RenderRTGBuffer(buffer, giGBuffer, "GIGBuffer", 0,
           raysBufferWidth, raysBufferHeight);
        DeferredRenderer.RenderRTLighting(buffer, giGBuffer, "GIRayLighting",
           raysBufferWidth, raysBufferHeight);

        int kernelCountZ = Mathf.CeilToInt(activeProbeCount / 16.0f);
        buffer.SetComputeIntParam(CameraRenderer.giUpdateShader, "_NumRaysPerProbe", numRaysPerPerProbe);

        buffer.SetComputeIntParam(CameraRenderer.giUpdateShader, "_ProbeTexSize", probeColorTexSize);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 0, "ProbeOutput", probeIrradiance);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 0, "RaysPosition", giGBuffer.depthOrPosition);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 0, "RaysIrradiance", giGBuffer.irradiance);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 0, "RaysNormal", giGBuffer.normal);
        buffer.DispatchCompute(CameraRenderer.giUpdateShader, 0, probeColorTexSize / 8,
            probeColorTexSize / 8, kernelCountZ);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 2, "ProbeOutput", probeIrradiance);
        buffer.DispatchCompute(CameraRenderer.giUpdateShader, 2, probeColorTexSize / 8,
          probeColorTexSize / 8, kernelCountZ);

        buffer.SetComputeIntParam(CameraRenderer.giUpdateShader, "_ProbeTexSize", probeVisibilityTexSize);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 1, "ProbeOutput", probeVisibility);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 1, "RaysPosition", giGBuffer.depthOrPosition);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 1, "RaysIrradiance", giGBuffer.irradiance);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 1, "RaysNormal", giGBuffer.normal);
        buffer.DispatchCompute(CameraRenderer.giUpdateShader, 1, probeVisibilityTexSize / 8,
            probeVisibilityTexSize / 8, kernelCountZ);
        buffer.SetComputeTextureParam(CameraRenderer.giUpdateShader, 2, "ProbeOutput", probeVisibility);
        buffer.DispatchCompute(CameraRenderer.giUpdateShader, 2, probeVisibilityTexSize / 8,
          probeVisibilityTexSize / 8, kernelCountZ);

        giGBuffer.Release(buffer);

        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        if (updateSVO)
            UpdateSVO();
    }
    public void Cleanup ()
    {
        probeIrradiance.Release();
        probeVisibility.Release();

        tempSvo.Dispose();
        finalSVO.Dispose();
        args.Dispose();
        count.Dispose();
        tempNodes.Dispose();
        leafNodes.Dispose();
        leafProbeIndices.Dispose();

        newProbePositions.Dispose();
        probePositions.Dispose();

        voxels.Release();
        voxelDebug.Release();
    }

}
