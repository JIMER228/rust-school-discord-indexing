using System;
using UnityEngine;

namespace BountyBooster.Utils
{
	// Token: 0x02000007 RID: 7
	public class Render
	{
		// Token: 0x06000012 RID: 18 RVA: 0x000020AE File Offset: 0x000002AE
		public Render()
		{
		}

		// Token: 0x06000013 RID: 19 RVA: 0x0000258E File Offset: 0x0000078E
		public static global::UnityEngine.GUIStyle None()
		{
			if (global::BountyBooster.Utils.Render.none == null)
			{
				global::BountyBooster.Utils.Render.none = new global::UnityEngine.GUIStyle();
			}
			return global::BountyBooster.Utils.Render.none;
		}

		// Token: 0x06000014 RID: 20 RVA: 0x000025A6 File Offset: 0x000007A6
		public static void Rectangle(float x, float y, float w, float h, global::UnityEngine.Color color, float rounding = 0f)
		{
			global::UnityEngine.GUI.DrawTexture(new global::UnityEngine.Rect(x, y, w, h), global::BountyBooster.Utils.Render.Texture(color), 0, false, 1f, color, 0f, rounding);
		}

		// Token: 0x06000015 RID: 21 RVA: 0x000025CD File Offset: 0x000007CD
		public static void RectangleOutlined(float x, float y, float w, float h, float thickness, global::UnityEngine.Color color, float rounding = 0f)
		{
			global::UnityEngine.GUI.DrawTexture(new global::UnityEngine.Rect(x, y, w, h), global::BountyBooster.Utils.Render.Texture(color), 0, false, 1f, color, thickness, rounding);
		}

		// Token: 0x06000016 RID: 22 RVA: 0x000025F4 File Offset: 0x000007F4
		public static void RectangleWithShadow(float x, float y, float w, float h, global::UnityEngine.Color backgroundColor, global::UnityEngine.Color shadowColor, float shadowOffset)
		{
			global::UnityEngine.GUI.color = shadowColor;
			global::UnityEngine.GUI.DrawTexture(new global::UnityEngine.Rect(x + shadowOffset, y + shadowOffset, w, h), global::BountyBooster.Utils.Render.Texture(shadowColor));
			global::UnityEngine.GUI.color = global::UnityEngine.Color.white;
			global::UnityEngine.GUI.DrawTexture(new global::UnityEngine.Rect(x, y, w, h), global::BountyBooster.Utils.Render.Texture(backgroundColor));
		}

		// Token: 0x06000017 RID: 23 RVA: 0x00002644 File Offset: 0x00000844
		public static void String(float x, float y, string text, global::UnityEngine.Color color, int fontSize = 0x10, global::UnityEngine.FontStyle fontStyle = 0)
		{
			global::UnityEngine.GUI.skin.label.fontSize = fontSize;
			global::UnityEngine.GUI.skin.label.alignment = 0;
			global::UnityEngine.GUI.skin.label.fontStyle = fontStyle;
			global::UnityEngine.GUI.skin.label.normal.textColor = color;
			global::UnityEngine.Vector2 vector = global::UnityEngine.GUI.skin.label.CalcSize(new global::UnityEngine.GUIContent(text));
			global::UnityEngine.GUI.Label(new global::UnityEngine.Rect(x, y, vector.x + 4f, vector.y + 4f), text);
		}

		// Token: 0x06000018 RID: 24 RVA: 0x000026D4 File Offset: 0x000008D4
		public static void String(float x, float y, float w, float h, string text, global::UnityEngine.Color color, int fontSize = 0x10, global::UnityEngine.TextAnchor alignment = 0, global::UnityEngine.FontStyle fontStyle = 0)
		{
			global::UnityEngine.GUI.skin.label.fontSize = fontSize;
			global::UnityEngine.GUI.skin.label.alignment = alignment;
			global::UnityEngine.GUI.skin.label.fontStyle = fontStyle;
			global::UnityEngine.GUI.skin.label.normal.textColor = color;
			global::UnityEngine.GUI.Label(new global::UnityEngine.Rect(x, y, w, h), text);
		}

		// Token: 0x06000019 RID: 25 RVA: 0x0000273C File Offset: 0x0000093C
		public static global::UnityEngine.Vector2 StringSize(string text, int fontSize = 0x10, global::UnityEngine.FontStyle fontStyle = 0)
		{
			global::UnityEngine.GUI.skin.label.fontSize = fontSize;
			global::UnityEngine.GUI.skin.label.alignment = 0;
			global::UnityEngine.GUI.skin.label.fontStyle = fontStyle;
			return global::UnityEngine.GUI.skin.label.CalcSize(new global::UnityEngine.GUIContent(text));
		}

		// Token: 0x0600001A RID: 26 RVA: 0x00002790 File Offset: 0x00000990
		private static global::UnityEngine.Texture2D Texture(global::UnityEngine.Color color)
		{
			if (global::BountyBooster.Utils.Render.texture == null)
			{
				global::BountyBooster.Utils.Render.texture = new global::UnityEngine.Texture2D(1, 1);
				global::BountyBooster.Utils.Render.textureColor = new global::UnityEngine.Color(1f, 0f, 0f, 0f);
			}
			if (global::BountyBooster.Utils.Render.textureColor != color)
			{
				global::BountyBooster.Utils.Render.texture.SetPixel(0, 0, color);
				global::BountyBooster.Utils.Render.texture.Apply();
				global::BountyBooster.Utils.Render.textureColor = color;
			}
			return global::BountyBooster.Utils.Render.texture;
		}

		// Token: 0x04000009 RID: 9
		private static global::UnityEngine.Color textureColor;

		// Token: 0x0400000A RID: 10
		private static global::UnityEngine.Texture2D texture;

		// Token: 0x0400000B RID: 11
		private static global::UnityEngine.GUIStyle none;
	}
}
