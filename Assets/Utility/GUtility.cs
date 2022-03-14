using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GUtility {

	static Material blitCopyMip, blitCopyBasic, blitCopySlice, accumulate;

	public static void Init () {
        blitCopyMip = new Material(Shader.Find("Hidden/BlitCopyMip"));
        //blitCopyBasic = new Material (Shader.Find ("Hidden/BlitCopyBasic"));
		//blitCopySlice = new Material (Shader.Find ("Hidden/BlitCopySlice"));
		//accumulate = new Material (Shader.Find ("Utility/Accumulate"));
	}

    public static void Cleanup ()
    {
        Object.DestroyImmediate(blitCopyMip);
    }

	//textures
	public static void ClearColor(RenderTexture[] texs) {
		
		for(int i = 0; i < texs.Length; i++) {
			Graphics.SetRenderTarget(texs[i]);
			GL.Clear(false, true, Color.clear);
		}
	}

	public static void ClearStencil(RenderTexture tex) {
		
		Graphics.SetRenderTarget (tex);
		Graphics.Blit (tex, tex, new Material (Shader.Find ("Hidden/ClearStencil")));

	}

	public static void Clear(RenderTexture tex) {

		Graphics.SetRenderTarget (tex);
		GL.Clear (true, true, Color.clear);

	}

	public static void Clear(RenderTexture tex, Color col) {

		Graphics.SetRenderTarget (tex);
		GL.Clear (true, true, col);

	}

	public static void Swap(RenderTexture[] texs)
	{
		RenderTexture temp = texs[0];	
		texs[0] = texs[1];
		texs[1] = temp;
	}

	public static void Blit (RenderTexture tex, Material mat, int depthSlice = 0, int pass = 0){

		Graphics.SetRenderTarget (tex, 0, CubemapFace.Unknown, depthSlice);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		mat.SetPass (pass);

        DrawQuad();
        GL.PopMatrix ();

	}

    public static void Blit(RenderTexture[] tex, Material mat, int pass = 0, int depthSlice = 0, int mipLevel = 0)
    {
        RenderTexture temp = RenderTexture.active;

        RenderBuffer[] colorBuffers = new RenderBuffer[tex.Length];
        for (int i = 0; i < tex.Length; i++)
        {
            colorBuffers[i] = tex[i].colorBuffer;
        }

        RenderTargetSetup setup = new RenderTargetSetup(colorBuffers, tex[0].depthBuffer, mipLevel);
        setup.depthSlice = depthSlice;

        Graphics.SetRenderTarget(setup);

        GL.PushMatrix();
        GL.LoadOrtho();

        mat.SetPass(pass);

        DrawQuad();
        GL.PopMatrix();

        RenderTexture.active = temp; //crucial if using a compute shader after MRT rendering
    }

    public static void Blit (RenderTexture[] tex, RenderTexture depth, Material mat, int pass = 0){

		RenderBuffer[] colorBuffers = new RenderBuffer[tex.Length];
		RenderBuffer depthBuffer = depth.depthBuffer;

		for (int i = 0; i < tex.Length; i++) {

			colorBuffers [i] = tex [i].colorBuffer;

		}

		Graphics.SetRenderTarget (colorBuffers, depthBuffer);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		if (pass >= 0) {

			mat.SetPass (pass);
			DrawQuad ();

		} else if (pass == -1) {

			for (int i = 0; i < mat.passCount; i++) {

				mat.SetPass (i);
				DrawQuad ();

			}

		}

		GL.PopMatrix ();

	}

	//public static void Blit (RenderTexture color, RenderBuffer depth, Material mat, int pass = 0, int ignorePass = -1){

	//	mat.SetTexture ("_MainTex", color);
	//	Graphics.SetRenderTarget (color.colorBuffer, depth);

	//	GL.PushMatrix ();
	//	GL.LoadOrtho ();

	//	if (pass >= 0) {

	//		mat.SetPass (pass);
	//		DrawQuad ();

	//	} else if (pass == -1) {

	//		for (int i = 0; i < mat.passCount; i++) {

	//			if (ignorePass != -1 && i == ignorePass)
	//				continue;
					
	//			mat.SetPass (i);
	//			DrawQuad ();

	//		}

	//	}

	//	GL.PopMatrix ();

	//}

	public static void Accum (RenderTexture src, RenderTexture dst, int pass = 0){

		accumulate.SetTexture ("_Source", src);
		accumulate.SetTexture ("_Dest", dst);
		Graphics.SetRenderTarget (dst);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		if (pass >= 0) {

			accumulate.SetPass (pass);
			DrawQuad ();

		}

		GL.PopMatrix ();

	}

	public static void MeshBlit (RenderBuffer color, RenderBuffer depth, Material mat, Mesh mesh, Matrix4x4 matrix, int pass = 0){

		Graphics.SetRenderTarget (color, depth);

		if (pass >= 0) {

			mat.SetPass (pass);
			Graphics.DrawMeshNow (mesh, matrix);

		} else if (pass == -1) {

			for (int i = 0; i < mat.passCount; i++) {

				mat.SetPass (i);
				Graphics.DrawMeshNow (mesh, matrix);

			}

		}

	}

	public static void CubeBlit (RenderTexture tex, Material mat, int face = 0, int mipLevel = 0, int pass = 0){

		Graphics.SetRenderTarget (tex, mipLevel, (CubemapFace)face);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		mat.SetPass (pass);
		DrawQuad ();

		GL.PopMatrix ();

	}

	public static void CopyBlit (Texture a, RenderTexture b, int depthSlice = 0){

		blitCopyBasic.SetTexture ("_MainTex", a);
		Graphics.SetRenderTarget (b, 0, CubemapFace.Unknown, depthSlice);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		blitCopyBasic.SetPass (0);
		DrawQuad ();

		GL.PopMatrix ();

	}

	public static void CopyBlitSlice (RenderTexture a, RenderTexture b, int depthSlice = 0){

		blitCopySlice.SetTexture ("_MainTex", a);
		blitCopySlice.SetInt ("layer", depthSlice);
		Graphics.SetRenderTarget (b);

		GL.PushMatrix ();
		GL.LoadOrtho ();

		blitCopySlice.SetPass (0);
		DrawQuad ();

		GL.PopMatrix ();

	}

    public static void CopyBlitMip(RenderTexture[] tex, int index, int srcMip, int dstMip)
    {
        RenderTexture temp = RenderTexture.active;

        RenderBuffer[] colorBuffers = new RenderBuffer[tex.Length];
        for (int i = 0; i < tex.Length; i++)
        {
            colorBuffers[i] = tex[i].colorBuffer;
        }

        blitCopyMip.SetTexture("_MainTex1", tex[0]);
        blitCopyMip.SetTexture("_MainTex2", tex[1]);
        blitCopyMip.SetTexture("_MainTex3", tex[2]);

        RenderTargetSetup setup = new RenderTargetSetup(colorBuffers, tex[0].depthBuffer, dstMip);
        setup.depthSlice = index;
        Graphics.SetRenderTarget(setup);

        blitCopyMip.SetInt("_SrcIndex", index);
        blitCopyMip.SetInt("_SrcMip", srcMip);
        GL.PushMatrix();
        GL.LoadOrtho();

        blitCopyMip.SetPass(0);

        DrawQuad();
        GL.PopMatrix();

        RenderTexture.active = temp;
    }

    public static void VolumeBlit (RenderTexture tex, int depth, Material mat) {

		GL.PushMatrix ();
		GL.LoadOrtho ();

		RenderTargetSetup setup = new RenderTargetSetup (tex.colorBuffer, tex.depthBuffer, 0, CubemapFace.Unknown);

		for (int i = 0; i < depth; i++) {

			setup.depthSlice = i;
			Graphics.SetRenderTarget (setup);

			mat.SetInt ("layer", i);
			mat.SetPass (0);
			DrawQuad ();

		}

		GL.PopMatrix ();

	}

	public static void VolumeBlit (RenderTexture tex0, RenderTexture tex1, int depth, Material mat) {

		GL.PushMatrix ();
		GL.LoadOrtho ();

		RenderBuffer[] colorBuffers = new RenderBuffer[2];
		colorBuffers [0] = tex0.colorBuffer;
		colorBuffers [1] = tex1.colorBuffer;
		RenderTargetSetup setup = new RenderTargetSetup (colorBuffers, tex0.depthBuffer, 0, CubemapFace.Unknown);

		for (int i = 0; i < depth; i++) {

			setup.depthSlice = i;
			Graphics.SetRenderTarget (setup);

			mat.SetInt ("layer", i);
			mat.SetPass (0);
			DrawQuad ();

		}

		GL.PopMatrix ();

	}

	//global parameters
	public static void UpdateFrustumCorners (Camera cam) {

		Matrix4x4 frustumCornersMatrix = Matrix4x4.identity;
		Matrix4x4 vsFrustumCornersMatrix = Matrix4x4.identity;

		float CAMERA_NEAR = cam.nearClipPlane;
		float CAMERA_FAR = cam.farClipPlane;
		float CAMERA_FOV = cam.fieldOfView;
		float CAMERA_ASPECT_RATIO = cam.aspect;

		float fovWHalf = CAMERA_FOV * 0.5f;

		//		Vector3 forward = cam.transform.forward;
		//		Vector3 right = cam.transform.right;
		//		Vector3 up = cam.transform.up;

		Vector3 forward = cam.cameraToWorldMatrix * -Vector3.forward;
		Vector3 right = cam.cameraToWorldMatrix * Vector3.right;
		Vector3 up = cam.cameraToWorldMatrix * Vector3.up;

		Vector3 toRight = right * CAMERA_NEAR * Mathf.Tan (fovWHalf * Mathf.Deg2Rad) * CAMERA_ASPECT_RATIO;
		Vector3 toTop = up * CAMERA_NEAR * Mathf.Tan (fovWHalf * Mathf.Deg2Rad);

		Vector3 topLeft = (forward * CAMERA_NEAR - toRight + toTop);
		float CAMERA_SCALE = topLeft.magnitude * CAMERA_FAR/CAMERA_NEAR;

		topLeft.Normalize();

		Vector3 topRight = (forward * CAMERA_NEAR + toRight + toTop);
		topRight.Normalize();

		Vector3 bottomRight = (forward * CAMERA_NEAR + toRight - toTop);
		bottomRight.Normalize();

		Vector3 bottomLeft = (forward * CAMERA_NEAR - toRight - toTop);
		bottomLeft.Normalize();

		topLeft *= CAMERA_SCALE;
		topRight *= CAMERA_SCALE;
		bottomRight *= CAMERA_SCALE;
		bottomLeft *= CAMERA_SCALE;

		frustumCornersMatrix.SetRow (0, topLeft); 
		frustumCornersMatrix.SetRow (1, topRight);		
		frustumCornersMatrix.SetRow (2, bottomRight);
		frustumCornersMatrix.SetRow (3, bottomLeft);

		vsFrustumCornersMatrix.SetRow (0, cam.worldToCameraMatrix * topLeft); 
		vsFrustumCornersMatrix.SetRow (1, cam.worldToCameraMatrix * topRight);		
		vsFrustumCornersMatrix.SetRow (2, cam.worldToCameraMatrix * bottomRight);
		vsFrustumCornersMatrix.SetRow (3, cam.worldToCameraMatrix * bottomLeft);

		Shader.SetGlobalMatrix ("_FrustumCorners", frustumCornersMatrix);
		Shader.SetGlobalMatrix ("_VSFrustumCorners", vsFrustumCornersMatrix);
		Shader.SetGlobalVector ("_FrustumX", topRight - topLeft);
		Shader.SetGlobalVector ("_FrustumY", bottomLeft - topLeft);
		Shader.SetGlobalVector ("_FrustumOffset", topLeft);

	}

	static void DrawQuad () {
        GL.Begin(GL.QUADS);

        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 0.1f);

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 0.1f);

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 0.1f);

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.1f);

        GL.End();
    }

	public static void ReadPixels3D (Texture3D dest, RenderTexture src) {

		int size = src.width * src.height * src.volumeDepth;
		Texture2D tex = new Texture2D (src.width, src.height * src.volumeDepth, TextureFormat.RGBAFloat, false);

		for (int i = 0; i < src.volumeDepth; i++) {

			Graphics.SetRenderTarget (src, 0, CubemapFace.Unknown, i);
			tex.ReadPixels (new Rect (0, 0, Screen.width, Screen.height), 0, src.height * i);

		}
			
		Color[] colors = tex.GetPixels ();
		dest.SetPixels (colors);
		dest.Apply ();

	}

	public static Vector4[] ReadPixels3D (RenderTexture src) {

		int size = src.width * src.height * src.volumeDepth;
		Texture2D tex = new Texture2D (src.width, src.height * src.volumeDepth, TextureFormat.RGBAFloat, false);

		for (int i = 0; i < src.volumeDepth; i++) {

			Graphics.SetRenderTarget (src, 0, CubemapFace.Unknown, i);
			tex.ReadPixels (new Rect (0, 0, Screen.width, Screen.height), 0, src.height * i);

		}

		Color[] colors = tex.GetPixels ();
		Vector4[] texels = new Vector4[colors.Length];

		for (int i = 0; i < colors.Length; i++) {

			texels [i] = (Vector4)colors [i];

		}

		return texels;

	}

	public static void SaveAsRaw2D(int width, int height, int channels, string filePath, string fileName, RenderTexture rtex) {

		int size = width * height;
		float[] data = new float[size * channels];
		Texture2D tex = new Texture2D (width, height, TextureFormat.RGBAFloat, false);

		Graphics.SetRenderTarget(rtex);
		tex.ReadPixels (new Rect (0, 0, Screen.width, Screen.height), 0, 0);

		Color[] colors = tex.GetPixels ();

		for(int x = 0; x < width; x++){

			for (int y = 0; y < height; y++) {

				for (int i = 0; i < channels; i++) {

					data [(x+y*width)*channels + i] = colors [(x+y*width)] [i];

				}

			}

		}

		byte[] byteArray = new byte[size * 4 * channels];
		System.Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);
		System.IO.File.WriteAllBytes(Application.dataPath + filePath + "/" + fileName + ".raw", byteArray);

	}

	public static void SaveAsRaw2D(int width, int height, int channels, string filePath, string fileName, Texture2D tex) {

		int size = width * height;
		float[] data = new float[size * channels];

		Color[] colors = tex.GetPixels ();

		for(int x = 0; x < width; x++){

			for (int y = 0; y < height; y++) {

				for (int i = 0; i < channels; i++) {

					data [(x+y*width)*channels + i] = colors [(x+y*width)] [i];

				}

			}

		}

		byte[] byteArray = new byte[size * 4 * channels];
		System.Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);
		System.IO.File.WriteAllBytes(Application.dataPath + filePath + "/" + fileName + ".raw", byteArray);

	}

	public static void SaveAsRaw3D(int width, int height, int depth, int channels, string filePath, string fileName, RenderTexture rtex) {

		int size = width * height * depth;
		float[] data = new float[size * channels];
		Texture2D tex = new Texture2D (width, height * depth, TextureFormat.RGBAFloat, false);

		for (int i = 0; i < depth; i++) {

			Graphics.SetRenderTarget (rtex, 0, CubemapFace.Unknown, i);
			tex.ReadPixels (new Rect (0, 0, Screen.width, Screen.height), 0, height * i);

		}
			
		Color[] colors = tex.GetPixels ();

		for(int x = 0; x < width; x++){

			for (int y = 0; y < height * depth; y++) {

				for (int i = 0; i < channels; i++) {

					data [(y+x*height*depth)*channels + i] = colors [(y+x*height*depth)] [i];

				}

			}

		}

		byte[] byteArray = new byte[size * 4 * channels];
		System.Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);
		System.IO.File.WriteAllBytes(Application.dataPath + filePath + "/" + fileName + ".raw", byteArray);

	}

}
