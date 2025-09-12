using Newtonsoft.Json; 
using Newtonsoft.Json.Converters; 
using Newtonsoft.Json.Linq; 
using Oxide.Core; 
using Oxide.Core.Configuration; 
using Oxide.Core.Plugins; 
using Oxide.Game.Rust.Cui; 
using System; using System.Collections.Generic; 
using System.Globalization; 
using System.Linq; 
using UnityEngine; 

namespace Oxide.Plugins 
{ 
    [Info("VKBot", "ds:alone_sempai / https://topplugin.ru/", "9.0.0")] 
    class VKBot : RustPlugin 
    { 
        [PluginReference] Plugin Duel, ImageLibrary; 
        
        static string apiver = "v=5.92"; 
        
        private bool OxideUpdateSended = false; 
        private System.Random random = new System.Random(); 
        private bool NewWipe = false; 
        JsonSerializerSettings jsonsettings; 

        private List<string> allowedentity = new List<string>() 
        { 
            "door", 
            "wall.window.bars.metal", 
            "wall.window.bars.toptier", 
            "wall.external", 
            "gates.external.high", 
            "floor.ladder", 
            "embrasure", 
            "floor.grill", 
            "wall.frame.fence", 
            "wall.frame.cell", 
            "foundation", 
            "floor.frame", 
            "floor.triangle", 
            "floor", 
            "foundation.steps", 
            "foundation.triangle", 
            "roof", 
            "stairs.l", 
            "stairs.u", 
            "wall.doorway", 
            "wall.frame", 
            "wall.half", 
            "wall.low", 
            "wall.window", 
            "wall", 
            "wall.external.high.stone" 
        }; 
        
        List<string> ExplosiveList = new List<string>() 
        { 
            "explosive.satchel.deployed", 
            "grenade.f1.deployed", 
            "grenade.beancan.deployed", 
            "explosive.timed.deployed" 
        }; 
        
        private List<ulong> BDayPlayers = new List<ulong>(); 
        
        class GiftItem 
        { 
            public string shortname; 
            public ulong skinid; 
            public int count; 
        } 
        
        class ServerInfo 
        { 
            public string name; 
            public string online; 
            public string slots; 
            public string sleepers; 
            public string map; 
        } 
        
        private Dictionary<BasePlayer, DateTime> GiftsList = new Dictionary<BasePlayer, DateTime>(); 
        
        private ConfigData config; 
        private class ConfigData 
        { 
            [JsonProperty(PropertyName = "Ключи VK API, ID группы")] public VKAPITokens VKAPIT;
            [JsonProperty(PropertyName = "Настройки оповещений администраторов")] public AdminNotify AdmNotify;
            [JsonProperty(PropertyName = "Настройки оповещений в беседу")] public ChatNotify ChNotify;
            [JsonProperty(PropertyName = "Настройки статуса")] public StatusSettings StatusStg; 
            [JsonProperty(PropertyName = "Оповещения при вайпе")] public WipeSettings WipeStg;
            [JsonProperty(PropertyName = "Награда за вступление в группу")] public GroupGifts GrGifts;
            [JsonProperty(PropertyName = "Награда для именинников")] public BDayGiftSet BDayGift;
            [JsonProperty(PropertyName = "Поддержка нескольких серверов")] public MultipleServersSettings MltServSet;
            [JsonProperty(PropertyName = "Топ игроки вайпа и промо")] public TopWPlPromoSet TopWPlayersPromo;
            [JsonProperty(PropertyName = "Настройки чат команд")] public CommandSettings CMDSet;
            [JsonProperty(PropertyName = "Динамическая обложка группы")] public DynamicGroupLabelSettings DGLSet;
            [JsonProperty(PropertyName = "Виджет сообщества")] public GroupWidgetSettings GrWgSet;
            [JsonProperty(PropertyName = "Настройки GUI меню")] public GUISettings GUISet;
            
            public class VKAPITokens 
            { 
                [JsonProperty(PropertyName = "VK Token группы (для сообщений)")] public string VKToken = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "VK Token приложения (для записей на стене и статуса)")] public string VKTokenApp = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "VKID группы")] public string GroupID = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "Ссылка на группу вк")] public string txtvk = "   Ссылка на нашу группу вк\n         https://vk.com/droprustof";
            } 
            public class AdminNotify 
            { 
                [JsonProperty(PropertyName = "VkID модераторов (для отправки репортов)")] public string VkID = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "VkID администрации (для отправки всех уведомлений)")] public string VkOwnerID = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "Включить отправку сообщений администратору командой /report ?")] public bool SendReports = true; 
                [JsonProperty(PropertyName = "Включить GUI для команды /report ?")] public bool GUIReports = false; 
                [JsonProperty(PropertyName = "Очистка базы репортов при вайпе?")] public bool ReportsWipe = true; 
                [JsonProperty(PropertyName = "Предупреждение о злоупотреблении функцией репортов")] public string ReportsNotify = "Наличие в тексте нецензурных выражений, оскорблений администрации или игроков сервера, а так же большое количество безсмысленных сообщений приведет к бану!"; 
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о бане игрока?")] public bool UserBannedMsg = true; 
                [JsonProperty(PropertyName = "Комментарий в обсуждения о бане игрока?")] public bool UserBannedTopic = false; 
                [JsonProperty(PropertyName = "ID обсуждения")] public string BannedTopicID = "none";
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о нерабочих плагинах?")] public bool PluginsCheckMsg = true; 
                [JsonProperty(PropertyName = "Проверка обновлений Oxide")] public bool OxideCheckMsg = false; 
            }
            public class ChatNotify 
            { 
                [JsonProperty(PropertyName = "VK Token приложения (лучше использовать отдельную страницу для получения токена)")] public string ChNotfToken = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "ID беседы")] public string ChatID = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "Включить отправку оповещений в беседу?")] public bool ChNotfEnabled = false; 
                [JsonProperty(PropertyName = "Дополнительная отправка оповещений в личку администраторам?")] public bool AdmMsg = false; 
                [JsonProperty(PropertyName = "Список оповещений отправляемых в беседу (доступно: reports, wipe, bans, plugins)")] public string ChNotfSet = "reports, wipe, bans, plugins"; 
            } 
            public class StatusSettings 
            { 
                [JsonProperty(PropertyName = "Обновлять статус в группе? Если стоит /false/ статистика собираться не будет")] public bool UpdateStatus = true; 
                [JsonProperty(PropertyName = "Вид статуса (1 - текущий сервер, 2 - список серверов, необходим Rust:IO на каждом сервере)")] public int StatusSet = 1; 
                [JsonProperty(PropertyName = "Онлайн в статусе вида '125/200'")] public bool OnlWmaxslots = false; 
                [JsonProperty(PropertyName = "Таймер обновления статуса (минуты)")] public int UpdateTimer = 30; 
                [JsonProperty(PropertyName = "Формат статуса")] public string StatusText = "{usertext}. Сервер вайпнут: {wipedate}. Онлайн игроков: {onlinecounter}. Спящих: {sleepers}. Добыто дерева: {woodcounter}. Добыто серы: {sulfurecounter}. Выпущено ракет: {rocketscounter}. Время обновления: {updatetime}. Использовано взрывчатки: {explosivecounter}. Создано чертежей: {blueprintsconter}. {connect}"; [JsonProperty(PropertyName = "Список счетчиков, которые будут отображаться в виде emoji")] public string EmojiCounterList = "onlinecounter, rocketscounter, blueprintsconter, explosivecounter, wipedate"; 
                [JsonProperty(PropertyName = "Ссылка на коннект сервера вида /connect 111.111.111.11:11111/")] public string Connecturl = "connect 111.111.111.11:11111"; 
                [JsonProperty(PropertyName = "Текст для статуса")] public string StatusUT = "Сервер 1"; 
            } 
            public class WipeSettings 
            { 
                [JsonProperty(PropertyName = "Отправлять пост в группу после вайпа?")] public bool WPostB = false; 
                [JsonProperty(PropertyName = "Текст поста о вайпе")] public string WPostMsg = "Заполните эти поля, и выполните команду o.reload VKBot"; 
                [JsonProperty(PropertyName = "Добавить изображение к посту о вайпе?")] public bool WPostAttB = false; 
                [JsonProperty(PropertyName = "Ссылка на изображение к посту о вайпе вида 'photo-1_265827614' (изображение должно быть в альбоме группы)")] public string WPostAtt = "photo-1_265827614"; 
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о вайпе?")] public bool WPostMsgAdmin = true; 
                [JsonProperty(PropertyName = "Отправлять игрокам сообщение о вайпе автоматически?")] public bool WMsgPlayers = false; 
                [JsonProperty(PropertyName = "Текст сообщения игрокам о вайпе (сообщение отправляется только тем кто подписался командой /vk wipealerts)")] public string WMsgText = "Сервер вайпнут! Залетай скорее!";
                [JsonProperty(PropertyName = "Игнорировать команду /vk wipealerts? (если включено, сообщение о вайпе будет отправляться всем)")] public bool WCMDIgnore = false; [JsonProperty(PropertyName = "Смена названия группы после вайпа")] public bool GrNameChange = false; 
                [JsonProperty(PropertyName = "Название группы (переменная {wipedate} отображает дату последнего вайпа)")] public string GrName = "ServerName | WIPE {wipedate}"; 
            } 
            public class GroupGifts 
            { 
                [JsonProperty(PropertyName = "Выдавать подарок игроку за вступление в группу ВК?")] public bool VKGroupGifts = true; 
                [JsonProperty(PropertyName = "Подарок за вступление в группу (команда, если стоит none выдаются предметы из файла data/VKBot.json). Пример: grantperm {steamid} vkraidalert.allow 7d")] public string VKGroupGiftCMD = "none"; 
                [JsonProperty(PropertyName = "Описание команды")] public string GiftCMDdesc = "Оповещения о рейде на 7 дней"; 
                [JsonProperty(PropertyName = "Ссылка на группу ВК")] public string VKGroupUrl = "vk.com/tumblerzverya"; 
                [JsonProperty(PropertyName = "Оповещения в общий чат о получении награды")] public bool GiftsBool = true; 
                [JsonProperty(PropertyName = "Включить оповещения для игроков не получивших награду за вступление в группу?")] public bool VKGGNotify = true; 
                [JsonProperty(PropertyName = "Интервал оповещений для игроков не получивших награду за вступление в группу (в минутах)")] public int VKGGTimer = 30; 
                [JsonProperty(PropertyName = "Выдавать награду каждый вайп?")] public bool GiftsWipe = true; 
            } 
            public class BDayGiftSet 
            { 
                [JsonProperty(PropertyName = "Включить награду для именинников?")] public bool BDayEnabled = true; 
                [JsonProperty(PropertyName = "Группа для именинников")] public string BDayGroup = "bdaygroup"; 
                [JsonProperty(PropertyName = "Оповещения в общий чат о именинниках")] public bool BDayNotify = false; 
            } 
            public class MultipleServersSettings 
            {
                [JsonProperty(PropertyName = "Включить поддержку несколько серверов?")] public bool MSSEnable = false; 
                [JsonProperty(PropertyName = "Номер сервера")] public int ServerNumber = 1; 
                [JsonProperty(PropertyName = "Сервер 1 IP:PORT (пример: 111.111.111.111:28015)")] public string Server1ip = "none"; 
                [JsonProperty(PropertyName = "Название сервера 1 (если стоит none, используется номер)")] public string Server1name = "none"; 
                [JsonProperty(PropertyName = "Сервер 2 IP:PORT (пример: 111.111.111.111:28015)")] public string Server2ip = "none"; 
                [JsonProperty(PropertyName = "Название сервера 2 (если стоит none, используется номер)")] public string Server2name = "none"; 
                [JsonProperty(PropertyName = "Сервер 3 IP:PORT (пример: 111.111.111.111:28015)")] public string Server3ip = "none"; 
                [JsonProperty(PropertyName = "Название сервера 3 (если стоит none, используется номер)")] public string Server3name = "none"; 
                [JsonProperty(PropertyName = "Сервер 4 IP:PORT (пример: 111.111.111.111:28015)")] public string Server4ip = "none";
                [JsonProperty(PropertyName = "Название сервера 4 (если стоит none, используется номер)")] public string Server4name = "none"; 
                [JsonProperty(PropertyName = "Сервер 5 IP:PORT (пример: 111.111.111.111:28015)")] public string Server5ip = "none"; 
                [JsonProperty(PropertyName = "Название сервера 5 (если стоит none, используется номер)")] public string Server5name = "none"; 
                [JsonProperty(PropertyName = "Онлайн в emoji?")] public bool EmojiStatus = true; 
            } 
            public class TopWPlPromoSet 
            { 
                [JsonProperty(PropertyName = "Включить топ игроков вайпа")] public bool TopWPlEnabled = true; 
                [JsonProperty(PropertyName = "Включить отправку промо кодов за топ?")] public bool TopPlPromoGift = false; 
                [JsonProperty(PropertyName = "Пост на стене группы о топ игроках вайпа")] public bool TopPlPost = true; 
                [JsonProperty(PropertyName = "Ссылка на изображение к посту вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")] public string TopPlPostAtt = "none"; 
                [JsonProperty(PropertyName = "Промо для топ рэйдера")] public string TopRaiderPromo = "topraider"; 
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ рейдеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")] public string TopRaiderPromoAtt = "none"; 
                [JsonProperty(PropertyName = "Промо для топ килера")] public string TopKillerPromo = "topkiller"; 
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ киллеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")] public string TopKillerPromoAtt = "none"; 
                [JsonProperty(PropertyName = "Промо для топ фармера")] public string TopFarmerPromo = "topfarmer"; 
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ фармеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")] public string TopFarmerPromoAtt = "none"; 
                [JsonProperty(PropertyName = "Ссылка на донат магазин")] public string StoreUrl = "server.gamestores.ru"; 
                [JsonProperty(PropertyName = "Автоматическая генерация промокодов после вайпа")] public bool GenRandomPromo = false; 
            }
            public class CommandSettings 
            { 
                [JsonProperty(PropertyName = "Включить репорт систему?")] public bool ReportStatus = false; 
                [JsonProperty(PropertyName = "Команда отправки сообщения администратору")] public string CMDreport = "report"; 
            } 
            public class DynamicGroupLabelSettings 
            { 
                [JsonProperty(PropertyName = "Включить динамическую обложку?")] public bool DLEnable = false; 
                [JsonProperty(PropertyName = "Ссылка на скрипт обновления")] public string DLUrl = "none"; 
                [JsonProperty(PropertyName = "Таймер обновления (в минутах)")] public int DLTimer = 10; 
                [JsonProperty(PropertyName = "Обложка с онлайном нескольких серверов (все настройки ниже игнорируются)")] public bool DLMSEnable = false; 
                [JsonProperty(PropertyName = "Текст блока 1 (доступны все переменные как в статусе)")] public string DLText1 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 2 (доступны все переменные как в статусе)")] public string DLText2 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 3 (доступны все переменные как в статусе)")] public string DLText3 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 4 (доступны все переменные как в статусе)")] public string DLText4 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 5 (доступны все переменные как в статусе)")] public string DLText5 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 6 (доступны все переменные как в статусе)")] public string DLText6 = "none"; 
                [JsonProperty(PropertyName = "Текст блока 7 (доступны все переменные как в статусе)")] public string DLText7 = "none"; 
                [JsonProperty(PropertyName = "Включить вывод топ игроков на обложку?")] public bool TPLabel = false; 
            } 
            public class GroupWidgetSettings 
            { 
                [JsonProperty(PropertyName = "Включить обновление виджета?")] public bool WgEnable = false; 
                [JsonProperty(PropertyName = "Таймер обновления (минуты)")] public int UpdateTimer = 3; 
                [JsonProperty(PropertyName = "Заголовок виджета")] public string WgTitle = "Мониторинг серверов"; 
                [JsonProperty(PropertyName = "Ключ приложения для работы с виджетом (Инструкция - https://goo.gl/LpZujf)")] public string WgToken = "none"; 
                [JsonProperty(PropertyName = "Текст дополнительной ссылки (если стоит none, не используется)")] public string URLTitle = "none"; 
                [JsonProperty(PropertyName = "Дополнительная ссылка (разрешены только vk.com ссылки)")] public string URL = "none"; 
            } 
            public class GUISettings 
            { 
                [JsonProperty(PropertyName = "Ссылка на логотип сервера")] public string Logo = "https://i.imgur.com/QNZykaS.png"; 
                [JsonProperty(PropertyName = "Позиция GUI AnchorMin (дефолт 0.347 0.218)")] public string AnchorMin = "0.347 0.218"; 
                [JsonProperty(PropertyName = "Позиция GUI AnchorMax (дефолт 0.643 0.782)")] public string AnchorMax = "0.643 0.782"; 
                [JsonProperty(PropertyName = "Цвет фона меню")] public string BgColor = "#00000099"; 
                [JsonProperty(PropertyName = "Цвет кнопки ЗАКРЫТЬ")] public string BCloseColor = "#DB0000ff"; 
                [JsonProperty(PropertyName = "Цвет кнопки ПОЛУЧИТЬ КОД")] public string BSendColor = "#1FEF00ff"; 
                [JsonProperty(PropertyName = "Цвет остальных кнопок")] public string BMenuColor = "#494949ff"; 
            } 
        } 
        
        private void LoadVariables() 
        { 
            bool changed = false; 
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate; 
            config = Config.ReadObject<ConfigData>(); 
            if (config.AdmNotify == null) 
            { 
                config.AdmNotify = new ConfigData.AdminNotify(); 
                changed = true; 
            } 
            if (config.ChNotify == null) 
            {
                config.ChNotify = new ConfigData.ChatNotify(); 
                changed = true; 
            } 
            if (config.WipeStg == null) 
            { 
                config.WipeStg = new ConfigData.WipeSettings(); 
                changed = true; 
            } 
            if (config.GrGifts == null) 
            { 
                config.GrGifts = new ConfigData.GroupGifts(); 
                changed = true; 
            } 
            if (config.TopWPlayersPromo == null) 
            { 
                config.TopWPlayersPromo = new ConfigData.TopWPlPromoSet(); 
                changed = true; 
            } 
            if (config.CMDSet == null) 
            { 
                config.CMDSet = new ConfigData.CommandSettings(); 
                changed = true; 
            } 
            if (config.DGLSet == null) 
            { 
                config.DGLSet = new ConfigData.DynamicGroupLabelSettings(); 
                changed = true; 
            } 
            if (config.GrWgSet == null) 
            { 
                config.GrWgSet = new ConfigData.GroupWidgetSettings(); 
                changed = true; 
            } 
            if (config.GUISet == null) 
            { 
                config.GUISet = new ConfigData.GUISettings(); 
                changed = true; 
            } 
            if (config.GUISet.Logo == "https://i.imgur.com/QNZykaS.png" && ConVar.Server.headerimage != string.Empty) 
                config.GUISet.Logo = ConVar.Server.headerimage; Config.WriteObject(config, true); 
            if (changed) PrintWarning("Конфигурационный файл обновлен"); 
        } 
        
        protected override void LoadDefaultConfig() 
        { 
            var configData = new ConfigData 
            {
                VKAPIT = new ConfigData.VKAPITokens(), 
                AdmNotify = new ConfigData.AdminNotify(), 
                ChNotify = new ConfigData.ChatNotify(), 
                StatusStg = new ConfigData.StatusSettings(), 
                WipeStg = new ConfigData.WipeSettings(), 
                GrGifts = new ConfigData.GroupGifts(), 
                BDayGift = new ConfigData.BDayGiftSet(), 
                MltServSet = new ConfigData.MultipleServersSettings(), 
                TopWPlayersPromo = new ConfigData.TopWPlPromoSet(), 
                CMDSet = new ConfigData.CommandSettings(), 
                DGLSet = new ConfigData.DynamicGroupLabelSettings(), 
                GrWgSet = new ConfigData.GroupWidgetSettings(), 
                GUISet = new ConfigData.GUISettings() 
            }; 
            Config.WriteObject(configData, true); 
        } 
        
        class DataStorageStats 
        { 
            public int WoodGath; 
            public int SulfureGath; 
            public int Rockets; 
            public int Blueprints; 
            public int Explosive; 
            public int Reports; 
            public List<GiftItem> Gifts; 
            public DataStorageStats() { } 
        } 
        class DataStorageUsers 
        { 
            public Dictionary<ulong, VKUDATA> VKUsersData = new Dictionary<ulong, VKUDATA>(); 
            public DataStorageUsers() { } 
        } 
        class VKUDATA 
        { 
            public ulong UserID; 
            public string Name; 
            public string VkID; 
            public string VkOwnerID; 
            public int ConfirmCode; 
            public bool Confirmed; 
            public bool GiftRecived; 
            public string LastRaidNotice; 
            public bool WipeMsg; 
            public string Bdate; 
            public int Raids; 
            public int Kills; 
            public int Farm; 
            public string LastSeen; 
        } 
        class DataStorageReports 
        { 
            public Dictionary<int, REPORT> VKReportsData = new Dictionary<int, REPORT>(); 
            public DataStorageReports() { } 
        } 
        class REPORT 
        { 
            public ulong UserID; 
            public string Name;
            public string Text; 
        } 

        DataStorageStats statdata; 
        DataStorageUsers usersdata; 
        DataStorageReports reportsdata; 
        private DynamicConfigFile VKBData; 
        private DynamicConfigFile StatData; 
        private DynamicConfigFile ReportsData; 
        
        void LoadData() 
        { 
            try 
            { 
                statdata = Interface.GetMod().DataFileSystem.ReadObject<DataStorageStats>("VKBot"); 
                usersdata = Interface.GetMod().DataFileSystem.ReadObject<DataStorageUsers>("VKBotUsers"); 
                reportsdata = Interface.GetMod().DataFileSystem.ReadObject<DataStorageReports>("VKBotReports"); 
            } 
            catch 
            { 
                statdata = new DataStorageStats(); usersdata = new DataStorageUsers(); 
                reportsdata = new DataStorageReports(); 
            } 
        } 
        
        private void OnServerInitialized() 
        {			
            PrintWarning("\n-----------------------------\n " +" Author - Sempai#3239\n " +" VK - https://vk.com/rustnastroika\n " +" Forum - https://topplugin.ru\n " +" Discord - https://discord.gg/5DPTsRmd3G\n" +"-----------------------------");
            
            LoadVariables(); 
            
            if (!config.AdmNotify.GUIReports) 
            { 
                Unsubscribe(nameof(OnServerCommand)); 
                Unsubscribe(nameof(OnPlayerCommand)); 
            } 
            VKBData = Interface.Oxide.DataFileSystem.GetFile("VKBotUsers"); 
            StatData = Interface.Oxide.DataFileSystem.GetFile("VKBot"); 
            ReportsData = Interface.Oxide.DataFileSystem.GetFile("VKBotReports"); 
            ImageLibrary.Call("AddImage", VkICO, ".VkICO"); 
            ImageLibrary.Call("AddImage", GiftICO, ".GiftICO"); 
            ImageLibrary.Call("AddImage", AlertICO, ".AlertICO"); 
            ImageLibrary.Call("AddImage", "https://i.ibb.co/945mK12/f7ec5a22d194f345.png", "BackgroundImage"); 
            ImageLibrary.Call("AddImage", "https://i.ibb.co/rbnB64d/0cxybrs.png", "ButtonBlock");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/vxbzBdr/1AMYOO9.png", "alerts");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/vxbzBdr/1AMYOO9.png", "alerts1");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/hYf91T3/8seJ7Wi.png", "alertvkback");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/LtZ6b4F/Kz4YbZH.png", "vkdeleteback");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/hF6hqdj/GnyEmwq.png", "giftrewardback");
            
            permission.RegisterPermission(AlertPermission, this); 
            
            string msg2 = null; 
            msg2 = $"[VKBot] Сервер успешно загружен."; 
            
            LoadData(); 
            
            if (statdata.Gifts == null) 
            { 
                statdata.Gifts = new List<GiftItem>() 
                { 
                    new GiftItem 
                    { 
                        shortname = "supply.signal", 
                        count = 1, 
                        skinid = 0 
                    }, 
                    new GiftItem 
                    { 
                        shortname = "pookie.bear", 
                        count = 2, 
                        skinid = 0 
                    } 
                }; 
                
                StatData.WriteObject(statdata); 
            } 
            
            if (config.CMDSet.ReportStatus == true) 
            { 
                cmd.AddChatCommand(config.CMDSet.CMDreport, this, "SendReport"); 
            } 
            
            CheckAdminID(); 
            
            if (NewWipe) 
                WipeFunctions(); 
            if (config.StatusStg.UpdateStatus) 
            { 
                if (config.StatusStg.StatusSet == 1) 
                    timer.Repeat(config.StatusStg.UpdateTimer * 60, 0, Update1ServerStatus); 
                if (config.StatusStg.StatusSet == 2) 
                    timer.Repeat(config.StatusStg.UpdateTimer * 60, 0, () => { 
                        UpdateMultiServerStatus("status"); 
                    }); 
            } 
            if (config.GrWgSet.WgEnable) 
            { 
                if (config.GrWgSet.WgToken == "none") 
                    PrintWarning($"Ошибка обновления виджета! В файле конфигурации не указан ключ!"); 
                else 
                    timer.Repeat(config.GrWgSet.UpdateTimer * 60, 0, () => { 
                        UpdateMultiServerStatus("widget"); 
                    }); 
            } 
            if (config.DGLSet.DLEnable && config.DGLSet.DLUrl != "none") 
            { 
                timer.Repeat(config.DGLSet.DLTimer * 60, 0, () => { 
                    if (config.DGLSet.DLMSEnable) 
                    { 
                        UpdateMultiServerStatus("label"); 
                    } 
                    else 
                    {
                        UpdateVKLabel(); 
                    } 
                }); 
            } 
            if (config.GrGifts.VKGGNotify) 
                timer.Repeat(config.GrGifts.VKGGTimer * 60, 0, GiftNotifier); 
            if (config.AdmNotify.PluginsCheckMsg) 
                CheckPlugins(); 
            if (config.AdmNotify.OxideCheckMsg) 
                CheckOxideUpdate(); 
                    
            SendVkMessage(config.AdmNotify.VkOwnerID, msg2); 
        } 
            
        private void OnServerSave() 
        { 
            if (config.TopWPlayersPromo.TopWPlEnabled) 
                VKBData.WriteObject(usersdata); 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) 
                StatData.WriteObject(statdata); 
        } 
        
        private void Init() 
        { 
            cmd.AddChatCommand("regvk", this, "VKcommand"); 
            cmd.AddConsoleCommand("updatestatus", this, "UStatus"); 
            cmd.AddConsoleCommand("updatewidget", this, "UWidget"); 
            cmd.AddConsoleCommand("updatelabel", this, "ULabel"); 
            cmd.AddConsoleCommand("sendmsgadmin", this, "MsgAdmin"); 
            cmd.AddConsoleCommand("wipealerts", this, "WipeAlerts"); 
            cmd.AddConsoleCommand("userinfo", this, "GetUserInfo"); 
            cmd.AddConsoleCommand("report.answer", this, "ReportAnswer"); 
            cmd.AddConsoleCommand("reports.list", this, "ReportList"); 
            cmd.AddConsoleCommand("report.wipe", this, "ReportClear"); 
            cmd.AddConsoleCommand("usersdata.update", this, "UpdateUsersData"); 
            
            jsonsettings = new JsonSerializerSettings(); 
            jsonsettings.Converters.Add(new KeyValuePairConverter()); 
        } 
        
        private void Loaded() => LoadMessages(); 
        
        private void Unload() 
        { 
            if (config.AdmNotify.SendReports) 
                ReportsData.WriteObject(reportsdata); 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) 
                StatData.WriteObject(statdata); 
            if (config.TopWPlayersPromo.TopWPlEnabled) 
                VKBData.WriteObject(usersdata); 
            if (config.BDayGift.BDayEnabled && BDayPlayers.Count > 0) 
            { 
                foreach (var id in BDayPlayers) 
                    permission.RemoveUserGroup(id.ToString(), config.BDayGift.BDayGroup); 
                    
                BDayPlayers.Clear(); 
            } 
            
            UnloadAllGUI(); 
        } 
        
        private void OnNewSave(string filename) => NewWipe = true; 
        
        private void OnPlayerInit(BasePlayer player) 
        { 
            if (usersdata.VKUsersData.ContainsKey(player.userID) && usersdata.VKUsersData[player.userID].Name != player.displayName) 
            { 
                usersdata.VKUsersData[player.userID].Name = player.displayName; VKBData.WriteObject(usersdata); 
            } 
            if (OpenReportUI.Contains(player)) 
                OpenReportUI.Remove(player); 
        } 
        
        private void OnPlayerSleepEnded(BasePlayer player) 
        { 
            if (!usersdata.VKUsersData.ContainsKey(player.userID)) return; 
            if (!config.BDayGift.BDayEnabled) return; 
            if (config.BDayGift.BDayEnabled && permission.GroupExists(config.BDayGift.BDayGroup)) 
            { 
                if (permission.UserHasGroup(player.userID.ToString(), config.BDayGift.BDayGroup)) return; 
                
                var bday = usersdata.VKUsersData[player.userID].Bdate; 
                
                if (bday == null || bday == "noinfo") return; 
                if (bday.Split('.').Length == 3) 
                    bday.Remove(bday.Length - 5, 5); 
                if (bday == DateTime.Now.ToString("d.M", CultureInfo.InvariantCulture)) 
                { 
                    permission.AddUserGroup(player.userID.ToString(), config.BDayGift.BDayGroup); 
                    PrintToChat(player, string.Format(GetMsg("ПоздравлениеИгрока"))); 
                    Log("bday", $"Игрок {player.displayName} добавлен в группу {config.BDayGift.BDayGroup}"); 
                    BDayPlayers.Add(player.userID); 
                    
                    if (config.BDayGift.BDayNotify) 
                        Server.Broadcast(string.Format(GetMsg("ДеньРожденияИгрока"), player.displayName)); 
                }
            } 
        } 
            
        private void OnPlayerDisconnected(BasePlayer player, string reason) 
        { 
            if (config.BDayGift.BDayEnabled && permission.GroupExists(config.BDayGift.BDayGroup)) 
            { 
                if (BDayPlayers.Contains(player.userID)) 
                {
                    permission.RemoveUserGroup(player.userID.ToString(), config.BDayGift.BDayGroup); 
                    BDayPlayers.Remove(player.userID); 
                    Log("bday", $"Игрок {player.displayName} удален из группы {config.BDayGift.BDayGroup}"); 
                } 
            } 
            if (OpenReportUI.Contains(player)) 
                OpenReportUI.Remove(player); 
            if (usersdata.VKUsersData.ContainsKey(player.userID)) 
            { 
                usersdata.VKUsersData[player.userID].LastSeen = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"); 
                VKBData.WriteObject(usersdata); 
            } 
        } 
        
        private void OnPlayerBanned(string name, ulong id, string address, string reason, string msg2 = null) 
        { 
            if (config.MltServSet.MSSEnable) 
                msg2 = $"[Сервер {config.MltServSet.ServerNumber.ToString()}] Игрок {name} ({id}) был забанен на сервере. Причина: {reason}. Ссылка на профиль стим: steamcommunity.com/profiles/{id}/"; 
            else 
                msg2 = $"Игрок {name} ({id}) был забанен на сервере. Причина: {reason}. Ссылка на профиль стим: steamcommunity.com/profiles/{id}/"; 
                
            if (config.AdmNotify.UserBannedTopic && config.AdmNotify.BannedTopicID != "null") 
                AddComentToBoard(config.AdmNotify.BannedTopicID, msg2); 
            if (config.AdmNotify.UserBannedMsg) 
            { 
                if (usersdata.VKUsersData.ContainsKey(id) && usersdata.VKUsersData[id].Confirmed) 
                    msg2 = msg2 + $" . Ссылка на профиль ВК: vk.com/id{usersdata.VKUsersData[id].VkID}"; 
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("bans")) 
                { 
                    SendChatMessage(config.ChNotify.ChatID, msg2); 
                    
                    if (config.ChNotify.AdmMsg) 
                        SendVkMessage(config.AdmNotify.VkID, msg2); 
                } 
                else 
                    SendVkMessage(config.AdmNotify.VkID, msg2); 
            } 
        } 
            
        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player) 
        { 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) statdata.Blueprints++; 
        } 
        
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item) 
        { 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) 
            { 
                if (item.info.shortname == "wood") 
                    statdata.WoodGath = statdata.WoodGath + item.amount; 
                    if (item.info.shortname == "sulfur.ore") 
                        statdata.SulfureGath = statdata.SulfureGath + item.amount; 
            } 
            if (config.TopWPlayersPromo.TopWPlEnabled) 
            { 
                BasePlayer player = entity.ToPlayer(); 
                if (player == null) return; 
                if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                    usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount; 
            } 
        } 
        
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) 
        { 
            if ((config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) && item.info.shortname == "sulfur.ore") 
                statdata.SulfureGath = statdata.SulfureGath + item.amount; 
            if (config.TopWPlayersPromo.TopWPlEnabled && usersdata.VKUsersData.ContainsKey(player.userID)) 
                usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount; 
        } 
        
        private void OnCollectiblePickup(Item item, BasePlayer player) 
        { 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) 
            { 
                if (item.info.shortname == "wood") 
                    statdata.WoodGath = statdata.WoodGath + item.amount; 
                if (item.info.shortname == "sulfur.ore") 
                    statdata.SulfureGath = statdata.SulfureGath + item.amount; 
            } 
            if (config.TopWPlayersPromo.TopWPlEnabled && usersdata.VKUsersData.ContainsKey(player.userID)) 
                usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount; 
        } 
        
        private void OnRocketLaunched(BasePlayer player, BaseEntity entity) 
        { 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) 
                statdata.Rockets++; 
        } 
        
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity) 
        { 
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable && ExplosiveList.Contains(entity.ShortPrefabName)) 
                statdata.Explosive++; 
        } 
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo) 
        { 
            if (config.TopWPlayersPromo.TopWPlEnabled) 
            { 
                if (entity.name.Contains("corpse")) return; 
                if (hitInfo == null) return; 
                
                var attacker = hitInfo.Initiator?.ToPlayer(); 
                if (attacker == null) return; 
                if (entity is BasePlayer) 
                    CheckDeath(entity.ToPlayer(), hitInfo, attacker); 
                if (entity is BaseEntity) 
                { 
                    if (hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Explosion && hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Heat && hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Bullet) return; 
                    if (attacker.userID == entity.OwnerID) return; BuildingBlock block = entity.GetComponent<BuildingBlock>(); if (block != null) { if (block.currentGrade.gradeBase.type.ToString() == "Twigs" || block.currentGrade.gradeBase.type.ToString() == "Wood") return; 
                } 
                else 
                { 
                    bool ok = false; 
                    foreach (var ent in allowedentity) 
                    { 
                        if (entity.LookupPrefab().name.Contains(ent)) ok = true; 
                    } 
                    if (!ok) return; 
                } 
                if (entity.OwnerID == 0) return; 
                if (usersdata.VKUsersData.ContainsKey(attacker.userID)) 
                    usersdata.VKUsersData[attacker.userID].Raids++; 
                } 
            } 
        } 
            
        private void CheckDeath(BasePlayer player, HitInfo info, BasePlayer attacker) 
        { 
            if (IsNPC(player)) return; 
            if (!usersdata.VKUsersData.ContainsKey(attacker.userID)) return; 
            if (!player.IsConnected) return; 
            if (Duel && (bool)Duel?.Call("IsDuelPlayer", player)) return; 
            
            usersdata.VKUsersData[attacker.userID].Kills++; 
        } 
        
        private void WipeFunctions() 
        { 
            if (config.StatusStg.UpdateStatus) 
            { 
                statdata.Blueprints = 0; 
                statdata.Rockets = 0; 
                statdata.SulfureGath = 0; 
                statdata.WoodGath = 0; 
                statdata.Explosive = 0; 
                StatData.WriteObject(statdata); 
                
                if (config.StatusStg.StatusSet == 1) Update1ServerStatus(); 
                if (config.StatusStg.StatusSet == 2) UpdateMultiServerStatus("status"); 
            } 
            if (config.WipeStg.WPostMsgAdmin) 
            { 
                string msg2 = "[VKBot] Сервер "; 
                if (config.MltServSet.MSSEnable) 
                    msg2 = msg2 + config.MltServSet.ServerNumber.ToString() + " "; 
                if (ConVar.Server.levelurl != string.Empty) 
                    msg2 = msg2 + $"вайпнут. Установлена карта: {ConVar.Server.levelurl}."; 
                else 
                    msg2 = msg2 + $"вайпнут. Установлена карта: {ConVar.Server.level}. Размер: {ConVar.Server.worldsize}. Сид: {ConVar.Server.seed}"; 
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("wipe")) 
                { 
                    SendChatMessage(config.ChNotify.ChatID, msg2); 
                    if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkOwnerID, msg2); 
                } 
                else 
                    SendVkMessage(config.AdmNotify.VkOwnerID, msg2); 
                } 
                if (config.WipeStg.WPostB) 
                { 
                    if (config.WipeStg.WPostAttB) 
                        SendVkWall($"{config.WipeStg.WPostMsg}&attachments={config.WipeStg.WPostAtt}"); 
                    else 
                        SendVkWall($"{config.WipeStg.WPostMsg}"); 
                } 
                if (config.GrGifts.GiftsWipe) 
                { 
                    if (usersdata.VKUsersData.Count != 0) 
                    { 
                        for (int i = 0; i < usersdata.VKUsersData.Count; i++) 
                        { 
                            usersdata.VKUsersData.ElementAt(i).Value.GiftRecived = false; 
                        } 
                        VKBData.WriteObject(usersdata); 
                    } 
                } 
                if (config.TopWPlayersPromo.TopWPlEnabled) 
                { 
                    if (config.TopWPlayersPromo.TopPlPost || config.TopWPlayersPromo.TopPlPromoGift) 
                    { 
                        SendPromoMsgsAndPost(); 
                        if (config.TopWPlayersPromo.GenRandomPromo) SetRandomPromo(); 
                    } 
                    if (usersdata.VKUsersData.Count != 0) 
                    { 
                        for (int i = 0; i < usersdata.VKUsersData.Count; i++) 
                        { 
                            usersdata.VKUsersData.ElementAt(i).Value.Farm = 0; 
                            usersdata.VKUsersData.ElementAt(i).Value.Kills = 0; 
                            usersdata.VKUsersData.ElementAt(i).Value.Raids = 0; 
                        } 
                        VKBData.WriteObject(usersdata); 
                    } 
                } 
                if (config.WipeStg.WMsgPlayers) WipeAlertsSend(); 
                if (config.AdmNotify.SendReports && config.AdmNotify.ReportsWipe) 
                { 
                    reportsdata.VKReportsData.Clear(); 
                    ReportsData.WriteObject(reportsdata); 
                    statdata.Reports = 0; StatData.WriteObject(statdata); 
                } 
                if (config.WipeStg.GrNameChange) 
                { 
                    string wipedate = WipeDate(); 
                    string text = config.WipeStg.GrName.Replace("{wipedate}", wipedate); 
                    webrequest.Enqueue("https://api.vk.com/method/groups.edit?group_id=" + config.VKAPIT.GroupID + "&title=" + text + "&" + apiver + "&access_token=" + config.VKAPIT.VKTokenApp, null, (code, response) => { 
                        var json = JObject.Parse(response); 
                        string Result = (string)json["response"]; 
                        if (Result == "1") 
                            PrintWarning($"Новое имя группы - {text}"); 
                        else 
                        { 
                            PrintWarning("Ошибка смены имени группы. Логи - /oxide/logs/VKBot/"); 
                            Log("Errors", $"group title not changed. Error: {response}"); 
                        } 
                    }, this); 
                } 
            } 
            
            private void WipeAlerts(ConsoleSystem.Arg arg) 
            { 
                if (arg.IsAdmin != true) return; 
                WipeAlertsSend(); 
            } 
            
            private void WipeAlertsSend() 
            { 
                List<string> UserList = new List<string>(); 
                string userlist = ""; 
                int usercount = 0; 
                if (usersdata.VKUsersData.Count != 0) 
                { 
                    for (int i = 0; i < usersdata.VKUsersData.Count; i++) 
                    { 
                        if (config.WipeStg.WCMDIgnore || usersdata.VKUsersData.ElementAt(i).Value.WipeMsg) 
                        { 
                            if (!ServerUsers.BanListString().Contains(usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString())) 
                            { 
                                if (usercount == 100) 
                                { 
                                    UserList.Add(userlist); userlist = ""; 
                                    usercount = 0; 
                                } 
                                if (usercount > 0) 
                                    userlist = userlist + ", "; 
                                userlist = userlist + usersdata.VKUsersData.ElementAt(i).Value.VkID; 
                                usercount++;
                            } 
                        }
                    } 
                } 
                if (userlist == "" && UserList.Count == 0) 
                { 
                    PrintWarning($"Список адресатов рассылки о вайпе пуст."); 
                    return; 
                } 
                if (UserList.Count > 0) 
                { 
                    foreach (var list in UserList) 
                        SendVkMessage(list, config.WipeStg.WMsgText); 
                } 

                SendVkMessage(userlist, config.WipeStg.WMsgText); 
            } 
                
            private void UStatus(ConsoleSystem.Arg arg) 
            { 
                if (arg.IsAdmin != true) return; 
                if (config.StatusStg.UpdateStatus) 
                { 
                    if (config.StatusStg.StatusSet == 1) 
                        Update1ServerStatus(); 
                    if (config.StatusStg.StatusSet == 2) 
                        UpdateMultiServerStatus("status"); 
                } 
                else 
                    PrintWarning($"Функция обновления статуса отключена."); 
            } 
                
            private void UpdateUsersData(ConsoleSystem.Arg arg) 
            { 
                if (arg.IsAdmin != true) return; 
                DeleteOldUsers(arg.Args?[0]); 
            } 
            
            private void UWidget(ConsoleSystem.Arg arg) 
            { 
                if (arg.IsAdmin != true) return; 
                if (config.GrWgSet.WgEnable) 
                { 
                    if (config.GrWgSet.WgToken == "none") 
                    { 
                        PrintWarning($"Ошибка! В файле конфигурации не указан ключ!"); 
                        return; 
                    } 
                    UpdateMultiServerStatus("widget"); 
                } 
                else 
                    PrintWarning($"Функция обновления статуса отключена."); 
            } 
            
            private string PrepareStatus(string input, string target) 
            { 
                string text = input; 
                string temp = ""; 
                temp = GetOnline(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("onlinecounter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{onlinecounter}")) 
                    text = text.Replace("{onlinecounter}", temp); 

                temp = BasePlayer.sleepingPlayerList.Count.ToString(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("sleepers")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{sleepers}")) 
                    text = text.Replace("{sleepers}", temp); 

                temp = statdata.WoodGath.ToString(); 

                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("woodcounter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{woodcounter}")) 
                    text = text.Replace("{woodcounter}", temp); 
                
                temp = statdata.SulfureGath.ToString(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("sulfurecounter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{sulfurecounter}")) 
                    text = text.Replace("{sulfurecounter}", temp); 
                
                temp = statdata.Rockets.ToString(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("rocketscounter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{rocketscounter}")) 
                    text = text.Replace("{rocketscounter}", temp); 
                    
                temp = statdata.Blueprints.ToString(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("blueprintsconter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{blueprintsconter}")) 
                    text = text.Replace("{blueprintsconter}", temp); 
                
                temp = statdata.Explosive.ToString(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("explosivecounter")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{explosivecounter}")) 
                    text = text.Replace("{explosivecounter}", temp); 
                    
                temp = WipeDate(); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("wipedate")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{wipedate}")) 
                    text = text.Replace("{wipedate}", temp); 
                    
                temp = config.StatusStg.Connecturl; 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("connect")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{connect}")) 
                    text = text.Replace("{connect}", temp); 
                    
                temp = config.StatusStg.StatusUT; 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("usertext")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{usertext}")) 
                    text = text.Replace("{usertext}", temp); 
                    
                temp = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture); 
                
                if (target == "status" && config.StatusStg.EmojiCounterList.Contains("updatetime")) 
                    temp = EmojiCounters(temp); 
                if (input.Contains("{updatetime}")) 
                    text = text.Replace("{updatetime}", temp); 

                return text; 
            } 
            
            private void SendReport(BasePlayer player, string cmd, string[] args) 
            { 
                if (config.CMDSet.ReportStatus == true) 
                {
                    if (config.AdmNotify.SendReports) 
                    { 
                        if (args.Length > 0) 
                            CreateReport(player, string.Join(" ", args.Skip(0).ToArray())); 
                        else 
                        { 
                            if (config.AdmNotify.GUIReports) 
                                ReportGUI(player); 
                            else 
                            { 
                                PrintToChat(player, string.Format(GetMsg("КомандаРепорт"), config.AdmNotify.ReportsNotify)); 
                                return; 
                            } 
                        } 
                    } 
                } 
                else 
                    PrintToChat(player, string.Format(GetMsg("ФункцияОтключена"))); 
            } 
            
            private void CheckReport(BasePlayer player, string[] text) 
            { 
                if (text != null && text.Count() < 2) return; 
                
                ulong uid; 
                
                if (ulong.TryParse(text[1], out uid)) 
                { 
                    var utarget = BasePlayer.FindByID(uid); 
                    if (utarget != null && text.Count() > 2) 
                        CreateReport(player, string.Join(" ", text.Skip(2).ToArray()), utarget); 
                    else 
                        CreateReport(player, string.Join(" ", text.Skip(1).ToArray())); 
                } 
                else 
                    CreateReport(player, string.Join(" ", text.Skip(1).ToArray())); 
            } 
            
            private void CreateReport(BasePlayer player, string text, BasePlayer target = null) 
            { 
                string reportplayer = ""; if (target != null) 
                    reportplayer = reportplayer + "Жалоба на игрока " + target.displayName + " (" + "steamcommunity.com/profiles/" + target.userID + "/) "; 
                string reporttext = "[VKBot]"; 
                statdata.Reports = statdata.Reports + 1; 
                int reportid = statdata.Reports; 
                StatData.WriteObject(statdata); 
                
                if (config.MltServSet.MSSEnable) 
                    reporttext = reporttext + " [Сервер " + config.MltServSet.ServerNumber.ToString() + "]"; 
                reporttext = reporttext + " " + player.displayName + " " + "(" + player.UserIDString + ")"; 
                
                if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                { 
                    if (usersdata.VKUsersData[player.userID].Confirmed) 
                        reporttext = reporttext + ". ВК: vk.com/id" + usersdata.VKUsersData[player.userID].VkID; 
                    else 
                        reporttext = reporttext + ". ВК: vk.com/id" + usersdata.VKUsersData[player.userID].VkID + " (не подтвержден)"; 
                } 
                reporttext = reporttext + " ID репорта: " + reportid; 
                reporttext = reporttext + reportplayer; 
                reporttext = reporttext + ". Сообщение: " + text; 
                
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("reports")) 
                { 
                    SendChatMessage(config.ChNotify.ChatID, reporttext); 
                    if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkID, reporttext); 
                } 
                else SendVkMessage(config.AdmNotify.VkID, reporttext); 
                reportsdata.VKReportsData.Add(reportid, new REPORT 
                { 
                    UserID = player.userID, 
                    Name = player.displayName, 
                    Text = reportplayer + text 
                }); 
                ReportsData.WriteObject(reportsdata); 
                Log("Log", $"{player.displayName} ({player.userID}): написал администратору: {reporttext}"); 
                PrintToChat(player, string.Format(GetMsg("РепортОтправлен"), config.AdmNotify.ReportsNotify)); 
            } 
            
            private void CheckVkUser(BasePlayer player, string url) 
            { 
                string Userid = null; 
                string[] arr1 = url.Split('/'); 
                string vkname = arr1[arr1.Length - 1]; 
                webrequest.Enqueue("https://api.vk.com/method/users.get?user_ids=" + vkname + "&" + apiver + "&fields=bdate&access_token=" + config.VKAPIT.VKToken, null, (code, response) => { 
                    if (!response.Contains("error")) 
                    { 
                        var json = JObject.Parse(response); 
                        Userid = (string)json["response"][0]["id"]; 
                        string bdate = (string)json["response"][0]["bdate"] ?? "noinfo"; 
                        if (Userid != null) 
                            AddVKUser(player, Userid, bdate); 
                        else 
                            PrintToChat(player, "Ошибка обработки вашей ссылки ВК, обратитесь к администратору."); 
                    } 
                    else 
                    { 
                        PrintWarning($"Ошибка проверки ВК профиля игрока {player.displayName} ({player.userID}). URL - {url}"); 
                        Log("checkresponce", $"Ошибка проверки ВК профиля игрока {player.displayName} ({player.userID}). URL - {url}. Ответ сервера ВК: {response}"); 
                    } 
                }, this); 
            } 
            
            private void AddVKUser(BasePlayer player, string Userid, string bdate) 
            { 
                if (!usersdata.VKUsersData.ContainsKey(player.userID)) 
                { 
                    usersdata.VKUsersData.Add(player.userID, new VKUDATA() 
                    { 
                        UserID = player.userID, 
                        Name = player.displayName, 
                        VkID = Userid, 
                        ConfirmCode = random.Next(1, 9999999), 
                        Confirmed = false, 
                        GiftRecived = false, 
                        Bdate = bdate, 
                        Farm = 0, 
                        Kills = 0, 
                        Raids = 0, 
                        LastSeen = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") 
                    }); 
                    VKBData.WriteObject(usersdata); 
                    SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /regvk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player); 
                } 
                else 
                { 
                    if (Userid == usersdata.VKUsersData[player.userID].VkID && usersdata.VKUsersData[player.userID].Confirmed) 
                    { 
                        PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден"))); 
                        return; 
                    } 
                    if (Userid == usersdata.VKUsersData[player.userID].VkID && !usersdata.VKUsersData[player.userID].Confirmed) 
                    { 
                        PrintToChat(player, string.Format(GetMsg("ПрофильДобавлен"))); 
                        return; 
                    } 
                    usersdata.VKUsersData[player.userID].Name = player.displayName; 
                    usersdata.VKUsersData[player.userID].VkID = Userid; 
                    usersdata.VKUsersData[player.userID].Confirmed = false; 
                    usersdata.VKUsersData[player.userID].ConfirmCode = random.Next(1, 9999999); 
                    usersdata.VKUsersData[player.userID].Bdate = bdate; 
                    VKBData.WriteObject(usersdata); 
                    SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /regvk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player); 
                } 
            } 
            
            private void VKcommand(BasePlayer player, string cmd, string[] args) 
            { 
                Effect Confirmed = new Effect("assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab", player, 0, new Vector3(), new Vector3()); 
                if (args.Length > 0) 
                { 
                    if (args[0] == "add") 
                    { 
                        if (args.Length == 1) 
                        { 
                            PrintToChat(player, string.Format(GetMsg("Подсказка"))); 
                            return; 
                        } 
                        if (!args[1].Contains("vk.com/")) 
                        { 
                            PrintToChat(player, string.Format(GetMsg("НеправильнаяСсылка"))); 
                            return; 
                        } 
                        CheckVkUser(player, args[1]); 
                    } 
                    if (args[0] == "confirm") 
                    { 
                        if (args.Length >= 2) 
                        { 
                            if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                            { 
                                if (usersdata.VKUsersData[player.userID].Confirmed) 
                                { 
                                    PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден"))); 
                                    return; 
                                } 
                                if (args[1] == usersdata.VKUsersData[player.userID].ConfirmCode.ToString()) 
                                { 
                                    usersdata.VKUsersData[player.userID].Confirmed = true; 
                                    VKBData.WriteObject(usersdata); 
                                    PrintToChat(player, string.Format(GetMsg("ПрофильПодтвержден"))); 
                                    EffectNetwork.Send(Confirmed, player.Connection); 
                                    if (config.GrGifts.VKGroupGifts) 
                                        PrintToChat(player, string.Format(GetMsg("ОповещениеОПодарках"), config.GrGifts.VKGroupUrl)); 
                                } 
                                else 
                                    PrintToChat(player, string.Format(GetMsg("НеверныйКод"))); 
                            } 
                            else 
                                PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен"))); 
                        } 
                        else 
                        { 
                            if (!usersdata.VKUsersData.ContainsKey(player.userID)) 
                            { 
                                PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен"))); 
                                return; 
                            } 
                            if (usersdata.VKUsersData[player.userID].Confirmed) 
                            { 
                                PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден"))); 
                                return; 
                            } 
                            SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /regvk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player); 
                        } 
                    } 
                    if (args[0] == "gift") FixedGifts(player); 
                    if (args[0] == "wipealerts") 
                        WAlert(player); 
                    if (args[0] != "add" && args[0] != "gift" && args[0] != "confirm") 
                    { 
                        PrintToChat(player, string.Format(GetMsg("ДоступныеКоманды"))); 
                        if (config.GrGifts.VKGroupGifts) 
                            PrintToChat(player, string.Format(GetMsg("ОповещениеОПодарках"), config.GrGifts.VKGroupUrl)); 
                    } 
                } 
                else 
                    PrintToChat(player, string.Format(GetMsg("ДоступныеКоманды"))); 
            } 
                
            private void WAlert(BasePlayer player) 
            { 
                if (!usersdata.VKUsersData.ContainsKey(player.userID)) 
                { 
                    PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен"))); 
                    return; 
                } 
                if (!usersdata.VKUsersData[player.userID].Confirmed) 
                { 
                    PrintToChat(player, string.Format(GetMsg("ПрофильНеПодтвержден"))); 
                    return; 
                } 
                if (config.WipeStg.WCMDIgnore) 
                { 
                    PrintToChat(player, string.Format(GetMsg("АвтоОповещенияОвайпе"))); 
                    return; 
                } 
                if (usersdata.VKUsersData[player.userID].WipeMsg) 
                { 
                    usersdata.VKUsersData[player.userID].WipeMsg = false; 
                    VKBData.WriteObject(usersdata); 
                    PrintToChat(player, string.Format(GetMsg("ПодпискаОтключена"))); 
                } 
                else 
                { 
                    usersdata.VKUsersData[player.userID].WipeMsg = true; 
                    VKBData.WriteObject(usersdata); 
                    PrintToChat(player, string.Format(GetMsg("ПодпискаВключена"))); 
                } 
            } 
            
            private void VKGift(BasePlayer player) 
            { 
                if (config.GrGifts.VKGroupGifts) 
                { 
                    if (!usersdata.VKUsersData.ContainsKey(player.userID)) 
                    { 
                        PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен"))); 
                        return; 
                    } 
                    if (!usersdata.VKUsersData[player.userID].Confirmed) 
                    { 
                        PrintToChat(player, string.Format(GetMsg("ПрофильНеПодтвержден"))); 
                        return; 
                    } 
                    if (usersdata.VKUsersData[player.userID].GiftRecived) 
                    { 
                        PrintToChat(player, string.Format(GetMsg("НаградаУжеПолучена"))); 
                        return; 
                    } 
                    webrequest.Enqueue($"https://api.vk.com/method/groups.isMember?group_id={config.VKAPIT.GroupID}&user_id={usersdata.VKUsersData[player.userID].VkID}&" + apiver + $"&access_token={config.VKAPIT.VKToken}", null, (code, response) => { 
                        if (response == null || !response.Contains("response")) return; 
                            var json = JObject.Parse(response); 
                            
                            if (json == null) return; 
                                string Result = (string)json["response"]; 
                                
                                if (Result == null) return; 
                                GetGift(code, Result, player); 
                            }, this); 
                    } 
                    else 
                        PrintToChat(player, string.Format(GetMsg("ФункцияОтключена"))); 
                } 
                    
                private void GetGift(int code, string Result, BasePlayer player) 
                { 
                    string msg2 = null; 
                    msg2 = $"[VKBot] Игрок {player.displayName} ({player.userID}) получил награду за подписку!"; 
                    Effect Gift = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, new Vector3(), new Vector3()); 
                    if (Result == "1") 
                    { 
                        if (config.GrGifts.VKGroupGiftCMD == "none") 
                        { 
                            if ((24 - player.inventory.containerMain.itemList.Count) >= statdata.Gifts.Count) 
                            { 
                                usersdata.VKUsersData[player.userID].GiftRecived = true; 
                                VKBData.WriteObject(usersdata); 
                                PrintToChat(player, string.Format(GetMsg("НаградаПолучена"))); 
                                
                                if (config.GrGifts.GiftsBool) 
                                    Server.Broadcast(string.Format(GetMsg("ПолучилНаграду"), player.displayName, config.GrGifts.VKGroupUrl)); 
                                
                                foreach (GiftItem gf in statdata.Gifts) 
                                { 
                                    Item gift = ItemManager.CreateByName(gf.shortname, gf.count, gf.skinid); 
                                    gift.MoveToContainer(player.inventory.containerMain, -1, false); 
                                } 
                                EffectNetwork.Send(Gift, player.Connection); 
                                SendVkMessage(config.AdmNotify.VkOwnerID, msg2); 
                            } 
                            else 
                                PrintToChat(player, string.Format(GetMsg("НетМеста"))); 
                        } 
                        else 
                        { 
                            string cmd = config.GrGifts.VKGroupGiftCMD.Replace("{steamid}", player.userID.ToString()); 
                            rust.RunServerCommand(cmd); 
                            usersdata.VKUsersData[player.userID].GiftRecived = true; 
                            VKBData.WriteObject(usersdata); 
                            PrintToChat(player, string.Format(GetMsg("НаградаПолученаКоманда"), config.GrGifts.GiftCMDdesc)); 
                            
                            if (config.GrGifts.GiftsBool) 
                                Server.Broadcast(string.Format(GetMsg("ПолучилНаграду"), player.displayName, config.GrGifts.VKGroupUrl)); 
                            
                            EffectNetwork.Send(Gift, player.Connection); 
                            SendVkMessage(config.AdmNotify.VkOwnerID, msg2); 
                        } 
                    } 
                    else 
                        PrintToChat(player, string.Format(GetMsg("НеВступилВГруппу"), config.GrGifts.VKGroupUrl)); 
                } 
                    
                private void GiftNotifier() 
                { 
                    if (config.GrGifts.VKGroupGifts) 
                    { 
                        foreach (var pl in BasePlayer.activePlayerList) 
                        { 
                            if (!usersdata.VKUsersData.ContainsKey(pl.userID)) 
                                PrintToChat(pl, string.Format(GetMsg("ОповещениеОПодарках"), config.GrGifts.VKGroupUrl)); 
                            else 
                            { 
                                if (!usersdata.VKUsersData[pl.userID].GiftRecived) PrintToChat(pl, string.Format(GetMsg("ОповещениеОПодарках"), config.GrGifts.VKGroupUrl)); 
                            } 
                        } 
                    } 
                } 
                
                void Update1ServerStatus() 
                { 
                    string status = PrepareStatus(config.StatusStg.StatusText, "status"); 
                    StatusCheck(status); 
                    SendVkStatus(status); 
                } 
                
                void UpdateMultiServerStatus(string target) 
                { 
                    string text = ""; 
                    string server1 = ""; 
                    string server2 = ""; 
                    string server3 = ""; 
                    string server4 = ""; 
                    string server5 = ""; 
                    
                    Dictionary<int, ServerInfo> SList = new Dictionary<int, ServerInfo>(); 
                    
                    if (config.MltServSet.Server1ip != "none") 
                    { 
                        var url = "http://" + config.MltServSet.Server1ip + "/status.json"; 
                        webrequest.Enqueue(url, null, (code, response) => { if (response != null || code == 200) 
                        { 
                            var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings); 
                            
                            if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return; 
                            if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return; 
                            string online = jsonresponse3["players"].ToString(); 
                            string slots = jsonresponse3["maxplayers"].ToString(); 
                            if (config.MltServSet.EmojiStatus && target == "status") 
                            { 
                                online = EmojiCounters(online); 
                                slots = EmojiCounters(slots); 
                            } 
                            string name = "1⃣: "; 
                            if (target == "widget") name = "1: "; 
                            if (config.MltServSet.Server1name != "none") 
                            { 
                                name = config.MltServSet.Server1name + " "; 
                                if (target == "widget") 
                                    name = config.MltServSet.Server1name; 
                                } 
                                server1 = name + online.ToString() + "/" + slots.ToString(); 
                                if (target == "widget") 
                                { 
                                    SList.Add(1, new ServerInfo() 
                                    { 
                                        name = name, 
                                        online = online, 
                                        slots = slots, 
                                        sleepers = jsonresponse3["sleepers"].ToString(), 
                                        map = jsonresponse3["level"].ToString() 
                                    }); 
                                } 
                            } 
                        }, this); 
                    } 
                    if (config.MltServSet.Server2ip != "none") 
                    { 
                        var url = "http://" + config.MltServSet.Server2ip + "/status.json"; webrequest.Enqueue(url, null, (code, response) => 
                        { 
                            if (response != null || code == 200) 
                            { 
                                var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings); 
                                if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return; 
                                if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return; 
                                string online = jsonresponse3["players"].ToString(); 
                                string slots = jsonresponse3["maxplayers"].ToString(); 
                                if (config.MltServSet.EmojiStatus && target == "status") 
                                { 
                                    online = EmojiCounters(online); 
                                    slots = EmojiCounters(slots); 
                                } 
                                string name = ", 2⃣: "; 
                                if (target == "widget") 
                                    name = "2:"; 
                                if (config.MltServSet.Server2name != "none") 
                                { 
                                    name = ", " + config.MltServSet.Server2name + " "; 
                                    if (target == "widget") 
                                        name = config.MltServSet.Server2name; 
                                } 
                                server2 = name + online.ToString() + "/" + slots.ToString(); 
                                if (target == "widget") 
                                { 
                                    SList.Add(2, new ServerInfo() 
                                    { 
                                        name = name, 
                                        online = online, 
                                        slots = slots, 
                                        sleepers = jsonresponse3["sleepers"].ToString(), 
                                        map = jsonresponse3["level"].ToString() 
                                    }); 
                                } 
                            } 
                        }, this); 
                    } 
                    if (config.MltServSet.Server3ip != "none") 
                    { 
                        var url = "http://" + config.MltServSet.Server3ip + "/status.json"; 
                        webrequest.Enqueue(url, null, (code, response) => 
                        { 
                            if (response != null || code == 200) 
                            { 
                                var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings); 
                                if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return; 
                                if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return; 
                                
                                string online = jsonresponse3["players"].ToString(); 
                                string slots = jsonresponse3["maxplayers"].ToString(); 
                                
                                if (config.MltServSet.EmojiStatus && target == "status") 
                                { 
                                    online = EmojiCounters(online); 
                                    slots = EmojiCounters(slots); 
                                } 
                                string name = ", 3⃣: "; 
                                if (target == "widget") name = "3:"; 
                                if (config.MltServSet.Server3name != "none") 
                                { 
                                    name = ", " + config.MltServSet.Server3name + " "; 
                                    
                                    if (target == "widget") 
                                        name = config.MltServSet.Server3name; 
                                } 
                                server3 = name + online.ToString() + "/" + slots.ToString(); 
                                if (target == "widget") 
                                { 
                                    SList.Add(3, new ServerInfo() 
                                    { 
                                        name = name, 
                                        online = online, 
                                        slots = slots, 
                                        sleepers = jsonresponse3["sleepers"].ToString(), 
                                        map = jsonresponse3["level"].ToString() 
                                    }); 
                                } 
                            } 
                        }, this); 
                    } 
                    if (config.MltServSet.Server4ip != "none") 
                    { 
                        var url = "http://" + config.MltServSet.Server4ip + "/status.json"; 
                        webrequest.Enqueue(url, null, (code, response) => 
                        { 
                            if (response != null || code == 200) 
                            { 
                                var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings); 
                                if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return; 
                                if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return; 
                                string online = jsonresponse3["players"].ToString(); 
                                string slots = jsonresponse3["maxplayers"].ToString(); 
                                if (config.MltServSet.EmojiStatus && target == "status") 
                                { 
                                    online = EmojiCounters(online); 
                                    slots = EmojiCounters(slots); 
                                } 
                                string name = ", 4⃣: "; 
                                if (target == "widget") 
                                    name = "4:"; 
                                if (config.MltServSet.Server4name != "none") 
                                { 
                                    name = ", " + config.MltServSet.Server4name + " "; 
                                    if (target == "widget") 
                                        name = config.MltServSet.Server4name; 
                                } 
                                server4 = name + online.ToString() + "/" + slots.ToString(); 
                                if (target == "widget") 
                                { 
                                    SList.Add(4, new ServerInfo() 
                                    { 
                                        name = name, 
                                        online = online, 
                                        slots = slots, 
                                        sleepers = jsonresponse3["sleepers"].ToString(), 
                                        map = jsonresponse3["level"].ToString() 
                                    }); 
                                } 
                            } 
                        }, this); 
                    } 
                    if (config.MltServSet.Server5ip != "none") 
                    { 
                        var url = "http://" + config.MltServSet.Server5ip + "/status.json"; 
                        webrequest.Enqueue(url, null, (code, response) => 
                        { 
                            if (response != null || code == 200) 
                            { 
                                var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings); 
                                if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return; 
                                if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return; 
                                string online = jsonresponse3["players"].ToString(); 
                                string slots = jsonresponse3["maxplayers"].ToString(); 
                                if (config.MltServSet.EmojiStatus && target == "status") 
                                { 
                                    online = EmojiCounters(online); 
                                    slots = EmojiCounters(slots); 
                                } 
                                string name = ", 5⃣: "; 
                                if (target == "widget") 
                                    name = "5:"; 
                                if (config.MltServSet.Server5name != "none") 
                                { 
                                    name = ", " + config.MltServSet.Server5name + " "; 
                                    if (target == "widget") 
                                        name = config.MltServSet.Server5name; 
                                } 
                                server5 = name + online.ToString() + "/" + slots.ToString(); 
                                if (target == "widget") 
                                { 
                                    SList.Add(5, new ServerInfo() 
                                    { 
                                        name = name, 
                                        online = online, 
                                        slots = slots, 
                                        sleepers = jsonresponse3["sleepers"].ToString(), 
                                        map = jsonresponse3["level"].ToString() 
                                    }); 
                                } 
                            } 
                        }, this); 
                    } 
                    
                    Puts("Обработка данных. Статус/обложка/виджет будет отправлен(а) через 10 секунд."); 
                    timer.Once(10f, () => { 
                        if (target == "widget") 
                        { 
                            PrepareWidgetCode(SList); 
                            return; 
                        } 
                        text = server1 + server2 + server3 + server4 + server5; 
                        if (text != "") 
                        { 
                            if (target == "status") 
                            { 
                                StatusCheck(text); SendVkStatus(text); 
                            } 
                            if (target == "label") 
                            { 
                                text = text.Replace("⃣", "%23"); 
                                UpdateLabelMultiServer(text); 
                            } 
                        } 
                        else 
                            PrintWarning("Текст для статуса/обложки пуст, не заполнен конфиг или не получены данные с Rust:IO"); 
                    }); 
                } 
                
                private void MsgAdmin(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (arg.Args == null) 
                    { 
                        PrintWarning($"Текст сообщения отсутсвует, правильная команда |sendmsgadmin сообщение|."); 
                        return; 
                    } 
                    string[] args = arg.Args; 
                    if (args.Length > 0) 
                    { 
                        string text = null; 
                        if (config.MltServSet.MSSEnable) 
                            text = $"[VKBot msgadmin] [Сервер {config.MltServSet.ServerNumber}] " + string.Join(" ", args.Skip(0).ToArray()); 
                        else 
                            text = $"[VKBot msgadmin] " + string.Join(" ", args.Skip(0).ToArray()); 
                            SendVkMessage(config.AdmNotify.VkID, text); 
                            Log("Log", $"|sendmsgadmin| Отправлено новое сообщение администратору: ({text})"); 
                    } 
                } 
                    
                private void ReportAnswer(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (arg.Args == null || arg.Args.Count() < 2) 
                    { 
                        PrintWarning($"Использование команды - reportanswer 'ID репорта' 'текст ответа'"); 
                        return; 
                    } 
                    if (reportsdata.VKReportsData.Count == 0) 
                    { 
                        PrintWarning($"База репортов пуста"); 
                        return; 
                    } 
                    int reportid = 0; 
                    reportid = Convert.ToInt32(arg.Args[0]); 
                    if (reportid == 0 || !reportsdata.VKReportsData.ContainsKey(reportid)) 
                    { 
                        PrintWarning($"Указан неверный ID репорта"); 
                        return; 
                    } 
                    string answer = string.Join(" ", arg.Args.Skip(1).ToArray()); 
                    if (usersdata.VKUsersData.ContainsKey(reportsdata.VKReportsData[reportid].UserID) && usersdata.VKUsersData[reportsdata.VKReportsData[reportid].UserID].Confirmed) 
                    { 
                        string msg = string.Format(GetMsg("ОтветНаРепортВК")) + answer; 
                        SendVkMessage(usersdata.VKUsersData[reportsdata.VKReportsData[reportid].UserID].VkID, msg); 
                        PrintWarning($"Ваш ответ был отправлен игроку в ВК."); reportsdata.VKReportsData.Remove(reportid); 
                        ReportsData.WriteObject(reportsdata); 
                    } 
                    else 
                    { 
                        BasePlayer reciver = BasePlayer.FindByID(reportsdata.VKReportsData[reportid].UserID); 
                        if (reciver != null) 
                        { 
                            PrintToChat(reciver, string.Format(GetMsg("ОтветНаРепортЧат")) + answer); 
                            PrintWarning($"Ваш ответ был отправлен игроку в игровой чат."); 
                            reportsdata.VKReportsData.Remove(reportid); 
                            ReportsData.WriteObject(reportsdata); 
                        } 
                        else 
                            PrintWarning($"Игрок отправивший репорт оффлайн. Невозможно отправить ответ."); 
                    } 
                } 
                
                private void ReportList(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (reportsdata.VKReportsData.Count == 0) 
                    { 
                        PrintWarning($"База репортов пуста"); 
                        return; 
                    } 
                    
                    foreach (var report in reportsdata.VKReportsData) 
                    { 
                        string status = "offline"; 
                        if (BasePlayer.FindByID(report.Value.UserID) != null) 
                            status = "online"; 
                        PrintWarning($"Репорт: ID {report.Key} от игрока {report.Value.Name} ({report.Value.UserID.ToString()}) ({status}). Текст: {report.Value.Text}"); 
                    } 
                } 
                
                private void ReportClear(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (reportsdata.VKReportsData.Count == 0) 
                    { 
                        PrintWarning($"База репортов пуста"); 
                        return; 
                    } 
                    reportsdata.VKReportsData.Clear(); 
                    ReportsData.WriteObject(reportsdata); 
                    statdata.Reports = 0; StatData.WriteObject(statdata); 
                    PrintWarning($"База репортов очищена"); 
                } 
                
                private void GetUserInfo(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (arg.Args == null) 
                    { 
                        PrintWarning($"Введите команду userinfo ник/steamid/vkid для получения информации о игроке из базы vkbot"); 
                        return; 
                    } 
                    string[] args = arg.Args; 
                    if (args.Length > 0) 
                    { 
                        bool returned = false; 
                        foreach (var pl in usersdata.VKUsersData) 
                        { 
                            if (pl.Value.Name.ToLower().Contains(args[0]) || pl.Value.UserID.ToString() == (args[0]) || pl.Value.VkID == (args[0])) 
                            { 
                                returned = true; 
                                string text = "Никнейм: " + pl.Value.Name + "\nSTEAM: steamcommunity.com/profiles/" + pl.Value.UserID + "/"; 
                                if (pl.Value.Confirmed) 
                                    text = text + "\nVK: vk.com/id" + pl.Value.VkID; 
                                else 
                                    text = text + "\nVK: vk.com/id" + pl.Value.VkID + " (не подтвержден)"; if (pl.Value.Bdate != null && pl.Value.Bdate != "noinfo") 
                                text = text + "\nДата рождения: " + pl.Value.Bdate; 
                                if (config.TopWPlayersPromo.TopWPlEnabled) 
                                    text = text + "\nРазрушено строений: " + pl.Value.Raids + "\nУбито игроков: " + pl.Value.Kills + "\nНафармил: " + pl.Value.Farm; Puts(text); 
                            } 
                        } 
                        if (!returned) 
                            Puts("Не найдено игроков с таким именем / steamid / vkid"); 
                    } 
                } 
                
                private void SendConfCode(string reciverID, string msg, BasePlayer player) => webrequest.Enqueue("https://api.vk.com/method/messages.send?user_ids=" + reciverID + "&message=" + msg + "&" + apiver + "&random_id=" + RandomId() + "&access_token=" + config.VKAPIT.VKToken, null, (code, response) => GetCallback(code, response, "Код подтверждения", player), this); 
                
                private void CheckPlugins() 
                { 
                    var loadedPlugins = plugins.GetAll().Where(pl => !pl.IsCorePlugin).ToArray(); 
                    var loadedPluginNames = new HashSet<string>(loadedPlugins.Select(pl => pl.Name)); 
                    var unloadedPluginErrors = new Dictionary<string, string>(); 
                    
                    foreach (var loader in Interface.Oxide.GetPluginLoaders()) 
                    { 
                        string msg; 
                        
                        foreach (var name in loader.ScanDirectory(Interface.Oxide.PluginDirectory).Except(loadedPluginNames)) 
                        { 
                            unloadedPluginErrors[name] = loader.PluginErrors.TryGetValue(name, out msg) ? msg : "Unloaded"; 
                        } 
                    } 
                    if (unloadedPluginErrors.Count > 0) 
                    { 
                        string text = null; 
                        if (config.MltServSet.MSSEnable) 
                            text = $"[VKBot] [Сервер {config.MltServSet.ServerNumber}] Произошла ошибка загрузки следующих плагинов:"; 
                        else 
                            text = $"[VKBot]  Произошла ошибка загрузки следующих плагинов:"; 
                        
                        foreach (var pluginerror in unloadedPluginErrors) 
                            text = text + " " + pluginerror.Key + "."; 
                        if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("plugins")) 
                        { 
                            SendChatMessage(config.ChNotify.ChatID, text); 
                            if (config.ChNotify.AdmMsg) 
                                SendVkMessage(config.AdmNotify.VkOwnerID, text); 
                        } 
                        else 
                            SendVkMessage(config.AdmNotify.VkOwnerID, text); 
                    } 
                } 
                
                private void PrepareWidgetCode(Dictionary<int, ServerInfo> Slist) 
                { 
                    string code = @"return{""title"":""" + config.GrWgSet.WgTitle + @""",""head"":[{""text"":""Сервер""},{""text"":""Онлайн""},{""text"":""Спящие""},{""text"":""Слоты""},{""text"":""Карта""}],""body"":["; 
                    if (Slist.Count != 0) 
                    { 
                        foreach (var info in Slist) 
                            code = code + @"[{""text"":""" + info.Value.name + @"""},{""text"":""" + info.Value.online + @"""},{""text"":""" + info.Value.sleepers + @"""},{""text"":""" + info.Value.slots + @"""},{""text"":""" + info.Value.map + @"""}],"; 
                    } 
                    else 
                    { 
                        string map = ConVar.Server.level; 
                        if (ConVar.Server.levelurl != string.Empty) 
                            map = "Custom Map"; 
                            code = code + @"[{""text"":""" + ConVar.Server.hostname + @"""},{""text"":""" + BasePlayer.activePlayerList.Count.ToString() + @"""},{""text"":""" + BasePlayer.sleepingPlayerList.Count.ToString() + @"""},{""text"":""" + ConVar.Server.maxplayers.ToString() + @"""},{""text"":""" + map + @"""}],"; 
                    } 
                    code = code + @"],"; 
                    if (config.GrWgSet.URLTitle != "none") 
                        code = code + @"""more"":""" + config.GrWgSet.URLTitle + @""",""more_url"": """ + config.GrWgSet.URL + @""","; code = code + "};"; 
                    SendWidget(code); 
                } 
                
                private void SendWidget(string widget) => webrequest.Enqueue("https://api.vk.com/method/appWidgets.update?type=table" + "&code=" + URLEncode(widget) + "&"+apiver+"&access_token=" + config.GrWgSet.WgToken, null, (code, response) => GetCallback(code, response, "Виджет"), this); 
                
                private void SendChatMessage(string chatid, string msg) => webrequest.Enqueue("https://api.vk.com/method/messages.send?chat_id=" + chatid + "&message=" + URLEncode(msg) + "&"+apiver+ "&random_id=" + RandomId() +"&access_token=" + config.ChNotify.ChNotfToken, null, (code, response) => GetCallback(code, response, "Сообщение в беседу"), this); 
                
                private void SendVkMessage(string reciverID, string msg) => webrequest.Enqueue("https://api.vk.com/method/messages.send?user_ids=" + reciverID + "&message=" + URLEncode(msg) + "&"+apiver + "&random_id=" + RandomId() + "&access_token=" + config.VKAPIT.VKToken, null, (code, response) => GetCallback(code, response, "Сообщение"), this); 
                
                private void SendVkWall(string msg) => webrequest.Enqueue("https://api.vk.com/method/wall.post?owner_id=-" + config.VKAPIT.GroupID + "&message=" + URLEncode(msg) + "&from_group=1&"+apiver+"&access_token=" + config.VKAPIT.VKTokenApp, null, (code, response) => GetCallback(code, response, "Пост"), this); 
                
                private void SendVkStatus(string msg) => webrequest.Enqueue("https://api.vk.com/method/status.set?group_id=" + config.VKAPIT.GroupID + "&text=" + URLEncode(msg) + "&" + apiver + "&access_token=" + config.VKAPIT.VKTokenApp, null, (code, response) => GetCallback(code, response, "Статус"), this); 
                
                private void AddComentToBoard(string topicid, string msg) => webrequest.Enqueue("https://api.vk.com/method/board.createComment?group_id=" + config.VKAPIT.GroupID + "&topic_id=" + URLEncode(topicid) + "&from_group=1&message=" + msg + "&"+apiver+"&access_token=" + config.VKAPIT.VKTokenApp, null, (code, response) => GetCallback(code, response, "Комментарий в обсуждения"), this); 
                
                private string RandomId() => random.Next(Int32.MinValue, Int32.MaxValue).ToString(); 
                string GetUserVKId(ulong userid) 
                { 
                    if (!usersdata.VKUsersData.ContainsKey(userid) || !usersdata.VKUsersData[userid].Confirmed) return null; 
                    if (BannedUsers.Contains(userid.ToString())) return null; 
                    return usersdata.VKUsersData[userid].VkID; 
                } 
                string GetUserLastNotice(ulong userid) 
                { 
                    if (!usersdata.VKUsersData.ContainsKey(userid) || !usersdata.VKUsersData[userid].Confirmed) return null; 
                    return usersdata.VKUsersData[userid].LastRaidNotice; 
                } 
                string ModerVkID() => config.AdmNotify.VkID; 
                
                string AdminVkID() => config.AdmNotify.VkOwnerID; 
                
                private void VKAPIChatMsg(string text) 
                { 
                    if (config.ChNotify.ChNotfEnabled) 
                        SendChatMessage(config.ChNotify.ChatID, text); 
                    else 
                        PrintWarning($"Сообщение не отправлено в беседу. Данная функция отключена. Текст сообщения: {text}"); 
                } 
                
                private void VKAPISaveLastNotice(ulong userid, string lasttime) 
                { 
                    if (usersdata.VKUsersData.ContainsKey(userid)) 
                    { 
                        usersdata.VKUsersData[userid].LastRaidNotice = lasttime; 
                        VKBData.WriteObject(usersdata); 
                    } 
                } 
                
                private void VKAPIWall(string text, string attachments, bool atimg) 
                { 
                    if (atimg) 
                    { 
                        SendVkWall($"{text}&attachments={attachments}"); 
                        Log("vkbotapi", $"Отправлен новый пост на стену: ({text}&attachments={attachments})"); 
                    } 
                    else 
                    { 
                        SendVkWall($"{text}"); Log("vkbotapi", $"Отправлен новый пост на стену: ({text})"); 
                    } 
                } 
                
                private void VKAPIMsg(string text, string attachments, string reciverID, bool atimg) 
                { 
                    if (atimg) 
                    { 
                        SendVkMessage(reciverID, $"{text}&attachment={attachments}"); 
                        Log("vkbotapi", $"Отправлено новое сообщение пользователю {reciverID}: ({text}&attachments={attachments})"); 
                    } 
                    else 
                    { 
                        SendVkMessage(reciverID, $"{text}"); Log("vkbotapi", $"Отправлено новое сообщение пользователю {reciverID}: ({text})"); 
                    } 
                } 
                
                private void VKAPIStatus(string msg) 
                { 
                    StatusCheck(msg); 
                    SendVkStatus(msg); 
                    Log("vkbotapi", $"Отправлен новый статус: {msg}"); 
                } 
                
                void Log(string filename, string text) => LogToFile(filename, $"[{DateTime.Now}] {text}", this); 
                
                void GetCallback(int code, string response, string type, BasePlayer player = null) 
                { 
                    if (!response.Contains("error")) 
                    { 
                        Puts($"{type} отправлен(о): {response}"); 
                        if (type == "Код подтверждения" && player != null) 
                            StartCodeSendedGUI(player); 
                        PrintToChat(player, string.Format(GetMsg("СообщениеОтправлено"))); 
                    } 
                    else 
                    { 
                        if (type == "Код подтверждения") 
                        { 
                            if (response.Contains("Can't send messages for users without permission") && player != null) 
                            { 
                                StartVKBotHelpVKGUI(player); 
                                PrintToChat(player, string.Format(GetMsg("СообщениеНеОтправлено"))); 
                            } 
                            else 
                                Log("errorconfcode", $"Ошибка отправки кода подтверждения. Ответ сервера ВК: {response}"); 
                        } 
                        else 
                        { 
                            PrintWarning($"{type} не отправлен(о). Файлы лога: /oxide/logs/VKBot/"); 
                            Log("Errors", $"{type} не отправлен(о). Ошибка: " + response); 
                        } 
                    } 
                } 
                
                private string EmojiCounters(string counter) 
                { 
                    var chars = counter.ToCharArray(); 
                    List<object> digits = new List<object>() 
                    { 
                        "0", 
                        "1", 
                        "2", 
                        "3", 
                        "4", 
                        "5", 
                        "6", 
                        "7", 
                        "8", 
                        "9" 
                    }; 

                    string emoji = ""; 
                    
                    for (int ctr = 0; ctr < chars.Length; ctr++) 
                    { 
                        if (digits.Contains(chars[ctr].ToString())) 
                        { 
                            string replace = chars[ctr] + "⃣"; 
                            emoji = emoji + replace; 
                        } 
                        else 
                            emoji = emoji + chars[ctr]; 
                    } 

                    return emoji; 
                } 
                
                private string WipeDate() => SaveRestore.SaveCreatedTime.ToLocalTime().ToString("dd.MM"); 
                
                private string GetOnline() 
                { 
                    string onlinecounter = BasePlayer.activePlayerList.Count.ToString(); 
                    if (config.StatusStg.OnlWmaxslots) 
                        onlinecounter = onlinecounter + "/" + ConVar.Server.maxplayers.ToString(); 

                    return onlinecounter; 
                } 
                
                private string URLEncode(string input) 
                { 
                    if (input.Contains("#")) 
                        input = input.Replace("#", "%23"); 
                    if (input.Contains("$")) 
                        input = input.Replace("$", "%24"); 
                    if (input.Contains("+")) 
                        input = input.Replace("+", "%2B"); 
                    if (input.Contains("/")) 
                        input = input.Replace("/", "%2F"); 
                    if (input.Contains(":")) 
                        input = input.Replace(":", "%3A"); 
                    if (input.Contains(";")) 
                        input = input.Replace(";", "%3B"); 
                    if (input.Contains("?")) 
                        input = input.Replace("?", "%3F"); 
                    if (input.Contains("@")) 
                        input = input.Replace("@", "%40"); 
                    return input; 
                } 
                
                private void StatusCheck(string msg) 
                { 
                    if (msg.Length > 140) 
                        PrintWarning($"Текст статуса слишком длинный. Измените формат статуса чтобы текст отобразился полностью. Лимит символов в статусе - 140. Длина текста - {msg.Length.ToString()}"); 
                } 
                
                private bool IsNPC(BasePlayer player) 
                { 
                    if (player is NPCPlayer) return true; 
                    if (!(player.userID >= 76560000000000000L || player.userID <= 0L)) return true; 
                    return false; 
                } 
                
                private void CheckAdminID() 
                { 
                    if (config.AdmNotify.VkID.Contains("/")) 
                    { 
                        string id = config.AdmNotify.VkID.Trim(new char[] { '/' }); 
                        config.AdmNotify.VkID = id; 
                        Config.WriteObject(config, true); 
                        PrintWarning("VK ID администратора исправлен"); 
                    } 
                } 
                
                private static string GetColor(string hex) 
                { 
                    if (string.IsNullOrEmpty(hex)) 
                        hex = "#FFFFFFFF"; var str = hex.Trim('#'); 
                    if (str.Length == 6) 
                        str += "FF"; 
                    if (str.Length != 8) 
                    { 
                        throw new Exception(hex); 
                        throw new InvalidOperationException("Cannot convert a wrong format."); 
                    } 
                    var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber); 
                    var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber); 
                    var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber); 
                    var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber); 
                    Color color = new Color32(r, g, b, a); 
                    return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}"; 
                } 
                
                private void DeleteOldUsers(string days = null) 
                { 
                    int ddays = 30; 
                    int t; 
                    if (days != null && Int32.TryParse(days, out t)) 
                        ddays = t; 
                    int deleted = 0; 
                    List<ulong> ForDelete = new List<ulong>(); 
                        
                    foreach (var user in usersdata.VKUsersData) 
                    { 
                        if (user.Value.LastSeen == null) 
                            ForDelete.Add(user.Key); 
                        else 
                        { 
                            DateTime LNT; 
                            if (DateTime.TryParseExact(user.Value.LastSeen, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out LNT) && DateTime.Now.Subtract(LNT).Days >= ddays)        ForDelete.Add(user.Key); 
                        } 
                    } 
                    foreach (var d in ForDelete) 
                    { 
                        usersdata.VKUsersData.Remove(d); deleted++;
                    }
                    if (deleted > 0) 
                    { 
                        PrintWarning($"Удалено устаревших профилей игроков из базы VKBot: {deleted}"); 
                        VKBData.WriteObject(usersdata); 
                    } 
                    else 
                        PrintWarning($"Нет профилей для удаления."); 
                } 
                    
                private void CheckOxideUpdate() 
                { 
                    string currentver = Manager.GetPlugin("RustCore").Version.ToString(); 
                    webrequest.Enqueue("https://umod.org/games/rust.json", null, (code, response) => 
                    { 
                        if (code == 200 || response != null) 
                        { 
                            var json = JObject.Parse(response); 
                            if (json == null) return; 
                            string latestver = (string)json["latest_release_version"]; 
                            if (latestver == null) return; 
                            if (latestver != currentver && !OxideUpdateSended) 
                            { 
                                SendVkMessage(config.AdmNotify.VkOwnerID, $"Доступно новое обновление Oxide {latestver}. https://umod.org/games/rust"); 
                                OxideUpdateSended = true; 
                            } 
                        } 
                    }, this); 
                    timer.Once(3600f, () => { 
                        CheckOxideUpdate(); 
                    }); 
                } 
                
                private string BannedUsers = ServerUsers.BanListString(); 
                
                private ulong GetTopRaider() 
                { 
                    int max = 0; 
                    ulong TopID = 0; 
                    int amount = usersdata.VKUsersData.Count; 
                    
                    if (amount != 0) 
                    { 
                        foreach (var pl in usersdata.VKUsersData) 
                        { 
                            if (pl.Value.Confirmed && pl.Value.Raids > max && !BannedUsers.Contains(pl.Key.ToString())) 
                            { 
                                max = pl.Value.Raids; 
                                TopID = pl.Key; 
                            } 
                        }
                    } 
                    if (max != 0) 
                        return TopID; 
                    else 
                        return 0; 
                } 
                
                private ulong GetTopKiller() 
                { 
                    int max = 0; 
                    ulong TopID = 0; 
                    int amount = usersdata.VKUsersData.Count; 
                    
                    if (amount != 0) 
                    { 
                        foreach (var pl in usersdata.VKUsersData) 
                        { 
                            if (pl.Value.Confirmed && pl.Value.Kills > max && !BannedUsers.Contains(pl.Key.ToString())) 
                            { 
                                max = pl.Value.Kills; 
                                TopID = pl.Key; 
                            } 
                        } 
                    } 
                    if (max != 0) 
                        return TopID; 
                    else 
                        return 0; 
                } 
                
                private ulong GetTopFarmer() 
                { 
                    int max = 0; 
                    ulong TopID = 0; 
                    int amount = usersdata.VKUsersData.Count; 
                    
                    if (amount != 0) 
                    { 
                        foreach (var pl in usersdata.VKUsersData) 
                        { 
                            if (pl.Value.Confirmed && pl.Value.Farm > max && !BannedUsers.Contains(pl.Key.ToString())) 
                            { 
                                max = pl.Value.Farm; 
                                TopID = pl.Key; 
                            } 
                        } 
                    } 
                    if (max != 0) 
                        return TopID; 
                    else 
                        return 0; 
                } 
                
                private void SendPromoMsgsAndPost() 
                { 
                    var traider = GetTopRaider(); 
                    var tkiller = GetTopKiller(); 
                    var tfarmer = GetTopFarmer(); 
                    
                    if (config.TopWPlayersPromo.TopPlPost) 
                    { 
                        bool check = false; 
                        string text = "Топ игроки прошедшего вайпа:"; 
                        
                        if (traider != 0) 
                        { 
                            text = text + "\nТоп рэйдер: " + usersdata.VKUsersData[traider].Name; 
                            check = true; 
                        } 
                        if (tkiller != 0) 
                        { 
                            text = text + "\nТоп киллер: " + usersdata.VKUsersData[tkiller].Name; 
                            check = true; 
                        } 
                        if (tfarmer != 0) 
                        { 
                            text = text + "\nТоп фармер: " + usersdata.VKUsersData[tfarmer].Name; 
                            check = true; 
                        } 
                        if (config.TopWPlayersPromo.TopPlPromoGift) 
                            text = text + "\nТоп игроки получают в качестве награды промокод на баланс в магазине."; 
                        if (check) 
                        { 
                            if (config.TopWPlayersPromo.TopPlPostAtt != "none") 
                                text = text + "&attachments=" + config.TopWPlayersPromo.TopPlPostAtt; SendVkWall(text); 
                        } 
                    } 
                    if (traider != 0 && config.TopWPlayersPromo.TopPlPromoGift) 
                    { 
                        string text = string.Format(GetMsg("СообщениеИгрокуТопПромо"), "рейдер", config.TopWPlayersPromo.TopRaiderPromo, config.TopWPlayersPromo.StoreUrl); 
                        if (config.TopWPlayersPromo.TopRaiderPromoAtt != "none") 
                            text = text + "&attachments=" + config.TopWPlayersPromo.TopRaiderPromoAtt; 
                        SendVkMessage(usersdata.VKUsersData[traider].VkID, text); 
                    } 
                    if (tkiller != 0 && config.TopWPlayersPromo.TopPlPromoGift) 
                    { 
                        string text = string.Format(GetMsg("СообщениеИгрокуТопПромо"), "киллер", config.TopWPlayersPromo.TopKillerPromo, config.TopWPlayersPromo.StoreUrl); 
                        if (config.TopWPlayersPromo.TopKillerPromoAtt != "none") 
                            text = text + "&attachments=" + config.TopWPlayersPromo.TopKillerPromoAtt; 
                        SendVkMessage(usersdata.VKUsersData[tkiller].VkID, text); 
                    } 
                    if (tfarmer != 0 && config.TopWPlayersPromo.TopPlPromoGift) 
                    { 
                        string text = string.Format(GetMsg("СообщениеИгрокуТопПромо"), "фармер", config.TopWPlayersPromo.TopFarmerPromo, config.TopWPlayersPromo.StoreUrl); 
                        if (config.TopWPlayersPromo.TopFarmerPromoAtt != "none") 
                            text = text + "&attachments=" + config.TopWPlayersPromo.TopFarmerPromoAtt; 
                        SendVkMessage(usersdata.VKUsersData[tfarmer].VkID, text); 
                    } 
                } 
                
                private string PromoGenerator() 
                { 
                    List<string> Chars = new List<string>() 
                    { 
                        "A", 
                        "1", 
                        "B", 
                        "2", 
                        "C",
                        "3", 
                        "D", 
                        "4", 
                        "F", 
                        "5", 
                        "G", 
                        "6", 
                        "H", 
                        "7", 
                        "I", 
                        "8", 
                        "J", 
                        "9", 
                        "K", 
                        "0",
                        "L", 
                        "M", 
                        "N", 
                        "O", 
                        "P", 
                        "Q", 
                        "R", 
                        "S", 
                        "T", 
                        "U", 
                        "V", 
                        "W", 
                        "X", 
                        "Y", 
                        "Z" 
                    }; 
                    
                    string promo = ""; 
                    
                    for (int i = 0; i < 6; i++) 
                    { 
                        promo = promo + Chars.GetRandom(); 
                    } 
                    
                    return promo; 
                } 
                
                private void SetRandomPromo() 
                { 
                    config.TopWPlayersPromo.TopFarmerPromo = PromoGenerator(); 
                    config.TopWPlayersPromo.TopKillerPromo = PromoGenerator(); 
                    config.TopWPlayersPromo.TopRaiderPromo = PromoGenerator(); 
                    Config.WriteObject(config, true); string msg = "[VKBot]"; 
                    
                    if (config.MltServSet.MSSEnable) 
                        msg = msg + " [Сервер " + config.MltServSet.ServerNumber.ToString() + "]"; 
                    msg = msg + " В настройки добавлены новые промокоды: \nТоп рейдер - " + config.TopWPlayersPromo.TopRaiderPromo + "\nТоп киллер - " + config.TopWPlayersPromo.TopKillerPromo + "\nТоп фармер - " + config.TopWPlayersPromo.TopFarmerPromo; 
                    SendVkMessage(config.AdmNotify.VkID, msg); 
                } 
                
                private void UpdateVKLabel() 
                { 
                    string url = config.DGLSet.DLUrl + "?"; 
                    int count = 0; 
                    if (config.DGLSet.DLText1 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t1=" + PrepareStatus(config.DGLSet.DLText1, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t1=" + PrepareStatus(config.DGLSet.DLText1, "label"); 
                    } 
                    if (config.DGLSet.DLText2 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t2=" + PrepareStatus(config.DGLSet.DLText2, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t2=" + PrepareStatus(config.DGLSet.DLText2, "label"); 
                    } 
                    if (config.DGLSet.DLText3 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t3=" + PrepareStatus(config.DGLSet.DLText3, "label"); 
                            count++; 
                        } 
                        else url = url + "&t3=" + PrepareStatus(config.DGLSet.DLText3, "label"); 
                    } 
                    if (config.DGLSet.DLText4 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t4=" + PrepareStatus(config.DGLSet.DLText4, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t4=" + PrepareStatus(config.DGLSet.DLText4, "label"); 
                    } 
                    if (config.DGLSet.DLText5 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t5=" + PrepareStatus(config.DGLSet.DLText5, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t5=" + PrepareStatus(config.DGLSet.DLText5, "label"); 
                    } 
                    if (config.DGLSet.DLText6 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t6=" + PrepareStatus(config.DGLSet.DLText6, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t6=" + PrepareStatus(config.DGLSet.DLText6, "label"); 
                    } 
                    if (config.DGLSet.DLText7 != "none") 
                    { 
                        if (count == 0) 
                        { 
                            url = url + "t7=" + PrepareStatus(config.DGLSet.DLText7, "label"); 
                            count++; 
                        } 
                        else 
                            url = url + "&t7=" + PrepareStatus(config.DGLSet.DLText7, "label"); 
                    } 
                    if (config.TopWPlayersPromo.TopWPlEnabled && config.DGLSet.TPLabel) 
                    { 
                        var tr = GetTopRaider(); 
                        var tk = GetTopKiller(); 
                        var tf = GetTopFarmer(); 
                        if (tf != 0) 
                            url = url + "&tfarmer=" + tf.ToString(); 
                        if (tk != 0) 
                            url = url + "&tkiller=" + tk.ToString(); 
                        if (tr != 0) 
                            url = url + "&traider=" + tr.ToString(); 
                    } 
                    webrequest.Enqueue(url, null, (code, response) => DLResult(code, response), this); 
                } 
                    
                private void DLResult(int code, string response) 
                { 
                    if (response.Contains("good")) 
                        Puts("Обложка группы обновлена"); 
                    else 
                        Puts("Прозошла ошибка обновления обложки, проверьте настройки."); 
                }
                
                private void ULabel(ConsoleSystem.Arg arg) 
                { 
                    if (arg.IsAdmin != true) return; 
                    if (config.DGLSet.DLEnable && config.DGLSet.DLUrl != "none") 
                    { 
                        if (config.DGLSet.DLMSEnable) 
                            UpdateMultiServerStatus("label"); 
                        else 
                            UpdateVKLabel(); 
                    } 
                    else 
                        PrintWarning($"Функция обновления обложки отключена, или не указана ссылка на скрипт обновления."); 
                } 
                
                private void UpdateLabelMultiServer(string text) => webrequest.Enqueue(config.DGLSet.DLUrl + "?t1=" + text, null, (code, response) => DLResult(code, response), this); 
                
                private CuiElement BPanel(string name, string color, string anMin, string anMax, string parent = "Hud", bool cursor = false, float fade = 1f) 
                { 
                    var Element = new CuiElement() 
                    { 
                        Name = name, 
                        Parent = parent, 
                        Components = { 
                            new CuiImageComponent { Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade, Color = color }, 
                            new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax }
                        }
                    }; 
                
                    if (cursor) 
                        Element.Components.Add(new CuiNeedsCursorComponent()); 
                    
                    return Element; 
                } 
                    
                private CuiElement Panel(string name, string color, string anMin, string anMax, string parent = "Hud", bool cursor = false, float fade = 1f) 
                { 
                    var Element = new CuiElement() 
                    { 
                        Name = name, 
                        Parent = parent, 
                        Components = { 
                            new CuiImageComponent { FadeIn = fade, Color = color }, 
                            new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax } 
                        } 
                    }; 
                    
                    if (cursor) 
                        Element.Components.Add(new CuiNeedsCursorComponent()); 
                    
                    return Element; 
                } 
                
                private CuiElement Text(string parent, string color, string text, TextAnchor pos, int fsize, string anMin = "0 0", string anMax = "1 1", string fname = "robotocondensed-bold.ttf", float fade = 3f) 
                { 
                    var Element = new CuiElement() 
                    { 
                        Parent = parent, 
                        Components = { 
                            new CuiTextComponent() { Color = color, Text = text, Align = pos, Font = fname, FontSize = fsize, FadeIn = fade }, 
                            new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax } 
                        } 
                    }; 
                    
                    return Element; 
                } 
                
                private CuiElement Button(string name, string parent, string command, string color, string anMin, string anMax, float fade = 3f) 
                { 
                    var Element = new CuiElement() 
                    { 
                        Name = name, 
                        Parent = parent, 
                        Components = { 
                            new CuiButtonComponent { Command = command, Color = color, FadeIn = fade}, 
                            new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax } 
                        } 
                    }; 
                    
                    return Element; 
                } 
                
                private CuiElement Image(string parent, string url, string anMin, string anMax, float fade = 3f, string color = "1 1 1 1") 
                { 
                    var Element = new CuiElement 
                    { 
                        Parent = parent, 
                        Components = { 
                            new CuiRawImageComponent { Color = color, Url = url, FadeIn = fade}, 
                            new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax } 
                        } 
                    }; 
                    
                    return Element; 
                } 
                
                private CuiElement Input(string name, string parent, int fsize, string command, string anMin = "0 0", string anMax = "1 1", TextAnchor pos = TextAnchor.MiddleCenter, int chlimit = 300, bool psvd = false, float fade = 3f) 
                { 
                    string text = ""; 
                    var Element = new CuiElement 
                    { 
                        Name = name, 
                        Parent = parent, 
                        Components = { 
                            new CuiInputFieldComponent { Align = pos, CharsLimit = chlimit, FontSize = fsize, Command = command + text, IsPassword = psvd, Text = text }, 
                            new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax } 
                        } 
                    }; 
                    
                    return Element; 
                } 
                
                private void UnloadAllGUI() 
                { 
                    foreach (var player in BasePlayer.activePlayerList) 
                    { 
                        CuiHelper.DestroyUi(player, VkWait); 
                        CuiHelper.DestroyUi(player, VkHelp); 
                        CuiHelper.DestroyUi(player, VkConnect); 
                        CuiHelper.DestroyUi(player, MainLayer); 
                        CuiHelper.DestroyUi(player, VkReward); 
                        CuiHelper.DestroyUi(player, VkAlert); 
                    } 
                } 
                
                private string UserName(string name) 
                { 
                    if (name.Length > 15) 
                        name = name.Remove(12) + "..."; 
                    
                    return name; 
                } 
                
                private void StartVKBotAddVKGUI(BasePlayer player) 
                { 
                    var container = new CuiElementContainer(); 
                    
                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" }
                    }, MainLayer, VkConnect); 

                    container.Add(new CuiElement
                    {
                        Parent = VkConnect,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "alertvkback") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Close = VkConnect, Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkConnect);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Привязка страницы вк", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkConnect); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0.57", AnchorMax = "0.9 0.9", OffsetMin = "0 0", OffsetMax = "1 1" }, 
                        Text = { Text = "Укажите ссылку на страницу ВК\nв поле ниже и нажмите ENTER", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkConnect); 
                    
                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.53", AnchorMax = "1 0.63", OffsetMin = "0 0", OffsetMax = "1 1" }, 
                        Text = { Text = "пример vk.com/nickname", Color = "1 1 1 0.3", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkConnect); 

                    container.Add(new CuiElement 
                    { 
                        Parent = VkConnect, 
                        Name = "Input", 
                        Components = { 
                            new CuiImageComponent { Color = "0 0 0 0" }, 
                            new CuiRectTransformComponent{ AnchorMin = "0.21 0.35", AnchorMax = "0.79 0.48", OffsetMin = "0 0", OffsetMax = "1 1" }, 
                            new CuiOutlineComponent { Color = "1 1 1 0.4", UseGraphicAlpha = true } 
                        } 
                    }); 
                    
                    container.Add(new CuiElement 
                    { 
                        Parent = "Input", 
                        Components = { 
                            new CuiInputFieldComponent { Text = "", FontSize = 12, Command = "vk.menugui46570981 addvkgui.addvk", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.4", CharsLimit = 34}, 
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "1 1" }, 
                            new CuiOutlineComponent { Color = "0.4 0.4 0.4 0.8", Distance = "0.4 -0.4", UseGraphicAlpha = true } 
                        } 
                    }); 

                    CuiHelper.AddUi(player, container); 
                } 
                
                public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin); 
                public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin); 
                public bool HasImage(string imageName) => (bool)ImageLibrary?.Call("HasImage", imageName); 
                public void SendImage(BasePlayer player, string imageName, ulong imageId = 0) => ImageLibrary?.Call("SendImage", player, imageName, imageId); 
                void OnPlayerConnected(BasePlayer player) 
                { 
                    SteamAvatarAdd(player.UserIDString); 
                } 
                void SteamAvatarAdd(string userid) 
                { 
                    if (ImageLibrary == null) return; 
                    if (HasImage(userid)) return; 
                    string url = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=AE4C104E68AB334B06F065DCD2B03014&" + "steamids=" + userid; 
                    webrequest.Enqueue(url, null, (code, response) => { 
                        if (code == 200) 
                        { 
                            string Avatar = (string)JObject.Parse(response)["response"]?["players"]?[0]?["avatarfull"]; 
                            AddImage(Avatar, userid); 
                        } 
                    }, this); 
                } 

                public string VkICO = "https://i.imgur.com/etwraYj.png"; 
                public string GiftICO = "https://i.imgur.com/GS6VKn4.png"; 
                public string AlertICO = "https://i.imgur.com/Cyh2d7y.png"; 
                public string VkConnect = "VkConnect_UI"; 
                public string VkHelp = "VkHelp_UI"; 
                public string VkWait = "VkWait_UI"; 
                public string MainLayer = "lay" + ".Main"; 
                public string VkReward = "VkReward_UI"; 
                public string VkAlert = "VkAlert_UI"; 
                public string AlertPermission = "VKBot.Alert"; 
                
                [ChatCommand("vk")]
                void StartVKBotMainGUI(BasePlayer player, ulong target = 0) 
                { 
                    string addvkbuttoncommand = "vk.menugui46570981 maingui.addvk"; 
                    string addvkbuttongift = "vk.menugui46570981 giftopen"; 
                    string addvkbuttonalert = "vk.menugui46570981 alert"; 
                    string addvkbuutontext = "ОТКРЫТЬ"; 
                    string addvkbuutontextgift = "ОТКРЫТЬ"; 
                    string addvkbuutontextalert = "ОТКРЫТЬ"; 
                    string giftvkbuutontext = "Получить награду за\nвступление в группу ВК"; 
                    string giftvkbuttoncommand = "vk.menugui46570981 maingui.gift"; 
                    string giftvkbuttoncolor = GetColor(config.GUISet.BMenuColor); 
                    string addvkbuttonanmax = "0.99 0.5"; 
                    string imagevk = "";
                    string imagegift = "";
                    string imagealert = "";
                    ulong Person = target == 0 ? player.userID : target; 
                    string ImageAvatar = GetImage(Person.ToString()); 
                    var container = new CuiElementContainer(); 
                    
                    if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                    { 
                        if (!usersdata.VKUsersData[player.userID].Confirmed) 
                        { 
                            addvkbuttoncommand = "vk.menugui46570981 maingui.confirm"; 
                        } 
                        else 
                        { 
                            addvkbuutontext = "ДОБАВЛЕНО"; 
                            addvkbuttoncommand = ""; 
                            imagevk = "ButtonBlock";
                            addvkbuttongift = "vk.menugui46570981 giftcomplete"; 
                        } 
                    } 
                    if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                    { 
                        if (usersdata.VKUsersData[player.userID].GiftRecived) 
                        { 
                            addvkbuttongift = ""; 
                            addvkbuutontextgift = "ДОБАВЛЕНО"; 
                            imagegift = "ButtonBlock";
                        } 
                    } 
                    if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                    { 
                        if (permission.UserHasPermission(player.UserIDString, AlertPermission) && usersdata.VKUsersData[player.userID].Confirmed) 
                        { 
                            addvkbuttonalert = ""; 
                            addvkbuutontextalert = "ДОБАВЛЕНО"; 
                            imagealert = "ButtonBlock";
                        } 
                    } 
                    
                    container.Add(new CuiElement
                    {
                        Name = MainLayer,
                        Parent = ".Mains",
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "BackgroundImage") },
                            new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                        Button = { Close = "Menu_UI", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, MainLayer);

                    container.Add(new CuiPanel() 
                    { 
                        RectTransform = {AnchorMin = "0.34 0.517", AnchorMax = "0.418 0.656", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" } 
                    }, MainLayer, "AVATAR"); 

                    container.Add(new CuiElement 
                    { 
                        Parent = "AVATAR", 
                        Components = { 
                            new CuiRawImageComponent { Png = ImageAvatar,Color = "1 1 1 1" }, 
                            new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0"}, 
                        } 
                    });

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.33 0.48", AnchorMax = "0.428 0.515", OffsetMax = "0 0"}, 
                        Text = { Text = player.displayName, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.6"} 
                    }, MainLayer); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = {AnchorMin = "0.45 0.6", AnchorMax = "0.73 0.675", OffsetMax = "0 0"}, 
                        Text = { Text = config.VKAPIT.txtvk, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "1 1 1 0.6"} 
                    }, MainLayer); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = {AnchorMin = "0.448 0.555", AnchorMax = "0.67 0.59", OffsetMax = "0 0"}, 
                        Text = { Text = "            Для привязки используйте ваш вк", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.6"} 
                    }, MainLayer); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = {AnchorMin = "0.448 0.51", AnchorMax = "0.67 0.545", OffsetMax = "0 0"}, 
                        Text = { Text = "            Вступите в группу вк и получайте награды", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.6"} 
                    }, MainLayer); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = {AnchorMin = "0.448 0.465", AnchorMax = "0.67 0.5", OffsetMax = "0 0"}, 
                        Text = { Text = "            Подключить оповещение о рейде", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.6"} 
                    }, MainLayer); 

                    container.Add(new CuiPanel() 
                    { 
                        RectTransform = {AnchorMin = "0.675 0.553", AnchorMax = "0.73 0.59", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" } 
                    }, MainLayer, "Vk"); 

                    if (imagevk != "")
                    {
                        container.Add(new CuiElement
                        {
                            Parent = "Vk",
                            Components = 
                            {
                                new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", imagevk) },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            }
                        });
                    }

                    container.Add(new CuiButton 
                    { 
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }, 
                        Button = { Command = addvkbuttoncommand, Color = "0 0 0 0" }, 
                        Text = { Text = addvkbuutontext, FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" } 
                    }, "Vk"); 

                    container.Add(new CuiPanel() 
                    { 
                        RectTransform = {AnchorMin = "0.675 0.51", AnchorMax = "0.73 0.547", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" } 
                    }, MainLayer, "gift"); 

                    if (imagegift != "")
                    {
                        container.Add(new CuiElement
                        {
                            Parent = "gift",
                            Components = 
                            {
                                new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", imagegift) },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            }
                        });
                    }

                    container.Add(new CuiButton 
                    { 
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }, 
                        Button = { Color = "0 0 0 0", Command = addvkbuttongift }, 
                        Text = { Text = addvkbuutontextgift, FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" } 
                    }, "gift"); 

                    container.Add(new CuiPanel() 
                    { 
                        RectTransform = {AnchorMin = "0.675 0.465", AnchorMax = "0.73 0.503", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" } 
                    }, MainLayer, "alert"); 

                    if (imagealert != "")
                    {
                        container.Add(new CuiElement
                        {
                            Parent = "alert",
                            Components = 
                            {
                                new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", imagealert) },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            }
                        });
                    }

                    container.Add(new CuiButton 
                    { 
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }, 
                        Button = { Color = "0 0 0 0", Command = addvkbuttonalert }, 
                        Text = { Text = addvkbuutontextalert, FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" } 
                    }, "alert"); 

                    string helpText = "Если у вас возникают проблемы с меню, вы можете использовать чатовые команды:\n<b>/regvk add</b> - привязка вашего профиля ВК\n<b>/regvk confirm</b> - подтверждение вашего профиля ВК\n<b>/regvk gift</b> - получение подарка за подписку ВК"; 
                    
                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.45 0.35", AnchorMax = "0.73 0.46", OffsetMax = "0 0" }, 
                        Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.67 0.63 0.596"} 
                    }, MainLayer); 

                    CuiHelper.DestroyUi(player, MainLayer); 
                    CuiHelper.AddUi(player, container); 
                } 
                
                private void StartVKBotHelpVKGUI(BasePlayer player) 
                {
                    CuiHelper.DestroyUi(player, VkConnect);
                    CuiHelper.DestroyUi(player, VkHelp); 
                    CuiElementContainer container = new CuiElementContainer(); 
                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" } 
                    }, MainLayer, VkHelp); 
                    
                    container.Add(new CuiElement
                    {
                        Parent = VkHelp,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "alerts1") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Close = VkHelp, Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkHelp);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Привязка страницы вк", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkHelp); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, 
                        Text = { Text = "Бот не может отправить вам сообщение :(\nОтправьте в сообщения группы любое слово и попробуйте снова", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkHelp); 
                    
                    CuiHelper.AddUi(player, container); 
                } 
                
                private void StartCodeSendedGUI(BasePlayer player) 
                { 
                    if (player == null || player.Connection == null) return; 
                    CuiHelper.DestroyUi(player, VkConnect); 
                    CuiHelper.DestroyUi(player, VkReward); 
                    CuiHelper.DestroyUi(player, VkHelp); 
                    CuiElementContainer container = new CuiElementContainer(); 

                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" }
                    }, MainLayer, VkWait); 

                    container.Add(new CuiElement
                    {
                        Parent = VkWait,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "alerts") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Command = "vk.refresh help", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkWait);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Привязка страницы вк", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkWait); 

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0.48", AnchorMax = "0.9 0.9" }, 
                        Text = { Text = "На вашу страницу ВК отправлено сообщение с дальнейшими инструкциями", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkWait); 

                    container.Add(new CuiButton 
                    { 
                        RectTransform = { AnchorMin = "0.315 0.35", AnchorMax = "0.68 0.49", }, 
                        Button = { Command = "vk.menugui46570981 maingui.removevk8974321765", Color = "0 0 0 0" }, 
                        Text = { Text = "     Удалить профиль", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkWait); 

                    CuiHelper.AddUi(player, container); 
                } 
                
                private void RewardVKBotGUI(BasePlayer player) 
                { 
                    var container = new CuiElementContainer(); 

                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" }
                    }, MainLayer, VkReward); 

                    container.Add(new CuiElement
                    {
                        Parent = VkReward,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "alerts") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Command = "vk.refresh gift", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkReward);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Получение подарка", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkReward); 
                    
                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, 
                        Text = { Text = "\nДля получения подарка привяжите вашу страницу, затем подпишитесь на нашу группу ВК", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkReward); 

                    CuiHelper.AddUi(player, container); 
                } 
                
                private void CompleteRewardVKBotGUI(BasePlayer player) 
                { 
                    var container = new CuiElementContainer(); 

                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" }
                    }, MainLayer, VkReward); 

                    container.Add(new CuiElement
                    {
                        Parent = VkReward,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "giftrewardback") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Command = "vk.refresh gift", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkReward);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Получение подарка", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkReward); 
                    
                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.9 0.9" }, 
                        Text = { Text = "Для получения подарка, нажмите кнопку получить. Убедитесь что подписались на нашу группу", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkReward); 

                    container.Add(new CuiButton 
                    { 
                        RectTransform = { AnchorMin = "0.315 0.35", AnchorMax = "0.68 0.49", }, 
                        Button = { Command = "vk.menugui46570981 maingui.gift", Color = "0 0 0 0" }, 
                        Text = { Text = "     Получить", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkReward); 
 
                    CuiHelper.AddUi(player, container); 
                } 
                
                private void AlertVKBotGUI(BasePlayer player) 
                { 
                    var container = new CuiElementContainer(); 
                    
                    container.Add(new CuiPanel() 
                    { 
                        CursorEnabled = true, 
                        RectTransform = {AnchorMin = "0.35 0.38", AnchorMax = "0.65 0.62", OffsetMin = "0 0", OffsetMax = "0 0"}, 
                        Image = {Color = "0 0 0 0" }
                    }, MainLayer, VkAlert); 

                    container.Add(new CuiElement
                    {
                        Parent = VkAlert,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "alerts") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.93 0.81", AnchorMax = "1 1" },
                        Button = { Command = "vk.refresh alert", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, VkAlert);

                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0 0.86", AnchorMax = "0.93 1" }, 
                        Text = { Text = "     Уведомление о рейде", FontSize = 12, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft } 
                    }, VkAlert); 
                    
                    container.Add(new CuiLabel 
                    { 
                        RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, 
                        Text = { Text = "\nПривяжите вашу страницу ВК и активируйте оповещение о рейде бесплатно", Color = "1 1 1 0.6", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter } 
                    }, VkAlert); 
                        
                    CuiHelper.AddUi(player, container); 
                } 
                    
                    [ConsoleCommand("vk.menugui46570981")] 
                    private void CmdChoose(ConsoleSystem.Arg arg) 
                    { 
                        BasePlayer player = arg.Player(); 
                        if (player == null) return; 
                        if (arg.Args == null) return; 
                        
                        switch (arg.Args[0]) 
                        { 
                            case "maingui.close": 
                                CuiHelper.DestroyUi(player, "MainUI"); 
                                break; 
                            case "maingui.addvk": 
                                CuiHelper.DestroyUi(player, "MainUI"); 
                                StartVKBotAddVKGUI(player); 
                                break; 
                            case "maingui.removevk8974321765": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkConnect); 
                                CuiHelper.DestroyUi(player, MainLayer); 
                                if (usersdata.VKUsersData.ContainsKey(player.userID)) 
                                { 
                                    usersdata.VKUsersData.Remove(player.userID); VKBData.WriteObject(usersdata); 
                                } 
                                StartVKBotMainGUI(player); 
                                break; 
                            case "maingui.walert": 
                                WAlert(player); 
                                break; 
                            case "maingui.gift": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkConnect); 
                                CuiHelper.DestroyUi(player, MainLayer); 
                                CuiHelper.DestroyUi(player, VkReward); 
                                CuiHelper.DestroyUi(player, "Menu_UI"); 
                                FixedGifts(player); 
                                break; 
                            case "maingui.confirm": 
                                CuiHelper.DestroyUi(player, "MainUI"); 
                                SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /regvk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player); 
                                break; 
                            case "maingui.wait": 
                                CuiHelper.DestroyUi(player, "MainUI"); 
                                StartCodeSendedGUI(player); 
                                break;
                            case "addvkgui.close": 
                                CuiHelper.DestroyUi(player, "AddVKUI"); 
                                break; 
                            case "addvkgui.addvk": 
                                string url = string.Join(" ", arg.Args.Skip(1).ToArray()); 
                                if (!url.Contains("vk.com/")) 
                                { 
                                    PrintToChat(player, string.Format(GetMsg("НеправильнаяСсылка"))); 
                                    return; 
                                } 
                                CuiHelper.DestroyUi(player, "AddVKUI"); 
                                CheckVkUser(player, url); 
                                break; 
                            case "helpgui.close": 
                                CuiHelper.DestroyUi(player, "HelpUI"); 
                                break; 
                            case "helpgui.confirm": 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /regvk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player); 
                                break; 
                            case "csendui.close": 
                                CuiHelper.DestroyUi(player, "CodeSendedUI"); 
                                break; 
                            case "giftopen": 
                                RewardVKBotGUI(player); 
                                break; 
                            case "giftcomplete": 
                                CompleteRewardVKBotGUI(player); 
                                break; 
                            case "alert": 
                                AlertVKBotGUI(player); 
                                break; 
                        } 
                    } 
                    
                    [ConsoleCommand("vk.refresh")] 
                    private void CmdRefresh(ConsoleSystem.Arg arg) 
                    { 
                        BasePlayer player = arg.Player(); 
                        if (player == null) return; 
                        if (arg.Args == null) return; 
                        
                        switch (arg.Args[0]) 
                        { 
                            case "connect": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkReward); 
                                CuiHelper.DestroyUi(player, VkAlert); 
                                StartVKBotMainGUI(player); 
                                break; 
                            case "help": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkConnect); 
                                CuiHelper.DestroyUi(player, MainLayer); 
                                CuiHelper.DestroyUi(player, VkReward); 
                                CuiHelper.DestroyUi(player, VkAlert); 
                                StartVKBotMainGUI(player); 
                                break; 
                            case "gift": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkConnect); 
                                CuiHelper.DestroyUi(player, MainLayer); 
                                CuiHelper.DestroyUi(player, VkReward); 
                                CuiHelper.DestroyUi(player, VkAlert); 
                                StartVKBotMainGUI(player); 
                                break; 
                            case "alert": 
                                CuiHelper.DestroyUi(player, VkWait); 
                                CuiHelper.DestroyUi(player, VkHelp); 
                                CuiHelper.DestroyUi(player, VkConnect); 
                                CuiHelper.DestroyUi(player, MainLayer); 
                                CuiHelper.DestroyUi(player, VkReward); 
                                CuiHelper.DestroyUi(player, VkAlert); 
                                StartVKBotMainGUI(player); 
                                break; 
                        } 
                    } 
                    
                    private void FixedGifts(BasePlayer player) 
                    { 
                        if (GiftsList.ContainsKey(player)) 
                        { 
                            TimeSpan interval = DateTime.Now - GiftsList[player]; 
                            if (interval.TotalSeconds < 15) 
                            { 
                                PrintToChat(player, "Вы отправляете запрос слишком часто, попробуйте немного позже"); 
                                return; 
                            } 
                            else 
                            { 
                                GiftsList[player] = DateTime.Now; VKGift(player); 
                            } 
                        } 
                        else 
                        { 
                            GiftsList.Add(player, DateTime.Now); VKGift(player); 
                        } 
                    } 
                    
                    private List<BasePlayer> OpenReportUI = new List<BasePlayer>(); 
                    
                    private void ReportGUI(BasePlayer player, BasePlayer target = null) 
                    { 
                        string chpl = "\nЕсли хотите отправить жалобу на игрока, сначала нажмите на кнопку <color=#ff0000>ВЫБРАТЬ ИГРОКА</color>"; 
                        
                        if (target != null) 
                            chpl = $"\nЖалоба на игрока <color=#ff0000>{target.displayName}</color>"; 
                        
                        string title = "<color=#ff0000>" + config.AdmNotify.ReportsNotify + "</color>" + chpl + "\nВведите ваше сообщение в поле ниже и нажмите <color=#ff0000>ENTER</color>"; 
                        
                        CuiElementContainer container = new CuiElementContainer 
                        { 
                            BPanel("ReportGUI", "0 0 0 0.75", "0.2 0.125", "0.8 0.9", "Hud", true), 
                            Panel("header", "0 0 0 0.75", "0 0.93", "1 1", "ReportGUI"), 
                            Text("header", "1 1 1 1", "Отправка сообщения администратору", TextAnchor.MiddleCenter, 20), 
                            Button("close", "header", "vk.report close", "1 0 0 1", "0.94 0.01", "1.0 0.98"), 
                            Text("close", "1 1 1 1", "X", TextAnchor.MiddleCenter, 20), 
                            Panel("text", "0 0 0 0.75", "0 0.77", "1 0.93", "ReportGUI"), 
                            Text("text", "1 1 1 1", title, TextAnchor.MiddleCenter, 18) 
                        }; 
                        if (target == null) 
                        { 
                            container.Add(Button("PlayerChoise", "ReportGUI", "vk.report choiceplayer", "0.7 1 0.6 0.4", "0.378 0.71", "0.628 0.76")); 
                            container.Add(Text("PlayerChoise", "1 1 1 1", "ВЫБРАТЬ ИГРОКА", TextAnchor.MiddleCenter, 18)); 
                        } 
                        container.Add(Panel("inputbg", "0 0.115 0 0.65", "0 0", "1 0.698", "ReportGUI"));
                        string command = "vk.report send "; 
                        if (target != null) 
                            command = command + target.userID + " "; 
                        container.Add(Input("reportinput", "inputbg", 18, command)); 
                        OpenReportUI.Add(player); CuiHelper.AddUi(player, container); 
                    } 
                    
                    [ConsoleCommand("vk.report")] 
                    private void ReportGUIChoose(ConsoleSystem.Arg arg) 
                    { 
                        BasePlayer player = arg.Player(); 
                        if (player == null) return; 
                        if (!config.AdmNotify.SendReports) 
                        { 
                            PrintToChat(player, string.Format(GetMsg("ФункцияОтключена"))); 
                            return; 
                        } 
                        if (arg.Args == null) return; 
                        
                        switch (arg.Args[0]) 
                        { 
                            case "close": 
                                if (OpenReportUI.Contains(player)) 
                                    OpenReportUI.Remove(player); 
                                CuiHelper.DestroyUi(player, "ReportGUI"); 
                                break; 
                            case "choiceplayer": 
                                if (OpenReportUI.Contains(player)) 
                                    OpenReportUI.Remove(player); 
                                CuiHelper.DestroyUi(player, "ReportGUI"); 
                                PListUI(player); 
                                break; 
                            case "send": 
                                if (OpenReportUI.Contains(player)) 
                                    OpenReportUI.Remove(player); 
                                CuiHelper.DestroyUi(player, "ReportGUI"); 
                                CheckReport(player, arg.Args); 
                                break; 
                        } 
                    } 
                    
                    private object OnServerCommand(ConsoleSystem.Arg arg) 
                    { 
                        BasePlayer player = arg.Player(); 
                        if (player == null || arg.cmd == null) return null; 
                        if (OpenReportUI.Contains(player) && !arg.cmd.FullName.ToLower().StartsWith("vk.report")) return true; 
                        
                        return null; 
                    } 
                    
                    private object OnPlayerCommand(ConsoleSystem.Arg arg) 
                    { 
                        var player = (BasePlayer)arg.Connection.player; 
                        if (player != null) 
                        { 
                            if (OpenReportUI.Contains(player) && !arg.cmd.FullName.ToLower().StartsWith("vk.report")) return true; 
                        } 
                        
                        return null; 
                    } 
                    
        [ConsoleCommand("vk.pllist")] 
        private void PListCMD(ConsoleSystem.Arg arg) 
        {
            BasePlayer player = arg.Player(); 
            if (player == null) return; 
            if (arg.Args == null) return; 
                        
            switch (arg.Args[0]) 
            { 
                case "close": 
                    CuiHelper.DestroyUi(player, "PListGUI"); 
                    break; 
                case "report": 
                    ulong uid = 0; 
                    if (arg.Args.Length > 1 && ulong.TryParse(arg.Args[1], out uid)) 
                    { 
                        var utarget = BasePlayer.FindByID(uid); 
                        if (utarget != null) 
                        { 
                            CuiHelper.DestroyUi(player, "PListGUI"); 
                            ReportGUI(player, utarget); 
                        } 
                        else 
                        { 
                            PrintToChat(player, string.Format(GetMsg("ИгрокНеНайден"))); 
                            return; 
                        } 
                    } 
                    else 
                    {
                        PrintToChat(player, string.Format(GetMsg("ИгрокНеНайден"))); 
                        return; 
                    } 
                    break; 
                case "page": 
                    int page; 
                    if (arg.Args.Length < 2) return; 
                    if (!Int32.TryParse(arg.Args[1], out page)) return; 
                    GUIManager.Get(player).Page = page; 
                    CuiHelper.DestroyUi(player, "PListGUI"); 
                    PListUI(player); 
                    break; 
            } 
        } 
                    
        private void PListUI(BasePlayer player) 
        { 
            string text = "Выберите игрока на которого хотите пожаловаться."; 
            List<BasePlayer> players = new List<BasePlayer>(); 
                        
            foreach (var pl in BasePlayer.activePlayerList) 
            { 
                if (pl == player) continue; 
                players.Add(pl); 
            } 
            if (players.Count == 0) 
            { 
                PrintToChat(player, "На сервере нет никого кроме вас."); 
                if (OpenReportUI.Contains(player)) 
                    OpenReportUI.Remove(player); return; 
            }
            players = players.OrderBy(x => x.displayName).ToList(); 
            int maxPages = CalculatePages(players.Count); string pageNum = (maxPages > 1) ? $" - {GUIManager.Get(player).Page}" : ""; 
                            
            CuiElementContainer container = new CuiElementContainer 
            { 
                BPanel("PListGUI", "0 0 0 0.75", "0.2 0.125", "0.8 0.9", "Hud", true), 
                Panel("header", "0 0 0 0.75", "0 0.93", "1 1", "PListGUI") 
            }; 
            if (maxPages != 1) 
                text = text + " Страница " + pageNum.ToString(); 
            container.Add(Text("header", "1 1 1 1", text, TextAnchor.MiddleCenter, 20)); 
            container.Add(Button("close", "header", "vk.pllist close", "1 0 0 1", "0.94 0.01", "1.0 0.98")); 
            container.Add(Text("close", "1 1 1 1", "X", TextAnchor.MiddleCenter, 20)); 
            container.Add(Panel("playerslist", "0 0 0 0.75", "0 0", "1 0.9", "PListGUI")); 
            var page = GUIManager.Get(player).Page; 
            int playerCount = (page * 100) - 100; 
                                
            for (int j = 0; j < 20; j++) 
            { 
                for (int i = 0; i < 5; i++) 
                { 
                    if (players.ToArray().Length <= playerCount) continue; 
                    string AnchorMin = (0.2f * i).ToString() + " " + (1f - (0.05f * j) - 0.05f).ToString(); 
                    string AnchorMax = ((0.2f * i) + 0.2f).ToString() + " " + (1f - (0.05f * j)).ToString(); 
                    string id = players.ToArray()[playerCount].UserIDString; 
                    container.Add(Panel($"pn{id}", "0 0 0 0", AnchorMin, AnchorMax, "playerslist")); 
                    container.Add(Button($"plbtn{id}", $"pn{id}", $"vk.pllist report {id}", "0 0 0 0.85", "0.05 0.05", "0.95 0.95")); 
                    container.Add(Text($"plbtn{id}", "1 1 1 1", UserName(players.ToArray()[playerCount].displayName), TextAnchor.MiddleCenter, 18)); 
                    playerCount++; 
                } 
            } 
            if (page < maxPages) 
            { 
                container.Add(Button("npg", "PListGUI", $"vk.pllist page {(page + 1).ToString()}", "0 0 0 0.75", "1.025 0.575", "1.1 0.675")); 
                container.Add(Text("npg", "1 1 1 1", ">>", TextAnchor.MiddleCenter, 16)); 
            } 
            if (page > 1) 
            { 
                container.Add(Button("ppg", "PListGUI", $"vk.pllist page {(page - 1).ToString()}", "0 0 0 0.75", "1.025 0.45", "1.1 0.55")); 
                container.Add(Text("ppg", "1 1 1 1", ">>", TextAnchor.MiddleCenter, 16)); 
            } 
            CuiHelper.AddUi(player, container); 
        } 
        int CalculatePages(int value) => (int)Math.Ceiling(value / 100d); 
                            
        class GUIManager 
        { 
            public static Dictionary<BasePlayer, GUIManager> Players = new Dictionary<BasePlayer, GUIManager>(); 
            public int Page = 1; 
            public static GUIManager Get(BasePlayer player) 
            { 
                if (Players.ContainsKey(player)) 
                    return Players[player]; 
                Players.Add(player, new GUIManager()); 
                    
                return Players[player]; 
            } 
        } 
                            
        private void LoadMessages() 
        { 
            lang.RegisterMessages(new Dictionary<string, string> 
            { 
                {"ПоздравлениеИгрока", "Администрация сервера поздравляет вас с Днем Рождения!"}, 
                {"ДеньРожденияИгрока", "Администрация сервера поздравляет игрока <color=#81BEF7>{0}</color> с Днем Рождения!"}, 
                {"РепортОтправлен", "Ваше сообщение было отправлено администратору"}, 
                {"КомандаРепорт", "Используйте:\n<color=#81BEF7>/report</color> сообщение"}, 
                {"ФункцияОтключена", "Данная функция отключена администратором"}, 
                {"ПрофильДобавленИПодтвержден", "Вы уже добавили и подтвердили свой профиль"}, 
                {"ПрофильДобавлен", "Вы уже добавили свой профиль. Если вам не пришел код подтверждения, введите команду <color=#81BEF7>/regvk confirm</color>"}, 
                {"ДоступныеКоманды", "<color=#F5DA81>ДОСТУПНЫЕ КОМАНДЫ:</color>\n/vk - открыть меню функций\n/regvk add - привязка вашего профиля ВК\n/regvk confirm - подтверждение вашего профиля ВК\n/regvk gift - получение подарка за подписку ВК"}, 
                {"НеправильнаяСсылка", "Ссылка на страницу должна быть вида \"vk.com/nickname\""}, 
                {"Подсказка", "Используйте:\n<color=#cfc580>/regvk add</color> ваша_ссылка\nСсылка на страницу должна быть вида \"vk.com/nickname\""}, 
                {"ПрофильПодтвержден", "Вы подтвердили свой профиль!"}, 
                {"ОповещениеОПодарках", "Вы можете получить награду, если вступили в нашу группу <color=#81BEF7>{0}</color>"}, 
                {"НеверныйКод", "Неверный код подтверждения"}, 
                {"ПрофильНеДобавлен", "Сначала добавьте и подтвердите свой профиль"}, 
                {"КодОтправлен", "Вам был отправлен код подтверждения. Если сообщение не пришло, зайдите в группу <color=#81BEF7>{0}</color> и напишите любое сообщение"}, {"ПрофильНеПодтвержден", "Сначала подтвердите свой профиль ВК"}, 
                {"НаградаУжеПолучена", "Вы уже получили свою награду!"}, 
                {"ПодпискаОтключена", "Вы <color=#ffd700>отключили</color> подписку на сообщения о вайпах сервера"}, 
                {"ПодпискаВключена", "Вы <color=#ffd700>включили</color> подписку на сообщения о вайпах сервера"}, 
                {"НаградаПолучена", "Вы получили свою награду!"}, 
                {"ПолучилНаграду", "Игрок <color=#81BEF7>{0}</color> получил награду за вступление в группу сервера!\n<size=12>Подробнее: <color=#ffd700>/menu</color></size>"}, {"НетМеста", "Недостаточно места для получения награды"}, 
                {"НаградаПолученаКоманда", "За вступление в группу нашего сервера вы получили {0}"}, 
                {"НеВступилВГруппу", "Вы не являетесь участником нашей группы!"},
                {"ОтветНаРепортЧат", "<color=#81BEF7>Администратор</color> ответил на ваше сообщение:\n"},
                {"ОтветНаРепортВК", "Администратор ответил на ваше сообщение:\n"},
                {"ИгрокНеНайден", "Игрок не найден"}, 
                {"СообщениеИгрокуТопПромо", "Поздравляем! Вы Топ {0} по результатам этого вайпа, в качестве награды, вы получаете промокод {1} на баланс в нашем магазине. {2}"}, 
                {"АвтоОповещенияОвайпе", "Сервер рассылает оповещения о вайпе всем, подписка не требуется"},
                {"СообщениеОтправлено", "На вашу страницу ВК отправлено сообщение с дальнейшими инструкциями"}, 
                {"СообщениеНеОтправлено", "Бот не может отправить вам сообщение :(\nОтправьте в сообщения группы любое слово и попробуйте снова"} 
            }, this); 
        } 
        string GetMsg(string key) => lang.GetMessage(key, this); 
    } 
}