using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("MapGenerator", "Ryamkk", "1.0.1")]
	public class MapGenerator : RustPlugin
	{
		public static int MapResolution = 2048;
		public string MapFileName => $"Map.png";
		public string MaskName => $"mask.png";
		
		private void OnServerInitialized()
		{
			if (!Interface.Oxide.DataFileSystem.GetFiles(Name).ToList().Any(p => p.Contains(MapFileName)))
			{
				Image maskfile = Image.FromFile(Interface.Oxide.DataDirectory + "/" + Name + "/" + MaskName);
				
				var map = GenerateMap(maskfile, MapResolution);
				byte[] maparr = map.EncodeToPNG();
				
				var image = Image.FromStream(new MemoryStream(maparr));
				
				ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Png);
				Encoder myEncoder = Encoder.Quality;
					
				EncoderParameters myEncoderParameters = new EncoderParameters(1);  
  
				EncoderParameter myEncoderParameter1 = new EncoderParameter(myEncoder, 500L);  
				myEncoderParameters.Param[0] = myEncoderParameter1;  
				image.Save(Interface.Oxide.DataDirectory + "/" + Name + "/" + "TestQualityFifty.png", jpgEncoder, myEncoderParameters);  
  
				EncoderParameter myEncoderParameter2 = new EncoderParameter(myEncoder, 1000);  
				myEncoderParameters.Param[0] = myEncoderParameter2;  
				image.Save(Interface.Oxide.DataDirectory + "/" + Name + "/" + "TestQualityHundred.png", jpgEncoder, myEncoderParameters);  
					
				EncoderParameter myEncoderParameter3 = new EncoderParameter(myEncoder, 100L);  
				myEncoderParameters.Param[0] = myEncoderParameter3;  
				image.Save(Interface.Oxide.DataDirectory + "/" + Name + "/" + "TestQualityZero.png", jpgEncoder, myEncoderParameters);
					
				image.Save(Interface.Oxide.DataDirectory + "/" + Name + "/" + MapFileName, ImageFormat.Png);
			}
		}
		
		private ImageCodecInfo GetEncoder(ImageFormat format)  
		{  
			ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();  
			foreach (ImageCodecInfo codec in codecs)  
			{  
				if (codec.FormatID == format.Guid)  
				{  
					return codec;  
				}  
			}  
			return null;  
		}  
		
		private static Vector3 StartColor { get; } = new Vector3(0.286274523f, 0.270588249f, 0.247058839f);
        private static Vector4 WaterColor { get; } = new Vector4(0.16941601f, 0.317557573f, 0.362000018f, 1f);
        private static Vector4 GravelColor { get; } = new Vector4(0.25f, 0.243421048f, 0.220394745f, 1f);
        private static Vector4 DirtColor { get; } = new Vector4(0.6f, 0.479594618f, 0.33f, 1f);
        private static Vector4 SandColor { get; } = new Vector4(0.7f, 0.65968585f, 0.5277487f, 1f);
        private static Vector4 GrassColor { get; } = new Vector4(0.354863644f, 0.37f, 0.2035f, 1f);
        private static Vector4 ForestColor { get; } = new Vector4(0.248437509f, 0.3f, 0.0703125f, 1f);
        private static Vector4 RockColor { get; } = new Vector4(0.4f, 0.393798441f, 0.375193775f, 1f);
        private static Vector4 SnowColor { get; } = new Vector4(0.862745166f, 0.9294118f, 0.941176534f, 1f);
        private static Vector4 PebbleColor { get; } = new Vector4(0.137254909f, 0.2784314f, 0.2761563f, 1f);
        private static Vector4 OffShoreColor { get; } = new Vector4(0.04090196f, 0.220600322f, 0.274509817f, 1f);

        private static Vector3 SunDirection { get; } = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f));
        
        private static Terrain terrain;
        private static TerrainHeightMap terrainHeightMap;
        private static TerrainSplatMap terrainSplatMap;
        
        private static Texture2D GenerateMap(Image maskimg = null, int resolution = 500, float padding = 0.15f)
        {
            terrain = TerrainMeta.Terrain;
            terrainHeightMap = TerrainMeta.HeightMap;
            terrainSplatMap = TerrainMeta.SplatMap;
            
            float pad = resolution * padding;
            float scale = (World.Size / (resolution - pad)) / World.Size;
            Texture2D map = new Texture2D(resolution, resolution);

            Texture2D mask = new Texture2D(1, 1);
            if (maskimg != null)
            {
                MemoryStream ms = new MemoryStream();
                maskimg.Save(ms, ImageFormat.Png);
                mask.LoadImage(ms.ToArray());
            }

            UnityEngine.Parallel.For(0, resolution, (x) =>
            {
                for (int z = 0; z < resolution; z++)
                {
                    float _x = (x - pad / 2) * scale;
                    float _z = (z - pad / 2) * scale;

                    float height = terrainHeightMap.GetHeight(_x, _z);
                    float illumination = Math.Max(Vector3.Dot(terrainHeightMap.GetNormal(_x, _z), SunDirection), 0.0f);
                    Vector3 color = StartColor;
                    color = Vector3.Lerp(color, DirtColor, terrainSplatMap.GetSplat(_x, _z, 1) * DirtColor.w);
                    color = Vector3.Lerp(color, SnowColor, terrainSplatMap.GetSplat(_x, _z, 2) * SnowColor.w);
                    color = Vector3.Lerp(color, SandColor, terrainSplatMap.GetSplat(_x, _z, 4) * SandColor.w);
                    color = Vector3.Lerp(color, RockColor, terrainSplatMap.GetSplat(_x, _z, 8) * RockColor.w);
                    color = Vector3.Lerp(color, GrassColor, terrainSplatMap.GetSplat(_x, _z, 16) * GrassColor.w);
                    color = Vector3.Lerp(color, ForestColor, terrainSplatMap.GetSplat(_x, _z, 32) * ForestColor.w);
                    color = Vector3.Lerp(color, PebbleColor, terrainSplatMap.GetSplat(_x, _z, 64) * PebbleColor.w);
                    color = Vector3.Lerp(color, GravelColor, terrainSplatMap.GetSplat(_x, _z, 128) * GravelColor.w);
                    float dh = 0f - height;
                    if (dh >= 0.0f)
                    {
                        color = Vector3.Lerp(color, WaterColor, Clamp(0.5f + dh / 5f, 0f, 1f));
                        color = Vector3.Lerp(color, OffShoreColor, Clamp(dh / 50f, 0f, 1f));
                        illumination = 0.5f;
                    }
                    color += (illumination - 0.5f) * 0.65f * color;
                    color = color * 0.94f + new Vector3(0.03f, 0.03f, 0.03f);
                    color *= 1.05f;

                    float a = 1f;
                    if (maskimg != null)
                    {
                        int __x = (int)((float)x / resolution * mask.width);
                        int __z = (int)((float)z / resolution * mask.height);
                        a = (mask).GetPixel(__x, __z).a;
                    }
                    map.SetPixel(x, z, new UnityEngine.Color(color.x, color.y, color.z, a));
                }
            });
            
            return map;
        } 
        
        private static float Clamp(float val, float min, float max)
        {
	        if (val > max) return max;
	        if (val < min) return min;
	        return val;
        }
	}
}