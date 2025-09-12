using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BStream", "Hougan", "0.0.1")]
    public class BStream : RustPlugin
    {
        #region Classes

        private static class Configuration
        {
            [JsonProperty("Количество радиации")]
            public static int RadiationAmount = 200;
            [JsonProperty("Урон от удушья")]
            public static int DrownDamage = 25;
            [JsonProperty("Количество крови")]
            public static int BloodAmount = 100;
        }
        
        #endregion
        
        #region Commands

        [ConsoleCommand("bstream")]
        private void ConsoleExecute(ConsoleSystem.Arg args)
        {
            if (!args.HasArgs(4))
            {
                PrintError($"Failed to execute stream: {args.FullString}");
                return;
            } 
            
            
            ulong targetId = 0UL;
            if (!ulong.TryParse(args.Args[0], out targetId))
            {
                PrintError($"Unknown userID [{args.Args[0]}]!");
                return; 
            }
            
            
            int price = 0;
            if (!int.TryParse(args.Args[3], out price))
            {
                PrintError($"Unknown price [{args.Args[3]}]!");
                return; 
            }
                
            string actionName = args.Args[1];
            string initiator = args.Args[2];
            
            PrintError($"Executing '{actionName}' for '{targetId}' by '{initiator}' for '{price}'");
            var targetPlayer = BasePlayer.FindByID(targetId);
            if (targetPlayer == null || !targetPlayer.IsConnected) return;
            
            ExecuteAction(targetPlayer, actionName, initiator, price); 
        }

        private void ExecuteAction(BasePlayer player, string action, string target, float price)
        {
            switch (action.ToLower())
            {
                case "damage":
                {
                    if (!player.IsAlive()) return;
                   
                    foreach (var check in player.inventory.containerBelt.itemList)
                        check.LoseCondition(1000);
                    foreach (var check in player.inventory.containerWear.itemList)
                        check.LoseCondition(1000); 
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> сломал вам предметы <color=#e57373>за {price:F1} руб.</color>"); 
                    Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", player.transform.position);					
                    break;
                }
                case "kill": 
                {
                    if (!player.IsAlive()) return;
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> убил вас <color=#e57373>за {price:F1} руб.</color>");
					Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", player.transform.position); 
                    player.Die();
                    break;
                }
                case "radiation":
                {
                    EffectNetwork.Send(new Effect("PREFAB", player, 0, Vector3.zero, Vector3.zero, player.Connection));
                    
                    if (!player.IsAlive()) return;
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> облучил вас радиацией <color=#e57373>за {price:F1} руб.</color>");
                    player.metabolism.radiation_poison.SetValue(Configuration.RadiationAmount);
                    player.metabolism.radiation_level.SetValue(Configuration.RadiationAmount);
                    player.metabolism.SendChangesToClient();
                    break;
                }
                case "drown":
                {
                    if (!player.IsAlive()) return;
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> начал душить вас <color=#e57373>за {price:F1} руб.</color>");
                    timer.Repeat(3, 4, () =>
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/player/drown.prefab", player.transform.position);
						Effect.server.Run("assets/bundled/prefabs/fx/screen_jump.prefab", player.transform.position);
                        player.health -= Configuration.DrownDamage;
                    });
                    break;
                }
                case "blood":
                {
                    if (!player.IsAlive()) return;
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> пустил вам кровь <color=#e57373>за {price:F1} руб.</color>");
					Effect.server.Run("assets/bundled/prefabs/fx/player/gutshot_scream.prefab", player.transform.position);
                    player.metabolism.bleeding.SetValue(Configuration.BloodAmount);
                    player.metabolism.SendChangesToClient(); 
                    break;
                }
                case "destroy":
                {
                    var list = BaseNetworkable.serverEntities.Where(p => p is BuildingBlock && ((BuildingBlock) p).OwnerID == player.userID && (((BuildingBlock) p).GetBuildingPrivilege()?.IsAuthed(player) ?? false));  
                    foreach (var check in list)
                        check.Kill();
                    
                    player.ChatMessage($"Меценат <color=#bbbbbb>{target}</color> разрушил ваши постройки <color=#e57373>за {price:F1}р</color>");
                    break;
                }
            }
        }
        
        #endregion
    }
}