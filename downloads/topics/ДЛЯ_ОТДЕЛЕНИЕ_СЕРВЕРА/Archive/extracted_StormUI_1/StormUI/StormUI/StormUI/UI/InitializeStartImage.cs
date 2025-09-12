using Facepunch.Utility;
using System.IO;
using UnityEngine;

namespace StormUI.UI
{
    public class InitializeStartImage : MonoBehaviour
    {
        private void Start()
        {
            FixText();
        }

        private void OnGUI()
        {
            if (_background)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Factory.GetAsset<Texture2D>("assets/images/loading.png"));
                Render.String(new Vector2((Screen.width / 2), (Screen.height - (_fontSize * 2))), _text, new Color32(250, 250, 250, 255), true, _fontSize, FontStyle.Normal);
            }
        }

        private void FixText()
        {
            if (Screen.width > 600)
                InitializeStartImage._fontSize = 9;
            if (Screen.width > 720)
                InitializeStartImage._fontSize = 11;
            if (Screen.width > 1000)
                InitializeStartImage._fontSize = 14;
            if (Screen.width > 1200)
                InitializeStartImage._fontSize = 15;
            if (Screen.width > 1777)
                InitializeStartImage._fontSize = 20;

            if (Screen.height < 900)
                InitializeStartImage._height = 11f;
            if (Screen.height < 1081)
                InitializeStartImage._height = 10.5f;

            if (Screen.width > 1920 && Screen.height > 1081)
            {
                InitializeStartImage._height = 10.5f;
                InitializeStartImage._fontSize = 30;
            }
        }
        private static int _fontSize = 14;
        private static float _height = 20f;
        public static string _text = "";
        public static bool _background;
    }
}
