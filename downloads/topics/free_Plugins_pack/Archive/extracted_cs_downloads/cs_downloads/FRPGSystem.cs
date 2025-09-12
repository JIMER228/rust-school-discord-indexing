using Oxide.Core;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Network;
using System;

namespace Oxide.Plugins
{
    [Info("FRPGSystem", "FourTeen", "1.0.6")]
    class FRPGSystem : RustPlugin
    {
        private Dictionary<ulong, ulong> timesplayer = new Dictionary<ulong, ulong>();
        private List<ulong> needexit = new List<ulong>();
        [PluginReference] private Plugin HealthTick, WeaponDamageScale, BetterHealth, IQEconomic;
        protected override void LoadDefaultConfig() => config = RPGConfig.GetNewConfiguration();

        private void DamageUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ".Damage");
            CuiElementContainer container = new CuiElementContainer();
            float rateore = 172800;
            if (timesplayer.ContainsKey(player.userID.Get()))
            {
                ulong ftime = timesplayer[player.userID.Get()] + GetSecondsConnected(player.Connection);
                rateore = ftime;
            }
            float maxrateore = 172800;
            float damage = 1;
            float hookdm = (float)WeaponDamageScale.CallHook("GetDamageScale", player.userID.Get());
            if (hookdm != null && hookdm != 0)
            {
                damage = hookdm;
            }
            string pregressore = rateore >= maxrateore ? "0.985 0.9" : config.Panel.Progress ? $"{0.137 + ((rateore / maxrateore) * 0.848)} 0.9" : $"{0.137 + (rateore * (0.848 / maxrateore))} 0.9";
            string textore = $"Урон: x{damage}";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.685", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat" }
            }, ".RPG", ".Damage");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.0125 0.155", AnchorMax = "0.11 0.845", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.5", Sprite = "assets/icons/bullet.png" },
                Text = { Text = "" }
            }, ".Damage");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.137 0.1", AnchorMax = pregressore, OffsetMax = "0 0" },
                Image = { Color = "0.2986275 0.6076471 0.8384314 0.92921569", Material = "assets/icons/greyout.mat" }
            }, ".Damage");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.171 0", AnchorMax = "1 1.1", OffsetMax = "0 0" },
                Text = { Text = textore, Align = TextAnchor.MiddleLeft, FontSize = 15, Color = "1 1 1 0.6003938" }
            }, ".Damage");

            CuiHelper.AddUi(player, container);
        }
        private void RegenUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ".Regen");
            CuiElementContainer container = new CuiElementContainer();
            float rateanimal = 172800;
            if (timesplayer.ContainsKey(player.userID.Get()))
            {
                ulong ftime = timesplayer[player.userID.Get()] + GetSecondsConnected(player.Connection);
                rateanimal = ftime;
            }
            float maxrateanimal = 172800;
            int regen = 0;
            int hookrg = (int)HealthTick.CallHook("GetCurrentRegenPlayer", player);
            if (hookrg != null || hookrg != 0)
            {
                regen = hookrg;
            }
            string pregressanimal = rateanimal >= maxrateanimal ? "0.985 0.9" : config.Panel.Progress ? $"{0.137 + ((rateanimal / maxrateanimal) * 0.848)} 0.9" : $"{0.137 + (rateanimal * (0.848 / maxrateanimal))} 0.9";
            string textanimal = $"Реген: {regen}";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.345", AnchorMax = "1 0.66", OffsetMax = "0 0" },
                Image = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat" }
            }, ".RPG", ".Regen");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.0125 0.155", AnchorMax = "0.11 0.845", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.5", Sprite = "assets/icons/health.png" },
                Text = { Text = "" }
            }, ".Regen");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.137 0.1", AnchorMax = pregressanimal, OffsetMax = "0 0" },
                Image = { Color = "0.7886275 0.4476471 0.2184314 0.92921569", Material = "assets/icons/greyout.mat" }
            }, ".Regen");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.171 0", AnchorMax = "1 1.1", OffsetMax = "0 0" },
                Text = { Text = textanimal, Align = TextAnchor.MiddleLeft, FontSize = 15, Color = "1 1 1 0.6003938" }
            }, ".Regen");

            CuiHelper.AddUi(player, container);
        }
        private void IQEcoBalance(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ".HP");
            CuiElementContainer container = new CuiElementContainer();
            int balance = (int)IQEconomic.CallHook("API_GET_BALANCE", player);
            string pregresswood = "0.985 0.9";
            string textwood = $"Shop баланс: {balance}";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.315", OffsetMax = "0 0" },
                Image = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat" }
            }, ".RPG", ".HP");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.0125 0.155", AnchorMax = "0.11 0.845", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.5", Sprite = "assets/icons/connection.png" },
                Text = { Text = "" }
            }, ".HP");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.137 0.1", AnchorMax = pregresswood, OffsetMax = "0 0" },
                Image = { Color = "0.33 0.33 0.88 0.84", Material = "assets/icons/greyout.mat" }
            }, ".HP");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.171 0", AnchorMax = "1 1.1", OffsetMax = "0 0" },
                Text = { Text = textwood, Align = TextAnchor.MiddleLeft, FontSize = 15, Color = "1 1 1 0.6003938" }
            }, ".HP");

            CuiHelper.AddUi(player, container);
        }
        //private void HPUI(BasePlayer player)
        //{
        //    CuiHelper.DestroyUi(player, ".HP");
        //    CuiElementContainer container = new CuiElementContainer();

        //    float ratewood = StoredData[player.userID.Get()].Hp;
        //    float maxratewood = StoredData[player.userID.Get()].MaxHP;
        //    float hp = 100;
        //    float hookhp = (float)BetterHealth.CallHook("GetMaxHealth", player);
        //    if (hookhp != null || hookhp != 0)
        //    {
        //        hp = hookhp;
        //    }
        //    string pregresswood = hp >= maxratewood ? "0.985 0.9" : config.Panel.Progress ? $"{0.137 + ((hp / maxratewood) * 0.848)} 0.9" : $"{0.137 + (ratewood * (0.848 / maxratewood))} 0.9";
        //    string textwood = $"Хп: {hp}";
        //    container.Add(new CuiPanel
        //    {
        //        RectTransform = { AnchorMin = "0 0.685", AnchorMax = "1 1", OffsetMax = "0 0" },
        //        Image = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat" }
        //    }, ".RPG", ".HP");

        //    container.Add(new CuiButton
        //    {
        //        RectTransform = { AnchorMin = "0.0125 0.155", AnchorMax = "0.11 0.845", OffsetMax = "0 0" },
        //        Button = { Color = "1 1 1 0.5", Sprite = "assets/icons/level_wood.png" },
        //        Text = { Text = "" }
        //    }, ".HP");

        //    container.Add(new CuiPanel
        //    {
        //        RectTransform = { AnchorMin = "0.137 0.1", AnchorMax = pregresswood, OffsetMax = "0 0" },
        //        Image = { Color = "0.5586275 0.7376471 0.2484314 0.92921569", Material = "assets/icons/greyout.mat" }
        //    }, ".HP");

        //    container.Add(new CuiLabel
        //    {
        //        RectTransform = { AnchorMin = "0.171 0", AnchorMax = "1 1.1", OffsetMax = "0 0" },
        //        Text = { Text = textwood, Align = TextAnchor.MiddleLeft, FontSize = 15, Color = "1 1 1 0.6003938" }
        //    }, ".HP");

        //    CuiHelper.AddUi(player, container);
        //}
        private void RPGUI(BasePlayer player)
        {
            if (StoredData != null && StoredData.ContainsKey(player.userID.Get()) && !StoredData[player.userID.Get()].ActiveUI) return;
            CuiHelper.DestroyUi(player, ".RPG");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = config.Panel.AnchorMin, AnchorMax = config.Panel.AnchorMax, OffsetMin = config.Panel.OffsetMin, OffsetMax = config.Panel.OffsetMax },
                Image = { Color = "0 0 0 0" }
            }, "Hud", ".RPG");

            CuiHelper.AddUi(player, container);

            //HPUI(player);
            IQEcoBalance(player);
            DamageUI(player);
            RegenUI(player);
        }
        private void ShowUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ".Show");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-236 16", OffsetMax = "-210 42" },
                Button = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat", Command = "ui show" },
                Text = { Text = "<<", Align = TextAnchor.MiddleCenter, FontSize = 13, Color = "1 1 1 0.6" }
            }, "Overlay", ".Show");

            CuiHelper.AddUi(player, container);
        }
        private void HideUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, ".Hide");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-0.1475 0", AnchorMax = "-0.01 0.315", OffsetMax = "0 0" },
                Button = { Color = "0.9686275 0.9176471 0.8784314 0.02921569", Material = "assets/icons/greyout.mat", Command = "ui hide" },
                Text = { Text = ">>", Align = TextAnchor.MiddleCenter, FontSize = 15, Color = "1 1 1 0.6" }
            }, ".RPG", ".Hide");

            CuiHelper.AddUi(player, container);
        }
        [ConsoleCommand("ui")]
        void ccmdUI(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            switch (args.Args[0])
            {
                case "show":
                    {
                        CuiHelper.DestroyUi(player, ".Show");
                        StoredData[player.userID.Get()].ActiveUI = true;

                        RPGUI(player);
                        HideUI(player);
                        break;
                    }
                case "hide":
                    {
                        CuiHelper.DestroyUi(player, ".RPG");
                        CuiHelper.DestroyUi(player, ".Hide");
                        StoredData[player.userID.Get()].ActiveUI = false;

                        ShowUI(player);
                        break;
                    }
            }

            EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
        }
        void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            timer.Once(1, () =>
            {
                RPGUI(player);
            });
        }
        private void CheckAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    try { RPGUI(player); }
                    catch { }
                    ulong time = 0;
                    if (timesplayer.ContainsKey(player.userID.Get()) && !needexit.Contains(player.userID.Get()))
                    {
                        if (timesplayer.TryGetValue(player.userID.Get(), out time))
                        {
                            ulong ftime = time + GetSecondsConnected(player.Connection);
                            if (ftime >= 172800) //36000 //172800
                            {
                                float damage = (float)WeaponDamageScale.CallHook("GetDamageScale", player.userID.Get());
                                float hp = (float)BetterHealth.CallHook("GetMaxHealth", player);
                                int regen = (int)HealthTick.CallHook("GetMaxRegenForPlayer", player);
                                if (damage > StoredData[player.userID.Get()].MaxDamage && hp > StoredData[player.userID.Get()].MaxHP && regen > StoredData[player.userID.Get()].MaxRegen)
                                {
                                    timesplayer.Remove(player.userID.Get());
                                    return;
                                }
                                GiveNextPerms(player);
                                //timer.Once(1, () => { player.ChatMessage($"<color=#6A5ACD><b>[</color><color=#6b53f5>GORGONA</color><color=#6A5ACD>]</b></color> <color=#1E90FF>-</color> Вы успешно получили <color=#FF9740>{hp} xп</color>, <color=#FF9740>x{damage} урон(а)</color>, <color=#FF9740>{regen} реген(а)</color> за наигранные <color=#FF9740>10 часов</color> на сервере."); });
                                timesplayer.Remove(player.userID.Get());
                                needexit.Add(player.userID.Get());
                                player.ChatMessage("Поздравляем с получением дополнительного Урона, ХП и Регена!");
                                player.ChatMessage("Сейчас ваше игровое время не засчитывается. Чтобы оно начало засчитываться, пожалуйста, перезайдите на сервер!");
                                SaveData();
                                try { NextTick(() => RPGUI(player)); }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        if (!timesplayer.ContainsKey(player.userID.Get())) timesplayer.Add(player.userID.Get(), GetSecondsConnected(player.Connection));
                    }
                }
            }
        }
        void OnServerSave()
        {
            NextTick(() => CheckAllPlayers());
        }
        private void GiveNextPerms(BasePlayer player)
        {
            float damage = (float)WeaponDamageScale.CallHook("GetDamageScale", player.userID.Get());
            float hp = (float)BetterHealth.CallHook("GetMaxHealth", player);
            int regen = (int)HealthTick.CallHook("GetMaxRegenForPlayer", player);
            if (hp < StoredData[player.userID.Get()].MaxHP)
            {
                rust.RunServerCommand($"bhealthi {player.userID.Get()}");
            }
            if (damage < StoredData[player.userID.Get()].MaxDamage)
            {
                rust.RunServerCommand($"wpdamagei {player.userID.Get()}");
            }
            if (regen < StoredData[player.userID.Get()].MaxRegen)
            {
                rust.RunServerCommand($"hticki {player.userID.Get()}");
            }
        }
        private Dictionary<ulong, RPGData> StoredData = new Dictionary<ulong, RPGData>();
        private void OnUserPermissionGranted(string id, string permName) => CheckPermission(id, permName);


        private class RPGData
        {
            [JsonProperty("ХП")] public float Hp = 100;
            [JsonProperty("УРОН")] public float Damage = 1;
            [JsonProperty("РЕГЕН")] public float Regen = 0;
            [JsonProperty("Максимальный ХП")] public float MaxHP = 10000;
            [JsonProperty("Максимальный УРОН")] public float MaxDamage = 100;
            [JsonProperty("Максимальный РЕГЕН")] public float MaxRegen = 100;
            [JsonProperty("Активность UI")] public bool ActiveUI = true;
        }

        private void CheckPermission(string id, string permName)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(id));

            if (player != null)
                OnPlayerConnected(player);
        }

        private RPGConfig config;


        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            ulong time = 0;
            if (player == null) return;
            if (needexit.Contains(player.userID.Get())) return;
            if (timesplayer.ContainsKey(player.userID.Get()))
            {
                if (timesplayer.TryGetValue(player.userID.Get(), out time))
                {
                    timesplayer.Remove(player.userID.Get());
                    timesplayer.Add(player.userID.Get(), time + GetSecondsConnected(player.Connection));
                    SaveData();
                }
            }
            else
            {
                timesplayer.Add(player.userID.Get(), GetSecondsConnected(player.Connection));
                SaveData();
            }
        }
        public ulong GetSecondsConnected(Network.Connection connection)
        {
            if (connection == null) return 0;
            return (ulong)(TimeEx.realtimeSinceStartup - connection.connectionTime);
        }
        private void OnServerInitialized()
        {
            try
            {
                timesplayer = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ulong>>("FRPGSystemPlayerTime");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            CheckAllPlayers();
            timer.Every(60, () => { foreach (var player in BasePlayer.activePlayerList) IQEcoBalance(player); });
            timer.Every(90, () => { foreach (var player in BasePlayer.activePlayerList) player.ChatMessage($"<color=#6A5ACD><b>[</color><color=#6b53f5>GORGONA</color><color=#6A5ACD>]</b></color> <color=#1E90FF>-</color> За каждые наигранные <color=#4169E1>24 часа</color> вы будете получать <color=#16C553FF>подарки</color> <color=#4169E1>X1 урон и + 1 рег в сек.!</color>."); });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                if (needexit.Contains(player.userID.Get())) needexit.Remove(player.userID.Get());
                if (!timesplayer.ContainsKey(player.userID.Get())) timesplayer.Add(player.userID.Get(), 0);
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            if (!StoredData.ContainsKey(player.userID.Get()))
            {
                StoredData.Add(player.userID.Get(), new RPGData());
                StoredData[player.userID.Get()].ActiveUI = false;

                ShowUI(player);
            }
            else
            {
                if (StoredData[player.userID.Get()].ActiveUI)
                {
                    RPGUI(player);
                    HideUI(player);
                }
                else
                {
                    ShowUI(player);
                }
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("FRPGSystemPlayerTime", timesplayer);
        }
        private void OnUserPermissionRevoked(string id, string permName) => CheckPermission(id, permName);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<RPGConfig>();
            }
            catch
            {
                PrintWarning("Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, ".RPG");
            }

            SaveData();
            config = null;
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        private class RPGConfig
        {
            internal class PanelSetting
            {
                [JsonProperty("Отображение прогресса: TRUE - от минимальных до максимальных рейтов | FALSE - от 0 до максимальных рейтов ")] public bool Progress;
                [JsonProperty("AnchorMin")] public string AnchorMin;
                [JsonProperty("OffsetMin")] public string OffsetMin;
                [JsonProperty("OffsetMax")] public string OffsetMax;
                [JsonProperty("AnchorMax")] public string AnchorMax;
            }
            [JsonProperty("Расположение мини-панели")]
            public PanelSetting Panel = new PanelSetting();
            public static RPGConfig GetNewConfiguration()
            {
                return new RPGConfig
                {
                    Panel = new PanelSetting
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = "-402 16",
                        OffsetMax = "-210 98",
                        Progress = true
                    }
                };
            }
            internal class SSetting
            {
                [JsonProperty("Включить рпг панель")] public bool Panel;
                [JsonProperty("Размер текста")] public int TextSize;
            }
        }
    }
}