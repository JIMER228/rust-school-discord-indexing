using Oxide.Core;
using System;
using System.Collections.Generic;

/*

	TO DO List:

	* Smelt items as soon as they hit inventory (permission)
	* Smelt on command (permission, cooldown, sound)
	* Remade List in Dictionary with cooldowns, etc

*/

namespace Oxide.Plugins
{
    [Info("Magic Smelt", "Lomarine", "1.0.0")]
	[Description("Smelts resources on gather.")]

    class MagicSmelt : RustPlugin
    {
		List<ulong> ActiveUsers;

		private string PermTools = "MagicSmelt.tools";
		private string GatherEffect;
		private int CharcoalMultiplier;

		#region Initialization

		void Init()
        {
            permission.RegisterPermission(PermTools, this);

			LoadDefaultMessages();
			LoadData();
			LoadDefaultConfig();
        }

		#endregion

		#region Commands

		[ChatCommand("tools")]
        private void cmdtools(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermTools))
            {
                PrintToChat(player, lang.GetMessage("MissingPerm", this, player.UserIDString));
                return;
            }
            if (ActiveUsers.Contains(player.userID))
            {
                PrintToChat(player, lang.GetMessage("Tools deactivated", this, player.UserIDString));
                ActiveUsers.Remove(player.userID);
                return;
            }
            PrintToChat(player, lang.GetMessage("Tools activated", this, player.UserIDString));
            ActiveUsers.Add(player.userID);
        }

		#endregion

		#region Helpers

		void OnServerSave()
		{
			Interface.Oxide.DataFileSystem.WriteObject("MagicSmelt", ActiveUsers);
		}

		void LoadData()
		{
			try
            {
                ActiveUsers = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("MagicSmelt");
            }
            catch
            {
                ActiveUsers = new List<ulong>();
                PrintWarning("Failed to load datafile. Creating new datafile...");
            }
		}

		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MissingPerm", "<color=with>You do not have permission to use that command.</color>"},
                {"Tools activated", "<color=with>Магический инструмент <color=lime>Активирован</color></color>"},
                {"Tools deactivated", "<color=with>Магический инструмент <color=red>Деактивирован</color></color>"}
            }, this);
		}

		protected override void LoadDefaultConfig()
        {
            Config["Gather Effect"] = GatherEffect = GetConfig("Gather Effect", "assets/prefabs/weapons/wooden spear/effects/strike_wood.prefab");
			Config["Charcoal Multiplier"] = CharcoalMultiplier = GetConfig("Charcoal Multiplier", 1);
            SaveConfig();
        }

		T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

		#endregion

		#region Smelt

		// On Gather
		void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermTools) || !ActiveUsers.Contains(player.userID)) return;
            switch (item.info.shortname)
            {
				// Resources
                case "sulfur.ore":
                    SmeltItems(-891243783, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
                case "hq.metal.ore":
                    SmeltItems(374890416, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
                case "metal.ore":
                    SmeltItems(688032252, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "wood":
                    SmeltItems(1436001773, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				// Meat
				case "bearmeat":
                    SmeltItems(-2043730634, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "deermeat.raw":
                    SmeltItems(-202239044, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "humanmeat.raw":
                    SmeltItems(-991829475, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "meat.boar":
                    SmeltItems(991728250, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "wolfmeat.raw":
                    SmeltItems(-1691991080, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "chicken.raw":
                    SmeltItems(1734319168, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
            }

        }

		// On Collect
		void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermTools) || !ActiveUsers.Contains(player.userID)) return;
            switch (item.info.shortname)
            {
                case "sulfur.ore":
                    SmeltItems(-891243783, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
                case "metal.ore":
                    SmeltItems(688032252, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
				case "wood":
                    SmeltItems(1436001773, ref item);
					Effect.server.Run(GatherEffect, player.transform.position);
                    break;
            }
		}

		// Smelter
		private void SmeltItems(int ItemId, ref Item item)
        {
			if(item.info.shortname == "wood")
				item.amount = item.amount * CharcoalMultiplier;
            Item _item = ItemManager.CreateByItemID(ItemId, item.amount);
            item.info = _item.info;
            item.contents = _item.contents;
        }

		#endregion
    }
}