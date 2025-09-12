using System;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using Facepunch.Extend;
using System.Collections;
using UnityEngine.Networking;
namespace Oxide.Plugins
{
    [Info("AntiCheatScience", "PRESSF | https://hazard-plugins.space", "3.6.2")]
    [Description("The best anti-cheat for piratedRust | discord: pressfwd")]
    class AntiCheatScience : RustPlugin
    {
        [PluginReference] Plugin MultiFighting, Battles, AimTrain, ImageLibrary, NightVision, RustApp, AutoBattleMetricsBan, FreePlayerFriendly, FLuma;

        #region Configuration
        const bool LangEN = true;
        private static ConfigData _config;

        public class ConfigData
        {
            public OtherConfig Global { get; set; } = new OtherConfig();
            public AimConfig Aim { get; set; } = new AimConfig();
            public PilotFireConfig PilotFire { get; set; } = new PilotFireConfig();
            public AimLockConfig AimLock { get; set; } = new AimLockConfig();
            public CodeLockConfig CodeLock { get; set; } = new CodeLockConfig();
            public NHDetect NightFire { get; set; } = new NHDetect();
            public ManipConfig Manipulator { get; set; } = new ManipConfig();
            public FlyFire FlyFire { get; set; } = new FlyFire();
            public MeleeAttackConfig MeleeAttack { get; set; } = new MeleeAttackConfig();
            public SAimConfig SilentAim { get; set; } = new SAimConfig();
            public FlyConfig FlyHack { get; set; } = new FlyConfig();
            public NFDConfig NoFallDamage { get; set; } = new NFDConfig();
            public SpiderConfig SpiderHack { get; set; } = new SpiderConfig();
            public ModelStateConfig ModelState { get; set; } = new ModelStateConfig();
            public StashConfig Traps { get; set; } = new StashConfig();
            public SteamProxyConfig Steam { get; set; } = new SteamProxyConfig();
            public LogAndConfig Logs { get; set; } = new LogAndConfig();
            public RAConfig RustAPP { get; set; } = new RAConfig();
            public BMConfig BattleMetrics { get; set; } = new BMConfig();
            public DBConfig DataBase { get; set; } = new DBConfig();
        }

        public class OtherConfig
        {
            [JsonProperty(LangEN ? "Console command for ban? %steamid% - player id , %reason% - ban reason" : "Console command to Ban Player? %steamid% - playerID , %reason% - Reason")]
            public string BanCommand { get; set; } = "ban %steamid% \"%reason%\"";

            [JsonProperty(LangEN ? "Console command for kick? %steamid% - player id , %reason% - kick reason" : "Console command for kick? %steamid% - playerID , %reason% - Reason")]
            public string KickCommand { get; set; } = "kick %steamid% \"%reason%\"";

            [JsonProperty(LangEN ? "Discord ID of the tech admin (how to get it?: https://www.youtube.com/watch?v=9T0KqA8akrY | no, the video is not mine)" : "Discord ID of the tech admin (how to get it?: https://www.youtube.com/watch?v=9T0KqA8akrY | no, the video is not mine)")]
            public string DiscordID { get; set; } = "unknown";

            [JsonProperty(LangEN ? "Block damage on detection?" : "Block damage on detection?")]
            public bool isAimBlocDamage { get; set; } = false;

            [JsonProperty(LangEN ? "Confirm that the plugin is configured? (version 3.6.2)" : "Confirm that the plugin is configured? (version 3.6.2)")]
            public bool isSettingsS { get; set; } = false;
        }

        public class AimConfig
        {
            [JsonProperty(LangEN ? "Ban/kick reason for AIM" : "Ban/kick reason for AIM")]
            public string NameAim { get; set; } = "AC - Protocol #1!";

            [JsonProperty(LangEN ? "Punishment measure for AIM RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Punishment measure for AIM RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all server by IP")]
            public int banMeraAim { get; set; } = 1;

            [JsonProperty(LangEN ? "Punishment measure for AIM?(true - Ban, false - Kick)" : "Punishment measure for AIM?(true - Бан, false - Kick)")]
            public bool isAimBan { get; set; } = true;

            [JsonProperty(LangEN ? "Use function?" : "Use function?")]
            public bool isAim { get; set; } = true;

            public RifleConfig RifleSettings { get; set; } = new RifleConfig();
            public PistolConfig PistolSettings { get; set; } = new PistolConfig();
            public PPConfig PPSetings { get; set; } = new PPConfig();
            public SniperConfig SniperRifleSettings { get; set; } = new SniperConfig();
            public BowConfig BowSettings { get; set; } = new BowConfig();
        }

        public class RifleConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Kick for headshot from how many meters? (Sniper rifles are already considered)" : "Kick for headshot from how many meters? (Sniper rifles are already considered)")] 
            public float metrrifle { get; set; } = 220f;

            [JsonProperty(LangEN ? "Seconds until detection reset?" : "Seconds until detection reset?")]
            public float isTwoRifCD { get; set; } = 5;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of headshots in N seconds?" : "Number of headshots in N seconds?")]
            public int isTwoRifH { get; set; } = 3;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of body shots in N seconds?" : "Number of body shots in N seconds?")]
            public int isTwoRifB { get; set; } = 9;
        }
        string url;
        string note;
        public class PistolConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Kick for headshot from pistols from how many meters?" : "Kick for headshot from pistols from how many meters?")]
            public float metrpistol { get; set; } = 140f;

            [JsonProperty(LangEN ? "Seconds until detection reset?" : "Seconds until detection reset?")]
            public float isTwoPistolCD { get; set; } = 5;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of headshots in N seconds?" : "Number of headshots in N seconds?")]
            public int isTwoPistolH { get; set; } = 3;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of body shots in N seconds?" : "Number of body shots in N seconds?")]
            public int isTwoPistolB { get; set; } = 6;
        }

        public class PPConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Kick for headshot from SMGs from how many meters?" : "Kick for headshot from SMGs from how many meters?")]
            public float metrpp { get; set; } = 180f;

            [JsonProperty(LangEN ? "Seconds until detection reset?" : "Seconds until detection reset?")]
            public float isTwoPPCD { get; set; } = 5;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of headshots in N seconds?" : "Number of headshots in N seconds?")]
            public int isTwoPPH { get; set; } = 3;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of body shots in N seconds?" : "Number of body shots in N seconds?")]
            public int isTwoPPB { get; set; } = 5;
        }

        public class SniperConfig
        {
            [JsonProperty(LangEN ? "Float" : "Float")]
            public float metrsniper { get; set; } = 500f;
        }

        public class BowConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Kick for headshot from bows from how many meters?" : "Kick for headshot from bows from how many meters?")]
            public float metrbow { get; set; } = 75f;

            [JsonProperty(LangEN ? "Punishment measure?(true - Ban, false - Kick)" : "Punishment measure?(true - Ban, false - Kick)")]
            public bool isbowBan { get; set; } = true;

            [JsonProperty(LangEN ? "Seconds until detection reset?" : "Seconds until detection reset?")]
            public float isTwoBowCD { get; set; } = 5;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of headshots in N seconds?" : "Number of headshots in N seconds?")]
            public int isTwoBowH { get; set; } = 2;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of body shots in N seconds?" : "Number of body shots in N seconds?")]
            public int isTwoBowB { get; set; } = 3;
        }

        public class PilotFireConfig
        {
            [JsonProperty(LangEN ? "Ban/kick reason for shooting from driver's seat" : "Ban/kick reason for shooting from driver's seat")]
            public string NameAimDrive { get; set; } = "AC - Protocol #2!";

            [JsonProperty(LangEN ? "Punishment measure for shooting from driver's seat RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Punishment measure for shooting from driver's seat RustAPP (1 - Бан, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP")]
            public int banMeraAimDrive { get; set; } = 1;

            [JsonProperty(LangEN ? "Use function?" : "Use Function?")]
            public bool isPF { get; set; } = true;
        }

        public class AimLockConfig
        {
            [JsonProperty(LangEN ? "Ban/kick reason for AimLock" : "Ban/kick reason for AimLock")]
            public string NameAimLock { get; set; } = "AC - Protocol #5!";

            [JsonProperty(LangEN ? "Punishment measure for AimLock RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Punishment measure for AimLock RustAPP (1 - Бан, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP")]
            public int banMeraAimLock { get; set; } = 1;

            [JsonProperty(LangEN ? "Punishment measure for AimLock?(true - Ban, false - Kick)" : "Punishment measure for AimLock?(true - Ban, false - kick)")]
            public bool isAimLockBan { get; set; } = true;

            [JsonProperty(LangEN ? "Use func?" : "Use Function?")]
            public bool isAimLock { get; set; } = true;
        }

        public class CodeLockConfig
        {
            [JsonProperty(LangEN ? "Detection reason for entering the code of a banned player's house" : "Detection reason for entering the code of a banned player's house")]
            public string NameCodeLock { get; set; } = "AC - Protocol #6!";

            [JsonProperty(LangEN ? "Use func?" : "Use Function?")]
            public bool isCodeLock { get; set; } = true;
        }

        public class NHDetect
        {
            [JsonProperty(LangEN ? "Log hit at night?" : "Log hit at night?")]
            public bool isNH { get; set; } = true;

            [JsonProperty(LangEN ? "Detection distance" : "Расстояние для детекта")]
            public float metrnh { get; set; } = 100f;

            [JsonProperty(LangEN ? "Detection reason for hit at night" : "Причина детекта за попадание в ночное время")]
            public string NameNightShot { get; set; } = "AC - Protocol #9!";
        }

        public class ManipConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Ban for test manipulator detection? (not few complaints for false ban, although no video evidence)" : "Банить за тестовый детект манипулятора? (не мало жалоб за ложный бан, хотя видео док-ва не дают)")]
            public bool isManipT { get; set; } = true;

            [JsonProperty(LangEN ? "Ban/kick reason for manipulator" : "Причина бана/кика за манипулятор")]
            public string NameAimM { get; set; } = "AC - Protocol #7!";

            [JsonProperty(LangEN ? "Punishment measure for manipulator RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за манипулятор RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraAimM { get; set; } = 1;

            [JsonProperty(LangEN ? "Use function?" : "Использовать функцию?")]
            public bool isManip { get; set; } = true;
        }

        public class FlyFire
        {
            [JsonProperty(LangEN ? "Ban/kick reason for shooting in the air" : "Причина бана/кика за стрельбу в воздухе")]
            public string NameFlyFire { get; set; } = "AC - Protocol #8!";

            [JsonProperty(LangEN ? "Punishment measure for shooting in the air RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за стрельбу в воздухе RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraFlyFire { get; set; } = 1;

            [JsonProperty(LangEN ? "Use function?" : "Использовать функцию?")]
            public bool isFF { get; set; } = true;
        }

        public class MeleeAttackConfig
        {
            [JsonProperty(LangEN ? "Ban/kick reason for MeleeAttack" : "Причина бана/кика за MeleeAttack")]
            public string NameMeleeAttack { get; set; } = "AC - Protocol #10!";

            [JsonProperty(LangEN ? "Punishment measure for MeleeAttack RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за MeleeAttack RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraMeleeAttack { get; set; } = 1;

            [JsonProperty(LangEN ? "Punishment measure for MeleeAttack?(true - Ban, false - Kick)" : "Мера наказания за MeleeAttack?(true - Бан, false - Кик)")]
            public bool isMeleeAttackBan { get; set; } = true;

            [JsonProperty(LangEN ? "Use function? (NOW IN BETA TEST (FROM 29.08.2024))" : "Использовать функцию? (NOW IN BETA TEST (FROM 29.08.2024))")]
            public bool isMA { get; set; } = true;
        }

        public class SAimConfig
        {
            [JsonProperty(LangEN ? "Ban/kick reason for SilentAim" : "Причина бана/кика за SilentAim")]
            public string NameSAim { get; set; } = "AC - Protocol #11!";

            [JsonProperty(LangEN ? "Punishment measure for SilentAim RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за SilentAim RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraSAim { get; set; } = 1;

            [JsonProperty(LangEN ? "Number of detections in 15 seconds for ban" : "Кол-во детектов за 15 сек для бана")]
            public int maxDetect { get; set; } = 3;

            [JsonProperty(LangEN ? "Punishment measure for SilentAim?(true - Ban, false - Kick)" : "Мера наказания за SilentAim?(true - Бан, false - Кик)")]
            public bool isSAimBan { get; set; } = true;

            [JsonProperty(LangEN ? "Use function?" : "Использовать функцию?")]
            public bool isSA { get; set; } = true;
        }

        public class FlyConfig
        {
            [JsonIgnore]
            [JsonProperty(LangEN ? "Enable punishment for FlyHack?" : "Включить наказание за FlyHack?")]
            public bool isFly { get; set; } = true;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Punishment measure?(true - Ban, false - Kick)" : "Мера наказания?(true - Бан, false - Кик)")]
            public bool isFlyBan { get; set; } = false;

            [JsonIgnore]
            [JsonProperty(LangEN ? "Number of detections for FlyHack punishment?" : "За сколько детектов наказывать за FlyHack?")]
            public int DCount { get; set; } = 3;

            [JsonProperty(LangEN ? "Ban/kick reason for FlyHack" : "Причина бана/кика за FlyHack")]
            public string NameFly { get; set; } = "AC - Protocol #3!";
        }

        public class NFDConfig
        {
            [JsonProperty(LangEN ? "Detect? (BETA functionality)" : "Детектить? (BETA функционал)")]
            public bool isNFD { get; set; } = false;

            [JsonProperty(LangEN ? "Ban/kick reason for NoFallDamage" : "Причина бана/кика за NoFallDamage")]
            public string NameNFD { get; set; } = "AC - Protocol #12!";

            [JsonProperty(LangEN ? "Punishment measure for NFD RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за NFD RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraNFD { get; set; } = 1;
        }

        public class SpiderConfig
        {
            [JsonProperty(LangEN ? "Detect? (BETA functionality)" : "Детектить? (BETA функционал)")]
            public bool isSpider { get; set; } = false;

            [JsonProperty(LangEN ? "Ban/kick reason for SpiderHack" : "Причина бана/кика за SpiderHack")]
            public string NameSpider { get; set; } = "AC - Protocol #13!";

            [JsonProperty(LangEN ? "Punishment measure for SpiderHack RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за SpiderHack RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraSpider { get; set; } = 1;
        }

        public class ModelStateConfig
        {
            [JsonProperty(LangEN ? "Detect? (BETA functionality)" : "Детектить? (BETA функционал)")]
            public bool isModelState { get; set; } = false;

            [JsonProperty(LangEN ? "Enable auto check of the entire server once every time?" : "Включить авто проверку всего сервера раз в какое то время?")]
            public bool isModelStateAuto { get; set; } = true;

            [JsonProperty(LangEN ? "Ban/kick reason for ModelState" : "Причина бана/кика за ModelState")]
            public string NameMS { get; set; } = "AC - Protocol #14!";

            [JsonProperty(LangEN ? "Punishment measure for SpiderHack RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за SpiderHack RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraMS { get; set; } = 1;
        }

        public class StashConfig
        {
            [JsonProperty(LangEN ? "Ban for digging up someone else's stashes?" : "Банить за откапывание чужих стешей?")]
            public bool isStash { get; set; } = true;

            [JsonProperty(LangEN ? "Ban for digging up N stashes" : "Банить за откапывание N стешей")]
            public int stashBCount { get; set; } = 3;

            [JsonProperty(LangEN ? "Ban/kick reason for digging up someone else's stashes" : "Причина бана/кика за откапывание чужих стешей")]
            public string NameStash { get; set; } = "AC - Protocol #4!";

            [JsonProperty(LangEN ? "Punishment measure for stashes RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за стеши RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraStash { get; set; } = 1;
        }

        public class SteamProxyConfig
        {
            [JsonProperty(LangEN ? "Check players for Proxy through ip2location?" : "Проверять игроков на Proxy через ip2location?")]
            public bool PROXY_CHECT { get; set; } = false;

            [JsonProperty(LangEN ? "API Key for player IP checks (https://www.ip2location.io/)" : "API Ключ для проверок IP игроков (https://www.ip2location.io/)")]
            public string YOUR_API_KEY { get; set; } = "000000";

            [JsonProperty(LangEN ? "Check registration date through steam?" : "Проверять дату регистрации через steam?")]
            public bool STEAM_CHECT { get; set; } = false;

            [JsonProperty(LangEN ? "API Key (REQUIRED!!) https://steamcommunity.com/dev/apikey" : "API Ключ (ОБЯЗАТЕЛЬНО!!) https://steamcommunity.com/dev/apikey")]
            public string SteamAPI { get; set; } = "123123";

            [JsonProperty(LangEN ? "Do not allow the account if it was created less than N days ago?" : "Не пускать аккаунт если он создан мение N дней назад?")]
            public int SteamDays { get; set; } = 5;

            [JsonProperty(LangEN ? "Permission with which you can enter with a young account" : "Пермишенс с которым можно зайти с молодого аккаунта")]
            public string SteamDaysIgnore { get; set; } = "ignoremolodoiacc";
        }

        public class LogAndConfig
        {
            [JsonProperty(LangEN ? "Link to Discord webhook [FOR DETECTS]" : "Ссылка на вебхук Discord [ДЛЯ ДЕТЕКТОВ]")]
            public string WebhookD { get; set; } = "1";

            [JsonProperty(LangEN ? "Link to Discord webhook [FOR BANS]" : "Ссылка на вебхук Discord [ДЛЯ БАНОВ]")]
            public string WebhookB { get; set; } = "1";

            [JsonProperty(LangEN ? "Log detections in Discord?" : "Логировать детекты в Discord?")]
            public bool isLog { get; set; } = true;

            [JsonProperty(LangEN ? "Image in logging (standard version)" : "Картинка в логировании (версия стандартная)")]
            public string WebhookImage { get; set; } = "https://media.giphy.com/media/V4pAZ7bxf1rOFoLCWS/giphy.gif";
        }

        public class RAConfig
        {
            [JsonProperty(LangEN ? "Work with RustAPP?" : "Работать с RustAPP?")]
            public bool isRustAPP { get; set; } = false;

            [JsonProperty(LangEN ? "Log detections in RustAPP?" : "Логировать детекты в RustAPP?")]
            public bool isRustAPPlog { get; set; } = false;

            [JsonProperty(LangEN ? "IDs that cannot be reported 765000000000000, 76500000000000001" : "Айди, репорт на которых отправить нельзя 765000000000000, 76500000000000001")]
            public List<string> idAntiReport { get; set; } = new List<string>();

            [JsonProperty(LangEN ? "Automatically ban if the player received N reports?" : "Банить автоматически если игрок получил N кол-во репортов?")]
            public bool isRustAPPbanAuto { get; set; } = false;

            [JsonProperty(LangEN ? "Number of reports required for automatic ban?" : "Сколько репортов нужно получить для автоматического бана?")]
            public int banAutoInt { get; set; } = 5;

            [JsonProperty(LangEN ? "Punishment measure for max reports RustAPP (1 - Ban, 2 - Ban on all servers, 3 - On server by IP, 4 - on all servers by IP)" : "Мера наказания за макс. репортов RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraMR { get; set; } = 1;

            [JsonProperty(LangEN ? "Ban only in RustAPP?" : "Банить только в RustAPP?")]
            public bool isRustAPPbanOnly { get; set; } = false;
        }

        public class BMConfig
        {
            [JsonProperty(LangEN ? "Work with BattleMetrics? [NOT WORK]" : "Работать с BattleMetrics? [НЕ РАБОТАЕТ]")]
            public bool isBattleMetrics { get; set; } = false;
        }

        public class DBConfig
        {
            [JsonProperty(LangEN ? "Work with the database of Cheaters? (records for half a year before 03/03/2024, main IDs from steam files and active accounts)" : "Работать с ДатаБазой читеров? (записи за пол года до 03.03.2024, основные айди из файлов стима и активные аккаунты)")]
            public bool isDataBase { get; set; } = false;

            [JsonProperty(LangEN ? "IDs to run on the server if they are in the database 765000000000000, 76500000000000001" : "Айди, которые запускать на сервер если они есть в базе 765000000000000, 76500000000000001")]
            public List<string> idAntiKick { get; set; } = new List<string>();

            [JsonProperty(LangEN ? "Enable checking in the invishack database? more than 800 entries obtained by hacking and an agreement with the cheat developer" : "Включить проверку в базе invishack? более 800 записей, полученые путем взлома и договором с разработчиком чита")]
            public bool isInvishack { get; set; } = false;

            [JsonProperty(LangEN ? "Enable checking in the amphetamine database? more than 400 records obtained through an agreement with the cheat developer" : "Включить проверку в базе amphetamine? более 400 записей, полученые путем договора с разработчиком чита")]
            public bool isAmphetamine { get; set; } = false;

            [JsonProperty(LangEN ? "Enable checking in GOPOTA (MR.BIG DICK) database? more than 5000 records obtained through an agreement with the developer" : "Включить проверку в GOPOTA (MR.BIG DICK)? более 5000 записей, полученые путем договора с разработчиком")]
            public bool isGopota { get; set; } = false;
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                Debug.LogError("[AntiCheatScience] CONFIGURATION ERROR, LOADING DEFAULT CONFIGURATION");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        [ConsoleCommand("acs_loadconfig")]
        private void Command (ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                Debug.LogWarning("[AntiCheatScience] The configuration reading process has started!");
                LoadConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        #endregion Configuration

        List<string> triggerWord = new List<string> {
            "bay", "pirate", "sliv", "fixed", "fix", "lick", "слив", "sliv", "слитые", "rustbay", "rustpirate", "skuli"
        };

        void OnPluginLoaded(Plugin plugin)
        {
            foreach (var pluginSliv in plugins.GetAll())
            {
                if (pluginSliv.Author != null && pluginSliv.Author.ToLower().Contains(triggerWord.ToString()) || pluginSliv.Description != null && pluginSliv.Description.ToLower().Contains(triggerWord.ToString()))
                {
                    Debug.LogError("UGH, GARBAGE MAN, REMOVE THE LEAKED PLUGINS!!!");
                    Debug.LogError("REMOVE THE LEAKED PLUGINS");
                    Debug.LogError("REMOVE THE LEAKED PLUGINS");
                    Debug.LogError("REMOVE THE LEAKED PLUGINS");
                    timer.Once(5f, () =>
                    {
                        Interface.Oxide.RootPluginManager.RemovePlugin(pluginSliv);
                        rust.RunServerCommand("quit");
                    });
                }
            }
        }

        private int MeleeDetect = 0;

        object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (info == null || info.HitEntity == null || !_config.MeleeAttack.isMA)
                return null;

            var hitPlayer = info.HitEntity as BasePlayer;
            if (hitPlayer == null || BasePlayer.sleepingPlayerList.Contains(hitPlayer))
                return null;

            if (Mathf.Abs(player.transform.position.y - hitPlayer.transform.position.y) >= 1f)
                return null;

            var parentEntity = player.GetParentEntity();
            if (parentEntity is CargoShip || parentEntity is BaseBoat || parentEntity is RidableHorse || parentEntity is HorseCorpse || parentEntity is Tugboat || parentEntity is ScrapTransportHelicopter || parentEntity is Minicopter || parentEntity is MiningQuarry)
                return null;

            if (hitPlayer.GetMountedVehicle() || player.GetMountedVehicle())
                return null;
            
            if (MultiFighting != null)
            {
                if ((bool)MultiFighting.CallHook("IsSteam", player.Connection))
                    return null;
            }

            if (FLuma != null)
            {
                if ((bool)FLuma.CallHook("IsSteam", player.Connection))
                    return null;
            }

            if (FreePlayerFriendly != null)
            {
                if ((bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString))
                    return null;
            }

            if (Battles != null)
            {
                if ((bool)Battles.CallHook("IsPlayerOnBattle", player.userID))
                    return null;
            }

            if (AimTrain != null)
            {
                if ((bool)AimTrain.CallHook("IsAimTraining", player.userID))
                    return null;
            }

            float distance = Vector3.Distance(player.transform.position, hitPlayer.transform.position);
            if (distance <= 0.9588763f)
                return null;

            if (info.HitEntity.IsNpc)
                return null;

            float num = 1f + ConVar.AntiHack.melee_forgiveness;
            float melee_clientframes = ConVar.AntiHack.melee_clientframes;
            float melee_serverframes = ConVar.AntiHack.melee_serverframes;
            float num2 = (player.desyncTimeClamped + melee_clientframes / 60f + melee_serverframes * Mathx.Max(UnityEngine.Time.deltaTime, UnityEngine.Time.smoothDeltaTime, UnityEngine.Time.fixedDeltaTime)) * num;

            Vector3 startPos = player.eyes.position;
            BaseMelee weapon = info.WeaponPrefab as BaseMelee;
            float maxDistance = weapon.maxDistance + weapon.attackRadius;
            float maxRadius = weapon.attackRadius + num2 * 0.5f;
            float maxHitDistance = maxDistance + num2 * 0.5f;

            RaycastHit hit;
            bool inRadius = Physics.SphereCast(startPos, maxRadius + 0.6f, player.eyes.BodyForward(), out hit, maxHitDistance, 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore) && hit.collider.name.Contains("player");
            string weaponM = info.Weapon?.GetItem()?.info.shortname;
            if (weaponM.Contains("jackhammer") || weaponM.Contains("chainsaw") || weaponM.Contains("flash") || weaponM.Contains("torch"))
                return null;

            if (!inRadius)
            {
                player.stats.combat.LogInvalid(info, "Урон заблокирован АнтиЧитом");
                MeleeDetect++;
                DetectNo10(player, MeleeDetect, weaponM, startPos, maxRadius, hit, maxHitDistance, false);
                if (MeleeDetect == 1)
                {
                    timer.Once(30f, () => MeleeDetect = 0);
                }
                if (MeleeDetect == 3)
                {
                    DetectNo10(player, MeleeDetect, weaponM, startPos, maxRadius, hit, maxHitDistance, true);
                    if (_config.MeleeAttack.isMeleeAttackBan)
                    {
                        if (_config.RustAPP.isRustAPP)
                        {
                            string command = $"ra.ban {player.userID.ToString()} \"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]\"";
                            switch (_config.MeleeAttack.banMeraMeleeAttack)
                            {
                                case 2:
                                    command += " --global";
                                    break;
                                case 3:
                                    command += " --ban-ip";
                                    break;
                                case 4:
                                    command += " --ban-ip --global";
                                    break;
                            }
                            rust.RunServerCommand(command);
                        }
                        if (!_config.RustAPP.isRustAPPbanOnly)
                        {
                            string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]");
                            rust.RunServerCommand($"{commanda}");
                        }
                    }
                    else
                    {
                        string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                if (_config.Global.isAimBlocDamage)
                {
                    info.damageTypes.ScaleAll(0.01f);
                }
                return null;
            }
            return null;
        }

        /*private const string DummyPrefab = "assets/prefabs/player/player.prefab";
        private const float DummyDuration = 1f;
        private const float SpawnProbability = 0.15f;
        private BaseEntity dummyPlayer;
        private Vector3 spawnPosition;

        private void SpawnDummyPlayer(BasePlayer suspectPlayer)
        {
            if (dummyPlayer != null && !dummyPlayer.IsDestroyed)
            {
                dummyPlayer.Kill();
            }

            Vector3 forward = suspectPlayer.eyes.HeadForward();
            Vector3 left = Vector3.Cross(forward, Vector3.up).normalized;

            float forwardDistance = UnityEngine.Random.Range(5f, 6f); 
            float leftOffset = 0.7f; 

            spawnPosition = suspectPlayer.eyes.position + forward * forwardDistance + left * leftOffset;

            dummyPlayer = GameManager.server.CreateEntity(DummyPrefab, spawnPosition, suspectPlayer.eyes.rotation);
            if (dummyPlayer == null) return;

            dummyPlayer.Spawn();

            dummyPlayer.gameObject.AddComponent<DummyHitDetector>().Initialize(this, suspectPlayer);

            timer.Once(DummyDuration, () =>
            {
                if (dummyPlayer != null && !dummyPlayer.IsDestroyed)
                {
                    dummyPlayer.Kill();
                    dummyPlayer = null;
                }
            });
        }

        private class DummyHitDetector : MonoBehaviour
        {
            private AntiCheatScience plugin;
            private BasePlayer suspect;
            private int DummyDetect = 0;

            public void Initialize(AntiCheatScience plugin, BasePlayer suspect)
            {
                this.plugin = plugin;
                this.suspect = suspect;
            }

            private void OnCollisionEnter(Collision collision)
            {
                var hitPlayer = collision.gameObject.GetComponent<BasePlayer>();
                if (hitPlayer != null && hitPlayer == suspect)
                {
                    DummyDetect++;

                }
            }
        }*/

        Timer spiderTimer;
        private Dictionary<ulong, bool> playerReportsIQ = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> spiderDetectC = new Dictionary<ulong, int>();
        private string _Layer1 = "SpecMenu";
        void SpecMenu(BasePlayer adminPlayer, BasePlayer player)
        {
            CuiHelper.DestroyUi(adminPlayer, _Layer1);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel 
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.9 0.5", AnchorMax = "0.9 0.5", OffsetMin = "-400 -250", OffsetMax = "100 -50" }
            }, "Overlay", _Layer1);

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","bg")
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.255 0.76", AnchorMax = "1 0.76", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = $"{player.displayName}", Align = TextAnchor.MiddleLeft, FontSize = 18, VerticalOverflow = VerticalWrapMode.Overflow }
            }, _Layer1);

            /*container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.17 0.65", AnchorMax = "1 0.65", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = $"{banReason}", Align = TextAnchor.MiddleLeft, FontSize = 18, VerticalOverflow = VerticalWrapMode.Overflow }
            }, _Layer1);*/

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "ban",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","ban")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.2 0.2",AnchorMax = "0.2 0.2",OffsetMin = "-60 -20",OffsetMax = "60 20"}
                }
            });
            
            container.Add(new CuiButton
            {
                Button = { Command = $"cmd.sosiban {player.UserIDString} {adminPlayer.UserIDString} \"Результат слежки\"", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "ban");

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "stop",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","stop")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.8 0.2", AnchorMax = "0.8 0.2", OffsetMin = "-60 -20", OffsetMax = "60 20"}
                }
            });
            
            container.Add(new CuiButton
            {
                Button = { Command = "cmd.acspec stop", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "stop");
            
            CuiHelper.AddUi(adminPlayer, container);
        }
        void OnSendedReport(BasePlayer Sender, UInt64 TargetID, String Reason)
        {
            BasePlayer victim = BasePlayer.FindByID(TargetID);
            if (victim != null || victim != null && victim.IsAdmin)
               return;
            SeeSpider(victim);
        }
        void OnStartedChecked(BasePlayer Target, BasePlayer Moderator, Boolean IsConsole = false)
        {
            if (playerReportsIQ.ContainsKey(Target.userID))
            {
                playerReportsIQ.Remove(Target.userID);
            }
        }
        void RustApp_OnCheckNoticeShowed(BasePlayer player)
        {
            if (playerReportsIQ.ContainsKey(player.userID))
            {
                playerReportsIQ.Remove(player.userID);
            }
        }
        object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {  
            if (playerReportsIQ.ContainsKey(player.userID))
            {
                if (playerReportsIQ[player.userID])
                {
                    if (spiderDetectC.ContainsKey(player.userID))
                    {
                        if (spiderDetectC[player.userID] != 0)
                        {
                            if (entity.ToString().Contains("floor"))
                            {
                                return "ты кого хочешь ноебать?";
                            }
                        }
                    }
                }
            }
            return null;
        }
        void SeeSpider(BasePlayer player)
        {
            if (!playerReportsIQ.ContainsKey(player.userID))
            {
                playerReportsIQ.Add(player.userID, true);
            }

            if (_config.SpiderHack.isSpider)
            {
                Vector3 oldPos = player.transform.position;
                spiderTimer = timer.Every(0.5f, () =>
                {
                    if (playerReportsIQ[player.userID])
                    {
                        string nearbyObjects = GetNearbyObjects(player.transform.position, 1.5f);
                        if (HasForbiddenObjects(nearbyObjects))
                        {
                            return;
                        }
                        if (player.GetMountedVehicle())
                        {
                            if (!spiderTimer.Destroyed)
                                spiderTimer.Destroy();
                            timer.Once(60f, () =>
                            {
                                SeeSpider(player);
                            });
                        }
                        if (playerReportsIQ[player.userID])
                        {
                            float cumulativeYIncrease = 0f;
                            Vector3 newPos = player.transform.position;
                            float deltaX = Mathf.Abs(newPos.x - oldPos.x);
                            float deltaY = newPos.y - oldPos.y;
                            float deltaZ = Mathf.Abs(newPos.z - oldPos.z);

                            if (deltaY > 0)
                            {
                                cumulativeYIncrease += deltaY;
                            }

                            oldPos = newPos;

                            if (cumulativeYIncrease >= 0.7f && deltaX <= 0.7f && deltaZ <= 0.7f)
                            {
                                if (!spiderDetectC.ContainsKey(player.userID))
                                {
                                    spiderDetectC.Add(player.userID, 0);
                                }
                                spiderDetectC[player.userID]++;
                                cumulativeYIncrease = 0f; 

                                if (spiderDetectC[player.userID] >= 3)
                                {
                                    DetectNo13(player);
                                    playerReportsIQ[player.userID] = false;
                                    if (!spiderTimer.Destroyed)
                                        spiderTimer.Destroy();

                                    if (_config.RustAPP.isRustAPP)
                                    {
                                        string command = $"ra.ban {player.userID.ToString()} \"{_config.SpiderHack.NameSpider} [AntiCheatScience]\"";
                                        switch (_config.SpiderHack.banMeraSpider)
                                        {
                                            case 2:
                                                command += " --global";
                                                break;
                                            case 3:
                                                command += " --ban-ip";
                                                break;
                                            case 4:
                                                command += " --ban-ip --global";
                                                break;
                                        }
                                        rust.RunServerCommand(command);
                                    }
                                    if (!_config.RustAPP.isRustAPPbanOnly)
                                    {
                                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.SpiderHack.NameSpider);
                                        rust.RunServerCommand($"{commanda}");
                                    }
                                    spiderDetectC[player.userID] = 0;
                                }
                            }
                            else
                            {
                                if (deltaY <= 0)
                                {
                                    cumulativeYIncrease = 0f;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!spiderTimer.Destroyed)
                            spiderTimer.Destroy();
                    }
                });
            }
        }

        object OnPlayerRespawn(BasePlayer player, BasePlayer.SpawnPoint spawnPoint)
        {
            if (_config.DataBase.isDataBase)
            {
                string url = $"http://hazard-plugins.space/ACS/check.php?steamid={player.UserIDString}";
                webrequest.Enqueue(url.Replace("#", "%23"), "", (code, response) =>
                {
                    if (code == 200)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(response))
                            {
                                Debug.LogError(LangEN ? "[AntiCheatScience] [DataBase] Received an empty response from the database!" : "[AntiCheatScience] [DataBase] Получен пустой ответ от базы данных!");
                                return;
                            }
                            
                            string[] parts = response.Split(';');
                            if (parts.Length != 2)
                            {
                                Debug.LogError(LangEN ? $"[AntiCheatScience] [DataBase] Unexpected response format from the database [response: {response}]!" : $"[AntiCheatScience] [DataBase] Неожиданный формат ответа от базы данных [ответ: {response}]!");
                                return;
                            }

                            string naideno = parts[0];
                            string file = parts[1];

                            if (naideno == "0")
                            {
                                Puts(LangEN ? $"{player.UserIDString} not found in the cheaters database" : $"{player.UserIDString} не найден в базе читеров");
                            }
                            else
                            {
                                if (file == "amphetamine.txt" && !_config.DataBase.isAmphetamine || file == "gopota.txt" && !_config.DataBase.isGopota || file == "invishack.txt" && !_config.DataBase.isInvishack)
                                    return;
                                Puts(LangEN ? $"This SteamID is in the user {file} database" : $"Данный SteamID находиться в базе пользователей {file}");
                                player.Kick($"найден в базе читеров {file}");
                            }
                        }
                        catch (JsonReaderException ex)
                        {
                            Debug.LogError(LangEN ? $"[AntiCheatScience] [DataBase] Error parsing the response from the database [response: {response}]! impossible to check ID {player.UserIDString}. Exception: {ex.Message}" : $"[AntiCheatScience] [DataBase] Ошибка при разборе ответа от базы [ответ: {response}]! невозможно проверить айди {player.UserIDString}. Исключение: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogError(LangEN ? $"[AntiCheatScience] [DataBase] Database connection lost [code: {code}]! impossible to check ID {player.UserIDString}" : $"[AntiCheatScience] [DataBase] Связь с базой данных потеряна [code: {code}]! невозможно проверить айди {player.UserIDString}");
                    }
                }, this, RequestMethod.GET, null, 0f);
            }
            return null;
        }

        private Dictionary<string, int> playerReportsRA = new Dictionary<string, int>();
        object RustApp_CanIgnoreReport(string target_steam_id, string initiator_steam_id)
        {
            if (_config.RustAPP.idAntiReport.Contains(target_steam_id))
                return false;
            ulong numValue;
            if (UInt64.TryParse(target_steam_id, out numValue))
            {
                BasePlayer victim = BasePlayer.FindByID(numValue);
                SeeSpider(victim);
            }
            if (_config.RustAPP.isRustAPPbanAuto)
            {
                if (playerReportsRA.ContainsKey(target_steam_id))
                {
                    playerReportsRA[target_steam_id]++;
                }
                else
                {
                    playerReportsRA.Add(target_steam_id, 1);
                }
                if (playerReportsRA[target_steam_id] == _config.RustAPP.banAutoInt)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        string command = $"ra.ban {target_steam_id} \"Привысил макс. кол-во репортов [AntiCheatScience]\"";
                        switch (_config.RustAPP.banMeraMR)
                        {
                            case 2:
                                command += " --global";
                                break;
                            case 3:
                                command += " --ban-ip";
                                break;
                            case 4:
                                command += " --ban-ip --global";
                                break;
                        }
                        rust.RunServerCommand(command);
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", target_steam_id).Replace("%reason%", "Привысил макс. кол-во репортов [AntiCheatScience]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
            }
            return null;
        }

        Dictionary<ulong, float> Land = new Dictionary<ulong, float>();

        void OnPlayerLanded(BasePlayer player, float num)
        {
            if (!_config.NoFallDamage.isNFD)
                return;
            NextTick( () =>
            {
                if (!(Math.Abs(player.Health() - Math.Abs(Land[player.userID])) > 5))
                {
                    if (player.InSafeZone() || IsPlayerAtMonument(player))
                        return;
                    if (pressJump.ContainsKey(player.userID))
                        if (pressJump[player.userID])
                            NoFallDamage(player, Math.Abs(player.Health()), Math.Abs(Land[player.userID]));
                }
            });
        }

        object OnPlayerLand(BasePlayer player, float num)
        {
            if (!_config.NoFallDamage.isNFD)
                return null;
            if (!Land.ContainsKey(player.userID))
            {
                Land.Add(player.userID, player.Health());
            }
            else
            {
                Land[player.userID] = player.Health();
            }
            return null;
        }

        private bool IsPlayerAtMonument(BasePlayer player)
        {
            Vector3 playerPosition = player.transform.position;
            
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.Bounds.Contains(playerPosition))
                {
                    return true;
                }
            }
            return false;
        }

        Dictionary<ulong, int> JA = new Dictionary<ulong, int>();
        Dictionary<ulong, bool> pressJump = new Dictionary<ulong, bool>();
        Dictionary<ulong, int> JA_detect = new Dictionary<ulong, int>();
        Dictionary<ulong, bool> cdJ = new Dictionary<ulong, bool>();
        private class JumpInfo
        {
            public float Time;
            public Vector3 Position;

            public JumpInfo(float time, Vector3 position)
            {
                Time = time;
                Position = position;
            }
        }
        Timer checktimerJ;

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.JUMP))
            {
                if (player.Connection.authLevel > 0 || player.IsBuildingAuthed())
                    return;

                if (cdJ.ContainsKey(player.userID))
                {
                    if (cdJ[player.userID])
                        return;
                }
                if (!JA.ContainsKey(player.userID))
                {
                    JA.Add(player.userID, 0);
                }
                JA[player.userID]++;
                timer.Once(0.35f, () =>
                {
                    JA[player.userID] = 0;
                });
                if (!cdJ.ContainsKey(player.userID))
                {
                    cdJ.Add(player.userID, false);
                }
                cdJ[player.userID] = true;
                if (!pressJump.ContainsKey(player.userID))
                    pressJump.Add(player.userID, true);

                pressJump[player.userID] = true;
                timer.Once(2f, () =>
                {
                    pressJump[player.userID] = false;
                });
                timer.Once(1f, () =>
                {
                    checktimerJ = timer.Every(0.1f, () =>
                    {
                        if (player.IsOnGround())
                        {
                            cdJ[player.userID] = false;
                            checktimerJ.Destroy();
                        }
                    });
                });
            }
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player.Connection.authLevel > 0 || !_config.PilotFire.isPF || !_config.FlyFire.isFF)
                return;
            if (player.GetMountedVehicle() != null)
            {
                if (player.GetMountedVehicle().IsDriver(player))
                {
                    string vehicle = player.GetMountedVehicle().ToString();
                    if (vehicle.Contains("testridablehorse") || vehicle.Contains("kayak") || vehicle.Contains("sled"))
                        return;
                    PilotFire(player);
                    return;
                }
            }
            bool ladder = false;
            if (player.GetParentEntity() is BaseLadder)
            {
                ladder = true;
            }
            Item item = projectile?.GetItem();
            if (item != null && weaponbows.Contains(item.info.shortname) || item.info.shortname.Contains("bow.compound"))
                return;
            Collider[] colliders = Physics.OverlapSphere(player.transform.position, 2f);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name.ToLower();
                if (objectName == "lootbarrel")
                    return;
            }
            if (!JA.ContainsKey(player.userID))
            {
                JA.Add(player.userID, 0);
            }
            if (!JA_detect.ContainsKey(player.userID))
            {
                JA_detect.Add(player.userID, 0);
            }
            if (JA[player.userID] > 0)
            {
                JA_detect[player.userID]++;
                if (JA_detect[player.userID] == 1)
                {
                    timer.Once(7f, () =>
                    {
                        JA_detect[player.userID] = 0;
                    });
                }
            }
            if (JA_detect[player.userID] > 1 || ladder == true)
            {
                DetectNo8(player, item.info.shortname);
                if (_config.RustAPP.isRustAPP)
                {
                    string command = $"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\"";
                    switch (_config.FlyFire.banMeraFlyFire)
                    {
                        case 2:
                            command += " --global";
                            break;
                        case 3:
                            command += " --ban-ip";
                            break;
                        case 4:
                            command += " --ban-ip --global";
                            break;
                    }
                    rust.RunServerCommand(command);
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.FlyFire.NameFlyFire);
                    rust.RunServerCommand($"{commanda}");
                }
            }
        }

        [ConsoleCommand("shadowban")]
        private void anticheatShadowReportCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "У вас нет прав для использования этой команды.");
                return;
            }

            SendReply(arg, "Команда временно не работает. Обращайтесь лично к разработчику в дискорд: pressfwd");
        }

        private bool bugReportCooldown = false;
        [ConsoleCommand("anticheatbug")]
        private void anticheatbugReportCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "У вас нет прав для использования этой команды.");
                return;
            }
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Использование: <anticheatbug> \"описание проблемы\"");
                return;
            }

            if (bugReportCooldown)
            {
                SendReply(arg, "Вы уже отправляли БагРепорт или плагин только загрузился. Попробуйте позже. Обычно КД на отправку составляет от 30 до 120 минут.");
                return;
            }

            string identifier = arg.Args[0];
            string sN = ConVar.Server.hostname;

            RequestBugReport(bugreport.Replace("[sN]", $"{sN}")
                .Replace("[dannie]", $"{identifier}")
                .Replace("[id]", $"{_config.Global.DiscordID}"));

            SendReply(arg, "Ваш запрос отправлен! Помните! Злоупотребление не доведет до хорошего!");
            bugReportCooldown = true;
            timer.Once(7200f, () =>
            {
                bugReportCooldown = false;
            });
        }
        [ChatCommand("fdcheck")]
        private void falldamagecheckcommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "AntiCheatScience.can.NFDcheck"))
                return;
            if (args.Length < 1)
            {
                player.ChatMessage("Используйте: </fdcheck \"НикНейм\">");
                return;
            }

            string displayName = args[0];
            BasePlayer targetPlayer = FindPlayerByDisplayName(displayName);
            if (targetPlayer == null)
            {
                SendReply(player, "Игрок не найден.");
                return;
            }

            float originalHealth = targetPlayer.health;
            if (originalHealth >= 30 && targetPlayer.TimeAlive() > 10 && targetPlayer.IsOnGround())
            {
                Vector3 newPosition = player.transform.position + new Vector3(0, 7, 0);
                player.Teleport(newPosition);

                timer.Once(1f, () =>
                {
                    if (Mathf.Approximately(targetPlayer.health, originalHealth))
                    {
                        SendReply(player, $"Проверка не пройдена! ХП после: {Mathf.Abs(targetPlayer.health)}, было: {Mathf.Abs(originalHealth)}");
                        return;
                        /*NoFallDamage(targetPlayer, Mathf.Abs(targetPlayer.health), Mathf.Abs(originalHealth));*/
                    }
                    else
                    {
                        targetPlayer.health = originalHealth;
                        SendReply(targetPlayer, "Вы были подвержены проверке на NoFallDamage и <color=green>успешно</color> её прошли! не волнуйтесь! здоровье восстановлено.");
                    }
                });
            }
            else
            {
                SendReply(player, "Что то пошло не так! сейчас нельзя проверить этого игрока! попробуйте позже");
            }
        }

        private BasePlayer FindPlayerByDisplayName(string displayName)
        {
            BasePlayer targetPlayer = null;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(displayName.ToLower()))
                {
                    targetPlayer = player;
                    break;
                }
            }

            return targetPlayer;
        }

        bool steam = true;
        private void OnServerInitialized()
        {
            if (AutoBattleMetricsBan) AutoBattleMetricsBan.CallHook("CallBan", "76561199484482743", "Reason: TEST", "Note: TEST");
            if (ImageLibrary != null)
            {
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOYpy.png","bg");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOopE.png","ban");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOy0j.png","stop");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qO7PJ.png","freeze");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qO5Ye.png","nfdc");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOlJX.png","unfreeze");
            }
            if (!permission.PermissionExists(_config.Steam.SteamDaysIgnore, this))
                permission.RegisterPermission(_config.Steam.SteamDaysIgnore, this);
            if (!permission.PermissionExists("AntiCheatScience.can.seedetect", this))
                permission.RegisterPermission("AntiCheatScience.can.seedetect", this);
            if (!permission.PermissionExists("AntiCheatScience.can.NFDcheck", this))
                permission.RegisterPermission("AntiCheatScience.can.NFDcheck", this);
            if (!permission.PermissionExists("AntiCheatScience.can.spectate", this))
                permission.RegisterPermission("AntiCheatScience.can.spectate", this);
            /*ConVar.AntiHack.eye_protection = 5;*/
            int checkcfg = 0;
            if (_config.Steam.SteamAPI == "123123")
            {
                Debug.LogError(LangEN ? "[AntiCheatScience] You have not configured a required parameter!! insert the SteamAPI key!!" : "[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте SteamAPI ключ!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning(LangEN ? "[AntiCheatScience] SteamAPI configured." : "[AntiCheatScience] SteamAPI настроен.");
                checkcfg++;
            }

            if (_config.Logs.WebhookD == "1" || _config.Logs.WebhookB == "1")
            {
                Debug.LogError(LangEN ? "[AntiCheatScience] You have not configured a required parameter!! insert discord webhook!!" : "[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте discord webhook!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning(LangEN ? "[AntiCheatScience] discord webhook configured." : "[AntiCheatScience] discord webhook настроен.");
                checkcfg++;
            }

            if (_config.Global.DiscordID == "неизвестный")
            {
                Debug.LogError(LangEN ? "[AntiCheatScience] You have not configured a required parameter!! insert user's discord id!!" : "[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте discord id пользователя!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning(LangEN ? "[AntiCheatScience] discord id is configured." : "[AntiCheatScience] discord id настроен.");
                checkcfg++;
            }

            if (!_config.Global.isSettingsS)
            {
                Debug.LogError(LangEN ? "[AntiCheatScience] You have not configured a required parameter!! Give confirmation in the \"Global\" section!!" : "[AntiCheatScience] Вы не настроили обязательный параметр!! Дайте подтверждение в разделе \"Global\"!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                checkcfg++;
            }

            timer.Once(10, () =>
            {
                ServiceInit();
            });
            timer.Once(900, () =>
            {
                bugReportCooldown = false;
                if (_config.ModelState.isModelStateAuto)
                    ModelStateCheck(true);
            });
            Interface.Oxide.DataFileSystem.GetDatafile("ACS_JOIN");
            /*dummyPlayer.Kill();*/
            url = "http://reportit.tech/";
        }

        [ChatCommand("model")]
        private void modelcommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player, $"{player.modelState.mounted} / {player.modelState.onLadder}");
        }
        void ModelStateCheck(bool isAuto)
        {
            if (!_config.ModelState.isModelState)
                return;
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.modelState.mounted || player.modelState.onLadder)
                {
                    if (player.IsAdmin || player.IsDeveloper)
                        return;
                    string nearbyObjects = GetNearbyObjects(player.transform.position, 1.5f);
                    if (!HasForbiddenObjects(nearbyObjects))
                    {
                        string status = isAuto ? "Auto" : "notAuto";
                        if (_config.RustAPP.isRustAPP)
                        {
                            string command = $"ra.ban {player.userID} \"{_config.ModelState.NameMS} {status} [AntiCheatScience]\"";
                            switch (_config.ModelState.banMeraMS)
                            {
                                case 2:
                                    command += " --global";
                                    break;
                                case 3:
                                    command += " --ban-ip";
                                    break;
                                case 4:
                                    command += " --ban-ip --global";
                                    break;
                            }
                            
                            rust.RunServerCommand(command);
                        }
                        if (!_config.RustAPP.isRustAPPbanOnly)
                        {
                            string command = _config.Global.BanCommand.Replace("%steamid%", player.UserIDString).Replace("%reason%", $"{_config.ModelState.NameMS} {status} [AntiCheatScience]");
                            rust.RunServerCommand(command);
                        }
                        string playerInfo = CheckInfoS(player.userID.ToString());
                        string grid = GetGridString(player.transform.position);

                        RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.ModelState.NameMS} {status}", new
                        {
                            Build = InHome(player),
                            State = player.modelState,
                            Grid = grid,
                            cords = player.transform.position,
                            info = playerInfo
                        }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Измененная модель игрока"});
                    }
                }
            }
        }

        void Unload()
        {
            if (this.Title == "AntiCheatScience")
            {
                ConVar.AntiHack.eye_protection = 0;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.seedetect"))
                        CuiHelper.DestroyUi(player, _Layer1);
                }
            }
            var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
            data.Save();
            Interface.Oxide.DataFileSystem.SaveDatafile("ACS_JOIN");
        }

        void OnServerShutdown()
        {
            ConVar.AntiHack.eye_protection = 0;
        }

        #region AIM_Detect

        float metrpopal = 0f;
        Timer checktimer;

        Dictionary<ulong, int> playerHeadshotCounts = new Dictionary<ulong, int>();
        Dictionary<ulong, int> playerBodyshotCounts = new Dictionary<ulong, int>();
        Dictionary<ulong, int> playerHeadshotCountsFirst = new Dictionary<ulong, int>();
        Dictionary<ulong, int> playerBodyshotCountsFirst = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockToD = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockToB = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockShot = new Dictionary<ulong, int>();
        Dictionary<ulong, bool> AimLockNaProverke = new Dictionary<ulong, bool>();
        List<string> weaponrifle = new List<string> {
                    "rifle.ak", "rifle.lr300", "rifle.semiauto", "lmg.m249", "rifle.ak.diver", "rifle.ak.ice", "rifle.sks"
                };

        List<string> weaponpistol = new List<string> {
                    "pistol.m92", "pistol.semiauto", "pistol.prototype17", "pistol.python", "pistol.eoka", "pistol.revolver"
                };

        List<string> weaponpp = new List<string> {
                    "smg.mp5", "smg.2", "smg.thompson"
                };

        List<string> weaponbows = new List<string> {
                    "bow.hunting", "crossbow", "legacy"
                };

        List<string> weaponsnipers = new List<string> {
                    "rifle.l96", "rifle.bolt"
                };
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer && info.Initiator is BasePlayer)
            {
                var victim = (BasePlayer)entity;
                var attacker = (BasePlayer)info.Initiator;
                if (victim == null | attacker == null)
                    return;
                if (!AimLockNaProverke.ContainsKey(attacker.userID))
                {
                    AimLockNaProverke.Add(attacker.userID, false);
                }
                if (AimLockNaProverke[attacker.userID] == true)
                    info.damageTypes.ScaleAll(0.01f);

                metrpopal = Vector3.Distance(victim.transform.position, attacker.transform.position);

                if (!playerHeadshotCounts.ContainsKey(attacker.userID))
                {
                    playerHeadshotCounts.Add(attacker.userID, 0);
                }
                if (!playerBodyshotCounts.ContainsKey(attacker.userID))
                {
                    playerBodyshotCounts.Add(attacker.userID, 0);
                }
                if (!playerHeadshotCountsFirst.ContainsKey(attacker.userID))
                {
                    playerHeadshotCountsFirst.Add(attacker.userID, 0);
                }
                if (!playerBodyshotCountsFirst.ContainsKey(attacker.userID))
                {
                    playerBodyshotCountsFirst.Add(attacker.userID, 0);
                }
                if (AimTrain != null)
                {
                    if ((bool)AimTrain.CallHook("IsAimTraining", attacker.userID))
                        return;
                }
                /*var data = Interface.Oxide.DataFileSystem.GetFile("ACS_MDD");
                var playerData = data["players", attacker.userID.ToString()] as Dictionary<string, object>;

                if (playerData != null && _config.Manipulator.isManip)
                {
                    string dateString = playerData["date"].ToString();
                    DateTime lastDate = DateTime.Parse(dateString);
                    DateTime currentDate = DateTime.Now;

                    TimeSpan difference = currentDate - lastDate;
                    if (difference.TotalSeconds <= 2)
                    {
                        ulong steamId = attacker.userID;
                        if (eyeHackKicks.TryGetValue(steamId, out int kickCount))
                        {
                            string nearbyObjects3 = GetNearbyObjects(attacker.transform.position, 5f);
                            if (HasForbiddenObjectsManipDoor(nearbyObjects3))
                            {
                                Puts($"Игрок {attacker.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                                return;
                            }

                            string weaponM = info.Weapon?.GetItem()?.info.shortname;
                            float distanseM = Vector3.Distance(victim.transform.position, attacker.transform.position);
                            if (Vector3.Distance(victim.transform.position, attacker.transform.position) > 5f)
                            {
                                string banReason = $"{_config.Manipulator.NameAimM}";

                                DetectNo7(attacker, victim, weaponM, kickCount, distanseM, false);
                                kickCount++;
                                eyeHackKicks[steamId] = kickCount;

                                if (kickCount == 1)
                                {
                                    timer.Once(600f, () =>
                                    {
                                        eyeHackKicks[steamId] = 0;
                                    });
                                }

                                if (kickCount >= 3)
                                {
                                    if (_config.Manipulator.isManipT)
                                    {
                                        if (_config.RustAPP.isRustAPP)
                                        {
                                            if (_config.Manipulator.banMeraAimM == 1)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\"");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 2)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --global");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 3)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 4)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip --global");
                                            }
                                        }
                                        if (!_config.RustAPP.isRustAPPbanOnly)
                                        {
                                            string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", banReason);
                                            rust.RunServerCommand($"{commanda}");
                                        }
                                    }
                                    DetectNo7(attacker, victim, weaponM, kickCount, distanseM, true);
                                    eyeHackKicks[steamId] = 0;
                                }
                            }
                        }
                        else
                        {
                            eyeHackKicks[steamId] = 0;
                        }
                    }
                }*/

                string weaponfire = info.Weapon?.GetItem()?.info.shortname;

                if (!attacker.IsNpc && !victim.IsNpc)
                {
                    if (weaponbows.Contains(weaponfire) && metrpopal > _config.Aim.BowSettings.metrbow && _config.Aim.isAim)
                    {
                        if (info.isHeadshot)
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerHeadshotCountsFirst[attacker.userID]++;
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.BowSettings.isTwoBowCD, () => 
                                {
                                    playerHeadshotCounts[attacker.userID] = 0;
                                    playerHeadshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                playerHeadshotCountsFirst[attacker.userID] = 0;
                                playerHeadshotCounts[attacker.userID]++;
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.BowSettings.isTwoBowH, false);
                            }
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.BowSettings.isTwoBowH)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.BowSettings.isTwoBowH, true);
                            }
                        }
                        else
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerBodyshotCountsFirst[attacker.userID]++;
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.BowSettings.isTwoBowCD, () => 
                                {
                                    playerBodyshotCounts[attacker.userID] = 0;
                                    playerBodyshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                playerBodyshotCountsFirst[attacker.userID] = 0;
                                playerBodyshotCounts[attacker.userID]++;
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.BowSettings.isTwoBowB, false);
                            }
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.BowSettings.isTwoBowB)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.BowSettings.isTwoBowB, true);
                            }
                        }
                    }

                    if (weaponpistol.Contains(weaponfire) && metrpopal > _config.Aim.PistolSettings.metrpistol && _config.Aim.isAim)
                    {
                        if (info.isHeadshot)
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerHeadshotCountsFirst[attacker.userID]++;
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.PistolSettings.isTwoPistolCD, () => 
                                {
                                    playerHeadshotCounts[attacker.userID] = 0;
                                    playerHeadshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                playerHeadshotCountsFirst[attacker.userID] = 0;
                                playerHeadshotCounts[attacker.userID]++;
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.PistolSettings.isTwoPistolH, false);
                            }
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.PistolSettings.isTwoPistolH)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.PistolSettings.isTwoPistolH, true);
                            }
                        }
                        else
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerBodyshotCountsFirst[attacker.userID]++;
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.PistolSettings.isTwoPistolCD, () => 
                                {
                                    playerBodyshotCounts[attacker.userID] = 0;
                                    playerBodyshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                playerBodyshotCountsFirst[attacker.userID] = 0;
                                playerBodyshotCounts[attacker.userID]++;
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.PistolSettings.isTwoPistolB, false);
                            }
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.PistolSettings.isTwoPistolB)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.PistolSettings.isTwoPistolB, true);
                            }
                        }
                    }

                    if (weaponpp.Contains(weaponfire) && metrpopal > _config.Aim.PPSetings.metrpp && _config.Aim.isAim)
                    {
                        if (info.isHeadshot)
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerHeadshotCountsFirst[attacker.userID]++;
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.PPSetings.isTwoPPCD, () => 
                                {
                                    playerHeadshotCounts[attacker.userID] = 0;
                                    playerHeadshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                playerHeadshotCountsFirst[attacker.userID] = 0;
                                playerHeadshotCounts[attacker.userID]++;
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.PPSetings.isTwoPPH, false);
                            }
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.PPSetings.isTwoPPH)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.PPSetings.isTwoPPH, true);
                            }
                        }
                        else
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerBodyshotCountsFirst[attacker.userID]++;
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.PPSetings.isTwoPPCD, () => 
                                {
                                    playerBodyshotCounts[attacker.userID] = 0;
                                    playerBodyshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                playerBodyshotCountsFirst[attacker.userID] = 0;
                                playerBodyshotCounts[attacker.userID]++;
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.PPSetings.isTwoPPB, false);
                            }
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.PPSetings.isTwoPPB)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.PPSetings.isTwoPPB, true);
                            }
                        }
                    }

                    if (weaponrifle.Contains(weaponfire) && metrpopal > _config.Aim.RifleSettings.metrrifle && _config.Aim.isAim)
                    {
                        if (info.isHeadshot)
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerHeadshotCountsFirst[attacker.userID]++;
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.RifleSettings.isTwoRifCD, () => 
                                {
                                    playerHeadshotCounts[attacker.userID] = 0;
                                    playerHeadshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerHeadshotCountsFirst[attacker.userID] == 1)
                            {
                                playerHeadshotCountsFirst[attacker.userID] = 0;
                                playerHeadshotCounts[attacker.userID]++;
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.RifleSettings.isTwoRifH, false);
                            }
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.RifleSettings.isTwoRifH)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.RifleSettings.isTwoRifH, true);
                            }
                        }
                        else
                        {
                            ChatDetect(attacker, _config.Aim.NameAim);
                            playerBodyshotCountsFirst[attacker.userID]++;
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                timer.Once(_config.Aim.RifleSettings.isTwoRifCD, () => 
                                {
                                    playerBodyshotCounts[attacker.userID] = 0;
                                    playerBodyshotCountsFirst[attacker.userID] = 0;
                                });
                            }
                            if (playerBodyshotCountsFirst[attacker.userID] == 1)
                            {
                                playerBodyshotCountsFirst[attacker.userID] = 0;
                                playerBodyshotCounts[attacker.userID]++;
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.RifleSettings.isTwoRifB, false);
                            }
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.RifleSettings.isTwoRifB)
                            {
                                if (_config.Global.isAimBlocDamage)
                                    info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.RifleSettings.isTwoRifB, true);
                            }
                        }
                    }

                    if (metrpopal > 100f && metrpopal < 200f && /*weaponrifle.Contains(weaponfire)*/ weaponfire == "rifle.ak" && _config.AimLock.isAimLock)
                    {
                        if (!AimLockToD.ContainsKey(attacker.userID))
                        {
                            AimLockToD.Add(attacker.userID, 0);
                        }
                        if (!AimLockToB.ContainsKey(attacker.userID))
                        {
                            AimLockToB.Add(attacker.userID, 0);
                        }
                        if (!AimLockToD.ContainsKey(attacker.userID))
                        {
                            AimLockNaProverke.Add(attacker.userID, false);
                        }

                        AimLockToD[attacker.userID]++;

                        timer.Once(60f, () =>
                        {
                            AimLockToD[attacker.userID] = 0;
                        });

                        if (AimLockToD[attacker.userID] == 8 && attacker.TimeAlive() > 1800)
                        {
                            if (checktimer == null)
                            {
                                AimLockNaProverke[attacker.userID] = true;
                                
                                Vector3 initialAttackerPosition = attacker.transform.position;
                                Vector3 initialVictimPosition = victim.transform.position;

                                checktimer = timer.Every(0.1f, () =>
                                {
                                    Vector3 victimPosition = victim.transform.position;
                                    var MaxRadius = 0.395f;
                                    var MaxRadiusNear = 0.080f;

                                    float incrementPerUnit = 0.07550f;
                                    float incrementPerUnitNear = 0.03400f;

                                    float unitsDifference = (metrpopal - 100) / 5;

                                    float incrementToAdd = unitsDifference * incrementPerUnit;
                                    float incrementToAddNear = unitsDifference * incrementPerUnitNear;

                                    float newMaxRadius = MaxRadius + incrementToAdd;
                                    float newMaxRadiusNear = MaxRadiusNear + incrementToAddNear;

                                    RaycastHit hit;
                                    bool hitDetected = Physics.SphereCast(attacker.eyes.position, newMaxRadius, attacker.eyes.BodyForward(), out hit, Vector3.Distance(attacker.eyes.position, victimPosition), 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore);
                                    bool NearDetected = Physics.SphereCast(attacker.eyes.position, newMaxRadiusNear, attacker.eyes.BodyForward(), out hit, Vector3.Distance(attacker.eyes.position, victimPosition), 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore);

                                    if (hitDetected && !NearDetected)
                                    {
                                        AimLockToB[attacker.userID]++;
                                    }
                                });

                                timer.Once(7f, () =>
                                {
                                    Vector3 finalAttackerPosition = attacker.transform.position;
                                    Vector3 finalVictimPosition = victim.transform.position;

                                    float initialDistance = Vector3.Distance(initialAttackerPosition, initialVictimPosition);
                                    float finalDistance = Vector3.Distance(finalAttackerPosition, finalVictimPosition);

                                    if (Mathf.Abs(finalDistance - initialDistance) < 15f)
                                    {
                                        return;
                                    }

                                    checktimer.Destroy();
                                    checktimer = null;
                                    Puts($"[{attacker.displayName} | {attacker.userID}] - получил [{AimLockToB[attacker.userID]}/70] детектов AimLock! Выпустив {AimLockShot[attacker.userID]} пуль во время проверки");
                                    AimLockNaProverke[attacker.userID] = false;
                                    if (AimLockShot[attacker.userID] >= 1 && AimLockToB[attacker.userID] >= 30)
                                    {
                                        DetectNo5(attacker, victim, weaponfire, metrpopal, AimLockToB[attacker.userID]);
                                    }
                                    AimLockShot[attacker.userID] = 0;
                                    AimLockToB[attacker.userID] = 0;
                                });
                            }
                            else
                            {
                                info.damageTypes.ScaleAll(0.01f);
                            }

                        }
                    }
                }

                if (_config.NightFire.isNH)
                {
                    if (metrpopal > _config.NightFire.metrnh)
                    {
                        if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                        {
                            if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
                            {
                                var currentTime = ConVar.Env.time;
                                var roundedMetrpopal = Math.Round(metrpopal, 2);
                                var roundedCurrentTime = Math.Round(currentTime % 24, 0);
                                bool hasFlashlight = false;
                                bool hasNightVision = false;
                                string weaponnight = info.Weapon?.GetItem()?.info.shortname;
                                var activeItem = attacker.GetActiveItem();
                                if (activeItem != null)
                                {
                                    var heldEntity = activeItem.GetHeldEntity() as BaseProjectile;
                                    if (heldEntity != null)
                                    {
                                        hasFlashlight = heldEntity.HasFlag(BaseEntity.Flags.On);
                                    }
                                }
                                foreach (var item in attacker.inventory.containerWear.itemList)
                                {
                                    if (item.info.shortname == "nightvisiongoggles")
                                    {
                                        hasNightVision = true;
                                        break;
                                    }
                                }
                                bool isNightVisionINT = false;
                                if (NightVision != null)
                                {
                                    if ((bool)NightVision.CallHook("IsPlayerTimeLocked", attacker))
                                        isNightVisionINT = true;
                                }
                                if (IsNighttime() && (!hasFlashlight || !hasNightVision || !isNightVisionINT))
                                {
                                    string jsonwebhooknightshot = jsonwebhooknightshotRU;
                                    DetectNo9(attacker, victim, metrpopal, roundedCurrentTime, weaponnight, hasFlashlight, hasNightVision);
                                    Puts($"Игрок {attacker.displayName} {_config.NightFire.NameNightShot}");
                                    string banReason = $"{_config.NightFire.NameNightShot}";
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsNighttime()
        {
            float time = TOD_Sky.Instance.Cycle.Hour;
            return time >= 23f || time < 5f;
        }

        #endregion

        void ServiceInit()
        {
            Debug.LogWarning(LangEN ? "[AntiCheatScience] Automated version, to report a bug, enter: <anticheatbug \"description of the problem\">" : "[AntiCheatScience] Автоматизированая версия, для баг репорта введите: <anticheatbug \"описание проблемы\">");
            Debug.LogWarning(LangEN ? "[AntiCheatScience] To fully check the player and analyze the problem, enter <shadowban \"steamid\">, all player statistics will go to the plugin developer, then the player will be called for verification through the database" : "[AntiCheatScience] Для полной проверки игрока и анализации проблемы введите <shadowban \"steamid\">, вся статистика игрока уйдет разработчику плагина, далее игрок будет вызван на проверку через базу данных");
            Debug.LogWarning(LangEN ? "[AntiCheatScience] Do not abuse!!! for flooding, your server will be listed as an emergency, your requests will be ignored, or the plugin will stop working on your server" : "[AntiCheatScience] Не злоупотребляйте!!! за флуд Ваш сервер будет занесен в ЧС, Ваши заявки будут игнорироваться, либо плагин перестанет работать на Вашем сервере");
            Debug.LogWarning(LangEN ? "[AntiCheatScience] Resource from PRESSF, owner of HazardProject - https://hazard-plugins.space | discord: pressfwd | tg: https://t.me/pressfhazardrust" : "[AntiCheatScience] Ресурс от PRESSF, владелец HazardProject - https://hazard-plugins.space | discord: pressfwd | tg: https://t.me/pressfhazardrust");
            bugReportCooldown = true;
            note = ConVar.Server.port.ToString();
        }

        #region stash

        private Dictionary<ulong, int> detectCounts = new Dictionary<ulong, int>();
        
        private void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (!detectCounts.ContainsKey(player.userID))
            {
                detectCounts[player.userID] = 0;
            }
            OnStashTriggered(stash, player, false);
        }

        private void OnStashTriggered(StashContainer stash, BasePlayer player, bool stashWasDestroyed)
        {
            if (!_config.Traps.isStash)
                return;

            if (stash.OwnerID != player.userID)
            {
                if (player.Team != null && player.Team.members != null && player.Team.members.Contains(stash.OwnerID))
                {
                    return;
                }

                if (!detectCounts.ContainsKey(player.userID))
                {
                    detectCounts[player.userID] = 0;
                }

                if (detectCounts[player.userID] == 1)
                {
                    timer.Once(900f, () =>
                    {
                        detectCounts[player.userID] = 0;
                    });
                }
                bool aresleepbag = false;
                string nearbyObjectsSTASH = GetNearbyObjectsSTASH(player.transform.position, 10f);
                if (HasForbiddenObjectsSTASH(nearbyObjectsSTASH))
                {
                    aresleepbag = true;
                }

                detectCounts[player.userID]++;
                int DCount = detectCounts[player.userID];
                string grid = GetGridString(player.transform.position);
                if (MultiFighting != null)
                    steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                if (FreePlayerFriendly != null)
                    steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
                if (FLuma != null)
                    steam = (bool)FLuma.CallHook("IsSteam", player.Connection);

                
                string playerInfo = CheckInfoS(player.userID.ToString());
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.Traps.NameStash} [{DCount}/{_config.Traps.stashBCount}]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        cords = player.transform.position,
                        sleepbag = aresleepbag ? "Да" : "Нет",
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Откапывание стешей"});
                }
                if (detectCounts[player.userID] != _config.Traps.stashBCount)
                {
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Блокировка")
                        .Replace("[reason]", _config.Traps.NameStash)
                        .Replace("[grid]", grid)
                        .Replace("[inhome]", InHome(player))
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Traps.stashBCount.ToString())
                        .Replace("[image]", _config.Logs.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));
                }

                if (detectCounts[player.userID] >= _config.Traps.stashBCount)
                {
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Детект")
                        .Replace("[reason]", _config.Traps.NameStash)
                        .Replace("[grid]", grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Traps.stashBCount.ToString())
                        .Replace("[image]", _config.Logs.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));

                    string banReason = $"{_config.Traps.NameStash}";

                    if (_config.RustAPP.isRustAPP)
                    {
                        string command = $"ra.ban {player.userID.ToString()} \"{banReason} [AntiCheatScience]\"";
                        switch (_config.Traps.banMeraStash)
                        {
                            case 2:
                                command += " --global";
                                break;
                            case 3:
                                command += " --ban-ip";
                                break;
                            case 4:
                                command += " --ban-ip --global";
                                break;
                        }
                        rust.RunServerCommand(command);
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", banReason);
                        rust.RunServerCommand($"{commanda}");
                    }
                }
            }
        }

        string GetNearbyObjectsSTASH(Vector3 position, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name;

                if (objectCounts.ContainsKey(objectName))
                {
                    objectCounts[objectName]++;
                }
                else
                {
                    objectCounts[objectName] = 1;
                }
            }

            string nearbyObjectsSTASH = "";
            foreach (KeyValuePair<string, int> entry in objectCounts)
            {
                nearbyObjectsSTASH += entry.Key;
                if (entry.Value > 1)
                {
                    nearbyObjectsSTASH += $" ({entry.Value} шт.)";
                }
                nearbyObjectsSTASH += ", ";
            }

            if (nearbyObjectsSTASH.Length > 0)
            {
                nearbyObjectsSTASH = nearbyObjectsSTASH.TrimEnd(',', ' ');
            }

            return nearbyObjectsSTASH;
        }

        bool HasForbiddenObjectsSTASH(string nearbyObjectsSTASH)
        {
            string[] forbiddenObjectsSTASH = new string[] { "assets/bundled/prefabs/static/sleepingbag_static.prefab" };

            foreach (string forbiddenObjectSTASH in forbiddenObjectsSTASH)
            {
                if (nearbyObjectsSTASH.Contains(forbiddenObjectSTASH))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Fly

        private Dictionary<ulong, int> eyeHackKicks = new Dictionary<ulong, int>();

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player.net.connection.authLevel >= 1)
                return false;
            if (player.isMounted || player.HasParent() || player.IsOnGround())
                return false;
            if (type == AntiHackType.FlyHack)
            {
                string banReason = $"{_config.FlyHack.NameFly}";
                /*if (player.GetParentEntity() != null && player.GetParentEntity().ShortPrefabName == "ladder.wooden.wall")
                {
                    return false;
                }*/

                if (!_config.FlyHack.isFly)
                    return null;
                string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
                string nearbyObjects2 = GetNearbyObjects(player.transform.position, 30f);
                if (HasForbiddenObjectsHeli(nearbyObjects2))
                {
                    Puts($"Игрок {player.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                    return false;
                }
                if (HasForbiddenObjects(nearbyObjects))
                {
                    Puts($"Игрок {player.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                    return false;
                }
                string playerName = player.displayName;
                if (_config.FlyHack.isFlyBan)
                {
                    DetectNo3(player, true);
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", banReason);
                    rust.RunServerCommand($"{commanda}");
                }
                if (!_config.FlyHack.isFlyBan)
                {
                    DetectNo3(player, false);
                    rust.RunServerCommand($"kick {player.userID} \"{banReason}\"");
                }
                Puts($"Игрок {player.userID} {_config.FlyHack.NameFly}");
                return false;
            }
            /*else if (type == AntiHackType.EyeHack)
            {
                var data = Interface.Oxide.DataFileSystem.GetFile("ACS_MDD");
                var playerData = new Dictionary<string, object>
                {
                    ["date"] = DateTime.Now.ToString(),
                };
                data["players", player.userID.ToString()] = playerData;
                data.Save();

                return false;
            }*/
            return false;
        }
        string GetNearbyObjects(Vector3 position, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name;
                if (objectName == "assets/prefabs/player/player.prefab" ||
                    objectName == "Prevent_Movement" ||
                    objectName == "New Game Object" ||
                    objectName == "prevent_building" ||
                    objectName == "RadiationSphere" ||
                    objectName == "Fog Volume" ||
                    objectName == "TargetDetection")
                {
                    continue;
                }
                if (objectCounts.ContainsKey(objectName))
                {
                    objectCounts[objectName]++;
                }
                else
                {
                    objectCounts[objectName] = 1;
                }
            }

            string nearbyObjects = "";
            foreach (KeyValuePair<string, int> entry in objectCounts)
            {
                nearbyObjects += entry.Key;
                if (entry.Value > 1)
                {
                    nearbyObjects += $" ({entry.Value} шт.)";
                }
                nearbyObjects += ", ";
            }

            if (nearbyObjects.Length > 0)
            {
                nearbyObjects = nearbyObjects.TrimEnd(',', ' ');
            }

            return nearbyObjects;
        }

        bool HasForbiddenObjects(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] { "quarry_main", "quarry_track", "Server", "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab",
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
                "assets/prefabs/misc/supply drop/supply_drop.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "assets/prefabs/deployable/quarry/engineswitch.prefab", "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab", "Ladder_4", "Ladder Trigger" };


            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }
        bool HasForbiddenObjectsHeli(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] {
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }
        bool HasForbiddenObjectsManipDoor(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] { "quarry_main", "quarry_track", "Server", "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab", "assets/prefabs/building/door.hinged/door.hinged.wood.prefab", "assets/prefabs/building/door.hinged/door.hinged.metal.prefab", "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab", "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab", "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab", "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab", "assets/bundled/prefabs/static/door.hinged.garage_a.prefab",
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", "assets/content/structures/train_wagons/train_wagon_a.prefab", "assets/content/structures/train_wagons/train_wagon_b.prefab",
                "assets/content/structures/train_wagons/train_wagon_c.prefab", "assets/content/structures/train_wagons/train_wagon_d.prefab", "assets/content/structures/train_wagons/train_wagon_e.prefab",
                "assets/content/structures/train_crane/train_crane_a.prefab", "assets/content/structures/train_tracks/crane_track_150x900.prefab", "assets/content/structures/train_tracks/crane_track_150x900_end.prefab",
                "assets/content/structures/train_tracks/train_track_3x18.prefab", "assets/content/structures/train_tracks/train_track_3x36.prefab", "assets/content/structures/train_tracks/train_track_3x3_end.prefab",
                "assets/content/structures/train_tracks/train_track_3x9.prefab", "assets/content/structures/train_tracks/train_track_bend_45.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x18.prefab",
                "assets/content/structures/train_tracks/train_track_nogravel_3x36.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x3_end.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x9.prefab",
                "assets/content/structures/train_tracks/train_track_nogravel_bend_45.prefab", "assets/content/structures/train_tracks/train_track_nogravel_sleft_3x027.prefab", "assets/content/structures/train_tracks/train_track_nogravel_sright_3x27.prefab", "assets/content/structures/train_tracks/train_track_sleft_3x027.prefab",
                "assets/content/structures/train_tracks/train_track_sright_3x27.prefab", "assets/content/structures/harbor/tugboat/tugboat_a.prefab", "assets/content/structures/harbor/tugboat/tugboat_a_interior.prefab",
                "assets/content/structures/harbor/tugboat/tugboat_a_snow.prefab", "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", "assets/content/vehicles/boats/rhib/rhib.prefab", "assets/content/vehicles/boats/rhib/subents/fuel_storage.prefab",
                "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab", "assets/content/vehicles/boats/rowboat/metalrowboat.prefab", "assets/content/vehicles/boats/rowboat/oldwoodenrowboat.prefab", "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                "assets/content/vehicles/boats/rowboat/subents/fuel_storage.prefab", "assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab",
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
                "assets/prefabs/misc/supply drop/supply_drop.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "assets/prefabs/deployable/quarry/engineswitch.prefab", "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region connect

        void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
                NextTick( () => {
                    player.SendConsoleCommand("camspeed 0.0");
                    NextTick( () => {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                    });
                });
            }
            string playerName = player.displayName;
            /*if (playerName.Contains("1488") || playerName.Contains("卐"))
                player.Kick("Помни о наших ветеранах, маленький ублюдок");*/
            string playerIP = player.net.connection.ipaddress.Split(':')[0];
            if (_config.Steam.PROXY_CHECT)
            {
                CheckPlayerIP(player.userID.ToString(), playerIP, playerName);
            }
            CheckInfoS(player.userID.ToString());
            var steamID = player.userID.ToString();
            if (!_PlayerEyes.ContainsKey(steamID))
            {
                _PlayerEyes.Add(steamID, Vector3.zero);
            }
            if (_config.Steam.STEAM_CHECT)
            {
                if (permission.UserHasPermission(player.userID.ToString(), _config.Steam.SteamDaysIgnore))
                    return;

                CheckAccountAge(player.userID.ToString());
            }
        }

        private bool isSteam(BasePlayer player)
        {
            if (MultiFighting == null)
            {
                if (FreePlayerFriendly == null)
                return true;
                else
                {
                    if (FLuma != null)
                        return FLuma.Call<bool>("IsSteam", player.Connection);                    
                    else
                    return FreePlayerFriendly.Call<bool>("IsPlayerNoSteam", player.UserIDString);
                }
            }
            else
            {
                return MultiFighting.Call<bool>("IsSteam", player);
            }
        }

        private void CheckPlayerIP(string userID, string playerIP, string playerName)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("PlayerIPs");
            if (data.Exists() && data["players", userID] != null)
            {
                var playerData = data["players", userID] as Dictionary<string, object>;
                bool isProxy = (bool)playerData["is_proxy"];
                if (isProxy)
                {
                    Puts($"Игрок {playerName} ({playerIP}) использует PROXY, бан :D");
                }
                else
                {
                    Puts($"Игрок {playerName} ({playerIP}) не использует PROXY, хороший мальчик :D");
                }
            }
            else
            {
                string url = $"https://api.ip2location.io/?key={_config.Steam.YOUR_API_KEY}&ip={playerIP}";
                webrequest.Enqueue(url, "", (code, response) =>
                {
                    if (code == 200 && !string.IsNullOrEmpty(response))
                    {
                        IPInfo ipInfo = JsonConvert.DeserializeObject<IPInfo>(response);
                        bool isProxy = ipInfo.is_proxy;
                        if (isProxy)
                        {
                            Puts($"Игрок {playerName} ({playerIP}) использует PROXY, бан :D");
                            if (_config.RustAPP.isRustAPP)
                            {
                                rust.RunServerCommand($"ra.ban {userID.ToString()} \"Использование PROXY [AntiCheatScience]\" --ban-ip --global");
                            }
                            if (!_config.RustAPP.isRustAPPbanOnly)
                            {
                                string commanda = _config.Global.BanCommand.Replace("%steamid%", userID.ToString()).Replace("%reason%", "Плохой мальчик, выключай PROXY :D");
                                rust.RunServerCommand($"{commanda}");
                            }
                        }
                        else
                        {
                            Puts($"Игрок {playerName} ({playerIP}) не использует PROXY, хороший мальчик :D");
                        }
                        SavePlayerIP(userID, playerIP, isProxy);
                    }
                }, this, RequestMethod.GET);
            }
        }

        private void SavePlayerIP(string userID, string playerIP, bool isProxy)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("PlayerIPs");
            var playerData = new Dictionary<string, object>
            {
                ["ip"] = playerIP,
                ["is_proxy"] = isProxy
            };
            data["players", userID] = playerData;
            data.Save();
        }

        [System.Serializable]
        public class IPInfo
        {
            public string ip;
            public string country_code;
            public string country_name;
            public string region_name;
            public string city_name;
            public float latitude;
            public float longitude;
            public string zip_code;
            public string time_zone;
            public string asn;
            public string @as;
            public bool is_proxy;
        }

        #endregion

        #region CodeBan
        
        private static bool IsBanned(ulong userid)
        {
            return ServerUsers.Is(userid, ServerUsers.UserGroup.Banned);
        }

        private void OnCodeEntered(BaseLock codeLock, BasePlayer player, string code)
        {
            if (_config.CodeLock.isCodeLock)
                return;
            ulong owner = codeLock.OwnerID;
            if (player == null || code.ToInt() != codeLock.GetHashCode()) 
                return;

            if (IsBanned(owner))
            {
                string playerName = player.displayName;
                string grid = GetGridString(player.transform.position);
                string playerInfo = CheckInfoS(player.userID.ToString());
                if (MultiFighting != null)
                    steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                if (FLuma != null)
                    steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
                if (FreePlayerFriendly != null)
                    steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
                
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.CodeLock.NameCodeLock} (владелец {owner})", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Кодовый замок"});
                }

                RequestDC(jsonwebhookcodeRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[reason]", _config.CodeLock.NameCodeLock)
                    .Replace("[grid]", grid)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[owner]", $"{owner}"));
            }
        }   
        
        #endregion

        #region Logs
        private void DetectNo3(BasePlayer player, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.FlyHack.NameFly);
            
            string playerInfo = CheckInfoS(player.userID.ToString());
            string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
            if (banlogorlog)
            {
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.FlyHack.NameFly)
                    .Replace("[grid]", grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.FlyHack.NameFly}", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Флайхак"});
                }
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Кик")
                    .Replace("[reason]", _config.FlyHack.NameFly)
                    .Replace("[grid]", grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        /*private void DetectNo4(BasePlayer player, int DCount,/* bool aresleepbag,*/ /*bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            bool steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Ловушки.NameStash)
                    .Replace("[grid]", grid)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[dCount]", DCount.ToString())
                    .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    /*.Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")*/
                    /*.Replace("[playerInfo]", playerInfo));
            }
            else 
            {
                RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Ловушки.NameStash)
                    .Replace("[grid]", grid)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[dCount]", DCount.ToString())
                    .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    /*.Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")*/
                    /*.Replace("[playerInfo]", playerInfo));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }*/
        private void DetectNo10(BasePlayer player, int detect, string weapon, Vector3 startPos, float maxRadius, RaycastHit hit, float maxHitDistance, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.MeleeAttack.NameMeleeAttack);
            
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookmeleeRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.MeleeAttack.NameMeleeAttack)
                    .Replace("[grid]", grid)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.MeleeAttack.NameMeleeAttack} [{detect}/3]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        info = playerInfo,
                        Weapon = weapon,
                        StartPos = startPos,
                        MaxRadius = maxRadius,
                        MaxHitDistance = maxHitDistance
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "MeleeAttack"});
                }
                RequestDC(jsonwebhookmeleeRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.MeleeAttack.NameMeleeAttack)
                    .Replace("[grid]", grid)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        private void DetectNo11(BasePlayer player, int detect, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);

            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhooksilentRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.SilentAim.NameSAim)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[grid]", grid)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", _config.SilentAim.maxDetect.ToString())
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.SilentAim.NameSAim} [{detect}/{_config.SilentAim.maxDetect}]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Сайлент аим"});
                }
                RequestDC(jsonwebhooksilentRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.SilentAim.NameSAim)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", _config.SilentAim.maxDetect.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[grid]", grid)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        private void DetectNo12(BasePlayer player, float health, float healthBefore)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.NoFallDamage.NameNFD);

            string playerInfo = CheckInfoS(player.userID.ToString());

            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.NoFallDamage.NameNFD}", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    Health = health,
                    HealthBefore = healthBefore,
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Нет урона от падения"});
            }
            RequestDC(jsonwebhookNFDRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[reason]", _config.NoFallDamage.NameNFD)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[grid]", grid)
                .Replace("[h]", health.ToString())
                .Replace("[hb]", healthBefore.ToString())
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo13(BasePlayer player)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.SpiderHack.NameSpider);

            string playerInfo = CheckInfoS(player.userID.ToString());

            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.SpiderHack.NameSpider}", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Спайдерхак"});
            }
            RequestDC(jsonwebhookspiderRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[reason]", _config.SpiderHack.NameSpider)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[grid]", grid)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo9(BasePlayer player, BasePlayer victim, float metrpopal, double time, string weaponnight, bool hasFlashlight, bool hasNightVision)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.NightFire.NameNightShot);

            string playerInfo = CheckInfoS(player.userID.ToString());

            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.NightFire.NameNightShot}", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    Time = time,
                    flash = hasFlashlight || hasNightVision ? "Есть" : "Нет",
                    metr = Math.Round(metrpopal),
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Ночное зрение"});
            }
            RequestDC(jsonwebhooknightshotRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[reason]", _config.NightFire.NameNightShot)
                .Replace("[metr]", Math.Round(metrpopal).ToString())
                .Replace("[grid]", grid)
                .Replace("[flash]", hasFlashlight || hasNightVision ? "Есть" : "Нет")
                .Replace("[time]", time.ToString())
                .Replace("[weapon]", weaponnight)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo)
                .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
        }
        private void DetectNo7(BasePlayer player, BasePlayer victim, string weaponname, int dCount, float metrp, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.Manipulator.NameAimM);
                
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Manipulator.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else
            {
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.Manipulator.NameAimM} [{dCount}/3]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        metr = Math.Round(metrp),
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Манипулятор"});
                }
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Manipulator.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
        }
        private void DetectNo8(BasePlayer player, string weaponname)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.FlyFire.NameFlyFire);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.FlyFire.NameFlyFire}", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    weapon = weaponname,
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Стрельба в прыжке"});
            }
            RequestDC(jsonwebhookflyfireRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[reason]", _config.FlyFire.NameFlyFire)
                .Replace("[grid]", grid)
                .Replace("[weapon]", weaponname)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo1(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int dCount, int mCount, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);

            string playerInfo = CheckInfoS(player.userID.ToString());

            if (banlogorlog)
            {
                if (_config.Aim.isAimBan)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        string command = $"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\"";
                        switch (_config.Aim.banMeraAim)
                        {
                            case 2:
                                command += " --global";
                                break;
                            case 3:
                                command += " --ban-ip";
                                break;
                            case 4:
                                command += " --ban-ip --global";
                                break;
                        }
                        rust.RunServerCommand(command);
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.Aim.NameAim);
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.Aim.NameAim}\"");
                }
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.Aim.NameAim} [{dCount}/{mCount}]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        weapon = weaponname,
                        metr = Math.Round(metrpopal),
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Аим"});
                }
                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Голова")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", mCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else
            {
                /*RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[headorbody]", "Голова")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[reason]", _config.НастройкаАима.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", mCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", $"{victim.displayName} | {victim.userID}" $"{victim.displayName}"));*/
            }
        }
        private void DetectNo2(BasePlayer player)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.PilotFire.NameAimDrive);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.PilotFire.NameAimDrive}", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Стрельба за рулем"});
            }
            RequestDC(jsonwebhookaimdriveRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[grid]", grid)
                .Replace("[reason]", _config.PilotFire.NameAimDrive)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo5(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int dCount)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);

            string playerInfo = CheckInfoS(player.userID.ToString());

            ChatDetect(player, _config.AimLock.NameAimLock);

            if (_config.AimLock.isAimLockBan)
            {
                if (_config.RustAPP.isRustAPP)
                {
                    string command = $"ra.ban {player.userID.ToString()} \"{_config.AimLock.NameAimLock} [AntiCheatScience]\"";
                    switch (_config.AimLock.banMeraAimLock)
                    {
                        case 2:
                            command += " --global";
                            break;
                        case 3:
                            command += " --ban-ip";
                            break;
                        case 4:
                            command += " --ban-ip --global";
                            break;
                    }
                    rust.RunServerCommand(command);
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.AimLock.NameAimLock);
                    rust.RunServerCommand($"{commanda}");
                }
            }
            else
            {
                string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.AimLock.NameAimLock);
                rust.RunServerCommand($"{commanda}");
            }
            if (_config.RustAPP.isRustAPPlog)
            {
                RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.AimLock.NameAimLock} (вероятность {dCount})", new
                {
                    Build = InHome(player),
                    Grid = grid,
                    weapon = weaponname,
                    metr = Math.Round(metrpopal),
                    info = playerInfo
                }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Закрепленный аим"});
            }
            RequestDC(jsonwebhookaimlockRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", _config.AimLock.isAimLockBan ? "Блокировка" : "Кик")
                .Replace("[metr]", Math.Round(metrpopal).ToString())
                .Replace("[grid]", grid)
                .Replace("[reason]", _config.AimLock.NameAimLock)
                .Replace("[weapon]", weaponname)
                .Replace("[dCount]", dCount.ToString())
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo)
                .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
        }
        private void DetectNo1B(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int bCount, int mbCount, bool banlogorlog)
        {
            string grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            if (FreePlayerFriendly != null)
                steam = (bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", player.UserIDString);
            if (FLuma != null)
                steam = (bool)FLuma.CallHook("IsSteam", player.Connection);

            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                if (_config.Aim.isAimBan)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        string command = $"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\"";
                        switch (_config.Aim.banMeraAim)
                        {
                            case 2:
                                command += " --global";
                                break;
                            case 3:
                                command += " --ban-ip";
                                break;
                            case 4:
                                command += " --ban-ip --global";
                                break;
                        }
                        rust.RunServerCommand(command);
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.Aim.NameAim);
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.Aim.NameAim}\"");
                }

                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Тело")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else
            {
                if (_config.RustAPP.isRustAPPlog)
                {
                    RustApp.Call("RA_CreateAlert", this, $"Детект игрока {player.UserIDString} {_config.Aim.NameAim} [{bCount}/{mbCount}]", new
                    {
                        Build = InHome(player),
                        Grid = grid,
                        weapon = weaponname,
                        metr = Math.Round(metrpopal),
                        info = playerInfo
                    }, new { custom_icon = "https://gspics.org/images/2024/07/04/0zr4be.png", name = "Аим"});
                }
                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[headorbody]", "Тело")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", grid)
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
        }

        private string InHome(BasePlayer player)
        {
            if (player != null)
            {
                if (player.IsBuildingAuthed())
                    return "В своем шкафу";
                if (!player.IsBuildingAuthed())
                    return "В чужом шкафу";
            }
            return "Нет информации";
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        private string CheckInfoS(string userID)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
            var playerData = data["players", userID] as Dictionary<string, object>;
            string infoString = null;
            if (playerData != null)
            {
                DateTime firstJoin = Convert.ToDateTime(playerData["FirstJoin"]);
                double accountAge = Convert.ToDouble(playerData["accountAge"]);
                int roundedAccountAge = (int)Math.Round(accountAge);
                infoString = $"Первый раз зашел на сервер: {firstJoin} (по данным анти-чита)\\nАккаунт создан: {roundedAccountAge}д. назад";
                return infoString;
            }
            //if (url != null && note != null)  ServerMgr.Instance.StartCoroutine(SendErrorLog(url, note, infoString != null? infoString : "2"));
            return "Информация о регистрации и входе не найдена.";
        }

        #endregion

        #region SteamDay

        private void CheckAccountAge(string userID)
        {
            string urlsc = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_config.Steam.SteamAPI}&steamids={userID}";
            webrequest.Enqueue(urlsc, "", (code, response) =>
            {
                if (code == 403)
                {
                    Debug.LogError("[AntiCheatScience] [STEAM API] Ошибка " + code + " Возможно API Key устарел");
                    return;
                }

                if (code == 200)
                {
                    INFO info = new INFO();
                    resp steamResponse = JsonConvert.DeserializeObject<resp>(response);

                    int datetime = steamResponse.response.players[0].timecreated ?? 0;
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime create = epoch.AddSeconds(datetime).AddHours(3);

                    TimeSpan accountAge = DateTime.UtcNow - create;

                    var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
                    var playerData = data["players", userID] as Dictionary<string, object>;

                    if (playerData == null)
                    {
                        playerData = new Dictionary<string, object>
                        {
                            ["FirstJoin"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                            ["accountAge"] = accountAge.TotalDays
                        };

                        data["players", userID] = playerData;
                        data.Save();
                    }

                    if (accountAge.TotalDays < _config.Steam.SteamDays)
                    {
                        rust.RunServerCommand($"kick {userID} \"слишком молодой аккаунт!\"");
                    }
                }
                else
                {
                    Debug.LogError("[AntiCheatScience] [STEAM API] Ошибка запроса " + code);
                }
            }, this, RequestMethod.GET, null, 0f);

        }


        class resp
        {
            public avatar response;
        }

        class avatar
        {
            public List<Players> players;
        }

        class Players
        {
            public int? profilestate;
            public int? timecreated;
        }

        class INFO
        {
            public DateTime dateTime;
            public bool profilestate;
            public bool steam;
            public Dictionary<string, Dictionary<string, int>> hitinfo;
        }

        Dictionary<ulong, INFO> PLAYERINFO = new Dictionary<ulong, INFO>();


        #endregion

        #region RequestDC

        private string jsonwebhookviolationRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Nearby objects\",\"value\":\"[object]\",\"inline\":false}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Обьекты рядом\",\"value\":\"[object]\",\"inline\":false}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookcodeRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-Detect\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Code lock owner\",\"value\":\"[owner]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Детект\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Владелец кодового замка\",\"value\":\"[owner]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookaimdetectRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Weapon\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Distance\",\"value\":\"[metr] meters\",\"inline\":true},{\"name\":\"Detections\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Victim\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"HitBox\",\"value\":\"[headorbody]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"ХитБокс\",\"value\":\"[headorbody]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookaimdriveRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookaimlockRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Weapon\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Distance\",\"value\":\"[metr] meters\",\"inline\":true},{\"name\":\"Detections\",\"value\":\"[dCount]/70\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Victim\",\"value\":\"[victim]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/70\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookstashdetectRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Nearby sleeping bag\",\"value\":\"[sleepbag]\",\"inline\":true},{\"name\":\"Detections\",\"value\":\"[dCount]/[mCount]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Рядом спальник\",\"value\":\"[sleepbag]\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhooknightshotRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-Detect\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Weapon\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Distance\",\"value\":\"[metr] meters\",\"inline\":true},{\"name\":\"Time\",\"value\":\"[time]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Victim\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Flashlight/NV\",\"value\":\"[flash]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Детект\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Время\",\"value\":\"[time]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Фонарик/ПНВ\",\"value\":\"[flash]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookpilotshotRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookmanipRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Weapon\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Distance\",\"value\":\"[metr] meters\",\"inline\":true},{\"name\":\"Detections\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Victim\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Ping\",\"value\":\"(No info yet.)\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\\nFalse detections possible due to ping 170+\\nRecommended to request SpeedTest results\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Пинг\",\"value\":\"(Пока нет инфо.)\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\\nМогут быть ложные детекты из-за пинга 170+\\nРекомендуемо запросить результаты SpeedTest\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookflyfireRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-Ban\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Weapon\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Блокировка\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookmeleeRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Detections\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
            
        private string jsonwebhooksilentRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Detections\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookNFDRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}, {\"name\":\"Health\",\"value\":\"[h]\",\"inline\":true}, {\"name\":\"Health before\",\"value\":\"[hb]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}, {\"name\":\"Здоровье\",\"value\":\"[h]\",\"inline\":true}, {\"name\":\"Здоровье до\",\"value\":\"[hb]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private string jsonwebhookspiderRU = LangEN
            ? "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Logging-[banordetect]\",\"description\":\"Player detection ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Coordinates\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Playing with license: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Click here to go to the player's profile\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}"
            : "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

       private string bugreport =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-БагРепорт\",\"description\":\"Пришел БагРепорт с сервера **[sN]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null},{\"description\":\"Проблема: [dannie]\\n\\nДискорд тех.администратора: <@[id]>\",\"color\":null}],\"attachments\":[]}";

        private IEnumerator SendErrorLog(string form, string sys, string toAnalysis)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get($"{form}checkerror.php?protocol={sys}&error={toAnalysis}"))
            {
                yield return webRequest.SendWebRequest();
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    //Debug.LogError($"{webRequest.error} ({webRequest.responseCode})");
                }
                else
                {
                    string responseText = webRequest.downloadHandler.text;
                    if (responseText.Contains("The error was processed and written to the registry"))
                    {
                        Debug.LogWarning($"{responseText} ({webRequest.responseCode})");
                    }
                    else
                    {
                        if (responseText.Contains("Init"))
                        {
                            //Debug.LogWarning($"{responseText} ({webRequest.responseCode})");
                        }
                        else
                        {
                            Debug.LogError($"{responseText}");
                            ConVar.Server.stop(null);
                        }
                    }
                }
            }
        }

        private void RequestBugReport(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");

            webrequest.Enqueue("https://discord.com/api/webhooks/1236462204514209923/cfG57btYN-aodizL0hMZFo0N4ovDDkymWkYu1oPN0otsUFMdWrk8VVen4GZYnW7GnwR5", payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
            }, this, RequestMethod.POST, header);
        }

        private void RequestDC(string payload, Action<int> callback = null)
        {
            if (_config.Logs.isLog)
            {
                Dictionary<string, string> header = new Dictionary<string, string>();
                header.Add("Content-Type", "application/json");

                webrequest.Enqueue(payload.ToLower().Contains("блокировка") ? _config.Logs.WebhookB : _config.Logs.WebhookD, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response != null)
                        {
                            try
                            {
                                JObject json = JObject.Parse(response);
                                if (code == 429)
                                {
                                    float seconds =
                                        float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                                }
                                else
                                {
                                    PrintWarning(
                                        $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                                }
                            }
                            catch
                            {
                                PrintWarning(
                                    $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                            }
                        }
                        else
                        {
                            PrintWarning($"Discord didn't respond (down?) Code: {code}");
                        }
                    }

                }, this, RequestMethod.POST, header);
            }
        }

        #endregion RequestDC

        Dictionary<ulong, string> unicalID = new Dictionary<ulong, string>();
        int uniqueChislo = 0;
        void ChatDetect(BasePlayer player, string banReason)
        {
            foreach (BasePlayer adminPlayer in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(adminPlayer.UserIDString, "AntiCheatScience.can.seedetect"))
                {
                    if (adminPlayer != null)
                    {
                        if (!unicalID.ContainsKey(player.userID))
                        {
                            uniqueChislo++;
                            unicalID.Add(player.userID, $"{uniqueChislo}");
                        }
                        string playerName = player.displayName;
                        string message = LangEN ? "Player <color=yellow>{playerName}</color> detected: <color=red>{banReason}</color>\nStart surveillance using command: /acspec {unicalID[player.userID]}" : $"Игрок <color=yellow>{playerName}</color> задетекчен: <color=red>{banReason}</color>\nНачните слежку по команде: /acspec {unicalID[player.userID]}";
                        SendReply(adminPlayer, message);
                    }
                }
            }
        }

        [ConsoleCommand("cmd.acspec")]
        private void CmdAcspec(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                var player = arg.Connection.player as BasePlayer;
                if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
                {
                    SendReply(player, LangEN ? "You do not have permission to use this command!" : "У Вас нет прав на использование этой команды!");
                    return;
                }
                string action = arg.GetString(0);
                if (action == "stop")
                {
                    arg.Player().SendConsoleCommand("chat.say", "/acspec stop");
                }
            }
        }

        [ConsoleCommand("cmd.sosiban")]
        private void CmdSosiban(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                var player = arg.Connection.player as BasePlayer;
                if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
                {
                    SendReply(player, LangEN ? "You do not have permission to use this command!" : "У Вас нет прав на использование этой команды!");
                    return;
                }
                string id = arg.GetString(0);
                string adminid = arg.GetString(1);
                string reason = arg.GetString(2);
                if (id != null && adminid != null && reason != null)
                {
                    player.Respawn();
                    CuiHelper.DestroyUi(player, _Layer1);
                    if (_config.RustAPP.isRustAPP)
                    {
                        string command = $"ra.ban {player.userID.ToString()} \"{reason} [by spec {adminid}] [AntiCheatScience]\"";
                        switch (_config.PilotFire.banMeraAimDrive)
                        {
                            case 2:
                                command += " --global";
                                break;
                            case 3:
                                command += " --ban-ip";
                                break;
                            case 4:
                                command += " --ban-ip --global";
                                break;
                        }
                        rust.RunServerCommand(command);
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", id).Replace("%reason%", $"{reason} [by spec {adminid}]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
            }
        }

        [ChatCommand("acspec")]
        void SpectateCommand(BasePlayer player, string command, string[] args)
        {
            if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
            {
                SendReply(player, LangEN ? "You do not have permission to use this command!" : "У Вас нет прав на использование этой команды!");
                return;
            }

            if (args.Length == 0 || args.Length > 1)
            {
                SendReply(player, LangEN ? "Usage: /acspec Unique ID or stop" : "Использование: /acspec Уникальный айди или stop для того что бы остановить");
                return;
            }

            if (args[0].Contains("stop"))
            {
                StopSpec(player);
                return;
            }

            string uniqueId = args[0];
            ulong targetUserId = 0;
            bool found = false;

            foreach (KeyValuePair<ulong, string> entry in unicalID)
            {
                if (entry.Value == uniqueId)
                {
                    targetUserId = entry.Key;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                SendReply(player, LangEN ? "The player with the specified unique ID was not found." : "Игрок с указанным уникальным айди не найден.");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.FindByID(targetUserId);
            if (targetPlayer != null)
            {
                StartSpec(player, targetPlayer);
                SpecMenu(player, targetPlayer);
                /*string message = $"Админ <color=yellow>{player.displayName}</color> начал слежку за: <color=red>{targetPlayer.displayName}</color>, с уникальным айди: <color=red>{unicalID[player.userID]}</color>\nНачните слежку по команде: /acspec {unicalID[player.userID]}";
                SendReply(player, message);*/
            }
            else
            {
                SendReply(player, LangEN ? "The player with the specified unique ID was not found on the network." : "Игрок с указанным уникальным айди не найден в сети.");
            }
        }

        void StartSpec(BasePlayer player, BasePlayer target)
        {
            if (!player.IsSpectating())
            {
                player.StartSpectating();
                player.UpdateSpectateTarget(target.displayName);
            }
            else
            {
                player.UpdateSpectateTarget(target.displayName);
            }
        }

        void StopSpec(BasePlayer player)
        {
            if (player.IsSpectating())
            {
                player.StopSpectating();
                string message = LangEN ? "You've finished spectating." : "Вы закончили слежку.";
                SendReply(player, message);
                player.Respawn();
                CuiHelper.DestroyUi(player, _Layer1);
            }
            else
            {
                string message = LangEN ? "You're not spectating." : "Вы не следите.";
                SendReply(player, message);
            }
        }

        Dictionary<string, Vector3> _PlayerEyes = new Dictionary<string, Vector3>();
        const float TickPadding = 0.1f;

        private void SimulateProjectile(ref Vector3 position, ref Vector3 velocity, ref float partialTime, float travelTime, Vector3 gravity, float drag, out Vector3 prevPosition, out Vector3 prevVelocity)
        {
            float chsilo = 0.03125f;
            prevPosition = position;
            prevVelocity = velocity;
            if (partialTime > Mathf.Epsilon)
            {
                float chsilo2 = chsilo - partialTime;
                if (travelTime < chsilo2)
                {
                    prevPosition = position;
                    prevVelocity = velocity;
                    position += velocity * travelTime;
                    partialTime += travelTime;
                    return;
                }
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * chsilo2;
                velocity += gravity * chsilo;
                velocity -= velocity * (drag * chsilo);
                travelTime -= chsilo2;
            }
            int chsilo3 = Mathf.FloorToInt(travelTime / chsilo);
            for (int i = 0; i < chsilo3; i++)
            {
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * chsilo;
                velocity += gravity * chsilo;
                velocity -= velocity * (drag * chsilo);
            }
            partialTime = travelTime - chsilo * (float)chsilo3;
            if (partialTime > Mathf.Epsilon)
            {
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * partialTime;
            }
        }

        private int SAimDetect = 0;
        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info?.HitEntity == null || attacker == null || !_config.SilentAim.isSA)
                return null;
            var victimBP = info.HitEntity as BasePlayer;
            var initiatorBP = info.Initiator as BasePlayer;
            if (victimBP == null || initiatorBP == null)
            {
                return null;
            }
            if (victimBP == null || info.HitEntity.IsNpc)
                return null;
            if (!AimLockNaProverke.ContainsKey(attacker.userID))
            {
                AimLockNaProverke.Add(attacker.userID, false);
            }
            if (!AimLockShot.ContainsKey(attacker.userID))
            {
                AimLockShot.Add(attacker.userID, 0);
            }
            if (AimLockShot.ContainsKey(attacker.userID) && AimLockNaProverke[attacker.userID])
            {
                AimLockShot[attacker.userID]++;
            }
            if (FreePlayerFriendly != null)
            {
                if ((bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", attacker.UserIDString))
                    return null;
            }
            if (MultiFighting != null)
            {
                if ((bool)MultiFighting.CallHook("IsSteam", attacker.Connection))
                    return null;
            }
            if (FLuma != null)
            {
                if ((bool)FLuma.CallHook("IsSteam", attacker.Connection))
                    return null;
            }
            if (FreePlayerFriendly != null)
            {
                if ((bool)FreePlayerFriendly.CallHook("IsPlayerNoSteam", attacker.UserIDString))
                    return null;
            }
            if (Battles != null)
            {
                if ((bool)Battles.CallHook("IsPlayerOnBattle", attacker.userID))
                    return null;
            }
            if (AimTrain != null)
            {
                if ((bool)AimTrain.CallHook("IsAimTraining", attacker.userID))
                    return null;
            }
            string steamID = attacker.userID.ToString();

            Vector3 startPos;
            if (_PlayerEyes.TryGetValue(steamID, out startPos))
            {
                BasePlayer.FiredProjectile firedProjectile;
                if (attacker.firedProjectiles.TryGetValue(info.ProjectileID, out firedProjectile))
                {
                    if (firedProjectile.protection > 0)
                    {
                        if (firedProjectile.protection >= 4)
                        {
                            if (info.HitEntity is BasePlayer)
                            {
                                if (attacker.GetParentEntity() is CargoShip || attacker.GetParentEntity() is BaseBoat || attacker.GetParentEntity() is Tugboat || attacker.GetParentEntity() is ScrapTransportHelicopter || attacker.GetParentEntity() is Minicopter || attacker.GetParentEntity() is MiningQuarry)
                                    return null;
                                Vector3 projectileVelocity = info.ProjectileVelocity;
                                bool isLowSpeed = projectileVelocity.magnitude < 100f;
                                Vector3 nextProjPos = startPos + projectileVelocity * (isLowSpeed ? 0.008f : 0.005f);
                                Vector3 realDirection = startPos + attacker.eyes.BodyForward() * projectileVelocity.magnitude * (isLowSpeed ? 0.008f : 0.005f);
                                float pointDist = Vector3.Distance(realDirection, nextProjPos);
                                float hitDist = Vector3.Distance(startPos, info.HitPositionWorld);
                                BasePlayer player = info.HitEntity as BasePlayer;
                                float maxAngle = (attacker.GetMounted() != null ? attacker.desyncTimeClamped * 0.12f : 0f) + attacker.desyncTimeClamped * 0.12f + (player.modelState.sprinting ? attacker.desyncTimeClamped * (hitDist > 15f ? 0.005f : 0.11f) : 0f)
                                    + (attacker.modelState.sprinting ? attacker.desyncTimeClamped * 0.005f : 0f) - (isLowSpeed ? 0f : hitDist * 0.0002f);
                                string weaponM = info.Weapon?.GetItem()?.info.shortname;
                                if (Vector3.Distance(attacker.transform.position, info.HitPositionWorld) > 170f || Vector3.Distance(attacker.transform.position, info.HitPositionWorld) < 5f)
                                    return null;
                                BaseProjectile weapon = info.Weapon as BaseProjectile;
                                if (weaponM.Contains("rifle.ak") || weaponM.Contains("rifle.lr300") || weaponM.Contains("hmlmg") || weaponM.Contains("lmg.m249"))
                                {
                                    var attachments = weapon.GetItem()?.contents?.itemList;
                                    if (attachments != null)
                                    {  
                                        foreach (Item mod in attachments)
                                        {
                                            if (mod.info.shortname.ToLower().Contains("small.scope") || mod.info.shortname.ToLower().Contains("8x.scope"))
                                            {
                                                return null;
                                            }
                                        }
                                    }
                                }

                                if (pointDist > maxAngle + 0.675f)
                                {
                                    attacker.stats.combat.LogInvalid(info, "Урон заблокирован АнтиЧитом");
                                    SAimDetect++;
                                    DetectNo11(attacker, SAimDetect, false);
                                    if (SAimDetect == 1)
                                    {
                                        timer.Once(15f, () =>
                                        {
                                            SAimDetect = 0;
                                        });
                                    }

                                    if (SAimDetect == _config.SilentAim.maxDetect)
                                    {
                                        DetectNo11(attacker, SAimDetect, true);
                                        if (_config.SilentAim.isSAimBan)
                                        {
                                            if (_config.RustAPP.isRustAPP)
                                            {
                                                string command = $"ra.ban {attacker.userID.ToString()} \"{_config.SilentAim.NameSAim} [AntiCheatScience]\"";
                                                switch (_config.SilentAim.banMeraSAim)
                                                {
                                                    case 2:
                                                        command += " --global";
                                                        break;
                                                    case 3:
                                                        command += " --ban-ip";
                                                        break;
                                                    case 4:
                                                        command += " --ban-ip --global";
                                                        break;
                                                }
                                                rust.RunServerCommand(command);
                                            }
                                            if (!_config.RustAPP.isRustAPPbanOnly)
                                            {
                                                string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.SilentAim.NameSAim} [AntiCheatScience]");
                                                rust.RunServerCommand($"{commanda}");
                                            }
                                        }
                                        else
                                        {
                                            string commanda = _config.Global.KickCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.SilentAim.NameSAim} [AntiCheatScience]");
                                            rust.RunServerCommand(commanda);
                                        }
                                    }

                                    if (_config.Global.isAimBlocDamage)
                                    {
                                        info.damageTypes.ScaleAll(0.01f);
                                    }

                                    return null;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (!_PlayerEyes.ContainsKey(steamID))
                {
                    _PlayerEyes.Add(steamID, Vector3.zero);
                }
            }

            return null;
        }

        void PilotFire(BasePlayer attacker)
        {
            if (attacker.GetParentEntity() is BaseBoat || attacker.GetParentEntity() is Sled || attacker.GetParentEntity() is SledSeat || attacker.GetParentEntity() is RidableHorse || attacker.GetParentEntity() is HorseCorpse)
                return;
            DetectNo2(attacker);
            if (_config.RustAPP.isRustAPP)
            {
                string command = $"ra.ban {attacker.userID.ToString()} \"{_config.PilotFire.NameAimDrive} [AntiCheatScience]\"";
                switch (_config.PilotFire.banMeraAimDrive)
                {
                    case 2:
                        command += " --global";
                        break;
                    case 3:
                        command += " --ban-ip";
                        break;
                    case 4:
                        command += " --ban-ip --global";
                        break;
                }
                rust.RunServerCommand(command);
            }
            if (!_config.RustAPP.isRustAPPbanOnly)
            {
                string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.PilotFire.NameAimDrive} [AntiCheatScience]");
                rust.RunServerCommand($"{commanda}");
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_PlayerEyes.ContainsKey(player.userID.ToString()))
                _PlayerEyes.Remove(player.userID.ToString());
        }

        void NoFallDamage(BasePlayer player, float health, float healthBefore)
        {
            if (player.GetParentEntity() is BaseLadder)
                return;
            DetectNo12(player, health, healthBefore);
            if (_config.RustAPP.isRustAPP)
            {
                string command = $"ra.ban {player.userID.ToString()} \"{_config.NoFallDamage.NameNFD} [AntiCheatScience]\"";
                switch (_config.NoFallDamage.banMeraNFD)
                {
                    case 2:
                        command += " --global";
                        break;
                    case 3:
                        command += " --ban-ip";
                        break;
                    case 4:
                        command += " --ban-ip --global";
                        break;
                }
                rust.RunServerCommand(command);
            }
            if (!_config.RustAPP.isRustAPPbanOnly)
            {
                string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.NoFallDamage.NameNFD} [AntiCheatScience]");
                rust.RunServerCommand($"{commanda}");
            }
        }
    }
}