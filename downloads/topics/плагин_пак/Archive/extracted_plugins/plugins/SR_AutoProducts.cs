using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SR Auto Products", "vlad-helper", "1.0.0")]
    [Description("Автогенерация полного products.json для ServerRewards v2 с ценами под 25 RP/час")]
    public class SRAutoProducts : CovalencePlugin
    {
        private static readonly HashSet<string> Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ammo.snowballgun","blueprintbase","rhib","attackhelicopter","motorbike","motorbike_sidecar",
            "bicycle","trike","kayak","rowboat","tugboat","skidoo","minihelicopter.repair",
            "scraptransportheli.repair","mlrs","snowmobile","spraycandecal","vehicle.chassis",
            "vehicle.module","water","water.salt"
        };

        // Базовые цены (RP) и пачки под 25 RP/час
        private int Pack(string shortname, ItemCategory cat)
        {
            if (shortname == "ammo.rifle" || shortname == "ammo.pistol") return 100;
            if (shortname == "ammo.shotgun") return 50;
            if (shortname == "bandage") return 10;
            if (shortname == "syringe.medical") return 5;
            if (shortname == "largemedkit") return 2;

            if (cat == ItemCategory.Resources)
            {
                if (shortname == "wood" || shortname == "stones" || shortname == "sulfur.ore" || shortname == "metal.ore") return 1000;
                if (shortname == "metal.fragments") return 500;
                if (shortname == "metal.refined") return 50; // HQM
                if (shortname == "gunpowder") return 500;
                if (shortname == "cloth") return 200;
                if (shortname == "lowgradefuel") return 100;
            }

            return 1;
        }

        private int Cost(string shortname, ItemCategory cat, int amount)
        {
            // Точные цены для ключевых ресурсов/расходников
            switch (shortname)
            {
                case "wood": return 5;                 // x1000
                case "stones": return 8;               // x1000
                case "metal.ore": return 15;           // x1000
                case "sulfur.ore": return 40;          // x1000
                case "metal.fragments": return 25;     // x500
                case "metal.refined": return 90;       // x50 (HQM)
                case "gunpowder": return 60;           // x500
                case "cloth": return 12;               // x200
                case "lowgradefuel": return 20;        // x100
                case "scrap": return 90;               // x100

                case "bandage": return 6;              // x10
                case "syringe.medical": return 30;     // x5
                case "largemedkit": return 40;         // x2

                case "ammo.rifle": return 60;          // x100
                case "ammo.pistol": return 50;         // x100
                case "ammo.shotgun": return 45;        // x50

                case "smg.thompson": return 100;
                case "rifle.ak": return 220;
                case "rifle.lr300": return 280;
                case "shotgun.spas12": return 140;

                case "metal.facemask": return 90;
                case "metal.plate.torso": return 80;
                case "roadsign.kilt": return 60;

                case "hammer": return 6;
                case "building.planner": return 6;
                case "tool.chain": return 80;
                case "furnace": return 25;
                case "workbench1": return 60;
                case "research.table": return 40;
            }

            // Фолбэк по категориям
            switch (cat)
            {
                case ItemCategory.Weapon: return 200;
                case ItemCategory.Ammunition: return Mathf.Clamp(Mathf.RoundToInt(amount * 0.5f), 25, 80);
                case ItemCategory.Medical: return Mathf.Clamp(Mathf.RoundToInt(amount * 6f), 12, 60);
                case ItemCategory.Attire: return 50;
                case ItemCategory.Component: return 40;
                case ItemCategory.Tool: return 40;
                case ItemCategory.Electrical: return 50;
                case ItemCategory.Traps: return 60;
                case ItemCategory.Food: return 10;
                case ItemCategory.Resources:
                {
                    // Если ресурс не из точных — масштаб от количества
                    float unit = 0.01f; // средняя базовая цена за штуку
                    return Mathf.Max(5, Mathf.RoundToInt(amount * unit));
                }
                case ItemCategory.Construction: return 40;
                case ItemCategory.Misc: return 20;
                case ItemCategory.Fun: return 30;
                default: return 25;
            }
        }

        private int Cooldown(string shortname, ItemCategory cat)
        {
            // Кулдауны на хай-импакт
            if (cat == ItemCategory.Weapon) return 3 * 3600;
            if (shortname == "smg.thompson") return 3600;
            if (shortname == "rifle.ak") return 4 * 3600;
            if (shortname == "rifle.lr300") return 5 * 3600;
            if (shortname == "shotgun.spas12") return 3 * 3600;

            if (shortname == "metal.facemask" || shortname == "metal.plate.torso") return 3 * 3600;
            if (shortname == "roadsign.kilt") return 2 * 3600;

            if (shortname == "tool.chain") return 3600;
            return 0;
        }

        private class Products
        {
            public int ProductIndex = 0;
            public List<Item> Items = new List<Item>();
            public List<object> Kits = new List<object>();
            public List<object> Commands = new List<object>();

            public class Item
            {
                public int ID;
                public string Shortname;
                public int Amount;
                public ulong SkinId;
                public bool IsBp;
                public bool IgnoreDlcCheck;
                public int Category;
                public string DisplayName;
                public int Cost;
                public int Cooldown;
                public string IconURL;
                public string Permission;
            }
        }

        [Command("sr.auto_products")]
        private void CmdGenerate(IPlayer caller, string cmd, string[] args)
        {
            if (caller?.IsServer == false && !caller.HasPermission("serverrewards.admin"))
            {
                caller?.Reply("Требуются права admin (serverrewards.admin).");
                return;
            }

            GenerateAll();
            caller?.Reply("Готово! Сгенерирован полный products.json. Перезагрузите ServerRewards: oxide.reload ServerRewards");
        }

        private void GenerateAll()
        {
            var products = new Products();
            int id = 0;

            foreach (var def in ItemManager.itemList)
            {
                if (!def) continue;
                if (Ignore.Contains(def.shortname)) continue;

                string name = def.displayName?.english ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;

                var amount = Pack(def.shortname, def.category);
                var cost = Cost(def.shortname, def.category, amount);
                var cd = Cooldown(def.shortname, def.category);

                products.Items.Add(new Products.Item
                {
                    ID = id++,
                    Shortname = def.shortname,
                    Amount = amount,
                    SkinId = 0UL,
                    IsBp = false,
                    IgnoreDlcCheck = false,
                    Category = (int)def.category,
                    DisplayName = name,
                    Cost = cost,
                    Cooldown = cd,
                    IconURL = string.Empty,
                    Permission = string.Empty
                });
            }

            products.ProductIndex = id;

            var dfs = Interface.Oxide.DataFileSystem;
            // Пишем ровно в ServerRewards/products.json, как ждёт основной плагин
            dfs.WriteObject("ServerRewards/products", products, true);
            Puts($"Сохранено {products.Items.Count} предметов в data/ServerRewards/products.json");
        }
    }
}