// Reference: System.Drawing
using System.IO;
using Oxide.Core;
using UnityEngine;
using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins 
{
	[Info("MapGenerator", "", "1.0.1")]
	[Description("Simple map generator")]
	
	class MapGenerator : RustPlugin 
	{
		#region Config

		private ConfigData configData;

		class ConfigData {
			[JsonProperty(PropertyName = "ОСНОВНЫЕ НАСТРОЙКИ")]
			public SettingOptions OptionsSetting { get; set; }

			public class SettingOptions {
				[JsonProperty(PropertyName = "Имя генерируемого изображения карты |без расширения .jpg|")]
				public string filename;

				[JsonProperty(PropertyName = "Автоматическая генерация нового изображения карты после вайпа")]
				public bool AutoMap;

				[JsonProperty(PropertyName = "Размер изображения для генерации карты |0 - стандартный размер|")]
				public int autosize;

				[JsonProperty(PropertyName = "Расширение генерируемого изображения карты |jpg или png|")]
				public string type;
			}
		}

		private ConfigData GetBaseConfig() {
			return new ConfigData {
				OptionsSetting = new ConfigData.SettingOptions {
					filename = "Map",
					AutoMap  = false,
					autosize = 0,
					type     = "jpg"
				}
			};
		}

		protected override void LoadConfig() {
			base.LoadConfig();

			try {
				configData = Config.ReadObject<ConfigData>();
				if (configData == null) Regenerate();
			} catch {
				Regenerate();
			}

			UpdateConfigValues();
			Config.WriteObject(configData, true);

			SaveConfig();
		}

		protected override void LoadDefaultConfig() {
			configData = GetBaseConfig();
			PrintWarning("Создан новый файл конфигурации.");
		}

		protected override void SaveConfig() => Config.WriteObject(configData, true);

		private void Regenerate() {
			PrintWarning($"Конфигурационный файл 'oxide/config/{Name}.json' поврежден, создается новый...");
			LoadDefaultConfig();
		}

		private void UpdateConfigValues() {
			configData = GetBaseConfig();
		}

		#endregion

		#region Properties

		private Terrain terrain;
		private bool NewWipe;

		#endregion
		#region Commands

		[ConsoleCommand("savemap")]
		private void SaveMapCMD(ConsoleSystem.Arg arg) {
			if (!arg.isAdmin) return;

			var size     = 0;
			var filetype = ".jpg";

			if (arg.HasArgs()) {
				if (!Int32.TryParse(arg.Args[0], out size))
					PrintWarning("Неправильно указан размер изображения. Установлено стандартное значение (размер карты / 2)");
				if (arg.Args.Length > 1 && arg.Args[1] == "jpg") filetype = ".jpg";
			}

			GenerateMap(size, filetype);
		}

		#endregion
		#region MapGenerator

		public class ColorRet {
			public Color color;
			public bool useHeight;

			public ColorRet(float r = 0f, float g = 0f, float b = 0f, float a = 1f, bool useHeight = false) {
				this.color     = new Color(r, g, b, a);
				this.useHeight = useHeight;
			}
		}

		Dictionary<string, ColorRet> raycastColors = new Dictionary<string, ColorRet> {
			{"train_track", new ColorRet(0.26f, 0.27f, 0.29f, 0.67f)},
			{"road_nopav", new ColorRet(0.26f, 0.27f, 0.29f, 0.67f)},
			{"road", new ColorRet(0.29f, 0.11f, 0.07f, 0.65f)},
			{"iceberg", new ColorRet(0.82f, 0.89f, 1f, 0.83f)},
			{"ice_lake", new ColorRet(0.82f, 0.89f, 1f, 0.83f)},
			{"collider_batch", new ColorRet(0.73f, 0.73f, 0.7f, 0.82f)},
			{"river", new ColorRet(0f, 0.27f, 0.65f, 0.83f)},
			{"dish_radio", new ColorRet(0.87f, 0.84f, 0.78f, 0.83f)},
			{"tunnel.single", new ColorRet(0.47f, 0.47f, 0.47f, 0.55f)},
			{"train_wagon", new ColorRet(0.65f, 0.53f, 0.53f, 0.55f)},
			{"bus_stop", new ColorRet(0.4f, 0.26f, 0.16f, 0.55f)},
			{"sewer_chim", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"sewer_drain", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"large_industrial", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"loading_bay_lane", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"harbor", new ColorRet(0.46f, 0.37f, 0.25f, 0.78f)},
			{"shipping_container", new ColorRet(0.46f, 0.37f, 0.25f, 0.78f)},
			{"crane_tower", new ColorRet(0.46f, 0.37f, 0.25f, 0.78f)},
			{"fuel_tank", new ColorRet(0.46f, 0.37f, 0.25f, 0.78f)},
			{"trailer", new ColorRet(0.46f, 0.37f, 0.25f, 0.78f)},
			{"rock_quarry", new ColorRet(0.36f, 0.33f, 0.31f, 0.95f)},
			{"water_body", new ColorRet(0.1f, 0.3f, 0.07f, 0.95f)},
			{"wooden_walkway", new ColorRet(0.69f, 0.63f, 0.54f, 0.95f)},
			{"cover", new ColorRet(0.56f, 0.51f, 0.42f, 0.95f)},
			{"windmill", new ColorRet(0.56f, 0.51f, 0.42f, 0.95f)},
			{"dredge_body", new ColorRet(0.61f, 0.48f, 0.47f, 0.78f)},
			{"launch_site_ground", new ColorRet(0.4f, 0.4f, 0.38f, 0.61f)},
			{"range_ground", new ColorRet(0.44f, 0.4f, 0.38f, 0.64f)},
			{"range_rails", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"floodlights_", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"warehouse_launch", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"space_center", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"rocket_factory", new ColorRet(0.61f, 0.48f, 0.47f, 0.78f)},
			{"runway", new ColorRet(0.51f, 0.51f, 0.49f, 0.55f)},
			{"office_bld", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"watch_tower", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"hangar_air", new ColorRet(0.61f, 0.48f, 0.47f, 0.78f)},
			{"perimeter_wall", new ColorRet(0.83f, 0.76f, 0.75f, 0.7f)},
			{"concrete_slabs", new ColorRet(0.47f, 0.47f, 0.47f, 0.55f)},
			{"outbuilding", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"rowhouse", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)},
			{"sphere", new ColorRet(0.61f, 0.32f, 0.22f)},
			{"coal_pile", new ColorRet(0.16f, 0.16f, 0.16f)},
			{"water_tower", new ColorRet(0.61f, 0.48f, 0.47f, 0.78f)},
			{"pipeline", new ColorRet(0.61f, 0.33f, 0.27f, 0.78f)},
			{"train_crane", new ColorRet(0.5f, 0.42f, 0.36f, 0.73f)}
		};

		public List<string> names = new List<string>();
		public Dictionary<string, ColorRet> junkpiles = new Dictionary<string, ColorRet>();

		private ColorRet CheckRaycast(Vector3 terrainWorldPosition, float highestTerrainHeight) {
			RaycastHit raycastHit;

			var rr = new Vector3(terrainWorldPosition.x, highestTerrainHeight + 10f, terrainWorldPosition.z);

			if (!Physics.Raycast(rr,
				Vector3.down,
				out raycastHit,
				highestTerrainHeight + 10f,
				LayerMask.GetMask("Terrain", "World", "Water"),
				QueryTriggerInteraction.Ignore))
				return new ColorRet();

			var name = raycastHit.collider.gameObject.name.ToLower();
			if (name == "Terrain") return new ColorRet();

			foreach (var raycastColor in raycastColors) {
				if (name.Contains(raycastColor.Key.ToLower())) return raycastColor.Value;
			}

			if (name.Contains("junkyard_mound")) {
				if (!junkpiles.ContainsKey(name)) {
					var r = Random.Range(0f, 1.75f);
					junkpiles.Add(name, new ColorRet(0.32f + r * 0.09f, 0.16f + r * 0.08f, 0.11f + r * 0.01f, 0.6f));
				}

				return junkpiles[name];
			}

			return new ColorRet();
		}

		private void ProcessPixel(Texture2D mapTex, Vector3 terrainPosition, int height, int width, int x, int y, float heightMin, float heightMax) {
			var heightsTex = TerrainMeta.HeightMap.NormalTexture;
			var isTerrain  = false;

			var pos = new Vector3(x * TerrainMeta.Size.x / width + terrainPosition.x, 0, y * TerrainMeta.Size.z / height + terrainPosition.z);

			var waterDepth    = TerrainMeta.WaterMap.GetDepth(pos);
			var waterHeight   = TerrainMeta.WaterMap.GetHeight01(pos) * TerrainMeta.Size.y;
			var currentHeight = TerrainMeta.HeightMap.GetHeight(pos);
			var pixelColor    = TerrainMeta.Colors.GetColor(pos);

			//Над водой
			if (currentHeight - 1.5f >= waterDepth) {
				pixelColor.a =  1f;
				pixelColor.r *= 0.95f;
				pixelColor.g *= 0.95f;
				pixelColor.b *= 0.95f;
				mapTex.SetPixel(x, y, pixelColor);
				isTerrain = true;
			} else {
				var waterColor = new Color(0.24f, 0.56f, 0.71f);
				mapTex.SetPixel(x, y, waterColor);
			}

			if (ConVar.Server.level.ToLower() != "barren" && !isTerrain || isTerrain) {
				var cr = CheckRaycast(pos, heightMax); 

				if (cr.color != Color.black) {
					mapTex.SetPixel(x, y, cr.color);
					if (!cr.useHeight) return;
				}
			}

			var finalTextureColor = mapTex.GetPixel(x, y);

			if (isTerrain) {
				var alphaBlendedColor = AlphaBlend(heightsTex.GetPixel(x, y), finalTextureColor);
				mapTex.SetPixel(x, y, alphaBlendedColor);

				return;
			}

			var waterLevel = (float)(1f - Math.Round(waterDepth / (waterHeight - heightMin), 6)) + 0.05f;

			var waterAlpha = waterLevel / 2f + waterLevel * 0.4f - 0.5f;

			if (waterAlpha <= 0.2f) waterAlpha = 0.2f;
			if (waterAlpha > 0.5f) waterAlpha  = waterAlpha * 1.1f;

			var blendedColor = AlphaBlend(new Color(finalTextureColor.r, finalTextureColor.g, finalTextureColor.b, waterAlpha * 0.8f + 0.1f),
				finalTextureColor);

			mapTex.SetPixel(x, y, blendedColor);
		}

		private void SetContrast(Texture2D finalTexture, double contrastD = 10, int whiteIdx = 180, double notWhiteMod = 0.9, double whiteMod = 1.0) {
			if (contrastD < -100) contrastD = -100;
			if (contrastD > 100) contrastD  = 100;
			contrastD =  (100.0 + contrastD) / 100.0;
			contrastD *= contrastD;
			Color color;

			for (var i = 0; i < finalTexture.width; i++) {
				for (var j = 0; j < finalTexture.height; j++) {
					color = finalTexture.GetPixel(i, j);

					double pR = color.r;
					pR -= 0.5;
					pR *= contrastD;
					pR += 0.5;
					pR *= 255;

					double pG = color.g;
					pG -= 0.5;
					pG *= contrastD * 1.1f;
					pG += 0.5;
					pG *= 255;

					double pB = color.b;
					pB -= 0.5;
					pB *= contrastD;
					pB += 0.5;
					pB *= 255;

					if (pR < 0) pR   = 0;
					if (pR > 255) pR = 255;
					if (pG < 0) pG   = 0;
					if (pG > 255) pG = 255;
					if (pB < 0) pB   = 0;
					if (pB > 255) pB = 255;

					if ((pR > whiteIdx && pG > whiteIdx & pB > whiteIdx)) {
						pR *= whiteMod;
						pG *= whiteMod;
						pB *= whiteMod;
					} else {
						pR *= notWhiteMod;
						pG *= notWhiteMod;
						pB *= notWhiteMod;
					}

					finalTexture.SetPixel(i, j, new Color((float)pR / 255, (float)pG / 255, (float)pB / 255, color.a));
				}
			}
		}

		private void GenerateMap(int size = 0, string filetype = ".jpg") {
			PrintWarning("Создается изображение карты. Сервер может подвиснуть на несколько минут!");
			TerrainMeta.HeightMap.GenerateTextures();
			var width        = TerrainMeta.Terrain.terrainData.heightmapWidth  - 1;
			var height       = TerrainMeta.Terrain.terrainData.heightmapHeight - 1;
			var finalTexture = new Texture2D(width, height);
			terrain = TerrainMeta.Terrain;

			var polarHeights = GetPolarTerrainHeights();

			Puts($"Начало генерации карты ({finalTexture.height * finalTexture.width} пикcелей)");

			var progress        = 0;
			var terrainPosition = TerrainMeta.Terrain.GetPosition();

			for (var y = 0; y < finalTexture.height; y++)
			for (var x = 0; x < finalTexture.width; x++) {
				var cP = Math.Round((float)y / finalTexture.height * 1000f);

				if (cP > progress) {
					progress = progress + 100;
					Puts($"Генерация карты [{progress / 10} %]");
				}

				ProcessPixel(finalTexture, terrainPosition, height, width, x, y, polarHeights.min, polarHeights.max);
			}

			Puts("Контрастность");
			SetContrast(finalTexture);
			Puts("Сохранение");

			finalTexture.Apply();
			SaveIMG(finalTexture, size, filetype);
			UnityEngine.Object.Destroy(finalTexture);
		}

		class PolarHeights {
			public float min;
			public float max;
		}

		private PolarHeights GetPolarTerrainHeights() {
			var   lowestHeight  = TerrainMeta.Size.y;
			float highestHeight = 0;

			for (var x = 0; x < TerrainMeta.Size.x; x++)
			for (var y = 0; y < TerrainMeta.Size.z; y++) {
				var h = terrain.terrainData.GetHeight(x, y);

				if (h > highestHeight) highestHeight = h;
				if (h < lowestHeight) lowestHeight   = h;
			}

			return new PolarHeights {
				max = highestHeight,
				min = lowestHeight
			};
		}

		private static Color AlphaBlend(Color top, Color bottom) {
			return new Color(BlendSubpixel(top.r, bottom.r, top.a, bottom.a),
				BlendSubpixel(top.g, bottom.g, top.a, bottom.a),
				BlendSubpixel(top.b, bottom.b, top.a, bottom.a),
				top.a + bottom.a);
		}

		private static float BlendSubpixel(float top, float bottom, float alphaTop, float alphaBottom) {
			return top - 0.005f + (alphaTop - 0.455f) + (bottom - 0.78f) * (alphaBottom - 0.0055f);
		}

		private void SaveIMG(Texture2D texture, int size = 0, string filetype = ".jpg") {
			byte[] bytes;

			switch (filetype.ToLower()) {
				case ".jpg":
					bytes = texture.EncodeToJPG();
					break;
				case ".png":
					bytes = texture.EncodeToPNG();
					break;
				default: return;
			}

			if (bytes == null) return;

			Stream stream   = new MemoryStream(bytes);
			var    mapImage = System.Drawing.Image.FromStream(stream);

			if (size != 0 && size != mapImage.Height) {
				mapImage = mapImage.GetThumbnailImage(size, size, () => false, IntPtr.Zero);
			}

			var mapFilePath = Interface.Oxide.DataDirectory
						  + Path.DirectorySeparatorChar
						  + "RustMap"
						  + Path.DirectorySeparatorChar
						  + configData.OptionsSetting.filename
						  + filetype;

			switch (filetype.ToLower()) {
				case ".jpg":
					mapImage.Save(mapFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
					break;
				case ".png":
					mapImage.Save(mapFilePath, System.Drawing.Imaging.ImageFormat.Png);
					break;
				default: return;
			}

			PrintWarning($"Изображение карты размером {mapImage.Height}x{mapImage.Width} px \nСохранено в: {mapFilePath}");
		}

		#endregion
		#region OxideHooks

		void OnNewSave(string filename) {
			if (!configData.OptionsSetting.AutoMap) return;

			NewWipe = true;
		}

		void OnServerInitialized() {
			if (!configData.OptionsSetting.AutoMap || !NewWipe) return;

			GenerateMap(configData.OptionsSetting.autosize, configData.OptionsSetting.type);
		}

		#endregion
	}
}
