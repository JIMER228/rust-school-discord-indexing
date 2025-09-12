using Oxide.Game.Rust.Cui;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Logo", "ferq3ns", "1.0.0")]
    public class Logo : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary = null;

        private Boolean HasImage(String imageName, UInt64 imageId = 0) => ImageLibrary?.Call<Boolean>("HasImage", imageName, imageId) ?? false;
        private Boolean AddImage(String url, String shortname, UInt64 skin = 0) => ImageLibrary?.Call<Boolean>("AddImage", url, shortname, skin) ?? false;
        private String GetImage(String shortname, UInt64 skin = 0) => ImageLibrary?.Call<String>("GetImage", shortname, skin);

        private const string LogoImageUrl = "https://i.postimg.cc/zG2dR2jW/beach220.png";

        private void OnServerInitialized()
        {
            if (ImageLibrary == null) return;

            if (plugins.Find("ImageLibrary") != null)
            {
                Interface.CallHook("AddImage", LogoImageUrl, "logo", 0UL);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                ShowUI(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            timer.Once(2f, () => ShowUI(player));
        }

        private void ShowUI(BasePlayer player)
        {
            DestroyUI(player);

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                Button = { Command = "info", Color = "0.82 0.8 0.82 0.48", Close = "" }, 
                RectTransform = { AnchorMin = "0.005 0.995", AnchorMax = "0.005 0.995", OffsetMin = "5 -30", OffsetMax = "30 -5" },
                Text = { Text = "", FontSize = 0 }
            },  Layer, "main");

            container.Add(new CuiElement
            {
                Button = { Color = "1 1 1 0.8" }, 
                RectTransform = {  AnchorMin = "0 0", AnchorMax = "0.94 0.94" },
            },  "blur_bg", "main");

            container.Add(new CuiElement
            {
                Button = { Color = "1 1 1 1", Url = LogoImageUrl }, 
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.94 0.94" },
            },  "logo", "main");

            CuiHelper.AddUi(player, container);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "main");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) DestroyUI(player);
        }
    }
}