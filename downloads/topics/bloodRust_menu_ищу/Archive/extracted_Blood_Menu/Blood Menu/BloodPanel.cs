using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BloodPanel", "[LimePlugin] Chibubrik", "1.0.0")]
    class BloodPanel : RustPlugin
    {
        #region Вар
        string Layer = "Panel_UI";

        [PluginReference] Plugin ImageLibrary;
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("Ссылка на логотип")] public string Url = "https://i.postimg.cc/FsWsD7LS/ba50956034dd762f6a3eca9f97888d96-Photoroom-1.png";
            [JsonProperty("Исполняемая команда")] public string Command = "menu";
            public static Configuration GetNewCong()
            {
                return new Configuration();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", config.Url, config.Url);
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }
        void OnPlayerConnected(BasePlayer player)
        {
            PanelUI(player);
        }
        #endregion

        #region Интерфейс
        void PanelUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-250 6", OffsetMax = "-218 38" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Hud", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "" },
                Image = { Color = HexToCuiColor("#363636", 30), Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", config.Url), Color = "1 1 1 0.3" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "" },
                Button = { Color = "0 0 0 0", Command = $"chat.say /{config.Command}" },
                Text = { Text = "" }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Хелпер
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        #endregion
    }
}