using Network;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SuperCard", "fermens", "0.0.2")]
    [Description("SuperCard")]
    public class SuperCard : RustPlugin
    {
        #region КОНФИГ
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Использований")]
            public int uses;

            [JsonProperty("Скин")]
            public ulong skin;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    name = "Супер карточка",
                    uses = 5,
                    skin = 1687308673

                };
            }
        }
        #endregion

        #region -0-
        [ConsoleCommand("card.give")]
        private void cmdfireaxe(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("card.give STEAMID");
                return;
            }

            ulong ID;
            if (!ulong.TryParse(arg.Args[0], out ID))
            {
                arg.ReplyWith($"Это '{arg.Args[0]}' не STEAMID игрока!");
                return;
            }

            BasePlayer player = BasePlayer.FindByID(ID);
            if (player == null)
            {
                arg.ReplyWith($"Игрока с ID:{arg.Args[0]} нет на сервере или указан не верный STEAMID!");
                return;
            }

            Item item = ItemManager.CreateByName("keycard_blue", 1, config.skin);
            if (item == null)
            {
                arg.ReplyWith($"Предмет '{arg.Args[1]}' не существует!");
                return;
            }

            item.name = config.name;

            player.GiveItem(item);
            arg.ReplyWith($"Игрок {player.displayName} получил карту от всех дверей РТ.");
        }
        #endregion

        #region -1-
        private object OnCardSwipe(CardReader cardReader, Keycard keycard, BasePlayer player)
        {
            if(keycard.skinID == config.skin)
            {
                Item item = keycard.GetItem();
                if (item != null && item.conditionNormalized > 0f)
                {
                    float amount = item.maxCondition / (config.uses * 1f);
                    item.condition -= amount;

                    if (item.condition <= 0.001f) item.OnBroken();
                    else item.MarkDirty();

                    timer.Once(0.5f, () =>
                    {
                        cardReader.SetFlag(BaseEntity.Flags.On, true, false, true);
                        cardReader.MarkDirty();
                        Effect.server.Run(cardReader.accessGrantedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up, (Connection)null, false);
                    });
                }
                else
                {
                    timer.Once(0.5f, () => Effect.server.Run(cardReader.accessDeniedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up, (Connection)null, false));
                }
                return false;
            }
            return null;
        }
        #endregion
    }
}
