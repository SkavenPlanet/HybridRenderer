using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatedEmission : MonoBehaviour
{
    Material animatedTex;
    RenderTexture tex;
    // Start is called before the first frame update
    void Start()
    {
        animatedTex = new Material(Shader.Find("Hidden/SynthwaveTexture"));
        tex = new RenderTexture(1024, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        GetComponent<Renderer>().material.SetTexture("_EmissionMap", tex);
    }

    private void OnDestroy()
    {
        tex.Release();
    }

    // Update is called once per frame
    void Update()
    {
        Graphics.Blit(tex, tex, animatedTex);
    }
}
