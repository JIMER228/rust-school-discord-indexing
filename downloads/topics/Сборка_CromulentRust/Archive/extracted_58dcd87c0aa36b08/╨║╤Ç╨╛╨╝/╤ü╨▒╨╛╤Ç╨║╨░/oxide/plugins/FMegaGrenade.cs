using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Facepunch.Extend;
using Oxide.Core.Plugins;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("FMegaGrenade", "FourTeen", "1.0.2")]
    public class FMegaGrenade : RustPlugin
    {
        List <string> cache = new List<string>();
        [PluginReference] private Plugin BuildProtection;
        private Dictionary<ulong, float> cooldown = new Dictionary<ulong, float>();
        private const ulong GrenadeSkin = 1052763402;
        private const string GrenadeName = "MEGA GRENADE";
        private const float ExplosionRadius = 25f;
        private const string Perm = "FMegaGrenade.use";
        void OnServerInitialized()
        {
            permission.RegisterPermission(Perm, this);
            LoadData();
            foreach (var kvp in cooldown)
            {
                timer.Once(kvp.Value, () => cooldown.Remove(kvp.Key));
            }
        }
        [ConsoleCommand("megagrenade.give")]
        private void GiveMegaGrenade(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            var player = BasePlayer.FindByID(((ulong)arg.Args[0].ToLong()));
            if (player == null) return;

            var item = ItemManager.CreateByName("grenade.f1", 1, GrenadeSkin);
            item.name = GrenadeName;
            player.GiveItem(item);
            player.Command("note.inv", item.info.shortname);
        }
        [ChatCommand("megagrenade")]
        void actionCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (permission.UserHasPermission(player.UserIDString, Perm))
            {
                float cd = GetGlobalCooldown(player);
                if (cd != 0)
                {
                    string formattedString = cd.ToString("0");
                    player.ChatMessage($"Вы уже использовали эту команду. Используйте её снова через <color=#FF9740>{formattedString}</color> секунд.");
                    return;
                }
                rust.RunServerCommand("megagrenade.give " + player.UserIDString);
                SetGlobalCooldown(player);
                SaveData();
            }
            else
            {
                player.ChatMessage($"Нет прав!");
                return;
            }
        }
        void OnExplosiveFuseSet(TimedExplosive ent, float fuseLength)
        {
            if (ent?.skinID == GrenadeSkin)
            {
                timer.Once(2.4f, () => CheckExplosion(ent, ent.creatorEntity.ToPlayer()));
            }
        }

        private void CheckExplosion(BaseEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return;
            List<BaseEntity> entities = new List<BaseEntity>();
            Vis.Entities(entity.transform.position, ExplosionRadius, entities);
            foreach (var ent in entities)
            {
                if (ent != null)
                {
                    if (ent is BasePlayer targetPlayer && targetPlayer != null)
                    {
                        ApplyEffect(targetPlayer);
                    }
                    else
                    {
                        if (ent.OwnerID != 0 && BuildProtection)
                        {
                            BuildingPrivlidge cupboard = ent.GetBuildingPrivilege();
                            if (cupboard != null && cupboard.OwnerID != player.userID.Get() && !cache.Contains(cupboard.net.ID.ToString()))
                            {
                                object protectionData = BuildProtection.CallHook("GetProtectionData", cupboard, player.userID.Get());
                                if (protectionData != null && protectionData is Dictionary<string, object>)
                                {
                                    var data = (Dictionary<string, object>)protectionData;
                                    ulong creator = (ulong)data["Creator"];
                                    int initiatorLvl = (int)data["InitiatorLvl"];
                                    int creatorLvl = (int)data["CreatorLvl"];
                                    if (creator != player.userID.Get())
                                    {
                                        if (initiatorLvl == creatorLvl || creatorLvl > initiatorLvl)
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            cache.Add(cupboard.net.ID.ToString());
                                            foreach (var building in cupboard.GetBuilding().buildingBlocks)
                                            {
                                                NextTick(() => building.Kill());
                                            }
                                            var creatorPlayer = BasePlayer.FindByID(creator);
                                            if (creatorPlayer != null) BuildProtection.CallHook("Message", creatorPlayer, initiatorLvl);
                                        }
                                    }
                                }
                                else
                                {
                                    cache.Add(cupboard.net.ID.ToString());
                                    foreach (var building in cupboard.GetBuilding().buildingBlocks)
                                    {
                                        NextTick(() => building.Kill());
                                    }
                                }
                            }
                            else if (cupboard == null)
                            {
                                NextTick(() => ent.Kill());
                            }
                        }
                    }
                }
            }
            entity.Kill();
        }

        private void ApplyEffect(BasePlayer player)
        {
            BaseEntity playerEntity = player;
            Effect reusableInstance = new Effect();
            reusableInstance.Clear();

            reusableInstance.Init(Effect.Type.Generic, playerEntity, 0, new Vector3(0, 0, 0), new Vector3(0, 0, 0), null);
            reusableInstance.scale = false ? 0.0f : 1f;


            reusableInstance.pooledString = "assets/prefabs/npc/sam_site_turret/effects/rocket_sam_explosion.prefab";
            EffectNetwork.Send(reusableInstance);
            player.metabolism.radiation_poison.SetValue(500);
            player.UpdateRadiation(5);
            MakeUi(player);
        }

        private void MakeUi(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1.0 1.0 1.0 1.0" },
                FadeOut = 1,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "Overlay", "Flashbang");

            CuiHelper.DestroyUi(player, "Flashbang");
            CuiHelper.AddUi(player, container);
            timer.Once(3f, () => { CuiHelper.DestroyUi(player, "Flashbang"); });
        }
        private void SetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return;
            float rb = 1800;
            ulong userid = player.userID;
            cooldown[userid] = Time.time + rb;
            timer.Once(rb, () => cooldown.Remove(userid));
        }
        private float GetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return 0f;
            float cd;
            if (!cooldown.TryGetValue(player.userID.Get(), out cd))
            {
                return 0f;
            }
            return cd - Time.time;
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title + "/CoolDown", cooldown);
        }
        void LoadData()
        {
            try
            {
                cooldown = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>(Title + "/CoolDown");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
        }
    }
}