using System;
using UnityEngine;

namespace StormUI
{
    public class Render
    {
        static Render()
        {
            if (txt3D == null)
            {
                txt3D = new Texture2D(1, 1, TextureFormat.ARGB32, true);
                txt3D.SetPixel(0, 1, UnityEngine.Color.white);
                txt3D.Apply();
            }
        }
        public static void String(Vector2 pos, string text, Color color, bool center = true, int size = 30, FontStyle fontStyle = FontStyle.Bold)
        {
            Render._style.fontSize = size;
            Render._style.richText = true;
            Render._style.normal.textColor = color;
            Render._style.fontStyle = fontStyle;
            //Render._style.font = FileSystem.Load<Font>("assets/content/ui/fonts/robotocondensed-regular.ttf", true);
            GUIContent content = new GUIContent(text);
            new GUIContent(text);
            if (center)
                pos.x -= Render._style.CalcSize(content).x / 2f;

            GUI.Label(new Rect(pos.x, pos.y, 5555f, 5555f), content, Render._style);
        }
        public static void DrawRect(Rect position, UnityEngine.Color color, float radius, GUIContent content = null)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            RectFilled(position, color, radius);
            GUI.backgroundColor = backgroundColor;
        }
        public static GUIStyle _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30
        };
        public float radius = 10f;
        public static void String2(Vector2 position, string text, Color textColor, float width, float height, Color backgroundColor, int fontSize, FontStyle fontStyle)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.wordWrap = true;
            style.normal.background = CreateTexture(backgroundColor);
            style.fontSize = fontSize;
           // style.border = new UnityEngine.RectOffset((int)radius, );
            style.fontStyle = fontStyle;
            GUI.Box(new Rect(position.x, position.y, width, height), text, style);
        }
        private static Texture2D txt3D = null;
        public static void RectFilled(Rect position, UnityEngine.Color color, float radius)
        {
            GUI.color = UnityEngine.Color.white;
            GUI.DrawTexture(position, txt3D, ScaleMode.StretchToFill, true, 1, color, 1111, radius);
        }
        private Texture2D CreateRoundedTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            int roundedRadius = (int)radius * 2;
            int textureSize = roundedRadius * 2;

            // Создание текстуры закругленного прямоугольника
            Texture2D roundedTexture = new Texture2D(textureSize, textureSize);
            for (int x = 0; x < textureSize; x++)
            {
                for (int y = 0; y < textureSize; y++)
                {
                    float distance = Mathf.Sqrt(Mathf.Pow(x - roundedRadius, 2) + Mathf.Pow(y - roundedRadius, 2));
                    if (distance <= roundedRadius)
                    {
                        roundedTexture.SetPixel(x, y, color);
                    }
                    else
                    {
                        roundedTexture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            roundedTexture.Apply();

            return roundedTexture;
        }
        static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
