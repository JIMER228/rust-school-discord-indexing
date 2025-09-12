using System;
using System.IO;

namespace BountyBooster
{
	// Token: 0x02000004 RID: 4
	internal class Logger
	{
		// Token: 0x06000006 RID: 6 RVA: 0x000020B8 File Offset: 0x000002B8
		public static void Output(string text, string tag = "")
		{
			if (tag == "")
			{
				tag = "Log";
			}
			global::System.IO.File.AppendAllText("bounty.log", string.Concat(new string[]
			{
				"[",
				tag,
				"] ",
				text,
				"\r\n"
			}));
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000020AE File Offset: 0x000002AE
		public Logger()
		{
		}
	}
}
