using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyBooster.Utils
{
	// Token: 0x02000006 RID: 6
	public class Notifier
	{
		// Token: 0x0600000D RID: 13 RVA: 0x0000222A File Offset: 0x0000042A
		static Notifier()
		{
		}

		// Token: 0x0600000E RID: 14 RVA: 0x000020AE File Offset: 0x000002AE
		public Notifier()
		{
		}

		// Token: 0x0600000F RID: 15 RVA: 0x00002238 File Offset: 0x00000438
		public static void Add(string text, float timeout = 3f)
		{
			global::System.Collections.Generic.List<global::BountyBooster.Utils.Notifier.INotify> notifies = global::BountyBooster.Utils.Notifier.Notifies;
			global::BountyBooster.Utils.Notifier.INotify item = new global::BountyBooster.Utils.Notifier.INotify
			{
				text = text,
				subtitle = string.Empty,
				timeout = timeout,
				createdIn = global::UnityEngine.Time.realtimeSinceStartup,
				position = new global::UnityEngine.Vector2(-350f, 0f)
			};
			notifies.Add(item);
		}

		// Token: 0x06000010 RID: 16 RVA: 0x00002298 File Offset: 0x00000498
		public static void Add(string text, string subtitle, float timeout = 3f)
		{
			global::System.Collections.Generic.List<global::BountyBooster.Utils.Notifier.INotify> notifies = global::BountyBooster.Utils.Notifier.Notifies;
			global::BountyBooster.Utils.Notifier.INotify item = new global::BountyBooster.Utils.Notifier.INotify
			{
				text = text,
				subtitle = subtitle,
				timeout = timeout,
				createdIn = global::UnityEngine.Time.realtimeSinceStartup,
				position = new global::UnityEngine.Vector2(-350f, 0f)
			};
			notifies.Add(item);
		}

		// Token: 0x06000011 RID: 17 RVA: 0x000022F4 File Offset: 0x000004F4
		public static void Render()
		{
			if (global::BountyBooster.Utils.Notifier.Notifies.Count != 0)
			{
				float num = 0f;
				int i = 0;
				while (i < global::BountyBooster.Utils.Notifier.Notifies.Count)
				{
					global::BountyBooster.Utils.Notifier.INotify notify = global::BountyBooster.Utils.Notifier.Notifies[i];
					bool flag = notify.subtitle.Length > 0;
					float num2 = global::UnityEngine.Time.realtimeSinceStartup - notify.createdIn;
					if (num2 < notify.timeout)
					{
						notify.position = global::UnityEngine.Vector2.Lerp(notify.position, new global::UnityEngine.Vector2(20f, 0f), global::UnityEngine.Time.deltaTime * notify.timeout);
						goto IL_DD;
					}
					if (num2 <= notify.timeout * 2f)
					{
						goto IL_DD;
					}
					notify.position = global::UnityEngine.Vector2.Lerp(notify.position, new global::UnityEngine.Vector2(-350f, 0f), global::UnityEngine.Time.deltaTime * notify.timeout);
					if (notify.position.x >= -300f)
					{
						goto IL_DD;
					}
					global::BountyBooster.Utils.Notifier.Notifies.RemoveAt(i);
					IL_279:
					i++;
					continue;
					IL_DD:
					global::BountyBooster.Utils.Render.Rectangle(notify.position.x, 20f + num, 300f, (float)(flag ? 0x50 : 0x3C), new global::UnityEngine.Color32(0x18, 0x18, 0x18, byte.MaxValue), 8f);
					global::BountyBooster.Utils.Render.String(notify.position.x + 20f, 20f + num, 260f, (float)(flag ? 0x30 : 0x38), notify.text, global::UnityEngine.Color.white, 0x10, 3, 0);
					if (flag)
					{
						global::BountyBooster.Utils.Render.String(notify.position.x + 20f, 56f + num, 260f, 24f, notify.subtitle, global::UnityEngine.Color.gray, 0xC, 3, 0);
					}
					if (num2 > notify.timeout)
					{
						float num3 = num2 - notify.timeout;
						if (num3 > notify.timeout)
						{
							num3 = notify.timeout;
						}
						global::BountyBooster.Utils.Render.Rectangle(notify.position.x + 4f, (float)(0x14 + (((!flag) ? 0x3C : 0x50) - 0xA)) + num, 292f, 6f, new global::UnityEngine.Color32(0x41, 0x41, 0x41, byte.MaxValue), 8f);
						global::BountyBooster.Utils.Render.Rectangle(notify.position.x + 4f, (float)(0x14 + ((flag ? 0x50 : 0x3C) - 0xA)) + num, 292f / notify.timeout * num3, 6f, new global::UnityEngine.Color32(0x78, 0x78, byte.MaxValue, byte.MaxValue), 8f);
					}
					global::BountyBooster.Utils.Notifier.Notifies[i] = notify;
					num += (float)((!flag) ? 0x50 : 0x64);
					goto IL_279;
				}
			}
		}

		// Token: 0x04000008 RID: 8
		private static global::System.Collections.Generic.List<global::BountyBooster.Utils.Notifier.INotify> Notifies = new global::System.Collections.Generic.List<global::BountyBooster.Utils.Notifier.INotify>();

		// Token: 0x0200000F RID: 15
		private struct INotify
		{
			// Token: 0x04000013 RID: 19
			public string text;

			// Token: 0x04000014 RID: 20
			public string subtitle;

			// Token: 0x04000015 RID: 21
			public float createdIn;

			// Token: 0x04000016 RID: 22
			public float timeout;

			// Token: 0x04000017 RID: 23
			public global::UnityEngine.Vector2 position;
		}
	}
}
