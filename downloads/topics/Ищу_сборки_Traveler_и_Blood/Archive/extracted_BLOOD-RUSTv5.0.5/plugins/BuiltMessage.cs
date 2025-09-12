using System.Collections.Generic;
using UnityEngine;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("Built Message", "discord.gg/9vyTXsJyKR", "1.0.0")]
    [Description("Send messsages to players when they build.")]
    public class BuiltMessage : RustPlugin
    {
        private List<string> messagesSent = new List<string>();

        private void Init()
        {
            if (!messages.ContainsKey("OnStructureUpgrade"))
            {
                Unsubscribe(nameof(OnStructureUpgrade));
            }
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (CanMessage(player))
            {
                Print(player, messages["OnStructureUpgrade"]);
            }
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var entity = go.ToBaseEntity();

            if (entity == null || !messages.ContainsKey(entity.ShortPrefabName))
            {
                return;
            }

            var player = planner.GetOwnerPlayer();

            if (!CanMessage(player))
            {
                return;
            }

            Print(player, messages[entity.ShortPrefabName]);
        }

        public bool CanMessage(BasePlayer player)
        {
            if (player == null || messagesSent.Contains(player.UserIDString))
            {
                return false;
            }

            string uid = player.UserIDString;

            messagesSent.Add(uid);
            timer.Once(10f, () => messagesSent.Remove(uid));

            return true;
        }
        
        private readonly Dictionary<string, string> messages = new Dictionary<string, string>/////////////////////Add additional messages here
        {
            ["autoturret_deployed"] = "<color=green>Elektriğe ihtiyacınız yok fakat isterseniz kablolarla da kontrol edebilirsiniz.</color></i>",
            ["furnace.large"] = "Büyük Fırın eritme hızı 10x",
            ["furnace"] = "Fırınlarda erime hızı 10x",
            ["OnStructureUpgrade"] = "Tüm binayı aynı anda yükseltmek ister misin?\nKomut: /yükselt",
			["wall"] = "BGrade ile belirli seviyelerde inşa yapabilirsin.\nKomut: /bgrade",
			["foundation"] = "BGrade ile belirli seviyelerde inşa yapabilirsin.\nKomut: /bgrade",

        };

        private void Print(BasePlayer player, string message) => Player.Message(player, message, $"<color=#ce422b>[RO Bilgi]</color>", 0);
    }
}