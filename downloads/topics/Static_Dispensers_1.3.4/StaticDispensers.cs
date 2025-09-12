using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info ( "StaticDispensers", "Raul-Sorin Sorban", "1.3.4" )]
    [Description ( "Harvest resources from objects that you normally cannot harvest from." )]
    public class StaticDispensers : RustPlugin
    {
        public static StaticDispensers Instance { get; private set; }

        public enum ZLevelGatherSkills
        {
            Woodcutting,
            Mining,
            Skinning
        }

        #region Raycast

        private static bool TryGetPlayerView ( BasePlayer player, out Quaternion viewAngle )
        {
            viewAngle = new Quaternion ( 0f, 0f, 0f, 0f );

            var input = player.serverInput;

            if ( input == null )
                return false;
            if ( input.current == null )
                return false;

            viewAngle = Quaternion.Euler ( input.current.aimAngles );
            return true;
        }
        private bool TryGetClosestRayPoint ( Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint )
        {
            var sourceEye = sourcePos + new Vector3 ( 0f, 1.5f, 0f );
            var ray = new Ray ( sourceEye, sourceDir * Vector3.forward );

            var hits = Physics.RaycastAll ( ray );
            var closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;

            foreach ( var hit in hits )
            {
                var colliderName = hit.collider.name.ToLower ().Trim ();
                if ( hit.distance < closestdist
                    && !colliderName.Contains ( "preventbuilding" )
                    && !colliderName.Contains ( "prevent_building" )
                    && !colliderName.Contains ( "prevent_movement" )
                    && !colliderName.Contains ( "terrain" )
                    && !colliderName.Contains ( "player.prefab" ) )
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }

            if ( closestEnt is bool )
                return false;

            return true;
        }

        #endregion

        #region Plugins

        [PluginReference] Plugin ZLevelsRemastered;

        #endregion

        #region Override

        private void OnServerInitialized ()
        {
            if ( Instance == null ) Instance = this;

            TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;

            Log ( $"Initialized dispensers." );
        }
        private void Loaded ()
        {
            if ( Instance == null ) Instance = this;

            if ( ConfigFile == null ) ConfigFile = new Core.Configuration.DynamicConfigFile ( $"{Manager.ConfigPath}/{Name}.json" );

            if ( !ConfigFile.Exists () ) { Config = GetDefaultConfig (); ConfigFile.WriteObject ( Config ?? ( Config = new RootConfig () ) ); }
            else { Config = ConfigFile.ReadObject<RootConfig> (); }

            if ( !DataFile.Exists () ) { Data = new RootData (); DataFile.WriteObject ( Data ?? ( Data = new RootData () ) ); }
            else { Data = DataFile.ReadObject<RootData> (); }
        }
        private void Unload ()
        {
            TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
        }
        private void OnServerSave ()
        {
            ConfigFile.WriteObject ( Config ?? ( Config = new RootConfig () ) );
            DataFile.WriteObject ( Data ?? ( Data = new RootData () ) );
        }

        public void OnSunrise ()
        {
            var dispenserCount = 0;

            foreach ( var dispenser in Data.Dispensers )
            {
                var definition = Config.Definitions.FirstOrDefault ( x => dispenser.Name.ToLower ().Contains ( x.PrefabFilter.ToLower ().Trim () ) );

                var changed = false;
                foreach ( var item in dispenser.Contents )
                {
                    var definitionItem = definition.Contents.FirstOrDefault ( x => x.ShortName == item.ShortName );

                    if ( definitionItem == null || item == null ) continue;
                    if ( item.Amount < definitionItem.Amount ) changed = true;

                    item.Amount += definitionItem.Amount / 4;
                    if ( item.Amount > definitionItem.Amount ) item.Amount = item.Amount;
                }

                if ( changed ) dispenserCount++;
            }
        }

        public RootConfig GetDefaultConfig ()
        {
            var stoneGatherables = new string [] { "rock", "pickaxe", "hammer.salvaged", "icepick.salvaged", "stone.pickaxe", "jackhammer" };
            var woodGatherables = new string [] { "rock", "axe.salvaged", "chainsaw", "stonehatchet", "hatchet" };

            var config = new RootConfig ()
            {
                Definitions = new RootConfig.DispenserDefinition []
                {
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "rock_small",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 100, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "rock_med",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 500, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "rock_cliff",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 5000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "rock_formation",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 10000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "powerline_pole",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "wood", 0UL, 200, 7, 100, ZLevelGatherSkills.Woodcutting ) },
                        whitelistedHeldItems: woodGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "pallet_stacks",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "wood", 0UL, 300, 10, 100, ZLevelGatherSkills.Woodcutting ) },
                        whitelistedHeldItems: woodGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "electrical_box_a",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 500, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "electrical_box_b",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 150, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "electrical_box_c",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 150, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "dish_radio",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 1000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: woodGatherables ),

                    new RootConfig.DispenserDefinition (
                        prefabFilter: "pickup_truck",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 500, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "compact_car",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 250, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "fuel_tank",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 250, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "bucket_excavator",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 750, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "sedan_",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 500, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "substation_",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 250, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "chainlink_fence",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 150, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "powerline_",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "metal.fragments", 0UL, 700, 7, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),

                    new RootConfig.DispenserDefinition (
                        prefabFilter: "tire_stack",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "rope", 0UL, 150, 2, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),

                    new RootConfig.DispenserDefinition (
                        prefabFilter: "peremiter_wall",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 500, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "cooling_tower",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 300, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "bus_stop",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 300, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "rock_quarry",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 150, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),

                    new RootConfig.DispenserDefinition (
                        prefabFilter: "cliff_low",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 100, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "cliff_medium",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 500, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "cliff_high",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 5000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "cliff_tall",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 5000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),

                    new RootConfig.DispenserDefinition (
                        prefabFilter: "formation_low",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 100, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables ),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "formation_medium",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 500, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                    new RootConfig.DispenserDefinition (
                        prefabFilter: "formation_high",
                        dispenserItems: new RootDispenserItem[] { new RootDispenserItem ( "stones", 0UL, 5000, 10, 100, ZLevelGatherSkills.Mining ) },
                        whitelistedHeldItems: stoneGatherables),
                }
            };

            return config;
        }

        private object OnMeleeAttack ( BasePlayer player, HitInfo info )
        {
            Quaternion currentRot;
            object closestEnt;
            Vector3 closestHitpoint;

            if ( !TryGetPlayerView ( player, out currentRot ) ) return null;
            if ( !TryGetClosestRayPoint ( player.transform.position, currentRot, out closestEnt, out closestHitpoint ) ) return null;

            var obj = ( closestEnt as Collider )?.gameObject;
            if ( closestEnt == null || obj == null ) return null;

            var definition = Config.Definitions.FirstOrDefault ( x => obj.name.ToLower ().Trim ().Contains ( x.PrefabFilter.ToLower ().Trim () ) );
            if ( definition != null )
            {
                var heldEntity = player.GetHeldEntity ();
                if ( !definition.WhitelistedHeldItems.Any ( x => x == heldEntity.GetItem ().info.shortname ) ) return null;

                var dispenser = Data.Dispensers.FirstOrDefault ( x => x.Name == obj.name && x.Position == obj.transform.position );
                if ( dispenser == null )
                {
                    Data.Dispensers.Add ( dispenser = new RootData.Dispenser ()
                    {
                        Name = obj.name,
                        Position = obj.transform.position
                    } );

                    foreach ( var contentsItem in definition.Contents )
                        dispenser.Contents.Add ( new RootItem ( contentsItem.ShortName, contentsItem.SkinId, contentsItem.Amount, contentsItem.CustomName ) );
                }

                if ( dispenser.IsDepleted () )
                {
                    return null;
                }

                var item = definition.GetItem ( dispenser );
                var definitionItem = definition.Contents.FirstOrDefault ( x => x.ShortName == item.ShortName );

                var previousAmount = item.Amount;
                var amount = ( int )( definitionItem.GatherAmount * Config.GatherMultiplier );
                if ( item.Amount <= amount ) { amount = item.Amount; item.Amount = 0; }
                else item.Amount -= amount;

                var playerItem = ItemManager.CreateByName ( item.ShortName, amount, item.SkinId );
                if ( !string.IsNullOrEmpty ( definitionItem.CustomName ) ) playerItem.name = definitionItem.CustomName;

                player.GiveItem ( playerItem, BaseEntity.GiveItemReason.ResourceHarvested );

                if ( Config.EnableZLevels && ZLevelsRemastered != null )
                {
                    var skill = "";
                    switch ( definitionItem.ZLevelsGatherSkill )
                    {
                        case ZLevelGatherSkills.Woodcutting: skill = "WC"; break;
                        case ZLevelGatherSkills.Mining: skill = "M"; break;
                        case ZLevelGatherSkills.Skinning: skill = "S"; break;
                    }

                    ZLevelsRemastered?.Call ( "levelHandler", player, playerItem, skill );
                }
            }

            return null;
        }

        public void Print ( object message, BasePlayer player = null )
        {
            if ( player == null ) PrintToChat ( $"<color=orange>StaticDispensers</color>: {message}" );
            else PrintToChat ( player, $"<color=orange>StaticDispensers</color> (OY): {message}" );
        }
        public void Log ( object message, BasePlayer player = null )
        {
            Puts ( $"{( player == null ? "" : $"({player.displayName}) " )}{message}" );
        }

        #endregion

        #region Config

        public new Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
        public new Core.Configuration.DynamicConfigFile DataFile { get; set; } = Interface.Oxide.DataFileSystem.GetFile ( "StaticDispensers_data" );

        public RootConfig Config { get; set; } = new RootConfig ();
        public RootData Data { get; set; } = new RootData ();

        public class RootItem
        {
            public string ShortName { get; set; }
            public string CustomName { get; set; }
            public ulong SkinId { get; set; }
            public int Amount { get; set; }

            public RootItem () { }
            public RootItem ( string shortName, ulong skinId, int amount, string customName = null )
            {
                ShortName = shortName;
                SkinId = skinId;
                Amount = amount;
                CustomName = customName;
            }
        }
        public class RootDispenserItem : RootItem
        {
            public int GatherChance { get; set; }
            public int GatherAmount { get; set; }
            public ZLevelGatherSkills ZLevelsGatherSkill { get; set; }

            public RootDispenserItem () { }
            public RootDispenserItem ( string shortName, ulong skinId, int amount, int gatherAmount, int gatherChance, ZLevelGatherSkills zLevelsGatherSkill ) : base ( shortName, skinId, amount ) { GatherAmount = gatherAmount; GatherChance = gatherChance; ZLevelsGatherSkill = zLevelsGatherSkill; }
        }

        public class RootConfig
        {
            public bool EnableZLevels { get; set; } = true;
            public string ZLevelSkills => "Woodcutting = 0, Mining = 1, Skinning = 2";

            public float GatherMultiplier { get; set; } = 1.25f;
            public DispenserDefinition [] Definitions { get; set; } = new DispenserDefinition [ 0 ];

            public class DispenserDefinition
            {
                private static System.Random Random { get; } = new System.Random ();

                public string PrefabFilter { get; set; }
                public RootDispenserItem [] Contents { get; set; } = new RootDispenserItem [ 0 ];
                public string [] WhitelistedHeldItems { get; set; } = new string [ 0 ];

                public DispenserDefinition () { }
                public DispenserDefinition (
                    string prefabFilter,
                    RootDispenserItem [] dispenserItems,
                    params string [] whitelistedHeldItems )
                {
                    PrefabFilter = prefabFilter;
                    Contents = dispenserItems;
                    WhitelistedHeldItems = whitelistedHeldItems;
                }

                public RootItem GetItem ( RootData.Dispenser dispenser )
                {
                    var contents = Contents.Where ( x => dispenser.Contents.FirstOrDefault ( y => x.ShortName == y.ShortName ).Amount > 0 ).OrderBy ( x => x.GatherChance ).ToArray ();
                    if ( contents.Length == 0 ) return null;

                    var totalRatio = contents.Sum ( x => x.GatherChance );
                    var randomNumber = Random.Next ( 0, totalRatio + 1 );
                    var outputContent = contents [ contents.Length - 1 ];

                    foreach ( var content in contents )
                    {
                        outputContent = content;

                        if ( content.GatherChance > randomNumber ) break;
                    }

                    return dispenser.Contents.FirstOrDefault ( x => x.ShortName == outputContent.ShortName );
                }
            }
        }
        public class RootData
        {
            public List<Dispenser> Dispensers { get; set; } = new List<Dispenser> ();

            public class Dispenser
            {
                private static System.Random Random { get; } = new System.Random ();

                public string Name { get; set; }
                public Vector3 Position { get; set; }
                public List<RootItem> Contents { get; set; } = new List<RootItem> ();

                public bool IsDepleted ()
                {
                    return Contents.Count == 0 || Contents.All ( x => x.Amount == 0 );
                }
            }
        }

        #endregion
    }
}
