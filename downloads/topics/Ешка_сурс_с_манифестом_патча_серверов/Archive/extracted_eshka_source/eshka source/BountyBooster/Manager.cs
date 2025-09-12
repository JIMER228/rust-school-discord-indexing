using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using BountyBooster.Utils;
using Rust;
using UnityEngine;

namespace BountyBooster
{
	// Token: 0x02000005 RID: 5
	public class Manager : global::UnityEngine.MonoBehaviour
	{
		// Token: 0x06000008 RID: 8 RVA: 0x0000210E File Offset: 0x0000030E
		public Manager()
		{
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00002116 File Offset: 0x00000316
		private global::System.Collections.IEnumerator GC_Cleaning()
		{
			return new global::BountyBooster.Manager.<GC_Cleaning>d__2(0);
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002120 File Offset: 0x00000320
		private void OnGUI()
		{
			global::BountyBooster.Utils.Notifier.Render();
			if (this.isOpen)
			{
				global::BountyBooster.Utils.UI.BeginWindow(200f, 100f, 400f, 400f);
				global::BountyBooster.Utils.UI.Title("BOUNTY RUST", new global::UnityEngine.Color32(0x69, 0x69, 0x69, byte.MaxValue));
				global::BountyBooster.Utils.UI.BeginCategory("Оптимизация", "Раздел отвечающий за основной буст вашего FPS\nблагодаря отключениям ненужных функций", 210f);
				global::BountyBooster.Utils.UI.Toggle("Тени", "Отключает тени в игре", ref global::BountyBooster.Globals.NoShadows);
				global::BountyBooster.Utils.UI.Toggle("Трава", "Полностью убирает траву", ref global::BountyBooster.Globals.NoGrass);
				global::BountyBooster.Utils.UI.Toggle("Очистка оперативной памяти", "Очищает оп.память с интервалом 30 мин", ref global::BountyBooster.Globals.CleanGC);
				global::BountyBooster.Utils.UI.EndCategory();
				global::BountyBooster.Utils.UI.EndWindow();
			}
		}

		// Token: 0x0600000B RID: 11 RVA: 0x000021CA File Offset: 0x000003CA
		private void Start()
		{
			global::BountyBooster.Utils.Notifier.Add("Бустер загружен!", "Открыть меню F2", 3f);
			base.StartCoroutine(global::BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(this.GC_Cleaning()));
		}

		// Token: 0x0600000C RID: 12 RVA: 0x000021F2 File Offset: 0x000003F2
		private void Update()
		{
			if (global::UnityEngine.Input.GetKeyUp(0x11B))
			{
				this.isOpen = !this.isOpen;
			}
			if (global::UnityEngine.Input.GetKeyUp(0x115))
			{
				this.isOpen = !this.isOpen;
			}
		}

		// Token: 0x04000007 RID: 7
		private bool isOpen;

		// Token: 0x0200000E RID: 14
		[global::System.Runtime.CompilerServices.CompilerGenerated]
		private sealed class <GC_Cleaning>d__2 : global::System.Collections.Generic.IEnumerator<object>, global::System.IDisposable, global::System.Collections.IEnumerator
		{
			// Token: 0x0600002E RID: 46 RVA: 0x00002CF5 File Offset: 0x00000EF5
			[global::System.Diagnostics.DebuggerHidden]
			public <GC_Cleaning>d__2(int <>1__state)
			{
				this.<>1__state = <>1__state;
			}

			// Token: 0x0600002F RID: 47 RVA: 0x00002D04 File Offset: 0x00000F04
			[global::System.Diagnostics.DebuggerHidden]
			void global::System.IDisposable.Dispose()
			{
				this.<>1__state = -2;
			}

			// Token: 0x06000030 RID: 48 RVA: 0x00002D10 File Offset: 0x00000F10
			bool global::System.Collections.IEnumerator.MoveNext()
			{
				int num = this.<>1__state;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					this.<>1__state = -1;
				}
				else
				{
					this.<>1__state = -1;
				}
				if (global::BountyBooster.Globals.CleanGC)
				{
					global::Rust.GC.Collect();
				}
				this.<>2__current = new global::UnityEngine.WaitForSeconds(1800f);
				this.<>1__state = 1;
				return true;
			}

			// Token: 0x17000002 RID: 2
			// (get) Token: 0x06000031 RID: 49 RVA: 0x00002D61 File Offset: 0x00000F61
			object global::System.Collections.Generic.IEnumerator<object>.Current
			{
				[global::System.Diagnostics.DebuggerHidden]
				get
				{
					return this.<>2__current;
				}
			}

			// Token: 0x06000032 RID: 50 RVA: 0x00002D69 File Offset: 0x00000F69
			[global::System.Diagnostics.DebuggerHidden]
			void global::System.Collections.IEnumerator.Reset()
			{
				throw new global::System.NotSupportedException();
			}

			// Token: 0x17000003 RID: 3
			// (get) Token: 0x06000033 RID: 51 RVA: 0x00002D61 File Offset: 0x00000F61
			object global::System.Collections.IEnumerator.Current
			{
				[global::System.Diagnostics.DebuggerHidden]
				get
				{
					return this.<>2__current;
				}
			}

			// Token: 0x04000011 RID: 17
			private int <>1__state;

			// Token: 0x04000012 RID: 18
			private object <>2__current;
		}
	}
}
