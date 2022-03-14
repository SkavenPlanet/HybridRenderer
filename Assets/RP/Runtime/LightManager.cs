using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class LightManager
{
    const string bufferName = "Lights";

    const int maxDirLights = 2;
    const int maxLights = 1024;
    const int maxLightsPerCell = 8;

    public int voxelsPerAxis = 64;
    public Vector3 volumeSize;
    public Vector3 volumeOffset;

    public struct Light
    {
        public Vector3 position; //unused for directional lights
        public int lightType;
        public Vector3 color;
        public float range;
        public Vector3 direction; //unused for point lights
        public float spotAngle;
    }

    public static Vector3 sunDir = Vector3.zero;

    Light[] dirLightsData = new Light[maxDirLights];
    public static Light[] lightsData = new Light[maxLights];
    public static int numLights;

    //this can be compacted even further
    struct SVONodeStruct
    {
        public Vector3 min;
        public int numLights;
        public int lightOffset;
        public uint data; //isLeaf (1 bit), octant (3 bit), depth (4 bit), parentIdx (24 bit)
        public int i0, i1, i2, i3, i4, i5, i6, i7;
    }

    float volumeRootSize = 4096;
    const int maxLightsPerNode = 1024;
    const int maxNodes = (int)1e6;
    const int maxDepth = 13;

    ComputeBuffer dirLightDataBuffer;
    ComputeBuffer lightDataBuffer;

    ComputeBuffer svo, lightList, tempNodes, lightCullBitmask, lightCullList, maxLightsAtDepth;
    ComputeBuffer final, count, args;
    ComputeShader vlc;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public void Init ()
    {
        dirLightDataBuffer = new ComputeBuffer(maxDirLights, 12 * sizeof(float));
        lightDataBuffer = new ComputeBuffer(maxLights, 12 * sizeof(float));

        InitLightSVO();
    }

/*    public void Setup(ScriptableRenderContext context)
    {
        buffer.BeginSample(bufferName);
        SetupLights();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }*/
    public void SetupLights() {

        sunDir = -Vector3.forward;
        int dirLightIdx = 0;
        int lightIdx = 0;
        foreach (var light in Object.FindObjectsOfType<UnityEngine.Light>())
        {
            switch(light.type)
            {
                case UnityEngine.LightType.Directional:
                    if (dirLightIdx >= maxDirLights) continue;
                    dirLightsData[dirLightIdx] = SetupLight(light);
                    dirLightIdx++;
                    break;
                default:
                    if (lightIdx >= maxLights) continue;
                    lightsData[lightIdx] = SetupLight(light);
                    lightIdx++;
                    break;
            }
        }

        dirLightDataBuffer.SetData(dirLightsData);
        Shader.SetGlobalInt("_DirLightCount", dirLightIdx);
        Shader.SetGlobalBuffer("_DirLightData", dirLightDataBuffer);

        lightDataBuffer.SetData(lightsData);
        Shader.SetGlobalInt("_LightCount", lightIdx);
        Shader.SetGlobalBuffer("_LightData", lightDataBuffer);
        numLights = lightIdx;

        UpdateLightSVO();
        Shader.SetGlobalInt("MAX_SVO_DEPTH", maxDepth);
    }

    Light SetupLight (UnityEngine.Light light)
    {
        bool isDirectional = light.type == LightType.Directional;

        if (isDirectional)
        {
            sunDir = -light.GetComponent<UnityEngine.Light>().transform.forward;
        }
        //Debug.Log(light.type + " : " + (int)light.type);
        //bool isSpot = light.lightType == LightType.Spot;
        return new Light()
        {
            position = light.transform.position,
            direction = -light.transform.forward,
            lightType = (int)light.type,
            color = (Vector3)(Vector4)light.color * light.intensity,
            range = light.range,
            spotAngle = light.spotAngle * Mathf.Deg2Rad * 0.5f
        };
    }

    void InitLightSVO ()
    {
        vlc = Resources.Load<ComputeShader>("LightSVO");

        //init light svo data
        int svoStride = sizeof(float) * 3 + sizeof(int) * (3 + 8);
        svo = new ComputeBuffer(maxNodes, svoStride,
                ComputeBufferType.Counter);
        lightList = new ComputeBuffer(maxNodes * maxLightsPerNode / 1000, sizeof(ushort),
                ComputeBufferType.Counter);
        tempNodes = new ComputeBuffer(maxNodes * 10, svoStride,
            ComputeBufferType.Append);

        lightCullList = new ComputeBuffer(maxNodes * maxLightsPerNode / 80, sizeof(int));
        maxLightsAtDepth = new ComputeBuffer(1, sizeof(int));

        //0 -> num nodes at previous depth, 1 -> numTempNodes, 2 -> numNewNodesAtDepth,
        //3, 4, 5-> dispatch args (0), 6, 7, 8 -> dispatch args (1)
        args = new ComputeBuffer(9, sizeof(int), ComputeBufferType.IndirectArguments);
        count = new ComputeBuffer(3, sizeof(int));

        //Debug.Log("SVO Size: " + (maxNodes * svoStride / 1e6f) + " mb");
        //Debug.Log("Light List: " + (maxNodes * maxLightsPerNode / 1e6f * sizeof(ushort) / 1000) + " mb");

        Shader.SetGlobalInt("MAX_LIGHT_SVO_DEPTH", maxDepth);
        Shader.SetGlobalBuffer("Light_SVO", svo);
        Shader.SetGlobalBuffer("CULLED_LIGHTS", lightList);
    }

    void UpdateLightSVO()
    {

        SVONodeStruct root = new SVONodeStruct();
        root.min = (-Vector3.one * volumeRootSize * 0.5f);
        root.data = 0;
        root.numLights = LightManager.numLights;
        root.lightOffset = 1;

        uint numInitialEntries = (uint)Mathf.CeilToInt(LightManager.numLights * 0.5f + 1);
        uint[] lightListInput = new uint[LightManager.numLights + 1];
        lightListInput[0] = numInitialEntries;
        for (int i = 0; i < LightManager.numLights; i++)
        {
            uint idx = (uint)i;
            lightListInput[i / 2 + 1] |= ((idx & 1) == 0 ? idx : idx << 16);
        }

        Shader.SetGlobalFloat("_VolumeSize", volumeRootSize);

        buffer.SetBufferData(svo, new SVONodeStruct[] { root });
        buffer.SetBufferCounterValue(svo, 1);

        buffer.SetBufferData(args, new uint[] { 1, 0, 0, 1, 1, 1, 1, 1, 1 });
        buffer.SetBufferData(lightList, lightListInput);
        buffer.SetBufferData(count, new uint[] { 0, 0 });

        buffer.SetComputeBufferParam(vlc, 0, "COUNT", count);
        buffer.SetComputeBufferParam(vlc, 0, "SVO", svo);
        buffer.SetComputeBufferParam(vlc, 0, "TempNodes_Append", tempNodes);
        buffer.SetComputeBufferParam(vlc, 0, "ARGS", args);
        buffer.SetComputeBufferParam(vlc, 0, "MAX_LIGHTS_AT_DEPTH", maxLightsAtDepth);

        buffer.SetComputeBufferParam(vlc, 1, "SVO", svo);
        buffer.SetComputeBufferParam(vlc, 1, "ARGS", args);
        buffer.SetComputeBufferParam(vlc, 1, "TempNodes", tempNodes);
        buffer.SetComputeBufferParam(vlc, 1, "LightCullListData", lightCullList);
        buffer.SetComputeBufferParam(vlc, 1, "LightList", lightList);
        buffer.SetComputeBufferParam(vlc, 1, "MAX_LIGHTS_AT_DEPTH", maxLightsAtDepth);

        buffer.SetComputeBufferParam(vlc, 2, "SVO", svo);
        buffer.SetComputeBufferParam(vlc, 2, "ARGS", args);
        buffer.SetComputeBufferParam(vlc, 2, "TempNodes", tempNodes);
        // buffer.SetComputeBufferParam(vlc, 2, "LightCullBitmask", lightCullBitmask);
        buffer.SetComputeBufferParam(vlc, 2, "LightCullListData", lightCullList);
        buffer.SetComputeBufferParam(vlc, 2, "LightList", lightList);
        buffer.SetComputeBufferParam(vlc, 2, "MAX_LIGHTS_AT_DEPTH", maxLightsAtDepth);

        buffer.SetComputeBufferParam(vlc, 3, "ARGS", args);
        buffer.SetComputeBufferParam(vlc, 3, "MAX_LIGHTS_AT_DEPTH", maxLightsAtDepth);

        buffer.SetComputeBufferParam(vlc, 4, "COUNT", count);
        buffer.SetComputeBufferParam(vlc, 4, "ARGS", args);

        for (int i = 1; i < maxDepth; i++)
        {
            buffer.SetBufferCounterValue(tempNodes, 0);
            float nodeSize = (volumeRootSize / (1 << i));
            buffer.SetComputeFloatParam(vlc, "_NodeSize", nodeSize);
            buffer.SetComputeFloatParam(vlc, "_BoundingSphereRadius", (Vector3.one * nodeSize * 0.5f).magnitude);
            buffer.SetComputeIntParam(vlc, "_Depth", i);
            buffer.SetComputeIntParam(vlc, "_ReachedMaxDepth", i == maxDepth - 1 ? 1 : 0);

            buffer.SetBufferData(maxLightsAtDepth, new int[] { 0 });
            buffer.DispatchCompute(vlc, 0, args, sizeof(int) * 3);
            buffer.CopyCounterValue(tempNodes, args, sizeof(int) * 1);
            buffer.DispatchCompute(vlc, 3, 1, 1, 1);
            buffer.DispatchCompute(vlc, 1, args, sizeof(int) * 3);
            buffer.DispatchCompute(vlc, 2, args, sizeof(int) * 6);
            buffer.CopyCounterValue(svo, args, sizeof(int) * 2);
            buffer.DispatchCompute(vlc, 4, 1, 1, 1);
        }
        buffer.CopyCounterValue(svo, count, sizeof(int));
        buffer.CopyCounterValue(tempNodes, count, sizeof(int) * 2);

        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        //Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Background);
    }

    public void Cleanup()
    {
        lightDataBuffer.Dispose();
        dirLightDataBuffer.Dispose();
        lightList.Dispose();

        svo.Dispose();
        args.Dispose();
        count.Dispose();
        lightList.Dispose();
        lightCullList.Dispose();
        tempNodes.Dispose();
    }
}