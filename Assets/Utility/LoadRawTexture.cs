using UnityEngine;
using System.Collections;
using System.IO;

static public class LoadRawTexture
{

	static public void WriteIntoTexture2D(Texture2D tex, int channels, string path)
	{

		if(tex == null)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - RenderTexture is null");
			return;
		}

		if(channels < 1 || channels > 4)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}

		int w = tex.width;
		int h = tex.height;
		int size = w*h*channels;

		float[] data = new float[size];

		if(!LoadRawFile(path, size, data))
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Error loading raw file " + path);
			return;
		}

		Color[] colors = new Color[w*h];
		Color newColor = new Color ();
		int c = channels;

		for(int x = 0; x < w; x++)
		{
			for(int y = 0; y < h; y++)
			{

				newColor.r = c > 0 ? data [(x + y * w) * c + 0] : 0;
				newColor.g = c > 1 ? data [(x + y * w) * c + 1] : 0;
				newColor.b = c > 2 ? data [(x + y * w) * c + 2] : 0;
				newColor.a = c > 3 ? data [(x + y * w) * c + 3] : 0;

				colors [x + y * w] = newColor;

			}
		}
			
		tex.SetPixels(colors);
		tex.Apply();

	}

	static public void WriteIntoTexture3D(Texture3D tex, int channels, string path)
	{

		if(tex == null)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture- RenderTexture is null");
			return;
		}

		if(channels < 1 || channels > 4)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}

		int d = tex.depth;
		int w = tex.width;
		int h = tex.height * d;

		int size = w*h*channels;

		float[] data = new float[size];

		if(!LoadRawFile(path, size, data))
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Error loading raw file " + path);
			return;
		}

		Color[] colors = new Color[w*h];
		Color newColor = new Color ();
		int c = channels;

		for(int x = 0; x < w; x++)
		{
			for(int y = 0; y < h; y++)
			{

				newColor.r = c > 0 ? data [(x + y * w) * c + 0] : 0;
				newColor.g = c > 1 ? data [(x + y * w) * c + 1] : 0;
				newColor.b = c > 2 ? data [(x + y * w) * c + 2] : 0;
				newColor.a = c > 3 ? data [(x + y * w) * c + 3] : 0;

				colors[x + y * w] = newColor;

			}
		}
			
		tex.SetPixels(colors);
		tex.Apply();

	}

	static public void WriteIntoTexture3DAlpha(Texture3D tex, int channels, string path)
	{

		if(tex == null)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - RenderTexture is null");
			return;
		}

		if(channels < 1 || channels > 4)
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}

		int d = tex.depth;
		int w = tex.width;
		int h = tex.height * d;

		int size = w*h*channels;

		float[] data = new float[size];

		if(!LoadRawFile(path, size, data))
		{
			Debug.Log("EncodeFloat::WriteIntoRenderTexture - Error loading raw file " + path);
			return;
		}

		Color[] colors = new Color[w*h];
		Color newColor = new Color ();
		int c = channels;

		for(int x = 0; x < w; x++)
		{
			for(int y = 0; y < h; y++)
			{

				newColor.r = 0;
				newColor.g = 0;
				newColor.b = 0;
				newColor.a = c > 0 ? data [(x + y * w) * c + 0] : 0;

				colors[x + y * w] = newColor;

			}
				
		}

		tex.SetPixels(colors);
		tex.Apply();

	}
		
	static public bool LoadRawFile(string path, int size, float[] fdata) 
	{	
		FileInfo fi = new FileInfo(path);

		if(fi == null)
		{
			Debug.Log("EncodeFloat::LoadRawFile - Raw file not found");
			return false;
		}

		FileStream fs = fi.OpenRead();

		byte[] data = new byte[fi.Length];
		fs.Read(data, 0, (int)fi.Length);
		fs.Close();

		//divide by 4 as there are 4 bytes in a 32 bit float
		if(size > fi.Length/4)
		{
			Debug.Log("EncodeFloat::LoadRawFile - Raw file is not the required size");
			return false;
		}

		int i = 0;
		for(int x = 0 ; x < size; x++) {
			
			//Convert 4 bytes to 1 32 bit float
			fdata[x] = System.BitConverter.ToSingle(data, i);

			i += 4; // theres 4 bytes in 32 bits so increment i by 4

		};

		return true;
	}

}
