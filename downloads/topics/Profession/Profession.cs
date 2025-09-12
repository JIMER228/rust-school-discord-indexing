using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Profession", "CommandoSoldat", "1.0.8")]
    public class Profession : CovalencePlugin
    {
        #region Cui
        private void InfoPanel(BasePlayer player, string txt)
        {

            CuiHelper.DestroyUi(player, "VoicePanel");

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.719 0.975", AnchorMax = "0.8 1" }
            }, "Overlay", "VoicePanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                FadeOut = 1,
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.25f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70.206 -25.428", OffsetMax = "300.204 5.428" }
            }, "VoicePanel", "VoicePanelBack");

            container.Add(new CuiElement
            {
                Name = "VoiceText",
                Parent = "VoicePanel",
                FadeOut = 1,
                Components = {
                    new CuiTextComponent { Text = $"<color=red>{lang.GetMessage(txt, this)}</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.25f },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75.206 -25.428", OffsetMax = "300.204 5.428" }
                }
            });

            CuiHelper.AddUi(player, container);
            timer.Once(5f, () => { CuiHelper.DestroyUi(player, "VoicePanelBack"); CuiHelper.DestroyUi(player, "VoiceText"); });
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WrongProfessionCraft"] = "Your profession cant craft this item",
                ["WrongProfessionResearch"] = "Your profession cant research this item",
                ["OnlyOilextractorUse"] = "Only the Oilextractor can use the refinery",
                ["OnlyMinerUse"] = "Only the Miner can use the big furnace",
                ["OnlyOilextractoPlace"] = "Only the Oilextractor can place the refinery",
                ["OnlyMinerPlace"] = "Only the Miner can place the big furnace",
                ["Command"] = "Command: /diesel (ammount)",
                ["Received"] = "You received ",
                ["LowGradeFuel"] = " low grade fuel",
                ["NotEnought"] = "Not enough diesel fuel",
                ["Argument"] = "Only one argument",
                ["NoAuthorization"] = "No authorization for this command",
                ["WrongProfessionRepair"] = "Your profession cant repair this item",
                ["OnlyVehicleengineerRepair"] = "Only the Vehicleengineer can repair",
                ["electrician"] = "Only the Electrician can use the wire tool"
            }, this);
            //German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WrongProfessionCraft"] = "Du kannst diesen Gegenstand nicht Craften",
                ["WrongProfessionResearch"] = "Du kannst diesen Gegenstand nicht Lernen",
                ["OnlyOilextractorUse"] = "Nur der Ölförderer kann die Raffinerie benutzen",
                ["OnlyMinerUse"] = "Nur der Minenarbeiter kann den großen Ofen benutzen",
                ["OnlyOilextractoPlace"] = "Nur der Ölförderer kann die Raffinerie platzieren",
                ["OnlyMinerPlace"] = "Nur der Minenarbeiter kann den großen Ofen platzieren",
                ["Command"] = "Befehl: /diesel (Anzahl)",
                ["Received"] = "Du hast ",
                ["LowGradeFuel"] = " Low Grade Fuel erhalten",
                ["NotEnought"] = "Nicht genug Diesel Fuel vorhanden",
                ["Argument"] = "Bitte nur ein Argument angeben",
                ["NoAuthorization"] = "Keine Berechtigung für diesen Befehl",
                ["WrongProfessionRepair"] = "Falscher Beruf um diesen Gegenstand zu Reparieren",
                ["OnlyVehicleengineerRepair"] = "Nur der Fahrzeugingenieur kann reparieren",
                ["electrician"] = "Nur der Elektriker kann das Drahtwerkzeug benutzen"
            }, this, "de");
        }
        #endregion

        #region Initialization
        private const string forester = "profession.forester.use";
        private const string electrician = "profession.electrician.use";
        private const string weaponengineer = "profession.weaponengineer.use";
        private const string miner = "profession.miner.use";
        private const string oilextractor = "profession.oilextractor.use";
        private const string vehicleengineer = "profession.vehicleengineer.use";
        private const string tailor = "profession.tailor.use";
        private const string graphicdesigner = "profession.graphicdesigner.use";
        private const string doctor = "profession.doctor.use";
        private const string farmer = "profession.farmer.use";
        private const string sanitaer = "profession.sanitaer.use";
        ArrayList professionList = new ArrayList
        {
            "Forester",
            "Electrician",
            "Weaponengineer",
            "Miner",
            "Oilextractor",
            "Vehicleengineer",
            "Tailor",
            "Graphicdesigner",
            "Doctor",
            "Farmer",
            "sanitaer"
        };
        private Dictionary<string, string> permissionList = new Dictionary<string, string>
        {
            { "forester", "profession.forester.use" },
            {"electrician", "profession.electrician.use" },
            {"weaponengineer", "profession.weaponengineer.use" },
            {"miner", "profession.miner.use" },
            {"oilextractor", "profession.oilextractor.use" },
            {"vehicleengineer", "profession.vehicleengineer.use" },
            {"tailor", "profession.tailor.use" },
            {"graphicdesigner", "profession.graphicdesigner.use" },
            {"doctor", "profession.doctor.use" },
            {"farmer", "profession.farmer.use" },
            {"sanitaer", "profession.sanitaer.use" }
        };

        public string fx = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";
        void Init()
        {
            permission.RegisterPermission(forester, this);
            permission.RegisterPermission(electrician, this);
            permission.RegisterPermission(weaponengineer, this);
            permission.RegisterPermission(miner, this);
            permission.RegisterPermission(oilextractor, this);
            permission.RegisterPermission(vehicleengineer, this);
            permission.RegisterPermission(tailor, this);
            permission.RegisterPermission(graphicdesigner, this);
            permission.RegisterPermission(doctor, this);
            permission.RegisterPermission(farmer, this);
            permission.RegisterPermission(sanitaer, this);
            LoadDefaultMessages();
            LoadConfig();
        }


        #endregion

        #region Config
        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        class Configuration
        {
            [JsonProperty("Use only Oilextractor can place Refinery")]
            public bool UseAllowPlaceRefinery { get; set; }
            [JsonProperty("Use only Miner can place Furnace")]
            public bool UseAllowPlaceFurnace { get; set; }
            [JsonProperty("Use only Oilextractor can start Refinery")]
            public bool UseAllowToggleRefinery { get; set; }
            [JsonProperty("Use only Miner can start Furnace")]
            public bool UseAllowToggleFurnace { get; set; }
            [JsonProperty("Use Oilextractor can exchange diesel to lowgradefuel")]
            public bool UseAllowExchangeDiesel { get; set; }
            [JsonProperty("Exchange rate for diesel (multiplier only integers)")]
            public int exchangeRate { get; set; }
            [JsonProperty("Use only Forester can repair Forester items")]
            public bool UseAllowRepairForester { get; set; }
            [JsonProperty("Use only Miner can repair Miner items")]
            public bool UseAllowRepairMiner { get; set; }
            [JsonProperty("Use only Weaponengineer can repair Weaponengineer items")]
            public bool UseAllowRepairWeaponengineer { get; set; }
            [JsonProperty("Use only Vehicleengineer can repair vehicles")]
            public bool UseAllowRepairVehicle { get; set; }
            [JsonProperty("Use player can grand Profession with chatcommand")]
            public bool UseProfessionSelfeGrand { get; set; }
            [JsonProperty("Use only the Electrician can use the wire tool")]
            public bool UseWireToolOnlyElectrician { get; set; }
            [JsonProperty("Use bonus for Forester and Miner on farming")]
            public bool UseBonusForForesterAndMiner { get; set; }
            [JsonProperty("Bonus for gathering trees or ores (in integer only)")]
            public int percentPerHit { get; set; }
            [JsonProperty("Bonus for the last hit on a tree or ore (in integer only)")]
            public int percentForBonusHit { get; set; }

            [JsonProperty(PropertyName = "Forester")]
            public HashSet<string> foresterList = new HashSet<string>();
            [JsonProperty(PropertyName = "Graphic_Designer")]
            public HashSet<string> graphicdesignerList = new HashSet<string>();
            [JsonProperty(PropertyName = "Doctor")]
            public HashSet<string> doctorList = new HashSet<string>();
            [JsonProperty(PropertyName = "Tailor")]
            public HashSet<string> tailorList = new HashSet<string>();
            [JsonProperty(PropertyName = "Electrician")]
            public HashSet<string> electricianList = new HashSet<string>();
            [JsonProperty(PropertyName = "Weapon_Engineer")]
            public HashSet<string> weaponengineerList = new HashSet<string>();
            [JsonProperty(PropertyName = "Miner")]
            public HashSet<string> minerList = new HashSet<string>();
            [JsonProperty(PropertyName = "Oil_Extractor")]
            public HashSet<string> oilextractorList = new HashSet<string>();
            [JsonProperty(PropertyName = "Vehicle_Engineer")]
            public HashSet<string> vehicleengineerList = new HashSet<string>();
            [JsonProperty(PropertyName = "Vehicle_Engineer_Prefab_List")]
            public HashSet<string> vehicleengineerPrefabList = new HashSet<string>();
            [JsonProperty(PropertyName = "Farmer")]
            public HashSet<string> farmerList = new HashSet<string>();
            [JsonProperty(PropertyName = "Sanitaer")]
            public HashSet<string> sanitaerList = new HashSet<string>();

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    UseAllowPlaceRefinery = true,
                    UseAllowPlaceFurnace = true,
                    UseAllowToggleRefinery = true,
                    UseAllowToggleFurnace = true,
                    UseAllowExchangeDiesel = true,
                    UseAllowRepairForester = true,
                    UseAllowRepairMiner = true,
                    UseAllowRepairWeaponengineer = true,
                    UseAllowRepairVehicle = true,
                    UseProfessionSelfeGrand = false,
                    UseWireToolOnlyElectrician = false,
                    UseBonusForForesterAndMiner = false,
                    percentPerHit = 20,
                    percentForBonusHit = 100,
                    exchangeRate = 300,
                    foresterList =
                    {
                        "chainsaw",
                        "wall.graveyard.fence"
                    },
                    graphicdesignerList = {
                        "sign.neon.xl",
                        "sign.neon.125x215.animated",
                        "sign.neon.125x215",
                        "sign.neon.xl.animated",
                        "sign.neon.125x125"
                    },
                    doctorList = {
                        "syringe.medical",
                        "largemedkit",
                        "Basic Healing Tea",
                        "maxhealthtea",
                        "healingtea.advanced",
                        "maxhealthtea.advanced",
                        "healingtea.pure",
                        "maxhealthtea.pure"
                    },
                    tailorList = {
                        "shoes.boots",
                        "diving.mask",
                        "hat.gas.mask",
                        "heavy.plate.helmet",
                        "tshirt.long",
                        "roadsign.jacket",
                        "jacket.snow",
                        "tshirt",
                        "diving.wetsuit",
                        "hat.cap",
                        "bucket.helmet",
                        "coffeecan.helmet",
                        "diving.tank",
                        "heavy.plate.jacket",
                        "metal.plate.torso",
                        "roadsign.kilt",
                        "tactical.gloves",
                        "heavy.plate.pants",
                        "jacket",
                        "metal.facemask",
                        "pants",
                        "roadsign.gloves",
                        "shirt.collared",
                        "hat.beenie",
                        "hat.boonie",
                        "diving.fins",
                        "hazmatsuit",
                        "hoodie",
                        "riot.helmet"
                    },
                    electricianList = {
                        "electric.andswitch",
                        "electric.button",
                        "ceilinglight",
                        "electric.counter",
                        "electric.doorcontroller",
                        "electrical.branch",
                        "electric.solarpanel.large",
                        "electric.battery.rechargable.medium",
                        "electric.random.switch",
                        "electrical.combiner",
                        "electric.fuelgenerator.small",
                        "electric.battery.rechargable.small",
                        "smart.switch",
                        "electric.switch",
                        "electric.blocker",
                        "elevator",
                        "electric.hbhfsensor",
                        "electric.battery.rechargable.large",
                        "electrical.memorycell",
                        "electric.orswitch",
                        "searchlight",
                        "electric.splitter",
                        "electric.teslacoil",
                        "electric.timer",
                        "generator.wind.scrap",
                        "electric.xorswitch",
                        "industrial.conveyor",
                        "storageadaptor",
                        "industrial.crafter",
                        "industrial.splitter",
                        "industrial.combiner"

                    },
                    weaponengineerList = {
                        "weapon.mod.small.scope",
                        "hmlmg",
                        "pistol.prototype17",
                        "weapon.mod.burstmodule",
                        "knife.combat",
                        "shotgun.double",
                        "weapon.mod.holosight",
                        "smg.mp5",
                        "weapon.mod.muzzleboost",
                        "rocket.launcher",
                        "rifle.semiauto",
                        "smg.thompson",
                        "weapon.mod.lasersight",
                        "pistol.semiauto",
                        "flamethrower",
                        "smg.2",
                        "rifle.bolt",
                        "rifle.ak",
                        "weapon.mod.muzzlebrake",
                        "shotgun.pump",
                        "weapon.mod.silencer",
                        "weapon.mod.flashlight",
                        "grenade.f1",
                        "pistol.python",
                        "explosive.timed",
                        "ammo.shotgun",
                        "ammo.grenadelauncher.buckshot",
                        "ammo.rifle.explosive",
                        "ammo.pistol.fire",
                        "ammo.shotgun.fire",
                        "ammo.grenadelauncher.smoke",
                        "ammo.rocket.fire",
                        "ammo.rocket.basic",
                        "submarine.torpedo.straight",
                        "ammo.shotgun.slug",
                        "ammo.rifle",
                        "ammo.rifle.hv",
                        "ammo.rocket.hv",
                        "ammo.rocket.sam",
                        "ammo.grenadelauncher.he",
                        "ammo.pistol.hv",
                        "ammo.rifle.incendiary",
                        "ammo.pistol",
                        "rifle.lr300",
                        "lmg.m249",
                        "rifle.l96",
                        "pistol.m92",
                        "rifle.m39",
                        "shotgun.spas12",
                        "multiplegrenadelauncher",
                        "weapon.mod.8x.scope"

                    },
                    minerList = {
                        "furnace.large",
                        "jackhammer"
                    },
                    oilextractorList = {
                        "small.oil.refinery"
                    },
                    vehicleengineerList = {
                        "vehicle.1mod.cockpit.armored",
                        "vehicle.1mod.cockpit",
                        "piston3",
                        "carburetor1",
                        "valve1",
                        "piston2",
                        "vehicle.1mod.storage",
                        "vehicle.1mod.passengers.armored",
                        "vehicle.1mod.cockpit.with.engine",
                        "vehicle.1mod.engine",
                        "sparkplug3",
                        "crankshaft1",
                        "sparkplug2",
                        "vehicle.1mod.flatbed",
                        "carburetor3",
                        "valve3",
                        "piston1",
                        "carburetor2",
                        "valve2",
                        "vehicle.2mod.passengers",
                        "vehicle.1mod.taxi",
                        "vehicle.2mod.camper",
                        "vehicle.2mod.fuel.tank",
                        "crankshaft3",
                        "vehicle.2mod.flatbed",
                        "sparkplug1",
                        "crankshaft2",
                        "vehicle.1mod.rear.seats",
                        "modularcarlift"
                    },
                    vehicleengineerPrefabList = {
                        "1module_cockpit_armored",
                        "1module_cockpit",
                        "1module_storage",
                        "1module_passengers_armored",
                        "1module_cockpit_with_engine",
                        "1module_engine",
                        "1module_flatbed",
                        "2module_flatbed",
                        "2module_passengers",
                        "1module_taxi",
                        "2module_camper",
                        "2module_fuel_tank",
                        "1module_rear_seats",
                        "car_chassis_2module.entity",
                        "car_chassis_3module.entity",
                        "car_chassis_4module.entity"
                    },
                    farmerList =
                    {
                        "radiationresisttea.advanced",
                        "healingtea.advanced",
                        "maxhealthtea.advanced",
                        "oretea.advanced",
                        "radiationremovetea.advanced",
                        "scraptea.advanced",
                        "woodtea.advanced",
                        "radiationresisttea",
                        "healingtea",
                        "maxhealthtea",
                        "oretea",
                        "scraptea",
                        "woodtea",
                        "radiationresisttea.pure",
                        "healingtea.pure",
                        "maxhealthtea.pure",
                        "oretea.pure",
                        "radiationremovetea.pure",
                        "scraptea.pure",
                        "woodtea.pure",
                        "radiationremovetea"
                    },
                    sanitaerList =
                    {
                        "fluid.splitter",
                        "electric.sprinkler",
                        "fluid.switch",
                        "powered.water.purifier",
                        "waterpump",
                        "fluid.combiner",
                        "electric.heater"
                    },
                };
            }


        }
        #endregion

        #region Hooks
       
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
           
            BasePlayer player = entity as BasePlayer;
            if (config.UseBonusForForesterAndMiner)
            {
                if (permission.UserHasPermission(player.UserIDString, forester) && dispenser.gatherType.ToString() == "Tree" || permission.UserHasPermission(player.UserIDString, miner) && dispenser.gatherType.ToString() == "Ore")
                {
                    var amountOfPercent = (item.amount * config.percentPerHit) / 100;
                    var newAmount = item.amount + amountOfPercent;
                    item.amount = newAmount;
                    return null;
                }

            }

            return null;
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (config.UseBonusForForesterAndMiner)
            {
                if (permission.UserHasPermission(player.UserIDString, forester) && dispenser.gatherType.ToString() == "Tree" || permission.UserHasPermission(player.UserIDString, miner) && dispenser.gatherType.ToString() == "Ore")
                {
                    var amountOfPercent = (item.amount * config.percentForBonusHit) / 100;
                    var newAmount = item.amount + amountOfPercent;
                    item.amount = newAmount;
                }

            }

        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if (config.UseWireToolOnlyElectrician)
            {
                if (!permission.UserHasPermission(player.UserIDString, electrician))
                {
                    RunEffect(player.transform.position, fx, player);
                    InfoPanel(player, "electrician");
                    player.ChatMessage(lang.GetMessage("electrician", this));
                    return false;
                }
            }

            return null;
        }
        object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            if (config.UseWireToolOnlyElectrician)
            {
                if (!permission.UserHasPermission(player.UserIDString, electrician))
                {
                    RunEffect(player.transform.position, fx, player);
                    InfoPanel(player, "electrician");
                    player.ChatMessage(lang.GetMessage("electrician", this));
                    return false;
                }
            }
            return null;
        }



        void OnMixingTableToggle(MixingTable table, BasePlayer player) => NextTick(() =>
        {

            if (player)
            {

                try
                {
                    if (config.farmerList.Contains(table.currentRecipe.ProducedItem.shortname) || config.weaponengineerList.Contains(table.currentRecipe.ProducedItem.shortname))
                    {

                        if (permission.UserHasPermission(player.UserIDString, farmer) && config.farmerList.Contains(table.currentRecipe.ProducedItem.shortname)
                        || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(table.currentRecipe.ProducedItem.shortname))
                        {

                        }
                        else
                        {

                            table.StopMixing();
                        }
                    }
                }
                catch
                {

                    //error
                }

            }



        });

        object OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
        {

            Item theItem = ItemManager.CreateByItemID(task.blueprint.targetItem.itemid);
            if (player)
            {
                if (config.sanitaerList.Contains(theItem.info.shortname) || config.foresterList.Contains(theItem.info.shortname) || config.electricianList.Contains(theItem.info.shortname) || config.weaponengineerList.Contains(theItem.info.shortname) || config.minerList.Contains(theItem.info.shortname) || config.oilextractorList.Contains(theItem.info.shortname) || config.vehicleengineerList.Contains(theItem.info.shortname) || config.tailorList.Contains(theItem.info.shortname) || config.graphicdesignerList.Contains(theItem.info.shortname) || config.doctorList.Contains(theItem.info.shortname))
                {
                    if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, electrician) && config.electricianList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, oilextractor) && config.oilextractorList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, vehicleengineer) && config.vehicleengineerList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, tailor) && config.tailorList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, graphicdesigner) && config.graphicdesignerList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, doctor) && config.doctorList.Contains(theItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, sanitaer) && config.sanitaerList.Contains(theItem.info.shortname))
                    {

                        return null;
                    }
                    else
                    {


                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "WrongProfessionCraft");
                        player.ChatMessage(lang.GetMessage("WrongProfessionCraft", this));
                        List<Item> itemsTaken = task.takenItems;
                        foreach (var i in itemsTaken)
                        {
                            player.GiveItem(i, BaseEntity.GiveItemReason.PickedUp);
                        }
                        return false;


                    }
                }
            }
            return null;
        }
        //Broken because of QuickCraft Middle Mouse Button 

        /* bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
         {
             var player = itemCrafter.GetComponent<BasePlayer>();
             if (player)
             {

                 if (config.sanitaerList.Contains(bp.targetItem.shortname) || config.foresterList.Contains(bp.targetItem.shortname) || config.electricianList.Contains(bp.targetItem.shortname) || config.weaponengineerList.Contains(bp.targetItem.shortname) || config.minerList.Contains(bp.targetItem.shortname) || config.oilextractorList.Contains(bp.targetItem.shortname) || config.vehicleengineerList.Contains(bp.targetItem.shortname) || config.tailorList.Contains(bp.targetItem.shortname) || config.graphicdesignerList.Contains(bp.targetItem.shortname) || config.doctorList.Contains(bp.targetItem.shortname))
                 {
                     if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, electrician) && config.electricianList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, oilextractor) && config.oilextractorList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, vehicleengineer) && config.vehicleengineerList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, tailor) && config.tailorList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, graphicdesigner) && config.graphicdesignerList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, doctor) && config.doctorList.Contains(bp.targetItem.shortname)
                     || permission.UserHasPermission(player.UserIDString, sanitaer) && config.sanitaerList.Contains(bp.targetItem.shortname))
                     {

                         return true;
                     }

                     else
                     {
                         RunEffect(player.transform.position, fx, player);
                         InfoPanel(player, "WrongProfessionCraft");
                         player.ChatMessage(lang.GetMessage("WrongProfessionCraft", this));
                         return false;
                     }
                 }
             }
             return true;
         }*/
        object CanResearchItem(BasePlayer player, Item targetItem)
        {

            if (player)
            {
                if (config.sanitaerList.Contains(targetItem.info.shortname) || config.foresterList.Contains(targetItem.info.shortname) || config.electricianList.Contains(targetItem.info.shortname) || config.weaponengineerList.Contains(targetItem.info.shortname) || config.minerList.Contains(targetItem.info.shortname) || config.oilextractorList.Contains(targetItem.info.shortname) || config.vehicleengineerList.Contains(targetItem.info.shortname) || config.tailorList.Contains(targetItem.info.shortname) || config.graphicdesignerList.Contains(targetItem.info.shortname) || config.doctorList.Contains(targetItem.info.shortname))
                {
                    if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, electrician) && config.electricianList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, oilextractor) && config.oilextractorList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, vehicleengineer) && config.vehicleengineerList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, tailor) && config.tailorList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, graphicdesigner) && config.graphicdesignerList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, doctor) && config.doctorList.Contains(targetItem.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, sanitaer) && config.sanitaerList.Contains(targetItem.info.shortname))
                        return null;
                    else
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "WrongProfessionResearch");
                        player.ChatMessage(lang.GetMessage("WrongProfessionResearch", this));
                        return false;
                    }
                }
            }
            return null;
        }

        object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (config.UseAllowToggleRefinery == true)
            {
                if (permission.UserHasPermission(player.UserIDString, oilextractor) && oven.PrefabName == "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab")
                {
                    return null;
                }
                else if (oven.PrefabName == "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab")
                {
                    RunEffect(player.transform.position, fx, player);
                    InfoPanel(player, "OnlyOilextractorUse");
                    player.ChatMessage(lang.GetMessage("OnlyOilextractorUse", this));
                    return false;
                }
            }
            if (config.UseAllowToggleFurnace == true)
            {
                if (permission.UserHasPermission(player.UserIDString, miner) && oven.PrefabName == "assets/prefabs/deployable/furnace.large/furnace.large.prefab")
                {
                    return null;
                }
                else if (oven.PrefabName == "assets/prefabs/deployable/furnace.large/furnace.large.prefab")
                {
                    RunEffect(player.transform.position, fx, player);
                    InfoPanel(player, "OnlyMinerUse");
                    player.ChatMessage(lang.GetMessage("OnlyMinerUse", this));
                    return false;
                }
            }
            return null;
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner?.GetOwnerPlayer();
            if (player)
            {
                if (config.UseAllowPlaceFurnace == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, miner) && planner.GetItem().info.shortname == "furnace.large")
                    {
                        return null;
                    }
                    else if (planner.GetItem().info.shortname == "furnace.large")
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "OnlyMinerPlace");
                        player.ChatMessage(lang.GetMessage("OnlyMinerPlace", this));
                        return false;
                    }
                }
                if (config.UseAllowPlaceRefinery == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, oilextractor) && planner.GetItem().info.shortname == "small.oil.refinery")
                    {
                        return null;
                    }
                    else if (planner.GetItem().info.shortname == "small.oil.refinery")
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "OnlyOilextractoPlace");
                        player.ChatMessage(lang.GetMessage("OnlyOilextractoPlace", this));
                        return false;
                    }
                }
            }
            return null;
        }
        List<Item> collect = new List<Item>();
        private Plugin owner;

        private ItemDefinition FindItem(string itemNameOrId)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemNameOrId.ToLower());
            if (itemDef == null)
            {
                int itemId;
                if (int.TryParse(itemNameOrId, out itemId))
                {
                    itemDef = ItemManager.FindItemDefinition(itemId);
                }
            }
            return itemDef;
        }

        [Command("diesel")]
        private void ExchangeCommand(IPlayer player, string command, string[] args)
        {

            var bplayer = (BasePlayer)player.Object;
            if (config.UseAllowExchangeDiesel == true)
            {
                if (bplayer)
                {
                    if (permission.UserHasPermission(bplayer.UserIDString, oilextractor))
                    {
                        if (args.Length == 0)
                        {
                            bplayer.ChatMessage(lang.GetMessage("Command", this));
                        }
                        else if (args.Length == 1)
                        {
                            int amount = int.Parse(args[0]);
                            try
                            {
                                if (bplayer.inventory.containerMain.FindItemByItemID(1568388703).amount >= amount)
                                {

                                    bplayer.inventory.containerMain.Take(collect, 1568388703, amount);
                                    int exchangeRate = amount * config.exchangeRate;
                                    bplayer.inventory.containerMain.AddItem(FindItem("lowgradefuel"), exchangeRate, 0);
                                    bplayer.ChatMessage(lang.GetMessage("Received", this) + exchangeRate + lang.GetMessage("LowGradeFuel", this));
                                }
                                else
                                {
                                    bplayer.ChatMessage(lang.GetMessage("NotEnought", this));
                                }
                            }
                            catch
                            {
                                bplayer.ChatMessage(lang.GetMessage("NotEnought", this));
                            }
                        }
                        else
                        {
                            bplayer.ChatMessage(lang.GetMessage("Argument", this));
                        }
                    }
                    else
                    {
                        bplayer.ChatMessage(lang.GetMessage("NoAuthorization", this));
                    }
                }
            }
        }
        object OnItemRepair(BasePlayer player, Item item)
        {
            if (player)
            {
                if (config.foresterList.Contains(item.info.shortname) && config.UseAllowRepairForester == true || config.weaponengineerList.Contains(item.info.shortname) && config.UseAllowRepairWeaponengineer == true || config.minerList.Contains(item.info.shortname) && config.UseAllowRepairMiner == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(item.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(item.info.shortname)
                    || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(item.info.shortname))
                        return null;
                    else
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "WrongProfessionRepair");
                        player.ChatMessage(lang.GetMessage("WrongProfessionRepair", this));
                        return false;
                    }
                }
            }
            return null;
        }
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (config.UseAllowRepairVehicle == true)
            {
                if (player)
                {
                    if (config.vehicleengineerPrefabList.Contains(info.HitEntity.ShortPrefabName))
                    {
                        if (permission.UserHasPermission(player.UserIDString, vehicleengineer))
                        {
                            return null;
                        }
                        else
                        {
                            player.ChatMessage(lang.GetMessage("OnlyVehicleengineerRepair", this));
                            return false;
                        }
                    }
                }
            }
            return null;
        }



        object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {

            if (player)
            {

                if (config.sanitaerList.Contains(node.itemDef.shortname) || config.foresterList.Contains(node.itemDef.shortname) || config.electricianList.Contains(node.itemDef.shortname) || config.weaponengineerList.Contains(node.itemDef.shortname) || config.minerList.Contains(node.itemDef.shortname) || config.oilextractorList.Contains(node.itemDef.shortname) || config.vehicleengineerList.Contains(node.itemDef.shortname) || config.tailorList.Contains(node.itemDef.shortname) || config.graphicdesignerList.Contains(node.itemDef.shortname) || config.doctorList.Contains(node.itemDef.shortname))
                {
                    if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, electrician) && config.electricianList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, oilextractor) && config.oilextractorList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, vehicleengineer) && config.vehicleengineerList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, tailor) && config.tailorList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, graphicdesigner) && config.graphicdesignerList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, doctor) && config.doctorList.Contains(node.itemDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, sanitaer) && config.sanitaerList.Contains(node.itemDef.shortname))
                    {

                        return true;
                    }
                    else
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "WrongProfessionResearch");
                        player.ChatMessage(lang.GetMessage("WrongProfessionResearch", this));
                        return false;
                    }

                }


            }
            return true;
        }
        object OnItemAction(Item item, string action, BasePlayer player)
        {

            if (player && action == "study")
            {
                if (config.sanitaerList.Contains(item.blueprintTargetDef.shortname) || config.foresterList.Contains(item.blueprintTargetDef.shortname) || config.electricianList.Contains(item.blueprintTargetDef.shortname) || config.weaponengineerList.Contains(item.blueprintTargetDef.shortname) || config.minerList.Contains(item.blueprintTargetDef.shortname) || config.oilextractorList.Contains(item.blueprintTargetDef.shortname) || config.vehicleengineerList.Contains(item.blueprintTargetDef.shortname) || config.tailorList.Contains(item.blueprintTargetDef.shortname) || config.graphicdesignerList.Contains(item.blueprintTargetDef.shortname) || config.doctorList.Contains(item.blueprintTargetDef.shortname))
                {
                    if (permission.UserHasPermission(player.UserIDString, forester) && config.foresterList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, electrician) && config.electricianList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, weaponengineer) && config.weaponengineerList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, miner) && config.minerList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, oilextractor) && config.oilextractorList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, vehicleengineer) && config.vehicleengineerList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, tailor) && config.tailorList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, graphicdesigner) && config.graphicdesignerList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, doctor) && config.doctorList.Contains(item.blueprintTargetDef.shortname)
                    || permission.UserHasPermission(player.UserIDString, sanitaer) && config.sanitaerList.Contains(item.blueprintTargetDef.shortname))
                        return null;
                    else
                    {
                        RunEffect(player.transform.position, fx, player);
                        InfoPanel(player, "WrongProfessionResearch");
                        player.ChatMessage(lang.GetMessage("WrongProfessionResearch", this));
                        return false;
                    }
                }
            }
            return null;
        }
        private static void RunEffect(Vector3 position, string prefab, BasePlayer player = null)
        {
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, position, Vector3.zero);
            effect.pooledString = prefab;

            if (player != null)
            {
                EffectNetwork.Send(effect, player.net.connection);
            }
            else
            {
                EffectNetwork.Send(effect);
            }
        }

        [Command("profession")]
        private void selectProfession(IPlayer player, string command, string[] args)
        {
            if (config.UseProfessionSelfeGrand == true)
            {
                var notfound = true;
                var bplayer = (BasePlayer)player.Object;
                if (args.Length == 0)
                {
                    var profList = string.Join("\n", professionList.ToArray());
                    player.Reply("Professions available!\n--------------------\n" + profList + "\n--------------------");
                }
                else if (args.Length == 1)
                {
                    foreach (var data in permissionList)
                    {
                        if (data.Key == args[0].ToLower())
                        {
                            var val = data.Value;
                            notfound = false;
                            if (permission.UserHasPermission(bplayer.UserIDString, forester)
                                || permission.UserHasPermission(bplayer.UserIDString, electrician)
                                || permission.UserHasPermission(bplayer.UserIDString, weaponengineer)
                                || permission.UserHasPermission(bplayer.UserIDString, miner)
                                || permission.UserHasPermission(bplayer.UserIDString, oilextractor)
                                || permission.UserHasPermission(bplayer.UserIDString, vehicleengineer)
                                || permission.UserHasPermission(bplayer.UserIDString, tailor)
                                || permission.UserHasPermission(bplayer.UserIDString, graphicdesigner)
                                || permission.UserHasPermission(bplayer.UserIDString, doctor)
                                || permission.UserHasPermission(bplayer.UserIDString, farmer)
                                || permission.UserHasPermission(bplayer.UserIDString, sanitaer))
                            {
                                bplayer.ChatMessage("Du hast bereits einen Beruf!");
                            }
                            else
                            {
                                permission.GrantUserPermission(bplayer.UserIDString, val, owner);
                                bplayer.ChatMessage("Professsion " + args[0] + " granted");
                            }
                        }


                    }
                    if (notfound)
                    {
                        bplayer.ChatMessage("Profession not found!");
                    }
                }
                else
                {
                    bplayer.ChatMessage("Wrong command!");
                }
            }

        }


        #endregion
    }
}

