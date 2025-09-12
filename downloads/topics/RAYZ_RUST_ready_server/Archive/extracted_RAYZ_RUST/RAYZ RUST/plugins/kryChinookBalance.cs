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
{
    ///
    /// 1.0.2:
    /// Added support for loot plugins
    /// 
    [Info("kryChinookBalance", "", "1.0.4")]
    [Description("Крейт с деньгами на баланс!!!")]
    class kryChinookBalance : RustPlugin
    {
        [PluginReference] private Plugin Clans, ImageLibrary;

        public float FadeIn = 1f;
        public float FadeOut = 0.25f;

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static int CurrentTime() => (int)DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        private const string Layer = "ui.kryChinookBalance.bg";
        private bool errorState = false;
        private List<ulong> _opened = new List<ulong>();
        private int endTime = 0;
        private Vector3 cratePosition;
        private HackableLockedCrate activeCrate;
        private int nextCrate = 0;
        private bool isFirst = true;
        private  static double cratetime = (double)HackableLockedCrate.requiredHackSeconds;

        private Dictionary<string, string> Images = new Dictionary<string, string>
        {
            ["Bilet_"] = "https://i.imgur.com/9ksfvbA.png"
        };

     


        #region Methods

        private void OnServerInitialized()
        {
            LoadData();
            nextCrate = cfg.spawnRate + CurrentTime();
            InvokeHandler.Instance.InvokeRepeating(SpawnChinookToPos, cfg.spawnRate, cfg.spawnRate);
            foreach (var check in Images)
                ImageLibrary.Call("AddImage", check.Value, check.Key);
            AddCovalenceCommand("openinfoqQQQ", nameof(CmdMenuOpen1q));
            AddCovalenceCommand("closeinfoqQQQ", nameof(CmdMenuClose1q));

            CreateSpawnGrid();
        }

       

        void OnPlayerConnected(BasePlayer player)
        {
            if(activeCrate != null)
            {
                if(!activeCrate.IsDestroyed)
                {
                    MainGUIqQQ(player);

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
                endTime = (int)cratetime + CurrentTime();
                var lc = crate.gameObject.GetComponent<LootContainer>();
                lc.inventory.Clear();
                AddBalanceList(lc);

                
               


                foreach (var x in BasePlayer.activePlayerList)
                {
                    MainGUIqQQ(x);
                    NotifyUI(x, string.Format($"Чинук с очками для кланов\nИвент чинук с очками начался"));
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", x, 0, Vector3.zero, Vector3.forward);

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
                    Server.Broadcast($"<color=green>{player.displayName} первым открыл чинук с билетами!</color>");
            }
        }


        double timeline = cratetime;
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

                foreach (var p in BasePlayer.activePlayerList)
                {
                    linesqQQ(p, timeline);
                    if (openPanel2.ContainsKey(p))
                        if (openPanel2.ContainsValue(true))
                        {
                            timerUIq(p, timeline);
                        }
                }
                    timeline--;
      

                yield return new WaitForSeconds(1f);
            }
        }

        void NotifyUI(BasePlayer player, string text)
        {
            CuiElementContainer container = new CuiElementContainer();

            UI.AddImage(ref container, "Hud", Layer, "0.5 0.5 0.5 0.25", "", "assets/icons/greyout.mat", "0 1", "0 1", "0 -115", "200 -75");
            UI.AddImage(ref container, Layer, "ImageBG", "0.7 0.7 0.7 0.4", "", "", "0.5 0.5", "0.5 0.5", "-99 -20", "-59 20");
            UI.AddRawImage(ref container, "ImageBG", "IMG", ImageLibrary?.Call<string>("GetImage", "Bilet_"), "1 1 1 1", "", "", "0 0", "1 1", "0 0", "0 0");
            UI.AddText(ref container, Layer, "InfoText", "1 1 1 0.9", $"{text}", TextAnchor.UpperLeft, 10, "0.5 0.5", "0.5 0.5", "-55 -20", "91 20");
            UI.AddButton(ref container, Layer, "closeq", "", Layer, "0.70 0.00 0.00 0.8", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "85 5", "100 19");
            UI.AddText(ref container, "closeq", "closesqQ", "1 1 1 0.9", $"☓", TextAnchor.MiddleCenter, 9, "0.5 0.5", "0.5 0.5", "-6.737 -6.201", "6.737 6.202");



            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
            timer.Once(5, () =>
            {
                CuiHelper.DestroyUi(player, Layer);
            });

        }

        public Dictionary<BasePlayer, bool> openPanel2 = new Dictionary<BasePlayer, bool>();

        private void CmdMenuOpen1q(IPlayer user, string cmd, string[] args)
        {
            var player = user?.Object as BasePlayer;
            if (player == null) return;

            consoleopenqQQ(player);
            openPanel2[player] = true;
        }

        private void CmdMenuClose1q(IPlayer user, string cmd, string[] args)
        {
            var player = user?.Object as BasePlayer;
            if (player == null) return;

            CuiHelper.DestroyUi(player, "infoqQQ");
            openPanel2[player] = false;
        }
        public void consoleopenqQQ(BasePlayer player)
        {
            InfoMenuq(player);
            timerUIq(player, timeline);
        }

        public void MainGUIqQQ(BasePlayer player)
        {
            var c = new CuiElementContainer();
            UI.AddImage(ref c, "Overlay", "MainGUIqQQ", "0 0 0 0", "", "", "1 0.50", "1 0.50", $"-44.182 -102.661", $"-3.618 -65.735");
            UI.AddImage(ref c, "MainGUIqQQ", "mainqQQ", "0.5 0.5 0.5 0.25", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-20.282 -20.715", "20.283 20.035");
            UI.AddRawImage(ref c, "mainqQQ", "iconsqQQ", ImageLibrary?.Call<string>("GetImage", "Bilet_"), "1 1 1 0.9", "", "", "0 0", "1 1", "6 7", "-7 -6");
            UI.AddImage(ref c, "MainGUIqQQ", "linesqQQ", "0 0 0 0", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-20.282 -20.715", "21.283 -18.585");
            UI.AddButton(ref c, "mainqQQ", "openqQQ", "openinfoqQQQ", "", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            CuiHelper.DestroyUi(player, "MainGUIqQQ");
            CuiHelper.AddUi(player, c);
        }
        public void InfoMenuq(BasePlayer player)
        {
            var c = new CuiElementContainer();
            UI.AddImage(ref c, "MainGUIqQQ", "infoqQQ", "0.5 0.5 0.5 0.25", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-223.521 -20.715", "-24.017 20.035");
            UI.AddText(ref c, "infoqQQ", "textqQQ", "1 1 1 0.9", $"В квадрате {GetNameGrid(cratePosition)} начался ивент\nоткрыв чинук вы можете получить очки!", TextAnchor.UpperLeft, 10, "0.5 0.5", "0.5 0.5", "-94.187 -20", "91.911 6.411");
            UI.AddButton(ref c, "infoqQQ", "closeqQQ", "closeinfoqQQQ", "", "0.70 0.00 0.00 0.8", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "85.23 5.691", "99.752 19.875");
            UI.AddText(ref c, "closeqQQ", "closesqQQ", "1 1 1 0.9", $"☓", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "-6.737 -6.201", "6.737 6.202");
            CuiHelper.DestroyUi(player, "infoqQQ");
            CuiHelper.AddUi(player, c);
        }
        public void timerUIq(BasePlayer player, double time)
        {
            var c = new CuiElementContainer();
            if(time > 0)
            {
                UI.AddText(ref c, "infoqQQ", "titleqQQ", "1 1 1 0.9", $"До конца чинука с очками: [{FormatTimes(TimeSpan.FromSeconds(time))}]", TextAnchor.UpperLeft, 12, "0.5 0.5", "0.5 0.5", "-94.187 -4.775", "85.183 19.875");
            }
            else
            {
                UI.AddText(ref c, "infoqQQ", "titleqQQ", "1 1 1 0.9", $"До конца чинука с очками: [0:00]", TextAnchor.UpperLeft, 12, "0.5 0.5", "0.5 0.5", "-94.187 -4.775", "85.183 19.875");
            }
           
            CuiHelper.DestroyUi(player, "titleqQQ");
            CuiHelper.AddUi(player, c);
        }
        public void linesqQQ(BasePlayer player, double time)
        {
            var c = new CuiElementContainer();


            double timeLines = (time / cratetime);

            UI.AddImage(ref c, "linesqQQ", "lineqQQ", "1 1 1 1", "", "assets/icons/greyout.mat", "0 0", $"{timeLines} 1", "", "");
            CuiHelper.DestroyUi(player, "lineqQQ");
            CuiHelper.AddUi(player, c);
        }


        private static string FormatTimes(TimeSpan time)
        {
            return ($"{FormatMinutes(time.Minutes)}:{FormatSeconds(time.Seconds)}");
        }
        private static string FormatMinutes(int minutes) => FormatUnits2(minutes);

        private static string FormatSeconds(int seconds) => FormatUnits(seconds);

        private static string FormatUnits2(int units)
        {
            var tmp = units % 10;

            if (units >= 10)
                return $"{units}";

            if (units >= 0 && units <= 10)
                return $"{units}";

            return $"{units}";
        }

        private static string FormatUnits(int units)
        {
            var tmp = units % 10;

            if (units >= 10)
                return $"{units}";

            if (units >= 0 && units <= 10)
                return $"0{units}";

            return $"0{units}";
        }

        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(SpawnChinookToPos);
            activeCrate?.Kill(BaseNetworkable.DestroyMode.None);
            ServerMgr.Instance.StopAllCoroutines();
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
                CuiHelper.DestroyUi(p, "MainGUIqQQ");
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
            p.ChatMessage($"Spawn crate pos setted to {cratePosition}");
        }

       
     

        [ChatCommand("whencrate")]
        private void cmdNextCrate(BasePlayer p)
        {
            if (cratePosition == Vector3.zero)
                return;
            p.ChatMessage($"Next crate in {TimeSpan.FromSeconds(nextCrate - CurrentTime())}");
            
        }

        #region UI class

        public static class UI
        {
            public static void AddImage(ref CuiElementContainer container, string parrent, string name, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Material = "assets/icons/greyout.mat"},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddRawImage(ref CuiElementContainer container, string parrent, string name, string png, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax)
            {
                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Png = png},
                        new CuiOutlineComponent { Color = "0 0 0 0", Distance = "0 0"},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddText(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0", string font = "robotocondensed-bold.ttf", string dist = "0.5 0.5", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = font, FadeIn = FadeIN},
                        new CuiOutlineComponent{Color = outColor, Distance = dist},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }

            public static void AddButton(ref CuiElementContainer container, string parrent, string name, string cmd, string close, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = "assets/icons/greyout.mat", },
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = "assets/icons/greyout.mat", },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Material = "assets/icons/greyout.mat", },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }
        }

        #endregion
    }
}