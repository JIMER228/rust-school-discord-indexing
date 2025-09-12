//Reference: 0Harmony

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.AtlasEx;
using Console = Oxide.Ext.AtlasEx.Console;

namespace Oxide.Plugins
{
    [Info("(Atlas) Vanish", "Ilovepatatos", "1.0.0")]
    public class AtlasVanish : CovalencePlugin
    {
        private const string CONFIGS_FILENAME = nameof(AtlasVanish);
        private const string WEBSITE_URL = "https://i.postimg.cc/2ynd0WPv/VANISH-4.png";

        private static AtlasVanish s_PluginInstance;
        private static string s_UserInterfaceCache;

        private static readonly HashSet<string> s_ToolsWhitelist = new HashSet<string>()
        {
            "assets/prefabs/weapons/hammer/hammer.entity.prefab"
        };

        private Configuration m_InternalConfigs;
        private DataFileSystem m_ConfigsFolder;

        private Harmony m_Harmony;

#pragma warning disable CS0649
        // ReSharper disable once InconsistentNaming
        [PluginReference] private Plugin ImageLibrary;
#pragma warning restore CS0649

#region Debug

        private static void Log(object msg, string color = Console.NORMAL)
        {
            Console.Log($"[(Atlas) Vanish] {msg}", color);
        }

        private static void Profile(object msg, string color = Console.NORMAL)
        {
            if (s_PluginInstance.Configs.EnableLogs)
                Console.Log($"[(Atlas) Vanish] {msg}", color);
        }

#endregion

#region Configs

        [Serializable]
        private class Configuration
        {
            public bool EnableLogs;

            public string UsePerms = "atlasvanish.use";
            public string StartVanishPerms = "atlasvanish.startvanish";
            public string HideUserInterfacePerms = "atlasvanish.hideui";

            public string IgnoreTrapsPerms = "atlasvanish.ignoretraps";
            public string IgnoreAIPerms = "atlasvanish.ignoreai";

            public string IgnoreLocksPerms = "atlasvanish.ignorelocks";

            [JsonProperty("Disable damage when invisible")]
            public bool DisableDamage = true;

            public string AppearSfx = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
            public string DisappearSfx = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";

            public static Configuration GetTemplate()
            {
                var configs = new Configuration();
                return configs;
            }
        }

        private Configuration Configs
        {
            get
            {
                if (m_InternalConfigs == null)
                    LoadConfig();

                return m_InternalConfigs;
            }
            set { m_InternalConfigs = value; }
        }

        private static Configuration StaticConfigs => s_PluginInstance.Configs;

        private DataFileSystem ConfigsFolder => m_ConfigsFolder ?? (m_ConfigsFolder = new DataFileSystem($"{Interface.Oxide.ConfigDirectory}"));

        protected override void LoadConfig()
        {
            if (ConfigsFolder.ExistsDatafile(CONFIGS_FILENAME))
            {
                try
                {
                    Configs = ConfigsFolder.ReadObject<Configuration>(CONFIGS_FILENAME);
                    SaveConfig();
                }
                catch (Exception ex)
                {
                    Log($"The configuration file seems to be corrupted!\n{ex}");
                    LoadDefaultConfig();
                }
            }
            else
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Configs = Configuration.GetTemplate();
        }

        protected override void SaveConfig()
        {
            ConfigsFolder.WriteObject(CONFIGS_FILENAME, Configs);
        }

#endregion

#region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BECOMING_VISIBLE"] = "You are no longer vanished",
                ["BECOMING_INVISIBLE"] = "You are now vanished",
            }, this);
        }

        public static string Lang(IPlayer iPlayer, string key)
        {
            return s_PluginInstance.lang.GetMessage(key, s_PluginInstance, iPlayer?.Id);
        }

        public static string Lang(string key)
        {
            return Lang((IPlayer)null, key);
        }

        public static string Lang(BasePlayer player, string key)
        {
            IPlayer iPlayer = player != null ? player.IPlayer : null;
            return Lang(iPlayer, key);
        }

#endregion

#region Command

        [UsedImplicitly]
        [Command("vanish")]
        private void VanishCmd(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.DoesPlayerHavePerms(Configs.UsePerms))
                return;

            var player = iPlayer.ToBasePlayer();
            if (player == null) return;

            if (player.limitNetworking)
            {
                Reappear(player);
            }
            else
            {
                Disappear(player);
            }
        }

#endregion

#region Hooks

        [UsedImplicitly]
        [HookMethod(nameof(Init))]
        public void Init()
        {
            Log($"Initializing {nameof(AtlasVanish)}...");
            s_PluginInstance = this;
            m_Harmony = new Harmony(Name + "Patches");
            SubscribeHooks();

            permission.RegisterPermission(Configs.UsePerms, this);
            permission.RegisterPermission(Configs.StartVanishPerms, this);
            permission.RegisterPermission(Configs.HideUserInterfacePerms, this);
            permission.RegisterPermission(Configs.IgnoreTrapsPerms, this);
            permission.RegisterPermission(Configs.IgnoreAIPerms, this);
            permission.RegisterPermission(Configs.IgnoreLocksPerms, this);
        }


        [UsedImplicitly]
        [HookMethod(nameof(OnServerInitialized))]
        private void OnServerInitialized(bool initial)
        {
            if (initial)
            {
                // Give time for image library to initialize...
                timer.Once(15f, () =>
                {
                    if (ImageLibrary == null)
                        Log($"THIS PLUGIN REQUIRES THE \"{nameof(ImageLibrary)}.cs\" PLUGIN FROM OXIDE", Console.RED);

                    ImageLibrary?.Call("AddImage", WEBSITE_URL + "atlas-vanish.png", "vanish (Image)", (ulong)0, new Action(InitializeUserInterface));
                });
            }
            else
            {
                if (ImageLibrary == null)
                    Log($"THIS PLUGIN REQUIRES THE \"{nameof(ImageLibrary)}.cs\" PLUGIN FROM OXIDE", Console.RED);

                ImageLibrary?.Call("AddImage", WEBSITE_URL + "atlas-vanish.png", "vanish (Image)", (ulong)0, new Action(InitializeUserInterface));
            }

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

// Define a custom ignore list to replace SimpleAIMemory
private static HashSet<BasePlayer> ignoredPlayers = new HashSet<BasePlayer>();

[UsedImplicitly]
[HookMethod(nameof(Unload))]
private void Unload()
{
    Log($"Unloading {nameof(AtlasVanish)}...");
    UnsubscribeHooks();
    ImageLibrary?.Call("RemoveImage", "vanish (Image)", (ulong)0);

    foreach (BasePlayer player in BasePlayer.activePlayerList)
    {
        if (player == null)
            continue;

        if (player.limitNetworking)
            Reappear(player);

        ignoredPlayers.Remove(player);
    }
}

[UsedImplicitly]
[HookMethod(nameof(OnPlayerConnected))]
private void OnPlayerConnected(BasePlayer player)
{
    if (player.DoesPlayerHavePerms(Configs.StartVanishPerms))
    {
        Disappear(player);
    }
    else if (player.limitNetworking)
    {
        Reappear(player);
    }

    if (!IsPlayerTargetableByAI(player))
        ignoredPlayers.Add(player);
}



[UsedImplicitly]
[HookMethod(nameof(OnPlayerDisconnected))]
private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    if (player != null)
        ignoredPlayers.Remove(player);
}


        private void SubscribeHooks()
        {
            if (Configs.DisableDamage)
            {
                {
                    var initial = AccessTools.Method(typeof(BaseMelee), nameof(BaseMelee.PlayerAttack));
                    var patch = new HarmonyMethod(typeof(BaseMeleeHarmony), nameof(BaseMeleeHarmony.PlayerAttackTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(BaseMelee), "CLProject");
                    var patch = new HarmonyMethod(typeof(BaseMeleeHarmony), nameof(BaseMeleeHarmony.CLProjectTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(BaseProjectile), "CLProject");
                    var patch = new HarmonyMethod(typeof(BaseProjectileHarmony), nameof(BaseProjectileHarmony.CLProjectTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(BaseLauncher), "SV_Launch");
                    var patch = new HarmonyMethod(typeof(BaseLauncherHarmony), nameof(BaseLauncherHarmony.SV_LaunchTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(FlameThrower), nameof(FlameThrower.FlameTick));
                    var patch = new HarmonyMethod(typeof(FlameThrowerHarmony), nameof(FlameThrowerHarmony.FlameTickTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(ThrownWeapon), "DoThrow");
                    var patch = new HarmonyMethod(typeof(ThrownWeaponHarmony), nameof(ThrownWeaponHarmony.DoThrowTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }

                {
                    var initial = AccessTools.Method(typeof(ThrownWeapon), "DoDrop");
                    var patch = new HarmonyMethod(typeof(ThrownWeaponHarmony), nameof(ThrownWeaponHarmony.DoDropTranspiler));
                    m_Harmony.Patch(initial, null, null, patch);
                }
            }

            {
                var initial = AccessTools.Method(typeof(TargetTrigger), nameof(TargetTrigger.InterestedInObject));
                var patch = new HarmonyMethod(typeof(TargetTriggerHarmony), nameof(TargetTriggerHarmony.InterestedInObjectPostfix));
                m_Harmony.Patch(initial, null, patch);
            }

            {
                var initial = AccessTools.Method(typeof(BaseTrapTrigger), nameof(BaseTrapTrigger.InterestedInObject));
                var patch = new HarmonyMethod(typeof(BearTrapTriggerHarmony), nameof(BearTrapTriggerHarmony.InterestedInObjectPostfix));
                m_Harmony.Patch(initial, null, patch);
            }

            {
                var initial = AccessTools.Method(typeof(ServerProjectile), "IsAValidHit");
                var patch = new HarmonyMethod(typeof(ServerProjectileHarmony), nameof(ServerProjectileHarmony.IsAValidHitPostfix));
                m_Harmony.Patch(initial, null, patch);
            }

            {
                var initial = AccessTools.Method(typeof(BradleyAPC), nameof(BradleyAPC.VisibilityTest));
                var patch = new HarmonyMethod(typeof(BradleyAPCHarmony), nameof(BradleyAPCHarmony.VisibilityTestTranspiler));
                m_Harmony.Patch(initial, null, null, patch);
            }

            {
                var initial = AccessTools.Method(typeof(PatrolHelicopterAI), nameof(PatrolHelicopterAI.UpdateTargetList));
                var patch = new HarmonyMethod(typeof(PatrolHelicopterHarmony), nameof(PatrolHelicopterHarmony.UpdateTargetListTranspiler));
                m_Harmony.Patch(initial, null, null, patch);
            }

            {
                var initial = AccessTools.Method(typeof(BaseVehicle), nameof(BaseVehicle.OnAttacked));
                var patch = new HarmonyMethod(typeof(BaseVehicleHarmony), nameof(BaseVehicleHarmony.OnAttackedPrefix));
                m_Harmony.Patch(initial, patch);
            }

            {
                var initial = AccessTools.Method(typeof(ScientistNPC), nameof(ScientistNPC.OnAttacked));
                var patch = new HarmonyMethod(typeof(ScientistNPCHarmony), nameof(ScientistNPCHarmony.OnAttackedPrefix));
                m_Harmony.Patch(initial, patch);
            }

            {
                var initial = AccessTools.Method(typeof(StorageContainer), nameof(StorageContainer.CanOpenLootPanel));
                var patch = new HarmonyMethod(typeof(StorageContainerHarmony), nameof(StorageContainerHarmony.CanOpenLootPanelPrefix));
                m_Harmony.Patch(initial, patch);
            }
        }

        private void UnsubscribeHooks()
        {
            m_Harmony.UnpatchAll(m_Harmony.Id);
        }

#endregion

#region Harmony

        /// <summary>
        /// Disable shooting server side while invisible
        /// </summary>
        private static class BaseMeleeHarmony
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(BaseMelee), nameof(BaseMelee.PlayerAttack))]
            public static IEnumerable<CodeInstruction> PlayerAttackTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();
                int index = list.FindIndex(x => x.opcode == OpCodes.Stloc_0);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH {nameof(BaseMelee.PlayerAttack)}() TO PREVENT INVISIBLE PLAYERS FROM USING MELEE TOOL", Console.RED);
                    return list;
                }

                index += 1;

                // Load arguments for method
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_0)); // BasePlayer
                list.Insert(index++, new CodeInstruction(OpCodes.Ldarg_0)); // this (BaseMelee)

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseMeleeTool), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));
                list[index].WithLabels(label);

                return list;
            }

            // ReSharper disable once InconsistentNaming
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(BaseMelee), "CLProject")]
            public static IEnumerable<CodeInstruction> CLProjectTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();
                int index = list.FindIndex(x => x.opcode == OpCodes.Stloc_0);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH BaseMelee.CLProject() TO PREVENT INVISIBLE PLAYERS FROM SHOOTING MELEE TOOL", Console.RED);
                    return list;
                }

                index += 1;

                // Load arguments for method
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_0)); // BasePlayer

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseBaseProjectile), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));
                list[index].WithLabels(label);

                return list;
            }
        }

        /// <summary>
        /// Disable shooting server side while invisible
        /// </summary>
        private static class BaseProjectileHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(BaseProjectile), "CLProject")]
            public static IEnumerable<CodeInstruction> CLProjectTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                var target = typeof(BaseProjectile).GetMethod(nameof(BaseProjectile.ModifyAmmoCount));
                int index = list.FindIndex(x => x.opcode == OpCodes.Call && x.operand == target);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH BaseProjectile.CLProject() TO PREVENT INVISIBLE PLAYERS FROM SHOOTING WEAPON", Console.RED);
                    return list;
                }

                index -= 5;

                // Load arguments for method
                Label previous = list[index].labels[0];
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_0).WithLabels(previous)); // BasePlayer

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseBaseProjectile), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                list[index].labels.Clear();
                list[index].WithLabels(label);

                return list;
            }
        }

        /// <summary>
        /// Disable shooting server side while invisible
        /// </summary>
        private static class BaseLauncherHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(BaseLauncher), "SV_Launch")]
            public static IEnumerable<CodeInstruction> SV_LaunchTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                var target = typeof(AttackEntity).GetProperty("UsingInfiniteAmmoCheat", ~(BindingFlags)0)?.GetGetMethod(true);
                int index = list.FindIndex(x => x.opcode == OpCodes.Call && x.operand == target);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH BaseLauncher.SV_Launch() TO PREVENT INVISIBLE PLAYERS FROM SHOOTING ROCKET LAUNCHERS", Console.RED);
                    return list;
                }

                index -= 1;

                // Load arguments for method
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_0)); // BasePlayer

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseBaseProjectile), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                // Assign labels to next instruction
                list[index].WithLabels(label);

                return list;
            }
        }

        /// <summary>
        /// Disable shooting server side while invisible
        /// </summary>
        private static class FlameThrowerHarmony
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(FlameThrower), nameof(FlameThrower.FlameTick))]
            public static IEnumerable<CodeInstruction> FlameTickTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                var target = typeof(FlameThrower).GetMethod(nameof(FlameThrower.ReduceAmmo));
                int index = list.FindIndex(x => x.opcode == OpCodes.Call && x.operand == target);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH {nameof(FlameThrower.FlameTick)}() TO PREVENT INVISIBLE PLAYERS FROM SHOOTING FLAME THROWER", Console.RED);
                    return list;
                }

                index -= 2;

                // Store labels
                var labels = list[index].labels.ToArray();
                list[index].labels.Clear();

                // Load arguments for method
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_1).WithLabels(labels)); // BasePlayer

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseBaseProjectile), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                // Assign labels to next instruction
                list[index].WithLabels(label);

                return list;
            }
        }

        /// <summary>
        /// Disable shooting server side while invisible
        /// </summary>
        private static class ThrownWeaponHarmony
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ThrownWeapon), "DoThrow")]
            public static IEnumerable<CodeInstruction> DoThrowTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                int index = list.FindIndex(x => x.opcode == OpCodes.Ret);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH ThrownWeapon.DoThrow() TO PREVENT PLAYERS FROM SHOOTING ON ISLANDS", Console.RED);
                    return list;
                }

                index += 1;

                // Load arguments for method
                Label previous = list[index].labels[0];
                list.Insert(index++, new CodeInstruction(OpCodes.Ldarg_1).WithLabels(previous)); // RPCMessage

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseThrownWeapon), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                list[index].labels.Clear();
                list[index].WithLabels(label);

                return list;
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ThrownWeapon), "DoDrop")]
            public static IEnumerable<CodeInstruction> DoDropTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                int index = list.FindIndex(x => x.opcode == OpCodes.Ret);

                if (index == -1)
                {
                    Log($"COULDN'T PATCH ThrownWeapon.DoDrop() TO PREVENT PLAYERS FROM SHOOTING ON ISLANDS", Console.RED);
                    return list;
                }

                index += 1;

                // Load arguments for method
                Label previous = list[index].labels[0];
                list.Insert(index++, new CodeInstruction(OpCodes.Ldarg_1).WithLabels(previous)); // RPCMessage

                // Call method
                MethodInfo canPlayerUseWeapon =
                    typeof(AtlasVanish).GetMethod(nameof(CanPlayerUseThrownWeapon), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canPlayerUseWeapon));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if false
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                list[index].labels.Clear();
                list[index].WithLabels(label);

                return list;
            }
        }

        /// <summary>
        /// Disable players with perm from entering trap colliders
        /// </summary>
        private static class TargetTriggerHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyPostfix]
            [HarmonyPatch(typeof(TargetTrigger), nameof(TargetTrigger.InterestedInObject))]
            public static void InterestedInObjectPostfix(GameObject obj, ref GameObject __result)
            {
                if (__result == null) return;

                BasePlayer player = obj.ToBaseEntity() as BasePlayer;
                if (player == null || player.IPlayer == null) return;

                if (player.IPlayer.HasPermission(StaticConfigs.IgnoreTrapsPerms))
                    __result = null;
            }
        }

        /// <summary>
        /// Disable players with perm from entering trap colliders
        /// </summary>
        private static class BearTrapTriggerHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyPostfix]
            [HarmonyPatch(typeof(BearTrapTrigger), nameof(BearTrapTrigger.InterestedInObject))]
            public static void InterestedInObjectPostfix(GameObject obj, ref GameObject __result)
            {
                if (__result == null) return;

                BasePlayer player = obj.ToBaseEntity() as BasePlayer;
                if (player == null || player.IPlayer == null) return;

                if (player.IPlayer.HasPermission(StaticConfigs.IgnoreTrapsPerms))
                    __result = null;
            }
        }

        private static class ServerProjectileHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ServerProjectile), "IsAValidHit")]
            public static void IsAValidHitPostfix(BaseEntity hitEnt, ref bool __result)
            {
                if (__result && hitEnt != null)
                    __result = !hitEnt.limitNetworking;
            }
        }

        /// <summary>
        /// Disable npc from targeting players with perm
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static class ScientistNPCHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ScientistNPC), nameof(ScientistNPC.OnAttacked))]
            public static bool OnAttackedPrefix(HitInfo info)
            {
                var initiator = info?.InitiatorPlayer;
                return initiator == null || !initiator.limitNetworking;
            }
        }

        /// <summary>
        /// Disable bradley from targeting players with perm
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static class BradleyAPCHarmony
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(BradleyAPC), nameof(BradleyAPC.VisibilityTest))]
            public static IEnumerable<CodeInstruction> VisibilityTestTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var list = instructions.ToList();

                int index = list.FindIndex(x => x.opcode == OpCodes.Stloc_1);

                if (index == -1)
                {
                    Log($"HARMONY EXCEPTION! COULDN'T PATCH {nameof(BradleyAPC.VisibilityTest)}() TO PREVENT BRADLEY FROM TARGETING PLAYERS", Console.RED);
                    return list;
                }

                index += 1;

                // Load arguments for method
                list.Insert(index++, new CodeInstruction(OpCodes.Ldloc_1));

                // Call method
                MethodInfo canTargetPlayer =
                    typeof(AtlasVanish).GetMethod(nameof(IsPlayerTargetableByAI), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(index++, new CodeInstruction(OpCodes.Call, canTargetPlayer));

                // Create new label to go to after if
                Label label = generator.DefineLabel();

                // Return if true
                list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, label));
                list.Insert(index++, new CodeInstruction(OpCodes.Ldc_I4_0)); // Push false onto the stack
                list.Insert(index++, new CodeInstruction(OpCodes.Ret));

                list[index].WithLabels(label); // Add label to default instruction to go back to

                return list;
            }
        }

        /// <summary>
        /// Disable patrol helicopter from targeting players with perm
        /// </summary>
        private static class PatrolHelicopterHarmony
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(PatrolHelicopterAI), nameof(PatrolHelicopterAI.UpdateTargetList))]
            public static IEnumerable<CodeInstruction> UpdateTargetListTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var list = instructions.ToList();

                var targetMethod = typeof(Vector3Ex).GetMethod(nameof(Vector3Ex.Distance2D));
                int index = list.FindIndex(x => x.opcode == OpCodes.Call && (MethodInfo)x.operand == targetMethod);

                if (index == -1)
                {
                    Log($"HARMONY EXCEPTION! COULDN'T PATCH {nameof(PatrolHelicopterAI.UpdateTargetList)}() TO PREVENT HELI FROM TARGETING PLAYERS",
                        Console.RED);

                    return list;
                }

                index += 3;

                // Load arguments for method
                list.Insert(index, new CodeInstruction(OpCodes.Ldloc_S, 8));

                // Call method
                MethodInfo canTargetPlayer =
                    typeof(AtlasVanish).GetMethod(nameof(IsPlayerTargetableByAI), BindingFlags.Static | BindingFlags.NonPublic);

                list.Insert(++index, new CodeInstruction(OpCodes.Call, canTargetPlayer));

                // Return if false
                list.Insert(++index, new CodeInstruction(OpCodes.Brfalse_S, new Label().SetValue(25)));

                return list;
            }
        }

        /// <summary>
        /// Disable patrol helicopter from targeting players with perm on getting attacked
        /// </summary>
        private static class BaseVehicleHarmony
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(BaseVehicle), nameof(BaseVehicle.OnAttacked))]
            public static bool OnAttackedPrefix(HitInfo info)
            {
                var initiator = info?.InitiatorPlayer;
                return initiator == null || !initiator.limitNetworking;
            }
        }

        private static class StorageContainerHarmony
        {
            // ReSharper disable once InconsistentNaming
            [HarmonyPrefix]
            [HarmonyPatch(typeof(StorageContainer), nameof(StorageContainer.CanOpenLootPanel))]
            public static bool CanOpenLootPanelPrefix(BasePlayer player, ref bool __result)
            {
                if (player != null && player.IPlayer.HasPermission(StaticConfigs.IgnoreLocksPerms))
                    __result = true;

                return !__result;
            }
        }

#endregion

#region Methods

        private void InitializeUserInterface()
        {
            Configuration configs = Configs;
            CreateVanishUI();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!player.limitNetworking)
                    continue;

                if (!player.DoesPlayerHavePerms(configs.UsePerms))
                    continue;

                CuiHelper.DestroyUi(player, "VanishUI");

                bool hideUserInterface = player.IPlayer.HasPermission(configs.HideUserInterfacePerms);

                if (!hideUserInterface)
                    CuiHelper.AddUi(player, s_UserInterfaceCache);
            }
        }


        private void Disappear(BasePlayer player)
        {
            Log($"{player} is now invisible");
            Configuration configs = Configs;

            Effect effect = new Effect(configs.DisappearSfx, player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.Connection);

            player.PauseFlyHackDetection(float.MaxValue);
            player.limitNetworking = true;

            player.fallDamageEffect = new GameObjectRef();
            player.drownEffect = new GameObjectRef();

            if (!player.IsFlying && !player.isMounted)
                player.SendConsoleCommand("noclip");

            CuiHelper.DestroyUi(player, "VanishUI");

            bool hideUserInterface = player.IPlayer.HasPermission(configs.HideUserInterfacePerms);

            if (!hideUserInterface)
                CuiHelper.AddUi(player, s_UserInterfaceCache);

            string text = Lang(player, "BECOMING_INVISIBLE");
            player.ShowToast(GameTip.Styles.Red_Normal, text);
        }


        private void Reappear(BasePlayer player)
        {
            Log($"{player} is now visible");
            Configuration configs = Configs;

            Effect effect = new Effect(configs.AppearSfx, player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.Connection);

            player.ResetAntiHack();
            player.limitNetworking = false;

            player.drownEffect.guid = "28ad47c8e6d313742a7a2740674a25b5";
            player.fallDamageEffect.guid = "ca14ed027d5924003b1c5d9e523a5fce";

            CuiHelper.DestroyUi(player, "VanishUI");

            string text = Lang(player, "BECOMING_VISIBLE");
            player.ShowToast(GameTip.Styles.Red_Normal, text);
        }


#endregion

#region Utility

        private static bool IsPlayerTargetableByAI(BasePlayer player)
        {
            return player == null || player.IPlayer == null || !player.IPlayer.HasPermission(StaticConfigs.IgnoreAIPerms);
        }

        private static bool CanPlayerUseMeleeTool(BasePlayer player, BaseMelee melee)
        {
            return player == null || !player.limitNetworking || s_ToolsWhitelist.Contains(melee.PrefabName);
        }

        private static bool CanPlayerUseBaseProjectile(BasePlayer player)
        {
            return player == null || !player.limitNetworking;
        }

        private static bool CanPlayerUseThrownWeapon(BaseEntity.RPCMessage msg)
        {
            BasePlayer player = msg.player;
            return player == null || !player.limitNetworking;
        }

#endregion

#region UI

        private void CreateVanishUI()
        {
            Profile("Creating vanish ui...");
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "VanishUI",
                Parent = "Under",
                Components =
                {
                    new CuiRawImageComponent { Color = "1.000 1.000 1.000 1.000", Png = ImageLibrary?.Call<string>("GetImage", "vanish (Image)") },
                    new CuiRectTransformComponent { AnchorMin = "0.219 0.04", AnchorMax = "0.338 0.092", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            s_UserInterfaceCache = container.ToJson();
        }

#endregion
    }
}