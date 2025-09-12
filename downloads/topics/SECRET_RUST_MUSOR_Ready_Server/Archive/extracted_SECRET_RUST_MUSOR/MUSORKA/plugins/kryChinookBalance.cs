using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;
using Rust.UI;
using UnityEngine.Assertions;
using Oxide.Core.Libraries.Covalence;
using System.Net;

namespace Oxide.Plugins
    ///
    /// 1.0.7:
    /// Added ticket with added a ticket with points for the clan
    /// 
{   ///
    /// 1.0.4:
    /// Added money in chinookcrate - settings in config
    /// 
    ///
    /// 1.0.2:
    /// Added support for loot plugins
    /// 
    [Info("kryChinookBalance", "", "1.0.7")]
    [Description("crate with ticket")]
    class kryChinookBalance : RustPlugin
    {
        [PluginReference] private Plugin Clans, ImageLibrary;

        public float FadeIn = 1f;
        public float FadeOut = 0.25f;

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static int CurrentTime() => (int)DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        private const string Layer = "ui.kryChinookBalance.bg";
        private const string LayerStatus = "ui.kryChinookBalance.status";
        private bool errorState = false;
        private List<ulong> _opened = new List<ulong>();
        private int endTime = 0;
        private Vector3 cratePosition;
        private HackableLockedCrate activeCrate;
        private int nextCrate = 0;
        private bool isFirst = true;
        private int cratetime = (int)HackableLockedCrate.requiredHackSeconds;

        private Dictionary<string, string> Images = new Dictionary<string, string>
        {
            ["Bilet_"] = "https://i.imgur.com/9ksfvbA.png"
        };

        #region Data

        private PluginData _data;

        private void _SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void _LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Hided Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> HidedPlayers = new List<ulong>();

            public bool IsHided(BasePlayer player)
            {
                if (player != null)
                    return HidedPlayers.Contains(player.userID);
                return false;
            }

            public bool ChangeStatus(BasePlayer player)
            {
                if (player == null) return false;

                if (IsHided(player))
                {
                    HidedPlayers.Remove(player.userID);
                    return false;
                }

                HidedPlayers.Add(player.userID);
                return true;
            }
        }

        #endregion


        #region Methods

        private void OnServerInitialized()
        {
            LoadData();
            _LoadData();
            nextCrate = cfg.spawnRate + CurrentTime();
            InvokeHandler.Instance.InvokeRepeating(SpawnChinookToPos, cfg.spawnRate, cfg.spawnRate);
            foreach (var check in Images)
                ImageLibrary.Call("AddImage", check.Value, check.Key);
            AddCovalenceCommand("balanceopen", nameof(CmdMenuHide));
            CreateSpawnGrid();
        }

        private void CmdMenuHide(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            _data.ChangeStatus(player);
           
            MainUI(player);
          
            
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if(activeCrate != null)
            {
                if(!activeCrate.IsDestroyed)
                {
                    MainUI(player);
                    
                }
            }
        }

        private void SpawnChinookToPos()
        {
            if (cratePosition == Vector3.zero)
                return;

            if (activeCrate != null)
                if (!activeCrate.IsDestroyed)
                    return;
            var crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", cratePosition, Quaternion.identity);
            crate.Spawn();
            NextTick(() =>
            {
                nextCrate = cfg.spawnRate + CurrentTime();
                crate._name = "kryChinook_crate";
                crate.gameObject.GetComponent<HackableLockedCrate>().StartHacking();
                activeCrate = crate.gameObject.GetComponent<HackableLockedCrate>();
                endTime = cratetime + CurrentTime();
                var lc = crate.gameObject.GetComponent<LootContainer>();
                lc.inventory.Clear();
                AddBalanceList(lc);

                
               


                foreach (var x in BasePlayer.activePlayerList)
                {
                    MainUI(x);
                    NotifyUI(x, string.Format($"Чинук с очками для кланов\n<b><size=12>Ивент чинук с очками начался</size></b>"));
                    Effect.server.Run("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB", x, 0, Vector3.zero, Vector3.forward);

                }
                Server.Broadcast("Начался ивент : CHINOOK POINTS\nВнутри чинука лежит билет на 250 очков клана\nЧинук появился на карте!");
                Invoke();
                
            });
        }


        private static Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        private void CreateSpawnGrid()
        {
            Grids.Clear();
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (0.0066666666666667f * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz + 20f));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos)
        {
            return Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        }


        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;
            if (info.InitiatorPlayer == null || !info.InitiatorPlayer.userID.IsSteamId())
                return null;
            if (entity is HackableLockedCrate) return false;
            return null;
        }



      
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker== null || info == null) return;
            if (info.HitEntity is HackableLockedCrate) return;
        }


        void MainUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-44 -27", OffsetMax = "-3 10" },
            }, "Hud", LayerStatus);

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.8 0.8 0.8 0.1", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -20", OffsetMax = "20 20" },
            }, LayerStatus, "EventBG");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 1", Png = (string)ImageLibrary.Call("GetImage", "Bilet_") },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
            }, "EventBG", "EventIMG");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "" },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "balanceopen"
                }

            }, "EventBG");

            if (!_data.IsHided(player))
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.8 0.8 0.8 0.1", Material = "assets/icons/greyout.mat" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-225 -20", OffsetMax = "-24 20" },
                }, LayerStatus, ".Info");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = HexToRustFormat("#FF000056") },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "86.5 5", OffsetMax = "100.5 20" },
                }, ".Info", ".Close");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-2 0" },
                    Text = { Text = "✕", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11, Color = "1 1 1 0.85" },
                    Button =
                {
                    Color = "0 0 0 0",
                    Command = "balanceopen"
                }

                }, ".Close");

                var time = TimeSpan.FromSeconds(endTime - CurrentTime());

                
                
                    container.Add(new CuiElement
                    {
                        Parent = LayerStatus,
                        Name = LayerStatus + ".Text",
                        Components =
                {
                    new CuiTextComponent {Text = string.Format($"CHINOOK POINTS ({GetFormatTime(time)})\n<b><size=12>Квадрат:{GetNameGrid(cratePosition)}</size></b>"), Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 15, Color = HexToRustFormat("#FFFFFF99") },
                    new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-223 -20", OffsetMax = "-40 20" },
                }
                    });
               

            }




            CuiHelper.DestroyUi(player, LayerStatus);
            CuiHelper.AddUi(player,container);
        }

        private string GetFormatTime(TimeSpan timespan)
        {
            if (timespan.TotalSeconds > 0)
            {
                return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
            }
            else return "0:00";
        }


    


        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if(player == null) return;
            if(entity == null) return;
            if(entity == activeCrate)
            {
                if(activeCrate.inventory.IsEmpty())
                    Server.Broadcast($"{player.displayName} первым открыл чинук с билетами!<color=green>");
            }
        }


        double timeline = 0;
        private IEnumerator ChinookUI()
        {
            while (true)
            {
                if (activeCrate == null)
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        NotifyUI(player,"Чинук с поинтами на фарм острове был залутан.");
                    }
                    DestroyUI();
                    break;
                }
                CuiElementContainer container = new CuiElementContainer();
                CuiElementContainer c = new CuiElementContainer();
                var time = TimeSpan.FromSeconds(endTime - CurrentTime());


                container.Add(new CuiElement
                {
                    Parent = LayerStatus,
                    Name = LayerStatus + ".Text",
                    Components =
                {
                    new CuiTextComponent {Text = string.Format($"CHINOOK POINTS ({GetFormatTime(time)})\n<b><size=12>Квадрат:{GetNameGrid(cratePosition)}</size></b>"), Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 15, Color = HexToRustFormat("#FFFFFF99") },
                    new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-223 -20", OffsetMax = "-40 20" },
                }
                });



                timeline++;

                var color = HexToRustFormat("#FFFFFFFF");

                
                c.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{1 - (timeline/cratetime)} 1", OffsetMin = "0 0", OffsetMax = "0 -37" },
                }, "EventBG", "LineStatus");

                foreach (var x in BasePlayer.activePlayerList.Where(x => !_data.IsHided(x)))
                {
                    CuiHelper.DestroyUi(x, LayerStatus + ".Text");
                    CuiHelper.AddUi(x, container);
                }

                foreach (var z in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(z, "LineStatus");
                    CuiHelper.AddUi(z, c);
                }

                yield return new WaitForSeconds(1f);
            }
        }

        void NotifyUI(BasePlayer player, string text)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = "0.8 0.8 0.8 0.1", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -135", OffsetMax = "260 -75" },
            }, "Hud", Layer);

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = HexToRustFormat("#FFFFFF51") },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 5", OffsetMax = "55 55" },
            }, Layer, ".CrateBG");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = "255 255 255 255", Png = (string)ImageLibrary.Call("GetImage", "Bilet_") },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
            }, ".CrateBG", ".CrateIMG");

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Text",
                FadeOut = FadeOut,
                Components =
                {
                    new CuiTextComponent {FadeIn = FadeIn, Text = $"<b>{text}</b>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 13, Color = HexToRustFormat("#FFFFFF99") },
                    new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "60 5", OffsetMax = "250 55" },
                }
            });

            

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = FadeOut,
                Image = { Color = HexToRustFormat("#FF00005B") },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "116 15", OffsetMax = "130 30" },
            }, Layer, ".CrateClose");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-2 0", OffsetMax = "0 0" },
                Text = { Text = "✕", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11, Color = "1 1 1 0.85"},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer
                }

            }, ".CrateClose");

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
            timer.Once(5, () => 
            {
                CuiHelper.DestroyUi(player, Layer);
            });

        }

        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(SpawnChinookToPos);
            activeCrate?.Kill(BaseNetworkable.DestroyMode.None);
            ServerMgr.Instance.StopAllCoroutines();
            _SaveData();
            DestroyUI();
           
        }
        private void AddBalanceList(LootContainer crate)
        {
            if (crate == null)
            {
                Puts("crate is null");
                return;
            }
            if (UnityEngine.Random.Range(0f, 100f) < cfg.chanceToDrop)
            {
                // crate.GiveItem(GetRandomBalanceItem());
                GetRandomBalanceItem().MoveToContainer(crate.inventory, allowStack: false);
            }
        }
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (cfg._balanceItems.FirstOrDefault(x => x.displayName == item.name && x.skinid == item.skin) != null)
            {
                if (action == "unwrap")
                {
                    if (errorState == true)
                        return false;

                    var clan = Clans.Call("GetClanOf", player);
                    if (clan == null)
                    {
                        SendReply(player, "Для получения очков клана необходимо находиться в клане.");
                        return false;
                    }

                    if(clan != null)
                    {
                        AddPoints(player, cfg._balanceItems.FirstOrDefault(x => x.displayName == item.name && x.skinid == item.skin).balanceAdd);
                        SendReply(player, "Вы успешно забрали очки на баланс клана");
                        item.amount -= 1;
                        item.RemoveFromContainer();
                        // item.RemoveFromWorld();
                        return false;
                    }
                }
            }
            return null;
        }

        private void AddPoints(BasePlayer player, int amount)
        {
            var clan = Clans.Call("GetClanOf", player);
            Clans.Call("AddClanPoints", Convert.ToString(clan), amount);
        }
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        private Item GetRandomBalanceItem()
        {
            var randomIndex = new System.Random().Next(0, cfg._balanceItems.Count() - 1);
            Item item = ItemManager.CreateByName("xmas.present.small", 1, cfg._balanceItems[randomIndex].skinid);
            item.name = cfg._balanceItems[randomIndex].displayName;
            item.GetHeldEntity()?.SendNetworkUpdate();
            return item;
        }
        private void Invoke()
        {
            ServerMgr.Instance.StopAllCoroutines();
            ServerMgr.Instance.StartCoroutine(ChinookUI());

        }


        void DestroyUI()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, LayerStatus);
            }
        }
        #endregion

        #region Config

       

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("chinookPosition", cratePosition);
            
        }
        void LoadData()
        {
            cratePosition = Interface.Oxide?.DataFileSystem?.ReadObject<Vector3>("chinookPosition")
                ?? new Vector3();
           
        }


        private ConfigData cfg;
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Hided Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> HidedPlayers = new List<ulong>();
            [JsonProperty("КД спавна чинука")]
            public int spawnRate;
            [JsonProperty("Настройки UI")]
            public UISettings uISettings;
            [JsonProperty("Настройки магазина")]
            public GamestoresSettings _settings;
            [JsonProperty("Шанс на выпадение листка с балансом")]
            public float chanceToDrop;
            [JsonProperty("Предметы с балансом")]
            public List<ConfigData.BalanceItems> _balanceItems;

            public class GamestoresSettings
            {
                [JsonProperty("Shopid магазина")]
                public string shopid;
                [JsonProperty("Secretkey магазина")]
                public string secretkey;
                [JsonProperty("Сообщение при пополнении баланса ({0} - кол-во рублей)")]
                public string message;
            }
            public class BalanceItems
            {
                [JsonProperty("Кол-во баланса")]
                public int balanceAdd;
                [JsonProperty("Скин предмета")]
                public ulong skinid;
                [JsonProperty("Название предмета")]
                public string displayName;
            }
            public class UISettings
            {
                [JsonProperty("Цвет BG")]
                public string colorBG;
                [JsonProperty("Цвет текста")]
                public string textColor;
                [JsonProperty("Формат текста в панели")]
                public string text;
            }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                spawnRate = 7200,
                uISettings = new ConfigData.UISettings()
                {
                    text = "Деньги на баланс в крейте на фарм острове!\nДо открытия осталось: {0}",
                    colorBG = "#445B1EFF",
                    textColor = "#C0F069FF"
                },
                _settings = new ConfigData.GamestoresSettings()
                {
                    secretkey = "secretkey",
                    shopid = "shopid",
                    message = "На ваш баланс было гивнуто! {0} руб."
                },
                chanceToDrop = 100f,
                _balanceItems = new List<ConfigData.BalanceItems>()
                {
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 25,
                        displayName = "Листок с 25 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 50,
                        displayName = "Листок с 50 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 75,
                        displayName = "Листок с 75 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 100,
                        displayName = "Листок с 100 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 125,
                        displayName = "Листок с 125 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 150,
                        displayName = "Листок с 150 рублями <size=10>на баланс</size>",
                        skinid = 2919520699
                    }
                }
            };
            SaveConfig(config);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }

        private void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

      

       

        [ConsoleCommand("kryCB_test")]
        private void cmdTest(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;
            var player = arg.Player();

            SpawnChinookToPos();
            PrintWarning($"Chinook with balance was been spawned on position {cratePosition}");
        }


        [ChatCommand("setcratepos")]
        private void cmdSetCratePos(BasePlayer p)
        {
            if (p.net.connection.authLevel < 2)
            {
                return;
            }
            cratePosition = p.GetNetworkPosition();
            SaveData();
            p.ChatMessage($"Вы успешно заспавнили чинук!, он появится на твоем местоположении");
        }

       

        public string CHLater = "Chlayer";
     

        [ChatCommand("chininfo")]
        private void cmdNextCrate(BasePlayer p)
        {
            if (cratePosition == Vector3.zero)
                return;
            p.ChatMessage($"Следующий чинук появится через {TimeSpan.FromSeconds(nextCrate - CurrentTime())}");
            
        }

       
       
    }
}