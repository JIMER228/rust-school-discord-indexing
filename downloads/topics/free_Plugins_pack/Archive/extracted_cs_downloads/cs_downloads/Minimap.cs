using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.Json;
using Oxide.Ext.Chaos.Map;
using Oxide.Ext.Chaos.UIFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Chaos = Oxide.Ext.Chaos;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;
using Layer = Oxide.Ext.Chaos.UIFramework.Layer;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Minimap", "k1lly0u", "1.2.2")]
    class Minimap : ChaosMapPlugin
    {
        #region Fields

        private bool _isPluginReady;
        
        private static Datafile<Hash<ulong, MinimapUser>> _userData;
        
        private static readonly Hash<EnvironmentVolume, (DungeonBaseLink, int)> VolumeLookup = new();
        
        [Chaos.Permission]
        private const string USE_PERMISSION = "minimap.use";
        
        #endregion
        
        #region Oxide

        private void Loaded()
        {
            _userData = new Datafile<Hash<ulong, MinimapUser>>("minimap.users");
        }
        
        private void OnServerInitialized()
        {
	        if (!ImageLibrary.IsLoaded)
	        {
		        Debug.LogError($"[Minimap] ImageLibrary is not loaded, this plugin requires ImageLibrary to function");
		        return;
	        }
	        
	        FindLabVolumes();
			SetupInterface();
		}

        private void OnPlayerConnected(BasePlayer player)
        {
	        if (!_isPluginReady)
		        return;
	        
	        if ((_userData.Data.TryGetValue(player.userID, out MinimapUser mapUser) && mapUser.IsActive) || 
	            (Configuration.Map.EnabledByDefault && player.HasPermission(USE_PERMISSION)))
		        MapManager.Register(player);
        }
        
        private void OnServerSave() => _userData.Save();

        private void Unload()
        {
	        if (!Interface.Oxide.IsShuttingDown)
		        _userData.Save();
	        
	        MapManager.Destroy();
        }
        
        #endregion
        
        #region Setup
        private void FindLabVolumes()
        {
	        List<EnvironmentVolume> childVolumes = Pool.Get<List<EnvironmentVolume>>();

	        foreach (DungeonBaseInfo dungeonBaseInfo in TerrainMeta.Path.DungeonBaseEntrances)
	        {
		        for (int i = 0; i < dungeonBaseInfo.Floors.Count; i++)
		        {
			        DungeonBaseFloor dungeonBaseFloor = dungeonBaseInfo.Floors[i];
			        if (dungeonBaseFloor != null)
			        {
				        int level = i;
				        foreach (DungeonBaseLink dungeonBaseLink in dungeonBaseFloor.Links)
				        {
					        if (dungeonBaseLink.MapRendererLods == null || dungeonBaseLink.MapRendererLods.Length == 0)
						        continue;

					        dungeonBaseLink.GetComponentsInChildren<EnvironmentVolume>(childVolumes);

					        foreach (EnvironmentVolume environmentVolume in childVolumes)
						        VolumeLookup[environmentVolume] = (dungeonBaseLink, level);

					        childVolumes.Clear();
				        }
			        }
		        }
	        }
        }
        
        protected override void SetupInterface()
        {
	        base.SetupInterface();
	        
	        onAvailableOverlaysChanged += OnAvailableOverlaysChanged;
	        onOverlayUpdated += OnOverlayUpdated;
	        
	        _mapUtility = new MapImageUtility(Configuration.Map);
	        CallbackHandler = new CommandCallbackHandler(this);
	        BorderColor = Configuration.UI.BorderColor;
	        MarkerColor = Configuration.UI.MarkerColor;
	        ForegroundColor = Configuration.UI.ForegroundColor;
	        TextColor = Configuration.UI.TextColor;
	        
	        MapManager.Initialize(InitialMapRender, UpdateMapRender);
	        
	        if (HasAllImages())
		        OnPluginReady();
	        else
	        {
		        ImportMapArrows();
		        ServerMgr.Instance.StartCoroutine(GenerateMaps());
	        }
        }

        private void OnPluginReady()
        {
	        _isPluginReady = true;
	        
	        foreach (BasePlayer player in BasePlayer.activePlayerList)
		        OnPlayerConnected(player);
        }
        #endregion
        
        #region ImageLibrary
        
        private static string _currentWorldID;

        private bool HasAllImages()
        {
	        _currentWorldID = CurrentWorldID(Configuration.Map.RenderResolution);
	        
	        if (!HasArrowImages())
		        return false;
	        
	        if (!TryGetImage(string.Format(WORLD_MAP, _currentWorldID), out string overworld))
		        return false;
	        
	        _mapLayers[MapLayer.Overworld] = overworld;

	        if (Configuration.Map.RenderMonumentNames || Configuration.Map.RenderTunnelEntrances)
	        {
		        if (!TryGetImage(string.Format(OVERWORLD_MARKER_MAP, _currentWorldID), out string overworldMarkers))
			        return false;

		        _mapLayers[MapLayer.OverworldMarkers] = overworldMarkers;

		        if (!TryGetImage(string.Format(UNDERWORLD_MARKER_MAP, _currentWorldID), out string underworldMarkers))
			        return false;

		        _mapLayers[MapLayer.UnderworldMarkers] = underworldMarkers;
	        }

	        if (Configuration.Map.EnableTunnels)
	        {
		        if (!TryGetImage(string.Format(TUNNEL_MAP, _currentWorldID), out string tunnels))
			        return false;
		        
		        _mapLayers[MapLayer.TrainTunnels] = tunnels;
	        }

	        if (Configuration.Map.EnableLabs)
	        {
		        for (MapLayer i = MapLayer.Underwater1; i < MapLayer.Underwater8; i++)
		        {
			        if (!TryGetImage(string.Format(LAB_MAP, _currentWorldID, (int)i - 1), out string lab))
				        return false;
			        
			        _mapLayers[i] = lab;
		        }
	        }

	        return true;
        }
        
        #endregion
        
        #region Map Generation
        
        private const string WORLD_MAP = "world.minimap.{0}";
        private const string TUNNEL_MAP = "world.tunnel.minimap.{0}";
        private const string LAB_MAP = "world.lab.minimap.{0}.{1}";
        private const string OVERWORLD_MARKER_MAP = "world.overworld.markers.{0}";
        private const string UNDERWORLD_MARKER_MAP = "world.underworld.markers.{0}";
        
        private int _pendingImages = 0;
        private int _imagesStored = 0;
        
        private IEnumerator GenerateMaps()
        {
            Debug.Log("[Minimap] Rendering world map layers");

            _pendingImages = 1 + (Configuration.Map.EnableTunnels ? 1 : 0) + (Configuration.Map.EnableLabs ? 8 : 0);

            using (MapRenderer renderer = new MapRenderer(_mapUtility, Configuration.Map))
            {
	            int renderRes = Configuration.Map.RenderResolution;//26509

	            NativeArray<Color> buffer0 = new NativeArray<Color>(renderRes * renderRes, Allocator.Persistent);
	            NativeArray<Color> buffer1 = new NativeArray<Color>(renderRes * renderRes, Allocator.Persistent);

	            Texture2D texture2D = new Texture2D(renderRes, renderRes, TextureFormat.RGBA32, false);

	            if (Configuration.Map.RenderMonumentNames || Configuration.Map.RenderTunnelEntrances)
	            {
		            Debug.Log("[Minimap] Rendering map markers and monument names");
		            
		            yield return renderer.DrawMonumentMarkers(buffer0, false);

		            StoreImageData(string.Format(OVERWORLD_MARKER_MAP, _currentWorldID), buffer0, texture2D, true, (png) => OnImageStored(MapLayer.OverworldMarkers, png));

		            yield return renderer.DrawMonumentMarkers(buffer0, true);

		            StoreImageData(string.Format(UNDERWORLD_MARKER_MAP, _currentWorldID), buffer0, texture2D, true, (png) => OnImageStored(MapLayer.UnderworldMarkers, png));
	            }

	            Debug.Log("[Minimap] Rendering overworld map layer");
	            
	            // Send both buffers, buffer0 will contain the base overworld map image to be used as the underlay
	            // for the other layers, buffer1 will contain the tinted overworld map image
	            yield return renderer.RenderOverworld(buffer0, buffer1, false);
	            
	            StoreImageData(string.Format(WORLD_MAP, _currentWorldID), buffer1, texture2D, false, (png) => OnImageStored(MapLayer.Overworld, png));

	            if (Configuration.Map.EnableTunnels || Configuration.Map.EnableLabs)
	            {
		            // Overlay overworld map image with underworld color
		            renderer.BlendUnderworldOverlay(buffer0);
		            yield return CoroutineEx.waitForEndOfFrame;

		            // Copy results to buffer1
		            buffer0.CopyTo(buffer1);

		            // Render tunnel layers
		            if (Configuration.Map.EnableTunnels)
		            {
			            Debug.Log("[Minimap] Rendering tunnel map layer");
			            yield return renderer.RenderTrainTunnels(buffer1, false);
			            StoreImageData(string.Format(TUNNEL_MAP, _currentWorldID), buffer1, texture2D, false, (png) => OnImageStored(MapLayer.TrainTunnels, png));
		            }

		            // Render lab layers
		            if (Configuration.Map.EnableLabs)
		            {
			            for (int i = 0; i < 8; i++)
			            {
				            MapLayer mapLayer = (MapLayer)(i + 1);
				            // Copy results from overworld to buffer1
				            buffer0.CopyTo(buffer1);
				            
				            Debug.Log($"[Minimap] Rendering lab map layer {i}");
				            yield return renderer.RenderUnderwaterLabs(buffer1, i, false);
				            StoreImageData(string.Format(LAB_MAP, _currentWorldID, i), buffer1, texture2D, false, (png) => OnImageStored(mapLayer, png));
			            }
		            }
	            }

	            buffer0.Dispose();
	            buffer1.Dispose();
            }

            Debug.Log("[Minimap] Finished rendering world map layers");
        }

        private void OnImageStored(MapLayer mapLayer, string png)
        {
	        _mapLayers[mapLayer] = png;

	        _imagesStored++;
		        
	        if (_imagesStored == _pendingImages)
		        OnPluginReady();
        }
        
        #endregion
        
        #region Map Manager

        private const string UI_MINIMAP = "ui.minimap";
        private const string UI_MOUSE_HELPER_PARENT = "ui.minimap.mouse1";
        private const string UI_MOUSE_HELPER = "ui.minimap.mouse2";
        private const string UI_MOUSE_OVERLAY_PARENT = "ui.minimap.mouse3";
        private const string UI_MOUSE_OVERLAY = "ui.minimap.mouse4";
        private const string UI_MINIMAP_SETTINGS = "ui.minimap.settings";
        private const string UI_PLAYER_POSITION_MARKER = "ui.minimap.player.position";
        private const string UI_PLAYER_ROTATION_MARKER = "ui.minimap.player.rotation";
        private const string UI_MAP_IMAGE = "ui.minimap.image";
        private const string UI_SCROLLVIEW = "ui.minimap.scrollview";
        private const string UI_MARKER_OVERLAY = "ui.minimap.markers";
        
        private const string UI_OVERLAYS = "ui.minimap.overlays";
        
        private static MapImageUtility _mapUtility;
        
        private static ScrollViewComponent _scrollView = new()
        {
	        Horizontal = true,
	        Vertical = true,
	        MovementType = ScrollRect.MovementType.Clamped
        };

        private static Hash<MapLayer, string> _mapLayers = new();
        
        private readonly List<UpdateComponent> _updates = new();
        
        public enum MapLayer
        {
	        UnderworldMarkers = -3, 
	        OverworldMarkers = -2,
	        Overworld = -1, 
	        TrainTunnels = 0,
	        Underwater1 = 1,
	        Underwater2 = 2,
	        Underwater3 = 3,
	        Underwater4 = 4,
	        Underwater5 = 5,
	        Underwater6 = 6,
	        Underwater7 = 7,
	        Underwater8 = 8,
	        Dungeons = 10, 
        }
        
        private class MapManager : MonoBehaviour
        {
	        private static MapManager _instance;

	        private Queue<MinimapUser> _activeQueue = new();
	        
	        private Queue<MinimapUser> _nextQueue = new();

	        private readonly Stopwatch _stopwatch = new();
	        
	        private Action<MinimapUser> _initialMapRender;
	        private Action<MinimapUser, Vector3?, float?, MapLayer?> _updateMapRender;

	        public static void Initialize(Action<MinimapUser> initialMapRender, Action<MinimapUser, Vector3?, float?, MapLayer?> updateMapRender)
	        {
		        if (_instance)
			        return;
		        
		        _instance = new GameObject("MapManager").AddComponent<MapManager>();
		        
		        _instance._initialMapRender = initialMapRender;
		        _instance._updateMapRender = updateMapRender;
	        }
	        
	        public static void Destroy()
	        {
		        if (!_instance)
			        return;
		        
		        foreach (MinimapUser mapUser in _instance._activeQueue)
		        {
			        if (mapUser.Player)
			        {
				        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP);
				        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP_SETTINGS);
				        ChaosUI.Destroy(mapUser.Player, UI_MOUSE_HELPER);
			        }
		        }

		        foreach (MinimapUser mapUser in _instance._nextQueue)
		        {
			        if (mapUser.Player)
			        {
				        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP);
				        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP_SETTINGS);
				        ChaosUI.Destroy(mapUser.Player, UI_MOUSE_HELPER);
			        }
		        }

		        _instance._activeQueue.Clear();
		        _instance._nextQueue.Clear();
		        
		        Destroy(_instance.gameObject);
		        
		        _instance = null;
	        }
	        
	        public static void Register(BasePlayer player)
	        {
		        if (!_instance)
			        return;
		        
		        if (!_userData.Data.TryGetValue(player.userID, out MinimapUser mapUser))
			        mapUser = _userData.Data[player.userID] = new MinimapUser(Mathf.Clamp(Configuration.Map.DefaultZoomLevel, 0, Configuration.Map.ZoomLevels));
		        
		        mapUser.Player = player;
		        mapUser.IsActive = true;
		        
		        _instance._initialMapRender(mapUser);
		        
		        _instance._nextQueue.Enqueue(mapUser);

	        }
	        
	        private void Update() => RunQueue(0.5);

	        private void RunQueue(double maximumMilliseconds)
	        {
		        _stopwatch.Restart();
		        while (_activeQueue.Count > 0)
		        {
			        if (_stopwatch.Elapsed.TotalMilliseconds >= maximumMilliseconds)
				        break;
			        
			        MinimapUser mapUser = _activeQueue.Dequeue();
			        if (mapUser == null || !mapUser.Player || !mapUser.IsActive)
				        continue;
			        
			        RunUpdateJob(mapUser);
		        }

		        CheckFlipQueue();
	        }

	        private void CheckFlipQueue()
	        {
		        if (_activeQueue.Count == 0 && _nextQueue.Count > 0)
			        (_activeQueue, _nextQueue) = (_nextQueue, _activeQueue);
	        }

	        private void RunUpdateJob(MinimapUser mapUser)
	        {
		        if (!mapUser.Player || !mapUser.IsActive)
			        return;

		        if (mapUser.ShouldUpdate(out Vector3? position, out float? rotation, out MapLayer? mapLayer) || mapUser.ForceUpdate)
			        _updateMapRender(mapUser, position, rotation, mapLayer);
		        
		        _nextQueue.Enqueue(mapUser);
	        }

	        public static IEnumerable<MinimapUser> GetActiveMapUsers()
	        {
		        if (!_instance)
			        yield break;
		        
		        foreach (MinimapUser mapUser in _instance._activeQueue)
		        {
			        if (mapUser.IsActive && mapUser.Player)
				        yield return mapUser;
		        }
		        
		        foreach (MinimapUser mapUser in _instance._nextQueue)
		        {
			        if (mapUser.IsActive && mapUser.Player)
				        yield return mapUser;
		        }
	        }
        }
        
        #endregion
        
        #region UI

        private void OnAvailableOverlaysChanged()
        {
	        if (!_isPluginReady)
		        return;

	        foreach (MinimapUser mapUser in MapManager.GetActiveMapUsers())
		        InitialMapRender(mapUser);
        }

        private void OnOverlayUpdated(Overlay overlay)
        {
	        if (!_isPluginReady)
		        return;

	        foreach (MinimapUser mapUser in MapManager.GetActiveMapUsers())
	        {
		        if (mapUser.GetOverlayState(overlay.Name))
			        OnOverlayUpdated(mapUser, overlay);
	        }
        }

        private void CreateMouseHelper(MinimapUser mapUser)
        {
	        if (!mapUser.Player || mapUser.MouseHelperOpen)
		        return;
	        
	        mapUser.MouseHelperOpen = true;
	        
	        BaseContainer root = ButtonContainer.Create(UI_MOUSE_HELPER, Layer.Hud, Anchor.Center, new Offset(2560, 1440))
		        .WithColor(Chaos.UIFramework.Color.Clear)
		        .WithCallback(CallbackHandler, arg => DestroyMouseHelper(mapUser), $"{mapUser.Player.UserIDString}.mousehelper")
		        .WithParent(UI_MOUSE_HELPER_PARENT)
		        .DestroyExisting()
		        .NeedsCursor();
	        
	        ChaosUI.Destroy(mapUser.Player, UI_MOUSE_OVERLAY);
	        ChaosUI.Show(mapUser.Player, root);
        }

        private void DestroyMouseHelper(MinimapUser mapUser)
        {
	        BaseContainer mouseOverlay = ButtonContainer.Create(UI_MOUSE_OVERLAY, Layer.Hud, Anchor.FullStretch, Offset.zero)
		        .WithColor(Chaos.UIFramework.Color.Clear)
		        .WithCallback(CallbackHandler, arg => CreateMouseHelper(mapUser), $"{mapUser.Player.UserIDString}.mouseoverlay")
		        .WithParent(UI_MOUSE_OVERLAY_PARENT)
		        .DestroyExisting();
	        
	        mapUser.MouseHelperOpen = false;
	        ChaosUI.Destroy(mapUser.Player, UI_MOUSE_HELPER);
	        ChaosUI.Show(mapUser.Player, mouseOverlay);
        }
	        
        private void InitialMapRender(MinimapUser mapUser)
        {
	        if (!mapUser.Player)
		        return;

	        (Anchor containerAnchor, Offset containerOffset, float screenSize) = mapUser.GetAnchorOffsetAndSize();
	        
	        mapUser.GetInitial(out Vector3 position, out float rotation, out MapLayer mapLayer);

	        Offset mapOffset = _mapUtility.CalculateImageOffsetForPosition(screenSize, position, mapUser.ZoomLevel, Configuration.Map.ZoomLevels);
	        
	        BaseContainer root = ImageContainer.Create(UI_MINIMAP, Layer.Hud, containerAnchor, containerOffset)
		        .WithColor(BorderColor)
		        .WithName(UI_MINIMAP)
		        .WithChildren(minimap =>
		        {
			        BaseContainer.Create(minimap, Anchor.FullStretch, Offset.zero)
				        .WithName(UI_MOUSE_HELPER_PARENT);
			        
			        // ScrollView
			        ImageContainer.Create(minimap, Anchor.FullStretch, new Offset(1f, 2f, -3.5f, -2f))
				        .WithColor(Chaos.UIFramework.Color.Clear)
				        .WithName(UI_SCROLLVIEW)
				        .WithScrollView(_scrollView.WithContentTransform(Anchor.Center, mapOffset))
				        .WithChildren(content =>
				        {
					        RawImageContainer.Create(content, Anchor.FullStretch, Offset.zero)
						        .WithPNG(GetMapForCurrentLayer(mapLayer))
						        .WithName(UI_MAP_IMAGE)
						        .WithChildren(image =>
						        {
							        // Third Party Overlay
							        BaseContainer.Create(image, Anchor.FullStretch, Offset.zero)
								        .WithName(UI_OVERLAYS);

							        if (Configuration.Map.RenderMonumentNames || Configuration.Map.RenderTunnelEntrances)
							        {
								        RawImageContainer.Create(image, Anchor.FullStretch, Offset.zero)
									        .WithPNG(GetMapForCurrentLayer(mapLayer < MapLayer.TrainTunnels ? MapLayer.OverworldMarkers : MapLayer.UnderworldMarkers))
									        .WithName(UI_MARKER_OVERLAY);
							        }
						        });

					        float2 markerPosition = _mapUtility.WorldToImage(mapOffset, position);
					        float2 halfSize = new float2(mapOffset.Width * 0.5f, mapOffset.Height * 0.5f);
					        markerPosition = math.clamp(markerPosition, -halfSize, halfSize);

					        ImageContainer.Create(content, Anchor.Center, new Offset(markerPosition.x - 7, markerPosition.y - 7, markerPosition.x + 7, markerPosition.y + 7))
						        .WithSprite(Icon.Circle_Closed)
						        .WithColor(BorderColor)
						        .WithName(UI_PLAYER_POSITION_MARKER)
						        .WithChildren(player =>
						        {
							        ImageContainer.Create(player, Anchor.FullStretch, new Offset(1, 1, -1, -1))
								        .WithSprite(Icon.Circle_Closed)
								        .WithColor(MarkerColor)
								        .WithChildren(arrow =>
								        {
									        RawImageContainer.Create(arrow, Anchor.FullStretch, Offset.zero)
										        .WithPNG(GetClosestDirectionIcon(rotation))
										        .WithColor(BorderColor)
										        .WithName(UI_PLAYER_ROTATION_MARKER);
								        });
						        });
				        });
			        
			        BaseContainer.Create(minimap, Anchor.FullStretch, Offset.zero)
				        .WithName(UI_MOUSE_OVERLAY_PARENT)
				        .WithChildren(parent =>
				        {
					        ButtonContainer.Create(parent, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg => CreateMouseHelper(mapUser), $"{mapUser.Player.UserIDString}.mouseoverlay")
						        .WithName(UI_MOUSE_OVERLAY);
				        });

			        // Close
			        ImageContainer.Create(minimap, Anchor.TopRight, new Offset(-24f, -24f, 0f, 0f))
				        .WithColor(BorderColor)
				        .WithChildren(header =>
				        {
					        ImageContainer.Create(header, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(close =>
						        {
							        ImageContainer.Create(close, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Close)
								        .WithColor(TextColor);

							        ButtonContainer.Create(close, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, arg =>
									        {
										        mapUser.IsActive = false;
										        mapUser.MouseHelperOpen = false;
										        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP);
										        ChaosUI.Destroy(mapUser.Player, UI_MINIMAP_SETTINGS);
										        ChaosUI.Destroy(mapUser.Player, UI_MOUSE_HELPER);
									        }, $"{mapUser.Player.UserIDString}.collapse");
						        });
				        });
			        
			        // Settings
			        ImageContainer.Create(minimap, Anchor.TopLeft, new Offset(0f, -24f, 24f, 0f))
				        .WithColor(BorderColor)
				        .WithChildren(header =>
				        {
					        ImageContainer.Create(header, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(close =>
						        {
							        ImageContainer.Create(close, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Gear)
								        .WithColor(TextColor);

							        ButtonContainer.Create(close, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, arg => CreateSettingsOverlay(mapUser), $"{mapUser.Player.UserIDString}.settings");
						        });
				        });
			        
			        // Zoom
			        ImageContainer.Create(minimap, Anchor.BottomRight, new Offset(-46f, 0f, 0f, 24f))
				        .WithColor(BorderColor)
				        .WithChildren(zoom =>
				        {
					        ImageContainer.Create(zoom, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(zoomIn =>
						        {
							        ImageContainer.Create(zoomIn, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Add)
								        .WithColor(TextColor);

							        ButtonContainer.Create(zoomIn, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, arg =>
									        {
										        OnZoomChanged(mapUser, 1);
										        CreateMouseHelper(mapUser);
									        }, $"{mapUser.Player.UserIDString}.zoom.in");
						        });


					        ImageContainer.Create(zoom, Anchor.BottomRight, new Offset(-44f, 2f, -24f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(zoomOut =>
						        {
							        ImageContainer.Create(zoomOut, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Subtract)
								        .WithColor(TextColor);

							        ButtonContainer.Create(zoomOut, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, arg =>
									        {
										        OnZoomChanged(mapUser, -1);
										        CreateMouseHelper(mapUser);
									        }, $"{mapUser.Player.UserIDString}.zoom.out");
						        });
				        });
		        })
		        .DestroyExisting();

	        ChaosUI.Show(mapUser.Player, root);
	        
	        RenderOverlays(mapUser, UI_OVERLAYS);
	        RenderOverlayToggles(mapUser, UI_MINIMAP);
        }

        private static float2 _verticalMovement = new float2(0f, 10f);
        private static float2 _horizontalMovement = new float2(10f, 0f);
        
        private void CreateSettingsOverlay(MinimapUser mapUser)
        {
	        if (!mapUser.Player)
		        return;
	        
	        BaseContainer root = ImageContainer.Create(UI_MINIMAP_SETTINGS, Layer.Hud, Anchor.Center, new Offset(-75f, -65f, 65f, 75f))
		        .WithColor(BorderColor)
		        .WithChildren(parent =>
		        {
			        ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, 0f, 0f, 22f))
				        .WithColor(BorderColor)
				        .WithChildren(header =>
				        {
					        ImageContainer.Create(header, Anchor.FullStretch, new Offset(2f, 0f, -2f, -2f))
						        .WithColor(ForegroundColor)
						        .WithChildren(inset =>
						        {
							        TextContainer.Create(inset, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
								        .WithSize(12)
								        .WithColor(TextColor)
								        .WithText(GetString("UI.Settings.Title", mapUser.Player))
								        .WithAlignment(TextAnchor.MiddleLeft);
						        });
				        });

			        
			        ImageContainer.Create(parent, Anchor.TopRight, new Offset(-24f, -2f, 0f, 22f))
				        .WithColor(BorderColor)
				        .WithChildren(header =>
				        {
					        ImageContainer.Create(header, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(close =>
						        {
							        ImageContainer.Create(close, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Close)
								        .WithColor(TextColor);

							        ButtonContainer.Create(close, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, 
									        arg => ChaosUI.Destroy(mapUser.Player, UI_MINIMAP_SETTINGS), 
									        $"{mapUser.Player.UserIDString}.settings.close");
						        });
				        });
			        
			        ImageContainer.Create(parent, Anchor.TopRight, new Offset(-46f, -2f, -22f, 22f))
				        .WithColor(BorderColor)
				        .WithChildren(header =>
				        {
					        ImageContainer.Create(header, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
						        .WithColor(ForegroundColor)
						        .WithChildren(close =>
						        {
							        ImageContainer.Create(close, Anchor.FullStretch, new Offset(4, 4, -4, -4))
								        .WithSprite(Icon.Icons_Rotate)
								        .WithColor(TextColor);

							        ButtonContainer.Create(close, Anchor.FullStretch, Offset.zero)
								        .WithColor(Chaos.UIFramework.Color.Clear)
								        .WithCallback(CallbackHandler, arg =>
									        {
										        if (mapUser.SetPosition(Configuration.UI.Position) || mapUser.SetSize(Configuration.UI.Size))
													UpdateSizeAndOffset(mapUser);
									        }, 
									        $"{mapUser.Player.UserIDString}.settings.reset");
						        });
				        });
			        
			        ImageContainer.Create(parent, Anchor.TopCenter, new Offset(-10f, -22f, 10f, -2f))
				        .WithColor(ForegroundColor)
				        .WithChildren(up =>
				        {
					        TextContainer.Create(up, Anchor.FullStretch, Offset.zero)
						        .WithText("▲")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        
					        ButtonContainer.Create(up, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(mapUser.Position + _verticalMovement))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.up");
				        });
			        
			        ImageContainer.Create(parent, Anchor.BottomCenter, new Offset(-10f, 2f, 10f, 22f))
				        .WithColor(ForegroundColor)
				        .WithChildren(down =>
				        {
					        TextContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithText("▼")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(mapUser.Position - _verticalMovement))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.down");
				        });
			        
			        ImageContainer.Create(parent, Anchor.CenterLeft, new Offset(2f, -10f, 22f, 10f))
				        .WithColor(ForegroundColor)
				        .WithChildren(left =>
				        {
					        TextContainer.Create(left, Anchor.FullStretch, Offset.zero)
						        .WithText("◄")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(left, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(mapUser.Position - _horizontalMovement))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.left");
				        });
			        
			        ImageContainer.Create(parent, Anchor.CenterRight, new Offset(-22f, -10f, -2f, 10f))
				        .WithColor(ForegroundColor)
				        .WithChildren(right =>
				        {
					        TextContainer.Create(right, Anchor.FullStretch, Offset.zero)
						        .WithText("►")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(right, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(mapUser.Position + _horizontalMovement))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.right");
				        });
			        
			        float halfSize = mapUser.Size * 0.5f;
			        
			        ImageContainer.Create(parent, Anchor.BottomLeft, new Offset(2f, 2f, 22f, 22f))
				        .WithColor(ForegroundColor)
				        .WithChildren(down =>
				        {
					        TextContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithText("\u2199")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(new float2(-(640 - halfSize), -(360 - halfSize))))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.bottomleft");
				        });
			        
			        ImageContainer.Create(parent, Anchor.BottomRight, new Offset(-22f, 2f, -2f, 22f))
				        .WithColor(ForegroundColor)
				        .WithChildren(down =>
				        {
					        TextContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithText("\u2198")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(new float2(640 - halfSize, -(360 - halfSize))))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.bottomright");
				        });
			        
			        ImageContainer.Create(parent, Anchor.TopLeft, new Offset(2f, -22f, 22f, -2f))
				        .WithColor(ForegroundColor)
				        .WithChildren(down =>
				        {
					        TextContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithText("\u2196")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(new float2(-(640 - halfSize), (360 - halfSize))))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.topleft");
				        });
			        
			        ImageContainer.Create(parent, Anchor.TopRight, new Offset(-22f, -22f, -2f, -2f))
				        .WithColor(ForegroundColor)
				        .WithChildren(down =>
				        {
					        TextContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithText("\u2197")
						        .WithColor(TextColor)
						        .WithAlignment(TextAnchor.MiddleCenter);
					        ButtonContainer.Create(down, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetPosition(new float2((640 - halfSize), (360 - halfSize))))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.move.topright");
				        });
			        
			        ImageContainer.Create(parent, Anchor.Center, new Offset(1f, -10f, 21f, 10f))
				        .WithColor(ForegroundColor)
				        .WithChildren(scaleIn =>
				        {
					        ImageContainer.Create(scaleIn, Anchor.FullStretch, new Offset(4, 4, -4, -4))
						        .WithSprite(Icon.Icons_Add)
						        .WithColor(TextColor);

					        ButtonContainer.Create(scaleIn, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg =>
							        {
								        if (mapUser.SetSize(mapUser.Size + 5f))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.scale.in");
				        });
			        
			        ImageContainer.Create(parent, Anchor.Center, new Offset(-21f, -10f, -1f, 10f))
				        .WithColor(ForegroundColor)
				        .WithChildren(scaleOut =>
				        {
					        ImageContainer.Create(scaleOut, Anchor.FullStretch, new Offset(4, 4, -4, -4))
						        .WithSprite(Icon.Icons_Subtract)
						        .WithColor(TextColor);

					        ButtonContainer.Create(scaleOut, Anchor.FullStretch, Offset.zero)
						        .WithColor(Chaos.UIFramework.Color.Clear)
						        .WithCallback(CallbackHandler, arg => {
								        if (mapUser.SetSize(mapUser.Size - 5f))
									        UpdateSizeAndOffset(mapUser);
							        }, $"{mapUser.Player.UserIDString}.scale.out");
				        });
		        })
		        .DestroyExisting()
		        .NeedsCursor();

	        ChaosUI.Show(mapUser.Player, root);
        }

        private void UpdateSizeAndOffset(MinimapUser mapUser)
        {
	        if (!mapUser.Player)
		        return;
	        
	        (Anchor anchor, Offset containerOffset, float screenSize) = mapUser.GetAnchorOffsetAndSize();
	        
	        UpdateComponent<RectTransformComponent> update = ChaosUI.PrepareUpdate<RectTransformComponent>(UI_MINIMAP);
	        update.Component.Set(anchor, containerOffset);
	        
	        update.MarkFieldsDirty(
		        nameof(RectTransformComponent.AnchorMin), 
			        nameof(RectTransformComponent.AnchorMax), 
			        nameof(RectTransformComponent.OffsetMin), 
			        nameof(RectTransformComponent.OffsetMax));
	        
	        update.Send(mapUser.Player);
        }
        
        private void OnZoomChanged(MinimapUser mapUser, int direction)
        {
	        if (!mapUser.Player)
		        return;
            
	        mapUser.ZoomLevel = Mathf.Clamp(mapUser.ZoomLevel + direction, 0, Configuration.Map.ZoomLevels);
            
	        Offset mapOffset = _mapUtility.CalculateImageOffsetForPosition(Configuration.UI.Size, mapUser.Player.transform.position, mapUser.ZoomLevel, Configuration.Map.ZoomLevels);
            
	        UpdateComponent<ScrollViewComponent> update = ChaosUI.PrepareUpdate<ScrollViewComponent>(UI_SCROLLVIEW);
	        update.Component.CopyFrom(_scrollView.WithContentTransform(Anchor.Center, mapOffset));

	        UpdateComponent<RectTransformComponent> playerUpdate = ChaosUI.PrepareUpdate<RectTransformComponent>(UI_PLAYER_POSITION_MARKER);
	        
	        float2 markerPosition = _mapUtility.WorldToImage(mapOffset, mapUser.Player.transform.position);
	        float2 halfSize = new float2(mapOffset.Width * 0.5f, mapOffset.Height * 0.5f);
	        markerPosition = math.clamp(markerPosition, -halfSize, halfSize);

	        playerUpdate.Component.Set(Anchor.Center, new Offset(markerPosition.x - 7, markerPosition.y - 7, markerPosition.x + 7, markerPosition.y + 7));
            
	        update.Send(mapUser.Player);
	        playerUpdate.Send(mapUser.Player);
        }

        private void UpdateMapRender(MinimapUser mapUser, Vector3? position, float? rotation, MapLayer? mapLayer)
        {
	        _updates.Clear();
	        
	        if (position.HasValue || mapUser.ForceUpdate)
	        {
		        Vector3 value = position ?? mapUser.LastPosition;

		        Offset mapOffset = _mapUtility.CalculateImageOffsetForPosition(Configuration.UI.Size, value, mapUser.ZoomLevel, Configuration.Map.ZoomLevels);
		        
		        UpdateComponent<ScrollViewComponent> mapPosition = ChaosUI.PrepareUpdate<ScrollViewComponent>(UI_SCROLLVIEW);
		        mapPosition.Component.CopyFrom(_scrollView.WithContentTransform(Anchor.Center, mapOffset));
		        
		        UpdateComponent<RectTransformComponent> playerPosition = ChaosUI.PrepareUpdate<RectTransformComponent>(UI_PLAYER_POSITION_MARKER);
		        float2 markerPosition = _mapUtility.WorldToImage(mapOffset, mapUser.Player.transform.position);
		        float2 halfSize = new float2(mapOffset.Width * 0.5f, mapOffset.Height * 0.5f);
		        markerPosition = math.clamp(markerPosition, -halfSize, halfSize);
		        playerPosition.Component.Set(Anchor.Center, new Offset(markerPosition.x - 7, markerPosition.y - 7, markerPosition.x + 7, markerPosition.y + 7));//26509
		        playerPosition.MarkFieldsDirty(nameof(RectTransformComponent.OffsetMin), nameof(RectTransformComponent.OffsetMax));
		        
		        _updates.Add(mapPosition);
		        _updates.Add(playerPosition);
	        }
	        
	        if (rotation.HasValue || mapUser.ForceUpdate)
	        {
		        float value = rotation ?? mapUser.LastRotation;
		        
		        UpdateComponent<RawImageComponent> mapArrow = ChaosUI.PrepareUpdate<RawImageComponent>(UI_PLAYER_ROTATION_MARKER);
		        mapArrow.Component.PNG = GetClosestDirectionIcon(value);
		        mapArrow.MarkFieldsDirty(nameof(RawImageComponent.PNG));
		        _updates.Add(mapArrow);
	        }
	        
	        if (mapLayer.HasValue || mapUser.ForceUpdate)
	        {
		        MapLayer value = mapLayer ?? mapUser.LastLayer;
		        
		        UpdateComponent<RawImageComponent> mapImage = ChaosUI.PrepareUpdate<RawImageComponent>(UI_MAP_IMAGE);
		        mapImage.Component.PNG = _mapLayers[value];
		        mapImage.MarkFieldsDirty(nameof(RawImageComponent.PNG));
		        _updates.Add(mapImage);

		        if (Configuration.Map.RenderMonumentNames || Configuration.Map.RenderTunnelEntrances)
		        {
			        UpdateComponent<RawImageComponent> markerImage = ChaosUI.PrepareUpdate<RawImageComponent>(UI_MARKER_OVERLAY);
			        markerImage.Component.PNG = GetMapForCurrentLayer(value < MapLayer.TrainTunnels ? MapLayer.OverworldMarkers : MapLayer.UnderworldMarkers);
			        markerImage.MarkFieldsDirty(nameof(RawImageComponent.PNG));
			        _updates.Add(markerImage);
		        }
	        }

	        if (_updates.Count > 0)
				ChaosUI.SendUpdates(mapUser.Player, _updates);
	        
	        mapUser.ForceUpdate = false;
        }

        private string GetMapForCurrentLayer(MapLayer mapLayer)
        {
	        if (!_mapLayers.TryGetValue(mapLayer, out string map))
		        return _mapLayers[MapLayer.Overworld];
	        return map;
        }

        #endregion
        
        #region Commands

        [ChatCommand("map")]
        private void cmdMinimap(BasePlayer player, string command, string[] args)
        {
	        if (!player.HasPermission(USE_PERMISSION))
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }

	        if (!_isPluginReady)
	        {
		        player.LocalizedMessage(this, "Error.Rendering");
		        return;
	        }

	        _userData.Data.TryGetValue(player.userID, out MinimapUser mapUser);

	        if (mapUser != null && args is { Length: 1 } && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
	        {
		        if (mapUser.SetSize(Configuration.UI.Size) || mapUser.SetPosition(Configuration.UI.Position))
		        {
			        if (mapUser.IsActive)
				        UpdateSizeAndOffset(mapUser);
		        }

		        return;
	        }

	        if (mapUser is not { IsActive: true })
		        MapManager.Register(player);
	        else
	        {
		        mapUser.IsActive = false;
		        ChaosUI.Destroy(player, UI_MINIMAP);
	        }
        }
        
        
        [ConsoleCommand("minimap.regenerate")]
        private void ccmdRegenerate(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (player && !player.IsAdmin)
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }
	        
	        SendReply(arg, "Regenerating map images...");
	        MapManager.Destroy();
	        
	        _isPluginReady = false;
	        _pendingImages = 0;
	        _imagesStored = 0;
	        
	        MapManager.Initialize(InitialMapRender, UpdateMapRender);
	        
	        ImportMapArrows();
	        ServerMgr.Instance.StartCoroutine(GenerateMaps());
        }
        
        [ConsoleCommand("minimap.reset")]
        private void ccmdMinimapReset(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (!player)
		        return;

	        if (!player.HasPermission(USE_PERMISSION))
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }
	        
	        if (!_isPluginReady)
	        {
		        player.LocalizedMessage(this, "Error.Rendering");
		        return;
	        }

	        if (!_userData.Data.TryGetValue(player.userID, out MinimapUser mapUser))
		        return;
	        
	        if (mapUser.SetSize(Configuration.UI.Size) || mapUser.SetPosition(Configuration.UI.Position))
	        {
		        if (mapUser.IsActive)
			        UpdateSizeAndOffset(mapUser);
	        }
        }

        [ConsoleCommand("minimap.toggle")]
        private void ccmdMinimapToggle(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (!player)
		        return;

	        if (!player.HasPermission(USE_PERMISSION))
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }
	        
	        if (!_isPluginReady)
	        {
		        player.LocalizedMessage(this, "Error.Rendering");
		        return;
	        }

	        _userData.Data.TryGetValue(player.userID, out MinimapUser mapUser);

	        if (mapUser is not { IsActive: true })
		        MapManager.Register(player);
	        else
	        {
		        mapUser.IsActive = false;
		        ChaosUI.Destroy(player, UI_MINIMAP);
	        }
        }
        
        [ConsoleCommand("minimap.zoom.in")]
        private void ccmdMinimapZoomIn(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (!player)
		        return;

	        if (!player.HasPermission(USE_PERMISSION))
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }
	        
	        if (!_userData.Data.TryGetValue(player.userID, out MinimapUser mapUser))
		        return;
	        
	        mapUser.ZoomLevel = Mathf.Clamp(mapUser.ZoomLevel + 1, 0, Configuration.Map.ZoomLevels);
	        mapUser.ForceUpdate = true;
        }
        
        [ConsoleCommand("minimap.zoom.out")]
        private void ccmdMinimapZoomOut(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (!player)
		        return;

	        if (!player.HasPermission(USE_PERMISSION))
	        {
		        player.LocalizedMessage(this, "Error.NoPermission");
		        return;
	        }
	        
	        if (!_userData.Data.TryGetValue(player.userID, out MinimapUser mapUser))
		        return;
	        
	        mapUser.ZoomLevel = Mathf.Clamp(mapUser.ZoomLevel - 1, 0, Configuration.Map.ZoomLevels);
	        mapUser.ForceUpdate = true;
        }

        [ConsoleCommand("minimap.render")]
        private void ccmdMinimapRender(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (player && !player.IsAdmin)
		        return;
	        
	        SendReply(arg, "[Minimap] Re-rendering world map layers");
	        ImportMapArrows();
	        ServerMgr.Instance.StartCoroutine(GenerateMaps());
        }
        #endregion
        
        #region Localization
        
        protected override Dictionary<string, string> Messages => new()
        {
	        ["Error.NoPermission"] = "You do not have permission to use this command",
	        ["Error.Rendering"] = "The minimap is currently rendering, please try again soon",
	        ["UI.Settings.Title"] = "Minimap Editor"
        };

        #endregion

        #region Configuration

        private static ConfigData Configuration;

        protected override void LoadConfig()
        {
	        base.LoadConfig();
	        Configuration = ConfigurationData as ConfigData;
        }

        protected override void PrepareConfigFile(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);
        
        protected class ConfigData : BaseConfigData
        {
	        [JsonProperty("Map Settings")]
	        public MapSettings Map = new MapSettings();
	        
	        [JsonProperty("UI Settings")]
	        public UISettings UI = new UISettings();

	        public class MapSettings : MapConfig
	        {
		        [JsonProperty("Zoom levels")]
		        public int ZoomLevels { get; set; }
		        
		        [JsonProperty("Default zoom level")]
		        public int DefaultZoomLevel { get; set; }
		        
		        [JsonProperty("Enable train tunnel map")]
		        public bool EnableTunnels { get; set; }
		        
		        [JsonProperty("Enable underwater labs map")]
		        public bool EnableLabs { get; set; }
		        
		        [JsonProperty("Enabled by default")]
		        public bool EnabledByDefault { get; set; }
	        }
	        
	        public class UISettings
	        {
		        [JsonProperty("Screen position (base screen size is 1280x720)")]
		        public ScreenPosition Position { get; set; }
		        
		        [JsonProperty("Minimap screen size (pixels)")]
		        public float Size { get; set; }
		        
		        [JsonProperty("Border color")]
		        public HexColor.Rgba BorderColor { get; set; }
		        
		        [JsonProperty("Foreground color")]
		        public HexColor.Rgba ForegroundColor { get; set; }
		        
		        [JsonProperty("Text color")]
		        public HexColor.Rgba TextColor { get; set; }
		        
		        [JsonProperty("Marker color")]
		        public HexColor.Rgba MarkerColor { get; set; }

		        public class ScreenPosition
		        {
			        [JsonProperty("Horizontal (-640.0 -> 640.0)")]
			        public float X { get; set; } = 531f;

			        [JsonProperty("Vertical (-360.0 -> 360.0)")]
			        public float Y { get; set; } = 251f;
			        
			        public ScreenPosition(){}
			        
			        public ScreenPosition(float x, float y)
			        {
				        X = x;
				        Y = y;
			        }
			        
			        public static implicit operator float2(ScreenPosition position) => new(position.X, position.Y);
		        }
	        }
        }

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
	            Map = new ConfigData.MapSettings
	            {
		            ZoomLevels = 10,
		            DefaultZoomLevel = 5,
		            EnableLabs = true,
		            EnableTunnels = true,
		            Underworld = new MapConfig.UnderworldColors()
	            },
	            UI = new ConfigData.UISettings
	            {
		            BorderColor = new HexColor.Rgba("1C1A16"),
		            ForegroundColor = new HexColor.Rgba("5D7239"),
		            TextColor = new HexColor.Rgba("B0CC80"),
		            MarkerColor = new HexColor.Rgba("FFD272"),
		            Size = 200,
		            Position = new ConfigData.UISettings.ScreenPosition(531, 251)
	            },
	            Version = Version
            } as T;
        }
        
        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
	        ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();
        }

        #endregion
        
        #region Data
        public class MinimapUser : BaseMapUser
        {
	        public int ZoomLevel = 1;

	        [JsonConverter(typeof(float2Converter))]
	        public float2 Position = new float2(0, 0);
	        
	        public float Size = 0f;

	        [JsonIgnore]
	        public bool MouseHelperOpen;

	        [JsonIgnore]
	        public bool ForceUpdate;

	        [JsonIgnore]
	        public Vector3 LastPosition = Vector3.zero;
	            
	        [JsonIgnore]
	        public float LastRotation;
	        
	        [JsonIgnore]
	        public MapLayer LastLayer = 0;

	        [JsonIgnore]
	        private EnvironmentType _lastEnvironment = EnvironmentType.Outdoor;
	        
	        [JsonIgnore]
	        private int _lastFloor = 0;

	        [JsonIgnore]
	        private float _nextEnvironmentUpdate;
	        
	        [JsonIgnore]
	        private static List<EnvironmentVolume> _environmentVolumes = new();
	        
	        public MinimapUser(){}
	        
	        public MinimapUser(int zoomLevel)
	        {
		        Size = Configuration.UI.Size;
		        Position = RectTransformUtils.ClampPosition(Configuration.UI.Position, Size);
		        
		        ZoomLevel = zoomLevel;
	        }

	        public (Anchor anchor, Offset offset, float size) GetAnchorOffsetAndSize()
	        {
		        if (Size == 0f)
			        Size = Configuration.UI.Size;
		        
		        if (Position is { x: 0, y: 0 })
			        Position = Configuration.UI.Position;

		        Position = RectTransformUtils.ClampPosition(Position, Size);

		        Anchor.Enum @enum = RectTransformUtils.PositionToAnchorType(Position);
		        Offset offset = RectTransformUtils.CalculateOffsetToAnchor(@enum, Position, Size);
		        Anchor anchor = RectTransformUtils.EnumToAnchor(@enum);

		        return (anchor, offset, Size);
	        }

	        public bool SetPosition(float2 position)
	        {
		        position = RectTransformUtils.ClampPosition(position, Size);
		        
		        if (position.Equals(Position))
			        return false;
		        
		        Position = position;
		        return true;
	        }
	        
	        public bool SetSize(float size)
	        {
		        size = Mathf.Clamp(size, 100, 400);
		        if (size == Size)
			        return false;
		        
		        Size = size;
		        return true;
	        }

	        public void GetInitial(out Vector3 position, out float rotation, out MapLayer mapLayer)
	        {
		        LastPosition = position = Player.transform.position;
		        LastRotation = rotation = Player.eyes.rotation.eulerAngles.y;
		        
		        (EnvironmentType currentEnvironment, int floor) = EnvironmentUpdate(LastPosition);
		        LastLayer = mapLayer = GetMapLayerFromEnvironment(currentEnvironment, floor);
		        _lastFloor = floor;
	        }
	        
	        public bool ShouldUpdate(out Vector3? position, out float? rotation, out MapLayer? mapLayer)
	        {
		        Vector3 currentPosition = Player.transform.position;
		        float currentRotation = Player.eyes.rotation.eulerAngles.y;
		        
		        (EnvironmentType currentEnvironment, int floor) = EnvironmentUpdate(currentPosition);
		        
		        MapLayer currentLayer = GetMapLayerFromEnvironment(currentEnvironment, floor);
		        
		        bool result = false;
		        position = null;
		        rotation = null;
		        mapLayer = null;
		        
		        if (LastPosition != currentPosition)
		        {
			        position = currentPosition;
			        LastPosition = currentPosition;
			        result = true;
		        }
		        
		        if (LastRotation != currentRotation)
		        {
			        rotation = currentRotation;
			        LastRotation = currentRotation;
			        result = true;
		        }
		        
		        if (LastLayer != currentLayer)
		        {
			        mapLayer = currentLayer;
			        LastLayer = currentLayer;
			        result = true;
		        }
		        
		        return result;
	        }
	        
	        private (EnvironmentType, int) EnvironmentUpdate(Vector3 position)
	        {
		        if (Time.realtimeSinceStartup < _nextEnvironmentUpdate)
			        return (_lastEnvironment, _lastFloor);
		        
		        (EnvironmentType environmentType, int floor) = GetCurrentEnvironment(position);
		        _nextEnvironmentUpdate = Time.realtimeSinceStartup + 1f;
		        _lastEnvironment = environmentType;
		        _lastFloor = floor;
		        return (environmentType, floor);
	        }

	        private (EnvironmentType, int) GetCurrentEnvironment(Vector3 position)
	        {
		        EnvironmentType mask = GetEnvironmentTypeAndVolumes(position);
		        if ((int)mask == 0)
			        return (EnvironmentType.Outdoor, 0);

		        if ((mask & EnvironmentType.TrainTunnels) != 0)
			        return (EnvironmentType.TrainTunnels, 0);

		        if ((mask & EnvironmentType.UnderwaterLab) != 0)
		        {
			        for (int i = 0; i < _environmentVolumes.Count; i++)
			        {
				        EnvironmentVolume environmentVolume = _environmentVolumes[i];
				        if (VolumeLookup.TryGetValue(environmentVolume, out (DungeonBaseLink dungeonBaseLink, int floor) info) && info.dungeonBaseLink)
					        return (EnvironmentType.UnderwaterLab, info.floor);
			        }

			        return (EnvironmentType.UnderwaterLab, 0);
		        }

		        return (EnvironmentType.Outdoor, 0);
	        }
	        
	        private EnvironmentType GetEnvironmentTypeAndVolumes(Vector3 position)
	        {
		        _environmentVolumes.Clear();
		        
		        EnvironmentType environmentType = EnvironmentManager.Get(position, ref _environmentVolumes, 1f);
		        
		        for (int i = 0; i < _environmentVolumes.Count; i++)
			        environmentType |= _environmentVolumes[i].Type;
		        
		        return environmentType;
	        }

	        private MapLayer GetMapLayerFromEnvironment(EnvironmentType environment, int floor)
	        {
		        switch (environment)
		        {
			        case EnvironmentType.TrainTunnels:
				        return MapLayer.TrainTunnels;
			        
			        case EnvironmentType.UnderwaterLab:
				        return floor + MapLayer.Underwater1;
			        
			        case EnvironmentType.Submarine:
				        return MapLayer.Underwater1;
			        
			        case EnvironmentType.Underground:
			        case EnvironmentType.Building:
			        case EnvironmentType.Outdoor:
			        case EnvironmentType.Elevator:
			        case EnvironmentType.PlayerConstruction:
			        case EnvironmentType.BuildingDark:
			        case EnvironmentType.BuildingVeryDark:
			        case EnvironmentType.NoSunlight:
			        default:
				        return MapLayer.Overworld;
		        }
	        }
        }

        private class RectTransformUtils
        {
	        private const float HALF_WIDTH = 640;
	        private const float HALF_HEIGHT = 360;
	        
	        public static float2 ClampPosition(float2 position, float size)
	        {
		        float halfSize = size * 0.5f;
		        
		        return new float2(
			        Mathf.Clamp(position.x, -HALF_WIDTH + halfSize, HALF_WIDTH - halfSize), 
			        Mathf.Clamp(position.y, -HALF_HEIGHT + halfSize, HALF_HEIGHT - halfSize));
	        }

	        public static Anchor.Enum PositionToAnchorType(float2 position)
	        {
		        if (position is { x: < 0, y: 0 })
			        return Anchor.Enum.CenterLeft;
			        
		        if (position is { x: > 0, y: 0 })
			        return Anchor.Enum.CenterRight;
			        
		        if (position is { x: 0, y: < 0 })
			        return Anchor.Enum.BottomCenter;
			        
		        if (position is { x: 0, y: > 0 })
			        return Anchor.Enum.TopCenter;
			        
		        if (position is { x: < 0, y: < 0 })
			        return Anchor.Enum.BottomLeft;
			        
		        if (position is { x: > 0, y: < 0 })
			        return Anchor.Enum.BottomRight;
			        
		        if (position is { x: < 0, y: > 0 })
			        return Anchor.Enum.TopLeft;
			        
		        if (position is { x: > 0, y: > 0 })
			        return Anchor.Enum.TopRight;
			        
		        return Anchor.Enum.Center;
	        }
	        
	        public static Anchor EnumToAnchor(Anchor.Enum @enum)
	        {
		        return @enum switch
		        {
			        Anchor.Enum.TopLeft => Anchor.TopLeft,
			        Anchor.Enum.TopCenter => Anchor.TopCenter,
			        Anchor.Enum.TopRight => Anchor.TopRight,
			        Anchor.Enum.CenterLeft => Anchor.CenterLeft,
			        Anchor.Enum.CenterRight => Anchor.CenterRight,
			        Anchor.Enum.BottomLeft => Anchor.BottomLeft,
			        Anchor.Enum.BottomCenter => Anchor.BottomCenter,
			        Anchor.Enum.BottomRight => Anchor.BottomRight,
			        _ => Anchor.Center
		        };
	        }
	        
	        public static float2 GetEffectiveAnchor(Anchor.Enum anchorEnum)
	        {
		        return anchorEnum switch
		        {
			        Anchor.Enum.TopRight     => new float2( HALF_WIDTH, HALF_HEIGHT),
			        Anchor.Enum.TopLeft      => new float2(-HALF_WIDTH, HALF_HEIGHT),
			        Anchor.Enum.BottomRight  => new float2( HALF_WIDTH, -HALF_HEIGHT),
			        Anchor.Enum.BottomLeft   => new float2(-HALF_WIDTH, -HALF_HEIGHT),
			        Anchor.Enum.CenterRight  => new float2( HALF_WIDTH, 0),
			        Anchor.Enum.CenterLeft   => new float2(-HALF_WIDTH, 0),
			        Anchor.Enum.TopCenter    => new float2(0, HALF_HEIGHT),
			        Anchor.Enum.BottomCenter => new float2(0, -HALF_HEIGHT),
			        _                       => new float2(0, 0),
		        };
	        }

	        public static Offset CalculateOffsetToAnchor(Anchor.Enum @enum, float2 position, float size)
	        {
		        float halfSize = size * 0.5f;
    
		        float2 effectiveAnchor = GetEffectiveAnchor(@enum);
    
		        float2 anchoredPos = position - effectiveAnchor;
    
		        float2 offMin = anchoredPos - new float2(halfSize, halfSize);
		        float2 offMax = offMin + new float2(size, size);
    
		        return new Offset(offMin.x, offMin.y, offMax.x, offMax.y);
	        }
        }

        #endregion
    }
}