    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Newtonsoft.Json;
    using Oxide.Core;
    using Oxide.Core.Plugins;
    using Oxide.Game.Rust.Cui;
    using UnityEngine;

    namespace Oxide.Plugins
    {
        [Info("WipeBlock", "dq & kpot", "1.0.4")]
        class WipeBlock : RustPlugin
        {
            [PluginReference] private Plugin ImageLibrary, Duel;
            private static ConfigData config;
            private string CONF_IgnorePermission = "WipeBlock.ignore";
            private class ConfigData
            {
                [JsonProperty(PropertyName = "Блокировка предметов")]
                public Dictionary<string, int> items;
            }

            private ConfigData GetDefaultConfig()
            {
                return new ConfigData
                {
                    items = new Dictionary<string, int>
                    {
                            {"pistol.revolver" , 1800},
                            {"shotgun.double", 1800},

                            {"flamethrower" , 3600},
                            {"bucket.helmet" , 3600},
                            {"riot.helmet", 3600},
                            {"hoodie", 3600},
                            {"pants" , 3600},

                            {"pistol.python" , 7200},
                            {"pistol.semiauto" , 7200},
                            {"pistol.prototype17" , 7200},
                            {"coffeecan.helmet" , 7200},
                            {"roadsign.jacket" , 7200},
                            {"roadsign.kilt" , 7200},
                            {"icepick.salvaged" , 7200},
                            {"axe.salvaged" , 7200},
                            {"hammer.salvaged" , 7200},

                            {"shotgun.pump" , 14400},
                            {"shotgun.spas12" , 14400},
                            {"pistol.m92" , 14400},
                            {"smg.mp5" , 14400},
                            {"jackhammer" , 0},
                            {"chainsaw" , 0},

                            {"smg.2" , 28800},
                            {"smg.thompson" , 28800},
                            {"rifle.semiauto", 28800},
                            {"surveycharge", 28800},
                            {"grenade.f1" , 28800},
                            {"grenade.beancan" , 28800},
                            {"explosive.satchel" , 28800},

                            {"rifle.m39" , 43200},
                            {"rifle.bolt" , 43200},
                            {"rifle.ak" , 43200},
                            {"rifle.lr300" , 43200},
                            {"metal.facemask" , 43200},
                            {"metal.plate.torso", 43200},

                            {"ammo.rifle.explosive" , 64800},
                            {"ammo.rocket.basic" , 64800},
                            {"ammo.rocket.fire" , 64800},
                            {"ammo.rocket.hv", 64800},
                            {"ammo.grenadelauncher.he", 64800},
                            {"multiplegrenadelauncher" , 64800},
                            {"rocket.launcher" , 64800},
                            {"explosive.timed", 64800},

                            {"lmg.m249" , 86400},
                            {"rifle.l96" , 86400},
                            {"heavy.plate.helmet" , 86400},
                            {"heavy.plate.jacket" , 86400},
                            {"heavy.plate.pants" , 86400},
                    }
                };
            }


            protected override void LoadConfig()
            {
                base.LoadConfig();

                try
                {
                    config = Config.ReadObject<ConfigData>();

                    if (config == null)
                    {
                        LoadDefaultConfig();
                    }
                }
                catch
                {
                    LoadDefaultConfig();
                }

                SaveConfig();
            }

            protected override void LoadDefaultConfig()
            {
                config = GetDefaultConfig();
            }
            protected override void SaveConfig()
            {
                Config.WriteObject(config);
            }
            private object CanAcceptItem(ItemContainer container, Item item)
            {
                if (container == null || item == null || container.entityOwner == null)
                    return null;

                if (container.entityOwner is AutoTurret)
                {
                    BasePlayer player = item.GetOwnerPlayer();
                    if (player == null)
                        return null;

                if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                    return null;

                if (!config.items.ContainsKey(item.info.shortname))
                {
                    return null;
                }

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                    {
                        MessBlockUi(player, item.info.shortname, item.info.displayName.english);
                        timer.Once(0.8f, () =>
                        {
                            CuiHelper.DestroyUi(player, Layer);
                        });
                        return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                    }
                }

                return null;
            }


            private object CanWearItem(PlayerInventory inventory, Item item)
            {
                var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
                if (!player.userID.IsSteamId())
                {
                    return null;
                }
                if (playerOnDuel(player)) return null;

                if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                    return null;

                if (!config.items.ContainsKey(item.info.shortname))
                {
                    return null;
                }

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                {
                    if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                        return null;

                    MessBlockUi(player, item.info.shortname, item.info.displayName.english);
                    timer.Once(0.8f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer);
                    });
                }
                return isBlocked;
            }

            private object CanEquipItem(PlayerInventory inventory, Item item)
            {
                var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null) return null;
                if (playerOnDuel(player)) return null;

                if (!config.items.ContainsKey(item.info.shortname))
                {
                    return null;
                }

                if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
                if (isBlocked == false)
                {
                    if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                        return null;
                    MessBlockUi(player, item.info.shortname, item.info.displayName.english);
                    timer.Once(3.8f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer);
                    });
                }
                return isBlocked;
            }

        private object OnWeaponReload(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

//            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
//                return null;

            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
            {
                SendReply(player, $"Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!");
            }
            return isBlocked;
        }

        private object OnMagazineReload(BaseProjectile projectile, int desiredAmount, BasePlayer player)
            {
                if (projectile == null || player == null) return null;
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                NextTick(() =>
                {
                    var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
                    if (isBlocked == false)
                    {
                        player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                        projectile.primaryMagazine.contents = 0;
                        projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                        projectile.SendNetworkUpdate();
                        player.SendNetworkUpdate();

                        PrintError($"[{DateTime.Now.ToShortTimeString()}] {player}/[{player.userID}] пытался взломать систему блокировки!");
                    SendReply(player, $"<color=#81B67A>Хорошая</color> попытка, правда ваше оружие теперь сломано!");


                    }
                });

                return null;
            }

            private bool playerOnDuel(BasePlayer player)
            {
                if (plugins.Find("Duel") && (bool)plugins.Find("Duel").Call("IsPlayerOnActiveDuel", player)) return true;
                if (plugins.Find("OneVSOne") && (bool)plugins.Find("OneVSOne").Call("IsEventPlayer", player)) return true;
                return false;
            }

            private void OnServerInitialized()
            {
                permission.RegisterPermission(CONF_IgnorePermission, this);
                foreach (var check in config.items)
                {
                    ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check.Key}.png", check.Key);
                }
            }

            private void MessBlockUi(BasePlayer player, string shortname, string name)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.2 0.2 0.2 0.95" },
                    RectTransform =
                         {AnchorMin = "0.5 0.7", AnchorMax = "0.5 0.7", OffsetMin = "-140 -25", OffsetMax = "140 50"},
                    CursorEnabled = false,
                }, "Overlay", Layer);

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name = Layer + ".BlockItem",
                    Components =
                     {
                         new CuiImageComponent {Color = "1 1 1 0.1"},
                         new CuiRectTransformComponent { AnchorMin = "0.01586128 0.05839238", AnchorMax = "0.2891653 0.9208925" }
                     }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".BlockItem",
                    Components =
                     {
                         new CuiRawImageComponent
                         {
                             Png = (string) ImageLibrary.Call("GetImage", $"{shortname}")
                         },
                         new CuiRectTransformComponent
                             {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                     }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                     {
                         new CuiTextComponent()
                         {
                             Color = "1 1 1 1",
                             Text = $"Предмет {name} заблокирован.\nДо его разблокировки осталось: <color=orange>{TimeSpan.FromSeconds((int) IsBlocked(shortname)).ToShortString()}</color>",
                             FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"
                         },
                         new CuiRectTransformComponent {AnchorMin = "0.3204 0.0833925", AnchorMax = "0.9802345 0.9458925"},
                     }
                });

                CuiHelper.AddUi(player, container);
            }

            private const string Layer = "lay";
            [ChatCommand("block")]
            private void BlockUi(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -250", OffsetMax = "400 250" },
                    CursorEnabled = true,
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMax = "800 800",
                        OffsetMin = "-800 -800"
                    },
                    Button =
                    {
                        Color = "0 0 0 0.9", Material = "assets/icons/greyout.mat",
                        Close = Layer
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11,
                        Color = HexToRustFormat("#D4AD5AFF")
                    }
                }, Layer);

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent {Color = "0.36 0.34 0.32 0.95", Material = "assets/icons/greyout.mat"},
                        new CuiRectTransformComponent { AnchorMin = "0 0.1", AnchorMax = "1 1" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = "1 1 1 1",
                            Text = "Список заблокированных предметов после вайпа",
                                FontSize = 20, Align = TextAnchor.UpperCenter, Font = "robotocondensed-bold.ttf"
                        },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 -3"},
                    }
                });

                foreach (var check in config.items.Select((i, t) => new { A = i, B = t }))
                {
                    var blockTime = UnBlockTime(config.items[check.A.Key]) - CurrentTime();
                    string text = BlockTimeGui(check.A.Key) ? $"<color=#A9A9A9>{TimeSpan.FromSeconds((int)IsBlocked(check.A.Key)).ToShortString()}</color>" : "<color=#D4AD5A>Доступно</color>";
                    container.Add(new CuiButton
                    {
                        RectTransform =
                            {
                                AnchorMin =
                                    $"{0.00961443 + check.B * 0.10 - Math.Floor((double) check.B / 10) * 10 * 0.10} {0.7812496 - Math.Floor((double) check.B / 10) * 0.16}",
                                AnchorMax =
                                    $"{0.09290709 + check.B * 0.10 - Math.Floor((double) check.B / 10) * 10 * 0.10} {0.9312496 - Math.Floor((double) check.B / 10) * 0.16}",
                                OffsetMax = "0 0"
                            },
                        Button =
                            {
                                Color = "1 1 1 0",
                            },
                        Text =
                            {
                                Text = ""
                            }
                    }, Layer, Layer + $".{check.B}");


                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}",
                        Name = Layer + $".{check.B}.Img",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage", check.A.Key)
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "-0.1 0", AnchorMax = "1.1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Color = "1 1 1 1",
                                Text =
                                    $"{text}",
                                FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"
                            },
                            new CuiRectTransformComponent {AnchorMin = "0 0.02", AnchorMax = "1.05 0.28"},
                        }
                    });
                }
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMax = "800 800",
                        OffsetMin = "-800 -800"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11
                    }
                }, Layer);
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = ""
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11
                    }
                }, Layer);
                CuiHelper.AddUi(player, container);
            }

            private double IsBlocked(string shortName)
            {
                var blockLeft = UnBlockTime(config.items[shortName]) - CurrentTime();
                return blockLeft > 0 ? blockLeft : 0;
            }

            private bool BlockTimeGui(string shortName)
            {
                var blockLeft = UnBlockTime(config.items[shortName]) - CurrentTime();
                if (blockLeft > 0)
                {
                    return true;
                }

                return false;
            }
            private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
            private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
            static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
            static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

            public static string ToShortString(TimeSpan timeSpan)
            {
                int i = 0;
                string resultText = "";
                if (timeSpan.Days > 0)
                {
                    resultText += timeSpan.Days + " Д";
                    i++;
                }
                if (timeSpan.Hours > 0 && i < 2)
                {
                    if (resultText.Length != 0)
                        resultText += " ";
                    resultText += timeSpan.Days + " Ч";
                    i++;
                }
                if (timeSpan.Minutes > 0 && i < 2)
                {
                    if (resultText.Length != 0)
                        resultText += " ";
                    resultText += timeSpan.Days + " М.";
                    i++;
                }
                if (timeSpan.Seconds > 0 && i < 2)
                {
                    if (resultText.Length != 0)
                        resultText += " ";
                    resultText += timeSpan.Days + " С.";
                    i++;
                }

                return resultText;
            }

            private static string HexToRustFormat(string hex)
            {
                if (string.IsNullOrEmpty(hex))
                {
                    hex = "#FFFFFFFF";
                }

                var str = hex.Trim('#');

                if (str.Length == 6)
                    str += "FF";

                if (str.Length != 8)
                {
                    throw new Exception(hex);
                }

                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
                var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

                Color color = new Color32(r, g, b, a);

                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            }
        }
    }