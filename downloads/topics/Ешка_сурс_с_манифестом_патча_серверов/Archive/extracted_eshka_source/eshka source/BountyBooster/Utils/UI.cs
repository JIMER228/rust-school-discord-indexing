using System;
using Il2CppSystem;
using UnityEngine;

namespace BountyBooster.Utils
{
	// Token: 0x02000008 RID: 8
	public class UI
	{
		// Token: 0x0600001B RID: 27 RVA: 0x00002803 File Offset: 0x00000A03
		static UI()
		{
		}

		// Token: 0x0600001C RID: 28 RVA: 0x000020AE File Offset: 0x000002AE
		public UI()
		{
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00002830 File Offset: 0x00000A30
		public static void BeginCategory(string name, string description, float h)
		{
			global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY = 10f;
			global::BountyBooster.Utils.Render.String(30f, global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY, name, global::UnityEngine.Color.white, 0x10, 1);
			global::UnityEngine.Vector2 vector = global::BountyBooster.Utils.Render.StringSize(name, 0x10, 1);
			global::BountyBooster.Utils.Render.String(30f, global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY + vector.y, 300f, 32f, description, global::UnityEngine.Color.gray, 0xC, 0, 0);
			global::BountyBooster.Utils.Render.Rectangle(20f, global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY + vector.y + 48f, 360f, h, new global::UnityEngine.Color32(0x35, 0x1E, 0x52, byte.MaxValue), 16f);
			global::UnityEngine.GUI.BeginGroup(new global::UnityEngine.Rect(20f, global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY + vector.y + 48f, 360f, h));
			global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY += global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY + vector.y + 48f + h + 12f;
		}

		// Token: 0x0600001E RID: 30 RVA: 0x0000291C File Offset: 0x00000B1C
		public static void BeginWindow(float x, float y, float w, float h)
		{
			global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY = 0f;
			global::BountyBooster.Utils.UI.CurrentWindowPosition = new global::UnityEngine.Vector2(x, y);
			global::BountyBooster.Utils.UI.CurrentWindowSize = new global::UnityEngine.Vector2(w, h);
			global::BountyBooster.Utils.Render.RectangleWithShadow(x, y, w, h, new global::UnityEngine.Color32(0x1E, 0x1E, 0x28, byte.MaxValue), new global::UnityEngine.Color32(0, 0, 0, 0x96), 8f);
			global::BountyBooster.Utils.Render.Rectangle(x, y, w, h, new global::UnityEngine.Color32(0x40, 0x31, 0x54, byte.MaxValue), 16f);
			global::UnityEngine.GUI.BeginGroup(new global::UnityEngine.Rect(x, y, w, h), global::Il2CppSystem.String.Empty, global::BountyBooster.Utils.Render.None());
		}

		// Token: 0x0600001F RID: 31 RVA: 0x000029BB File Offset: 0x00000BBB
		public static void EndCategory()
		{
			global::UnityEngine.GUI.EndGroup();
		}

		// Token: 0x06000020 RID: 32 RVA: 0x000029BB File Offset: 0x00000BBB
		public static void EndWindow()
		{
			global::UnityEngine.GUI.EndGroup();
		}

		// Token: 0x06000021 RID: 33 RVA: 0x000029C4 File Offset: 0x00000BC4
		private static bool isHover(float x, float y, float w, float h)
		{
			float x2 = global::UnityEngine.Input.mousePosition.x;
			float num = (float)global::UnityEngine.Screen.height - global::UnityEngine.Input.mousePosition.x;
			return (x2 >= x && x2 <= x + w) & (num >= y && num <= y + h);
		}

		// Token: 0x06000022 RID: 34 RVA: 0x00002A10 File Offset: 0x00000C10
		public static void Title(string text, global::UnityEngine.Color32 color)
		{
			global::BountyBooster.Utils.Render.String(0f, 0f, global::BountyBooster.Utils.UI.CurrentWindowSize.x, 64f, text, new global::UnityEngine.Color32(0x69, 0x69, 0x69, byte.MaxValue), 0x14, 4, 1);
			global::BountyBooster.Utils.UI.CurrentWindowItemOffsetY += 88f;
		}

		// Token: 0x06000023 RID: 35 RVA: 0x00002A68 File Offset: 0x00000C68
		public static void Toggle(string name, string description, ref bool state)
		{
			global::BountyBooster.Utils.Render.Rectangle(10f, global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY, 340f, 60f, new global::UnityEngine.Color32(0x4B, 0x21, 0x80, byte.MaxValue), 12f);
			global::BountyBooster.Utils.Render.String(30f, global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY + 12f, name, global::UnityEngine.Color.white, 0xE, 1);
			global::BountyBooster.Utils.Render.String(30f, global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY + 30f, description, global::UnityEngine.Color.gray, 0xC, 0);
			global::BountyBooster.Utils.Render.Rectangle(310f, global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY + 24f, 24f, 12f, state ? new global::UnityEngine.Color32(0x1E, 0xA9, 0x44, 0x64) : new global::UnityEngine.Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0x32), 12f);
			global::BountyBooster.Utils.Render.Rectangle((float)(0x136 + (state ? 0xD : 1)), global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY + 25f, 10f, 10f, (!state) ? new global::UnityEngine.Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0x64) : new global::UnityEngine.Color32(0x2F, 0xFD, 0x6A, byte.MaxValue), 10f);
			if (global::UnityEngine.GUI.Button(new global::UnityEngine.Rect(10f, global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY, 340f, 60f), global::Il2CppSystem.String.Empty, global::BountyBooster.Utils.Render.None()))
			{
				state = !state;
			}
			global::BountyBooster.Utils.UI.CurrentCategoryItemOffsetY += 65f;
		}

		// Token: 0x0400000C RID: 12
		private static global::UnityEngine.Vector2 CurrentWindowPosition = global::UnityEngine.Vector2.zero;

		// Token: 0x0400000D RID: 13
		private static global::UnityEngine.Vector2 CurrentWindowSize = global::UnityEngine.Vector2.zero;

		// Token: 0x0400000E RID: 14
		private static float CurrentWindowItemOffsetY = 0f;

		// Token: 0x0400000F RID: 15
		private static float CurrentCategoryItemOffsetY = 0f;
	}
}
