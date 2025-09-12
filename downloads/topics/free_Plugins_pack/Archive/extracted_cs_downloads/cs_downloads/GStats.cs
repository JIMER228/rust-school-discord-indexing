using System.IO;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GStats", "GAGA", "1.0.1")]
    public class GStats : RustPlugin
    {
        #region [Vars]
        [PluginReference] private Plugin ImageLibrary = null;
        private string[] _gatherHooks = {
            "OnDispenserGather",
            "OnDispenserBonus",
            "OnCollectiblePickup",
        };
        private static GStats plugin;
        private const string Layer = "GStats.Layer";

        private readonly Dictionary<ulong, BasePlayer> _lastHeli = new Dictionary<ulong, BasePlayer>();
        private Dictionary<string, int> _itemIds = new Dictionary<string, int>();
        private List<ulong> _lootEntity = new List<ulong>();
        #endregion

        #region [ImageLibrary]
        private bool HasImage(string imageName, ulong imageId = 0) => (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        private bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        private string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        #endregion

        #region [Data]
        Dictionary<ulong, playerData> _playerList = new Dictionary<ulong, playerData>();

		public class playerData
		{
			public string Name;

            public int Point;

            public int PlayTimeInServer = 0;

            public int Kill = 0;

            public int Death = 0;

            public Dictionary<string, int> Gather = new Dictionary<string, int>()
            {
                { "wood", 0 },
                { "stones", 0 },
                { "metal.ore", 0 },
                { "sulfur.ore", 0},
                { "hq.metal.ore", 0 },
                { "cloth", 0},
                { "leather", 0},
                { "fat.animal", 0},
                { "loot-barrel", 0}
            };

            public int TotalFarm() => Gather.Sum(p => p.Value);
        }

		private playerData GetPlayerData(ulong member)
		{
			if (!_playerList.ContainsKey(member))
				_playerList.Add(member, new playerData());

			return _playerList[member];
		}

		private void SavePlayer()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerList", _playerList);
		}

		private void LoadPlayer()
		{
			try
			{
				_playerList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, playerData>>($"{Name}/PlayerList");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_playerList == null) _playerList = new Dictionary<ulong, playerData>();
		}
        #endregion

        #region [Oxide]
        private void OnPluginLoaded(Plugin plugin)
        {
            NextTick(() =>
            {
                foreach (string hook in _gatherHooks)
                {
                    Unsubscribe(hook);
                    Subscribe(hook);
                }
            });
        }

		private void Init()
		{
			plugin = this;

			LoadPlayer();
		}

        private void OnServerInitialized()
        {
            cmd.AddChatCommand(config.openMenuTop, this, "cmdOpenStats");

ImageLibrary?.Call("AddImage", "https://i.postimg.cc/zfNVS87c/Group-1-7.png", "ANAK");
ImageLibrary?.Call("AddImage", "https://i.postimg.cc/wxrM4hFJ/Group-1.png", "fan");
ImageLibrary?.Call("AddImage", "https://i.postimg.cc/qMCRPzZF/Group-1-8.png", "aka");
         ImageLibrary?.Call("AddImage", "https://i.postimg.cc/g2V001pq/Rectangle-23.png", "ANAL");
            AddImage("https://i.postimg.cc/RVgjKFz0/Group-8.png", $"{Name}.Online");
            AddImage("https://i.postimg.cc/QtgztNbh/Group-8-1.png", $"{Name}.Offline");
  ImageLibrary?.Call("AddImage", "https://i.postimg.cc/5tf2QpNc/Group-2.png", "ffa");
  ImageLibrary?.Call("AddImage", "https://i.postimg.cc/FKGsyc7Y/Rectangle-4.png", "аа");
            ImageLibrary?.Call("AddImage", "https://i.postimg.cc/pTTVqTFq/Group-1-9.png", "ff");
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            if (config._NotifyChatRandom.chatSendTop)
                timer.Every(config._NotifyChatRandom.chatSendTopTime, GetRandomTopPlayer);
            timer.Every(60, TimeHandle);
        }

		private void Unload()
		{
            SavePlayer();

			foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }

			plugin = null;
		}

        private void OnServerSave() => SavePlayer();

		private void OnNewSave(string filename)
		{
            WipeEnded();
		}
        #endregion

        #region [Reward]
        private void WipeEnded()
        {
            if(config._GameStoreSettings.GivePrize && !string.IsNullOrEmpty(config._GameStoreSettings.ShopID) && !string.IsNullOrEmpty(config._GameStoreSettings.SecretKey))
            {
                var sortedData = _playerList.OrderByDescending(x => x.Value.Point);
                int pos = 1;

                foreach (var user in sortedData)
                {
                    if (config._GameStoreSettings.RewardSettings.ContainsKey(pos))
                    {
                        var args = new Dictionary<string, string>()
                        {
                            { "action", "moneys" },
                            { "type", "plus" },
                            { "steam_id", user.Key.ToString() },
                            { "amount", config._GameStoreSettings.RewardSettings[pos].ToString() }
                        };
                        string url = $"https://gamestores.ru/api/?shop_id={config._GameStoreSettings.ShopID}&secret={config._GameStoreSettings.SecretKey}" + $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
                        webrequest.Enqueue(url, null, (i, s) =>
                        {
                            if (i != 200)
                            {
                                PrintError($"Ошибка {i}: {s}");
                                return;
                            }
                        }, this);
                    }
                    pos++;
                }
            }

            foreach (var playerData in _playerList)
            {
                playerData.Value.Point = 0;
                playerData.Value.PlayTimeInServer = 0;
                playerData.Value.Kill = 0;
                playerData.Value.Death = 0;
                playerData.Value.Gather = new Dictionary<string, int>()
                {
                    ["wood"] = 0,
                    ["stones"] = 0,
                    ["metal.ore"] = 0,
                    ["hq.metal.ore"] = 0,
                    ["sulfur.ore"] = 0,
                    ["cloth"] = 0,
                    ["leather"] = 0,
                    ["fat.animal"] = 0,
                    ["loot-barrel"] = 0
                };
            }
            SavePlayer();
        }
        #endregion

        #region [Gui]
        private void PlayerTop(BasePlayer player)
        {
            #region [Vars]
            var container = new CuiElementContainer();
            #endregion

            #region [Parrent]
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Color = "0 0 0 0.7" }
            }, "Overlay", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.35", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, Layer);

           
            #endregion

            #region [Button]
               container.Add(new CuiElement
            {   
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "fan"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent {  AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "288 220", OffsetMax = "395 252" }
                }
            });
	        container.Add(new CuiButton
	        {
		        Button = { Color = "0 0 0 0", Close = Layer },
		        Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
		        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "288 220", OffsetMax = "395 252" }
	        },Layer);
     container.Add(new CuiElement
            {   
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "aka"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "144 -249", OffsetMax = "300 -218" }
                }
            });
	        container.Add(new CuiButton
	        {
		        Button = { Color = "0 0 0 0", Command = $"UI_GStats OpenProfileStats {player.userID}" },
		        Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
		        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "144 -249", OffsetMax = "300 -218" }
	        },Layer);

            #endregion

            #region [Main-Gui]
           
             container.Add(new CuiElement
            {   Name = Layer + ".Top",
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "ANAK"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent {  AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-393 -210", OffsetMax = "395 209" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48 -208", OffsetMax = "395 209" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Profile");

        
            #endregion

            #region [Text-Point]
            Dictionary<string, string> _pointScoreGive = new Dictionary<string, string>()
            {
                { "Сбитие вертолёта", $"<color=#08C05D>+{config._PointsDestroy.dHeli} очков</color>" },
                { "Уничтожение танка", $"<color=#08C05D>+{config._PointsDestroy.dBradley} очков</color>" },
                { "Убийство игрока", $"<color=#08C05D>+{config._PointsKillDeath.pKill} очков</color>" },
                { "Добыча камня", $"<color=#08C05D>+{config._PointsSettings.pStone} очков</color>" },
                { "Добыча металла", $"<color=#08C05D>+{config._PointsSettings.pMetal} очков</color>" },
                { "Добыча серы", $"<color=#08C05D>+{config._PointsSettings.pSulfur} очков</color>" },
                { "Разрушение бочки", $"<color=#08C05D>+{config._PointsSettings.pBarrel} очков</color>" }
            };

            container.Add(new CuiLabel
            {
                Text = { Text = $"Получение очков", Color = "1 1 1 0.85", FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = $"-0.05 0", AnchorMax = $"1.08 0.473" },
            }, Layer + ".Profile");

            foreach (var check in _pointScoreGive.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".Profile",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Key}", Color = "1 1 1 0.65", Align = TextAnchor.UpperLeft, FontSize = 8, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.375 0", AnchorMax = $"0.995 {0.423 - Math.Floor((float) check.B / 1) * 0.0325}", },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Profile",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value}", Color = "1 1 1 0.65", Align = TextAnchor.UpperLeft, FontSize = 8, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.58 0", AnchorMax = $"0.995 {0.423 - Math.Floor((float) check.B / 1) * 0.0325}", },
                    }
                });
            }

            Dictionary<string, string> _pointScoreRemove = new Dictionary<string, string>()
            {
                { "Смерть", $"<color=#FF4400FF>-{config._PointsKillDeath.pDeath} очков</color>" },
                { "Самоубийство", $"<color=#FF4400FF>-{config._PointsKillDeath.pSuicide} очков</color>" },
            };

            container.Add(new CuiLabel
            {
                Text = { Text = $"Лишение очков", Color = "1 1 1 0.85", FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = $"-0.05 0", AnchorMax = $"1.08 0.19" },
            }, Layer + ".Profile");

            foreach (var check in _pointScoreRemove.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".Profile",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Key}", Color = "1 1 1 0.65", Align = TextAnchor.UpperLeft, FontSize = 8, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.375 0", AnchorMax = $"0.845 {0.142 - Math.Floor((float) check.B / 1) * 0.0325}", },
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Profile",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{check.A.Value}", Color = "1 1 1 0.65", Align = TextAnchor.UpperLeft, FontSize = 8, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.58 0", AnchorMax = $"0.995 {0.142 - Math.Floor((float) check.B / 1) * 0.0325}", },
                    }
                });
            }
            #endregion

            #region [Text Top]
          
            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
            PlayerTopInfo(player, player.userID);
            TopPlayerList(player);
        }

        private void TopPlayerList(BasePlayer player, int page = 0)
        {
            #region [Vars]
            var playerList = _playerList.OrderByDescending(p => p.Value.Point);
            var container = new CuiElementContainer();
            int i = 1 + (page * 10);
            #endregion

            #region [Main]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".Top", Layer + ".Top" + ".Layer");
            #endregion

            #region [Button]
                container.Add(new CuiElement
            {   
                Parent = Layer + ".Top",
                Name = ".Layer",
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "аа"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = $"0.330 -0.097", AnchorMax = $"0.357 -0.028" }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.180 -0.097", AnchorMax = $"0.507 -0.028" },
                Text = { Text = $"{page + 1}", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, Layer + ".Top" + ".Layer");
       container.Add(new CuiElement
            {   
                Parent = Layer + ".Top",
                Name = ".Layer",
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "ff"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent {  AnchorMin = $"0.383 -0.097", AnchorMax = $"0.45 -0.028" }
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = playerList.Skip(10 * (page + 1)).Count() > 0 ? $"UI_GStats ChangeTopPage {page + 1}" : "" },
                Text = { Text = "", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = {AnchorMin = $"0.010 -0.097", AnchorMax = $"1.0 1.128" },
            }, ".Layer");
   container.Add(new CuiElement
            {   
                Parent = Layer + ".Top",
                Name = ".Layer",
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "ffa"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = $"0.233 -0.097", AnchorMax = $"0.3 -0.028" }
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page > 0 ? $"UI_GStats ChangeTopPage {page - 1}" : "" },
                Text = { Text = "", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"0.010 -0.097", AnchorMax = $"1.0 1.128" },
            }, ".Layer");
            #endregion

            #region [PlayerInfo]
            foreach (var check in playerList.Select((y, t) => new { A = y, B = t - page * 10 }).Skip(page * 10).Take(10))
            {
            
                     container.Add(new CuiElement
            {   Name = Layer + ".Top" + ".Layer" + $".playerInfo{check.B}",
                Parent = Layer + ".Top" + ".Layer",
                Components =
                {
                    new CuiRawImageComponent() { Png = (string)ImageLibrary?.Call("GetImage", "ANAL"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent {  AnchorMin = $"0.01 {0.735 - Math.Floor((float) check.B/ 1) * 0.081}", AnchorMax = $"0.58 {0.803 - Math.Floor((float) check.B / 1) * 0.081}" }
                }
            });

                container.Add(new CuiLabel
                {
                    Text = { Text = $"#{i}", Color = "1 1 1 1", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"-0.07 0", AnchorMax = $"0.24 1" },
                }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.A.Value.Name}", Color = "1 1 1 1", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.17 0", AnchorMax = "0.38 1" },
                }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");

                container.Add(new CuiLabel
                {
                    Text = { Text = BasePlayer.FindByID(check.A.Key) != null ? "<color=#08C05D>Online</color>" : "<color=#FF4400FF>Offline</color>", Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.385 0", AnchorMax = "0.54 1" },
                }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.A.Value.Point}", Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.6 0", AnchorMax = "0.76 1" },
                }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");

                if (config._GameStoreSettings.RewardSettings.ContainsKey(i))
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{config._GameStoreSettings.RewardSettings[i]}", Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0.75 0", AnchorMax = "1.06 1" },
                    }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");
                }

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"UI_GStats OpenProfileStats {check.A.Key}" },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                }, Layer + ".Top" + ".Layer" + $".playerInfo{check.B}");

                i++;
            }
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Top" + ".Layer");
            CuiHelper.AddUi(player, container);
        }

        private void PlayerTopInfo(BasePlayer player, ulong playerID)
        {
            #region [Vars]
            var container = new CuiElementContainer();

            var data = GetPlayerData(playerID);
            if (data == null) return;

            bool isOnline = BasePlayer.FindByID(playerID) != null ? true : false;
            #endregion

            #region [Main]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.022 0.465", AnchorMax = "0.997 0.918" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".Profile", Layer + ".Profile" + ".Layer");
            #endregion

            #region [Avatar]
            container.Add(new CuiElement
            {
                Name = Layer + ".Profile" + ".Layer" + ".Avatar",
                Parent = Layer + ".Profile" + ".Layer",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage($"avatar_{playerID}") },
                    new CuiRectTransformComponent { AnchorMin = $"0.365 0.22", AnchorMax = $"0.642 0.74" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Profile" + ".Layer" + ".Avatar",
                Components =
                {
                    new CuiRawImageComponent { Png = isOnline == true ? GetImage($"{Name}.Online") : GetImage($"{Name}.Offline") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            #endregion

            #region [Text]
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 0", AnchorMax = $"1 0.9" },
                Text = { Text = $"{data.Name}", Align = TextAnchor.UpperCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 0", AnchorMax = $"0.34 0.74" },
                Text = { Text = $"<b>Место в топе</b>\n<size=8>#{GetTopScore(playerID)}</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 0", AnchorMax = $"0.34 0.59" },
                Text = { Text = $"<b>Кол-во очков</b>\n<size=8>{data.Point} шт</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 0", AnchorMax = $"0.34 0.43" },
                Text = { Text = $"<b>Время в игре</b>\n<size=8>{data.PlayTimeInServer} мин.</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.642 0", AnchorMax = $"1 0.74" },
                Text = { Text = $"<b>Кол-во убийств</b>\n<size=8>{data.Kill}</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.642 0", AnchorMax = $"1 0.59" },
                Text = { Text = $"<b>Кол-во смертей</b>\n<size=8>{data.Death}</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.642 0", AnchorMax = $"1 0.43" },
                Text = { Text = $"<b>K/D</b>\n<size=8>{(data.Death == 0 ? data.Kill : (float)Math.Round(((float)data.Kill) / data.Death, 2))}</size>", Align = TextAnchor.UpperCenter, FontSize = 9, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Profile" + ".Layer");
            #endregion

            #region [Resourse]
            foreach (var check in data.Gather.OrderByDescending(x => x.Value).Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.185 + check.B * 0.088 - Math.Floor((float) check.B / 9) * 9 * 0.068} 0.085",
                                        AnchorMax = $"{0.26 + check.B * 0.088 - Math.Floor((float) check.B / 9) * 9 * 0.068} 0.187", },
                    Image = { Color = "0 0 0 0.35" }
                }, Layer + ".Profile" + ".Layer", Layer + ".Profile" + ".Layer" + $".{check.B}");

                if (FindItemID(check.A.Key) != 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Profile" + ".Layer" + $".{check.B}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(check.A.Key), SkinId = 0 },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Profile" + ".Layer" + $".{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage(check.A.Key) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Profile" + ".Layer" + $".{check.B}",
                    Components =
                    {
                        new CuiTextComponent { Text = GetFarm(check.A.Value), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"-0.2 -0.25", AnchorMax = $"1.2 0.25" },
                    }
                });
            }
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Profile" + ".Layer");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region [Connect]
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            GetAvatar(player.UserIDString, avatar => AddImage(avatar, $"avatar_{player.UserIDString}"));

            var data = GetPlayerData(player.userID);
            if (data == null || string.IsNullOrEmpty(player.displayName)) return;

            var Name = covalence.Players.FindPlayerById(player.UserIDString)?.Name;
            if (data.Name != Name)
                data.Name = Name;
        }
        #endregion

        #region [Gather]
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
            if (!entity.ToPlayer() || entity == null || item == null) return;

            var player = entity.ToPlayer();
            if (player == null || player.IsNpc) return;

            AddResourse(player, item.info.shortname, item.amount);
		}

		private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
            if (!entity.ToPlayer() || entity == null || item == null) return;

            var player = entity.ToPlayer();
            if (player == null || player.IsNpc) return;

            AddResourse(player, item.info.shortname, item.amount, true);
		}

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
			if (player == null || collectible == null || collectible.itemList == null) return;

			foreach (var itemAmount in collectible.itemList)
            {
			    if (itemAmount.itemDef != null)
                {
                    AddResourse(player, itemAmount.itemDef.shortname, (int)itemAmount.amount);
                }
            }
		}
        #endregion

        #region [Entity]
		private void OnEntityTakeDamage(PatrolHelicopter entity, HitInfo info)
		{
			if (entity != null && entity.net != null && info.InitiatorPlayer != null)
				_lastHeli[entity.net.ID.Value] = info.InitiatorPlayer;
		}

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return;

			if (entity is PatrolHelicopter)
			{
				if (_lastHeli.ContainsKey(entity.net.ID.Value))
				{
                    var dataHeli = GetPlayerData(_lastHeli[entity.net.ID.Value].userID);
                    if (dataHeli == null) return;
                    dataHeli.Point += config._PointsDestroy.dHeli;
				}
				return;
			}

			var player = info.InitiatorPlayer;
			if (player == null) return;

            var data = GetPlayerData(player.userID);
            if (data == null) return;

            if (entity is BradleyAPC)
            {
                data.Point += config._PointsDestroy.dBradley;
            }
            else if (entity.name.Contains("barrel"))
            {
                data.Point += config._PointsSettings.pBarrel;
                data.Gather["loot-barrel"]++;
            }
		}
        #endregion

        #region [Loot]
        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (player == null || entity == null || entity?.net?.ID == null || _lootEntity.Contains(entity.net.ID.Value)) return;

            var data = GetPlayerData(player.userID);
            if (data == null) return;

            data.Point += config._PointsSettings.pBarrel;
            data.Gather["loot-barrel"]++;
            
            _lootEntity.Add(entity.net.ID.Value);
        }
        #endregion

        #region [Death]
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || !player.userID.IsSteamId()) return;
            
            if (info.damageTypes.Has(DamageType.Suicide))
            {
                var data = GetPlayerData(player.userID);
                if (data == null) return;
                
                data.Point -= config._PointsKillDeath.pSuicide;
                data.Death++;
                return;
            }
            
            var attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId() || IsTeammates(player.userID, attacker.userID)) return;

            if (player.userID.IsSteamId())
            {
                var data = GetPlayerData(player.userID);
                if (data != null)
                {
                    data.Point -= config._PointsKillDeath.pDeath;
                    data.Death++;
                }

                var dataAttacker = GetPlayerData(attacker.userID);
                if (dataAttacker != null)
                {
                    dataAttacker.Point += config._PointsKillDeath.pKill;
                    dataAttacker.Kill++;
                }
            }
        }
        #endregion

        #region [ConsoleCommand]
        private void cmdOpenStats(BasePlayer player) => PlayerTop(player);

        [ConsoleCommand("UI_GStats")]
        private void StatsUIHandler(ConsoleSystem.Arg args)
        {
			BasePlayer player = args?.Player();
			if (player == null || !args.HasArgs()) return;

            switch (args.Args[0])
            {
                case "OpenProfileStats":
                {
                    PlayerTopInfo(player, ulong.Parse(args.Args[1]));
                    break;
                }
                case "ReturnToPlayerTop":
                {
                    PlayerTop(player);
                    break;
                }
                case "ChangeTopPage":
                {
                    TopPlayerList(player, int.Parse(args.Args[1]));
                    break;
                }
            }
        }
        #endregion

		#region [Avatar]
		private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");
		private void GetAvatar(string userId, Action<string> callback)
		{
			if (callback == null) return;

			try
			{
				webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
				{
					if (code != 200 || response == null)
						return;

					var avatar = Regex.Match(response).Groups[1].ToString();
					if (string.IsNullOrEmpty(avatar))
						return;

					callback.Invoke(avatar);
				}, this);
			}
			catch (Exception e)
			{
				PrintError($"{e.Message}");
			}
		}
        #endregion

        #region [NotifyChat]
        private void GetRandomTopPlayer()
        {
            int random = Core.Random.Range(0, 9);

            switch (random)
            {
                case 0:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Kill).Take(5);
                    int i = 1;
                    string message = "<size=17>Топ игроков по убийствам:</size>\n";
                    foreach (var key in playerList)
                    {
                        message += $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Kill}</color></size>";
                        i++;
                    }
                    Server.Broadcast($"{message}");
                    break;
                }
                case 1:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Death).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Больше всего смертей:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Death}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 2:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.TotalFarm()).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Больше всего фарма:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.TotalFarm()}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 3:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Gather["hq.metal.ore"]).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Добыто МВК:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Gather["hq.metal.ore"]}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 4:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Gather["metal.ore"]).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Добыто Металла:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Gather["metal.ore"]}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 5:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Gather["sulfur.ore"]).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Добыто Серы:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Gather["sulfur.ore"]}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 6:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Gather["loot-barrel"]).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Добыто бочек и залутно ящиков:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Gather["loot-barrel"]}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 7:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.PlayTimeInServer).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Проведено больше всего время на сервере:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{FormatShortTime(TimeSpan.FromSeconds(key.Value.PlayTimeInServer * 60))}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
                case 8:
                {
                    var playerList = _playerList.OrderByDescending(p => p.Value.Point).Take(5);
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        int i = 1;
                        ServerBroadcast(player, "<size=16><color=#70C3F8>Больше всего очков:</color></size>", 0);
                        foreach (var key in playerList)
                        {
                            ServerBroadcast(player, $"<size=14>{i}.{key.Value.Name} - <color=#70C3F8>{key.Value.Point}</color></size>", key.Key);
                            i++;
                        }
                    }
                    break;
                }
            }
        }

        private void ServerBroadcast(BasePlayer player, string message, ulong AvatarID)
        {
            if (player == null || string.IsNullOrEmpty(message)) return;

            Player.Message(player, $"{message}", AvatarID);
        }

        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{time.Days} д. ";

            if (time.Hours != 0)
                result += $"{time.Hours} час. ";

            if (time.Minutes != 0)
                result += $"{time.Minutes} мин. ";

            if (time.Seconds != 0)
                result += $"{time.Seconds} сек. ";

            return result;
        }
        #endregion

        #region [Functional]
        private string GetFarm(int value)
        {
            if (value >= 1000000)
                return $"{(float)Math.Round((float)value / 1000000, 2)}M";
            if (value >= 1000)
                return $"{(float)Math.Round((float)value / 1000, 2)}K";
            return $"{value}";
        }

		private bool IsTeammates(ulong player, ulong friend)
		{
			return player == friend ||
			       RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true;
		}

		private void TimeHandle()
		{
            foreach (var player in BasePlayer.activePlayerList)
            {
                var data = GetPlayerData(player.userID);
                if (data == null) continue;
                
                data.PlayTimeInServer++;
            }
		}

        private int GetTopScore(ulong userid)
        {
            int Top = 1;
            var RaitingNumber = _playerList.OrderByDescending(x => x.Value.Point);

            foreach (var Data in RaitingNumber)
            {
                if (Data.Key == userid)
                    break;
                Top++;
            }

            return Top;
        }

		private int FindItemID(string shortName)
		{
			int val;
			if (_itemIds.TryGetValue(shortName, out val))
				return val;

			var definition = ItemManager.FindItemDefinition(shortName);
			if (definition == null) return 0;

			val = definition.itemid;
			_itemIds[shortName] = val;
			return val;
		}
        #endregion

        #region [AddResourse]
        private void AddResourse(BasePlayer player, string shortname, int amount, bool GivePoint = false)
        {
            if (player == null || string.IsNullOrEmpty(shortname) || amount <= 0) return;

            var data = GetPlayerData(player.userID);
            if (data == null) return;

            switch (shortname)
            {
                case "wood":
                {
                    data.Gather[shortname] += amount;
                    if (GivePoint)
                    {
                        data.Point += config._PointsSettings.pWood;
                    }
                    break;
                }
                case "stones":
                {
                    data.Gather[shortname] += amount;
                    if (GivePoint)
                    {
                        data.Point += config._PointsSettings.pStone;
                    }
                    break;
                }
                case "metal.ore": case "metal.fragments":
                {
                    data.Gather["metal.ore"] += amount;
                    if (GivePoint)
                    {
                        data.Point += config._PointsSettings.pMetal;
                    }
                    break;
                }
                case "sulfur.ore": case "sulfur":
                {
                    data.Gather["sulfur.ore"] += amount;
                    if (GivePoint)
                    {
                        data.Point += config._PointsSettings.pMetal;
                    }
                    break;
                }
                case "hq.metal.ore": case "metal.refined":
                {
                    data.Gather["hq.metal.ore"] += amount;
                    break;
                }
                case "leather":
                {
                    data.Gather[shortname] += amount;
                    break;
                }
                case "cloth":
                {
                    data.Gather[shortname] += amount;
                    break;
                }
                case "fat.animal":
                {
                    data.Gather[shortname] += amount;                 
                    break;
                }
            }
        }
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(1, 1, 1))
                {
                    config._NotifyChatRandom.chatSendTop = true;
                    config._NotifyChatRandom.chatSendTopTime = 1200;
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        public class PointsSettings
        {
            [JsonProperty("Сколько давать очков за дерево")]
            public int pWood = 5;

            [JsonProperty("Сколько давать очков за каменный камень")]
            public int pStone = 5;

            [JsonProperty("Сколько давать очков за металический камень")]
            public int pMetal = 5;

            [JsonProperty("Сколько давать очков за серный камень")]
            public int pSulfur = 5;

            [JsonProperty("Сколько давать очков за уничтожение бочки | Лутание обычного ящика у дороги")]
            public int pBarrel = 5;
        }

        public class PointsDestroy
        {
            [JsonProperty("Сколько давать очков за уничтожение вертолета")]
            public int dHeli = 1500;

            [JsonProperty("Сколько давать очков за уничтожение танка")]
            public int dBradley = 750;
        }

        public class PointsKillDeath
        {
            [JsonProperty("Сколько давать очков за убийство игрока")]
            public int pKill = 40;

            [JsonProperty("Сколько отнимать очков за смерть")]
            public int pDeath = 15;

            [JsonProperty("Сколько отнимать очков за суицид")]
            public int pSuicide = 15;
        }

        public class GameStoreSettings
        {
            [JsonProperty("Включить авто выдачу призов при вайпе сервера?")]
            public bool GivePrize = true;

            [JsonProperty("ИД магазина в сервисе")] 
            public string ShopID = "";

            [JsonProperty("Секретный ключ (не распростраяйте его)")] 
            public string SecretKey = "";

            [JsonProperty("Место в топе и выдаваемый баланс игроку")]
            public Dictionary<int, float> RewardSettings;
        }

        public class NotifyChatRandom
        {
            [JsonProperty("Отправлять в чат сообщения с топ 5 игроками ?")]
            public bool chatSendTop = true;

            [JsonProperty("Раз в сколько секунд будет отправлятся сообщение ?")]
            public int chatSendTopTime = 1200;
        }

        private class PluginConfig
        {
            [JsonProperty("Команда для открытия топа")]
            public string openMenuTop;

            [JsonProperty("Настройка начисления очков за добычу")]
            public PointsSettings _PointsSettings = new PointsSettings();

            [JsonProperty("Настройка начисления очков за уничтожение")]
            public PointsDestroy _PointsDestroy = new PointsDestroy();

            [JsonProperty("Настройка начисления и отнимания очков за убийства и смерти")]
            public PointsKillDeath _PointsKillDeath = new PointsKillDeath();

            [JsonProperty("Настройка призов")]
            public GameStoreSettings _GameStoreSettings = new GameStoreSettings();

            [JsonProperty("Настройка оповещений в чате")]
            public NotifyChatRandom _NotifyChatRandom = new NotifyChatRandom();

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    openMenuTop = "top",
                    _PointsDestroy = new PointsDestroy()
                    {
                        dHeli = 1500,
                        dBradley = 750,
                    },
                    _PointsKillDeath = new PointsKillDeath()
                    {
                        pKill = 40,
                        pDeath = 15,
                        pSuicide = 15,
                    },
                    _PointsSettings = new PointsSettings()
                    {
                        pWood = 5,
                        pStone = 5,
                        pMetal = 5,
                        pSulfur = 5,
                        pBarrel = 5,
                    },
                    _GameStoreSettings = new GameStoreSettings()
                    {
                        GivePrize = true,
                        ShopID = "",
                        SecretKey = "",
                        RewardSettings = new Dictionary<int, float>()
                        {
                            [1] = 400f,
                            [2] = 250f,
                            [3] = 150f,
                            [4] = 100f,
                            [5] = 50f,
                            [6] = 50f,
                            [7] = 30f,
                        },
                    },
                    _NotifyChatRandom = new NotifyChatRandom()
                    {
                        chatSendTop = true,
                        chatSendTopTime = 1200,
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion
    }
}