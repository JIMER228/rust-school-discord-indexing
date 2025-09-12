using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.IO;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AnScreenLogo", "^^", "0.0.1")]
    [Description("Displays the button on the player screen.")]
    class AnScreenLogo : RustPlugin
    {
        private string PanelName = "GsAdX1wazasdsHs";
        private string Image = "";

        #region Config Setup
        private string Amax = "0.34 0.105";
        private string Amin = "0.26 0.025";
        private string ImageAddress = "https://fedoraproject.org/w/uploads/e/ee/Edition-server-full_one-color_black.png";
        #endregion

        #region ImLibrary
        [PluginReference] Plugin ImageLibrary;
        string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        #endregion

        #region Initialization
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            GetConfig("Image. Link or name of the file in the data folder", ref ImageAddress);
            GetConfig("Minimum anchor", ref Amin);
            GetConfig("Maximum anchor", ref Amax);
            if (!ImageAddress.ToLower().Contains("http"))
            {
                ImageAddress = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + ImageAddress;
            }
            permission.RegisterPermission("OnScreenLogo.refresh", this);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            AddImage(ImageAddress, ImageAddress);
            gettimage();
        }

        void gettimage()
        {
            Image = GetImage(ImageAddress);
            if (!Image.Equals("39274839"))
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CreateButton(player);
                }
                Debug.Log("Успешно подгрузили картинку.");
                return;
            }
            timer.Once(1f, () => gettimage());
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, PanelName);
        }
        #endregion


        #region UI
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            CreateButton(player);
        }

        private void CreateButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelName);
            CuiElementContainer elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0 0 0 0"
                },
                RectTransform = {
                    AnchorMin = Amin,
                    AnchorMax = Amax
                },
                CursorEnabled = false
            }, "Overlay", PanelName);
            var comp = new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga" };
            if (!string.IsNullOrEmpty(Image)) comp.Png = Image;
            elements.Add(new CuiElement
            {
                Parent = PanelName,
                Components =
                {
                    comp,
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Helpers
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
        #endregion
    }
}