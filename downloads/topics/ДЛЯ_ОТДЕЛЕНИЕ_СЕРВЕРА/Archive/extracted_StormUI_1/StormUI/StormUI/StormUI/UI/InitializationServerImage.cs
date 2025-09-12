using Facepunch.Utility;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleSystem;
using static System.Net.Mime.MediaTypeNames;

namespace StormUI.UI
{
    public class InitializationServerImage : MonoBehaviour
    {
        private static List<StringInfo> _string = new List<StringInfo>();
        private float _showDuration = 2.5f; // Длительность показа текста
        private float _moveDuration = 0.5f; // Длительность перемещения текста
        private float _moveDistance = 260; // Расстояние, на которое текст перемещается влево
        private GUIStyle None;
        private void Start()
        {
            FixText();
        }
        private void OnGUI()
        {
            if (LoadingScreen.isOpen)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Factory.GetAsset<Texture2D>("assets/images/logotype.png"));
                Render.String(new Vector2((Screen.width / 2), (Screen.height / 2 - (_fontSize * 2))), _text, new Color32(250, 250, 250, 255), true, _fontSize, FontStyle.Normal);

                if (LoadingScreen.Text != null)
                {
                        _text = LoadingScreen.Text;

                    GUI.skin.label.fontSize = 20;

                    if (None == null) None = new GUIStyle();

                    GUI.skin.label.fontSize = 18;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                   // None.normal.background = MakeBackgroundTexture(215, 48, Color.green);
                    GUI.skin.label.alignment = TextAnchor.UpperLeft;

                    if (GUI.Button(new Rect(Screen.width / 2 - 140, Screen.height / 2 + 22, 300, 55), "", None))
                        ConsoleSystem.Run(ConsoleSystem.Option.Client, "client.disconnect");
                }
            }
            for (var i = 0; i < _string.Count; i++)
            {
                if (_string[i].visible)
                {
                    if (_string[i].isShowing)
                    {
                        Render.String2(new Vector2(10, 10 + i * 100), "Подключение возможно только\nна STORM RUST", new Color32(250, 250, 250, 255), 250, 85, new Color32(30, 30, 30, 255), 14, FontStyle.Bold);

                        if (Time.time > _string[i].timer + _showDuration)
                        {
                            _string[i].isShowing = false;
                            _string[i].timer = Time.time;
                        }
                    }
                    else
                    {
                        var lerpPosition = Mathf.Lerp(10, -_moveDistance, (Time.time - _string[i].timer) / _moveDuration);
                        var position = new Vector2(lerpPosition, 10 + i * 100);

                        Render.String2(position, "Подключение возможно только\nна STORM RUST", new Color32(250, 250, 250, 255), 250, 85, new Color32(30, 30, 30, 255), 14, FontStyle.Bold);
                    }
                }

                if (Time.time > _string[i].timer + _showDuration + _moveDuration)
                    HideLabel(i);
            }
        }
        private Texture2D MakeBackgroundTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D backgroundTexture = new Texture2D(width, height);

            backgroundTexture.SetPixels(pixels);
            backgroundTexture.Apply();

            return backgroundTexture;
        }
        private void FixText()
        {
            if (Screen.width > 600)
                _fontSize = 9;
            if (Screen.width > 720)
                _fontSize = 11;
            if (Screen.width > 1000)
                _fontSize = 14;
            if (Screen.width > 1200)
                _fontSize = 15;
            if (Screen.width > 1777)
                _fontSize = 20;

            if (Screen.height < 900)
                _height = 11f;
            if (Screen.height < 1081)
                _height = 10.5f;

            if (Screen.width > 1920 && Screen.height > 1081)
            {
                _height = 10.5f;
                _fontSize = 30;
            }
        }
        private Texture2D _back;
        private static int _fontSize = 14;
        private static float _height = 20f;
        public static string _text = "";
        private static void ShowLabel()
        {
            var label = new StringInfo();
            label.visible = true;
            label.timer = Time.time;
            label.isShowing = true;
            _string.Add(label);
        }

        private static void HideLabel(int index)
        {
            _string.RemoveAt(index);
        }

        private class StringInfo
        {
            public bool visible;
            public bool isShowing;
            public float timer;
        }




        [HarmonyPatch(typeof(ConVar.Client), "connect")] // patch to ban on access other servers
        #region ConvarClientConnect
        class ConVar_Client_connect_Patch
        {
            static bool Prefix(ref string __result, string address = "127.0.0.1:28015", string protocol = "")
            {
                if (!address.Contains("37.230.228.37"))
                {
                    ShowLabel();
                    __result = "Подключение возможно только на STORM RUST";
                    return false;
                }

                return true;
            }
        }
        #endregion
    }
}
