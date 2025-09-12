// Reference: System.Drawing
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core.Logging;
using Rust;
using System.IO;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("Building Upgrade", "OxideBro", "1.0.0")]
    class BuildingUpgrade : RustPlugin
    {
        [PluginReference]
        private Plugin NoEscape;


        private MethodInfo payForUpgrade =
            typeof(BuildingBlock).GetMethod("PayForUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private MethodInfo getGrade =
            typeof(BuildingBlock).GetMethod("GetGrade",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private MethodInfo canUpgrade =
            typeof(BuildingBlock).GetMethod("CanAffordUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic);

        #region Fields

        Dictionary<BuildingGrade.Enum, string> gradesString = new Dictionary<BuildingGrade.Enum, string>()
        {
            {BuildingGrade.Enum.Wood, "<color=#EC402C>Дерева</color>"},
            {BuildingGrade.Enum.Stone, "<color=#EC402C>Камня</color>"},
            {BuildingGrade.Enum.Metal, "<color=#EC402C>Метала</color>"},
            {BuildingGrade.Enum.TopTier, "<color=#EC402C>Армора</color>"}
        };

        Dictionary<BasePlayer, BuildingGrade.Enum> grades = new Dictionary<BasePlayer, BuildingGrade.Enum>();

        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();
        #endregion

        #region CONFIGURATION

        // Основные настройки
        private int resetTime = 40;
        private string permissionAutoGrade = "buildingupgrade.build";
        private string permissionAutoGradeFree = "buildingupgrade.free";
        private string permissionAutoGradeHammer = "buildingupgrade.hammer";
        private bool permissionAutoGradeAdmin = true;
        private bool getBuild = true;
        private bool permissionOn = true;
        private bool useNoEscape = true;

        private bool CanUpgradeDamaged = false;
        // Настройки GUI Panel
        private string PanelAnchorMin = "0.0 0.908";
        private string PanelAnchorMax = "1 0.958";
        private string PanelColor = "0 0 0 0.50";

        // Настройки GUI Text
        private int TextFontSize = 16;
        private string TextСolor = "0 0 0 1";
        private string TextAnchorMin = "0.0 0.870";
        private string TextAnchorMax = "1 1";

        // Сообщения
        private string MessageAutoGradePremHammer = "У вас нету доступа к улучшению киянкой!";
        private string MessageAutoGradePrem = "У вас нету доступа к данной команде!";
        private string MessageAutoGradeNo = "<color=ffcc00><size=16>Для улучшения нехватает ресурсов!!!</size></color>";
        private string MessageAutoGradeOn = "<size=14><color=#EC402C>Upgrade включен!</color> \nДля быстрого переключения используйте: <color=#EC402C>/upgrade 0-4</color></size>";
        private string MessageAutoGradeOff = "<color=ffcc00><size=14>Вы отключили <color=#EC402C>Upgrade!</color></size></color>";
        private void LoadDefaultConfig()
        {
            GetConfig("Основные настройки", "Через сколько секунд автоматически выключать улучшение строений", ref resetTime);
            GetConfig("Основные настройки", "Привилегия что бы позволить улучшать объекты при строительстве", ref permissionAutoGrade);
            GetConfig("Основные настройки", "Включить доступ только по привилегиям?", ref permissionOn);
            GetConfig("Основные настройки", "Включить поддержку NoEscape (Запретить Upgrade в Raid Block)?", ref useNoEscape);
            GetConfig("Основные настройки", "Включить бесплатный Upgrade для администраторов?", ref permissionAutoGradeAdmin);
            GetConfig("Основные настройки", "Привилегия для улучшения при строительстве и ударе киянкой без траты ресурсов", ref permissionAutoGradeFree);
            GetConfig("Основные настройки", "Привилегия что бы позволить улучшать объекты ударом киянки", ref permissionAutoGradeHammer);
            GetConfig("Основные настройки", "Запретить Upgrade в Building Block?", ref getBuild);
            GetConfig("Основные настройки", "Разрешить улучшать повреждённые постройки?", ref CanUpgradeDamaged);
            GetConfig("Настройки GUI Panel", "Минимальный отступ:", ref PanelAnchorMin);
            GetConfig("Настройки GUI Panel", "Максимальный отступ:", ref PanelAnchorMax);
            GetConfig("Настройки GUI Panel", "Цвет фона:", ref PanelColor);
            GetConfig("Настройки GUI Text", "Размер текста в gui панели:", ref TextFontSize);
            GetConfig("Настройки GUI Text", "Цвет текста в gui панели:", ref TextСolor);
            GetConfig("Настройки GUI Text", "Минимальный отступ в gui панели:", ref TextAnchorMin);
            GetConfig("Настройки GUI Text", "Максимальный отступ в gui панели:", ref TextAnchorMax);
            GetConfig("Сообщения", "No Permissions Hammer:", ref MessageAutoGradePremHammer);
            GetConfig("Сообщения", "No Permissions:", ref MessageAutoGradePrem);
            GetConfig("Сообщения", "No Resources:", ref MessageAutoGradeNo);
            GetConfig("Сообщения", "Сообщение при включение Upgrade:", ref MessageAutoGradeOn);
            GetConfig("Сообщения", "Сообщение при выключение Upgrade:", ref MessageAutoGradeOff);
            SaveConfig();
        }


        private void GetConfig<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
            }
            Config[MainMenu, Key] = var;
        }

        #endregion

        #region COMMANDS
        [ChatCommand("upgrade")]
        void cmdAutoGrade(BasePlayer player, string command, string[] args)
        {
            if (permissionOn && !permission.UserHasPermission(player.UserIDString, permissionAutoGrade))
            {
                SendReply(player, MessageAutoGradePrem);
                return;
            }
            
        
            int grade;
            timers[player] = resetTime;

            if (player == null) return;
            if (args.Count() >= 1 && args[0] == "1")

            {
                grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                DrawUI(player, BuildingGrade.Enum.Wood, resetTime);
            }
            if (args.Count() >= 1 && args[0] == "2")
            {
                grade = (int)(grades[player] = BuildingGrade.Enum.Stone);
                DrawUI(player, BuildingGrade.Enum.Stone, resetTime);
            }
            if (args.Count() >= 1 && args[0] == "3")
            {
                grade = (int)(grades[player] = BuildingGrade.Enum.Metal);
                DrawUI(player, BuildingGrade.Enum.Metal, resetTime);
            }
            if (args.Count() >= 1 && args[0] == "4")
            {
                grade = (int)(grades[player] = BuildingGrade.Enum.TopTier);
                DrawUI(player, BuildingGrade.Enum.TopTier, resetTime);
            }
            if (args.Count() >= 1 && args[0] == "0")
            {
                grades.Remove(player);
                timers.Remove(player);
                DestroyUI(player);
                SendReply(player, MessageAutoGradeOff);
                return;
            }

            if (args == null || args.Length <= 0)
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, MessageAutoGradeOn);

                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }

                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, MessageAutoGradeOff);
                    return;
                }
                timers[player] = resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, resetTime);
            }
        }

        [ConsoleCommand("building.upgrade")]
        void consoleAutoGrade(ConsoleSystem.Arg arg, string[] args)
        {
            var player = arg.Player();
            if (permissionOn && !permission.UserHasPermission(player.UserIDString, permissionAutoGrade))
            {
                SendReply(player, MessageAutoGradePrem);
                return;
            }
            int grade;
            timers[player] = resetTime;

            if (player == null) return;
            if (args == null || args.Length <= 0)
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, MessageAutoGradeOn);

                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }

                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, MessageAutoGradeOff);
                    return;
                }
                timers[player] = resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, resetTime);
            }
        }


        #endregion

        #region OXIDE HOOKS
        private void Init()
        {
            permission.RegisterPermission(permissionAutoGrade, this);
            permission.RegisterPermission(permissionAutoGradeFree, this);
            permission.RegisterPermission(permissionAutoGradeHammer, this);
        }
            void OnServerInitialized()
        {
            LoadConfig();
            LoadDefaultConfig();
            timer.Every(1f, GradeTimerHandler);
        }
        void OnHammerHit(BasePlayer player, HitInfo info)
        {
           
            var buildingBlock = info.HitEntity as BuildingBlock;
            if (buildingBlock == null || player == null)
                return;

            if (permissionOn && !permission.UserHasPermission(player.UserIDString, permissionAutoGradeHammer))
            {
                SendReply(player, MessageAutoGradePremHammer);
                return;
            }
            Grade(buildingBlock, player);
        }
        void OnEntityBuilt(Planner planner, UnityEngine.GameObject gameObject)
        {
            if (planner == null || gameObject == null) return;
            var player = planner.GetOwnerPlayer();
            BuildingGrade.Enum grade;
            BuildingBlock entity = gameObject.ToBaseEntity() as BuildingBlock;
            if (entity == null || entity.IsDestroyed) return;
            if (player == null) return;
            var buildingBlock = gameObject.GetComponent<BuildingBlock>();
            var buildingGrade = (int)buildingBlock.grade;

            Grade(entity, player);
        }
        void Grade(BuildingBlock block, BasePlayer player)
        {
            BuildingGrade.Enum grade;
            if (useNoEscape)
            { 
            object can = NoEscape?.Call("IsRaidBlocked", player);
            if (can != null)
                if ((bool)can == true)
                {
                    SendReply(player, "Вы не можете использовать Upgrade во время рейд-блока");
                    return;
                }
            }
            if (!grades.TryGetValue(player, out grade) || grade == BuildingGrade.Enum.Count)
                return;
            if (block == null) return;
            if (!((int)grade >= 1 && (int)grade <= 4)) return;
            var targetLocation = player.transform.position + (player.eyes.BodyForward() * 4f);
            if (getBuild && player.IsBuildingBlocked(targetLocation, new Quaternion(0, 0, 0, 0), new Bounds(Vector3.zero, Vector3.zero)))
            {
                player.ChatMessage("<color=ffcc00><size=16><color=#EC402C>Upgrade</color> запрещен в билдинг блоке!!!</size></color>");
                return;
            }
            if (permissionAutoGradeAdmin != player != player.net?.connection?.authLevel >= 2 || player == !permission.UserHasPermission(player.UserIDString, permissionAutoGradeFree) == (bool)canUpgrade.Invoke(block, new object[] { grade, player }))
            {
                var ret = Interface.Call("CanUpgrade", player) as string;

                if (ret != null)
                {
                    SendReply(player, ret);
                    return;
                }
                if(block.grade > grade)
                {
                    SendReply(player, "Нельзя понижать уровень строения!");
                    return;
                }
                if(block.grade == grade)
                {
                    SendReply(player, "Уровень строения соответствует выбранному.");
                    return;
                }
                if(block.Health() != block.MaxHealth() && !CanUpgradeDamaged)
                {
                    SendReply(player, "Нельзя улучшать повреждённые постройки!");
                    return;
                }
                if (permissionAutoGradeAdmin == player != player.net?.connection?.authLevel >= 2)
                {
                    if (player == !permission.UserHasPermission(player.UserIDString, permissionAutoGradeFree))
                    {
                        payForUpgrade.Invoke(block, new object[] { getGrade.Invoke(block, new object[] { grade }), player });
                    }
                }

                block.SetGrade(grade);
                block.SetHealthToMax();
                block.UpdateSkin(false);
                Effect.server.Run(
                    string.Concat("assets/bundled/prefabs/fx/build/promote_", grade.ToString().ToLower(), ".prefab"),
                    block,
                    0, Vector3.zero, Vector3.zero, null, false);
                timers[player] = resetTime;
                DrawUI(player, grade, resetTime);
            }
            else
            {
                SendReply(player, MessageAutoGradeNo);
            }
        }



        #endregion

        #region CORE
        int NextGrade(int grade) => ++grade;

        void GradeTimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    BuildingGrade.Enum mode;
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    continue;
                }
                DrawUI(player, grades[player], seconds);
            }
        }

        #endregion

        #region UI
        void DrawUI(BasePlayer player, BuildingGrade.Enum grade, int seconds)
        {
            DestroyUI(player);
            CuiHelper.AddUi(player,
                GUI.Replace("{0}", gradesString[grade]).Replace("{1}", seconds.ToString())
                   .Replace("{PanelColor}", PanelColor.ToString())
                   .Replace("{PanelAnchorMin}", PanelAnchorMin.ToString())
                   .Replace("{PanelAnchorMax}", PanelAnchorMax.ToString())
                   .Replace("{TextFontSize}", TextFontSize.ToString())
                   .Replace("{TextСolor}", TextСolor.ToString())
                   .Replace("{TextAnchorMin}", TextAnchorMin.ToString())
                   .Replace("{TextAnchorMax}", TextAnchorMax.ToString()));
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }


        private string GUI = @"[{
	""name"": ""autograde.panel"",
	""parent"": ""Hud"",
	""components"": [{
		""type"": ""UnityEngine.UI.Image"",
        ""color"": ""{PanelColor}""
	}, {
		""type"": ""RectTransform"",
        ""anchormin"": ""{PanelAnchorMin}"",
        ""anchormax"": ""{PanelAnchorMax}""
	}]
}, {
	""name"": ""autogradetext"",
	""parent"": ""Hud"",
	""components"": [{
		""type"": ""UnityEngine.UI.Text"",
		""text"": ""Режим улучшения строения до {0} выключится через " + @"{1} секунд."",
		""fontSize"": ""{TextFontSize}"",
		""align"": ""MiddleCenter""
	}, {
		""type"": ""UnityEngine.UI.Outline"",
		""color"": ""{TextСolor}"",
		""distance"": ""0.1 -0.1""
	}, {
		""type"": ""RectTransform"",
        ""anchormin"": ""{TextAnchorMin}"",
        ""anchormax"": ""{TextAnchorMax}""
	}]
}]";

        #endregion

        #region API
        void UpdateTimer(BasePlayer player)
        {
            timers[player] = resetTime;
            DrawUI(player, grades[player], timers[player]);
        }
        #endregion
    }
}
