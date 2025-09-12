using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ScanObj", "Molik", "0.0.1")]
    public class ScanObj : RustPlugin
    {
        [ChatCommand("obj")]
        void cmdObj(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Вы не админ!");
                return;
            }
            else
            {
                int a = 0;
                var entities = player.GetBuildingPrivilege()?.GetBuilding()?.decayEntities;
                if (entities != null)
                {
                    foreach (var entitys in entities)
                    {
                        if (entitys.PrefabName.Contains("assets/prefabs/building core"))
                            a++;
                    }
                }
                player.ChatMessage($"В этом доме: {a} объектов(");
            }
        }
    }
}