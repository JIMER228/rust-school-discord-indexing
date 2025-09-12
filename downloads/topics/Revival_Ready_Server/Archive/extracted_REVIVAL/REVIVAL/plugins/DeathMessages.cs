using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Death Messages", "Developer x ", "2.1.53")]
    class DeathMessages : RustPlugin
    {
        private static PluginConfig _config;
        private string version = "2.1.53";
        private List<DeathMessage> _notes = new List<DeathMessage>();
        private Dictionary<ulong, HitInfo> _lastHits = new Dictionary<ulong, HitInfo>();

        #region Classes / Enums

        class PluginConfig
        {

            [JsonProperty("A. Время показа сообщения (сек)")]
            public int Cooldown { get; set; }
            [JsonProperty("B. Размер текста")]
            public int FontSize { get; set; }
            [JsonProperty("C. Показывать убиства животных")]
            public bool ShowDeathAnimals { get; set; }
            [JsonProperty("D. Показывать убийства спящих")]
            public bool ShowDeathSleepers { get; set; }
            [JsonProperty("E. Хранение логов")]
            public bool Log { get; set; }
            [JsonProperty("F. Цвет атакующего")]
            public string ColorAttacker { get; set; }
            [JsonProperty("G. Цвет убитого")]
            public string ColorVictim { get; set; }
            [JsonProperty("H. Цвет оружия")]
            public string ColorWeapon { get; set; }
            [JsonProperty("I. Цвет дистанции")]
            public string ColorDistance { get; set; }
            [JsonProperty("J. Цвет части тела")]
            public string ColorBodyPart { get; set; }
            [JsonProperty("K. Дистанция")]
            public double Distance { get; set; }

            [JsonProperty("Оружие")]
            public Dictionary<string, string> Weapons { get; set; }
            [JsonProperty("Конструкции")]
            public Dictionary<string, string> Structures { get; set; }
            [JsonProperty("Ловушки")]
            public Dictionary<string, string> Traps { get; set; }
            [JsonProperty("Животные")]
            public Dictionary<string, string> Animals { get; set; }
            [JsonProperty("Сообщения")]
            public Dictionary<string, string> Messages { get; set; }
            [JsonProperty("Части тела")]
            public Dictionary<string, string> BodyParts { get; set; }
        }

        enum AttackerType
        {
            Player,
            Animal,
            Structure,
            Trap,
            Invalid
        }

        enum VictimType
        {
            Player,
            Animal,
            Invalid
        }

        enum DeathReason
        {
            Structure,
            Trap,
            Animal,
            AnimalDeath,
            Generic,
            Hunger,
            Thirst,
            Cold,
            Drowned,
            Heat,
            Bleeding,
            Poison,
            Suicide,
            Bullet,
            Arrow,
            Slash,
            Blunt,
            Fall,
            Radiation,
            Stab,
            Explosion,
            Unknown
        }

        class Attacker
        {
            public Attacker(BaseEntity entity)
            {
                Entity = entity;
                Type = InitializeType();
                Name = InitializeName();
            }

            public BaseEntity Entity { get; }

            public string Name { get; }

            public AttackerType Type { get; }

            private AttackerType InitializeType()
            {
                if (Entity == null)
                    return AttackerType.Invalid;

                if (Entity is BasePlayer)
                    return AttackerType.Player;

                if (Entity.name.Contains("animal/"))
                    return AttackerType.Animal;

                if (Entity.name.Contains("barricades/") || Entity.name.Contains("wall.external.high"))
                    return AttackerType.Structure;

                if (Entity.name.Contains("beartrap.prefab") || Entity.name.Contains("landmine.prefab") || Entity.name.Contains("spikes.floor.prefab"))
                    return AttackerType.Trap;

                return AttackerType.Invalid;
            }

            private string InitializeName()
            {

                if (Entity == null)
                    return null;

                switch (Type)
                {
                    case AttackerType.Player:
                        return Entity.ToPlayer().displayName;

                    case AttackerType.Trap:
                    case AttackerType.Animal:
                    case AttackerType.Structure:
                        return FormatName(Entity.name);
                }

                return string.Empty;
            }
        }

        class Victim
        {
            public Victim(BaseCombatEntity entity)
            {
                Entity = entity;
                Type = InitializeType();
                Name = InitializeName();
            }

            public BaseCombatEntity Entity { get; }

            public string Name { get; }

            public VictimType Type { get; }

            private VictimType InitializeType()
            {
                if (Entity == null)
                    return VictimType.Invalid;

                if (Entity is BasePlayer)
                    return VictimType.Player;

                if (Entity.name.Contains("animals/"))
                    return VictimType.Animal;


                return VictimType.Invalid;
            }

            private string InitializeName()
            {
                switch (Type)
                {
                    case VictimType.Player:
                        return Entity.ToPlayer().displayName;

                    case VictimType.Animal:
                        return FormatName(Entity.name);
                }

                return string.Empty;
            }
        }

        class DeathMessage
        {
            public DeathMessage(Attacker attacker, Victim victim, string weapon, string damageType, string bodyPart, double distance)
            {
                Attacker = attacker;
                Victim = victim;
                Weapon = weapon;
                DamageType = damageType;
                BodyPart = bodyPart;
                Distance = distance;

                Reason = InitializeReason();
                Message = InitializeDeathMessage();

                if (_config.Distance <= 0)
                {
                    Players = BasePlayer.activePlayerList;
                }
                else
                {
                    var position = attacker?.Entity?.transform?.position;
                    if (position == null)
                        position = victim?.Entity?.transform?.position;

                    //if (position != null)
                    //    Players = BasePlayer.activePlayerList.Where(x => x.DistanceTo((UnityEngine.Vector3)position) <= _config.Distance).ToList();
                    //else
                    Players = new List<BasePlayer>();
                }

                if (victim.Type == VictimType.Player && !Players.Contains(victim.Entity.ToPlayer()))
                    Players.Add(victim.Entity.ToPlayer());

                if (attacker.Type == AttackerType.Player && !Players.Contains(attacker.Entity.ToPlayer()))
                    Players.Add(attacker.Entity.ToPlayer());
            }

            public List<BasePlayer> Players { get; }

            public Attacker Attacker { get; }

            public Victim Victim { get; }

            public string Weapon { get; }

            public string BodyPart { get; }

            public string DamageType { get; }

            public double Distance { get; }

            public DeathReason Reason { get; }

            public string Message { get; }

            private DeathReason InitializeReason()
            {
                if (Attacker.Type == AttackerType.Structure)
                    return DeathReason.Structure;

                else if (Attacker.Type == AttackerType.Trap)
                    return DeathReason.Trap;

                else if (Attacker.Type == AttackerType.Animal)
                    return DeathReason.Animal;

                else if (Victim.Type == VictimType.Animal)
                    return DeathReason.AnimalDeath;

                else if (Weapon == "F1 Grenade" || Weapon == "Survey Charge" || Weapon == "Timed Explosive Charge" || Weapon == "Satchel Charge" || Weapon == "Beancan Grenade")
                    return DeathReason.Explosion;

                else if (Victim.Type == VictimType.Player)
                    return GetDeathReason(DamageType);

                return DeathReason.Unknown;
            }

            private DeathReason GetDeathReason(string damage)
            {
                var reasons = (Enum.GetValues(typeof(DeathReason)) as DeathReason[]).Where(x => x.ToString().Contains(damage));

                if (reasons.Count() == 0)
                    return DeathReason.Unknown;

                return reasons.First();
            }

            private string InitializeDeathMessage()
            {
                string message = string.Empty;
                string reason = string.Empty;

                if (Victim.Type == VictimType.Player && Victim.Entity.ToPlayer().IsSleeping() && _config.Messages.ContainsKey(Reason + " Sleeping"))
                    reason = Reason + " Sleeping";
                else
                    reason = Reason.ToString();

                message = GetMessage(reason, _config.Messages);

                var attackerName = Attacker.Name;

                switch (Attacker.Type)
                {
                    case AttackerType.Trap:
                        attackerName = GetMessage(attackerName, _config.Traps);
                        break;

                    case AttackerType.Animal:
                        attackerName = GetMessage(attackerName, _config.Animals);
                        break;

                    case AttackerType.Structure:
                        attackerName = GetMessage(attackerName, _config.Structures);
                        break;
                }

                var victimName = Victim.Name;

                switch (Victim.Type)
                {
                    case VictimType.Animal:
                        victimName = GetMessage(victimName, _config.Animals);
                        break;
                }

                message = message.Replace("{attacker}", $"<color=#f0f0f0>{attackerName}</color>");
                message = message.Replace("{victim}", $"<color=#f0f0f0>{victimName}</color>");
                message = message.Replace("{distance}", $"<color=#9a9a9a>{Math.Round(Distance, 0)}</color>");
                message = message.Replace("{weapon}", $"<size=14><color=#9a9a9a>{GetMessage(Weapon, _config.Weapons)}</color></size>");
                message = message.Replace("{bodypart}", $"<color=#f0f0f0>{GetMessage(BodyPart, _config.BodyParts)}</color>");

                return message;
            }
        }

        #endregion

        #region Oxide Hooks

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(new PluginConfig
            {
                Cooldown = 7,
                FontSize = 15,
                Distance = -1,
                Log = true,
                ShowDeathAnimals = true,
                ShowDeathSleepers = true,

                ColorAttacker = "#ff9c00",
                ColorVictim = "#ff9c00",
                ColorDistance = "#ff9c00",
                ColorWeapon = "#ffffff",
                ColorBodyPart = "#ffffff",

                Weapons = new Dictionary<string, string>
                {
                    { "Assault Rifle", "AK47" },
                    { "Beancan Grenade", "Граната" },
                    { "Bolt Action Rifle", "Болт" },
                    { "Bone Club", "Дубина" },
                    { "Bone Knife", "Нож" },
                    { "Crossbow", "Арбалет" },
                    { "Custom SMG", "СМГ" },
                    { "Eoka Pistol", "Еока" },
                    { "F1 Grenade", "Граната" },
                    { "Hunting Bow", "Лук" },
                    { "Longsword", "Меч" },
                    { "Mace", "Булава" },
                    { "Machete", "Мачете" },
                    { "Pump Shotgun", "Дробовик" },
                    { "Revolver", "Револьвер" },
                    { "Salvaged Cleaver", "Меч" },
                    { "Salvaged Sword", "Меч" },
                    { "Semi-Automatic Pistol", "П250" },
                    { "Stone Spear", "Копьё" },
                    { "Thompson", "Томпсон" },
                    { "Waterpipe Shotgun", "Пайп" },
                    { "Wooden Spear", "Копьё" },
                    { "Hatchet", "Топор" },
                    { "Pick Axe", "Кирка" },
                    { "Salvaged Axe", "Топор" },
                    { "Salvaged Hammer", "Молот" },
                    { "Salvaged Icepick", "Кирка" },
                    { "Satchel Charge", "Сачель" },
                    { "Stone Hatchet", "Топор" },
                    { "Stone Pick Axe", "Кирка" },
                    { "Survey Charge", "Снаряд" },
                    { "Timed Explosive Charge", "С4" },
                    { "Torch", "Факел" },
                    { "Stone Pickaxe", "Кирка" },
                    { "RocketSpeed", "Ракета" },
                    { "Incendiary Rocket", "Ракета" },
                    { "Rocket", "Ракета" }

                },

                Structures = new Dictionary<string, string>
                {
                    { "Wooden Barricade", "Деревянная баррикада" },
                    { "Barbed Wooden Barricade", "Колючая деревянная баррикада" },
                    { "Metal Barricade", "Металлическая баррикада" },
                    { "High External Wooden Wall", "Высокая внешняя деревянная стена" },
                    { "High External Stone Wall", "Высокая внешняя каменная стена" }
                },

                Traps = new Dictionary<string, string>
                {
                    { "Snap Trap", "Капкан" },
                    { "Land Mine", "Мина" },
                    { "Wooden Floor Spikes", "Деревянные колья" }
                },

                Animals = new Dictionary<string, string>
                {
                    { "Boar", "Кабан" },
                    { "Horse", "Лошадь" },
                    { "Wolf", "Волк" },
                    { "Stag", "Олень" },
                    { "Chicken", "Курица" },
                    { "Bear", "Медведь" }
                },

                BodyParts = new Dictionary<string, string>
                {
                    { "body", "Тело" },
                    { "pelvis", "Таз" },
                    { "hip", "Бедро" },
                    { "left knee", "Левое колено" },
                    { "right knee", "Правое колено" },
                    { "left foot", "Левая стопа" },
                    { "right foot", "Правая стопа" },
                    { "left toe", "Левый палец" },
                    { "right toe", "Правый палец" },
                    { "groin", "Пах" },
                    { "lower spine", "Нижний позвоночник" },
                    { "stomach", "Желудок" },
                    { "chest", "Грудь" },
                    { "neck", "Шея" },
                    { "left shoulder", "Левое плечо" },
                    { "right shoulder", "Правое плечо" },
                    { "left arm", "Левая рука" },
                    { "right arm", "Правая рука" },
                    { "left forearm", "Левое предплечье" },
                    { "right forearm", "Правое предплечье" },
                    { "left hand", "Левая ладонь" },
                    { "right hand", "Правая ладонь" },
                    { "left ring finger", "Левый безымянный палец" },
                    { "right ring finger", "Правый безымянный палец" },
                    { "left thumb", "Левый большой палец" },
                    { "right thumb", "Правый большой палец" },
                    { "left wrist", "Левое запястье" },
                    { "right wrist", "Правое запястье" },
                    { "head", "Голова" },
                    { "jaw", "Челюсть" },
                    { "left eye", "Левый глаз" },
                    { "right eye", "Правый глаз" }
                },

                Messages = new Dictionary<string, string>
                {
                    { "Arrow", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Blunt",  "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Bullet", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Drowned", "" },
                    { "Explosion", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Fall", "" },
                    { "Generic", "" },
                    { "Heat", "" },
                    { "Animal", "" },
                    { "ZombieDeath", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Zombie", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "AnimalDeath", "" },
                    { "Hunger", "" },
                    { "Poison", "" },
                    { "Radiation", "" },
                    { "Slash", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Stab", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Structure", "" },
                    { "Suicide", "" },
                    { "Thirst", "" },
                    { "Trap", "" },
                    { "Cold", "" },
                    { "Guntrap", "" },
                    { "Unknown", "" },
                    { "Bleeding", "" },

                    //  Sleeping
                    { "Blunt Sleeping", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Bullet Sleeping", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Explosion Sleeping", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Generic Sleeping", "" },
                    { "Animal Sleeping", "" },
                    { "Slash Sleeping", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Stab Sleeping", "{attacker} {weapon} {victim} [{distance}м]" },
                    { "Unknown Sleeping", "" }
                }
            }, true);
        }

        private void OnServerInitialized()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
                _lastHits[entity.ToPlayer().userID] = info;
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            var _weapon = FirstUpper(info?.Weapon?.GetItem()?.info?.displayName?.english) ?? FormatName(info?.WeaponPrefab?.name);
            var _damageType = FirstUpper(victim.lastDamage.ToString());
            if (info == null)
                if (!(victim is BasePlayer) || !victim.ToPlayer().IsWounded() || !_lastHits.TryGetValue(victim.ToPlayer().userID, out info))
                    return;
            if (victim as BaseCorpse != null) return;
            var _victim = new Victim(victim);
            var _attacker = new Attacker(info.Initiator);
            if (_victim == null)
                return;
            if (_attacker == null)
                return;
            if (_victim.Type == VictimType.Invalid)
                return;
            if (_attacker.Type == AttackerType.Invalid)
                return;
            if (!_config.ShowDeathAnimals && _victim.Type == VictimType.Animal)
            {
                return;
            }
            if (!_config.ShowDeathAnimals && _attacker.Type == AttackerType.Animal)
            {
                return;
            }
            if (_victim.Type == VictimType.Player && _victim.Entity.ToPlayer().IsSleeping() && !_config.ShowDeathSleepers)
                return;
            var _bodyPart = victim?.skeletonProperties?.FindBone(info.HitBone)?.name?.english ?? "";
            var _distance = Vector3.Distance(victim.transform.position, info.Initiator.transform.position);
            AddNote(new DeathMessage(_attacker, _victim, _weapon, _damageType, _bodyPart, _distance));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }

        #endregion

        #region Core

        private void AddNote(DeathMessage note)
        {
            _notes.Insert(0, note);
            if (_notes.Count > 8)
                _notes.RemoveRange(7, _notes.Count - 8);

            RefreshUI(note);
            timer.Once(_config.Cooldown, () =>
            {
                _notes.Remove(note);
                RefreshUI(note);
            });
        }

        #endregion

        #region UI

        private void RefreshUI(DeathMessage note)
        {
            foreach (var player in note.Players)
            {
                DestroyUI(player);
                InitilizeUI(player);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ui.deathmessages");
        }

        private void InitilizeUI(BasePlayer player)
        {
            var notes = _notes.Where(x => x.Players.Contains(player)).Take(8);

            if (notes.Count() == 0)
                return;

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5078124 0.7657418", AnchorMax = "0.9978124 0.9607418" }
            }, name: "ui.deathmessages");

            double index = 1;
            foreach (var note in notes)
            {
                InitilizeLabel(container, note.Message, $"0 {index - 0.2}", $"0.99 {index}");
                index -= 0.14;
            }

            CuiHelper.AddUi(player, container);
        }

        private string InitilizeLabel(CuiElementContainer container, string text, string anchorMin, string anchorMax)
        {
            string Name = CuiHelper.GetGuid();
            container.Add(new CuiElement
            {
                Name = Name,
                Parent = "ui.deathmessages",
                Components =
                {
                    new CuiTextComponent { Align = UnityEngine.TextAnchor.MiddleRight, FontSize = _config.FontSize, Text = text},
                    new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1.0 -0.5" }
                }
            });
            return Name;
        }

        #endregion

        #region Helpers

        private static string FirstUpper(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return string.Join(" ", str.Split(' ').Select(x => x.Substring(0, 1).ToUpper() + x.Substring(1, x.Length - 1)).ToArray());
        }

        private static string FormatName(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return string.Empty;

            var formatedPrefab = FirstUpper(prefab.Split('/').Last().Replace(".prefab", "").Replace(".entity", "").Replace(".weapon", "").Replace(".deployed", "").Replace("_", "."));

            switch (formatedPrefab)
            {
                case "Autoturret.deployed": return "Auto Turret";
                case "Beartrap": return "Snap Trap";
                case "Landmine": return "Land Mine";
                case "Spikes.floor": return "Wooden Floor Spikes";

                case "Barricade.wood": return "Wooden Barricade";
                case "Barricade.woodwire": return "Barbed Wooden Barricade";
                case "Barricade.metal": return "Metal Barricade";
                case "Wall.external.high.wood": return "High External Wooden Wall";
                case "Wall.external.high.stone": return "High External Stone Wall";
                case "Gates.external.high.stone": return "High External Wooden Gate";
                case "Gates.external.high.wood": return "High External Stone Gate";

                case "Stone.hatchet": return "Stone Hatchet";
                case "Stone.pickaxe": return "Stone Pickaxe";
                case "Survey.charge": return "Survey Charge";
                case "Explosive.satchel": return "Satchel Charge";
                case "Explosive.timed": return "Timed Explosive Charge";
                case "Grenade.beancan": return "Beancan Grenade";
                case "Grenade.f1": return "F1 Grenade";
                case "Hammer.salvaged": return "Salvaged Hammer";
                case "Axe.salvaged": return "Salvaged Axe";
                case "Icepick.salvaged": return "Salvaged Icepick";
                case "Spear.stone": return "Stone Spear";
                case "Spear.wooden": return "Wooden Spear";
                case "Knife.bone": return "Bone Knife";
                case "Rocket.basic": return "Rocket";
                case "Rocket.hv": return "RocketSpeed";


                default: return formatedPrefab;
            }
        }

        private static string GetMessage(string name, Dictionary<string, string> source)
        {
            if (source.ContainsKey(name))
                return source[name];

            return name;
        }

        #endregion
    }
}