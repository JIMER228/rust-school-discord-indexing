using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AProtection", "test ", "1.1.0")]
    public class AProtection : RustPlugin
    {
        #region Variables

        private class IAuthorize
        {
            [JsonIgnore] public bool IsAuthed;
            [JsonIgnore] public Vector3 LastPosition;
            [JsonIgnore] public Timer Timer;

            [JsonProperty("Пароль пользователя")]
            public string Password;
            [JsonProperty("Авторизованные IP адреса")]
            public List<string> AuthedIP;

            public void Authed(BasePlayer player, string ip)
            {
                if (Timer != null && !Timer.Destroyed)
                    Timer.Destroy();

                if (!AuthedIP.Contains(ip))
                    AuthedIP.Add(ip);
                CuiHelper.DestroyUi(player, Layer + ".Hide2");
                CuiHelper.DestroyUi(player, Layer + ".Hide1");
                CuiHelper.DestroyUi(player, Layer + ".Hide");
                CuiHelper.DestroyUi(player, Layer);
            }
        }

        [JsonProperty("Информация об игроках")]
        private Dictionary<ulong, IAuthorize> StoredData = new Dictionary<ulong,IAuthorize>();
        [JsonProperty("Слой с интерфейсом")] private static string Layer = "UI.Login";

        #endregion

        #region Initialization

        void Loaded()
        {
          OnServerInitialized();
        }

        private void OnServerInitialized()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["АВТОРИЗОВАН.IP"] =           "<size=16>Вы автоматически <color=#ff6600>авторизовались</color>:</size>" +
                                               "\n<size=10>Вы уже авторизовались с IP: {0}</size>",

                ["ПОМОЩЬ.ЗАРЕГИСТРИРУЙТЕСЬ"] = "<size=16>Вы ещё не зарегистрировались.</size>" +
                                               "\nЗайдите в консоль F1 (<color=#ff6600>F1</color>) для регистрации"
            }, this);

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("AProtection"))
                StoredData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, IAuthorize>>("AProtection");

            BasePlayer.activePlayerList.ForEach(OnPlayerInit);
        }

        private void Unload() =>
            Interface.Oxide.DataFileSystem.WriteObject("AProtection", StoredData);

        #endregion

        #region Hooks

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !StoredData.ContainsKey(player.userID))
                return null;

            IAuthorize buffer = StoredData[player.userID];
            if (!buffer.IsAuthed && arg.cmd.namefull.ToLower() != "global.auth")
                return false;

            return null;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot())
            {
                NextTick(() => OnPlayerInit(player));
                return;
            }

            string IP = GetIP(player.net.connection.ipaddress);

            if (!StoredData.ContainsKey(player.userID))
            {
                IAuthorize buffer = new IAuthorize();
                buffer.AuthedIP = new List<string>() /*{ IP }*/;
                buffer.LastPosition = player.transform.position;
                buffer.IsAuthed = false;
                StoredData.Add(player.userID, buffer);
            }
            else
            {
                IAuthorize buffer = StoredData[player.userID];
                buffer.LastPosition = player.transform.position;
                buffer.IsAuthed = false;

                if (buffer.AuthedIP.Contains(IP))
                {
                    buffer.IsAuthed = true;
                    player.ChatMessage(FL("АВТОРИЗОВАН.IP").Replace("{0}", IP));
                    return;
                }
            }

            ForceAuthorization(player);
            DrawInterface(player);
        }

        #endregion

        #region Commands

        [ConsoleCommand("auth")]
        private void cmdAuthConsole(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null)
                return;

            if (!StoredData.ContainsKey(player.userID))
                OnPlayerInit(player);

            if (!args.HasArgs(1) || args.ArgsStr.Length == 0)
            {
                args.ReplyWithObject("Некорректный пароль, попробуйте увеличить его длинну");
                return;
            }

            string ip = GetIP(player.net.connection.ipaddress);
            string password = args.ArgsStr;
            IAuthorize buffer = StoredData[player.userID];
            if (buffer.IsAuthed)
                return;

            if (string.IsNullOrEmpty(buffer.Password))
            {
                args.ReplyWithObject($"Вы успешно зарегистрировались на сервере!\n" +
                                     $"Зарегестрированный пароль: {password}");
                buffer.Password = password;
                buffer.IsAuthed = true;
                buffer.Authed(player, ip);
            }
            else if (password == buffer.Password)
            {
                args.ReplyWithObject($"Вы успешно авторизовались на сервере!");
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, false);
                buffer.IsAuthed = true;
                buffer.Authed(player, ip);
            }
            else
            {
                args.ReplyWithObject($"Неверный пароль!" +
                                     $"Команда для авторизации на сервере (auth)");
                return;
            }
        }

        #endregion

        #region Functions

        private void ForceAuthorization(BasePlayer player)
        {
            IAuthorize buffer = StoredData[player.userID];
            if (buffer.IsAuthed)
                return;

            if (string.IsNullOrEmpty(buffer.Password))
            {
                player.ChatMessage(FL("ПОМОЩЬ.ЗАРЕГИСТРИРУЙТЕСЬ"));
                player.SendConsoleCommand($"echo Добро пожаловать на сервер, пройдите регистрацию аккаунта!\n" +
                                          $"Вам больше не придётся вводить пароль c IP: {GetIP(player.net.connection.ipaddress)}\n" +
                                          $"Команда: auth <пароль>");
                buffer.Timer = timer.Once(10, () => ForceAuthorization(player));
                return;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, true);
            player.SendConsoleCommand($"echo Добро пожаловать на сервер, пройдите авторизацию аккаунта!\n" +
                                      $"Вам больше не придётся вводить пароль c IP: {GetIP(player.net.connection.ipaddress)}\n" +
                                      $"Команда: auth <пароль>");
            player.Teleport(buffer.LastPosition);
            buffer.Timer = timer.Once(1, () => ForceAuthorization(player));
        }

        #endregion

        #region Helpers

        private string GetIP(string input)
        {
            if (input.Contains(":"))
                return input.Split(':')[0];

            return input;
        }

        private string FL(string key) => lang.GetMessage(key, this);

        #endregion

        #region GUI

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

        private void DrawInterface(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            string action = string.IsNullOrEmpty(StoredData[player.userID].Password) ? "Пройдите регистрацию аккаунта!" : "Пройдите авторизацию на сервере!";

            container.Add(new CuiPanel
            {
                FadeOut = 2f,
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { FadeIn = 2f, Color = "0 0 0 0.78" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                FadeOut = 2f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { FadeIn = 2f, Text = "", FontSize = 32, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter},
                Button = { FadeIn = 2f, Color = "0 0 0 0" },
            }, Layer);

            container.Add(new CuiLabel
            {
                FadeOut = 2f,
                RectTransform = { AnchorMin = "0 0.04999995", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { FadeIn = 2f, Text = "Пройдите авторизацию аккаунта", FontSize = 48, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5520477"},
            }, Layer);

            container.Add(new CuiLabel
            {
                FadeOut = 2f,
                RectTransform = { AnchorMin = "0 0.03796297", AnchorMax = "1 0.8842593", OffsetMax = "0 0" },
                Text = { FadeIn = 2f, Text = "Вся информация находится в консоли F1", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.3870119"},
            }, Layer);

            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}
