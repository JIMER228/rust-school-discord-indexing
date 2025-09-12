/*
 ########### README ####################################################
                                                                             
  !!! DON'T EDIT THIS FILE !!!
                                                                     
 ########### CHANGES ###################################################

 1.0.0
    - Plugin release

 #######################################################################
*/

using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Libraries;
using VLB;

namespace Oxide.Plugins
{
    [Info("Player Effects", "paulsimik", "1.0.0")]
    [Description("Adds Effect to the Player")]
    class PlayerEffects : RustPlugin
    {
        private bool DEBUG = false;

        #region [Fields]

        private const string permUse = "playereffects.use";
        private const string MISSING_EFFECT = "assets/bundled/prefabs/fx/missing.prefab";
        private const string BARRICADE_EFFECT = "assets/bundled/prefabs/fx/door/barricade_spawn.prefab";
        private const string FIRE2_EFFECT = "assets/bundled/prefabs/fx/fire/fire_v2.prefab";
        private const string STASH_EFFECT = "assets/bundled/prefabs/fx/dig_effect.prefab";
        private const string STONE_EFFECT = "assets/bundled/prefabs/fx/bucket_drop_debris.prefab";
        private const string GLASS_EFFECT = "assets/bundled/prefabs/fx/impacts/blunt/glass/glass1.prefab";
        private const string WATER_EFFECT = "assets/bundled/prefabs/fx/impacts/blunt/water/water.prefab";
        private const string WATERS_EFFECT = "assets/bundled/prefabs/fx/impacts/footstep/barefoot/water/boot_footstep_water.prefab";
        private const string STEP_EFFECT = "assets/bundled/prefabs/fx/impacts/jump-land/barefoot/snow/jump-land-snow.prefab";
        private const string DOG_EFFECT = "assets/bundled/prefabs/fx/player/howl.prefab";
        private const string SHO_EFFECT = "assets/bundled/prefabs/fx/water/midair_splash.prefab";
        private const string SHA_EFFECT = "assets/bundled/prefabs/fx/water/playerjumpinwater.prefab";
        private const string BLUE_EFFECT = "assets/content/effects/crossbreed/pfx crossbreed blue.prefab";
        private const string YE_EFFECT = "assets/content/effects/crossbreed/pfx crossbreed yellow.prefab";
        private const string BOOM_EFFECT = "assets/prefabs/weapons/satchelcharge/effects/satchel-charge-explosion.prefab";
        private const string BMP_EFFECT = "assets/prefabs/deployable/chinooklockedcrate/effects/landing.prefab";
        private const string CM_EFFECT = "assets/prefabs/deployable/reactive target/effects/bullseye.prefab";
        private const string CMS_EFFECT = "assets/prefabs/deployable/reactive target/effects/tire_smokepuff.prefab";
        private const string AP_EFFECT = "assets/prefabs/misc/casino/slotmachine/effects/payout_jackpot.prefab";
        private const string FR_EFFECT = "assets/prefabs/misc/chinesenewyear/throwablefirecrackers/firecracker_crack.prefab";
        private const string EG_EFFECT = "assets/prefabs/misc/easter/easter basket/effects/eggexplosion.prefab";
        private const string EGA_EFFECT = "assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab";
        private const string EGW_EFFECT = "assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab";
        private const string ES_EFFECT = "assets/prefabs/misc/halloween/candies/candypickup.prefab";
        private const string EA_EFFECT = "assets/prefabs/misc/halloween/skull_door_knocker/effects/door_knock_fx.prefab";
        private const string EAS_EFFECT = "assets/prefabs/misc/halloween/skull_door_knocker/effects/skull_door_knock_fx.prefab";
        private const string FS_EFFECT = "assets/prefabs/misc/orebonus/effects/bonus_hit.prefab";
		private const string FSA_EFFECT = "assets/prefabs/misc/orebonus/effects/hotspot_death.prefab";
		private const string FSQ_EFFECT = "assets/prefabs/misc/summer_dlc/boogie_board/effects/pfx_boogieboard_base.prefab";
		private const string FSE_EFFECT = "assets/prefabs/misc/xmas/advent_calendar/effects/open_advent.prefab";
		private const string SIF_EFFECT = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";

        #endregion

        #region [Oxide Hooks]

        private void Init() => permission.RegisterPermission(permUse, this);

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayerComponent(player);
        }

        private void OnPlayerDisconnected(BasePlayer player) => DestroyPlayerComponent(player);

        #endregion

        #region [Hooks]   

        private PlayerEffect GetPlayer(BasePlayer player)
        {
            if (player == null)
                return null;

            var playerEffect = player.GetComponent<PlayerEffect>();
            if (playerEffect == null)
                return null;

            return playerEffect;
        }

        private void DestroyPlayerComponent(BasePlayer player)
        {
            PlayerEffect playerEffect = GetPlayer(player);
            if (playerEffect == null)
                return;

            UnityEngine.Object.Destroy(playerEffect);
        }

        private void AddEffect(PlayerEffect playerEffect, string typeEffect)
        {
            switch (typeEffect)
            {
                case "0":
                case "disable":
                case "disabled":
                    {
                        playerEffect.DestroyTimer();
                        SendReply(playerEffect.player, GetLang("Disable", playerEffect.player.UserIDString));                 
                        return;
                    }
                case "1":
                case "particles":
                    {
                        playerEffect.effect = MISSING_EFFECT;
                        playerEffect.effectPosition = Vector3.zero;
                        playerEffect.time = 1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect1", playerEffect.player.UserIDString));
                        return;
                    }
                case "2":
                case "smoke":
                    {
                        playerEffect.effect = BARRICADE_EFFECT;
                        playerEffect.effectPosition = Vector3.zero;
                        playerEffect.time = 0.2f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect2", playerEffect.player.UserIDString));
                        return;
                    }
                case "3":
                case "fire":
                    {
                        playerEffect.effect = FIRE2_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 8f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect3", playerEffect.player.UserIDString));
                        return;
                    }
                case "4":
                case "stash":
                    {
                        playerEffect.effect = STASH_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.7f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect4", playerEffect.player.UserIDString));
                        return;
                    }
                case "5":
                case "stone":
                    {
                        playerEffect.effect = STONE_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.8f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect5", playerEffect.player.UserIDString));
                        return;
                    }
                case "6":
                case "glass":
                    {
                        playerEffect.effect = GLASS_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect6", playerEffect.player.UserIDString));
                        return;
                    }
                case "7":
                case "water":
                    {
                        playerEffect.effect = WATER_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.4f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect7", playerEffect.player.UserIDString));
                        return;
                    }
                case "8":
                case "waters":
                    {
                        playerEffect.effect = WATERS_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect8", playerEffect.player.UserIDString));
                        return;
                    }
                case "9":
                case "step":
                    {
                        playerEffect.effect = STEP_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.3f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect9", playerEffect.player.UserIDString));
                        return;
                    }
                case "10":
                case "dog":
                    {
                        playerEffect.effect = DOG_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 2.0f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect10", playerEffect.player.UserIDString));
                        return;
                    }
                case "11":
                case "sho":
                    {
                        playerEffect.effect = SHO_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.3f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect11", playerEffect.player.UserIDString));
                        return;
                    }
                case "12":
                case "sha":
                    {
                        playerEffect.effect = SHA_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.3f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect12", playerEffect.player.UserIDString));
                        return;
                    }
                case "13":
                case "blue":
                    {
                        playerEffect.effect = BLUE_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect13", playerEffect.player.UserIDString));
                        return;
                    }
                case "14":
                case "ye":
                    {
                        playerEffect.effect = YE_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect14", playerEffect.player.UserIDString));
                        return;
                    }
                case "15":
                case "boom":
                    {
                        playerEffect.effect = BOOM_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect15", playerEffect.player.UserIDString));
                        return;
                    }
                case "16":
                case "bmp":
                    {
                        playerEffect.effect = BMP_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 1.0f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect16", playerEffect.player.UserIDString));
                        return;
                    }
                case "17":
                case "cm":
                    {
                        playerEffect.effect = CM_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect17", playerEffect.player.UserIDString));
                        return;
                    }
                case "18":
                case "cms":
                    {
                        playerEffect.effect = CMS_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect18", playerEffect.player.UserIDString));
                        return;
                    }
                case "19":
                case "ap":
                    {
                        playerEffect.effect = AP_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 2.0f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect19", playerEffect.player.UserIDString));
                        return;
                    }
                case "20":
                case "fr":
                    {
                        playerEffect.effect = FR_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect20", playerEffect.player.UserIDString));
                        return;
                    }
                case "21":
                case "eg":
                    {
                        playerEffect.effect = EG_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.3f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect21", playerEffect.player.UserIDString));
                        return;
                    }
                case "22":
                case "ega":
                    {
                        playerEffect.effect = EGA_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.2f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect22", playerEffect.player.UserIDString));
                        return;
                    }
                case "23":
                case "egw":
                    {
                        playerEffect.effect = EGW_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect23", playerEffect.player.UserIDString));
                        return;
                    }
                case "24":
                case "es":
                    {
                        playerEffect.effect = ES_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect24", playerEffect.player.UserIDString));
                        return;
                    }
                case "25":
                case "ea":
                    {
                        playerEffect.effect = EA_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect25", playerEffect.player.UserIDString));
                        return;
                    }
                case "26":
                case "eas":
                    {
                        playerEffect.effect = EAS_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect26", playerEffect.player.UserIDString));
                        return;
                    }
                case "27":
                case "fs":
                    {
                        playerEffect.effect = FS_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect27", playerEffect.player.UserIDString));
                        return;
                    }
                case "28":
                case "fsa":
                    {
                        playerEffect.effect = FSA_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect28", playerEffect.player.UserIDString));
                        return;
                    }
                case "29":
                case "fsq":
                    {
                        playerEffect.effect = FSQ_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.2f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect29", playerEffect.player.UserIDString));
                        return;
                    }
                case "30":
                case "fse":
                    {
                        playerEffect.effect = FSE_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.2f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect30", playerEffect.player.UserIDString));
                        return;
                    }
                case "31":
                case "sif":
                    {
                        playerEffect.effect = SIF_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, 0);
                        playerEffect.time = 0.5f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect31", playerEffect.player.UserIDString));
                        return;
                    }
                case "help":
                    {
                        SendReply(playerEffect.player, GetLang("Help", playerEffect.player.UserIDString));
                        return;
                    }
                default:
                    SendReply(playerEffect.player, GetLang("Invalid", playerEffect.player.UserIDString));
                    return;
            }
        }

        #endregion

        #region [Chat Commands]

        [ChatCommand("pe")]
        private void cmdPlayerEffect(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return;
            }

            PlayerEffect playerEffect = player.gameObject.GetOrAddComponent<PlayerEffect>();
            if (playerEffect == null)
                return;

            var type = args.Length > 0 ? args[0] : null;
            AddEffect(playerEffect, type);
        }

        #endregion

        #region [Classes]

        public class PlayerEffect : MonoBehaviour
        {
            public BasePlayer player;
            public string effect;
            public Vector3 effectPosition;
            public float time;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                effect = string.Empty;
                effectPosition = Vector3.zero;
                time = 1f;
            }

            public void RunTimer() => InvokeRepeating("RunEffect", 0.2f, time);

            public void DestroyTimer() => CancelInvoke("RunEffect");

            private void RunEffect()
            {
                if (string.IsNullOrEmpty(effect) || player == null)
                    return;

                Effect.server.Run(effect, player, 0, effectPosition, new Vector3(0, 0, 0), null, true);
            }

            private void OnDestroy()
            {
                DestroyTimer();
                Destroy(this); 
            }
        }

        #endregion

        #region [Localization]

        private string GetLang(string key, string playerID) => lang.GetMessage(key, this, playerID);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPerm", "You don't have permissions" },
                { "Invalid", "Invalid syntax!\nType /pe help" },
                { "Disable", "Effect has been disabled" },
                { "Effect1", "Effect <color=#a8a6a6>'particles'</color> has been activated" },
                { "Effect2", "Effect <color=#a8a6a6>'smoke'</color> has been activated" },
                { "Effect3", "Effect <color=#a8a6a6>'fire'</color> has been activated" },
                { "Help", "<size=16><color=#3498db>Player Effects</color></size>" +
                "\n<color=#a8a6a6>/pe 0 or disable</color> - disable effect" +
                "\n<color=#a8a6a6>/pe 1 or particles</color> - particles effect" +
                "\n<color=#a8a6a6>/pe 2 or smoke</color> - smoke effect" +
                "\n<color=#a8a6a6>/pe 3 or fire</color> - fire effect" }

            }, this);
        }

        #endregion

        #region [Helpers]

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        private void SendMessage(BasePlayer player, string msg) => Player.Message(player, msg);

        private void PrintDebug(object message)
        {
            if (!DEBUG)
                return;

            Debug.Log($"{this.Name} Debug: {message}");
        }

        #endregion
    }
}