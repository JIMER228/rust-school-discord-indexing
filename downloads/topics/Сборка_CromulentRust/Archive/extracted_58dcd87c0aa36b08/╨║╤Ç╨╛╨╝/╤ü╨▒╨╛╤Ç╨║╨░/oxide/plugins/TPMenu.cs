using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins;

[Info("TPMenu", "Baks", "1.1")]
public class TPMenu : RustPlugin
{
    #region Fields

    [PluginReference] private Plugin Teleportation, NTeleportation, Teleport, HomesGUI, Friends, MutualPermission;
    private static TPMenu _;
    private static ImageUI _imageUI;

    #endregion

    #region Hooks

    void OnServerInitialized()
    {
        _ = this;
        _imageUI = new ImageUI();
        _imageUI.DownloadImage();
    }

    void Unload()
    {
        if (_imageUI != null)
        {
            _imageUI.UnloadImages();
            _imageUI = null;
        }
        _ = null;
    }

    #endregion

    #region DataAndMethods

    Dictionary<string, Vector3> GetHomes(BasePlayer player)
    {
        var a1 = (Dictionary<string, Vector3>)NTeleportation?.Call("API_GetHomes", player) ?? new Dictionary<string, Vector3>();
        var a2 = (Dictionary<string, Vector3>)Teleport?.Call("ApiGetHomes", player.userID.Get()) ?? new Dictionary<string, Vector3>();
        var a3 = (Dictionary<string, Vector3>)Teleportation?.Call("GetHomes", player.userID.Get()) ?? new Dictionary<string, Vector3>();
        var a4 = (Dictionary<string, Vector3>)HomesGUI?.Call("GetPlayerHomes", player.UserIDString) ?? new Dictionary<string, Vector3>();
        return a1.Concat(a2).Concat(a3).Concat(a4).GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.First().Value);
    }

    private int GetMaxHomes(BasePlayer player)
    {
        return Teleportation.Call<int>("GetHomeLimit", player.userID.Get());
    }

    int GetMaxFriends()
    {
        return Friends.Call<int>("GetMaxFriends");
    }

    public List<ulong> GetFriends(ulong playerid = 0)
    {
        if (MutualPermission)
        {
            var MutualFr = MutualPermission?.Call("GetFriends", playerid) as List<ulong>;
            return MutualFr;
        }
        if (Friends)
        {
            var friends = Friends?.Call("GetFriends", playerid) as ulong[];
            if (friends == null) return new List<ulong>();
            return friends.ToList();
        }


        /*ulong[] friends = Friends.Call<ulong[]>("GetFriends", playerid);
        PrintWarning($"null?{friends == null}");
        if (friends == null) return new List<ulong>();
        return friends.ToList();
        return new List<ulong>();*/
        return new List<ulong>();

    }

    private string GetGridString(Vector3 position)
    {
        Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
        return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
    }

    private string NumberToString(int number)
    {
        bool a = number > 26;
        Char c = (Char)(65 + (a ? number - 26 : number));
        return a ? "A" + c : c.ToString();
    }

    #endregion

    #region UI

    #region Images

    private class ImageUI
    {
        private const String _path = "CromulentMenu/";
        private const String _printPath = "data/" + _path;
        private readonly Dictionary<String, ImageData> _images = new()
            {
                { "tpMain", new ImageData() },
                { "homefree", new ImageData() },
                { "homebtn", new ImageData() },
                { "friendfree", new ImageData() },
            };

        private enum ImageStatus
        {
            NotLoaded,
            Loaded,
            Failed
        }

        private class ImageData
        {
            public ImageStatus Status = ImageStatus.NotLoaded;
            public string Id { get; set; }
        }

        public string GetImage(string name)
        {
            ImageData image;
            if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
                return image.Id;
            return null;
        }

        public void DownloadImage()
        {
            KeyValuePair<string, ImageData>? image = null;
            foreach (KeyValuePair<string, ImageData> img in _images)
            {
                if (img.Value.Status == ImageStatus.NotLoaded)
                {
                    image = img;
                    break;
                }
            }

            if (image != null)
            {
                ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
            }
            else
            {
                List<String> failedImages = new List<string>();

                foreach (KeyValuePair<String, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.Failed)
                    {
                        failedImages.Add(img.Key);
                    }
                }

                if (failedImages.Count > 0)
                {
                    String images = String.Join(", ", failedImages);
                    _.PrintError($"Не удалось загрузить изображения: {images}.");
                    Interface.Oxide.UnloadPlugin(_.Name);
                }
                else
                {
                    _.Puts($"{_images.Count} изображений успешно загружено!");
                }
            }
        }

        public void UnloadImages()
        {
            foreach (KeyValuePair<string, ImageData> item in _images)
                if (item.Value.Status == ImageStatus.Loaded)
                    if (item.Value?.Id != null)
                        FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

            _images?.Clear();
        }

        private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
        {
            string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";

            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    image.Value.Status = ImageStatus.Failed;
                }
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    image.Value.Status = ImageStatus.Loaded;
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                DownloadImage();
            }
        }
    }



    #endregion

    private string _layer = "TPMenuUI";
    private string _main = "TPMenuUI.Main";
    void TeleportMenuUI(BasePlayer player)
    {
        CuiHelper.DestroyUi(player, _layer);
        CuiElementContainer container = new CuiElementContainer();
        container.Add(new CuiPanel
        {
            Image = { Color = HexToRustFormat("#171025d8") },
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            CursorEnabled = true
        }, "Overlay", _layer);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            Button = { Color = "0 0 0 0", Close = _layer },
            Text = { Text = "" }
        }, _layer);
        /*container.Add(new CuiPanel
        {
            Image = {Color = "0 0 0 0"},
            RectTransform = { AnchorMin = "0.5 0.55",AnchorMax = "0.5 0.55",OffsetMin = "-332.5 -208.5",OffsetMax = "332.5 208.5"}
        }, _layer, _main);*/
        container.Add(new CuiElement
        {
            Parent = _layer,
            Name = _main,
            Components =
            {
                new CuiRawImageComponent
                {
                    Png = _imageUI.GetImage("tpMain")
                },
                new CuiRectTransformComponent
                {
                    AnchorMin = "0.5 0.55",AnchorMax = "0.5 0.55",OffsetMin = "-332.5 -208.5",OffsetMax = "332.5 208.5"
                }
            }
        });
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-59 -55", OffsetMax = "0 0" },
            Button =
            { Close = _layer,Color = "0 0 0 0" },
            Text = { Text = "" }
        }, _main);
        #region TownButtons

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "203 283", OffsetMax = "265 341" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "tpm.comaand /otp"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "169 245", OffsetMax = "299 277" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "tpm.comaand /otp"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "348 283", OffsetMax = "410 341" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "tpm.comaand /btp"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "314 245", OffsetMax = "444 277" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "tpm.comaand /btp"
            },
            Text = { Text = "" }
        }, _main);
        #endregion

        #region TPAccpetDecline

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "203 145", OffsetMax = "266 204" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "chat.say /tpa"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "168 108", OffsetMax = "300 141" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "chat.say /tpa"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "347 145", OffsetMax = "410 203" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "chat.say /tpc"
            },
            Text = { Text = "" }
        }, _main);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "313 108", OffsetMax = "444 141" },
            Button =
            {
                Color = "0 0 0 0",
                Close = _layer,
                Command = "chat.say /tpc"
            },
            Text = { Text = "" }
        }, _main);
        #endregion


        #region Homes

        float y = -80, height = 31.55f, padding = 4;

        Dictionary<string, Vector3> playerHomes = GetHomes(player);
        int maxHomes = GetMaxHomes(player);
        if (playerHomes.Count < maxHomes)
        {
            int i = 0;
            while (playerHomes.Count < maxHomes)
            {
                playerHomes.Add($"free_null_home{i}", new Vector3());
                i++;
            }
        }
        int hcount = 0;
        foreach (var home in playerHomes)
        {
            string command = "ДОМ";
            string btnText = "home";
            bool free = true;
            if (home.Value == new Vector3())
            {

                var name = GetGridString(player.transform.position);

                if (playerHomes.ContainsKey(name))
                {
                    var count = playerHomes.Where(p => p.Key.Contains(name)).Count();
                    name = name + $"({count})";
                }

                command = $"tpm.comaand /sethome {name}";
                btnText = "";
                //command = $"chat.say \"/sethome {name}\"";
            }
            else
            {
                free = false;
                btnText = home.Key;
                command = $"tpm.comaand /home {home.Key}";
                //command = $"chat.say \"/home {home.Key}\"";
            }

            string homelayer = $"Home{hcount}";

            container.Add(new CuiElement
            {
                Parent = _main,
                Name = homelayer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = free ? _imageUI.GetImage("homefree") : _imageUI.GetImage("homebtn")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"11 {y-height}",OffsetMax = $"140.64 {y}"
                    }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = command, Close = _layer },
                Text = { Text = btnText, Color = HexToRustFormat("#C7C9FF"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }
            }, homelayer);

            hcount++;
            y -= height + padding;
        }


        #endregion

        #region Friends

        y = -80;
        height = 31.55f;
        padding = 4;
        List<ulong> friends = GetFriends(player.userID.Get());
        friends = GetFriends(player.userID.Get());
        int maxFriends = GetMaxFriends();
        if (friends.Count < maxFriends)
        {
            ulong i = 0;
            while (friends.Count < maxFriends)
            {
                friends.Add(i);
                i++;
            }
        }
        hcount = 0;
        foreach (var friend in friends)
        {
            IPlayer covFriend;
            string command;
            string btnText = "home";
            bool free = true;
            if (friend > 100)
            {
                covFriend = covalence.Players.FindPlayerById(friend.ToString());
                command = $"tpm.comaand /tpr {friend}";
                if (covFriend != null) { btnText = covFriend.Name; }
                else btnText = "DEAD";
                //command = $"chat.say \"/sethome {name}\"";
                free = false;
            }
            else
            {
                btnText = "";
                command = $"";
                //command = $"chat.say \"/home {home.Key}\"";
            }
            string homelayer = $"Friend{hcount}";

            container.Add(new CuiElement
            {
                Parent = _main,
                Name = homelayer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = free ? _imageUI.GetImage("friendfree") : _imageUI.GetImage("homebtn")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"465 {y-height}",OffsetMax = $"595 {y}"
                    }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = command, Close = _layer },
                Text = { Text = btnText, Color = HexToRustFormat("#C7C9FF"), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf" }
            }, homelayer);
            hcount++;
            y -= height + padding;
        }

        #endregion

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

    #region Commands

    [ChatCommand("tpmenu")]
    void TPMenuCmd(BasePlayer player, string command, string[] args)
    {
        TeleportMenuUI(player);
    }

    [ConsoleCommand("tpm.comaand")]
    void PlayerCommandBtn(ConsoleSystem.Arg arg)
    {
        BasePlayer player = arg.Player();
        player.Command("chat.say", arg.FullString.Replace("tpm.comaand ", ""));
    }

    [ConsoleCommand("tpmenu")]
    void ConsoleMenuCmd(ConsoleSystem.Arg arg)
    {
        TeleportMenuUI(arg.Player());
    }

    #endregion
}