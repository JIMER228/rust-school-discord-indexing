using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Rust;
using Oxide.Core.Plugins;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("SimpleBotSpawner", "AI Assistant", "1.0.0")]
    [Description("Спавнит 30 голых ботов с луком рядом с игроками и заставляет их атаковать")]
    class SimpleBotSpawner : RustPlugin
    {
        private List<NPCPlayer> spawnedBots = new List<NPCPlayer>();
        private int botCount = 30;
        private float spawnRadius = 150f;
        private string[] botNames = new string[] {
            "ShadowReaper", "ToxicWolf", "RustyNail", "DeadlyAim", "SilentHunter", "MadDog", "NightStalker", "LoneSurvivor", "IronFist", "GhostRider",
            "SavageKing", "BulletStorm", "FrostBite", "Wasteland", "Bloodhound", "Vandal", "Nomad", "Bandit", "Outlaw", "SniperQueen",
            "Venom", "Rogue", "Mercenary", "Warlord", "Blaze", "Viper", "Maverick", "Raider", "Butcher", "Grim",
            "AlphaWolf", "Omega", "Phoenix", "Raven", "Havoc", "Rebel", "Juggernaut", "Specter", "Predator", "Berserk",
            "RustyBlade", "BoneCrusher", "WildCard", "Storm", "Dusk", "Echo", "Pulse", "Zero", "Trigger", "Bullet"
        };
        private Dictionary<NPCPlayer, bool> botEvading = new Dictionary<NPCPlayer, bool>();
        private Dictionary<NPCPlayer, float> botReturnTime = new Dictionary<NPCPlayer, float>();
        private Vector3? lastEvadePos = null;
        private enum BotActionType { Chase, Circle, Flank, Retreat, Patrol, Idle, Hide }
        private Dictionary<NPCPlayer, BotActionType> botActions = new Dictionary<NPCPlayer, BotActionType>();
        private Dictionary<NPCPlayer, float> botActionChangeTime = new Dictionary<NPCPlayer, float>();
        private System.Random sysRand = new System.Random();

        void OnPlayerRespawned(BasePlayer player)
        {
            SpawnBotNearPlayer(player);
        }

        void Unload()
        {
            foreach (var bot in spawnedBots)
            {
                if (bot != null && !bot.IsDestroyed)
                    bot.Kill();
            }
            spawnedBots.Clear();
            botEvading.Clear();
            botReturnTime.Clear();
        }

        private void SpawnBotNearPlayer(BasePlayer player)
        {
            Vector3 spawnPos = Vector3.zero;
            bool found = false;
            for (int i = 0; i < 150; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
                Vector3 tryPos = new Vector3(player.transform.position.x + offset.x, 0, player.transform.position.z + offset.y);
                tryPos.y = TerrainMeta.HeightMap.GetHeight(tryPos);
                var blocks = Pool.GetList<BuildingBlock>();
                Vis.Entities<BuildingBlock>(tryPos, 2f, blocks, 1 << 21);
                if (blocks.Count > 0)
                {
                    Pool.FreeList(ref blocks);
                    continue;
                }
                Pool.FreeList(ref blocks);
                var ents = Pool.GetList<BaseEntity>();
                Vis.Entities<BaseEntity>(tryPos, 2f, ents, -1, QueryTriggerInteraction.Ignore);
                bool hasPrefab = false;
                foreach (var ent in ents)
                {
                    if (ent is BuildingBlock) continue;
                    if (ent.PrefabName.Contains("door") || ent.PrefabName.Contains("wall") || ent.PrefabName.Contains("monument") || ent.PrefabName.Contains("prefab") || ent.PrefabName.Contains("item"))
                    {
                        hasPrefab = true;
                        break;
                    }
                }
                Pool.FreeList(ref ents);
                if (hasPrefab)
                {
                    continue;
                }
                RaycastHit upHit;
                if (Physics.Raycast(tryPos + Vector3.up * 1f, Vector3.up, out upHit, 30f))
                {
                    continue;
                }
                RaycastHit downHit;
                Vector3 skyPos = new Vector3(tryPos.x, 1000f, tryPos.z);
                if (Physics.Raycast(skyPos, Vector3.down, out downHit, 2000f))
                {
                    if (Mathf.Abs(downHit.point.y - tryPos.y) > 0.5f) {
                        continue;
                    }
                    if (downHit.collider.gameObject.layer != LayerMask.NameToLayer("Terrain")) {
                        continue;
                    }
                    spawnPos = downHit.point;
                    found = true;
                    break;
                } else {
                }
            }
            if (!found) {
                return;
            }

            var bot = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/pet/frankensteinpet.prefab", spawnPos) as NPCPlayer;
            if (bot == null) return;

            bot.Spawn();
            bot.inventory.Strip();
            var bow = ItemManager.CreateByName("bow.hunting", 1);
            var arrows = ItemManager.CreateByName("arrow.wooden", 30);
            bot.inventory.GiveItem(bow, bot.inventory.containerBelt);
            bot.inventory.GiveItem(arrows, bot.inventory.containerMain);
            // Медицинские предметы
            for (int i = 0; i < 5; i++)
            {
                var med = ItemManager.CreateByName("syringe.medical", 1);
                if (med != null) bot.inventory.GiveItem(med, bot.inventory.containerBelt);
            }
            for (int i = 0; i < 10; i++)
            {
                var bandage = ItemManager.CreateByName("bandage", 1);
                if (bandage != null) bot.inventory.GiveItem(bandage, bot.inventory.containerBelt);
            }
            string randomName = botNames[UnityEngine.Random.Range(0, botNames.Length)];
            bot.displayName = randomName;
            bot.InitializeHealth(100, 100);
            bot.UpdateProtectionFromClothing();
            bot.UpdateActiveItem(bow.uid);

            var insideBlocks = Pool.GetList<BuildingBlock>();
            Vis.Entities<BuildingBlock>(bot.transform.position, 0.5f, insideBlocks, 1 << 21);
            if (insideBlocks.Count > 0)
            {
                bot.Kill();
                Pool.FreeList(ref insideBlocks);
                return;
            }
            Pool.FreeList(ref insideBlocks);

            spawnedBots.Add(bot);
            botEvading[bot] = false;
            HuntPlayer(bot, player);
        }

        private void HuntPlayer(NPCPlayer bot, BasePlayer target)
        {
            timer.Repeat(0.5f, 0, () => {
                if (botEvading.ContainsKey(bot) && botEvading[bot]) return;
                if (bot == null || bot.IsDestroyed || target == null || target.IsDead()) return;
                if (botReturnTime.ContainsKey(bot) && Time.realtimeSinceStartup < botReturnTime[bot]) return;

                float now = Time.realtimeSinceStartup;
                if (!botActions.ContainsKey(bot) || now > botActionChangeTime.GetValueOrDefault(bot, 0))
                {
                    BotActionType newAction = ChooseBotAction(bot, target);
                    botActions[bot] = newAction;
                    botActionChangeTime[bot] = now + sysRand.Next(2, 6); // 2-5 сек
                }
                BotActionType action = botActions[bot];
                DoBotAction(bot, target, action);
            });
        }

        private BotActionType ChooseBotAction(NPCPlayer bot, BasePlayer target)
        {
            float dist = Vector3.Distance(bot.transform.position, target.transform.position);
            int roll = sysRand.Next(100);
            if (dist > 40f) return BotActionType.Patrol;
            if (dist < 10f && roll < 10) return BotActionType.Retreat;
            if (dist < 25f && roll < 20) return BotActionType.Circle;
            if (roll < 10) return BotActionType.Flank;
            if (roll < 20) return BotActionType.Hide;
            if (roll < 60) return BotActionType.Chase;
            return BotActionType.Idle;
        }

        private void DoBotAction(NPCPlayer bot, BasePlayer target, BotActionType action)
        {
            var navigator = bot.GetComponent<NPCPlayerNavigator>();
            if (navigator == null) return;
            float dist = Vector3.Distance(bot.transform.position, target.transform.position);
            switch (action)
            {
                case BotActionType.Chase:
                    if (dist > 5f)
                        navigator.SetDestination(target.transform.position, BaseNavigator.NavigationSpeed.Fast);
                    else
                        navigator.SetDestination(bot.transform.position, BaseNavigator.NavigationSpeed.Normal);
                    bot.SetAimDirection((target.transform.position - bot.transform.position).normalized);
                    break;
                case BotActionType.Circle:
                    Vector3 dir = (bot.transform.position - target.transform.position).normalized;
                    Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
                    float sign = sysRand.Next(2) == 0 ? 1f : -1f;
                    Vector3 circlePos = target.transform.position + dir * 15f + perp * sign * 10f;
                    navigator.SetDestination(circlePos, BaseNavigator.NavigationSpeed.Normal);
                    bot.SetAimDirection((target.transform.position - bot.transform.position).normalized);
                    break;
                case BotActionType.Flank:
                    Vector3 flankDir = Quaternion.Euler(0, sysRand.Next(-90, 90), 0) * (target.transform.position - bot.transform.position).normalized;
                    Vector3 flankPos = target.transform.position + flankDir * sysRand.Next(10, 25);
                    navigator.SetDestination(flankPos, BaseNavigator.NavigationSpeed.Fast);
                    bot.SetAimDirection((target.transform.position - bot.transform.position).normalized);
                    break;
                case BotActionType.Retreat:
                    Vector3 away = (bot.transform.position - target.transform.position).normalized;
                    Vector3 retreatPos = bot.transform.position + away * sysRand.Next(10, 20);
                    navigator.SetDestination(retreatPos, BaseNavigator.NavigationSpeed.Fast);
                    break;
                case BotActionType.Patrol:
                    Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * sysRand.Next(20, 40);
                    Vector3 patrolPos = target.transform.position + new Vector3(offset.x, 0, offset.y);
                    patrolPos.y = TerrainMeta.HeightMap.GetHeight(patrolPos);
                    navigator.SetDestination(patrolPos, BaseNavigator.NavigationSpeed.Normal);
                    break;
                case BotActionType.Idle:
                    navigator.SetDestination(bot.transform.position, BaseNavigator.NavigationSpeed.Slow);
                    break;
                case BotActionType.Hide:
                    Vector3 hideDir = UnityEngine.Random.onUnitSphere; hideDir.y = 0; hideDir.Normalize();
                    Vector3 hidePos = bot.transform.position + hideDir * sysRand.Next(10, 20);
                    navigator.SetDestination(hidePos, BaseNavigator.NavigationSpeed.Fast);
                    break;
            }
        }

        private Item FindItemInInventory(PlayerInventory inv, int itemid)
        {
            foreach (var container in new[] { inv.containerBelt, inv.containerMain, inv.containerWear })
            {
                foreach (var item in container.itemList)
                {
                    if (item.info.itemid == itemid)
                        return item;
                }
            }
            return null;
        }

        private void HealBotWithItem(NPCPlayer npc, Item item)
        {
            if (item.info.shortname == "syringe.medical")
            {
                npc.health = Mathf.Min(npc.health + 30f, npc.MaxHealth());
                item.Remove(1);
            }
            else if (item.info.shortname == "bandage")
            {
                npc.health = Mathf.Min(npc.health + 10f, npc.MaxHealth());
                item.Remove(1);
            }
        }

        object OnEntityTakeDamage(NPCPlayer npc, HitInfo info)
        {
            if (npc == null || !spawnedBots.Contains(npc) || botEvading.TryGetValue(npc, out var evading) && evading)
                return null;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return null;
            // Лечение при ранении
            if (npc.health < npc.MaxHealth())
            {
                int syringeId = ItemManager.FindItemDefinition("syringe.medical").itemid;
                int bandageId = ItemManager.FindItemDefinition("bandage").itemid;
                var syringe = FindItemInInventory(npc.inventory, syringeId);
                if (syringe != null)
                {
                    HealBotWithItem(npc, syringe);
                }
                else
                {
                    var bandage = FindItemInInventory(npc.inventory, bandageId);
                    if (bandage != null)
                        HealBotWithItem(npc, bandage);
                }
            }
            botEvading[npc] = true;
            EvadeSequence(npc, 0);
            return null;
        }

        private void EvadeSequence(NPCPlayer npc, int step)
        {
            if (npc == null || npc.IsDestroyed || step >= 25) {
                botEvading[npc] = false;
                botReturnTime[npc] = Time.realtimeSinceStartup + 60f;
                lastEvadePos = null;
                return;
            }
            // Случайное направление, избегаем слишком близких точек
            Vector3 randomDir;
            Vector3 evadePos;
            int attempts = 0;
            do {
                randomDir = UnityEngine.Random.onUnitSphere; randomDir.y = 0; randomDir.Normalize();
                evadePos = npc.transform.position + randomDir * 20f;
                attempts++;
            } while (lastEvadePos.HasValue && Vector3.Distance(evadePos, lastEvadePos.Value) < 5f && attempts < 10);
            lastEvadePos = evadePos;
            RaycastHit hit;
            if (Physics.Raycast(npc.transform.position, randomDir, out hit, 20f))
                evadePos = hit.point - randomDir * 2f;
            var navigator = npc.GetComponent<NPCPlayerNavigator>();
            if (navigator != null)
                navigator.SetDestination(evadePos, BaseNavigator.NavigationSpeed.Fast);
            timer.Once(2f, () => EvadeSequence(npc, step + 1));
        }
    }
}