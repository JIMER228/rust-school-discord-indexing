        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Globalization;
        using System.Linq;
        using Network;
        using Newtonsoft.Json;
        using Oxide.Core;
        using Oxide.Core.Configuration;
        using Oxide.Core.Plugins;
        using Oxide.Game.Rust.Cui;
        using Rust;
        using UnityEngine;

        namespace Oxide.Plugins
        {
            [Info("StudyTree", "Ryamkk", "1.0.9")]
            public class StudyTree : RustPlugin
            {
                private static StudyTree Instance;
                private GameObject FileManagerObject;
                private FileManager m_FileManager;

                private bool ImageInit = false;
                
                private bool WorkbenchLevelOneUpdate;
                private bool WorkbenchLevelTwoUpdate;
                private bool WorkbenchLevelThreeUpdate;

                [JsonProperty("Основной слой первого верстака")]
                private string LayerWorkbenchLevelOne = "StudyTree.WorkbenchLevelOne";

                [JsonProperty("Основной слой второго верстака")]
                private string LayerWorkbenchLevelTwo = "StudyTree.WorkbenchLevelTwo";

                [JsonProperty("Основной слой третьего верстака")]
                private string LayerWorkbenchLevelThree = "StudyTree.WorkbenchLevelThree";

                [JsonProperty("Цвет заднего фона")] 
                public string BackgroundСolor = "#212121";
                
                [JsonProperty("Цвет фона картинки")] 
                public string BackgroundColorClipArt = "#1E1D1C";

                [JsonProperty("Цвет заблокированного предмета")]
                public string LockedItemColor = "#4D4D4D";

                [JsonProperty("Цвет разблокированного предмета")]
                public string UnlockedItemColor = "#718a43";
                
                #region Задаём цену изучения для shortname

                [JsonProperty("Названия предмета и цена изучения на третьем верстаке")]
                public Dictionary<string, int> StudyItemsList = new Dictionary<string, int>()
                {
                    ["hat.miner"] = 20,
                    ["tshirt"] = 20,
                    ["shirt.collared"] = 75,
                    ["tshirt.long"] = 20,
                    ["pants"] = 75,
                    ["riot.helmet"] = 75,
                    ["bucket.helmet"] = 75,
                    ["jacket.snow"] = 20,
                    ["jacket"] = 20,
                    ["hammer.salvaged"] = 75,
                    ["salvaged.cleaver"] = 75,
                    ["mace"] = 75,
                    ["trap.bear"] = 75,
                    ["flameturret"] = 75,
                    ["guntrap"] = 75,
                    ["ammo.pistol"] = 75,
                    ["weapon.mod.silencer"] = 75,
                    ["shotgun.waterpipe"] = 75,
                    ["pistol.revolver"] = 75,
                    ["weapon.mod.flashlight"] = 75,
                    ["shotgun.double"] = 125,
                    ["explosive.satchel"] = 125,
                    ["grenade.beancan"] = 75,
                    ["barricade.woodwire"] = 75,
                    ["water.catcher.small"] = 20,
                    ["shutter.metal.embrasure.b"] = 20,
                    ["shutter.metal.embrasure.a"] = 20,
                    ["floor.grill"] = 75,
                    ["wall.window.bars.metal"] = 75,
                    ["watchtower.wood"] = 75,
                    ["ladder.wooden.wall"] = 125,
                    ["bed"] = 75,
                    ["dropbox"] = 75,
                    ["wall.external.high"] = 125,
                    ["gates.external.high.wood"] = 125,
                    ["wall.frame.garagedoor"] = 75,

                    ["hazmatsuit"] = 125,
                    ["coffeecan.helmet"] = 75,
                    ["shoes.boots"] = 75,
                    ["syringe.medical"] = 75,
                    ["roadsign.jacket"] = 75,
                    ["hoodie"] = 75,
                    ["largemedkit"] = 75,
                    ["roadsign.kilt"] = 75,
                    ["roadsign.gloves"] = 20,
                    ["heavy.plate.helmet"] = 125,
                    ["heavy.plate.jacket"] = 125,
                    ["heavy.plate.pants"] = 125,
                    ["longsword"] = 75,
                    ["axe.salvaged"] = 125,
                    ["icepick.salvaged"] = 125,
                    ["chainsaw"] = 125,
                    ["flamethrower"] = 125,
                    ["trap.landmine"] = 125,
                    ["ammo.shotgun"] = 75,
                    ["ammo.shotgun.slug"] = 75,
                    ["shotgun.pump"] = 125,
                    ["ammo.shotgun.fire"] = 75,
                    ["pistol.semiauto"] = 125,
                    ["grenade.f1"] = 75,
                    ["weapon.mod.muzzlebrake"] = 125,
                    ["weapon.mod.muzzleboost"] = 125,
                    ["pistol.python"] = 125,
                    ["ammo.pistol.fire"] = 125,
                    ["ammo.pistol.hv"] = 125,
                    ["smg.thompson"] = 125,
                    ["smg.2"] = 125,
                    ["rifle.semiauto"] = 125,
                    ["ammo.rifle"] = 125,
                    ["ammo.rocket.fire"] = 125,
                    ["water.catcher.large"] = 75,
                    ["wall.frame.cell.gate"] = 75,
                    ["barricade.concrete"] = 20,
                    ["wall.frame.cell"] = 75,
                    ["barricade.metal"] = 125,
                    ["locker"] = 75,
                    ["wall.external.high.stone"] = 500,
                    ["small.oil.refinery"] = 75,
                    ["floor.ladder.hatch"] = 125,
                    ["furnace.large"] = 125,
                    ["generator.wind.scrap"] = 125,
                    ["autoturret"] = 500,

                    ["wall.window.bars.toptier"] = 125,
                    ["door.hinged.toptier"] = 500,
                    ["door.double.hinged.toptier"] = 500,
                    ["gates.external.high.stone"] = 500,
                    ["metal.facemask"] = 500,
                    ["metal.plate.torso"] = 500,
                    ["weapon.mod.lasersight"] = 125,
                    ["ammo.rifle.hv"] = 125,
                    ["ammo.rifle.incendiary"] = 125,
                    ["smg.mp5"] = 500,
                    ["weapon.mod.holosight"] = 125,
                    ["weapon.mod.small.scope"] = 125,
                    ["ammo.rifle.explosive"] = 125,
                    ["rifle.ak"] = 500,
                    ["rifle.bolt"] = 500,
                    ["rocket.launcher"] = 500,
                    ["ammo.rocket.hv"] = 125,
                    ["explosives"] = 500,
                    ["explosive.timed"] = 500,
                    ["ammo.rocket.basic"] = 125
                };

                #endregion

                #region Задаём картинку для shortname

                [JsonProperty("Картинки для графического интерфейса плагина")]
                private Dictionary<string, string> StudyImageList = new Dictionary<string, string>()
                {
                    ["hat.miner"] = "https://rustlabs.com/img/items180/hat.miner.png",
                    ["tshirt"] = "https://rustlabs.com/img/items180/tshirt.png",
                    ["shirt.collared"] = "https://rustlabs.com/img/items180/shirt.collared.png",
                    ["tshirt.long"] = "https://rustlabs.com/img/items180/tshirt.long.png",
                    ["pants"] = "https://rustlabs.com/img/items180/pants.png",
                    ["riot.helmet"] = "https://rustlabs.com/img/items180/riot.helmet.png",
                    ["bucket.helmet"] = "https://rustlabs.com/img/items180/bucket.helmet.png",
                    ["jacket.snow"] = "https://rustlabs.com/img/items180/jacket.snow.png",
                    ["jacket"] = "https://rustlabs.com/img/items180/jacket.png",
                    ["hammer.salvaged"] = "https://rustlabs.com/img/items180/hammer.salvaged.png",
                    ["salvaged.cleaver"] = "https://rustlabs.com/img/items180/salvaged.cleaver.png",
                    ["mace"] = "https://rustlabs.com/img/items180/mace.png",
                    ["trap.bear"] = "https://rustlabs.com/img/items180/trap.bear.png",
                    ["flameturret"] = "https://rustlabs.com/img/items180/flameturret.png",
                    ["guntrap"] = "https://rustlabs.com/img/items180/guntrap.png",
                    ["ammo.pistol"] = "https://rustlabs.com/img/items180/ammo.pistol.png",
                    ["weapon.mod.silencer"] = "https://rustlabs.com/img/items180/weapon.mod.silencer.png",
                    ["shotgun.waterpipe"] = "https://rustlabs.com/img/items180/shotgun.waterpipe.png",
                    ["pistol.revolver"] = "https://rustlabs.com/img/items180/pistol.revolver.png",
                    ["weapon.mod.flashlight"] = "https://rustlabs.com/img/items180/weapon.mod.flashlight.png",
                    ["shotgun.double"] = "https://rustlabs.com/img/items180/shotgun.double.png",
                    ["explosive.satchel"] = "https://rustlabs.com/img/items180/explosive.satchel.png",
                    ["grenade.beancan"] = "https://rustlabs.com/img/items180/grenade.beancan.png",
                    ["barricade.woodwire"] = "https://rustlabs.com/img/items180/barricade.woodwire.png",
                    ["water.catcher.small"] = "https://rustlabs.com/img/items180/water.catcher.small.png",
                    ["shutter.metal.embrasure.b"] = "https://rustlabs.com/img/items180/shutter.metal.embrasure.b.png",
                    ["shutter.metal.embrasure.a"] = "https://rustlabs.com/img/items180/shutter.metal.embrasure.a.png",
                    ["floor.grill"] = "https://rustlabs.com/img/items180/floor.grill.png",
                    ["wall.window.bars.metal"] = "https://rustlabs.com/img/items180/wall.window.bars.metal.png",
                    ["watchtower.wood"] = "https://rustlabs.com/img/items180/watchtower.wood.png",
                    ["ladder.wooden.wall"] = "https://rustlabs.com/img/items180/ladder.wooden.wall.png",
                    ["bed"] = "https://rustlabs.com/img/items180/bed.png",
                    ["dropbox"] = "https://rustlabs.com/img/items180/dropbox.png",
                    ["wall.external.high"] = "https://rustlabs.com/img/items180/wall.external.high.png",
                    ["gates.external.high.wood"] = "https://rustlabs.com/img/items180/gates.external.high.wood.png",
                    ["wall.frame.garagedoor"] = "https://rustlabs.com/img/items180/wall.frame.garagedoor.png",

                    ["hazmatsuit"] = "https://rustlabs.com/img/items180/hazmatsuit.png",
                    ["coffeecan.helmet"] = "https://rustlabs.com/img/items180/coffeecan.helmet.png",
                    ["shoes.boots"] = "https://rustlabs.com/img/items180/shoes.boots.png",
                    ["syringe.medical"] = "https://rustlabs.com/img/items180/syringe.medical.png",
                    ["roadsign.jacket"] = "https://rustlabs.com/img/items180/roadsign.jacket.png",
                    ["hoodie"] = "https://rustlabs.com/img/items180/hoodie.png",
                    ["largemedkit"] = "https://rustlabs.com/img/items180/largemedkit.png",
                    ["roadsign.kilt"] = "https://rustlabs.com/img/items180/roadsign.kilt.png",
                    ["roadsign.gloves"] = "https://rustlabs.com/img/items180/roadsign.gloves.png",
                    ["heavy.plate.helmet"] = "https://rustlabs.com/img/items180/heavy.plate.helmet.png",
                    ["heavy.plate.jacket"] = "https://rustlabs.com/img/items180/heavy.plate.jacket.png",
                    ["heavy.plate.pants"] = "https://rustlabs.com/img/items180/heavy.plate.pants.png",
                    ["longsword"] = "https://rustlabs.com/img/items180/longsword.png",
                    ["axe.salvaged"] = "https://rustlabs.com/img/items180/axe.salvaged.png",
                    ["icepick.salvaged"] = "https://rustlabs.com/img/items180/icepick.salvaged.png",
                    ["chainsaw"] = "https://rustlabs.com/img/items180/chainsaw.png",
                    ["flamethrower"] = "https://rustlabs.com/img/items180/flamethrower.png",
                    ["trap.landmine"] = "https://rustlabs.com/img/items180/trap.landmine.png",
                    ["ammo.shotgun"] = "https://rustlabs.com/img/items180/ammo.shotgun.png",
                    ["ammo.shotgun.slug"] = "https://rustlabs.com/img/items180/ammo.shotgun.slug.png",
                    ["shotgun.pump"] = "https://rustlabs.com/img/items180/shotgun.pump.png",
                    ["ammo.shotgun.fire"] = "https://rustlabs.com/img/items180/ammo.shotgun.fire.png",
                    ["pistol.semiauto"] = "https://rustlabs.com/img/items180/pistol.semiauto.png",
                    ["grenade.f1"] = "https://rustlabs.com/img/items180/grenade.f1.png",
                    ["weapon.mod.muzzlebrake"] = "https://rustlabs.com/img/items180/weapon.mod.muzzlebrake.png",
                    ["weapon.mod.muzzleboost"] = "https://rustlabs.com/img/items180/weapon.mod.muzzleboost.png",
                    ["pistol.python"] = "https://rustlabs.com/img/items180/pistol.python.png",
                    ["ammo.pistol.fire"] = "https://rustlabs.com/img/items180/ammo.pistol.fire.png",
                    ["ammo.pistol.hv"] = "https://rustlabs.com/img/items180/ammo.pistol.hv.png",
                    ["smg.thompson"] = "https://rustlabs.com/img/items180/smg.thompson.png",
                    ["smg.2"] = "https://rustlabs.com/img/items180/smg.2.png",
                    ["rifle.semiauto"] = "https://rustlabs.com/img/items180/rifle.semiauto.png",
                    ["ammo.rifle"] = "https://rustlabs.com/img/items180/ammo.rifle.png",
                    ["ammo.rocket.fire"] = "https://rustlabs.com/img/items180/ammo.rocket.fire.png",
                    ["water.catcher.large"] = "https://rustlabs.com/img/items180/water.catcher.large.png",
                    ["wall.frame.cell.gate"] = "https://rustlabs.com/img/items180/wall.frame.cell.gate.png",
                    ["barricade.concrete"] = "https://rustlabs.com/img/items180/barricade.concrete.png",
                    ["wall.frame.cell"] = "https://rustlabs.com/img/items180/wall.frame.cell.png",
                    ["barricade.metal"] = "https://rustlabs.com/img/items180/barricade.metal.png",
                    ["locker"] = "https://rustlabs.com/img/items180/locker.png",
                    ["wall.external.high.stone"] = "https://rustlabs.com/img/items180/wall.external.high.stone.png",
                    ["small.oil.refinery"] = "https://rustlabs.com/img/items180/small.oil.refinery.png",
                    ["floor.ladder.hatch"] = "https://rustlabs.com/img/items180/floor.ladder.hatch.png",
                    ["furnace.large"] = "https://rustlabs.com/img/items180/furnace.large.png",
                    ["generator.wind.scrap"] = "https://rustlabs.com/img/items180/generator.wind.scrap.png",
                    ["autoturret"] = "https://rustlabs.com/img/items180/autoturret.png",

                    ["wall.window.bars.toptier"] = "https://rustlabs.com/img/items180/wall.window.bars.toptier.png",
                    ["door.hinged.toptier"] = "https://rustlabs.com/img/items180/door.hinged.toptier.png",
                    ["door.double.hinged.toptier"] = "https://rustlabs.com/img/items180/door.double.hinged.toptier.png",
                    ["gates.external.high.stone"] = "https://rustlabs.com/img/items180/gates.external.high.stone.png",
                    ["metal.facemask"] = "https://rustlabs.com/img/items180/metal.facemask.png",
                    ["metal.plate.torso"] = "https://rustlabs.com/img/items180/metal.plate.torso.png",
                    ["weapon.mod.lasersight"] = "https://rustlabs.com/img/items180/weapon.mod.lasersight.png",
                    ["ammo.rifle.hv"] = "https://rustlabs.com/img/items180/ammo.rifle.hv.png",
                    ["ammo.rifle.incendiary"] = "https://rustlabs.com/img/items180/ammo.rifle.incendiary.png",
                    ["smg.mp5"] = "https://rustlabs.com/img/items180/smg.mp5.png",
                    ["weapon.mod.holosight"] = "https://rustlabs.com/img/items180/weapon.mod.holosight.png",
                    ["weapon.mod.small.scope"] = "https://rustlabs.com/img/items180/weapon.mod.small.scope.png",
                    ["ammo.rifle.explosive"] = "https://rustlabs.com/img/items180/ammo.rifle.explosive.png",
                    ["rifle.ak"] = "https://rustlabs.com/img/items180/rifle.ak.png",
                    ["rifle.bolt"] = "https://rustlabs.com/img/items180/rifle.bolt.png",
                    ["rocket.launcher"] = "https://rustlabs.com/img/items180/rocket.launcher.png",
                    ["ammo.rocket.hv"] = "https://rustlabs.com/img/items180/ammo.rocket.hv.png",
                    ["explosives"] = "https://rustlabs.com/img/items180/explosives.png",
                    ["explosive.timed"] = "https://rustlabs.com/img/items180/explosive.timed.png",
                    ["ammo.rocket.basic"] = "https://rustlabs.com/img/items180/ammo.rocket.basic.png",
                    
                    ["scrap"] = "https://rustlabs.com/img/items180/scrap.png",
                    ["close"] = "https://cdn.discordapp.com/attachments/970815614417506305/978148719855796234/close.png",
                    ["unlock"] = "https://cdn.discordapp.com/attachments/970815614417506305/978151657907036190/unlock.png"
                };

                #endregion

                #region Создание и инициализация дата файла на игрока

                [JsonProperty("Дата файл изучений игроков")]
                public Dictionary<ulong, StudyTreeData> StudyTreeUser = new Dictionary<ulong, StudyTreeData>();

                public class StudyTreeData
                {
                    [JsonProperty("Beancan Grenade")] public bool BeancanGrenade;
                    [JsonProperty("Gas Mask")] public bool GasMask;
                    [JsonProperty("Snap Trap")] public bool SnapTrap;
                    [JsonProperty("Shotgun Trap")] public bool ShotgunTrap;
                    [JsonProperty("Flame Turret")] public bool FlameTurret;
                    [JsonProperty("Pistol Bullet")] public bool PistolBullet;
                    [JsonProperty("Fire Arrow")] public bool FireArrow;
                    [JsonProperty("Silencer")] public bool Silencer;
                    [JsonProperty("Weapon Flashlight")] public bool WeaponFlashlight;
                    [JsonProperty("Waterpipe Shotgun")] public bool WaterpipeShotgun;

                    [JsonProperty("Double Barrel Shotgun")]
                    public bool DoubleBarrelShotgun;

                    [JsonProperty("Salvaged Cleaver")] public bool SalvagedCleaver;
                    [JsonProperty("Mace")] public bool Mace;
                    [JsonProperty("Revolver")] public bool Revolver;
                    [JsonProperty("Salvaged Sword")] public bool SalvagedSword;
                    [JsonProperty("Pickaxe")] public bool Pickaxe;
                    [JsonProperty("Satchel Charge")] public bool SatchelCharge;
                    [JsonProperty("Hatchet")] public bool Hatchet;
                    [JsonProperty("Salvaged Hammer")] public bool SalvagedHammer;
                    [JsonProperty("Binoculars")] public bool Binoculars;
                    [JsonProperty("RF Transmitter")] public bool RFTransmitter;
                    [JsonProperty("Snow Jacket")] public bool SnowJacket;
                    [JsonProperty("Riot Helmet")] public bool RiotHelmet;
                    [JsonProperty("Tank Top")] public bool TankTop;
                    [JsonProperty("Shirt")] public bool Shirt;
                    [JsonProperty("Jacket")] public bool Jacket;
                    [JsonProperty("Miners Hat")] public bool MinersHat;
                    [JsonProperty("Bucket Helmet")] public bool BucketHelmet;
                    [JsonProperty("Longsleeve T-Shirt")] public bool LongsleeveTShirt;
                    [JsonProperty("Pants")] public bool Pants;
                    [JsonProperty("T-Shirt")] public bool TShirt;
                    [JsonProperty("Reactive Target")] public bool ReactiveTarget;
                    [JsonProperty("Fridge")] public bool Fridge;
                    [JsonProperty("Drop Box")] public bool DropBox;
                    [JsonProperty("Chair")] public bool Chair;
                    [JsonProperty("Ceiling Light")] public bool CeilingLight;
                    [JsonProperty("Bed")] public bool Bed;
                    [JsonProperty("Wooden Ladder")] public bool WoodenLadder;
                    [JsonProperty("Small Water Catcher")] public bool SmallWaterCatcher;

                    [JsonProperty("High External Wooden Wall")]
                    public bool HighExternalWoodenWall;

                    [JsonProperty("High External Wooden Gate")]
                    public bool HighExternalWoodenGate;

                    [JsonProperty("Barbed Wooden Barricade")]
                    public bool BarbedWoodenBarricade;

                    [JsonProperty("Reinforced Glass Window")]
                    public bool ReinforcedGlassWindow;

                    [JsonProperty("Metal Window Bars")] public bool MetalWindowBars;

                    [JsonProperty("Metal Vertical Embrasure")]
                    public bool MetalVerticalEmbrasure;

                    [JsonProperty("Metal horizontal Embrasure")]
                    public bool MetalHorizontalEmbrasure;

                    [JsonProperty("Sandbag Barricade")] public bool SandbagBarricade;
                    [JsonProperty("Watch Tower")] public bool WatchTower;
                    [JsonProperty("Garage Door")] public bool GarageDoor;
                    [JsonProperty("Chainlink Fence")] public bool ChainlinkFence;
                    [JsonProperty("Floor Grill")] public bool FloorGrill;
                    [JsonProperty("Chainsaw")] public bool Chainsaw;
                    [JsonProperty("Auto Turret")] public bool AutoTurret;
                    [JsonProperty("12 Gauge Slug")] public bool Gauge12Slug;

                    [JsonProperty("12 Gauge Incendiary Shell")]
                    public bool Gauge12IncendiaryShell;

                    [JsonProperty("Incendiary Rocket")] public bool IncendiaryRocket;
                    [JsonProperty("HV Pistol Ammo")] public bool HVPistolAmmo;

                    [JsonProperty("Incendiary Pistol Ammo")]
                    public bool IncendiaryPistolAmmo;

                    [JsonProperty("SAM Ammo")] public bool SAMAmmo;
                    [JsonProperty("12 Gauge Buckshot")] public bool Gauge12Buckshot;
                    [JsonProperty("5.56 Rifle Ammo")] public bool Rifle556Ammo;
                    [JsonProperty("Custom SMG")] public bool CustomSMG;
                    [JsonProperty("Muzzle Brake")] public bool MuzzleBrake;
                    [JsonProperty("Muzzle Boost")] public bool MuzzleBoost;
                    [JsonProperty("Thompson")] public bool Thompson;
                    [JsonProperty("Pump Shotgun")] public bool PumpShotgun;
                    [JsonProperty("Semi-Automatic Rifle")] public bool SemiAutomaticRifle;

                    [JsonProperty("Semi-Automatic Pistol")]
                    public bool SemiAutomaticPistol;

                    [JsonProperty("F1 Grenade")] public bool F1Grenade;
                    [JsonProperty("Longsword")] public bool Longsword;
                    [JsonProperty("Flame Thrower")] public bool FlameThrower;
                    [JsonProperty("Python Revolver")] public bool PythonRevolver;
                    [JsonProperty("Large Medkit")] public bool LargeMedkit;
                    [JsonProperty("Medical Syringe")] public bool MedicalSyringe;
                    [JsonProperty("Salvaged Icepick")] public bool SalvagedIcepick;
                    [JsonProperty("Сhainsaw")] public bool Сhainsaw;
                    [JsonProperty("Salveged Axe")] public bool SalvegedAxe;
                    [JsonProperty("Road Sing Jacket")] public bool RoadSingJacket;
                    [JsonProperty("Road Sing Kilt")] public bool RoadSingKilt;
                    [JsonProperty("Coffee Can Helmet")] public bool CoffeeCanHelmet;
                    [JsonProperty("Boots")] public bool Boots;
                    [JsonProperty("Roadsing Gloves")] public bool RoadsingGloves;
                    [JsonProperty("Hoodie")] public bool Hoodie;
                    [JsonProperty("Heavy Plate Pants")] public bool HeavyPlatePants;
                    [JsonProperty("Heavy Plate Jacket")] public bool HeavyPlateJacket;
                    [JsonProperty("Hazmat Suit")] public bool HazmatSuit;
                    [JsonProperty("Heavy Plate Helmet")] public bool HeavyPlateHelmet;
                    [JsonProperty("Search Light")] public bool SearchLight;
                    [JsonProperty("Wind Turbine")] public bool WindTurbine;
                    [JsonProperty("Small Oil Refinery")] public bool SmallOilRefinery;
                    [JsonProperty("Locker")] public bool Locker;
                    [JsonProperty("Large Furnace")] public bool LargeFurnace;

                    [JsonProperty("High External Stone Wall")]
                    public bool HighExternalStoneWall;

                    [JsonProperty("Metal Barricade")] public bool MetalBarricade;
                    [JsonProperty("Large Water Catcher")] public bool LargeWaterCatcher;
                    [JsonProperty("Prison Cell Wall")] public bool PrisonCellWall;
                    [JsonProperty("Prison Cell Gate")] public bool PrisonCellGate;
                    [JsonProperty("Concrete Barricade")] public bool ConcreteBarricade;
                    [JsonProperty("Ladder Hatch")] public bool LadderHatch;
                    [JsonProperty("Land Mine")] public bool LandMine;
                    [JsonProperty("RF Pager")] public bool RFPager;
                    [JsonProperty("RF Receiver")] public bool RFReceiver;

                    [JsonProperty("Large Rechargable Battery")]
                    public bool LargeRechargableBattery;

                    [JsonProperty("HBHF Sensor")] public bool HBHFSensor;
                    [JsonProperty("RF Broadcaster")] public bool RFBroadcaster;

                    [JsonProperty("Incendiary 5.56 Rifle Ammo")]
                    public bool Incendiary556RifleAmmo;

                    [JsonProperty("HV 5.56 Rifle Ammo")] public bool HV556RifleAmmo;
                    [JsonProperty("High Velocity Rocket")] public bool HighVelocityRocket;
                    [JsonProperty("Rocket")] public bool Rocket;

                    [JsonProperty("Timed Explosive Charge")]
                    public bool TimedExplosiveCharge;

                    [JsonProperty("Metal Chest Plate")] public bool MetalChestPlate;
                    [JsonProperty("Metal Facemask")] public bool MetalFacemask;
                    [JsonProperty("Explosives")] public bool Explosives;

                    [JsonProperty("High External Stone Gate")]
                    public bool HighExternalStoneGate;

                    [JsonProperty("Armored Door")] public bool ArmoredDoor;
                    [JsonProperty("Armored Double Door")] public bool ArmoredDoubleDoor;

                    [JsonProperty("Reinforced Window Bars")]
                    public bool ReinforcedWindowBars;

                    [JsonProperty("4x Zoom Scope")] public bool Zoom4xScope;
                    [JsonProperty("Rocket Launcher")] public bool RocketLauncher;
                    [JsonProperty("Weapon Lasersight")] public bool WeaponLasersight;
                    [JsonProperty("Holosight")] public bool Holosight;
                    [JsonProperty("Bolt Action Rifle")] public bool BoltActionRifle;
                    [JsonProperty("Assault Rifle")] public bool AssaultRifle;
                    [JsonProperty("MP5A4")] public bool MP5A4;

                    [JsonProperty("Explosive 5.56 Rifle Ammo")]
                    public bool Explosive556RifleAmmo;
                }

                void ReadData()
                {
                    StudyTreeUser =
                        Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, StudyTreeData>>("StudyTree/StudyTreeData");
                }

                void WriteData()
                {
                    Interface.Oxide.DataFileSystem.WriteObject("StudyTree/StudyTreeData", StudyTreeUser);
                }

                void RegisteredDataUser(BasePlayer player)
                {
                    if (!StudyTreeUser.ContainsKey(player.userID))
                    {
                        StudyTreeUser.Add(player.userID, new StudyTreeData
                        {
                            BeancanGrenade = false,
                            GasMask = false,
                            SnapTrap = false,
                            ShotgunTrap = false,
                            FlameTurret = false,
                            PistolBullet = false,
                            FireArrow = false,
                            Silencer = false,
                            WeaponFlashlight = false,
                            WaterpipeShotgun = false,
                            DoubleBarrelShotgun = false,
                            SalvagedCleaver = false,
                            Mace = false,
                            Revolver = false,
                            SalvagedSword = false,
                            Pickaxe = false,
                            SatchelCharge = false,
                            Hatchet = false,
                            SalvagedHammer = false,
                            Binoculars = false,
                            RFTransmitter = false,
                            SnowJacket = false,
                            RiotHelmet = false,
                            TankTop = false,
                            Shirt = false,
                            Jacket = false,
                            MinersHat = false,
                            BucketHelmet = false,
                            LongsleeveTShirt = false,
                            Pants = false,
                            TShirt = false,
                            ReactiveTarget = false,
                            Fridge = false,
                            DropBox = false,
                            Chair = false,
                            CeilingLight = false,
                            Bed = false,
                            WoodenLadder = false,
                            SmallWaterCatcher = false,
                            HighExternalWoodenWall = false,
                            HighExternalWoodenGate = false,
                            BarbedWoodenBarricade = false,
                            ReinforcedGlassWindow = false,
                            MetalWindowBars = false,
                            MetalVerticalEmbrasure = false,
                            MetalHorizontalEmbrasure = false,
                            SandbagBarricade = false,
                            WatchTower = false,
                            GarageDoor = false,
                            ChainlinkFence = false,
                            FloorGrill = false,

                            Chainsaw = false,
                            AutoTurret = false,
                            Gauge12Slug = false,
                            Gauge12IncendiaryShell = false,
                            IncendiaryRocket = false,
                            HVPistolAmmo = false,
                            IncendiaryPistolAmmo = false,
                            SAMAmmo = false,
                            Gauge12Buckshot = false,
                            Rifle556Ammo = false,
                            CustomSMG = false,
                            MuzzleBrake = false,
                            MuzzleBoost = false,
                            Thompson = false,
                            PumpShotgun = false,
                            SemiAutomaticRifle = false,
                            SemiAutomaticPistol = false,
                            F1Grenade = false,
                            Longsword = false,
                            FlameThrower = false,
                            PythonRevolver = false,
                            LargeMedkit = false,
                            MedicalSyringe = false,
                            SalvagedIcepick = false,
                            Сhainsaw = false,
                            SalvegedAxe = false,
                            RoadSingJacket = false,
                            RoadSingKilt = false,
                            CoffeeCanHelmet = false,
                            Boots = false,
                            RoadsingGloves = false,
                            Hoodie = false,
                            HeavyPlatePants = false,
                            HeavyPlateJacket = false,
                            HazmatSuit = false,
                            HeavyPlateHelmet = false,
                            SearchLight = false,
                            WindTurbine = false,
                            SmallOilRefinery = false,
                            Locker = false,
                            LargeFurnace = false,
                            HighExternalStoneWall = false,
                            MetalBarricade = false,
                            LargeWaterCatcher = false,
                            PrisonCellWall = false,
                            PrisonCellGate = false,
                            ConcreteBarricade = false,
                            LadderHatch = false,
                            LandMine = false,
                            RFPager = false,
                            RFReceiver = false,
                            LargeRechargableBattery = false,
                            HBHFSensor = false,
                            RFBroadcaster = false,

                            Incendiary556RifleAmmo = false,
                            HV556RifleAmmo = false,
                            HighVelocityRocket = false,
                            Rocket = false,
                            TimedExplosiveCharge = false,
                            MetalChestPlate = false,
                            MetalFacemask = false,
                            Explosives = false,
                            HighExternalStoneGate = false,
                            ArmoredDoor = false,
                            ArmoredDoubleDoor = false,
                            ReinforcedWindowBars = false,
                            Zoom4xScope = false,
                            RocketLauncher = false,
                            WeaponLasersight = false,
                            Holosight = false,
                            BoltActionRifle = false,
                            AssaultRifle = false,
                            MP5A4 = false,
                            Explosive556RifleAmmo = false
                        });
                    }
                }

                #endregion

                #region Хуки и вспомогательные классы для работы плагина

                void InitFileManager()
                {
                    FileManagerObject = new GameObject("StudyTree_FileManagerObject");
                    m_FileManager = FileManagerObject.AddComponent<FileManager>();
                }

                private void OnServerInitialized()
                {
                    Instance = this;

                    ReadData();
                    InitFileManager();
                    ServerMgr.Instance.StartCoroutine(LoadImages());

                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        RegisteredDataUser(player);
                    }
                }

                IEnumerator LoadImages()
                {
                    int i = 0;
                    int lastpercent = -1;

                    foreach (var name in StudyImageList.Keys.ToList())
                    {
                        yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, StudyImageList[name]));
                        if (m_FileManager.GetPng(name) == null) yield return new WaitForSeconds(3);
                        StudyImageList[name] = m_FileManager.GetPng(name);
                        int percent = (int) (i / (float) StudyImageList.Keys.ToList().Count * 100);
                        if (percent % 20 == 0 && percent != lastpercent)
                        {
                            Puts($"Идёт загрузка изображений, загружено: {percent}%");
                            lastpercent = percent;
                        }

                        i++;
                    }

                    ImageInit = true;
                    m_FileManager.SaveData();
                    PrintWarning("Успешно загружено {0} изображения", i);
                }

                void OnPlayerInit(BasePlayer player)
                {
                    RegisteredDataUser(player);
                }

                private void Unload()
                {
                    WriteData();

                    UnityEngine.Object.Destroy(FileManagerObject);
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        player.SetFlag(BaseEntity.Flags.Reserved3, false);

                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne);
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo);
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree);
                        
                        CuiHelper.DestroyUi(player, "PanalUI");
                        CuiHelper.DestroyUi(player, "InfoPanalUI");
                    }
                }

                #endregion

                #region Записываем изучение, когда игрок изучает предмет с помощью чертежа.

                private void OnItemAction(Item item, string action, BasePlayer player)
                {
                    if (player == null || item == null) return;
                    if (action != "study") return;

                    if (item.blueprintTargetDef.shortname == "hat.miner")
                    {
                        StudyTreeUser[player.userID].MinersHat = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "tshirt")
                    {
                        StudyTreeUser[player.userID].TShirt = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shirt.collared")
                    {
                        StudyTreeUser[player.userID].Shirt = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "tshirt.long")
                    {
                        StudyTreeUser[player.userID].LongsleeveTShirt = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "pants")
                    {
                        StudyTreeUser[player.userID].Pants = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "riot.helmet")
                    {
                        StudyTreeUser[player.userID].RiotHelmet = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "bucket.helmet")
                    {
                        StudyTreeUser[player.userID].GasMask = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "jacket.snow")
                    {
                        StudyTreeUser[player.userID].SnowJacket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "jacket")
                    {
                        StudyTreeUser[player.userID].Jacket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "hammer.salvaged")
                    {
                        StudyTreeUser[player.userID].SalvagedHammer = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "salvaged.cleaver")
                    {
                        StudyTreeUser[player.userID].SalvagedCleaver = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "mace")
                    {
                        StudyTreeUser[player.userID].Mace = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "trap.bear")
                    {
                        StudyTreeUser[player.userID].SnapTrap = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "flameturret")
                    {
                        StudyTreeUser[player.userID].FlameTurret = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "guntrap")
                    {
                        StudyTreeUser[player.userID].ShotgunTrap = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.pistol")
                    {
                        StudyTreeUser[player.userID].PistolBullet = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.silencer")
                    {
                        StudyTreeUser[player.userID].Silencer = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shotgun.waterpipe")
                    {
                        StudyTreeUser[player.userID].WaterpipeShotgun = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "pistol.revolver")
                    {
                        StudyTreeUser[player.userID].Revolver = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.flashlight")
                    {
                        StudyTreeUser[player.userID].WeaponFlashlight = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shotgun.double")
                    {
                        StudyTreeUser[player.userID].DoubleBarrelShotgun = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "explosive.satchel")
                    {
                        StudyTreeUser[player.userID].SatchelCharge = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "grenade.beancan")
                    {
                        StudyTreeUser[player.userID].BeancanGrenade = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "barricade.woodwire")
                    {
                        StudyTreeUser[player.userID].BarbedWoodenBarricade = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "water.catcher.small")
                    {
                        StudyTreeUser[player.userID].SmallWaterCatcher = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shutter.metal.embrasure.b")
                    {
                        StudyTreeUser[player.userID].MetalVerticalEmbrasure = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shutter.metal.embrasure.a")
                    {
                        StudyTreeUser[player.userID].MetalHorizontalEmbrasure = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "floor.grill")
                    {
                        StudyTreeUser[player.userID].FloorGrill = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.window.bars.metal")
                    {
                        StudyTreeUser[player.userID].MetalWindowBars = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "watchtower.wood")
                    {
                        StudyTreeUser[player.userID].WatchTower = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ladder.wooden.wall")
                    {
                        StudyTreeUser[player.userID].WoodenLadder = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "bed")
                    {
                        StudyTreeUser[player.userID].Bed = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "dropbox")
                    {
                        StudyTreeUser[player.userID].DropBox = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.external.high")
                    {
                        StudyTreeUser[player.userID].HighExternalWoodenWall = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "gates.external.high.wood")
                    {
                        StudyTreeUser[player.userID].HighExternalWoodenGate = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.frame.garagedoor")
                    {
                        StudyTreeUser[player.userID].GarageDoor = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "hazmatsuit")
                    {
                        StudyTreeUser[player.userID].HazmatSuit = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "coffeecan.helmet")
                    {
                        StudyTreeUser[player.userID].CoffeeCanHelmet = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shoes.boots")
                    {
                        StudyTreeUser[player.userID].Boots = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "syringe.medical")
                    {
                        StudyTreeUser[player.userID].MedicalSyringe = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "roadsign.jacket")
                    {
                        StudyTreeUser[player.userID].RoadSingJacket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "hoodie")
                    {
                        StudyTreeUser[player.userID].Hoodie = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "largemedkit")
                    {
                        StudyTreeUser[player.userID].LargeMedkit = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "roadsign.kilt")
                    {
                        StudyTreeUser[player.userID].RoadSingKilt = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "roadsign.gloves")
                    {
                        StudyTreeUser[player.userID].RoadsingGloves = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "heavy.plate.helmet")
                    {
                        StudyTreeUser[player.userID].HeavyPlateHelmet = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "heavy.plate.jacket")
                    {
                        StudyTreeUser[player.userID].HeavyPlateJacket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "heavy.plate.pants")
                    {
                        StudyTreeUser[player.userID].HeavyPlatePants = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "longsword")
                    {
                        StudyTreeUser[player.userID].Longsword = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "axe.salvaged")
                    {
                        StudyTreeUser[player.userID].SalvegedAxe = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "icepick.salvaged")
                    {
                        StudyTreeUser[player.userID].SalvagedIcepick = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "chainsaw")
                    {
                        StudyTreeUser[player.userID].Chainsaw = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "flamethrower")
                    {
                        StudyTreeUser[player.userID].FlameThrower = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "trap.landmine")
                    {
                        StudyTreeUser[player.userID].LandMine = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.shotgun")
                    {
                        StudyTreeUser[player.userID].Gauge12Buckshot = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.shotgun.slug")
                    {
                        StudyTreeUser[player.userID].Gauge12Slug = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "shotgun.pump")
                    {
                        StudyTreeUser[player.userID].PumpShotgun = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.shotgun.fire")
                    {
                        StudyTreeUser[player.userID].Gauge12IncendiaryShell = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "pistol.semiauto")
                    {
                        StudyTreeUser[player.userID].SemiAutomaticPistol = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "grenade.f1")
                    {
                        StudyTreeUser[player.userID].F1Grenade = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.muzzlebrake")
                    {
                        StudyTreeUser[player.userID].MuzzleBrake = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.muzzleboost")
                    {
                        StudyTreeUser[player.userID].MuzzleBoost = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "pistol.python")
                    {
                        StudyTreeUser[player.userID].PythonRevolver = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.pistol.fire")
                    {
                        StudyTreeUser[player.userID].IncendiaryPistolAmmo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.pistol.hv")
                    {
                        StudyTreeUser[player.userID].HVPistolAmmo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "smg.thompson")
                    {
                        StudyTreeUser[player.userID].Thompson = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "smg.2")
                    {
                        StudyTreeUser[player.userID].CustomSMG = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "rifle.semiauto")
                    {
                        StudyTreeUser[player.userID].SemiAutomaticRifle = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rifle")
                    {
                        StudyTreeUser[player.userID].Rifle556Ammo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rocket.fire")
                    {
                        StudyTreeUser[player.userID].IncendiaryRocket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "water.catcher.large")
                    {
                        StudyTreeUser[player.userID].LargeWaterCatcher = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.frame.cell.gate")
                    {
                        StudyTreeUser[player.userID].PrisonCellGate = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "barricade.concrete")
                    {
                        StudyTreeUser[player.userID].ConcreteBarricade = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.frame.cell")
                    {
                        StudyTreeUser[player.userID].PrisonCellWall = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "barricade.metal")
                    {
                        StudyTreeUser[player.userID].MetalBarricade = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "locker")
                    {
                        StudyTreeUser[player.userID].Locker = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.external.high.stone")
                    {
                        StudyTreeUser[player.userID].HighExternalStoneWall = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "small.oil.refinery")
                    {
                        StudyTreeUser[player.userID].SmallOilRefinery = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "floor.ladder.hatch")
                    {
                        StudyTreeUser[player.userID].LadderHatch = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "furnace.large")
                    {
                        StudyTreeUser[player.userID].LargeFurnace = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "generator.wind.scrap")
                    {
                        StudyTreeUser[player.userID].WindTurbine = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "autoturret")
                    {
                        StudyTreeUser[player.userID].AutoTurret = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "wall.window.bars.toptier")
                    {
                        StudyTreeUser[player.userID].ReinforcedWindowBars = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "door.hinged.toptier")
                    {
                        StudyTreeUser[player.userID].ArmoredDoor = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "door.double.hinged.toptier")
                    {
                        StudyTreeUser[player.userID].ArmoredDoubleDoor = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "gates.external.high.stone")
                    {
                        StudyTreeUser[player.userID].HighExternalStoneGate = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "metal.facemask")
                    {
                        StudyTreeUser[player.userID].MetalFacemask = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "metal.plate.torso")
                    {
                        StudyTreeUser[player.userID].MetalChestPlate = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.lasersight")
                    {
                        StudyTreeUser[player.userID].WeaponLasersight = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rifle.hv")
                    {
                        StudyTreeUser[player.userID].HV556RifleAmmo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rifle.incendiary")
                    {
                        StudyTreeUser[player.userID].Incendiary556RifleAmmo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "smg.mp5")
                    {
                        StudyTreeUser[player.userID].MP5A4 = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.holosight")
                    {
                        StudyTreeUser[player.userID].Holosight = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "weapon.mod.small.scope")
                    {
                        StudyTreeUser[player.userID].Zoom4xScope = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rifle.explosive")
                    {
                        StudyTreeUser[player.userID].Explosive556RifleAmmo = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "rifle.ak")
                    {
                        StudyTreeUser[player.userID].AssaultRifle = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "rifle.bolt")
                    {
                        StudyTreeUser[player.userID].BoltActionRifle = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "rocket.launcher")
                    {
                        StudyTreeUser[player.userID].RocketLauncher = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rocket.hv")
                    {
                        StudyTreeUser[player.userID].HighVelocityRocket = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "explosives")
                    {
                        StudyTreeUser[player.userID].Explosives = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "explosive.timed")
                    {
                        StudyTreeUser[player.userID].TimedExplosiveCharge = true;
                        WriteData();
                    }

                    if (item.blueprintTargetDef.shortname == "ammo.rocket.basic")
                    {
                        StudyTreeUser[player.userID].Rocket = true;
                        WriteData();
                    }
                }

                #endregion

                #region Команды плагина
				
				[ChatCommand("Open.Workbench.StudyTree")]
                void OpenWorkbenchStudyTree(BasePlayer player)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
                    {
                        BaseCombatEntity entity = null;
                        try { entity = hit.GetEntity() as BaseCombatEntity; }
                        catch { return; }

                        if (entity.ShortPrefabName.Contains("workbench1.deployed"))
                        {
                            UIWorkbenchLevelOne(player);
                        }

                        if (entity.ShortPrefabName.Contains("workbench2.deployed"))
                        {
                           UIWorkbenchLevelTwo(player);
                        }

                        if (entity.ShortPrefabName.Contains("workbench3.deployed"))
                        {
                            UIWorkbenchLevelThree(player);
                        }
                    }
                }

                [ConsoleCommand("Open_Panel_Blueprints_Workbench_StudyTree")]
                void OpenPanelBlueprintsWorkbenchStudyTree(ConsoleSystem.Arg arg)
                {
                    BasePlayer player = arg.Player();
                    
                    if (arg.Args[0] == "hat.miner")
                    {
                        string item = "hat.miner";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MinersHat = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "tshirt")
                    {
                        string item = "tshirt";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].TShirt = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shirt.collared")
                    {
                        string item = "shirt.collared";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Shirt = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "tshirt.long")
                    {
                        string item = "tshirt.long";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LongsleeveTShirt = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "pants")
                    {
                        string item = "pants";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Pants = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "riot.helmet")
                    {
                        string item = "riot.helmet";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].RiotHelmet = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "bucket.helmet")
                    {
                        string item = "bucket.helmet";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].GasMask = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "jacket.snow")
                    {
                        string item = "jacket.snow";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SnowJacket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "jacket")
                    {
                        string item = "jacket";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Jacket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "hammer.salvaged")
                    {
                        string item = "hammer.salvaged";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SalvagedHammer = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "salvaged.cleaver")
                    {
                        string item = "salvaged.cleaver";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SalvagedCleaver = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "mace")
                    {
                        string item = "mace";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Mace = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "trap.bear")
                    {
                        string item = "trap.bear";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SnapTrap = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "flameturret")
                    {
                        string item = "flameturret";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].FlameTurret = true; 
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "guntrap")
                    {
                        string item = "guntrap";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].ShotgunTrap = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.pistol")
                    {
                        string item = "ammo.pistol";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].PistolBullet = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.silencer")
                    {
                        string item = "weapon.mod.silencer";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Silencer = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shotgun.waterpipe")
                    {
                        string item = "shotgun.waterpipe";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WaterpipeShotgun = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "pistol.revolver")
                    {
                        string item = "pistol.revolver";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Revolver = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.flashlight")
                    {
                        string item = "weapon.mod.flashlight";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WeaponFlashlight = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shotgun.double")
                    {
                        string item = "shotgun.double";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].DoubleBarrelShotgun = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "explosive.satchel")
                    {
                        string item = "explosive.satchel";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SatchelCharge = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "grenade.beancan")
                    {
                        string item = "grenade.beancan";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].BeancanGrenade = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "barricade.woodwire")
                    {
                        string item = "barricade.woodwire";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].BarbedWoodenBarricade = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "water.catcher.small")
                    {
                        string item = "water.catcher.small";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SmallWaterCatcher = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shutter.metal.embrasure.b")
                    {
                        string item = "shutter.metal.embrasure.b";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalVerticalEmbrasure = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shutter.metal.embrasure.a")
                    {
                        string item = "shutter.metal.embrasure.a";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalHorizontalEmbrasure = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "floor.grill")
                    {
                        string item = "floor.grill";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].FloorGrill = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.window.bars.metal")
                    {
                        string item = "wall.window.bars.metal";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalWindowBars = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "watchtower.wood")
                    {
                        string item = "watchtower.wood";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WatchTower = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ladder.wooden.wall")
                    {
                        string item = "ladder.wooden.wall";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WoodenLadder = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "bed")
                    {
                        string item = "bed";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Bed = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "dropbox")
                    {
                        string item = "dropbox";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].DropBox = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.external.high")
                    {
                        string item = "wall.external.high";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HighExternalWoodenWall = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "gates.external.high.wood")
                    {
                        string item = "gates.external.high.wood";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HighExternalWoodenGate = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.frame.garagedoor")
                    {
                        string item = "wall.frame.garagedoor";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].GarageDoor = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "hazmatsuit")
                    {
                        string item = "hazmatsuit";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HazmatSuit = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "coffeecan.helmet")
                    {
                        string item = "coffeecan.helmet";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].CoffeeCanHelmet = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shoes.boots")
                    {
                        string item = "shoes.boots";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Boots = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "syringe.medical")
                    {
                        string item = "syringe.medical";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MedicalSyringe = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "roadsign.jacket")
                    {
                        string item = "roadsign.jacket";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].RoadSingJacket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "hoodie")
                    {
                        string item = "hoodie";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Hoodie = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "largemedkit")
                    {
                        string item = "largemedkit";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LargeMedkit = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "roadsign.kilt")
                    {
                        string item = "roadsign.kilt";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].RoadSingKilt = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "roadsign.gloves")
                    {
                        string item = "roadsign.gloves";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].RoadsingGloves = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "heavy.plate.helmet")
                    {
                        string item = "heavy.plate.helmet";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HeavyPlateHelmet = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "heavy.plate.jacket")
                    {
                        string item = "heavy.plate.jacket";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HeavyPlateJacket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "heavy.plate.pants")
                    {
                        string item = "heavy.plate.pants";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HeavyPlatePants = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "longsword")
                    {
                        string item = "longsword";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Longsword = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "axe.salvaged")
                    {
                        string item = "axe.salvaged";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SalvegedAxe = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "icepick.salvaged")
                    {
                        string item = "icepick.salvaged";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SalvagedIcepick = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "chainsaw")
                    {
                        string item = "chainsaw";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Chainsaw = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "flamethrower")
                    {
                        string item = "flamethrower";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].FlameThrower = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "trap.landmine")
                    {
                        string item = "trap.landmine";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LandMine = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.shotgun")
                    {
                        string item = "ammo.shotgun";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Gauge12Buckshot = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.shotgun.slug")
                    {
                        string item = "ammo.shotgun.slug";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Gauge12Slug = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "shotgun.pump")
                    {
                        string item = "shotgun.pump";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].PumpShotgun = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.shotgun.fire")
                    {
                        string item = "ammo.shotgun.fire";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Gauge12IncendiaryShell = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "pistol.semiauto")
                    {
                        string item = "pistol.semiauto";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SemiAutomaticPistol = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "grenade.f1")
                    {
                        string item = "grenade.f1";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].F1Grenade = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.muzzlebrake")
                    {
                        string item = "weapon.mod.muzzlebrake";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MuzzleBrake = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.muzzleboost")
                    {
                        string item = "weapon.mod.muzzleboost";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MuzzleBoost = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "pistol.python")
                    {
                        string item = "pistol.python";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].PythonRevolver = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.pistol.fire")
                    {
                        string item = "ammo.pistol.fire";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].IncendiaryPistolAmmo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.pistol.hv")
                    {
                        string item = "ammo.pistol.hv";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HVPistolAmmo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "smg.thompson")
                    {
                        string item = "smg.thompson";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Thompson = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "smg.2")
                    {
                        string item = "smg.2";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].CustomSMG = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "rifle.semiauto")
                    {
                        string item = "rifle.semiauto";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SemiAutomaticRifle = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rifle")
                    {
                        string item = "ammo.rifle";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Rifle556Ammo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rocket.fire")
                    {
                        string item = "ammo.rocket.fire";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].IncendiaryRocket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "water.catcher.large")
                    {
                        string item = "water.catcher.large";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LargeWaterCatcher = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.frame.cell.gate")
                    {
                        string item = "wall.frame.cell.gate";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].PrisonCellGate = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "barricade.concrete")
                    {
                        string item = "barricade.concrete";
                        int amount = 20;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].ConcreteBarricade = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.frame.cell")
                    {
                        string item = "wall.frame.cell";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].PrisonCellWall = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "barricade.metal")
                    {
                        string item = "barricade.metal";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalBarricade = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "locker")
                    {
                        string item = "locker";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Locker = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.external.high.stone")
                    {
                        string item = "wall.external.high.stone";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HighExternalStoneWall = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "small.oil.refinery")
                    {
                        string item = "small.oil.refinery";
                        int amount = 75;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].SmallOilRefinery = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "floor.ladder.hatch")
                    {
                        string item = "floor.ladder.hatch";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LadderHatch = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "furnace.large")
                    {
                        string item = "furnace.large";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].LargeFurnace = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "generator.wind.scrap")
                    {
                        string item = "generator.wind.scrap";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WindTurbine = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "autoturret")
                    {
                        string item = "autoturret";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].AutoTurret = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "wall.window.bars.toptier")
                    {
                        string item = "wall.window.bars.toptier";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].ReinforcedWindowBars = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "door.hinged.toptier")
                    {
                        string item = "door.hinged.toptier";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].ArmoredDoor = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "door.double.hinged.toptier")
                    {
                        string item = "door.double.hinged.toptier";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].ArmoredDoubleDoor = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "gates.external.high.stone")
                    {
                        string item = "gates.external.high.stone";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HighExternalStoneGate = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "metal.facemask")
                    {
                        string item = "metal.facemask";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalFacemask = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "metal.plate.torso")
                    {
                        string item = "metal.plate.torso";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MetalChestPlate = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.lasersight")
                    {
                        string item = "weapon.mod.lasersight";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].WeaponLasersight = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rifle.hv")
                    {
                        string item = "ammo.rifle.hv";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HV556RifleAmmo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rifle.incendiary")
                    {
                        string item = "ammo.rifle.incendiary";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Incendiary556RifleAmmo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "smg.mp5")
                    {
                        string item = "smg.mp5";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].MP5A4 = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.holosight")
                    {
                        string item = "weapon.mod.holosight";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Holosight = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "weapon.mod.small.scope")
                    {
                        string item = "weapon.mod.small.scope";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Zoom4xScope = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rifle.explosive")
                    {
                        string item = "ammo.rifle.explosive";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Explosive556RifleAmmo = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "rifle.ak")
                    {
                        string item = "rifle.ak";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].AssaultRifle = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "rifle.bolt")
                    {
                        string item = "rifle.bolt";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].BoltActionRifle = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "rocket.launcher")
                    {
                        string item = "rocket.launcher";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].RocketLauncher = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rocket.hv")
                    {
                        string item = "ammo.rocket.hv";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].HighVelocityRocket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "explosives")
                    {
                        string item = "explosives";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Explosives = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "explosive.timed")
                    {
                        string item = "explosive.timed";
                        int amount = 500;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].TimedExplosiveCharge = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }

                    if (arg.Args[0] == "ammo.rocket.basic")
                    {
                        string item = "ammo.rocket.basic";
                        int amount = 125;
                        
                        if (!player.blueprints.IsUnlocked(FindItemDefinition(player, item)))
                        {
                            if (CheckAmountScrap(player) >= amount)
                            {
                                player.blueprints.Unlock(FindItemDefinition(player, item));
                                TakeScrap(player, amount);
                                SendSound(player, "assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab");
                                SendReply(player, $"Вы успешно изучили: {FindItemDefinition(player, item).displayName.english}");
                                
                                StudyTreeUser[player.userID].Rocket = true;
                                WriteData();
                            }
                            else
                            {
                                SendReply(player, $"Вам не хватает скрапа чтобы изучить данный предмет.\nУ вас имеется: {CheckAmountScrap(player)}");
                            }
                        }
                        else
                        {
                            SendReply(player, $"Предмет {FindItemDefinition(player, item).displayName.english} уже изучен!");
                        }
                    }
                }
                
                [ChatCommand("Workbench.Item.Pickup")]
                void CMD_WorkbenchPickup(BasePlayer player)
                { 
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
                    {
                        BaseCombatEntity entity = null;
                        try { entity = hit.GetEntity() as BaseCombatEntity; }
                        catch { return; }

                        if (entity.ShortPrefabName.Contains("workbench1.deployed") || entity.ShortPrefabName.Contains("workbench2.deployed") || entity.ShortPrefabName.Contains("workbench3.deployed"))
                        {
                            ItemDefinition itemDef = entity.pickup.itemTarget;
                            player.GiveItem(ItemManager.Create(itemDef, 1, entity.skinID));
                            entity.Kill();
                        }
                    }
                }

                [ConsoleCommand("UI_CLOSE")]
                private void cmdConsoleCloseUI(ConsoleSystem.Arg args)
                {
                    if (args.Player() == null) return; 
                    args.Player()?.SetFlag(BaseEntity.Flags.Reserved3, false);
                    
                    CuiHelper.DestroyUi(args.Player(), "PanalUI");
                    CuiHelper.DestroyUi(args.Player(), "InfoPanalUI");
                    CuiHelper.DestroyUi(args.Player(), "PanelPanalUI");
                    
                    CuiHelper.DestroyUi(args.Player(), LayerWorkbenchLevelOne);
                    CuiHelper.DestroyUi(args.Player(), LayerWorkbenchLevelTwo);
                    CuiHelper.DestroyUi(args.Player(), LayerWorkbenchLevelThree);
                }
                
                [ConsoleCommand("Give_Blueprints_Workbench_StudyTree")]
                private void GiveBlueprintsWorkbenchStudyTree(ConsoleSystem.Arg args)
                {
                    BasePlayer player = args.Player();
                    
                    if (player == null) return;
                    if (!ImageInit) return;
                    
                    CuiElementContainer container = new CuiElementContainer();
                    
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = HexFormat("#2A2A21BA") },
                        RectTransform = { AnchorMin = "0.88 0.5", AnchorMax = "0.88 0.5", OffsetMin = "-146 -360", OffsetMax = "153 360" },
                    }, "Overlay", "PanalUI");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.92 0.965", AnchorMax = "0.92 0.965", OffsetMin = "-15 -15", OffsetMax = "15 15" },
                        Button = { Color = "0.929 0.882 0.847 0.6", Command = "UI_CLOSE", Sprite = "assets/icons/close.png" }, 
                        Text = { Text = "" } 
                    }, "PanalUI");
                    
                    container.Add(new CuiElement
                    {
                        Parent = "PanalUI",
                        Name = "PanalUI" + ".ItemInfo",
                        Components =
                        {
                            new CuiImageComponent { Color = HexFormat("#6D6D6D05") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = "-135 -50", OffsetMax = "135 50" }
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = "PanalUI" + ".ItemInfo",
                        Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList[$"{args.Args[0]}"] },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.5", AnchorMax = "0.2 0.5", OffsetMin = "-40 -40", OffsetMax = "40 40" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.65 0.5", OffsetMin = "-70 -40", OffsetMax = "75 40" },
                        Button = { Color = "0 0 0 0" }, 
                        Text = { Text = $"{FindItemDefinition(player, args.Args[0]).displayName.translated}", Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 20 } 
                    }, "PanalUI" + ".ItemInfo");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.5 0.43", AnchorMax = "0.5 0.43", OffsetMin = "-130 -50", OffsetMax = "130 50" },
                        Button = { Color = "0 0 0 0" }, 
                        Text = { Text = $"{FindItemDefinition(player, args.Args[0]).displayDescription.translated}", Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, FontSize = 16 } 
                    }, "PanalUI");

                    if (!player.blueprints.IsUnlocked(FindItemDefinition(player, args.Args[0])))
                    { 
                        container.Add(new CuiElement
                        {
                            Parent = "PanalUI",
                            Name = "PanalUI" + ".ButtonBG",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat("#0000003F") },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.07", AnchorMax = "0.5 0.07", OffsetMin = "-120 -35", OffsetMax = "120 35" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = "PanalUI" + ".ButtonBG",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["scrap"] },
                                new CuiRectTransformComponent { AnchorMin = "0.1 0.5", AnchorMax = "0.1 0.5", OffsetMin = "-15 -15", OffsetMax = "15 15" }
                            }
                        });

                        foreach (var check in StudyItemsList)
                        {
                            if (args.Args[0] == check.Key)
                            {
                                var color = CheckAmountScrap(player) >= check.Value ? "#FFFFFFFF" : "#F18787FF";

                                container.Add(new CuiButton
                                {
                                    RectTransform = { AnchorMin = "0.3 0.5", AnchorMax = "0.3 0.5", OffsetMin = "-28 -15", OffsetMax = "25 15" },
                                    Button = { Color = "0 0 0 0" },
                                    Text = { Text = $"{check.Value.ToString()}", Font = "robotocondensed-bold.ttf",  Color = HexFormat(color), Align = TextAnchor.MiddleLeft, FontSize = 16 }
                                }, "PanalUI" + ".ButtonBG");
                            }
                        }

                        foreach (var check in StudyItemsList)
                        {
                            if (args.Args[0] == check.Key)
                            {
                                var color = CheckAmountScrap(player) >= check.Value ? UnlockedItemColor : "#C43E00FF";

                                container.Add(new CuiElement
                                {
                                    Parent = "PanalUI" + ".ButtonBG",
                                    Name = "PanalUI" + ".Button",
                                    Components =
                                    {
                                        new CuiImageComponent { Color = HexFormat(color) },
                                        new CuiRectTransformComponent { AnchorMin = "0.75 0.5", AnchorMax = "0.75 0.5", OffsetMin = "-60 -35", OffsetMax = "60 35" }
                                    }
                                });
                            
                                var image = CheckAmountScrap(player) >= check.Value ? "unlock" : "close";
                                var colorImage = CheckAmountScrap(player) >= check.Value ? "#FFFFFFFF" : "#DA6C1EFF";

                                container.Add(new CuiElement
                                {
                                    Parent = "PanalUI" + ".Button",
                                    Components =
                                    {
                                        new CuiRawImageComponent { Color = HexFormat(colorImage), Png = StudyImageList[image] },
                                        new CuiRectTransformComponent { AnchorMin = "0.2 0.5", AnchorMax = "0.2 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10" }
                                    }
                                });
                            
                                var name = CheckAmountScrap(player) >= check.Value ? "UNLOCK" : "CAN'T AFFORD";
                                var size = CheckAmountScrap(player) >= check.Value ? 14 : 11;
                                var button = CheckAmountScrap(player) >= check.Value ? $"Open_Panel_Blueprints_Workbench_StudyTree {args.Args[0]}" : "";
                            
                                container.Add(new CuiButton
                                {
                                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-18 -35", OffsetMax = "60 32" },
                                    Button = { Color = "0 0 0 0", Command = button },
                                    Text = { Text = $"{name}", Font = "robotocondensed-bold.ttf",  Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = size }
                                }, "PanalUI" + ".Button");
                            } 
                        }
                    }

                    CuiHelper.DestroyUi(player, "InfoPanalUI");
                    CuiHelper.DestroyUi(player, "PanalUI");
                    CuiHelper.DestroyUi(player, "PanelPanalUI");
                    CuiHelper.AddUi(player, container);
                }
                
                [ConsoleCommand("Give_Info_Blueprints_Workbench_StudyTree")]
                private void GiveInfoBlueprintsWorkbenchStudyTree(ConsoleSystem.Arg args)
                {
                    BasePlayer player = args.Player();
                    
                    if (player == null) return;
                    if (!ImageInit) return;
                    
                    CuiElementContainer container = new CuiElementContainer();
                    
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = HexFormat("#2A2A21BA") },
                        RectTransform = { AnchorMin = "0.88 0.5", AnchorMax = "0.88 0.5", OffsetMin = "-146 -360", OffsetMax = "153 360" },
                    }, "Overlay", "InfoPanalUI");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.92 0.965", AnchorMax = "0.92 0.965", OffsetMin = "-15 -15", OffsetMax = "15 15" },
                        Button = { Color = "0.929 0.882 0.847 0.6", Command = "UI_CLOSE", Sprite = "assets/icons/close.png" }, 
                        Text = { Text = "" } 
                    }, "InfoPanalUI");
                    
                    container.Add(new CuiElement
                    {
                        Parent = "InfoPanalUI",
                        Name = "InfoPanalUI" + ".ItemInfo",
                        Components =
                        {
                            new CuiImageComponent { Color = HexFormat("#6D6D6D05") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = "-135 -50", OffsetMax = "135 50" }
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = "InfoPanalUI" + ".ItemInfo",
                        Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList[$"{args.Args[0]}"] },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.5", AnchorMax = "0.2 0.5", OffsetMin = "-40 -40", OffsetMax = "40 40" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.65 0.5", OffsetMin = "-70 -40", OffsetMax = "75 40" },
                        Button = { Color = "0 0 0 0" }, 
                        Text = { Text = $"{FindItemDefinition(player, args.Args[0]).displayName.translated}", Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 20 } 
                    }, "InfoPanalUI" + ".ItemInfo");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.5 0.43", AnchorMax = "0.5 0.43", OffsetMin = "-130 -50", OffsetMax = "130 50" },
                        Button = { Color = "0 0 0 0" }, 
                        Text = { Text = $"{FindItemDefinition(player, args.Args[0]).displayDescription.translated}", Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.UpperCenter, FontSize = 16 } 
                    }, "InfoPanalUI");

                    CuiHelper.DestroyUi(player, "PanalUI");
                    CuiHelper.DestroyUi(player, "PanelPanalUI");
                    CuiHelper.DestroyUi(player, "InfoPanalUI");
                    CuiHelper.AddUi(player, container);
                }
                
               private void PanelBlueprintsWorkbenchStudyTree(BasePlayer player)
                {
                    if (player == null) return;
                    if (!ImageInit) return;
                    
                    CuiElementContainer container = new CuiElementContainer();
                    
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = HexFormat("#2A2A21BA") },
                        RectTransform = { AnchorMin = "0.88 0.5", AnchorMax = "0.88 0.5", OffsetMin = "-146 -360", OffsetMax = "153 360" },
                    }, "Overlay", "PanelPanalUI");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.92 0.965", AnchorMax = "0.92 0.965", OffsetMin = "-15 -15", OffsetMax = "15 15" },
                        Button = { Color = "0.929 0.882 0.847 0.6", Command = "UI_CLOSE", Sprite = "assets/icons/close.png" }, 
                        Text = { Text = "" } 
                    }, "PanelPanalUI");

                    CuiHelper.DestroyUi(player, "PanalUI");
                    CuiHelper.DestroyUi(player, "PanelPanalUI");
                    CuiHelper.DestroyUi(player, "InfoPanalUI");
                    CuiHelper.AddUi(player, container);
                }

                #endregion

                #region Графический интерфейс плагина
				
				[ChatCommand("l1")]
                private void UIWorkbenchLevelOneT(BasePlayer player)
                {
					SendReply(player, "Чтобы открыть дерево изучений вам необходимо подойти к первому верстаку и нажать букву Е смотря на него.");
				}

                private void UIWorkbenchLevelOne(BasePlayer player)
                {
                    if (!ImageInit) return;

                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
                    {
                        BaseCombatEntity entity = null;
                        try
                        {
                            entity = hit.GetEntity() as BaseCombatEntity;
                        }
                        catch
                        {
                            return;
                        }

                        if (entity.ShortPrefabName.Contains("workbench1.deployed"))
                        {
                            if (player.HasFlag(BaseEntity.Flags.Reserved3)) return;
                            player.SetFlag(BaseEntity.Flags.Reserved3, true);

                            CuiElementContainer container = new CuiElementContainer();

                            container.Add(new CuiPanel
                            {
                                CursorEnabled = true,
                                Image = {Color = "0 0 0 0"},
                                RectTransform =
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-625 -355",
                                    OffsetMax = "220 350"
                                },
                            }, "Overlay", LayerWorkbenchLevelOne);

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne,
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(BackgroundСolor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -1000",
                                        OffsetMax = "1000 1000"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne,
                                Name = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = "0 0 0 0"},
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                                }
                            });

                            #region Отрисовка линий дерева изучений

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.871788", AnchorMax = "0.1495879 0.871788",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.7460195", AnchorMax = "0.1495879 0.7460195",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.3706053", AnchorMax = "0.1495879 0.3706053",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3736517 0.3715509", AnchorMax = "0.3736517 0.3715509",
                                        OffsetMin = "-1 -10", OffsetMax = "1 10"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4675375 0.3696597", AnchorMax = "0.4675375 0.3696597",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5637879 0.3696597", AnchorMax = "0.5637879 0.3696597",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8407013 0.871788", AnchorMax = "0.8407013 0.871788",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7949435 0.120014", AnchorMax = "0.7949435 0.120014",
                                        OffsetMin = "-1 -12", OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.6268704", AnchorMax = "0.1495879 0.6268704",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.6268704", AnchorMax = "0.1495879 0.6268704",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.4859719", AnchorMax = "0.1495879 0.4859719",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4667486 0.879353", AnchorMax = "0.4667486 0.879353",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.422567 0.7554758", AnchorMax = "0.422567 0.7554758",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5148744 0.7554758", AnchorMax = "0.5148744 0.7554758",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4699044 0.7365632", AnchorMax = "0.4699044 0.7365632",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4667486 0.627816", AnchorMax = "0.4667486 0.627816",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4202001 0.50583", AnchorMax = "0.4202001 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5148744 0.50583", AnchorMax = "0.5148744 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3744406 0.2552387", AnchorMax = "0.3744406 0.2552387",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4691154 0.2552387", AnchorMax = "0.4691154 0.2552387",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5637879 0.2552387", AnchorMax = "0.5637879 0.2552387",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5637879 0.2552387", AnchorMax = "0.5637879 0.2552387",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5172412 0.2363262", AnchorMax = "0.5172412 0.2363262",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4194112 0.2363262", AnchorMax = "0.4194112 0.2363262",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7957324 0.7375088", AnchorMax = "0.7957324 0.7375088",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.887248 0.7375088", AnchorMax = "0.887248 0.7375088",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7909989 0.6306529", AnchorMax = "0.7909989 0.6306529",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8856701 0.6306529", AnchorMax = "0.8856701 0.6306529",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7452411 0.50583", AnchorMax = "0.7452411 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7452411 0.50583", AnchorMax = "0.7452411 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8383345 0.50583", AnchorMax = "0.8383345 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9345836 0.50583", AnchorMax = "0.9345836 0.50583",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7925767 0.4859719", AnchorMax = "0.7925767 0.4859719",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8904037 0.4859719", AnchorMax = "0.8904037 0.4859719",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7925767 0.3810072", AnchorMax = "0.7925767 0.3810072",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.887248 0.3800616", AnchorMax = "0.887248 0.3800616",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8399124 0.361149", AnchorMax = "0.8399124 0.361149",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8407013 0.2552387", AnchorMax = "0.8407013 0.2552387",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7957324 0.2353805", AnchorMax = "0.7957324 0.2353805",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8896148 0.2353805", AnchorMax = "0.8896148 0.2353805",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1038284 0.8074853", AnchorMax = "0.1038284 0.8074853",
                                        OffsetMin = "-8 -1", OffsetMax = "8 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1985032 0.8074853", AnchorMax = "0.1985032 0.8074853",
                                        OffsetMin = "-8 -1", OffsetMax = "8 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1985032 0.8074853", AnchorMax = "0.1985032 0.8074853",
                                        OffsetMin = "-8 -1", OffsetMax = "8 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.6174141", AnchorMax = "0.1495879 0.6174141",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1495879 0.4954281", AnchorMax = "0.1495879 0.4954281",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4667486 0.8698967", AnchorMax = "0.4667486 0.8698967",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4691154 0.7460195", AnchorMax = "0.4691154 0.7460195",
                                        OffsetMin = "-40 -1", OffsetMax = "40 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4667486 0.6174141", AnchorMax = "0.4667486 0.6174141",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8422791 0.7460195", AnchorMax = "0.8422791 0.7460195",
                                        OffsetMin = "-40 -1", OffsetMax = "40 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8399124 0.3706053", AnchorMax = "0.8399124 0.3706053",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8430681 0.2448368", AnchorMax = "0.8430681 0.2448368",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4691154 0.4954281", AnchorMax = "0.4691154 0.4954281",
                                        OffsetMin = "-81 -1", OffsetMax = "81 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4691154 0.2457824", AnchorMax = "0.4691154 0.2457824",
                                        OffsetMin = "-81 -1", OffsetMax = "81 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8414902 0.620251", AnchorMax = "0.8414902 0.620251",
                                        OffsetMin = "-80 -1", OffsetMax = "80 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8399124 0.4954281", AnchorMax = "0.8399124 0.4954281",
                                        OffsetMin = "-81 -1", OffsetMax = "81 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1022505 0.5568939", AnchorMax = "0.1022505 0.5568939",
                                        OffsetMin = "-1 -44", OffsetMax = "1 44"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1969253 0.5568939", AnchorMax = "0.1969253 0.5568939",
                                        OffsetMin = "-1 -44", OffsetMax = "1 44"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4194112 0.8613861", AnchorMax = "0.4194112 0.8613861",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5140855 0.8613861", AnchorMax = "0.5140855 0.8613861",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4194112 0.6089035", AnchorMax = "0.4194112 0.6089035",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5140855 0.6089035", AnchorMax = "0.5140855 0.6089035",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3744406 0.4869175", AnchorMax = "0.3744406 0.4869175",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5637879 0.4869175", AnchorMax = "0.5637879 0.4869175",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4691154 0.4859719", AnchorMax = "0.4691154 0.4859719",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8422791 0.7564214", AnchorMax = "0.8422791 0.7564214",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7476079 0.6107947", AnchorMax = "0.7476079 0.6107947",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8399124 0.6107947", AnchorMax = "0.8399124 0.6107947",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9345836 0.6107947", AnchorMax = "0.9345836 0.6107947",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelOne + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3736517 0.3706053", AnchorMax = "0.3736517 0.3706053",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            #endregion

                            CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne);
                            CuiHelper.AddUi(player, container);

                            PanelBlueprintsWorkbenchStudyTree(player);
                            ServerMgr.Instance.StartCoroutine(UpdateWorkbenchLevelOne(player));
                        }
                        else
                        {
                            SendReply(player, "Чтобы открыть дерево изучений вам необходимо смотреть на первый верстак.");
                        }
                    }
                }

                private IEnumerator UpdateWorkbenchLevelOne(BasePlayer player)
                {
                    while (player.HasFlag(BaseEntity.Flags.Reserved3) && player.IsConnected)
                    {
                        CuiElementContainer container = new CuiElementContainer();
                        
                        #region 1. Отрисовка квадрата и картинки предмета: hat.miner
                        
                        var MinersHat = StudyTreeUser[player.userID].MinersHat == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareOneUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MinersHat },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.9341995", AnchorMax = "0.1503769 0.9341995", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareOne",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.9341995", AnchorMax = "0.1503769 0.9341995", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareOne",
                            Name = LayerWorkbenchLevelOne + ".IconOne",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconOne",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["hat.miner"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree hat.miner" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareOne");

                        #endregion
                        
                        #region 2. Отрисовка квадрата и картинки предмета: tshirt
                        
                        var TShirt = StudyTreeUser[player.userID].TShirt == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwoUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = TShirt },
                                new CuiRectTransformComponent { AnchorMin = "0.05570209 0.808431", AnchorMax = "0.05570209 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.05570209 0.808431", AnchorMax = "0.05570209 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwo",
                            Name = LayerWorkbenchLevelOne + ".IconTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwo",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["tshirt"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var TShirtCommand = StudyTreeUser[player.userID].MinersHat != true ? "Give_Info_Blueprints_Workbench_StudyTree tshirt" : "Give_Blueprints_Workbench_StudyTree tshirt";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = TShirtCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwo");

                        #endregion
                        
                        #region 3. Отрисовка квадрата и картинки предмета: shirt.collared
                        
                        var Shirt = StudyTreeUser[player.userID].Shirt == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThreeUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Shirt },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.808431", AnchorMax = "0.1503769 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThree",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.808431", AnchorMax = "0.1503769 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThree",
                            Name = LayerWorkbenchLevelOne + ".IconThree",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThree",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["shirt.collared"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var ShirtCommand = StudyTreeUser[player.userID].MinersHat != true ? "Give_Info_Blueprints_Workbench_StudyTree shirt.collared" : "Give_Blueprints_Workbench_StudyTree shirt.collared";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = ShirtCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThree");

                        #endregion
                        
                        #region 4. Отрисовка квадрата и картинки предмета: tshirt.long
                        
                        var LongsleeveTShirt = StudyTreeUser[player.userID].LongsleeveTShirt == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFourUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = LongsleeveTShirt },
                                new CuiRectTransformComponent { AnchorMin = "0.2450517 0.808431", AnchorMax = "0.2450517 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFour",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.2450517 0.808431", AnchorMax = "0.2450517 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareFour",
                            Name = LayerWorkbenchLevelOne + ".IconFour",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconFour",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["tshirt.long"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var LongsleeveTShirtCommand = StudyTreeUser[player.userID].MinersHat != true ? "Give_Info_Blueprints_Workbench_StudyTree tshirt.long" : "Give_Blueprints_Workbench_StudyTree tshirt.long";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = LongsleeveTShirtCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareFour");

                        #endregion
                        
                        #region 5. Отрисовка квадрата и картинки предмета: pants
                        
                        var Pants = StudyTreeUser[player.userID].Pants == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFiveUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Pants },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.6826625", AnchorMax = "0.1503769 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFive",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.6826625", AnchorMax = "0.1503769 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareFive",
                            Name = LayerWorkbenchLevelOne + ".IconFive",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconFive",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["pants"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var PantsCommand = StudyTreeUser[player.userID].Shirt != true ? "Give_Info_Blueprints_Workbench_StudyTree pants" : "Give_Blueprints_Workbench_StudyTree pants";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = PantsCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareFive");

                        #endregion
                        
                        #region 6. Отрисовка квадрата и картинки предмета: riot.helmet
                        
                        var RiotHelmet = StudyTreeUser[player.userID].RiotHelmet == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSixUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = RiotHelmet },
                                new CuiRectTransformComponent { AnchorMin = "0.1030395 0.556894", AnchorMax = "0.1030395 0.556894", OffsetMin = "-33 -33", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSix",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1030395 0.556894", AnchorMax = "0.1030395 0.556894", OffsetMin = "-33 -33", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareSix",
                            Name = LayerWorkbenchLevelOne + ".IconSix",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconSix",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["riot.helmet"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var RiotHelmetCommand = StudyTreeUser[player.userID].Pants != true ? "Give_Info_Blueprints_Workbench_StudyTree riot.helmet" : "Give_Blueprints_Workbench_StudyTree riot.helmet";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = RiotHelmetCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareSix");

                        #endregion
                        
                        #region 7. Отрисовка квадрата и картинки предмета: bucket.helmet
                        
                        var GasMask = StudyTreeUser[player.userID].GasMask == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSevenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = GasMask },
                                new CuiRectTransformComponent { AnchorMin = "0.1977143 0.556894", AnchorMax = "0.1977143 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSeven",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1977143 0.556894", AnchorMax = "0.1977143 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareSeven",
                            Name = LayerWorkbenchLevelOne + ".IconSeven",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconSeven",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["bucket.helmet"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var GasMaskCommand = StudyTreeUser[player.userID].Pants != true ? "Give_Info_Blueprints_Workbench_StudyTree bucket.helmet" : "Give_Blueprints_Workbench_StudyTree bucket.helmet";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = GasMaskCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareSeven");

                        #endregion
                        
                        #region 8. Отрисовка квадрата и картинки предмета: jacket.snow
                        
                        var SnowJacket = StudyTreeUser[player.userID].SnowJacket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareEightUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SnowJacket },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.4311255", AnchorMax = "0.1503769 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareEight",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.4311255", AnchorMax = "0.1503769 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareEight",
                            Name = LayerWorkbenchLevelOne + ".IconEight",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconEight",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["jacket.snow"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SnowJacketCommand = StudyTreeUser[player.userID].GasMask != true || StudyTreeUser[player.userID].RiotHelmet != true ? "Give_Info_Blueprints_Workbench_StudyTree jacket.snow" : "Give_Blueprints_Workbench_StudyTree jacket.snow";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SnowJacketCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareEight");

                        #endregion
                        
                        #region 9. Отрисовка квадрата и картинки предмета: jacket
                        
                        var Jacket = StudyTreeUser[player.userID].Jacket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareNineUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Jacket },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.3072483", AnchorMax = "0.1503769 0.3072483", OffsetMin = "-33 -32.5", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareNine",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1503769 0.3072483", AnchorMax = "0.1503769 0.3072483", OffsetMin = "-33 -32.5", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareNine",
                            Name = LayerWorkbenchLevelOne + ".IconNine",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconNine",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["jacket"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var JacketCommand = StudyTreeUser[player.userID].SnowJacket != true ? "Give_Info_Blueprints_Workbench_StudyTree jacket" : "Give_Blueprints_Workbench_StudyTree jacket";
                    
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = JacketCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareNine");

                        #endregion
                        
                        #region 10. Отрисовка квадрата и картинки предмета: hammer.salvaged
                        
                        var SalvagedHammer = StudyTreeUser[player.userID].SalvagedHammer == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SalvagedHammer },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.9341995", AnchorMax = "0.4683265 0.9341995", OffsetMin = "-33.5 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.9341995", AnchorMax = "0.4683265 0.9341995", OffsetMin = "-33.5 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTen",
                            Name = LayerWorkbenchLevelOne + ".IconTen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["hammer.salvaged"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree hammer.salvaged" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTen");

                        #endregion
                        
                        #region 11. Отрисовка квадрата и картинки предмета: salvaged.cleaver
                        
                        var SalvagedCleaver = StudyTreeUser[player.userID].SalvagedCleaver == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareElevenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SalvagedCleaver },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.808431", AnchorMax = "0.4209891 0.808431", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareEleven",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.808431", AnchorMax = "0.4209891 0.808431", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareEleven",
                            Name = LayerWorkbenchLevelOne + ".IconEleven",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconEleven",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["salvaged.cleaver"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SalvagedCleaverCommand = StudyTreeUser[player.userID].SalvagedHammer != true ? "Give_Info_Blueprints_Workbench_StudyTree salvaged.cleaver" : "Give_Blueprints_Workbench_StudyTree salvaged.cleaver";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SalvagedCleaverCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareEleven");

                        #endregion
                        
                        #region 12. Отрисовка квадрата и картинки предмета: mace
                        
                        var Mace = StudyTreeUser[player.userID].Mace == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwelveUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Mace },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.808431", AnchorMax = "0.5156633 0.808431", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwelve",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.808431", AnchorMax = "0.5156633 0.808431", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwelve",
                            Name = LayerWorkbenchLevelOne + ".IconTwelve",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwelve",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["mace"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var MaceCommand = StudyTreeUser[player.userID].SalvagedHammer != true ? "Give_Info_Blueprints_Workbench_StudyTree mace" : "Give_Blueprints_Workbench_StudyTree mace";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = MaceCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwelve");

                        #endregion
                        
                        #region 13. Отрисовка квадрата и картинки предмета: trap.bear
                        
                        var SnapTrap = StudyTreeUser[player.userID].SnapTrap == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFourteenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SnapTrap },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.6826625", AnchorMax = "0.4683265 0.6826625", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFourteen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.6826625", AnchorMax = "0.4683265 0.6826625", OffsetMin = "-34 -33", OffsetMax = "33 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareFourteen",
                            Name = LayerWorkbenchLevelOne + ".IconFourteen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconFourteen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["trap.bear"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SnapTrapCommand = StudyTreeUser[player.userID].SalvagedCleaver != true || StudyTreeUser[player.userID].Mace != true ? "Give_Info_Blueprints_Workbench_StudyTree trap.bear" : "Give_Blueprints_Workbench_StudyTree trap.bear";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SnapTrapCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareFourteen");

                        #endregion
                        
                        #region 14. Отрисовка квадрата и картинки предмета: flameturret
                        
                        var FlameTurret = StudyTreeUser[player.userID].FlameTurret == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFifteenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = FlameTurret },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.556894", AnchorMax = "0.4209891 0.556894", OffsetMin = "-33 -33", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareFifteen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.556894", AnchorMax = "0.4209891 0.556894", OffsetMin = "-33 -33", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareFifteen",
                            Name = LayerWorkbenchLevelOne + ".IconFifteen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconFifteen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["flameturret"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var FlameTurretCommand = StudyTreeUser[player.userID].SnapTrap != true ? "Give_Info_Blueprints_Workbench_StudyTree flameturret" : "Give_Blueprints_Workbench_StudyTree flameturret";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = FlameTurretCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareFifteen");

                        #endregion
                        
                        #region 15. Отрисовка квадрата и картинки предмета: guntrap
                        
                        var ShotgunTrap = StudyTreeUser[player.userID].ShotgunTrap == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSixteenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = ShotgunTrap },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.556894", AnchorMax = "0.5156633 0.556894", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareSixteen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.556894", AnchorMax = "0.5156633 0.556894", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareSixteen",
                            Name = LayerWorkbenchLevelOne + ".IconSixteen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconSixteen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["guntrap"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var ShotgunTrapCommand = StudyTreeUser[player.userID].SnapTrap != true ? "Give_Info_Blueprints_Workbench_StudyTree guntrap" : "Give_Blueprints_Workbench_StudyTree guntrap";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = ShotgunTrapCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareSixteen");

                        #endregion
                        
                        #region 16. Отрисовка квадрата и картинки предмета: ammo.pistol
                        
                        var PistolBullet = StudyTreeUser[player.userID].PistolBullet == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareEighteenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = PistolBullet },
                                new CuiRectTransformComponent { AnchorMin = "0.3736517 0.4311255", AnchorMax = "0.3736517 0.4311255", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareEighteen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.3736517 0.4311255", AnchorMax = "0.3736517 0.4311255", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareEighteen",
                            Name = LayerWorkbenchLevelOne + ".IconEighteen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconEighteen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["ammo.pistol"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var PistolBulletCommand = StudyTreeUser[player.userID].FlameTurret != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.pistol" : "Give_Blueprints_Workbench_StudyTree ammo.pistol";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = PistolBulletCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareEighteen");

                        #endregion
                        
                        #region 17. Отрисовка квадрата и картинки предмета: weapon.mod.silencer
                        
                        var Silencer = StudyTreeUser[player.userID].Silencer == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareNineteenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Silencer },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.4311255", AnchorMax = "0.4683265 0.4311255", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareNineteen",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.4311255", AnchorMax = "0.4683265 0.4311255", OffsetMin = "-33 -33.5", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareNineteen",
                            Name = LayerWorkbenchLevelOne + ".IconNineteen",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconNineteen",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["weapon.mod.silencer"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SilencerCommand = StudyTreeUser[player.userID].ShotgunTrap != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.silencer" : "Give_Blueprints_Workbench_StudyTree weapon.mod.silencer";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SilencerCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareNineteen");

                        #endregion
                        
                        #region 18. Отрисовка квадрата и картинки предмета: shotgun.waterpipe
                        
                        var WaterpipeShotgun = StudyTreeUser[player.userID].WaterpipeShotgun == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = WaterpipeShotgun },
                                new CuiRectTransformComponent { AnchorMin = "0.562999 0.4311255", AnchorMax = "0.562999 0.4311255", OffsetMin = "-33.5 -33", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwenty",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.562999 0.4311255", AnchorMax = "0.562999 0.4311255", OffsetMin = "-33.5 -33", OffsetMax = "33.5 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwenty",
                            Name = LayerWorkbenchLevelOne + ".IconTwenty",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwenty",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["shotgun.waterpipe"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var WaterpipeShotgunCommand = StudyTreeUser[player.userID].ShotgunTrap != true ? "Give_Info_Blueprints_Workbench_StudyTree shotgun.waterpipe" : "Give_Blueprints_Workbench_StudyTree shotgun.waterpipe";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = WaterpipeShotgunCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwenty");

                        #endregion
                        
                        #region 19. Отрисовка квадрата и картинки предмета: pistol.revolver
                        
                        var Revolver = StudyTreeUser[player.userID].Revolver == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyOneUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Revolver },
                                new CuiRectTransformComponent { AnchorMin = "0.3736517 0.3072483", AnchorMax = "0.3736517 0.3072483", OffsetMin = "-33 -33", OffsetMax = "33.5 33.3" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyOne",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.3736517 0.3072483", AnchorMax = "0.3736517 0.3072483", OffsetMin = "-33 -33", OffsetMax = "33.5 33.3" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyOne",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyOne",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyOne",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["pistol.revolver"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var RevolverCommand = StudyTreeUser[player.userID].PistolBullet != true ? "Give_Info_Blueprints_Workbench_StudyTree pistol.revolver" : "Give_Blueprints_Workbench_StudyTree pistol.revolver";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = RevolverCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyOne");

                        #endregion
                        
                        #region 20. Отрисовка квадрата и картинки предмета: weapon.mod.flashlight
                        
                        var WeaponFlashlight = StudyTreeUser[player.userID].WeaponFlashlight == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyTwoUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = WeaponFlashlight },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.3072483", AnchorMax = "0.4683265 0.3072483", OffsetMin = "-33 -33", OffsetMax = "33.5 33.3" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4683265 0.3072483", AnchorMax = "0.4683265 0.3072483", OffsetMin = "-33 -33", OffsetMax = "33.5 33.3" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyTwo",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyTwo",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["weapon.mod.flashlight"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var WeaponFlashlightCommand = StudyTreeUser[player.userID].Silencer != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.flashlight" : "Give_Blueprints_Workbench_StudyTree weapon.mod.flashlight";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = WeaponFlashlightCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyTwo");

                        #endregion
                        
                        #region 21. Отрисовка квадрата и картинки предмета: shotgun.double
                        
                        var DoubleBarrelShotgun = StudyTreeUser[player.userID].DoubleBarrelShotgun == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyThreeUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = DoubleBarrelShotgun },
                                new CuiRectTransformComponent { AnchorMin = "0.562999 0.3072483", AnchorMax = "0.562999 0.3072483", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyThree",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.562999 0.3072483", AnchorMax = "0.562999 0.3072483", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyThree",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyThree",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyThree",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["shotgun.double"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var DoubleBarrelShotgunCommand = StudyTreeUser[player.userID].WaterpipeShotgun != true ? "Give_Info_Blueprints_Workbench_StudyTree shotgun.double" : "Give_Blueprints_Workbench_StudyTree shotgun.double";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = DoubleBarrelShotgunCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyThree");

                        #endregion
                        
                        #region 22. Отрисовка квадрата и картинки предмета: explosive.satchel
                        
                        var SatchelCharge = StudyTreeUser[player.userID].SatchelCharge == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyFourUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SatchelCharge },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.1814798", AnchorMax = "0.4209891 0.1814798", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyFour",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4209891 0.1814798", AnchorMax = "0.4209891 0.1814798", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyFour",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyFour",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyFour",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["explosive.satchel"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SatchelChargeCommand = StudyTreeUser[player.userID].Revolver != true ? "Give_Info_Blueprints_Workbench_StudyTree explosive.satchel" : "Give_Blueprints_Workbench_StudyTree explosive.satchel";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SatchelChargeCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyFour");

                        #endregion
                        
                        #region 23. Отрисовка квадрата и картинки предмета: grenade.beancan
                        
                        var BeancanGrenade = StudyTreeUser[player.userID].BeancanGrenade == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyFiveUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = SatchelCharge },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.1814798", AnchorMax = "0.5156633 0.1814798", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyFive",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.5156633 0.1814798", AnchorMax = "0.5156633 0.1814798", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyFive",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyFive",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyFive",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["grenade.beancan"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var BeancanGrenadeCommand = StudyTreeUser[player.userID].Revolver != true ? "Give_Info_Blueprints_Workbench_StudyTree grenade.beancan" : "Give_Blueprints_Workbench_StudyTree grenade.beancan";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = BeancanGrenadeCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyFive");

                        #endregion
                        
                        #region 24. Отрисовка квадрата и картинки предмета: barricade.woodwire
                        
                        var BarbedWoodenBarricade = StudyTreeUser[player.userID].BarbedWoodenBarricade == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentySixUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = BarbedWoodenBarricade },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.9341995", AnchorMax = "0.8407013 0.9341995", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentySix",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.9341995", AnchorMax = "0.8407013 0.9341995", OffsetMin = "-33 -33.3", OffsetMax = "33.3 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentySix",
                            Name = LayerWorkbenchLevelOne + ".IconTwentySix",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentySix",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["barricade.woodwire"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree barricade.woodwire" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentySix");

                        #endregion
                        
                        #region 25. Отрисовка квадрата и картинки предмета: water.catcher.small
                        
                        var SmallWaterCatcher = StudyTreeUser[player.userID].SmallWaterCatcher == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentySevenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = BarbedWoodenBarricade },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.808431", AnchorMax = "0.8407013 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentySeven",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.808431", AnchorMax = "0.8407013 0.808431", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentySeven",
                            Name = LayerWorkbenchLevelOne + ".IconTwentySeven",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentySeven",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["water.catcher.small"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var SmallWaterCatcherCommand = StudyTreeUser[player.userID].BarbedWoodenBarricade != true ? "Give_Info_Blueprints_Workbench_StudyTree water.catcher.small" : "Give_Blueprints_Workbench_StudyTree water.catcher.small";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = SmallWaterCatcherCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentySeven");

                        #endregion
                        
                        #region 26. Отрисовка квадрата и картинки предмета: shutter.metal.embrasure.b
                        
                        var MetalVerticalEmbrasure = StudyTreeUser[player.userID].MetalVerticalEmbrasure == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyEightUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MetalVerticalEmbrasure },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.6826625", AnchorMax = "0.7933657 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyEight",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.6826625", AnchorMax = "0.7933657 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyEight",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyEight",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyEight",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["shutter.metal.embrasure.b"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var MetalVerticalEmbrasureCommand = StudyTreeUser[player.userID].SmallWaterCatcher != true ? "Give_Info_Blueprints_Workbench_StudyTree shutter.metal.embrasure.b" : "Give_Blueprints_Workbench_StudyTree shutter.metal.embrasure.b";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = MetalVerticalEmbrasureCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyEight");

                        #endregion
                        
                        #region 27. Отрисовка квадрата и картинки предмета: shutter.metal.embrasure.a
                        
                        var MetalHorizontalEmbrasure = StudyTreeUser[player.userID].MetalHorizontalEmbrasure == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyNineUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MetalHorizontalEmbrasure },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.6826625", AnchorMax = "0.8880369 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareTwentyNine",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.6826625", AnchorMax = "0.8880369 0.6826625", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareTwentyNine",
                            Name = LayerWorkbenchLevelOne + ".IconTwentyNine",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconTwentyNine",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["shutter.metal.embrasure.a"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var MetalHorizontalEmbrasureCommand = StudyTreeUser[player.userID].SmallWaterCatcher != true ? "Give_Info_Blueprints_Workbench_StudyTree shutter.metal.embrasure.a" : "Give_Blueprints_Workbench_StudyTree shutter.metal.embrasure.a";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = MetalHorizontalEmbrasureCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareTwentyNine");

                        #endregion
                        
                        #region 28. Отрисовка квадрата и картинки предмета: floor.grill
                        
                        var FloorGrill = StudyTreeUser[player.userID].FloorGrill == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = FloorGrill },
                                new CuiRectTransformComponent { AnchorMin = "0.74603 0.556894", AnchorMax = "0.74603 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirty",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.74603 0.556894", AnchorMax = "0.74603 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirty",
                            Name = LayerWorkbenchLevelOne + ".IconThirty",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirty",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["floor.grill"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var FloorGrillCommand = StudyTreeUser[player.userID].MetalHorizontalEmbrasure != true ? "Give_Info_Blueprints_Workbench_StudyTree floor.grill" : "Give_Blueprints_Workbench_StudyTree floor.grill";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = FloorGrillCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirty");

                        #endregion
                        
                        #region 29. Отрисовка квадрата и картинки предмета: wall.window.bars.metal
                        
                        var MetalWindowBars = StudyTreeUser[player.userID].MetalWindowBars == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyOneUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MetalWindowBars },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.556894", AnchorMax = "0.8407013 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyOne",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.556894", AnchorMax = "0.8407013 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyOne",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyOne",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyOne",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["wall.window.bars.metal"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var MetalWindowBarsCommand = StudyTreeUser[player.userID].MetalHorizontalEmbrasure != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.window.bars.metal" : "Give_Blueprints_Workbench_StudyTree wall.window.bars.metal";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = MetalWindowBarsCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyOne");

                        #endregion
                        
                        #region 30. Отрисовка квадрата и картинки предмета: watchtower.wood
                        
                        var WatchTower = StudyTreeUser[player.userID].WatchTower == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyTwoUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = WatchTower },
                                new CuiRectTransformComponent { AnchorMin = "0.9353725 0.556894", AnchorMax = "0.9353725 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.9353725 0.556894", AnchorMax = "0.9353725 0.556894", OffsetMin = "-33 -33", OffsetMax = "34 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyTwo",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyTwo",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["watchtower.wood"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var WatchTowerCommand = StudyTreeUser[player.userID].MetalHorizontalEmbrasure != true ? "Give_Info_Blueprints_Workbench_StudyTree watchtower.wood" : "Give_Blueprints_Workbench_StudyTree watchtower.wood";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = WatchTowerCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyTwo");

                        #endregion
                        
                        #region 31. Отрисовка квадрата и картинки предмета: ladder.wooden.wall
                        
                        var WoodenLadder = StudyTreeUser[player.userID].WoodenLadder == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyThreeUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = WoodenLadder },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.4311255", AnchorMax = "0.7933657 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyThree",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.4311255", AnchorMax = "0.7933657 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyThree",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyThree",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyThree",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["ladder.wooden.wall"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var WoodenLadderCommand = StudyTreeUser[player.userID].FloorGrill != true ? "Give_Info_Blueprints_Workbench_StudyTree ladder.wooden.wall" : "Give_Blueprints_Workbench_StudyTree ladder.wooden.wall";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = WoodenLadderCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyThree");

                        #endregion
                        
                        #region 32. Отрисовка квадрата и картинки предмета: bed
                        
                        var Bed = StudyTreeUser[player.userID].Bed == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyFourUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Bed },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.4311255", AnchorMax = "0.8880369 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyFour",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.4311255", AnchorMax = "0.8880369 0.4311255", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyFour",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyFour",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyFour",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["bed"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var BedCommand = StudyTreeUser[player.userID].FloorGrill != true ? "Give_Info_Blueprints_Workbench_StudyTree bed" : "Give_Blueprints_Workbench_StudyTree bed";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = BedCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyFour");

                        #endregion
                        
                        #region 33. Отрисовка квадрата и картинки предмета: dropbox
                        
                        var DropBox = StudyTreeUser[player.userID].DropBox == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyFiveUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = DropBox },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.3072483", AnchorMax = "0.8407013 0.3072483", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyFive",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8407013 0.3072483", AnchorMax = "0.8407013 0.3072483", OffsetMin = "-33.3 -33", OffsetMax = "34 33.3" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyFive",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyFive",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyFive",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["dropbox"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var DropBoxCommand = StudyTreeUser[player.userID].WoodenLadder != true ? "Give_Info_Blueprints_Workbench_StudyTree dropbox" : "Give_Blueprints_Workbench_StudyTree dropbox";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = DropBoxCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyFive");

                        #endregion
                        
                        #region 34. Отрисовка квадрата и картинки предмета: wall.external.high
                        
                        var HighExternalWoodenWall = StudyTreeUser[player.userID].HighExternalWoodenWall == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtySixUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = HighExternalWoodenWall },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.1814798", AnchorMax = "0.7933657 0.1814798", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtySix",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.1814798", AnchorMax = "0.7933657 0.1814798", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtySix",
                            Name = LayerWorkbenchLevelOne + ".IconThirtySix",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtySix",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["wall.external.high"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var HighExternalWoodenWallCommand = StudyTreeUser[player.userID].DropBox != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.external.high" : "Give_Blueprints_Workbench_StudyTree wall.external.high";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = HighExternalWoodenWallCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtySix");

                        #endregion
                        
                        #region 35. Отрисовка квадрата и картинки предмета: wall.frame.garagedoor
                        
                        var GarageDoor = StudyTreeUser[player.userID].GarageDoor == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyEightUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = GarageDoor },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.1814798", AnchorMax = "0.8880369 0.1814798", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtyEight",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.8880369 0.1814798", AnchorMax = "0.8880369 0.1814798", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtyEight",
                            Name = LayerWorkbenchLevelOne + ".IconThirtyEight",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtyEight",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["wall.frame.garagedoor"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var GarageDoorCommand = StudyTreeUser[player.userID].DropBox != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.frame.garagedoor" : "Give_Blueprints_Workbench_StudyTree wall.frame.garagedoor";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = GarageDoorCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtyEight");

                        #endregion
                        
                        #region 36. Отрисовка квадрата и картинки предмета: gates.external.high.wood
                        
                        var HighExternalWoodenGate = StudyTreeUser[player.userID].HighExternalWoodenGate == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtySevenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = HighExternalWoodenGate },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.05760257", AnchorMax = "0.7933657 0.05760257", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".BG",
                            Name = LayerWorkbenchLevelOne + ".SquareThirtySeven",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.7933657 0.05760257", AnchorMax = "0.7933657 0.05760257", OffsetMin = "-33 -33", OffsetMax = "34 33.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".SquareThirtySeven",
                            Name = LayerWorkbenchLevelOne + ".IconThirtySeven",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelOne + ".IconThirtySeven",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["gates.external.high.wood"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var HighExternalWoodenGateCommand = StudyTreeUser[player.userID].HighExternalWoodenWall != true ? "Give_Info_Blueprints_Workbench_StudyTree gates.external.high.wood" : "Give_Blueprints_Workbench_StudyTree gates.external.high.wood";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = HighExternalWoodenGateCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelOne + ".SquareThirtySeven");

                        #endregion
                        
                        DestroyWorkbenchLevelOneUI(player);
                        CuiHelper.AddUi(player, container);
                        yield return new WaitForSeconds(2f);
                    }
                    player.SetFlag(BaseEntity.Flags.Reserved3, false);
                    yield break;
                }
                
                void DestroyWorkbenchLevelOneUI(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareSixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareSevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareElevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwelveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFourteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFifteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareSixteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareSeventeenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareEighteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareNineteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentySixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentySevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareTwentyNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyFiveUpdate"); 
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtySixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtySevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareThirtyNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortyFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelOne + ".SquareFortySixUpdate");
                }
				
				[ChatCommand("l2")]
                private void UIWorkbenchLevelTwoT(BasePlayer player)
                {
					SendReply(player, "Чтобы открыть дерево изучений вам необходимо подойти к второму верстаку и нажать букву Е смотря на него.");
				}
				
                private void UIWorkbenchLevelTwo(BasePlayer player)
                {
                    if (!ImageInit) return;
                    
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
                    {
                        BaseCombatEntity entity = null;
                        try
                        {
                            entity = hit.GetEntity() as BaseCombatEntity;
                        }
                        catch
                        {
                            return;
                        }

                        if (entity.ShortPrefabName.Contains("workbench2.deployed"))
                        {

                            if (player.HasFlag(BaseEntity.Flags.Reserved3)) return;
                            player.SetFlag(BaseEntity.Flags.Reserved3, true);

                            CuiElementContainer container = new CuiElementContainer();

                            container.Add(new CuiPanel
                            {
                                CursorEnabled = true,
                                Image = {Color = "0 0 0 0"},
                                RectTransform =
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-630 -350",
                                    OffsetMax = "320 350"
                                },
                            }, "Overlay", LayerWorkbenchLevelTwo);

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo,
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(BackgroundСolor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -1000",
                                        OffsetMax = "1000 1000"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo,
                                Name = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = "0 0 0 0"},
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                                }
                            });

                            #region Отрисовка линий дерева изучений

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1412846 0.8725188", AnchorMax = "0.1412846 0.8725188",
                                        OffsetMin = "-88 -1",
                                        OffsetMax = "88 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2333315 0.8725188", AnchorMax = "0.2333315 0.8725188",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1406998 0.8648999", AnchorMax = "0.1406998 0.8648999",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.04947162 0.8658522", AnchorMax = "0.04947162 0.8658522",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.05087513 0.7477592", AnchorMax = "0.05087513 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1406998 0.7477592", AnchorMax = "0.1406998 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2333315 0.7477592", AnchorMax = "0.2333315 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.05087513 0.6249044", AnchorMax = "0.05087513 0.6249044",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.05087513 0.5001448", AnchorMax = "0.05087513 0.5001448",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.05087513 0.3772862", AnchorMax = "0.05087513 0.3772862",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3575422 0.8715664", AnchorMax = "0.3575422 0.8715664",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3575422 0.7487116", AnchorMax = "0.3575422 0.7487116",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3575422 0.6249044", AnchorMax = "0.3575422 0.6249044",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3575422 0.5010972", AnchorMax = "0.3575422 0.5010972",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4838581 0.7477592", AnchorMax = "0.4838581 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5680659 0.7477592", AnchorMax = "0.5680659 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4838581 0.6249044", AnchorMax = "0.4838581 0.6249044",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4838581 0.5010972", AnchorMax = "0.4838581 0.5010972",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4838581 0.3772862", AnchorMax = "0.4838581 0.3772862",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.75683 0.7477592", AnchorMax = "0.75683 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8410371 0.7477592", AnchorMax = "0.8410371 0.7477592",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7547249 0.623952", AnchorMax = "0.7547249 0.623952",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8410371 0.623952", AnchorMax = "0.8410371 0.623952",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9273493 0.623952", AnchorMax = "0.9273493 0.623952",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8410371 0.5010972", AnchorMax = "0.8410371 0.5010972",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9273493 0.5010972", AnchorMax = "0.9273493 0.5010972",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9273493 0.3772862", AnchorMax = "0.9273493 0.3772862",
                                        OffsetMin = "-1 -11", OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.09508568 0.3134758", AnchorMax = "0.09508568 0.3134758",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1863139 0.3134758", AnchorMax = "0.1863139 0.3134758",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5308744 0.316333", AnchorMax = "0.5308744 0.316333",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.6220987 0.316333", AnchorMax = "0.6220987 0.316333",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.6220987 0.4391917", AnchorMax = "0.6220987 0.4391917",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5308744 0.4391917", AnchorMax = "0.5308744 0.4391917",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5308744 0.06776136", AnchorMax = "0.5308744 0.06776136",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.6220987 0.06776136", AnchorMax = "0.6220987 0.06776136",
                                        OffsetMin = "-11 -1", OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4852616 0.1268088", AnchorMax = "0.4852616 0.1268088",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4859634 0.2515703", AnchorMax = "0.4859634 0.2515703",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5259624 0.8725188", AnchorMax = "0.5259624 0.8725188",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7989336 0.8725188", AnchorMax = "0.7989336 0.8725188",
                                        OffsetMin = "-41 -1", OffsetMax = "41 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8845441 0.7487116", AnchorMax = "0.8845441 0.7487116",
                                        OffsetMin = "-42 -1", OffsetMax = "42 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5259624 0.8791853", AnchorMax = "0.5259624 0.8791853",
                                        OffsetMin = "-1 -5", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4837801 0.8649932", AnchorMax = "0.4837801 0.8649932",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5679879 0.8649932", AnchorMax = "0.5679879 0.8649932",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4837801 0.2602352", AnchorMax = "0.4837801 0.2602352",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4437801 0.244997", AnchorMax = "0.4437801 0.244997",
                                        OffsetMin = "-1 -5", OffsetMax = "1 5"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5279896 0.244997", AnchorMax = "0.5279896 0.244997",
                                        OffsetMin = "-1 -5", OffsetMax = "1 5"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4430783 0.1354735", AnchorMax = "0.4430783 0.1354735",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5272878 0.1354735", AnchorMax = "0.5272878 0.1354735",
                                        OffsetMin = "-1 -7", OffsetMax = "1 7"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4858854 0.1202355", AnchorMax = "0.4858854 0.1202355",
                                        OffsetMin = "-1 -5", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7988556 0.8792787", AnchorMax = "0.7988556 0.8792787",
                                        OffsetMin = "-1 -5", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.8409591 0.8649932", AnchorMax = "0.8409591 0.8649932",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7567521 0.8649932", AnchorMax = "0.7567521 0.8649932",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelTwo + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.9279731 0.7421384", AnchorMax = "0.9279731 0.7421384",
                                        OffsetMin = "-1 -6", OffsetMax = "1 6"
                                    }
                                }
                            });


                            #endregion


                            CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo);
                            CuiHelper.AddUi(player, container);

                            PanelBlueprintsWorkbenchStudyTree(player);
                            ServerMgr.Instance.StartCoroutine(UpdateWorkbenchLevelTwo(player));
                        }
                        else
                        {
                            SendReply(player, "Чтобы открыть дерево изучений вам необходимо смотреть на второй верстак.");
                        }
                    }
                }
                
                private IEnumerator UpdateWorkbenchLevelTwo(BasePlayer player)
                {
                    while (player.HasFlag(BaseEntity.Flags.Reserved3) && player.IsConnected)
                    {
                        CuiElementContainer container = new CuiElementContainer();
                        
                        #region 1. Отрисовка квадрата и картинки предмета: hazmatsuit
                        
                        var HazmatSuitColor = StudyTreeUser[player.userID].HazmatSuit == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelTwo + ".BG",
                            Name = LayerWorkbenchLevelTwo + ".SquareOneUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = HazmatSuitColor },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.2326298 0.9344954", AnchorMax = "0.2326298 0.9344954", OffsetMin = "-33.5 -33.5",
                                    OffsetMax = "33 33"
                                }
                            }
                        });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareOne",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2326298 0.9344954", AnchorMax = "0.2326298 0.9344954", OffsetMin = "-33.5 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareOne",
                        Name = LayerWorkbenchLevelTwo + ".IconOne",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconOne",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["hazmatsuit"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree hazmatsuit"},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareOne");

                    #endregion

                        #region 2. Отрисовка квадрата и картинки предмета: coffeecan.helmet
                    
                    var CoffeeCanHelmetColor = StudyTreeUser[player.userID].CoffeeCanHelmet == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwoUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = CoffeeCanHelmetColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.8106882", AnchorMax = "0.05017346 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.05017346 0.8106882", AnchorMax = "0.05017346 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwo",
                        Name = LayerWorkbenchLevelTwo + ".IconTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwo",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["coffeecan.helmet"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var CoffeeCanHelmetCommand = StudyTreeUser[player.userID].HazmatSuit != true ? "Give_Info_Blueprints_Workbench_StudyTree coffeecan.helmet" : "Give_Blueprints_Workbench_StudyTree coffeecan.helmet";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = CoffeeCanHelmetCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwo");

                    #endregion

                        #region 3. Отрисовка квадрата и картинки предмета: shoes.boots
                    
                    var BootsColor = StudyTreeUser[player.userID].Boots == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThreeUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = BootsColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.1406999 0.8106882", AnchorMax = "0.1406999 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThree",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1406999 0.8106882", AnchorMax = "0.1406999 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThree",
                        Name = LayerWorkbenchLevelTwo + ".IconThree",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThree",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["shoes.boots"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var BootsCommand = StudyTreeUser[player.userID].HazmatSuit != true ? "Give_Info_Blueprints_Workbench_StudyTree shoes.boots" : "Give_Blueprints_Workbench_StudyTree shoes.boots";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = BootsCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThree");

                    #endregion

                        #region 4. Отрисовка квадрата и картинки предмета: syringe.medical
                    
                    var MedicalSyringeolor = StudyTreeUser[player.userID].MedicalSyringe == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFourUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = MedicalSyringeolor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.2319281 0.8106882", AnchorMax = "0.2319281 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFour",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2319281 0.8106882", AnchorMax = "0.2319281 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFour",
                        Name = LayerWorkbenchLevelTwo + ".IconFour",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFour",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["syringe.medical"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var MedicalSyringeCommand = StudyTreeUser[player.userID].HazmatSuit != true ? "Give_Info_Blueprints_Workbench_StudyTree syringe.medical" : "Give_Blueprints_Workbench_StudyTree syringe.medical";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = MedicalSyringeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFour");

                    #endregion

                        #region 5. Отрисовка квадрата и картинки предмета: roadsign.jacket
                    
                    var RoadSingJacketColor = StudyTreeUser[player.userID].RoadSingJacket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFiveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = RoadSingJacketColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.686881", AnchorMax = "0.05017346 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFive",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.686881", AnchorMax = "0.05017346 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFive",
                        Name = LayerWorkbenchLevelTwo + ".IconFive",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFive",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["roadsign.jacket"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var RoadSingJacketCommand = StudyTreeUser[player.userID].CoffeeCanHelmet != true ? "Give_Info_Blueprints_Workbench_StudyTree roadsign.jacket" : "Give_Blueprints_Workbench_StudyTree roadsign.jacket";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = RoadSingJacketCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFive");

                    #endregion

                        #region 6. Отрисовка квадрата и картинки предмета: hoodie
                    
                    var HoodieColor = StudyTreeUser[player.userID].Hoodie == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSixUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HoodieColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.1406999 0.686881", AnchorMax = "0.1406999 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSix",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1406999 0.686881", AnchorMax = "0.1406999 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareSix",
                        Name = LayerWorkbenchLevelTwo + ".IconSix",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconSix",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["hoodie"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HoodieCommand = StudyTreeUser[player.userID].Boots != true ? "Give_Info_Blueprints_Workbench_StudyTree hoodie" : "Give_Blueprints_Workbench_StudyTree hoodie";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HoodieCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareSix");

                    #endregion

                        #region 7. Отрисовка квадрата и картинки предмета: largemedkit
                    
                    var LargeMedkitColor = StudyTreeUser[player.userID].LargeMedkit == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSevenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = LargeMedkitColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.2326298 0.686881", AnchorMax = "0.2326298 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSeven",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2326298 0.686881", AnchorMax = "0.2326298 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareSeven",
                        Name = LayerWorkbenchLevelTwo + ".IconSeven",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconSeven",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["largemedkit"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var LargeMedkitCommand = StudyTreeUser[player.userID].MedicalSyringe != true ? "Give_Info_Blueprints_Workbench_StudyTree largemedkit" : "Give_Blueprints_Workbench_StudyTree largemedkit";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = LargeMedkitCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareSeven");

                    #endregion

                    #region 8. Отрисовка квадрата и картинки предмета: roadsign.kilt
                    
                    var RoadSingKiltColor = StudyTreeUser[player.userID].RoadSingKilt == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareEightUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = RoadSingKiltColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.5630738", AnchorMax = "0.05017346 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareEight",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.05017346 0.5630738", AnchorMax = "0.05017346 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareEight",
                        Name = LayerWorkbenchLevelTwo + ".IconEight",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconEight",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["roadsign.kilt"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var RoadSingKiltCommand = StudyTreeUser[player.userID].RoadSingJacket != true ? "Give_Info_Blueprints_Workbench_StudyTree roadsign.kilt" : "Give_Blueprints_Workbench_StudyTree roadsign.kilt";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = RoadSingKiltCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareEight");

                    #endregion

                    #region 9. Отрисовка квадрата и картинки предмета: roadsign.gloves
                    
                    var RoadsingGlovesColor = StudyTreeUser[player.userID].RoadsingGloves == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareNineUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = RoadsingGlovesColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.4392647", AnchorMax = "0.05017346 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareNine",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.05017346 0.4392647", AnchorMax = "0.05017346 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareNine",
                        Name = LayerWorkbenchLevelTwo + ".IconNine",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconNine",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["roadsign.gloves"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var RoadsingGlovesCommand = StudyTreeUser[player.userID].RoadSingKilt != true ? "Give_Info_Blueprints_Workbench_StudyTree roadsign.gloves" : "Give_Blueprints_Workbench_StudyTree roadsign.gloves";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = RoadsingGlovesCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareNine");

                    #endregion

                    #region 10. Отрисовка квадрата и картинки предмета: heavy.plate.helmet
                    
                    var HeavyPlateHelmet = StudyTreeUser[player.userID].HeavyPlateHelmet == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HeavyPlateHelmet },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05017346 0.3154536", AnchorMax = "0.05017346 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.05017346 0.3154536", AnchorMax = "0.05017346 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTen",
                        Name = LayerWorkbenchLevelTwo + ".IconTen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["heavy.plate.helmet"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HeavyPlateHelmetCommand = StudyTreeUser[player.userID].RoadsingGloves != true ? "Give_Info_Blueprints_Workbench_StudyTree heavy.plate.helmet" : "Give_Blueprints_Workbench_StudyTree heavy.plate.helmet";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HeavyPlateHelmetCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTen");

                    #endregion

                    #region 11. Отрисовка квадрата и картинки предмета: heavy.plate.jacket

                    var HeavyPlateJacket = StudyTreeUser[player.userID].HeavyPlateJacket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareElevenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HeavyPlateJacket },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.1406999 0.3154536", AnchorMax = "0.1406999 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareEleven",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1406999 0.3154536", AnchorMax = "0.1406999 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareEleven",
                        Name = LayerWorkbenchLevelTwo + ".IconEleven",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconEleven",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["heavy.plate.jacket"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var HeavyPlateJacketCommand = StudyTreeUser[player.userID].HeavyPlateHelmet != true ? "Give_Info_Blueprints_Workbench_StudyTree heavy.plate.jacket" : "Give_Blueprints_Workbench_StudyTree heavy.plate.jacket";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HeavyPlateJacketCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareEleven");

                    #endregion

                    #region 12. Отрисовка квадрата и картинки предмета: heavy.plate.pants
                    
                    var HeavyPlatePants = StudyTreeUser[player.userID].HeavyPlatePants == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwelveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HeavyPlatePants },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.2319281 0.3154536", AnchorMax = "0.2319281 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwelve",
                        Components =
                        {
                            new CuiImageComponent { Color = "0 0 0 0" },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2319281 0.3154536", AnchorMax = "0.2319281 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwelve",
                        Name = LayerWorkbenchLevelTwo + ".IconTwelve",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwelve",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["heavy.plate.pants"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HeavyPlatePantsCommand = StudyTreeUser[player.userID].HeavyPlateJacket != true ? "Give_Info_Blueprints_Workbench_StudyTree heavy.plate.pants" : "Give_Blueprints_Workbench_StudyTree heavy.plate.pants";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HeavyPlatePantsCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwelve");

                    #endregion

                    #region 13. Отрисовка квадрата и картинки предмета: longsword
                    
                    var Longsword = StudyTreeUser[player.userID].Longsword == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Longsword },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.3589458 0.9344954", AnchorMax = "0.3589458 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3589458 0.9344954", AnchorMax = "0.3589458 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirteen",
                        Name = LayerWorkbenchLevelTwo + ".IconThirteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["longsword"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree longsword"},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirteen");

                    #endregion

                    #region 14. Отрисовка квадрата и картинки предмета: axe.salvaged
                    
                    var SalvegedAxe = StudyTreeUser[player.userID].SalvegedAxe == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFourteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = SalvegedAxe },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.3589458 0.8106882", AnchorMax = "0.3589458 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFourteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3589458 0.8106882", AnchorMax = "0.3589458 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFourteen",
                        Name = LayerWorkbenchLevelTwo + ".IconFourteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFourteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["axe.salvaged"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var SalvegedAxeCommand = StudyTreeUser[player.userID].Longsword != true ? "Give_Info_Blueprints_Workbench_StudyTree axe.salvaged" : "Give_Blueprints_Workbench_StudyTree axe.salvaged";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = SalvegedAxeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFourteen");

                    #endregion

                    #region 15. Отрисовка квадрата и картинки предмета: icepick.salvaged
                    
                    var SalvagedIcepick = StudyTreeUser[player.userID].SalvagedIcepick == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFifteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = SalvagedIcepick },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.3589458 0.686881", AnchorMax = "0.3589458 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFifteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3589458 0.686881", AnchorMax = "0.3589458 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFifteen",
                        Name = LayerWorkbenchLevelTwo + ".IconFifteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFifteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["icepick.salvaged"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var SalvagedIcepickCommand = StudyTreeUser[player.userID].SalvegedAxe != true ? "Give_Info_Blueprints_Workbench_StudyTree icepick.salvaged" : "Give_Blueprints_Workbench_StudyTree icepick.salvaged";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = SalvagedIcepickCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFifteen");

                    #endregion

                    #region 16. Отрисовка квадрата и картинки предмета: chainsaw
                    
                    var Chainsaw = StudyTreeUser[player.userID].Chainsaw == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSixteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Chainsaw },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.3589458 0.5630738", AnchorMax = "0.3589458 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSixteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3589458 0.5630738", AnchorMax = "0.3589458 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareSixteen",
                        Name = LayerWorkbenchLevelTwo + ".IconSixteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconSixteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["chainsaw"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var ChainsawCommand = StudyTreeUser[player.userID].SalvagedIcepick != true ? "Give_Info_Blueprints_Workbench_StudyTree chainsaw" : "Give_Blueprints_Workbench_StudyTree chainsaw";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = ChainsawCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareSixteen");

                    #endregion

                    #region 17. Отрисовка квадрата и картинки предмета: flamethrower
                    
                    var FlameThrower = StudyTreeUser[player.userID].FlameThrower == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSeventeenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = FlameThrower },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.3589458 0.4392647", AnchorMax = "0.3589458 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareSeventeen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3589458 0.4392647", AnchorMax = "0.3589458 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareSeventeen",
                        Name = LayerWorkbenchLevelTwo + ".IconSeventeen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconSeventeen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["flamethrower"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var FlameThrowerCommand = StudyTreeUser[player.userID].Chainsaw != true ? "Give_Info_Blueprints_Workbench_StudyTree flamethrower" : "Give_Blueprints_Workbench_StudyTree flamethrower";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = FlameThrowerCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareSeventeen");

                    #endregion

                    #region 18. Отрисовка квадрата и картинки предмета: trap.landmine
                    
                    var LandMine = StudyTreeUser[player.userID].LandMine == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareEighteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = LandMine },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5273659 0.9344954", AnchorMax = "0.5273659 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareEighteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5273659 0.9344954", AnchorMax = "0.5273659 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareEighteen",
                        Name = LayerWorkbenchLevelTwo + ".IconEighteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconEighteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["trap.landmine"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree trap.landmine"},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareEighteen");

                    #endregion

                    #region 19. Отрисовка квадрата и картинки предмета: ammo.shotgun
                    
                    var Gauge12Buckshot = StudyTreeUser[player.userID].Gauge12Buckshot == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareNineteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Gauge12Buckshot },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4852617 0.8106882", AnchorMax = "0.4852617 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareNineteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4852617 0.8106882", AnchorMax = "0.4852617 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareNineteen",
                        Name = LayerWorkbenchLevelTwo + ".IconNineteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconNineteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.shotgun"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var Gauge12BuckshotCommand = StudyTreeUser[player.userID].LandMine != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.shotgun" : "Give_Blueprints_Workbench_StudyTree ammo.shotgun";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Gauge12BuckshotCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareNineteen");

                    #endregion

                    #region 20. Отрисовка квадрата и картинки предмета: ammo.shotgun.slug
                    
                    var Gauge12Slug = StudyTreeUser[player.userID].Gauge12Slug == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Gauge12Slug },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5694695 0.8106882", AnchorMax = "0.5694695 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwenty",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5694695 0.8106882", AnchorMax = "0.5694695 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwenty",
                        Name = LayerWorkbenchLevelTwo + ".IconTwenty",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwenty",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.shotgun.slug"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    var Gauge12SlugCommand = StudyTreeUser[player.userID].LandMine != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.shotgun.slug" : "Give_Blueprints_Workbench_StudyTree ammo.shotgun.slug";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Gauge12SlugCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwenty");

                    #endregion

                    #region 21. Отрисовка квадрата и картинки предмета: shotgun.pump
                    
                    var PumpShotgun = StudyTreeUser[player.userID].PumpShotgun == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyOneUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = PumpShotgun },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4859636 0.686881", AnchorMax = "0.4859636 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4859636 0.686881", AnchorMax = "0.4859636 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyOne",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyOne",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["shotgun.pump"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var PumpShotgunCommand = StudyTreeUser[player.userID].Gauge12Buckshot != true ? "Give_Info_Blueprints_Workbench_StudyTree shotgun.pump" : "Give_Blueprints_Workbench_StudyTree shotgun.pump";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = PumpShotgunCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyOne");

                    #endregion

                    #region 22. Отрисовка квадрата и картинки предмета: ammo.shotgun.fire
                    
                    var Gauge12IncendiaryShell = StudyTreeUser[player.userID].Gauge12IncendiaryShell == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyTwoUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Gauge12IncendiaryShell },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5701712 0.686881", AnchorMax = "0.5701712 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5701712 0.686881", AnchorMax = "0.5701712 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyTwo",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyTwo",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.shotgun.fire"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var Gauge12IncendiaryShellCommand = StudyTreeUser[player.userID].Gauge12Slug != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.shotgun.fire" : "Give_Blueprints_Workbench_StudyTree ammo.shotgun.fire";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command =Gauge12IncendiaryShellCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyTwo");

                    #endregion

                    #region 23. Отрисовка квадрата и картинки предмета: pistol.semiauto
                    
                    var SemiAutomaticPistol = StudyTreeUser[player.userID].SemiAutomaticPistol == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyThreeUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = SemiAutomaticPistol },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4859636 0.5630738", AnchorMax = "0.4859636 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4859636 0.5630738", AnchorMax = "0.4859636 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyThree",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyThree",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["pistol.semiauto"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var SemiAutomaticPistolCommand = StudyTreeUser[player.userID].PumpShotgun != true ? "Give_Info_Blueprints_Workbench_StudyTree pistol.semiauto" : "Give_Blueprints_Workbench_StudyTree pistol.semiauto";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = SemiAutomaticPistolCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyThree");

                    #endregion

                    #region 24. Отрисовка квадрата и картинки предмета: grenade.f1
                    
                    var F1Grenade = StudyTreeUser[player.userID].F1Grenade == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyFourUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = F1Grenade },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4859636 0.4392647", AnchorMax = "0.4859636 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4859636 0.4392647", AnchorMax = "0.4859636 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyFour",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyFour",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["grenade.f1"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var F1GrenadeCommand = StudyTreeUser[player.userID].SemiAutomaticPistol != true ? "Give_Info_Blueprints_Workbench_StudyTree grenade.f1" : "Give_Blueprints_Workbench_StudyTree grenade.f1";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = F1GrenadeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyFour");

                    #endregion

                    #region 25. Отрисовка квадрата и картинки предмета: weapon.mod.muzzlebrake
                    
                    var MuzzleBrake = StudyTreeUser[player.userID].MuzzleBrake == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyFiveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = MuzzleBrake },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5771885 0.4392647", AnchorMax = "0.5771885 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5771885 0.4392647", AnchorMax = "0.5771885 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyFive",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyFive",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["weapon.mod.muzzlebrake"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var MuzzleBrakeCommand = StudyTreeUser[player.userID].F1Grenade != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.muzzlebrake" : "Give_Blueprints_Workbench_StudyTree weapon.mod.muzzlebrake";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = MuzzleBrakeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyFive");

                    #endregion

                    #region 26. Отрисовка квадрата и картинки предмета: weapon.mod.muzzleboost
                    
                    var MuzzleBoost = StudyTreeUser[player.userID].MuzzleBoost == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentySixUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = MuzzleBoost },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.6684128 0.4392647", AnchorMax = "0.6684128 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentySix",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.6684128 0.4392647", AnchorMax = "0.6684128 0.4392647", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentySix",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentySix",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentySix",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["weapon.mod.muzzleboost"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var MuzzleBoostCommand = StudyTreeUser[player.userID].MuzzleBrake != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.muzzleboost" : "Give_Blueprints_Workbench_StudyTree weapon.mod.muzzleboost";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = MuzzleBoostCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentySix");

                    #endregion

                    #region 27. Отрисовка квадрата и картинки предмета: pistol.python
                    
                    var PythonRevolver = StudyTreeUser[player.userID].PythonRevolver == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentySevenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = PythonRevolver },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4852618 0.3154536", AnchorMax = "0.4852618 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentySeven",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4852618 0.3154536", AnchorMax = "0.4852618 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentySeven",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentySeven",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentySeven",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["pistol.python"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var PythonRevolverCommand = StudyTreeUser[player.userID].F1Grenade != true ? "Give_Info_Blueprints_Workbench_StudyTree pistol.python" : "Give_Blueprints_Workbench_StudyTree pistol.python";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = PythonRevolverCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentySeven");

                    #endregion

                    #region 28. Отрисовка квадрата и картинки предмета: ammo.pistol.fire
                    
                    var IncendiaryPistolAmmo = StudyTreeUser[player.userID].IncendiaryPistolAmmo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyEightUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = IncendiaryPistolAmmo },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5771885 0.3154536", AnchorMax = "0.5771885 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyEight",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5771885 0.3154536", AnchorMax = "0.5771885 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyEight",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyEight",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyEight",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.pistol.fire"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var IncendiaryPistolAmmoCommand = StudyTreeUser[player.userID].PythonRevolver != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.pistol.fire" : "Give_Blueprints_Workbench_StudyTree ammo.pistol.fire";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = IncendiaryPistolAmmoCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyEight");

                    #endregion

                    #region 29. Отрисовка квадрата и картинки предмета: ammo.pistol.hv
                    
                    var HVPistolAmmo = StudyTreeUser[player.userID].HVPistolAmmo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyNineUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HVPistolAmmo },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.6684128 0.3154536", AnchorMax = "0.6684128 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareTwentyNine",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.6684128 0.3154536", AnchorMax = "0.6684128 0.3154536", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareTwentyNine",
                        Name = LayerWorkbenchLevelTwo + ".IconTwentyNine",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconTwentyNine",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.pistol.hv"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HVPistolAmmoCommand = StudyTreeUser[player.userID].IncendiaryPistolAmmo != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.pistol.hv" : "Give_Blueprints_Workbench_StudyTree ammo.pistol.hv";


                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HVPistolAmmoCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareTwentyNine");

                    #endregion

                    #region 30. Отрисовка квадрата и картинки предмета: smg.thompson
                    
                    var Thompson = StudyTreeUser[player.userID].Thompson == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Thompson },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4431565 0.1916435", AnchorMax = "0.4431565 0.1916435", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirty",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4431565 0.1916435", AnchorMax = "0.4431565 0.1916435", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirty",
                        Name = LayerWorkbenchLevelTwo + ".IconThirty",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirty",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["smg.thompson"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var ThompsonCommand = StudyTreeUser[player.userID].PythonRevolver != true ? "Give_Info_Blueprints_Workbench_StudyTree smg.thompson" : "Give_Blueprints_Workbench_StudyTree smg.thompson";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = ThompsonCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirty");

                    #endregion

                    #region 31. Отрисовка квадрата и картинки предмета: smg.2
                    
                    var CustomSMG = StudyTreeUser[player.userID].CustomSMG == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyOneUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = CustomSMG },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5280677 0.1916435", AnchorMax = "0.5280677 0.1916435", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5280677 0.1916435", AnchorMax = "0.5280677 0.1916435", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyOne",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyOne",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["smg.2"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var CustomSMGCommand = StudyTreeUser[player.userID].PythonRevolver != true ? "Give_Info_Blueprints_Workbench_StudyTree smg.2" : "Give_Blueprints_Workbench_StudyTree smg.2";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = CustomSMGCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyOne");

                    #endregion

                    #region 32. Отрисовка квадрата и картинки предмета: rifle.semiauto
                    
                    var SemiAutomaticRifle = StudyTreeUser[player.userID].SemiAutomaticRifle == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyTwoUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = SemiAutomaticRifle },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4859636 0.06783438", AnchorMax = "0.4859636 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4859636 0.06783438", AnchorMax = "0.4859636 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyTwo",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyTwo",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["rifle.semiauto"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var SemiAutomaticRifleCommand = StudyTreeUser[player.userID].Thompson != true || StudyTreeUser[player.userID].CustomSMG != true ? "Give_Info_Blueprints_Workbench_StudyTree rifle.semiauto" : "Give_Blueprints_Workbench_StudyTree rifle.semiauto";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = SemiAutomaticRifleCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyTwo");

                    #endregion

                    #region 33. Отрисовка квадрата и картинки предмета: ammo.rifle
                    
                    var Rifle556Ammo = StudyTreeUser[player.userID].Rifle556Ammo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyThreeUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Rifle556Ammo },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5771885 0.06783438", AnchorMax = "0.5771885 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5771885 0.06783438", AnchorMax = "0.5771885 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyThree",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyThree",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rifle"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var Rifle556AmmoCommand = StudyTreeUser[player.userID].SemiAutomaticRifle != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rifle" : "Give_Blueprints_Workbench_StudyTree ammo.rifle";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Rifle556AmmoCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyThree");

                    #endregion

                    #region 34. Отрисовка квадрата и картинки предмета: ammo.rocket.fire
                    
                    var IncendiaryRocket = StudyTreeUser[player.userID].IncendiaryRocket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyFourUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = IncendiaryRocket },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.6684128 0.06783438", AnchorMax = "0.6684128 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.6684128 0.06783438", AnchorMax = "0.6684128 0.06783438", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyFour",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyFour",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rocket.fire"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var IncendiaryRocketCommand = StudyTreeUser[player.userID].Rifle556Ammo != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rocket.fire" : "Give_Blueprints_Workbench_StudyTree ammo.rocket.fire";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = IncendiaryRocketCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyFour");

                    #endregion

                    #region 35. Отрисовка квадрата и картинки предмета: water.catcher.large
                    
                    var LargeWaterCatcher = StudyTreeUser[player.userID].LargeWaterCatcher == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyFiveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = LargeWaterCatcher },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8010389 0.9344954", AnchorMax = "0.8010389 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8010389 0.9344954", AnchorMax = "0.8010389 0.9344954", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyFive",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyFive",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["water.catcher.large"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree water.catcher.large"},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyFive");

                    #endregion

                    #region 36. Отрисовка квадрата и картинки предмета: wall.frame.cell.gate
                    
                    var PrisonCellGate = StudyTreeUser[player.userID].PrisonCellGate == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtySixUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = PrisonCellGate },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.7568302 0.8106882", AnchorMax = "0.7568302 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtySix",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.7568302 0.8106882", AnchorMax = "0.7568302 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtySix",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtySix",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtySix",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["wall.frame.cell.gate"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var PrisonCellGateCommand = StudyTreeUser[player.userID].LargeWaterCatcher != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.frame.cell.gate" : "Give_Blueprints_Workbench_StudyTree wall.frame.cell.gate";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = PrisonCellGateCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtySix");

                    #endregion

                    #region 37. Отрисовка квадрата и картинки предмета: barricade.concrete
                    
                    var ConcreteBarricade = StudyTreeUser[player.userID].ConcreteBarricade == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtySevenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = ConcreteBarricade },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8431424 0.8106882", AnchorMax = "0.8431424 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtySeven",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8431424 0.8106882", AnchorMax = "0.8431424 0.8106882", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtySeven",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtySeven",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtySeven",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["barricade.concrete"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var ConcreteBarricadeCommand = StudyTreeUser[player.userID].LargeWaterCatcher != true ? "Give_Info_Blueprints_Workbench_StudyTree barricade.concrete" : "Give_Blueprints_Workbench_StudyTree barricade.concrete";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = ConcreteBarricadeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtySeven");

                    #endregion

                    #region 38. Отрисовка квадрата и картинки предмета: wall.frame.cell
                    
                    var PrisonCellWall = StudyTreeUser[player.userID].PrisonCellWall == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyEightUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = PrisonCellWall },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.7568302 0.686881", AnchorMax = "0.7568302 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyEight",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.7568302 0.686881", AnchorMax = "0.7568302 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyEight",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyEight",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyEight",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["wall.frame.cell"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var PrisonCellWallCommand = StudyTreeUser[player.userID].PrisonCellGate != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.frame.cell" : "Give_Blueprints_Workbench_StudyTree wall.frame.cell";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = PrisonCellWallCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyEight");

                    #endregion

                    #region 39. Отрисовка квадрата и картинки предмета: barricade.metal
                    
                    var MetalBarricade = StudyTreeUser[player.userID].MetalBarricade == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyNineUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = MetalBarricade },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8431424 0.686881", AnchorMax = "0.8431424 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareThirtyNine",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8431424 0.686881", AnchorMax = "0.8431424 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareThirtyNine",
                        Name = LayerWorkbenchLevelTwo + ".IconThirtyNine",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconThirtyNine",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["barricade.metal"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var MetalBarricadeCommand = StudyTreeUser[player.userID].PrisonCellGate != true ? "Give_Info_Blueprints_Workbench_StudyTree barricade.metal" : "Give_Blueprints_Workbench_StudyTree barricade.metal";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = MetalBarricadeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareThirtyNine");

                    #endregion

                    #region 40. Отрисовка квадрата и картинки предмета: locker
                    
                    var Locker = StudyTreeUser[player.userID].Locker == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Locker },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.9294547 0.686881", AnchorMax = "0.9294547 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareForty",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.9294547 0.686881", AnchorMax = "0.9294547 0.686881", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareForty",
                        Name = LayerWorkbenchLevelTwo + ".IconForty",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconForty",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["locker"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var LockerCommand = StudyTreeUser[player.userID].ConcreteBarricade != true ? "Give_Info_Blueprints_Workbench_StudyTree locker" : "Give_Blueprints_Workbench_StudyTree locker";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = LockerCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareForty");

                    #endregion

                    #region 41. Отрисовка квадрата и картинки предмета: wall.external.high.stone
                    
                    var HighExternalStoneWall = StudyTreeUser[player.userID].HighExternalStoneWall == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyOneUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HighExternalStoneWall },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.7568302 0.5630738", AnchorMax = "0.7568302 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.7568302 0.5630738", AnchorMax = "0.7568302 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortyOne",
                        Name = LayerWorkbenchLevelTwo + ".IconFortyOne",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortyOne",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["wall.external.high.stone"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HighExternalStoneWallCommand = StudyTreeUser[player.userID].PrisonCellGate != true ? "Give_Info_Blueprints_Workbench_StudyTree wall.external.high.stone" : "Give_Blueprints_Workbench_StudyTree wall.external.high.stone";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HighExternalStoneWallCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortyOne");

                    #endregion
                    
                    #region 42. Отрисовка квадрата и картинки предмета: small.oil.refinery
                    
                    var SmallOilRefinery = StudyTreeUser[player.userID].SmallOilRefinery == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyTwoUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = SmallOilRefinery },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8431424 0.5630738", AnchorMax = "0.8431424 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8431424 0.5630738", AnchorMax = "0.8431424 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortyTwo",
                        Name = LayerWorkbenchLevelTwo + ".IconFortyTwo",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortyTwo",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["small.oil.refinery"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var SmallOilRefineryCommand = StudyTreeUser[player.userID].MetalBarricade != true ? "Give_Info_Blueprints_Workbench_StudyTree small.oil.refinery" : "Give_Blueprints_Workbench_StudyTree small.oil.refinery";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = SmallOilRefineryCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortyTwo");

                    #endregion
                    
                    #region 43. Отрисовка квадрата и картинки предмета: floor.ladder.hatch
                    
                    var LadderHatch = StudyTreeUser[player.userID].LadderHatch == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyThreeUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = LadderHatch },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.9294547 0.5630738", AnchorMax = "0.9294547 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.9294547 0.5630738", AnchorMax = "0.9294547 0.5630738", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortyThree",
                        Name = LayerWorkbenchLevelTwo + ".IconFortyThree",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortyThree",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["floor.ladder.hatch"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var LadderHatchCommand = StudyTreeUser[player.userID].Locker != true ? "Give_Info_Blueprints_Workbench_StudyTree floor.ladder.hatch" : "Give_Blueprints_Workbench_StudyTree floor.ladder.hatch";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = LadderHatchCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortyThree");

                    #endregion
                    
                    #region 44. Отрисовка квадрата и картинки предмета: furnace.large
                    
                    var LargeFurnace = StudyTreeUser[player.userID].LargeFurnace == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyFourUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = LargeFurnace },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8431424 0.4391663", AnchorMax = "0.8431424 0.4391663", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8431424 0.4391663", AnchorMax = "0.8431424 0.4391663", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortyFour",
                        Name = LayerWorkbenchLevelTwo + ".IconFortyFour",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortyFour",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["furnace.large"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var LargeFurnaceCommand = StudyTreeUser[player.userID].SmallOilRefinery != true ? "Give_Info_Blueprints_Workbench_StudyTree furnace.large" : "Give_Blueprints_Workbench_StudyTree furnace.large";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = LargeFurnaceCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortyFour");

                    #endregion
                    
                    #region 45. Отрисовка квадрата и картинки предмета: generator.wind.scrap
                    
                    var WindTurbine = StudyTreeUser[player.userID].WindTurbine == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyFiveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = WindTurbine },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.9294546 0.4391663", AnchorMax = "0.9294546 0.4391663", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.9294546 0.4391663", AnchorMax = "0.9294546 0.4391663", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortyFive",
                        Name = LayerWorkbenchLevelTwo + ".IconFortyFive",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortyFive",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["generator.wind.scrap"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var WindTurbineCommand = StudyTreeUser[player.userID].LadderHatch != true ? "Give_Info_Blueprints_Workbench_StudyTree generator.wind.scrap" : "Give_Blueprints_Workbench_StudyTree generator.wind.scrap";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = WindTurbineCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortyFive");

                    #endregion
                    
                    #region 46. Отрисовка квадрата и картинки предмета: autoturret
                    
                    var AutoTurret = StudyTreeUser[player.userID].AutoTurret == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortySixUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = AutoTurret },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.9294546 0.3153553", AnchorMax = "0.9294546 0.3153553", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".BG",
                        Name = LayerWorkbenchLevelTwo + ".SquareFortySix",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.9294546 0.3153553", AnchorMax = "0.9294546 0.3153553", OffsetMin = "-34 -33.5",
                                OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".SquareFortySix",
                        Name = LayerWorkbenchLevelTwo + ".IconFortySix",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelTwo + ".IconFortySix",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["autoturret"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var AutoTurretCommand = StudyTreeUser[player.userID].WindTurbine != true ? "Give_Info_Blueprints_Workbench_StudyTree autoturret" : "Give_Blueprints_Workbench_StudyTree autoturret";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = AutoTurretCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelTwo + ".SquareFortySix");

                    #endregion
                    
                        DestroyWorkbenchLevelTwoUI(player);
                        CuiHelper.AddUi(player, container);
                        yield return new WaitForSeconds(2f);
                    }
                    
                    player.SetFlag(BaseEntity.Flags.Reserved3, false);
                    yield break;
                }
                
                void DestroyWorkbenchLevelTwoUI(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareSixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareSevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareElevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwelveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFourteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFifteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareSixteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareSeventeenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareEighteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareNineteenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentySixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentySevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareTwentyNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyFiveUpdate"); 
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtySixUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyEightUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtySevenUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareThirtyNineUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyOneUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyTwoUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyThreeUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyFourUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortyFiveUpdate");
                    CuiHelper.DestroyUi(player, LayerWorkbenchLevelTwo + ".SquareFortySixUpdate");
                }
				
				[ChatCommand("l3")]
                private void UIWorkbenchLevelThreeT(BasePlayer player)
                {
					SendReply(player, "Чтобы открыть дерево изучений вам необходимо подойти к третьему верстаку и нажать букву Е смотря на него.");
				}

                private void UIWorkbenchLevelThree(BasePlayer player)
                {
                    if (!ImageInit) return;
                    
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 2))
                    {
                        BaseCombatEntity entity = null;
                        try
                        {
                            entity = hit.GetEntity() as BaseCombatEntity;
                        }
                        catch
                        {
                            return;
                        }

                        if (entity.ShortPrefabName.Contains("workbench3.deployed"))
                        {

                            if (player.HasFlag(BaseEntity.Flags.Reserved3)) return;
                            player.SetFlag(BaseEntity.Flags.Reserved3, true);

                            CuiElementContainer container = new CuiElementContainer();

                            container.Add(new CuiPanel
                            {
                                CursorEnabled = true,
                                Image = {Color = "0 0 0 0"},
                                RectTransform =
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-550 -300",
                                    OffsetMax = "50 350"
                                },
                            }, "Overlay", LayerWorkbenchLevelThree);

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree,
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(BackgroundСolor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -1000",
                                        OffsetMax = "1000 1000"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree,
                                Name = LayerWorkbenchLevelThree + ".BG",
                                Components =
                                {
                                    new CuiImageComponent {Color = "0 0 0 0"},
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                                }
                            });

                            #region Отрисовка линий дерева изучений

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineOne",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2844427 0.8620428", AnchorMax = "0.2844427 0.8620428",
                                        OffsetMin = "-1 -12",
                                        OffsetMax = "1 12"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineTwo",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2866649 0.8620428", AnchorMax = "0.2866649 0.8620428",
                                        OffsetMin = "-110 -1",
                                        OffsetMax = "110 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineThree",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1044445 0.8533271", AnchorMax = "0.1044445 0.8533271",
                                        OffsetMin = "-1 -6",
                                        OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineFour",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1044445 0.7282018", AnchorMax = "0.1044445 0.7282018",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineFive",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1044445 0.5928203", AnchorMax = "0.1044445 0.5928203",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineSix",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2844442 0.7282018", AnchorMax = "0.2844442 0.7282018",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineSeven",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2844442 0.593846", AnchorMax = "0.2844442 0.593846",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineEight",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.467779 0.7292274", AnchorMax = "0.467779 0.7292274",
                                        OffsetMin = "-1 -10",
                                        OffsetMax = "1 10"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineNine",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.467779 0.8533271", AnchorMax = "0.467779 0.8533271",
                                        OffsetMin = "-1 -6",
                                        OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineTen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2844442 0.4594889", AnchorMax = "0.2844442 0.4594889",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineEleven",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3577781 0.3917962", AnchorMax = "0.3577781 0.3917962",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineTwelve",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5033347 0.3917962", AnchorMax = "0.5033347 0.3917962",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineThirteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.2844442 0.3271805", AnchorMax = "0.2844442 0.3271805",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineFourteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1955553 0.3271805", AnchorMax = "0.1955553 0.3271805",
                                        OffsetMin = "-54 -1",
                                        OffsetMax = "54 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineFifteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1066668 0.3200009", AnchorMax = "0.1066668 0.3200009",
                                        OffsetMin = "-1 -6",
                                        OffsetMax = "1 6"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineSixteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.1077779 0.1928207", AnchorMax = "0.1077779 0.1928207",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineSeventeen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.3577781 0.2584621", AnchorMax = "0.3577781 0.2584621",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineEighteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5033347 0.2584621", AnchorMax = "0.5033347 0.2584621",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineNineteen",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.6466653 0.2584621", AnchorMax = "0.6466653 0.2584621",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineTwenty",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.7911071 0.2584621", AnchorMax = "0.7911071 0.2584621",
                                        OffsetMin = "-11 -1",
                                        OffsetMax = "11 1"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = LayerWorkbenchLevelThree + ".BG",
                                Name = LayerWorkbenchLevelThree + ".LineTwentyOne",
                                Components =
                                {
                                    new CuiImageComponent {Color = HexFormat(LockedItemColor)},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.4322231 0.1928207", AnchorMax = "0.4322231 0.1928207",
                                        OffsetMin = "-1 -11",
                                        OffsetMax = "1 11"
                                    }
                                }
                            });

                            #endregion

                            CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree);
                            CuiHelper.AddUi(player, container);

                            PanelBlueprintsWorkbenchStudyTree(player);
                            ServerMgr.Instance.StartCoroutine(UpdateWorkbenchLevelThree(player));
                        }
                        else
                        {
                            SendReply(player, "Чтобы открыть дерево изучений вам необходимо смотреть на третий верстак.");
                        }
                    }
                }

                private IEnumerator UpdateWorkbenchLevelThree(BasePlayer player)
                {
                    while (player.HasFlag(BaseEntity.Flags.Reserved3) && player.IsConnected)
                    {
                        CuiElementContainer container = new CuiElementContainer();

                        #region 1. Отрисовка квадрата и картинки предмета: wall.window.bars.toptier
                    
                        var ReinforcedWindowBarsColor = StudyTreeUser[player.userID].ReinforcedWindowBars == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareOneUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = ReinforcedWindowBarsColor },
                                new CuiRectTransformComponent { AnchorMin = "0.286665 0.9297336", AnchorMax = "0.286665 0.9297336", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareOne",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.286665 0.9297336", AnchorMax = "0.286665 0.9297336", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareOne",
                            Name = LayerWorkbenchLevelThree + ".IconOne",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconOne",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["wall.window.bars.toptier"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = "Give_Blueprints_Workbench_StudyTree wall.window.bars.toptier" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelThree + ".SquareOne", LayerWorkbenchLevelThree + ".BTSquareOne");

                        #endregion
                        
                        #region 2. Отрисовка квадрата и картинки предмета: door.hinged.toptier
                        
                        var ArmoredDoorColor = StudyTreeUser[player.userID].ArmoredDoor == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareTwoUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = ArmoredDoorColor },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.7943521", AnchorMax = "0.1066653 0.7943521", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.7943521", AnchorMax = "0.1066653 0.7943521", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareTwo",
                            Name = LayerWorkbenchLevelThree + ".IconTwo",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconTwo",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["door.hinged.toptier"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });

                        var ArmoredDoorCommand = StudyTreeUser[player.userID].ReinforcedWindowBars != true ? "Give_Info_Blueprints_Workbench_StudyTree door.hinged.toptier" : "Give_Blueprints_Workbench_StudyTree door.hinged.toptier";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = ArmoredDoorCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelThree + ".SquareTwo");

                        #endregion
                        
                        #region 3. Отрисовка квадрата и картинки предмета: door.double.hinged.toptier
                        
                        var ArmoredDoubleDoorColor = StudyTreeUser[player.userID].ArmoredDoubleDoor == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareThreeUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = ArmoredDoubleDoorColor },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.6610219", AnchorMax = "0.1066653 0.6610219", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareThree",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.6610219", AnchorMax = "0.1066653 0.6610219", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareThree",
                            Name = LayerWorkbenchLevelThree + ".IconThree",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconThree",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["door.double.hinged.toptier"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });

                        var ArmoredDoubleDoorCommand = StudyTreeUser[player.userID].ArmoredDoor != true ? "Give_Info_Blueprints_Workbench_StudyTree door.double.hinged.toptier" : "Give_Blueprints_Workbench_StudyTree door.double.hinged.toptier";
                        
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = ArmoredDoubleDoorCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelThree + ".SquareThree");

                        #endregion
                        
                        #region 4. Отрисовка квадрата и картинки предмета: gates.external.high.stone
                        
                        var HighExternalStoneGateColor = StudyTreeUser[player.userID].HighExternalStoneGate == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareFourUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = HighExternalStoneGateColor },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.5276917", AnchorMax = "0.1066653 0.5276917", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareFour",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.1066653 0.5276917", AnchorMax = "0.1066653 0.5276917", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareFour",
                            Name = LayerWorkbenchLevelThree + ".IconFour",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 31" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconFour",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["gates.external.high.stone"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var HighExternalStoneGateCommand = StudyTreeUser[player.userID].ArmoredDoubleDoor != true ? "Give_Info_Blueprints_Workbench_StudyTree gates.external.high.stone" : "Give_Blueprints_Workbench_StudyTree gates.external.high.stone";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = HighExternalStoneGateCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelThree + ".SquareFour");

                        #endregion
                        
                        #region 5. Отрисовка квадрата и картинки предмета: metal.plate.torso
                        
                        var MetalChestPlateColor = StudyTreeUser[player.userID].MetalChestPlate == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareFiveUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MetalChestPlateColor },
                                new CuiRectTransformComponent { AnchorMin = "0.4666664 0.7943521", AnchorMax = "0.4666664 0.7943521", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareFive",
                            Components =
                            {
                                new CuiImageComponent { Color = "0 0 0 0" },
                                new CuiRectTransformComponent { AnchorMin = "0.4666664 0.7943521", AnchorMax = "0.4666664 0.7943521", OffsetMin = "-34 -33.5", OffsetMax = "33 33" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareFive",
                            Name = LayerWorkbenchLevelThree + ".IconFive",
                            Components =
                            {
                                new CuiImageComponent { Color = HexFormat(BackgroundColorClipArt) },
                                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "31 30.5" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconFive",
                            Components =
                            {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = StudyImageList["metal.plate.torso"] },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                            }
                        });
                        
                        var MetalChestPlateCommand = StudyTreeUser[player.userID].ReinforcedWindowBars != true ? "Give_Info_Blueprints_Workbench_StudyTree metal.plate.torso" : "Give_Blueprints_Workbench_StudyTree metal.plate.torso";

                        container.Add(new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = MetalChestPlateCommand },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31" },
                            Text = { Text = "" }
                        }, LayerWorkbenchLevelThree + ".SquareFive");

                        #endregion
                        
                        #region 6. Отрисовка квадрата и картинки предмета: metal.facemask
                        
                        var MetalFacemaskColor = StudyTreeUser[player.userID].MetalFacemask == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareSixUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = MetalFacemaskColor },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.4666664 0.6640987", AnchorMax = "0.4666664 0.6640987",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareSix",
                            Components =
                            {
                                new CuiImageComponent {Color = "0 0 0 0"},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.4666664 0.6640987", AnchorMax = "0.4666664 0.6640987",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareSix",
                            Name = LayerWorkbenchLevelThree + ".IconSix",
                            Components =
                            {
                                new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "31 30.5"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconSix",
                            Components =
                            {
                                new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["metal.facemask"]},
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                            }
                        });
                        
                        var MetalFacemaskCommand = StudyTreeUser[player.userID].MetalChestPlate != true ? "Give_Info_Blueprints_Workbench_StudyTree metal.facemask" : "Give_Blueprints_Workbench_StudyTree metal.facemask";
                        
                        container.Add(new CuiButton
                        {
                            Button = {Color = "0 0 0 0", Command = MetalFacemaskCommand},
                            RectTransform =
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                            Text = {Text = ""}
                        }, LayerWorkbenchLevelThree + ".SquareSix");
                        #endregion

                        #region 7. Отрисовка квадрата и картинки предмета: weapon.mod.lasersight
                        
                        var WeaponLasersightColor = StudyTreeUser[player.userID].WeaponLasersight == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareSevenUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = WeaponLasersightColor },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.2866649 0.7943521", AnchorMax = "0.2866649 0.7943521",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareSeven",
                            Components =
                            {
                                new CuiImageComponent {Color = "0 0 0 0"},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2866649 0.7943521", AnchorMax = "0.2866649 0.7943521",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareSeven",
                            Name = LayerWorkbenchLevelThree + ".IconSeven",
                            Components =
                            {
                                new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                                new CuiRectTransformComponent
                                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconSeven",
                            Components =
                            {
                                new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["weapon.mod.lasersight"]},
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                            }
                        });
                        
                        var WeaponLasersightCommand = StudyTreeUser[player.userID].ReinforcedWindowBars != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.lasersight" : "Give_Blueprints_Workbench_StudyTree weapon.mod.lasersight";

                        container.Add(new CuiButton
                        {
                            Button = {Color = "0 0 0 0", Command = WeaponLasersightCommand},
                            RectTransform =
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                            Text = {Text = ""}
                        }, LayerWorkbenchLevelThree + ".SquareSeven");

                        #endregion

                        #region 8. Отрисовка квадрата и картинки предмета: ammo.rifle.hv
                        
                        var HV556RifleAmmoColor = StudyTreeUser[player.userID].HV556RifleAmmo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareEightUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = HV556RifleAmmoColor },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.2866649 0.6610219", AnchorMax = "0.2866649 0.6610219",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareEight",
                            Components =
                            {
                                new CuiImageComponent {Color = "0 0 0 0"},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2866649 0.6610219", AnchorMax = "0.2866649 0.6610219",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".SquareEight",
                            Name = LayerWorkbenchLevelThree + ".IconEight",
                            Components =
                            {
                                new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                                new CuiRectTransformComponent
                                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".IconEight",
                            Components =
                            {
                                new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rifle.hv"]},
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                            }
                        });
                        
                        var HV556RifleAmmoCommand = StudyTreeUser[player.userID].WeaponLasersight != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rifle.hv" : "Give_Blueprints_Workbench_StudyTree ammo.rifle.hv";

                        container.Add(new CuiButton
                        {
                            Button = {Color = "0 0 0 0", Command = HV556RifleAmmoCommand},
                            RectTransform =
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                            Text = {Text = ""}
                        }, LayerWorkbenchLevelThree + ".SquareEight");

                        #endregion
                        
                        #region 9. Отрисовка квадрата и картинки предмета: ammo.rifle.incendiary
                        
                        var Incendiary556RifleAmmoColor = StudyTreeUser[player.userID].Incendiary556RifleAmmo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareNineUpdate",
                            Components =
                            {
                                new CuiImageComponent { Color = Incendiary556RifleAmmoColor },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.2866649 0.5276917", AnchorMax = "0.2866649 0.5276917",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = LayerWorkbenchLevelThree + ".BG",
                            Name = LayerWorkbenchLevelThree + ".SquareNine",
                            Components =
                            {
                                new CuiImageComponent {Color = "0 0 0 0"},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.2866649 0.5276917", AnchorMax = "0.2866649 0.5276917",
                                    OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                                }
                            }
                        });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareNine",
                        Name = LayerWorkbenchLevelThree + ".IconNine",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconNine",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rifle.incendiary"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var Incendiary556RifleAmmoCommand = StudyTreeUser[player.userID].HV556RifleAmmo != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rifle.incendiary" : "Give_Blueprints_Workbench_StudyTree ammo.rifle.incendiary";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Incendiary556RifleAmmoCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareNine");

                    #endregion

                        #region 10. Отрисовка квадрата и картинки предмета: smg.mp5
                    
                    var MP5A4Color = StudyTreeUser[player.userID].MP5A4 == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = MP5A4Color },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.2866649 0.3943583", AnchorMax = "0.2866649 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2866649 0.3943583", AnchorMax = "0.2866649 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareTen",
                        Name = LayerWorkbenchLevelThree + ".IconTen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconTen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["smg.mp5"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var MP5A4Command = StudyTreeUser[player.userID].Incendiary556RifleAmmo != true ? "Give_Info_Blueprints_Workbench_StudyTree smg.mp5" : "Give_Blueprints_Workbench_StudyTree smg.mp5";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = MP5A4Command},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareTen", LayerWorkbenchLevelThree + ".BTSquareTen");

                    #endregion

                        #region 11. Отрисовка квадрата и картинки предмета: weapon.mod.holosight
                    
                    var HolosightColor = StudyTreeUser[player.userID].Holosight == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareElevenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HolosightColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4311105 0.3943583", AnchorMax = "0.4311105 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareEleven",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4311105 0.3943583", AnchorMax = "0.4311105 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareEleven",
                        Name = LayerWorkbenchLevelThree + ".IconEleven",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconEleven",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["weapon.mod.holosight"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HolosightCommand = StudyTreeUser[player.userID].MP5A4 != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.holosight" : "Give_Blueprints_Workbench_StudyTree weapon.mod.holosight";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HolosightCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareEleven", LayerWorkbenchLevelThree + ".BTSquareEleven");

                    #endregion
                    
                        #region 12. Отрисовка квадрата и картинки предмета: weapon.mod.small.scope
                    
                    var Zoom4xScopeColor = StudyTreeUser[player.userID].Zoom4xScope == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTwelveUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Zoom4xScopeColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5755541 0.3943583", AnchorMax = "0.5755541 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTwelve",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5755541 0.3943583", AnchorMax = "0.5755541 0.3943583",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareTwelve",
                        Name = LayerWorkbenchLevelThree + ".IconTwelve",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "30.5 31"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconTwelve",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["weapon.mod.small.scope"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var Zoom4xScopeCommand = StudyTreeUser[player.userID].Holosight != true ? "Give_Info_Blueprints_Workbench_StudyTree weapon.mod.small.scope" : "Give_Blueprints_Workbench_StudyTree weapon.mod.small.scope";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Zoom4xScopeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareTwelve", LayerWorkbenchLevelThree + ".BTSquareTwelve");

                    #endregion

                        #region 13. Отрисовка квадрата и картинки предмета: ammo.rifle.explosive
                    
                    var Explosive556RifleAmmoColor = StudyTreeUser[player.userID].Explosive556RifleAmmo == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareThirteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = Explosive556RifleAmmoColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.2866649 0.2610242", AnchorMax = "0.2866649 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareThirteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.2866649 0.2610242", AnchorMax = "0.2866649 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareThirteen",
                        Name = LayerWorkbenchLevelThree + ".IconThirteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconThirteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rifle.explosive"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var Explosive556RifleAmmoCommand = StudyTreeUser[player.userID].MP5A4 != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rifle.explosive" : "Give_Blueprints_Workbench_StudyTree ammo.rifle.explosive";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = Explosive556RifleAmmoCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareThirteen", LayerWorkbenchLevelThree + ".BTSquareThirteen");

                    #endregion

                        #region 14. Отрисовка квадрата и картинки предмета: rifle.ak
                    
                    var AssaultRifleAmmoColor = StudyTreeUser[player.userID].AssaultRifle == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareFourteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = AssaultRifleAmmoColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.1066653 0.2610242", AnchorMax = "0.1066653 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareFourteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1066653 0.2610242", AnchorMax = "0.1066653 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareFourteen",
                        Name = LayerWorkbenchLevelThree + ".IconFourteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconFourteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["rifle.ak"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var AssaultRifleCommand = StudyTreeUser[player.userID].MP5A4 != true ? "Give_Info_Blueprints_Workbench_StudyTree rifle.ak" : "Give_Blueprints_Workbench_StudyTree rifle.ak";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = AssaultRifleCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareFourteen", LayerWorkbenchLevelThree + ".BTSquareFourteen");

                    #endregion

                        #region 15. Отрисовка квадрата и картинки предмета: rifle.bolt
                    
                    var BoltActionRifleColor = StudyTreeUser[player.userID].BoltActionRifle == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareFifteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = BoltActionRifleColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.1066653 0.1276901", AnchorMax = "0.1066653 0.1276901",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareFifteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1066653 0.1276901", AnchorMax = "0.1066653 0.1276901",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareFifteen",
                        Name = LayerWorkbenchLevelThree + ".IconFifteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconFifteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["rifle.bolt"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var BoltActionRifleCommand = StudyTreeUser[player.userID].AssaultRifle != true ? "Give_Info_Blueprints_Workbench_StudyTree rifle.bolt" : "Give_Blueprints_Workbench_StudyTree rifle.bolt";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = BoltActionRifleCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareFifteen", LayerWorkbenchLevelThree + ".BTSquareFifteen");

                    #endregion

                        #region 16. Отрисовка квадрата и картинки предмета: rocket.launcher
                    
                    var RocketLauncherColor = StudyTreeUser[player.userID].RocketLauncher == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareSixteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = RocketLauncherColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4311105 0.2610242", AnchorMax = "0.4311105 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareSixteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4311105 0.2610242", AnchorMax = "0.4311105 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareSixteen",
                        Name = LayerWorkbenchLevelThree + ".IconSixteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconSixteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["rocket.launcher"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });

                    var RocketLauncherCommand = StudyTreeUser[player.userID].Explosive556RifleAmmo != true ? "Give_Info_Blueprints_Workbench_StudyTree rocket.launcher" : "Give_Blueprints_Workbench_StudyTree rocket.launcher";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = RocketLauncherCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareSixteen", LayerWorkbenchLevelThree + ".BTSquareSixteen");

                    #endregion

                        #region 17. Отрисовка квадрата и картинки предмета: ammo.rocket.hv
                    
                    var HighVelocityRocketColor = StudyTreeUser[player.userID].HighVelocityRocket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareSeventeenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = HighVelocityRocketColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.4311105 0.1276901", AnchorMax = "0.4311105 0.1276901",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareSeventeen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.4311105 0.1276901", AnchorMax = "0.4311105 0.1276901",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareSeventeen",
                        Name = LayerWorkbenchLevelThree + ".IconSeventeen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.5 -31", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconSeventeen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rocket.hv"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var HighVelocityRocketCommand = StudyTreeUser[player.userID].RocketLauncher != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rocket.hv" : "Give_Blueprints_Workbench_StudyTree ammo.rocket.hv";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = HighVelocityRocketCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareSeventeen", LayerWorkbenchLevelThree + ".BTSquareSeventeen");

                    #endregion

                        #region 18. Отрисовка квадрата и картинки предмета: explosives
                    
                    var ExplosivesColor = StudyTreeUser[player.userID].Explosives == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareEighteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = ExplosivesColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.5755541 0.2610242", AnchorMax = "0.5755541 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareEighteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5755541 0.2610242", AnchorMax = "0.5755541 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareEighteen",
                        Name = LayerWorkbenchLevelThree + ".IconEighteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "30.5 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconEighteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["explosives"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var ExplosivesCommand = StudyTreeUser[player.userID].RocketLauncher != true ? "Give_Info_Blueprints_Workbench_StudyTree explosives" : "Give_Blueprints_Workbench_StudyTree explosives";

                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = ExplosivesCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareEighteen", LayerWorkbenchLevelThree + ".BTSquareEighteen");

                    #endregion

                        #region 19. Отрисовка квадрата и картинки предмета: explosive.timed
                    
                    var TimedExplosiveChargeColor = StudyTreeUser[player.userID].TimedExplosiveCharge == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareNineteenUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = TimedExplosiveChargeColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.7199959 0.2610242", AnchorMax = "0.7199959 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareNineteen",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.7199959 0.2610242", AnchorMax = "0.7199959 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareNineteen",
                        Name = LayerWorkbenchLevelThree + ".IconNineteen",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconNineteen",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["explosive.timed"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var TimedExplosiveChargeCommand = StudyTreeUser[player.userID].Explosives != true ? "Give_Info_Blueprints_Workbench_StudyTree explosive.timed" : "Give_Blueprints_Workbench_StudyTree explosive.timed";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = TimedExplosiveChargeCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareNineteen", LayerWorkbenchLevelThree + ".BTSquareNineteen");

                    #endregion

                        #region 20. Отрисовка квадрата и картинки предмета: ammo.rocket.basic
                    
                    var RocketColor = StudyTreeUser[player.userID].Rocket == true ? HexFormat(UnlockedItemColor) : HexFormat(LockedItemColor);
                        
                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTwentyUpdate",
                        Components =
                        {
                            new CuiImageComponent { Color = RocketColor },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.8644376 0.2610242", AnchorMax = "0.8644376 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".BG",
                        Name = LayerWorkbenchLevelThree + ".SquareTwenty",
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.8644376 0.2610242", AnchorMax = "0.8644376 0.2610242",
                                OffsetMin = "-34 -33.5", OffsetMax = "33 33"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".SquareTwenty",
                        Name = LayerWorkbenchLevelThree + ".IconTwenty",
                        Components =
                        {
                            new CuiImageComponent {Color = HexFormat(BackgroundColorClipArt)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -30.5", OffsetMax = "31 30.5"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerWorkbenchLevelThree + ".IconTwenty",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", Png = StudyImageList["ammo.rocket.basic"]},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6"}
                        }
                    });
                    
                    var RocketCommand = StudyTreeUser[player.userID].TimedExplosiveCharge != true ? "Give_Info_Blueprints_Workbench_StudyTree ammo.rocket.basic" : "Give_Blueprints_Workbench_StudyTree ammo.rocket.basic";
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0 0 0 0", Command = RocketCommand},
                        RectTransform =
                            {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -31", OffsetMax = "31 31"},
                        Text = {Text = ""}
                    }, LayerWorkbenchLevelThree + ".SquareTwenty", LayerWorkbenchLevelThree + ".BTSquareTwenty");

                    #endregion
                    
                        DestroyWorkbenchLevelThreeUI(player);
                        CuiHelper.AddUi(player, container);
                        yield return new WaitForSeconds(2f);
                    }
                    
                    player.SetFlag(BaseEntity.Flags.Reserved3, false);
                    yield break;
                }

                void DestroyWorkbenchLevelThreeUI(BasePlayer player)
                {
                    if(StudyTreeUser[player.userID].ReinforcedWindowBars != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareOneUpdate");
                    
                    if(StudyTreeUser[player.userID].ArmoredDoor != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareTwoUpdate");
                    
                    if(StudyTreeUser[player.userID].ArmoredDoubleDoor != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareThreeUpdate");
                    
                    if(StudyTreeUser[player.userID].HighExternalStoneGate != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareFourUpdate");
                    
                    if(StudyTreeUser[player.userID].MetalChestPlate != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareFiveUpdate");
                    
                    if(StudyTreeUser[player.userID].MetalFacemask != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareSixUpdate");
                    
                    if(StudyTreeUser[player.userID].WeaponLasersight != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareSevenUpdate");
                    
                    if(StudyTreeUser[player.userID].HV556RifleAmmo != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareEightUpdate");
                    
                    if(StudyTreeUser[player.userID].Incendiary556RifleAmmo != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareNineUpdate");
                    
                    if(StudyTreeUser[player.userID].MP5A4 != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareTenUpdate");
                    
                    if(StudyTreeUser[player.userID].Holosight != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareElevenUpdate");
                    
                    if(StudyTreeUser[player.userID].Zoom4xScope != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareTwelveUpdate");
                    
                    if(StudyTreeUser[player.userID].Explosive556RifleAmmo != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareThirteenUpdate");
                    
                    if(StudyTreeUser[player.userID].AssaultRifle != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareFourteenUpdate");
                    
                    if(StudyTreeUser[player.userID].BoltActionRifle != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareFifteenUpdate");
                    
                    if(StudyTreeUser[player.userID].RocketLauncher != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareSixteenUpdate");
                    
                    if(StudyTreeUser[player.userID].HighVelocityRocket != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareSeventeenUpdate");
                    
                    if(StudyTreeUser[player.userID].Explosives != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareEighteenUpdate");
                    
                    if(StudyTreeUser[player.userID].TimedExplosiveCharge != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareNineteenUpdate");
                    
                    if(StudyTreeUser[player.userID].Rocket != true) 
                        CuiHelper.DestroyUi(player, LayerWorkbenchLevelThree + ".SquareTwentyUpdate");
                }

                #endregion

                #region Кастомные проверки и оптимизация кода

                private static string HexFormat(string hex)
                {
                    if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

                    var str = hex.Trim('#');
                    if (str.Length == 6) str += "FF";
                    if (str.Length != 8) throw new Exception(hex);

                    var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                    var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
                    var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

                    Color color = new Color32(r, g, b, a);

                    return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
                }

                class FileManager : MonoBehaviour
                {
                    int loaded = 0;
                    int needed = 0;

                    Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
                    DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("StudyTree/Images");

                    private class FileInfo
                    {
                        public string Url;
                        public string Png;
                    }

                    public void SaveData()
                    {
                        dataFile.WriteObject(files);
                    }

                    public void WipeData()
                    {
                        Interface.Oxide.DataFileSystem.WriteObject("StudyTree/Images", new sbyte());
                        Interface.Oxide.ReloadPlugin(Instance.Title);
                    }

                    public string GetPng(string name)
                    {
                        if (!files.ContainsKey(name)) return null;
                        return files[name].Png;
                    }

                    private void Awake()
                    {
                        LoadData();
                    }

                    void LoadData()
                    {
                        try
                        {
                            files = dataFile.ReadObject<Dictionary<string, FileInfo>>();
                        }
                        catch
                        {
                            files = new Dictionary<string, FileInfo>();
                        }
                    }

                    public IEnumerator LoadFile(string name, string url)
                    {
                        if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png))
                            yield break;
                        files[name] = new FileInfo() {Url = url};
                        needed++;

                        yield return StartCoroutine(LoadImageCoroutine(name, url));
                    }

                    IEnumerator LoadImageCoroutine(string name, string url)
                    {
                        using (WWW www = new WWW(url))
                        {
                            yield return www;
                            {
                                if (string.IsNullOrEmpty(www.error))
                                {
                                    var entityId = CommunityEntity.ServerInstance.net.ID;
                                    var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
                                    files[name].Png = crc32;
                                }
                            }
                        }

                        loaded++;
                    }
                }

                private void SendEffect(BasePlayer player, string effect)
                {
                    if (player == null) return;
                    var Effect = new Effect(effect, player, 0, Vector3.zero, Vector3.forward);
                    if (Effect == null) return;
                    EffectNetwork.Send(Effect, player.net.connection);
                }

                private void SendSound(BasePlayer player, string sound)
                {
                    if (player == null) return;
                    var Sound = new Effect(sound, player.transform.position, Vector3.zero);
                    if (Sound == null) return;
                    EffectNetwork.Send(Sound, player.net.connection);
                }
                
                private void PlayFx(BasePlayer player, string fx)
                {
                    if (player == null) return;
               
                    var EffectInstance = new Effect();
                    EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
                    EffectInstance.pooledstringid = StringPool.Get(fx);
                    Net.sv.write.Start();
                    Net.sv.write.PacketID(Message.Type.Effect);
                    EffectInstance.WriteToStream(Net.sv.write);
                    Net.sv.write.Send(new SendInfo(player.net.connection));
                    EffectInstance.Clear();
                }

                private int CheckAmountScrap(BasePlayer player)
                {
                    return player.inventory.GetAmount(ItemManager.FindItemDefinition("scrap").itemid);
                }
                
                private int TakeScrap(BasePlayer player, int amount)
                {
                    return player.inventory.Take(null, ItemManager.FindItemDefinition("scrap").itemid, amount);
                }
                
                private ItemDefinition FindItemDefinition(BasePlayer player, string item)
                {
                    return ItemManager.FindItemDefinition(item);
                }

                #endregion
            }
        }