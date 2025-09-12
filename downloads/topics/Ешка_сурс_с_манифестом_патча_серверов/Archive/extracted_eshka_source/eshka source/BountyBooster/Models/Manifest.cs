using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BountyBooster.Models
{
	// Token: 0x0200000D RID: 13
	public class Manifest
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x0600002B RID: 43 RVA: 0x00002CE4 File Offset: 0x00000EE4
		// (set) Token: 0x0600002C RID: 44 RVA: 0x00002CEC File Offset: 0x00000EEC
		public global::BountyBooster.Models.Manifest.Servers servers
		{
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			get
			{
				return this.<servers>k__BackingField;
			}
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			set
			{
				this.<servers>k__BackingField = value;
			}
		}

		// Token: 0x0600002D RID: 45 RVA: 0x000020AE File Offset: 0x000002AE
		public Manifest()
		{
		}

		// Token: 0x04000010 RID: 16
		[global::System.Runtime.CompilerServices.CompilerGenerated]
		private global::BountyBooster.Models.Manifest.Servers <servers>k__BackingField;

		// Token: 0x02000022 RID: 34
		public class Servers
		{
			// Token: 0x17000004 RID: 4
			// (get) Token: 0x0600004D RID: 77 RVA: 0x000031B1 File Offset: 0x000013B1
			// (set) Token: 0x0600004E RID: 78 RVA: 0x000031B9 File Offset: 0x000013B9
			public global::System.Collections.Generic.List<global::BountyBooster.Models.Manifest.ServerDesc> official
			{
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				get
				{
					return this.<official>k__BackingField;
				}
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				set
				{
					this.<official>k__BackingField = value;
				}
			}

			// Token: 0x0600004F RID: 79 RVA: 0x000020AE File Offset: 0x000002AE
			public Servers()
			{
			}

			// Token: 0x04000018 RID: 24
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			private global::System.Collections.Generic.List<global::BountyBooster.Models.Manifest.ServerDesc> <official>k__BackingField;
		}

		// Token: 0x02000023 RID: 35
		public class ServerDesc
		{
			// Token: 0x17000005 RID: 5
			// (get) Token: 0x06000050 RID: 80 RVA: 0x000031C2 File Offset: 0x000013C2
			// (set) Token: 0x06000051 RID: 81 RVA: 0x000031CA File Offset: 0x000013CA
			public string name
			{
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				get
				{
					return this.<name>k__BackingField;
				}
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				set
				{
					this.<name>k__BackingField = value;
				}
			}

			// Token: 0x17000006 RID: 6
			// (get) Token: 0x06000052 RID: 82 RVA: 0x000031D3 File Offset: 0x000013D3
			// (set) Token: 0x06000053 RID: 83 RVA: 0x000031DB File Offset: 0x000013DB
			public string address
			{
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				get
				{
					return this.<address>k__BackingField;
				}
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				set
				{
					this.<address>k__BackingField = value;
				}
			}

			// Token: 0x17000007 RID: 7
			// (get) Token: 0x06000054 RID: 84 RVA: 0x000031E4 File Offset: 0x000013E4
			// (set) Token: 0x06000055 RID: 85 RVA: 0x000031EC File Offset: 0x000013EC
			public int port
			{
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				get
				{
					return this.<port>k__BackingField;
				}
				[global::System.Runtime.CompilerServices.CompilerGenerated]
				set
				{
					this.<port>k__BackingField = value;
				}
			}

			// Token: 0x06000056 RID: 86 RVA: 0x000020AE File Offset: 0x000002AE
			public ServerDesc()
			{
			}

			// Token: 0x04000019 RID: 25
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			private string <name>k__BackingField;

			// Token: 0x0400001A RID: 26
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			private string <address>k__BackingField;

			// Token: 0x0400001B RID: 27
			[global::System.Runtime.CompilerServices.CompilerGenerated]
			private int <port>k__BackingField;
		}
	}
}
