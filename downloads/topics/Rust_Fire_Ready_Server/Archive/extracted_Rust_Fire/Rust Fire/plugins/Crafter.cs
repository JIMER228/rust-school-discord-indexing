using System;

namespace Oxide.Plugins
{
    [Info("Crafter", "GmDen", "0.0.1")]
    public class Crafter : RustPlugin
    {
        #region Oxide Hooks
	    
        private void Loaded()
        {
            permission.RegisterPermission("crafter.use", this);
        }

        #endregion

        #region Commands
	    
        [ChatCommand("craftad")]
        private void cmdCraftUser(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "crafter.use") && !player.IsAdmin)
            {
                SendReply(player, "У тебя нету прав использовать эту команду!");
                return;
            }
			
			if (args.Length >= 0)
			{
				var amount = args.Length == 1 ? Convert.ToInt32(args[0]) : 1;
				var mvk = player.inventory.GetAmount(-1021495308); //317398316 metal.refined
				var Gears = player.inventory.GetAmount(479143914); //69511070 metal.fragments
				var metal = player.inventory.GetAmount(69511070); //69511070 metal.fragments

				if (mvk >= amount & Gears >= amount & metal >= amount * 100)
				{
					player.inventory.Take(null, -1021495308, amount);
					player.inventory.Take(null, 479143914, amount);
					player.inventory.Take(null, 69511070, amount * 100);
				}
				else
				{
					player.ChatMessage($"<b><color=#ff0000ff>Недостаточно ресурсов!</color></b>\n" +
					                   $"<b><color=#ce422b> <size=15>Для крафта автодовотчика нужны ресурсы:</size></color></b>\n" +
					                   $"<b>Пружина <color=#ce422b>{amount}</color> шт. (В наличии <color=#ce422b>{mvk}</color> шт.)</b>\n" +
					                   $"<b>Шестиренка <color=#ce422b>{amount}</color> шт. (В наличии <color=#ce422b>{Gears}</color> шт.)</b>\n" +
					                   $"<b>Металл <color=#ce422b>{amount * 100}</color> шт. (В наличии <color=#ce422b>{metal}</color> шт.)</b>\n" +
					                   $"\n" +
					                   $"Как соберешь эти ресурсы, прописывай <color=#ce422b>/craftad колличество</color>");
					return;
				}

				player.inventory.GiveItem(ItemManager.CreateByItemID(1409529282, amount));
				player.ChatMessage($"<b>Автодовотчик успешно скрафчен!</b>");
			}
        }
        #endregion

    }
}
