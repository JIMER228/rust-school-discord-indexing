using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HitAdvance", "Hougan", "1.3.4")]
    [Description("Уникальный маркер для вашего сервера. Куплено на DarkPlugins.RU")]
    public class HitAdvance : RustPlugin
    {
        #region Classes

        private class PlayerSettings
        {
            public int Current;
            public bool Blocks;
        }

        private class Configuration
        {
            [JsonProperty("Изначально включенный режим (0 - отключено, 1 - полоса, 2 - текст, 3 - оба)")]
            public int CONF_DefaultEnable = 2;
        }

        #endregion

        #region Variables

        private static Configuration config;

        [JsonProperty("Настройки игроков")]
        private Dictionary<ulong, PlayerSettings> playerMarkers = new Dictionary<ulong, PlayerSettings>();

        [JsonProperty("Настройка маркеров и их привлегий")]
        private Dictionary<string, string> markerPermissions = new Dictionary<string, string>
        {
            ["ОТКЛЮЧЕНО"] = "",
            ["ПОЛОСА"] = "HitAdvance.Line",
            ["ТЕКСТ"] = "HitAdvance.Text",
            ["ОБА"] = "HitAdvance.Both"
        };

        private string Layer = "UI_HitMarker";

        #endregion

        #region Hooks

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("HitAdvance/Player"))
                playerMarkers =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSettings>>("HitAdvance/Player");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("HitAdvance/Permissions"))
                markerPermissions =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>("HitAdvance/Permissions");
            else
            {
                Interface.Oxide.DataFileSystem.WriteObject("HitAdvance/Permissions", markerPermissions);
                OnServerInitialized();
                return;
            }

            foreach (var check in markerPermissions.Where(p => p.Value != ""))
                permission.RegisterPermission(check.Value, this);
            foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!playerMarkers.ContainsKey(player.userID))
                playerMarkers.Add(player.userID,
                    new PlayerSettings() { Current = config.CONF_DefaultEnable, Blocks = false });
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            NextTick(() =>
            {
                if (entity == null || entity.GetComponent<BuildingBlock>() == null ||
                    entity.GetComponent<BuildingBlock>().IsDestroyed ||
                    entity.GetComponent<BuildingBlock>().IsDead() || info == null || info.Initiator == null ||
                    entity.lastAttacker == null)
                    return;
                if (entity is BuildingBlock &&
                    ((info.Initiator is BasePlayer && info.Initiator.GetComponent<NPCPlayer>() == null) ||
                     entity.lastAttacker.GetComponent<BasePlayer>() != null))
                {
                    if (entity.GetComponent<BuildingBlock>().IsDestroyed ||
                        entity.GetComponent<BuildingBlock>().IsDead())
                        return;

                    BasePlayer attacker = entity.lastAttacker.GetComponent<BasePlayer>();
                    if (attacker == null || attacker.GetComponent<NPCPlayer>() != null ||
                        !playerMarkers.ContainsKey(attacker.userID))
                        return;

                    if (playerMarkers[attacker.userID].Current == 1)
                    {
                        DrawLine(attacker, entity as BaseCombatEntity);
                    }
                    else if (playerMarkers[attacker.userID].Current == 2)
                    {
                        //  NextTick(() =>
                        //  {
                        DrawText(attacker, entity as BaseCombatEntity, info);
                        // });
                    }
                    else if (playerMarkers[attacker.userID].Current == 3)
                    {
                        DrawLine(attacker, entity as BaseCombatEntity);
                        DrawText(attacker, entity as BaseCombatEntity, info);
                    }
                }
            });
            return;
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if ((info?.HitEntity is BasePlayer ||
                 (info?.HitEntity is BuildingBlock && playerMarkers[attacker.userID].Blocks)) &&
                attacker.GetComponent<NPCPlayer>() == null)
            {
                if (playerMarkers[attacker.userID].Current == 1)
                {
                    NextTick(() => { DrawLine(attacker, info.HitEntity as BaseCombatEntity); });
                }
                else if (playerMarkers[attacker.userID].Current == 2)
                {
                    NextTick(() => { DrawText(attacker, info.HitEntity as BaseCombatEntity, info); });
                }
                else if (playerMarkers[attacker.userID].Current == 3)
                {
                    NextTick(() =>
                    {
                        DrawLine(attacker, info.HitEntity as BaseCombatEntity);
                        DrawText(attacker, info.HitEntity as BaseCombatEntity, info);
                    });
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("marker")]
        private void chatCmdchange(BasePlayer player)
        {
            DrawChangeMenu(player);
        }

        [ConsoleCommand("changemarker")]
        private void consoleCmdChange(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            BasePlayer player = args.Player();
            if (!args.HasArgs(1))
                DrawChangeMenu(player);
            else
            {
                int newId;
                if (int.TryParse(args.Args[0], out newId))
                {
                    if (markerPermissions.ElementAt(newId).Value == "" ||
                        permission.UserHasPermission(player.UserIDString, markerPermissions.ElementAt(newId).Value))
                    {
                        SendReply(player, $"");
                        playerMarkers[player.userID].Current = newId;
                        DrawChangeMenu(player);
                    }
                    else
                    {
                        SendReply(player,
                            $"У вас <color=#F99C53FF>недостаточно прав</color> для включения этого хит-маркера!\nКупите <color=#F99C53FF>VIP</color> или <color=#F99C53FF>отдельную услугу</color> на сайте!");
                    }
                }
                else
                {
                    if (playerMarkers[player.userID].Blocks)
                        playerMarkers[player.userID].Blocks = false;
                    else
                        playerMarkers[player.userID].Blocks = true;

                    SendReply(player, $"");
                    DrawChangeMenu(player);
                }
            }
        }

        #endregion

        #region GUI

        private void DrawLine(BasePlayer player, BaseCombatEntity target)
        {
            try
            {
                CuiHelper.DestroyUi(player, Layer);
                //if (player.currentTeam == target.currentTeam && player.currentTeam != 0)
                //    return;

                CuiElementContainer container = new CuiElementContainer();

                string lineColor;
                Vector2 linePosition;
                if (target != null)
                {
                    lineColor = GetGradientColor((int)(target.MaxHealth() - target.health), (int)target.MaxHealth());
                    linePosition = GetLinePosition(target.health / target.MaxHealth());
                }
                else
                {
                    lineColor = GetGradientColor((int)(1), 1000);
                    linePosition = GetLinePosition((float)1 / 1000);
                }

                if (target is BasePlayer)
                {
                    if ((target as BasePlayer).IsWounded() || target.IsDead())
                    {
                        lineColor = GetGradientColor((int)(100), (int)target.MaxHealth());
                        linePosition = GetLinePosition(1 / target.MaxHealth());
                    }
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    RectTransform =
                        {AnchorMin = $"{linePosition[0]} 0.1101852", AnchorMax = $"{linePosition[1]} 0.1157407"},
                    Image = { Color = "0 0 0 0" }
                }, "Hud", Layer);
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer,
                    Name = Layer + ".Showed",
                    Components =
                    {
                        new CuiImageComponent {Color = lineColor},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                    }
                });

                CuiHelper.AddUi(player, container);
                timer.Once(0.1f, () => { CuiHelper.DestroyUi(player, Layer + ".Showed"); });
                timer.Once(5f, () => { CuiHelper.DestroyUi(player, Layer); });
            }
            catch (NullReferenceException)
            {
            }
        }

        private void DrawText(BasePlayer player, BaseCombatEntity target, HitInfo info)
        {
            try
            {
                if (target == null)
                    return;

                var id = CuiHelper.GetGuid();
                CuiElementContainer container = new CuiElementContainer();

                float divisionDmg = info.damageTypes.Total() / target.MaxHealth();
                float divisionHP = (target.health / target.MaxHealth());
                float avgDivision = (target.IsDead() ? 1 : 1 - divisionHP);

                var position = GetRandomTextPosition(divisionDmg, divisionHP);

                string textDamage = info.damageTypes.Total().ToString("F0");
                if (Convert.ToInt32(Math.Floor(info.damageTypes.Total())) == 0)
                    return;

                if (target is BasePlayer)
                {
                    if (info.isHeadshot)
                        textDamage = $"<color=#FF8D33>ГОЛОВА</color> - {info.damageTypes.Total().ToString("F0")}";
                    if ((target as BasePlayer).IsWounded())
                    {
                        textDamage = "<color=#FF8D33>УПАЛ</color>";
                        if (info.isHeadshot)
                            textDamage += " <color=#FF8D33>ГОЛОВА</color>";
                    }
                    else if (target.IsDead())
                    {
                        textDamage = "<color=#FF8D33>УМЕР</color>";
                        if (info.isHeadshot)
                        {
                            textDamage += " <color=#FF8D33>ГОЛОВА</color>";
                        }
                    }

                    if (player.currentTeam == (target as BasePlayer).currentTeam && player.currentTeam != 0)
                    {
                        textDamage = "<color=#76D09C>ДРУГ</color>";
                        avgDivision = 1;
                    }
                }


                container.Add(new CuiElement()
                {
                    Name = id,
                    Parent = "Hud",
                    FadeOut = 0.5f,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<b>{textDamage}</b>",
                            Color = HexToRustFormat("#FFFFFFFF"),
                            Font = "robotocondensed-bold.ttf",
                            FontSize = (int) Mathf.Lerp(15, 15, avgDivision),
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiOutlineComponent() {Color = "0 0 0 1", Distance = "0.155 0.155"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{position.x} {position.y}", AnchorMax = $"{position.x} {position.y}",
                            OffsetMin = "-100 -100", OffsetMax = "100 100"
                        }
                    }
                });

                CuiHelper.AddUi(player, container);
                timer.Once(0.1f, () => { CuiHelper.DestroyUi(player, id); });
            }
            catch (NullReferenceException)
            {
            }
        }

        private void DrawChangeMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform =
                    {AnchorMin = "0.3046875 0.3733796", AnchorMax = "0.3046875 0.3733796", OffsetMax = "425 182"},
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                Button = { Close = Layer, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".BG",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#0000003C")},
                    new CuiRectTransformComponent {AnchorMin = "0 0.26", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Header",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#F99C53FF")},
                    new CuiRectTransformComponent {AnchorMin = "0 0.8144424", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ВЫБОР ХИТ-МАРКЕРА", Color = HexToRustFormat("#476443FF"), FontSize = 20,
                        Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Здесь вы можете выбрать вид хит-маркера, либо полностью отключить его.",
                        Color = HexToRustFormat("#FFFFFFFF"), FontSize = 14, Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = "0.05 0.620658", AnchorMax = "0.95 0.8180986"}
                }
            });

            string color = "";
            int i = 0;
            foreach (var check in markerPermissions)
            {
                color = playerMarkers[player.userID].Current == i
                    ? HexToRustFormat("#518eefFF")
                    :
                    permission.UserHasPermission(player.UserIDString, check.Value) || check.Value == ""
                        ?
                        HexToRustFormat("#F99C53FF")
                        :
                        HexToRustFormat("#C44B4BFF");

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name = Layer + $".{i}",
                    Components =
                    {
                        new CuiImageComponent {Color = color},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0.01254448 + i * 0.2468} 0.4561242",
                            AnchorMax = $"{0.2443728 + i * 0.2468} 0.5950636", OffsetMax = "0 0"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{i}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = check.Key, FontSize = 14, Font = "robotocondensed-bold.ttf",
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0"}
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"changemarker {i}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + $".{i}");

                i++;
            }

            color = playerMarkers[player.userID].Blocks ? HexToRustFormat("#518eefFF") : HexToRustFormat("#F99C53FF");

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + $".{i}",
                Components =
                {
                    new CuiImageComponent {Color = color},
                    new CuiRectTransformComponent
                        {AnchorMin = $"0.25 0.2861242", AnchorMax = $"0.75 0.4261242", OffsetMax = "0 0"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + $".{i}",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ПО ПОСТРОЙКАМ", FontSize = 14, Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = $"changemarker block", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + $".{i}");

            CuiHelper.AddUi(player, container);
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
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion

        #region Utils

        private Vector2 GetLinePosition(float divisionDmg)
        {
            float centerX = 0.4932292f;
            float xMax = 0.6411458f;
            float diff = xMax - centerX;
            float lenght05 = diff * divisionDmg;

            float xLeft = centerX - lenght05;
            float xRight = centerX + lenght05;

            return new Vector2(xLeft, xRight);
        }

        Vector2 GetRandomTextPosition(float divisionDmg, float divisionHP)
        {
            float x = (float)Oxide.Core.Random.Range(45, 55) / 100;
            float y = (float)Oxide.Core.Random.Range(45, 55) / 100;

            return new Vector2(x, y);
        }

        public string GetGradientColor(int count, int max)
        {
            if (count > max)
                count = max;
            float n = max > 0 ? (float)ColorsGradientDB.Length / max : 0;
            var index = (int)(count * n);
            if (index > 0) index--;
            return ColorsGradientDB[index];
        }

        private string[] ColorsGradientDB = new string[100]
        {
            "0.2000 0.8000 0.2000 1.0000",
            "0.2471 0.7922 0.1961 1.0000",
            "0.2824 0.7843 0.1922 1.0000",
            "0.3176 0.7725 0.1843 1.0000",
            "0.3451 0.7647 0.1804 1.0000",
            "0.3686 0.7569 0.1765 1.0000",
            "0.3922 0.7490 0.1725 1.0000",
            "0.4118 0.7412 0.1686 1.0000",
            "0.4314 0.7333 0.1647 1.0000",
            "0.4471 0.7216 0.1608 1.0000",
            "0.4667 0.7137 0.1569 1.0000",
            "0.4784 0.7059 0.1529 1.0000",
            "0.4941 0.6980 0.1490 1.0000",
            "0.5098 0.6902 0.1412 1.0000",
            "0.5216 0.6824 0.1373 1.0000",
            "0.5333 0.6706 0.1333 1.0000",
            "0.5451 0.6627 0.1294 1.0000",
            "0.5569 0.6549 0.1255 1.0000",
            "0.5647 0.6471 0.1216 1.0000",
            "0.5765 0.6392 0.1176 1.0000",
            "0.5843 0.6314 0.1137 1.0000",
            "0.5922 0.6235 0.1137 1.0000",
            "0.6039 0.6118 0.1098 1.0000",
            "0.6118 0.6039 0.1059 1.0000",
            "0.6196 0.5961 0.1020 1.0000",
            "0.6275 0.5882 0.0980 1.0000",
            "0.6314 0.5804 0.0941 1.0000",
            "0.6392 0.5725 0.0902 1.0000",
            "0.6471 0.5647 0.0863 1.0000",
            "0.6510 0.5569 0.0824 1.0000",
            "0.6588 0.5451 0.0784 1.0000",
            "0.6627 0.5373 0.0784 1.0000",
            "0.6667 0.5294 0.0745 1.0000",
            "0.6745 0.5216 0.0706 1.0000",
            "0.6784 0.5137 0.0667 1.0000",
            "0.6824 0.5059 0.0627 1.0000",
            "0.6863 0.4980 0.0588 1.0000",
            "0.6902 0.4902 0.0588 1.0000",
            "0.6941 0.4824 0.0549 1.0000",
            "0.6980 0.4745 0.0510 1.0000",
            "0.7020 0.4667 0.0471 1.0000",
            "0.7020 0.4588 0.0471 1.0000",
            "0.7059 0.4471 0.0431 1.0000",
            "0.7098 0.4392 0.0392 1.0000",
            "0.7098 0.4314 0.0392 1.0000",
            "0.7137 0.4235 0.0353 1.0000",
            "0.7176 0.4157 0.0314 1.0000",
            "0.7176 0.4078 0.0314 1.0000",
            "0.7216 0.4000 0.0275 1.0000",
            "0.7216 0.3922 0.0275 1.0000",
            "0.7216 0.3843 0.0235 1.0000",
            "0.7255 0.3765 0.0235 1.0000",
            "0.7255 0.3686 0.0196 1.0000",
            "0.7255 0.3608 0.0196 1.0000",
            "0.7255 0.3529 0.0196 1.0000",
            "0.7294 0.3451 0.0157 1.0000",
            "0.7294 0.3373 0.0157 1.0000",
            "0.7294 0.3294 0.0157 1.0000",
            "0.7294 0.3216 0.0118 1.0000",
            "0.7294 0.3137 0.0118 1.0000",
            "0.7294 0.3059 0.0118 1.0000",
            "0.7294 0.2980 0.0118 1.0000",
            "0.7294 0.2902 0.0078 1.0000",
            "0.7255 0.2824 0.0078 1.0000",
            "0.7255 0.2745 0.0078 1.0000",
            "0.7255 0.2667 0.0078 1.0000",
            "0.7255 0.2588 0.0078 1.0000",
            "0.7255 0.2510 0.0078 1.0000",
            "0.7216 0.2431 0.0078 1.0000",
            "0.7216 0.2353 0.0039 1.0000",
            "0.7176 0.2275 0.0039 1.0000",
            "0.7176 0.2196 0.0039 1.0000",
            "0.7176 0.2118 0.0039 1.0000",
            "0.7137 0.2039 0.0039 1.0000",
            "0.7137 0.1961 0.0039 1.0000",
            "0.7098 0.1882 0.0039 1.0000",
            "0.7098 0.1804 0.0039 1.0000",
            "0.7059 0.1725 0.0039 1.0000",
            "0.7020 0.1647 0.0039 1.0000",
            "0.7020 0.1569 0.0039 1.0000",
            "0.6980 0.1490 0.0039 1.0000",
            "0.6941 0.1412 0.0039 1.0000",
            "0.6941 0.1333 0.0039 1.0000",
            "0.6902 0.1255 0.0039 1.0000",
            "0.6863 0.1176 0.0039 1.0000",
            "0.6824 0.1098 0.0039 1.0000",
            "0.6784 0.1020 0.0039 1.0000",
            "0.6784 0.0941 0.0039 1.0000",
            "0.6745 0.0863 0.0039 1.0000",
            "0.6706 0.0784 0.0039 1.0000",
            "0.6667 0.0706 0.0039 1.0000",
            "0.6627 0.0627 0.0039 1.0000",
            "0.6588 0.0549 0.0039 1.0000",
            "0.6549 0.0431 0.0039 1.0000",
            "0.6510 0.0353 0.0000 1.0000",
            "0.6471 0.0275 0.0000 1.0000",
            "0.6392 0.0196 0.0000 1.0000",
            "0.6353 0.0118 0.0000 1.0000",
            "0.6314 0.0039 0.0000 1.0000",
            "0.6275 0.0000 0.0000 1.0000",
        };

        #endregion
    }
}