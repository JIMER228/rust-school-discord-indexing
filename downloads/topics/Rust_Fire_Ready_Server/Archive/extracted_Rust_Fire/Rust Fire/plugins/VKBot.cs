using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VKBot", "SkiTles", "1.7.3")]
    class VKBot : RustPlugin
    {
        //Данный плагин принадлежит группе vk.com/vkbotrust
        //Данный плагин предоставляется в существующей форме,
        //"как есть", без каких бы то ни было явных или
        //подразумеваемых гарантий, разработчик не несет
        //ответственность в случае его неправильного использования.

        [PluginReference]
        Plugin Duel;

        #region Variables
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
        private List<ulong> BDayPlayers = new List<ulong>();
        class ServerInfo
        {
            public string name;
            public string online;
            public string slots;
            public string sleepers;
            public string map;
        }
        private Dictionary<BasePlayer, DateTime> GiftsList = new Dictionary<BasePlayer, DateTime>();
        #endregion

        #region Config
        private ConfigData config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Ключи VK API, ID группы")]
            public VKAPITokens VKAPIT { get; set; }
            [JsonProperty(PropertyName = "Настройки оповещений администраторов")]
            public AdminNotify AdmNotify { get; set; }
            [JsonProperty(PropertyName = "Настройки оповещений в беседу")]
            public ChatNotify ChNotify { get; set; }
            [JsonProperty(PropertyName = "Настройки статуса")]
            public StatusSettings StatusStg { get; set; }
            [JsonProperty(PropertyName = "Оповещения при вайпе")]
            public WipeSettings WipeStg { get; set; }
            [JsonProperty(PropertyName = "Награда за вступление в группу")]
            public GroupGifts GrGifts { get; set; }
            [JsonProperty(PropertyName = "Награда для именинников")]
            public BDayGiftSet BDayGift { get; set; }
            [JsonProperty(PropertyName = "Поддержка нескольких серверов")]
            public MultipleServersSettings MltServSet { get; set; }
            [JsonProperty(PropertyName = "Топ игроки вайпа и промо")]
            public TopWPlPromoSet TopWPlayersPromo { get; set; }
            [JsonProperty(PropertyName = "Настройки чат команд")]
            public CommandSettings CMDSet { get; set; }
            [JsonProperty(PropertyName = "Динамическая обложка группы")]
            public DynamicGroupLabelSettings DGLSet { get; set; }
            [JsonProperty(PropertyName = "Виджет сообщества")]
            public GroupWidgetSettings GrWgSet { get; set; }
            [JsonProperty(PropertyName = "Настройки GUI меню")]
            public GUISettings GUISet { get; set; }

            public class VKAPITokens
            {
                [JsonProperty(PropertyName = "VK Token группы (для сообщений)")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string VKToken { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "VK Token приложения (для записей на стене и статуса)")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string VKTokenApp { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "VKID группы")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string GroupID { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
            }
            public class AdminNotify
            {
                [JsonProperty(PropertyName = "VkID администраторов (пример /11111, 22222/)")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string VkID { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "Включить отправку сообщений администратору командой /report ?")]
                [DefaultValue(true)]
                public bool SendReports { get; set; } = true;
                [JsonProperty(PropertyName = "Включить GUI для команды /report ?")]
                [DefaultValue(false)]
                public bool GUIReports { get; set; } = false;
                [JsonProperty(PropertyName = "Очистка базы репортов при вайпе?")]
                [DefaultValue(true)]
                public bool ReportsWipe { get; set; } = true;
                [JsonProperty(PropertyName = "Предупреждение о злоупотреблении функцией репортов")]
                [DefaultValue("Наличие в тексте нецензурных выражений, оскорблений администрации или игроков сервера, а так же большое количество безсмысленных сообщений приведет к бану!")]
                public string ReportsNotify { get; set; } = "Наличие в тексте нецензурных выражений, оскорблений администрации или игроков сервера, а так же большое количество безсмысленных сообщений приведет к бану!";
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о бане игрока?")]
                [DefaultValue(true)]
                public bool UserBannedMsg { get; set; } = true;
                [JsonProperty(PropertyName = "Комментарий в обсуждения о бане игрока?")]
                [DefaultValue(false)]
                public bool UserBannedTopic { get; set; } = false;
                [JsonProperty(PropertyName = "ID обсуждения")]
                [DefaultValue("none")]
                public string BannedTopicID { get; set; } = "none";
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о нерабочих плагинах?")]
                [DefaultValue(true)]
                public bool PluginsCheckMsg { get; set; } = true;
            }
            public class ChatNotify
            {
                [JsonProperty(PropertyName = "VK Token приложения (лучше использовать отдельную страницу для получения токена)")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string ChNotfToken { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "ID беседы")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string ChatID { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "Включить отправку оповещений в беседу?")]
                [DefaultValue(false)]
                public bool ChNotfEnabled { get; set; } = false;
                [JsonProperty(PropertyName = "Дополнительная отправка оповещений в личку администраторам?")]
                [DefaultValue(false)]
                public bool AdmMsg { get; set; } = false;
                [JsonProperty(PropertyName = "Список оповещений отправляемых в беседу (доступно: reports, wipe, bans, plugins)")]
                [DefaultValue("reports, wipe, oxideupdate, bans")]
                public string ChNotfSet { get; set; } = "reports, wipe, bans, plugins";
            }
            public class StatusSettings
            {
                [JsonProperty(PropertyName = "Обновлять статус в группе? Если стоит /false/ статистика собираться не будет")]
                [DefaultValue(true)]
                public bool UpdateStatus { get; set; } = true;
                [JsonProperty(PropertyName = "Вид статуса (1 - текущий сервер, 2 - список серверов, необходим Rust:IO на каждом сервере)")]
                [DefaultValue(1)]
                public int StatusSet { get; set; } = 1;
                [JsonProperty(PropertyName = "Онлайн в статусе вида '125/200'")]
                [DefaultValue(false)]
                public bool OnlWmaxslots { get; set; } = false;
                [JsonProperty(PropertyName = "Таймер обновления статуса (минуты)")]
                [DefaultValue(30)]
                public int UpdateTimer { get; set; } = 30;
                [JsonProperty(PropertyName = "Формат статуса")]
                [DefaultValue("{usertext}. Сервер вайпнут: {wipedate}. Онлайн игроков: {onlinecounter}. Спящих: {sleepers}. Добыто дерева: {woodcounter}. Добыто серы: {sulfurecounter}. Выпущено ракет: {rocketscounter}. Время обновления: {updatetime}. Использовано взрывчатки: {explosivecounter}. Создано чертежей: {blueprintsconter}. {connect}")]
                public string StatusText { get; set; } = "{usertext}. Сервер вайпнут: {wipedate}. Онлайн игроков: {onlinecounter}. Спящих: {sleepers}. Добыто дерева: {woodcounter}. Добыто серы: {sulfurecounter}. Выпущено ракет: {rocketscounter}. Время обновления: {updatetime}. Использовано взрывчатки: {explosivecounter}. Создано чертежей: {blueprintsconter}. {connect}";
                [JsonProperty(PropertyName = "Список счетчиков, которые будут отображаться в виде emoji")]
                [DefaultValue("onlinecounter, rocketscounter, blueprintsconter, explosivecounter, wipedate")]
                public string EmojiCounterList { get; set; } = "onlinecounter, rocketscounter, blueprintsconter, explosivecounter, wipedate";
                [JsonProperty(PropertyName = "Ссылка на коннект сервера вида /connect 111.111.111.11:11111/")]
                [DefaultValue("connect 111.111.111.11:11111")]
                public string connecturl { get; set; } = "connect 111.111.111.11:11111";
                [JsonProperty(PropertyName = "Текст для статуса")]
                [DefaultValue("Сервер 1")]
                public string StatusUT { get; set; } = "Сервер 1";
            }
            public class WipeSettings
            {
                [JsonProperty(PropertyName = "Отправлять пост в группу после вайпа?")]
                [DefaultValue(false)]
                public bool WPostB { get; set; } = false;
                [JsonProperty(PropertyName = "Текст поста о вайпе")]
                [DefaultValue("Заполните эти поля, и выполните команду o.reload VKBot")]
                public string WPostMsg { get; set; } = "Заполните эти поля, и выполните команду o.reload VKBot";
                [JsonProperty(PropertyName = "Добавить изображение к посту о вайпе?")]
                [DefaultValue(false)]
                public bool WPostAttB { get; set; } = false;
                [JsonProperty(PropertyName = "Ссылка на изображение к посту о вайпе вида 'photo-1_265827614' (изображение должно быть в альбоме группы)")]
                [DefaultValue("photo-1_265827614")]
                public string WPostAtt { get; set; } = "photo-1_265827614";
                [JsonProperty(PropertyName = "Отправлять сообщение администратору о вайпе?")]
                [DefaultValue(true)]
                public bool WPostMsgAdmin { get; set; } = true;
                [JsonProperty(PropertyName = "Отправлять игрокам сообщение о вайпе автоматически?")]
                [DefaultValue(false)]
                public bool WMsgPlayers { get; set; } = false;
                [JsonProperty(PropertyName = "Текст сообщения игрокам о вайпе (сообщение отправляется только тем кто подписался командой /vk wipealerts)")]
                [DefaultValue("Сервер вайпнут! Залетай скорее!")]
                public string WMsgText { get; set; } = "Сервер вайпнут! Залетай скорее!";
                [JsonProperty(PropertyName = "Игнорировать команду /vk wipealerts? (если включено, сообщение о вайпе будет отправляться всем)")]
                [DefaultValue(false)]
                public bool WCMDIgnore { get; set; } = false;
                [JsonProperty(PropertyName = "Смена названия группы после вайпа")]
                [DefaultValue(false)]
                public bool GrNameChange { get; set; } = false;
                [JsonProperty(PropertyName = "Название группы (переменная {wipedate} отображает дату последнего вайпа)")]
                [DefaultValue("ServerName | WIPE {wipedate}")]
                public string GrName { get; set; } = "ServerName | WIPE {wipedate}";
            }
            public class GroupGifts
            {
                [JsonProperty(PropertyName = "Выдавать подарок игроку за вступление в группу ВК?")]
                [DefaultValue(true)]
                public bool VKGroupGifts { get; set; } = true;
                [JsonProperty(PropertyName = "Подарки за вступление в группу (shortname предмета, количество)")]
                [DefaultValue(null)]
                public Dictionary<string, object> VKGroupGiftList { get; set; } = new Dictionary<string, object>
                {
                  {"supply.signal", 1},
                  {"pookie.bear", 2}
                };
                [JsonProperty(PropertyName = "Подарок за вступление в группу (команда, если стоит none выдаются предметы из списка выше). Пример: grantperm {steamid} vkraidalert.allow 7d")]
                [DefaultValue("none")]
                public string VKGroupGiftCMD { get; set; } = "none";
                [JsonProperty(PropertyName = "Описание команды")]
                [DefaultValue("Оповещения о рейде на 7 дней")]
                public string GiftCMDdesc { get; set; } = "Оповещения о рейде на 7 дней";
                [JsonProperty(PropertyName = "Ссылка на группу ВК")]
                [DefaultValue("vk.com/1234")]
                public string VKGroupUrl { get; set; } = "vk.com/1234";
                [JsonProperty(PropertyName = "Оповещения в общий чат о получении награды")]
                [DefaultValue(true)]
                public bool GiftsBool { get; set; } = true;
                [JsonProperty(PropertyName = "Включить оповещения для игроков не получивших награду за вступление в группу?")]
                [DefaultValue(true)]
                public bool VKGGNotify { get; set; } = true;
                [JsonProperty(PropertyName = "Интервал оповещений для игроков не получивших награду за вступление в группу (в минутах)")]
                [DefaultValue(30)]
                public int VKGGTimer { get; set; } = 30;
                [JsonProperty(PropertyName = "Выдавать награду каждый вайп?")]
                [DefaultValue(true)]
                public bool GiftsWipe { get; set; } = true;
            }
            public class BDayGiftSet
            {
                [JsonProperty(PropertyName = "Включить награду для именинников?")]
                [DefaultValue(true)]
                public bool BDayEnabled { get; set; } = true;
                [JsonProperty(PropertyName = "Группа для именинников")]
                [DefaultValue("bdaygroup")]
                public string BDayGroup { get; set; } = "bdaygroup";
                [JsonProperty(PropertyName = "Оповещения в общий чат о имениннках")]
                [DefaultValue(false)]
                public bool BDayNotify { get; set; } = false;
            }
            public class MultipleServersSettings
            {
                [JsonProperty(PropertyName = "Включить поддержку несколько серверов?")]
                [DefaultValue(false)]
                public bool MSSEnable { get; set; } = false;
                [JsonProperty(PropertyName = "Номер сервера")]
                [DefaultValue(1)]
                public int ServerNumber { get; set; } = 1;
                [JsonProperty(PropertyName = "Сервер 1 IP:PORT (пример: 111.111.111.111:28015)")]
                [DefaultValue("none")]
                public string Server1ip { get; set; } = "none";
                [JsonProperty(PropertyName = "Название сервера 1 (если стоит none, используется номер)")]
                [DefaultValue("none")]
                public string Server1name { get; set; } = "none";
                [JsonProperty(PropertyName = "Сервер 2 IP:PORT (пример: 111.111.111.111:28015)")]
                [DefaultValue("none")]
                public string Server2ip { get; set; } = "none";
                [JsonProperty(PropertyName = "Название сервера 2 (если стоит none, используется номер)")]
                [DefaultValue("none")]
                public string Server2name { get; set; } = "none";
                [JsonProperty(PropertyName = "Сервер 3 IP:PORT (пример: 111.111.111.111:28015)")]
                [DefaultValue("none")]
                public string Server3ip { get; set; } = "none";
                [JsonProperty(PropertyName = "Название сервера 3 (если стоит none, используется номер)")]
                [DefaultValue("none")]
                public string Server3name { get; set; } = "none";
                [JsonProperty(PropertyName = "Сервер 4 IP:PORT (пример: 111.111.111.111:28015)")]
                [DefaultValue("none")]
                public string Server4ip { get; set; } = "none";
                [JsonProperty(PropertyName = "Название сервера 4 (если стоит none, используется номер)")]
                [DefaultValue("none")]
                public string Server4name { get; set; } = "none";
                [JsonProperty(PropertyName = "Сервер 5 IP:PORT (пример: 111.111.111.111:28015)")]
                [DefaultValue("none")]
                public string Server5ip { get; set; } = "none";
                [JsonProperty(PropertyName = "Название сервера 5 (если стоит none, используется номер)")]
                [DefaultValue("none")]
                public string Server5name { get; set; } = "none";
                [JsonProperty(PropertyName = "Онлайн в emoji?")]
                [DefaultValue(true)]
                public bool EmojiStatus { get; set; } = true;
            }
            public class TopWPlPromoSet
            {
                [JsonProperty(PropertyName = "Включить топ игроков вайпа")]
                [DefaultValue(true)]
                public bool TopWPlEnabled { get; set; } = true;
                [JsonProperty(PropertyName = "Включить отправку промо кодов за топ?")]
                [DefaultValue(false)]
                public bool TopPlPromoGift { get; set; } = false;
                [JsonProperty(PropertyName = "Пост на стене группы о топ игроках вайпа")]
                [DefaultValue(true)]
                public bool TopPlPost { get; set; } = true;
                [JsonProperty(PropertyName = "Ссылка на изображение к посту вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")]
                [DefaultValue("none")]
                public string TopPlPostAtt { get; set; } = "none";
                [JsonProperty(PropertyName = "Промо для топ рэйдера")]
                [DefaultValue("topraider")]
                public string TopRaiderPromo { get; set; } = "topraider";
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ рейдеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")]
                [DefaultValue("none")]
                public string TopRaiderPromoAtt { get; set; } = "none";
                [JsonProperty(PropertyName = "Промо для топ килера")]
                [DefaultValue("topkiller")]
                public string TopKillerPromo { get; set; } = "topkiller";
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ киллеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")]
                [DefaultValue("none")]
                public string TopKillerPromoAtt { get; set; } = "none";
                [JsonProperty(PropertyName = "Промо для топ фармера")]
                [DefaultValue("topfarmer")]
                public string TopFarmerPromo { get; set; } = "topfarmer";
                [JsonProperty(PropertyName = "Ссылка на изображение к сообщению топ фармеру вида 'photo-1_265827614' (изображение должно быть в альбоме группы), оставить 'none' если не нужно")]
                [DefaultValue("none")]
                public string TopFarmerPromoAtt { get; set; } = "none";
                [JsonProperty(PropertyName = "Ссылка на донат магазин")]
                [DefaultValue("server.gamestores.ru")]
                public string StoreUrl { get; set; } = "server.gamestores.ru";
                [JsonProperty(PropertyName = "Автоматическая генерация промокодов после вайпа")]
                [DefaultValue(false)]
                public bool GenRandomPromo { get; set; } = false;
            }
            public class CommandSettings
            {
                [JsonProperty(PropertyName = "Команда отправки сообщения администратору")]
                [DefaultValue("report")]
                public string CMDreport { get; set; } = "report";
            }
            public class DynamicGroupLabelSettings
            {
                [JsonProperty(PropertyName = "Включить динамическую обложку?")]
                [DefaultValue(false)]
                public bool DLEnable { get; set; } = false;
                [JsonProperty(PropertyName = "Ссылка на скрипт обновления")]
                [DefaultValue("none")]
                public string DLUrl { get; set; } = "none";
                [JsonProperty(PropertyName = "Таймер обновления (в минутах)")]
                [DefaultValue(10)]
                public int DLTimer { get; set; } = 10;
                [JsonProperty(PropertyName = "Обложка с онлайном нескольких серверов (все настройки ниже игнорируются)")]
                [DefaultValue(false)]
                public bool DLMSEnable { get; set; } = false;
                [JsonProperty(PropertyName = "Текст блока 1 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText1 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 2 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText2 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 3 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText3 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 4 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText4 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 5 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText5 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 6 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText6 { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст блока 7 (доступны все переменные как в статусе)")]
                [DefaultValue("none")]
                public string DLText7 { get; set; } = "none";
                [JsonProperty(PropertyName = "Включить вывод топ игроков на обложку?")]
                [DefaultValue(false)]
                public bool TPLabel { get; set; } = false;
            }
            public class GroupWidgetSettings
            {
                [JsonProperty(PropertyName = "Включить обновление виджета?")]
                [DefaultValue(false)]
                public bool WgEnable { get; set; } = false;
                [JsonProperty(PropertyName = "Таймер обновления (минуты)")]
                [DefaultValue(3)]
                public int UpdateTimer { get; set; } = 3;
                [JsonProperty(PropertyName = "Заголовок виджета")]
                [DefaultValue("Мониторинг серверов")]
                public string WgTitle { get; set; } = "Мониторинг серверов";
                [JsonProperty(PropertyName = "Ключ приложения для работы с виджетом (Инструкция - https://goo.gl/LpZujf)")]
                [DefaultValue("none")]
                public string WgToken { get; set; } = "none";
                [JsonProperty(PropertyName = "Текст дополнительной ссылки (если стоит none, не используется)")]
                [DefaultValue("none")]
                public string URLTitle { get; set; } = "none";
                [JsonProperty(PropertyName = "Дополнительная ссылка (разрешены только vk.com ссылки)")]
                [DefaultValue("none")]
                public string URL { get; set; } = "none";
            }
            public class GUISettings
            {
                [JsonProperty(PropertyName = "Ссылка на логотип сервера")]
                [DefaultValue("https://i.imgur.com/QNZykaS.png")]
                public string Logo { get; set; } = "https://i.imgur.com/QNZykaS.png";
                [JsonProperty(PropertyName = "Позиция GUI AnchorMin (дефолт 0.347 0.218)")]
                [DefaultValue("0.347 0.218")]
                public string AnchorMin { get; set; } = "0.347 0.218";
                [JsonProperty(PropertyName = "Позиция GUI AnchorMax (дефолт 0.643 0.782)")]
                [DefaultValue("0.643 0.782")]
                public string AnchorMax { get; set; } = "0.643 0.782";
                [JsonProperty(PropertyName = "Цвет фона меню")]
                [DefaultValue("#00000099")]
                public string BgColor { get; set; } = "#00000099";
                [JsonProperty(PropertyName = "Цвет кнопки ЗАКРЫТЬ")]
                [DefaultValue("#DB0000ff")]
                public string bCloseColor { get; set; } = "#DB0000ff";
                [JsonProperty(PropertyName = "Цвет кнопки ПОЛУЧИТЬ КОД")]
                [DefaultValue("#1FEF00ff")]
                public string bSendColor { get; set; } = "#1FEF00ff";
                [JsonProperty(PropertyName = "Цвет остальных кнопок")]
                [DefaultValue("#494949ff")]
                public string bMenuColor { get; set; } = "#494949ff";
            }
        }
        private void LoadVariables()
        {
            bool changed = false;
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            config = Config.ReadObject<ConfigData>();
            if (config.AdmNotify == null) { config.AdmNotify = new ConfigData.AdminNotify(); changed = true; }
            if (config.ChNotify == null) { config.ChNotify = new ConfigData.ChatNotify(); changed = true; }
            if (config.WipeStg == null) { config.WipeStg = new ConfigData.WipeSettings(); changed = true; }
            if (config.GrGifts == null) { config.GrGifts = new ConfigData.GroupGifts(); changed = true; }
            if (config.TopWPlayersPromo == null) { config.TopWPlayersPromo = new ConfigData.TopWPlPromoSet(); changed = true; }
            if (config.CMDSet == null) { config.CMDSet = new ConfigData.CommandSettings(); changed = true; }
            if (config.DGLSet == null) { config.DGLSet = new ConfigData.DynamicGroupLabelSettings(); changed = true; }
            if (config.GrWgSet == null) { config.GrWgSet = new ConfigData.GroupWidgetSettings(); changed = true; }
            if (config.GUISet == null) { config.GUISet = new ConfigData.GUISettings(); changed = true; }
            if (config.GUISet.Logo == "https://i.imgur.com/QNZykaS.png" && ConVar.Server.headerimage != string.Empty) config.GUISet.Logo = ConVar.Server.headerimage;
            Config.WriteObject(config, true);
            if (changed) PrintWarning("Конфигурационный файл обновлен. Добавлены новые настройки. Список изменений в плагине - vk.com/topic-30818042_36264027");
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
            PrintWarning("Поддержи разработчика! Вступи в группу vk.com/vkbotrust");
            PrintWarning("Инструкция по настройке плагина - goo.gl/xRkEUa");
        }
        #endregion

        #region Datastorage
        class DataStorageStats
        {
            public int WoodGath;
            public int SulfureGath;
            public int Rockets;
            public int Blueprints;
            public int Explosive;
            public int Reports;
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
            public int ConfirmCode;
            public bool Confirmed;
            public bool GiftRecived;
            public string LastRaidNotice;
            public bool WipeMsg;
            public string Bdate;
            public int Raids;
            public int Kills;
            public int Farm;
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
                statdata = new DataStorageStats();
                usersdata = new DataStorageUsers();
                reportsdata = new DataStorageReports();
            }
        }
        #endregion

        #region Oxidehooks
        void OnServerInitialized()
        {
            LoadVariables();
            if (!config.AdmNotify.GUIReports) { Unsubscribe(nameof(OnServerCommand)); Unsubscribe(nameof(OnPlayerCommand)); }
            VKBData = Interface.Oxide.DataFileSystem.GetFile("VKBotUsers");
            StatData = Interface.Oxide.DataFileSystem.GetFile("VKBot");
            ReportsData = Interface.Oxide.DataFileSystem.GetFile("VKBotReports");
            LoadData();
            cmd.AddChatCommand(config.CMDSet.CMDreport, this, "SendReport");
            CheckAdminID();
            if (NewWipe) WipeFunctions();
            if (config.StatusStg.UpdateStatus)
            {
                if (config.StatusStg.StatusSet == 1) timer.Repeat(config.StatusStg.UpdateTimer * 60, 0, Update1ServerStatus);
                if (config.StatusStg.StatusSet == 2) timer.Repeat(config.StatusStg.UpdateTimer * 60, 0, () => { UpdateMultiServerStatus("status"); });
            }
            if (config.GrWgSet.WgEnable)
            {
                if (config.GrWgSet.WgToken == "none") { PrintWarning($"Ошибка обновления виджета! В файле конфигурации не указан ключ! Инструкция - https://goo.gl/LpZujf"); return; }
                timer.Repeat(config.GrWgSet.UpdateTimer * 60, 0, () => { UpdateMultiServerStatus("widget"); });
            }
            if (config.DGLSet.DLEnable && config.DGLSet.DLUrl != "none")
            {
                timer.Repeat(config.DGLSet.DLTimer * 60, 0, () => {
                    if (config.DGLSet.DLMSEnable) { UpdateMultiServerStatus("label"); }
                    else { UpdateVKLabel(); }
                });
            }
            if (config.GrGifts.VKGGNotify) timer.Repeat(config.GrGifts.VKGGTimer * 60, 0, GiftNotifier);
            if (config.AdmNotify.PluginsCheckMsg) CheckPlugins();
            timer.Every(345, SaveData);
        }
        void SaveData()
        {
            if (config.TopWPlayersPromo.TopWPlEnabled) VKBData.WriteObject(usersdata);
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) StatData.WriteObject(statdata);
        }
        private void Init()
        {
            cmd.AddChatCommand("vk", this, "VKcommand");
            cmd.AddConsoleCommand("updatestatus", this, "UStatus");
            cmd.AddConsoleCommand("updatewidget", this, "UWidget");
            cmd.AddConsoleCommand("updatelabel", this, "ULabel");
            cmd.AddConsoleCommand("sendmsgadmin", this, "MsgAdmin");
            cmd.AddConsoleCommand("wipealerts", this, "WipeAlerts");
            cmd.AddConsoleCommand("userinfo", this, "GetUserInfo");
            cmd.AddConsoleCommand("report.answer", this, "ReportAnswer");
            cmd.AddConsoleCommand("report.list", this, "ReportList");
            cmd.AddConsoleCommand("report.wipe", this, "ReportClear");
            jsonsettings = new JsonSerializerSettings();
            jsonsettings.Converters.Add(new KeyValuePairConverter());
        }
        void Loaded() => LoadMessages();
        void Unload()
        {
            if (config.AdmNotify.SendReports) ReportsData.WriteObject(reportsdata);
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) StatData.WriteObject(statdata);
            if (config.TopWPlayersPromo.TopWPlEnabled) VKBData.WriteObject(usersdata);
            if (config.BDayGift.BDayEnabled && BDayPlayers.Count > 0)
            {
                foreach (var id in BDayPlayers)
                {
                    permission.RemoveUserGroup(id.ToString(), config.BDayGift.BDayGroup);
                }
                BDayPlayers.Clear();
            }
            UnloadAllGUI();
        }
        void OnNewSave(string filename) { NewWipe = true; }
        void OnPlayerConnected(BasePlayer player)
        {
            if (usersdata.VKUsersData.ContainsKey(player.userID))
            {
                if (usersdata.VKUsersData[player.userID].Name != player.displayName)
                {
                    usersdata.VKUsersData[player.userID].Name = player.displayName;
                    VKBData.WriteObject(usersdata);
                }
            }
            if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player);
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!usersdata.VKUsersData.ContainsKey(player.userID)) return;
            if (!config.BDayGift.BDayEnabled) return;
            if (config.BDayGift.BDayEnabled && permission.GroupExists(config.BDayGift.BDayGroup))
            {
                if (permission.UserHasGroup(player.userID.ToString(), config.BDayGift.BDayGroup)) return;
                var today = DateTime.Now.ToString("d.M", CultureInfo.InvariantCulture);
                var bday = usersdata.VKUsersData[player.userID].Bdate;
                if (bday == null || bday == "noinfo") return;
                string[] array = bday.Split('.');
                if (array.Length == 3) bday.Remove(bday.Length - 5, 5);
                if (bday == today)
                {
                    permission.AddUserGroup(player.userID.ToString(), config.BDayGift.BDayGroup);
                    PrintToChat(player, string.Format(GetMsg("ПоздравлениеИгрока", player)));
                    Log("bday", $"Игрок {player.displayName} добавлен в группу {config.BDayGift.BDayGroup}");
                    BDayPlayers.Add(player.userID);
                    if (config.BDayGift.BDayNotify) Server.Broadcast(string.Format(GetMsg("ДеньРожденияИгрока", player), player.displayName));
                }
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
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
            if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player);
        }
        void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            string msg2 = null;
            if (config.MltServSet.MSSEnable) { msg2 = $"[Сервер {config.MltServSet.ServerNumber.ToString()}] Игрок {name} ({id}) был забанен на сервере. Причина: {reason}. Ссылка на профиль стим: steamcommunity.com/profiles/{id}/"; }
            else { msg2 = $"Игрок {name} ({id}) был забанен на сервере. Причина: {reason}. Ссылка на профиль стим: steamcommunity.com/profiles/{id}/"; }
            if (config.AdmNotify.UserBannedTopic && config.AdmNotify.BannedTopicID != "null") AddComentToBoard(config.AdmNotify.BannedTopicID, msg2);
            if (config.AdmNotify.UserBannedMsg)
            {
                if (usersdata.VKUsersData.ContainsKey(id) && usersdata.VKUsersData[id].Confirmed) { msg2 = msg2 + $" . Ссылка на профиль ВК: vk.com/id{usersdata.VKUsersData[id].VkID}"; }
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("bans"))
                {
                    SendChatMessage(config.ChNotify.ChatID, msg2);
                    if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkID, msg2);
                }
                else { SendVkMessage(config.AdmNotify.VkID, msg2); }
            }
        }
        #endregion

        #region Stats
        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
        {
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) statdata.Blueprints++;
        }
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable)
            {
                if (item.info.shortname == "wood") statdata.WoodGath = statdata.WoodGath + item.amount;
                if (item.info.shortname == "sulfur.ore") statdata.SulfureGath = statdata.SulfureGath + item.amount;
            }
            if (config.TopWPlayersPromo.TopWPlEnabled)
            {
                BasePlayer player = entity.ToPlayer();
                if (player == null) return;
                if (usersdata.VKUsersData.ContainsKey(player.userID)) usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount;
            }
        }
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if ((config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) && item.info.shortname == "sulfur.ore") statdata.SulfureGath = statdata.SulfureGath + item.amount;
            if (config.TopWPlayersPromo.TopWPlEnabled)
            {
                if (usersdata.VKUsersData.ContainsKey(player.userID)) usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount;
            }
        }
        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable)
            {
                if (item.info.shortname == "wood") statdata.WoodGath = statdata.WoodGath + item.amount;
                if (item.info.shortname == "sulfur.ore") statdata.SulfureGath = statdata.SulfureGath + item.amount;
            }
            if (config.TopWPlayersPromo.TopWPlEnabled)
            {
                if (usersdata.VKUsersData.ContainsKey(player.userID)) usersdata.VKUsersData[player.userID].Farm = usersdata.VKUsersData[player.userID].Farm + item.amount;
            }
        }
        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable) statdata.Rockets++;
        }
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (config.StatusStg.UpdateStatus || config.DGLSet.DLEnable)
            {
                List<object> include = new List<object>()
                {
                "explosive.satchel.deployed",
                "grenade.f1.deployed",
                "grenade.beancan.deployed",
                "explosive.timed.deployed"
                };
                if (include.Contains(entity.ShortPrefabName)) statdata.Explosive++;
            }
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (config.TopWPlayersPromo.TopWPlEnabled)
            {
                if (entity.name.Contains("corpse")) return;
                if (hitInfo == null) return;
                var attacker = hitInfo.Initiator?.ToPlayer();
                if (attacker == null) return;
                if (entity is BasePlayer) CheckDeath(entity.ToPlayer(), hitInfo, attacker);
                if (entity is BaseEntity)
                {
                    if (hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Explosion && hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Heat && hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Bullet) return;
                    if (attacker.userID == entity.OwnerID) return;
                    BuildingBlock block = entity.GetComponent<BuildingBlock>();
                    if (block != null)
                    {
                        if (block.currentGrade.gradeBase.type.ToString() == "Twigs" || block.currentGrade.gradeBase.type.ToString() == "Wood") return;
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
                    if (usersdata.VKUsersData.ContainsKey(attacker.userID)) usersdata.VKUsersData[attacker.userID].Raids++;
                }
            }
        }
        private void CheckDeath(BasePlayer player, HitInfo info, BasePlayer attacker)
        {
            if (IsNPC(player)) return;
            if (!usersdata.VKUsersData.ContainsKey(attacker.userID)) return;
            if (!player.IsConnected) return;
            bool Duelist = false;
            if (Duel) Duelist = (bool)Duel?.Call("IsDuelPlayer", player);
            if (Duelist) return;
            usersdata.VKUsersData[attacker.userID].Kills++;
        }
        #endregion

        #region Wipe
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
                NewWipe = false;
                if (config.StatusStg.StatusSet == 1) { Update1ServerStatus(); }
                if (config.StatusStg.StatusSet == 2) { UpdateMultiServerStatus("status"); }
            }
            if (config.WipeStg.WPostMsgAdmin)
            {
                string msg2 = "[VKBot] Сервер ";
                if (config.MltServSet.MSSEnable) msg2 = msg2 + config.MltServSet.ServerNumber.ToString() + " ";
                if (ConVar.Server.levelurl != string.Empty) { msg2 = msg2 + $"вайпнут. Установлена карта: {ConVar.Server.levelurl}."; }
                else  { msg2 = msg2 + $"вайпнут. Установлена карта: {ConVar.Server.level}. Размер: {ConVar.Server.worldsize}. Сид: {ConVar.Server.seed}"; }
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("wipe"))
                {
                    SendChatMessage(config.ChNotify.ChatID, msg2);
                    if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkID, msg2);
                }
                else { SendVkMessage(config.AdmNotify.VkID, msg2); }
            }
            if (config.WipeStg.WPostB)
            {
                if (config.WipeStg.WPostAttB) { SendVkWall($"{config.WipeStg.WPostMsg}&attachments={config.WipeStg.WPostAtt}"); }
                else { SendVkWall($"{config.WipeStg.WPostMsg}"); }
            }
            if (config.GrGifts.GiftsWipe)
            {
                int amount = usersdata.VKUsersData.Count;
                if (amount != 0)
                {
                    for (int i = 0; i < amount; i++)
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
                    if (config.TopWPlayersPromo.TopPlPromoGift && config.TopWPlayersPromo.GenRandomPromo) SetRandomPromo();
                }
                int amount = usersdata.VKUsersData.Count;
                if (amount != 0)
                {
                    for (int i = 0; i < amount; i++)
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
                statdata.Reports = 0;
                StatData.WriteObject(statdata);
            }
            if (config.WipeStg.GrNameChange)
            {
                string wipedate = WipeDate();
                string text = config.WipeStg.GrName.Replace("{wipedate}", wipedate);
                string url = "https://api.vk.com/method/groups.edit?group_id=" + config.VKAPIT.GroupID + "&title=" + text + "&v=5.85&access_token=" + config.VKAPIT.VKTokenApp;
                webrequest.Enqueue(url, null, (code, response) =>
                {
                    var json = JObject.Parse(response);
                    string Result = (string)json["response"];
                    if (Result == "1") { PrintWarning($"Новое имя группы - {text}"); }
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
            var BannedUsers = ServerUsers.BanListString();
            string userlist = "";
            int usercount = 0;
            int amount = usersdata.VKUsersData.Count;
            if (amount != 0)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (config.WipeStg.WCMDIgnore || usersdata.VKUsersData.ElementAt(i).Value.WipeMsg)
                    {
                        if (!BannedUsers.Contains(usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString()))
                        {
                            if (usercount == 100)
                            {
                                UserList.Add(userlist);
                                userlist = "";
                                usercount = 0;
                            }
                            if (usercount > 0) userlist = userlist + ", ";
                            userlist = userlist + usersdata.VKUsersData.ElementAt(i).Value.VkID;
                            usercount++;
                        }
                    }
                }
            }
            if (userlist == "" && UserList.Count == 0) { PrintWarning($"Список адресатов рассылки о вайпе пуст."); return; }
            if (UserList.Count > 0)
            {
                foreach (var list in UserList)
                {
                    SendVkMessage(list, config.WipeStg.WMsgText);
                }
            }
            SendVkMessage(userlist, config.WipeStg.WMsgText);
        }
        #endregion

        #region MainMethods
        private void UStatus(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (config.StatusStg.UpdateStatus)
            {
                if (config.StatusStg.StatusSet == 1) { Update1ServerStatus(); }
                if (config.StatusStg.StatusSet == 2) { UpdateMultiServerStatus("status"); }
            }
            else { PrintWarning($"Функция обновления статуса отключена."); }
        }
        private void UWidget(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (config.GrWgSet.WgEnable)
            {
                if (config.GrWgSet.WgToken == "none") { PrintWarning($"Ошибка! В файле конфигурации не указан ключ!"); return; }
                UpdateMultiServerStatus("widget");
            }
            else { PrintWarning($"Функция обновления статуса отключена."); }
        }
        private string PrepareStatus(string input, string target)
        {
            string text = input;
            string temp = "";
            temp = GetOnline();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("onlinecounter")) temp = EmojiCounters(temp);
            if (input.Contains("{onlinecounter}")) text = text.Replace("{onlinecounter}", temp);
            temp = BasePlayer.sleepingPlayerList.Count.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("sleepers")) temp = EmojiCounters(temp);
            if (input.Contains("{sleepers}")) text = text.Replace("{sleepers}", temp);
            temp = statdata.WoodGath.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("woodcounter")) temp = EmojiCounters(temp);
            if (input.Contains("{woodcounter}")) text = text.Replace("{woodcounter}", temp);
            temp = statdata.SulfureGath.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("sulfurecounter")) temp = EmojiCounters(temp);
            if (input.Contains("{sulfurecounter}")) text = text.Replace("{sulfurecounter}", temp);
            temp = statdata.Rockets.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("rocketscounter")) temp = EmojiCounters(temp);
            if (input.Contains("{rocketscounter}")) text = text.Replace("{rocketscounter}", temp);
            temp = statdata.Blueprints.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("blueprintsconter")) temp = EmojiCounters(temp);
            if (input.Contains("{blueprintsconter}")) text = text.Replace("{blueprintsconter}", temp);
            temp = statdata.Explosive.ToString();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("explosivecounter")) temp = EmojiCounters(temp);
            if (input.Contains("{explosivecounter}")) text = text.Replace("{explosivecounter}", temp);
            temp = (string)WipeDate();
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("wipedate")) temp = EmojiCounters(temp);
            if (input.Contains("{wipedate}")) text = text.Replace("{wipedate}", temp);
            temp = config.StatusStg.connecturl;
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("connect")) temp = EmojiCounters(temp);
            if (input.Contains("{connect}")) text = text.Replace("{connect}", temp);
            temp = config.StatusStg.StatusUT;
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("usertext")) temp = EmojiCounters(temp);
            if (input.Contains("{usertext}")) text = text.Replace("{usertext}", temp);
            temp = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (target == "status" && config.StatusStg.EmojiCounterList.Contains("updatetime")) temp = EmojiCounters(temp);
            if (input.Contains("{updatetime}")) text = text.Replace("{updatetime}", temp);
            return text;
        }
        private void SendReport(BasePlayer player, string cmd, string[] args)
        {
            if (config.AdmNotify.SendReports)
            {
                if (args.Length > 0) { CreateReport(player, string.Join(" ", args.Skip(0).ToArray())); }
                else
                {
                    if (config.AdmNotify.GUIReports) { ReportGUI(player); }
                    else { PrintToChat(player, string.Format(GetMsg("КомандаРепорт", player), config.AdmNotify.ReportsNotify)); return; }
                }
            }
            else { PrintToChat(player, string.Format(GetMsg("ФункцияОтключена", player))); }
        }
        private void CheckReport(BasePlayer player, string[] text)
        {
            if (text != null && text.Count() < 2) return;
            ulong uid = 0;
            if (ulong.TryParse(text[1], out uid))
            {
                var utarget = BasePlayer.FindByID(uid);
                if (utarget != null && text.Count() > 2) { CreateReport(player, string.Join(" ", text.Skip(2).ToArray()), utarget); }
                else { CreateReport(player, string.Join(" ", text.Skip(1).ToArray())); }
            }
            else { CreateReport(player, string.Join(" ", text.Skip(1).ToArray())); }
        }
        private void CreateReport(BasePlayer player, string text, BasePlayer target = null)
        {
            string reportplayer = "";
            if (target != null) { reportplayer = reportplayer + "Жалоба на игрока " + target.displayName + " (" + "steamcommunity.com/profiles/" + target.userID + "/) "; }
            string reporttext = "[VKBot]";
            statdata.Reports = statdata.Reports + 1;
            int reportid = statdata.Reports;
            StatData.WriteObject(statdata);
            if (config.MltServSet.MSSEnable) reporttext = reporttext + " [Сервер " + config.MltServSet.ServerNumber.ToString() + "]";
            reporttext = reporttext + " " + player.displayName + " " + "(" + player.UserIDString + ")";
            if (usersdata.VKUsersData.ContainsKey(player.userID))
            {
                if (usersdata.VKUsersData[player.userID].Confirmed) { reporttext = reporttext + ". ВК: vk.com/id" + usersdata.VKUsersData[player.userID].VkID; }
                else { reporttext = reporttext + ". ВК: vk.com/id" + usersdata.VKUsersData[player.userID].VkID + " (не подтвержден)"; }
            }
            reporttext = reporttext + " ID репорта: " + reportid;
            reporttext = reporttext + reportplayer;
            reporttext = reporttext + ". Сообщение: " + text;
            if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("reports"))
            {
                SendChatMessage(config.ChNotify.ChatID, reporttext);
                if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkID, reporttext);
            }
            else { SendVkMessage(config.AdmNotify.VkID, reporttext); }
            reportsdata.VKReportsData.Add(reportid, new REPORT
            {
                UserID = player.userID,
                Name = player.displayName,
                Text = reportplayer + text
            });
            ReportsData.WriteObject(reportsdata);
            Log("Log", $"{player.displayName} ({player.userID}): написал администратору: {reporttext}");
            PrintToChat(player, string.Format(GetMsg("РепортОтправлен", player), config.AdmNotify.ReportsNotify));
        }
        private void CheckVkUser(BasePlayer player, string url)
        {
            string Userid = null;
            string[] arr1 = url.Split('/');
            int num = arr1.Length - 1;
            string vkname = arr1[num];
            string url2 = "https://api.vk.com/method/users.get?user_ids=" + vkname + "&v=5.85&fields=bdate&access_token=" + config.VKAPIT.VKToken;
            webrequest.Enqueue(url2, null, (code, response) => {
                if (!response.Contains("error"))
                {
                    var json = JObject.Parse(response);
                    Userid = (string)json["response"][0]["id"];
                    string bdate = "noinfo";
                    bdate = (string)json["response"][0]["bdate"];
                    if (Userid != null) { AddVKUser(player, Userid, bdate); }
                    else { PrintToChat(player, "Ошибка обработки вашей ссылки ВК, обратитесь к администратору."); }
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
                    Raids = 0
                });
                VKBData.WriteObject(usersdata);
                SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /vk confirm {usersdata.VKUsersData[player.userID].ConfirmCode},затем войдите в магазин и пришлите свой ник следующим сообщением", player);
            }
            else
            {
                if (Userid == usersdata.VKUsersData[player.userID].VkID && usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден", player))); return; }
                if (Userid == usersdata.VKUsersData[player.userID].VkID && !usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильДобавлен", player))); return; }
                usersdata.VKUsersData[player.userID].Name = player.displayName;
                usersdata.VKUsersData[player.userID].VkID = Userid;
                usersdata.VKUsersData[player.userID].Confirmed = false;
                usersdata.VKUsersData[player.userID].ConfirmCode = random.Next(1, 9999999);
                usersdata.VKUsersData[player.userID].Bdate = bdate;
                VKBData.WriteObject(usersdata);
                SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /vk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player);
            }
        }
        private void VKcommand(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "add")
                {
                    if (args.Length == 1) { PrintToChat(player, string.Format(GetMsg("ДоступныеКоманды", player))); return; }
                    if (!args[1].Contains("vk.com/")) { PrintToChat(player, string.Format(GetMsg("НеправильнаяСсылка", player))); return; }
                    CheckVkUser(player, args[1]);
                }
                if (args[0] == "confirm")
                {
                    if (args.Length >= 2)
                    {
                        if (usersdata.VKUsersData.ContainsKey(player.userID))
                        {
                            if (usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден", player))); return; }
                            if (args[1] == usersdata.VKUsersData[player.userID].ConfirmCode.ToString())
                            {
                                usersdata.VKUsersData[player.userID].Confirmed = true;
                                VKBData.WriteObject(usersdata);
                                PrintToChat(player, string.Format(GetMsg("ПрофильПодтвержден", player)));
                                if (config.GrGifts.VKGroupGifts) { PrintToChat(player, string.Format(GetMsg("ОповещениеОПодарках", player), config.GrGifts.VKGroupUrl)); }
                            }
                            else { PrintToChat(player, string.Format(GetMsg("НеверныйКод", player))); }
                        }
                        else { PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен", player))); }
                    }
                    else
                    {
                        if (!usersdata.VKUsersData.ContainsKey(player.userID)) { PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен", player))); return; }
                        if (usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильДобавленИПодтвержден", player))); return; }
                        SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /vk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player);
                    }
                }
                if (args[0] == "gift") FixedGifts(player);
                if (args[0] == "wipealerts") WAlert(player);
                if (args[0] != "add" && args[0] != "gift" && args[0] != "confirm")
                {
                    PrintToChat(player, string.Format(GetMsg("ДоступныеКоманды", player)));
                    if (config.GrGifts.VKGroupGifts) PrintToChat(player, string.Format(GetMsg("ОповещениеОПодарках", player), config.GrGifts.VKGroupUrl));
                }
            }
            else { StartVKBotMainGUI(player); }
        }
        private void WAlert(BasePlayer player)
        {
            if (!usersdata.VKUsersData.ContainsKey(player.userID)) { PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен", player))); return; }
            if (!usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильНеПодтвержден", player))); return; }
            if (config.WipeStg.WCMDIgnore) { PrintToChat(player, string.Format(GetMsg("АвтоОповещенияОвайпе", player))); return; }
            if (usersdata.VKUsersData[player.userID].WipeMsg)
            {
                usersdata.VKUsersData[player.userID].WipeMsg = false;
                VKBData.WriteObject(usersdata);
                PrintToChat(player, string.Format(GetMsg("ПодпискаОтключена", player)));
            }
            else
            {
                usersdata.VKUsersData[player.userID].WipeMsg = true;
                VKBData.WriteObject(usersdata);
                PrintToChat(player, string.Format(GetMsg("ПодпискаВключена", player)));
            }
        }
        private void VKGift(BasePlayer player)
        {
            if (config.GrGifts.VKGroupGifts)
            {
                if (!usersdata.VKUsersData.ContainsKey(player.userID)) { PrintToChat(player, string.Format(GetMsg("ПрофильНеДобавлен", player))); return; }
                if (!usersdata.VKUsersData[player.userID].Confirmed) { PrintToChat(player, string.Format(GetMsg("ПрофильНеПодтвержден", player))); return; }
                if (usersdata.VKUsersData[player.userID].GiftRecived) { PrintToChat(player, string.Format(GetMsg("НаградаУжеПолучена", player))); return; }
                string url = $"https://api.vk.com/method/groups.isMember?group_id={config.VKAPIT.GroupID}&user_id={usersdata.VKUsersData[player.userID].VkID}&v=5.85&access_token={config.VKAPIT.VKToken}";
                try
                {
                    webrequest.Enqueue(url, null, (code, response) => {
                        if (response == null || !response.Contains("response")) return;
                        var json = JObject.Parse(response);
                        if (json == null) return;
                        string Result = (string)json["response"];
                        if (Result == null) return;
                        GetGift(code, Result, player);
                    }, this);
                }
                catch { }
                
            }
            else { PrintToChat(player, string.Format(GetMsg("ФункцияОтключена", player))); }
        }
        private void GetGift(int code, string Result, BasePlayer player)
        {
            if (Result == "1")
            {
                if (config.GrGifts.VKGroupGiftCMD == "none")
                {
                    int FreeSlots = 24 - player.inventory.containerMain.itemList.Count;
                    if (FreeSlots >= config.GrGifts.VKGroupGiftList.Count)
                    {
                        usersdata.VKUsersData[player.userID].GiftRecived = true;
                        VKBData.WriteObject(usersdata);
                        PrintToChat(player, string.Format(GetMsg("НаградаПолучена", player)));
                        if (config.GrGifts.GiftsBool) Server.Broadcast(string.Format(GetMsg("ПолучилНаграду", player), player.displayName, config.GrGifts.VKGroupUrl));
                        for (int i = 0; i < config.GrGifts.VKGroupGiftList.Count; i++)
                        {
                            if (Convert.ToInt32(config.GrGifts.VKGroupGiftList.ElementAt(i).Value) > 0)
                            {
                                Item gift = ItemManager.CreateByName(config.GrGifts.VKGroupGiftList.ElementAt(i).Key, Convert.ToInt32(config.GrGifts.VKGroupGiftList.ElementAt(i).Value));
                                gift.MoveToContainer(player.inventory.containerMain, -1, false);
                            }
                        }
                    }
                    else { PrintToChat(player, string.Format(GetMsg("НетМеста", player))); }
                }
                else
                {
                    string cmd = config.GrGifts.VKGroupGiftCMD.Replace("{steamid}", player.userID.ToString());
                    rust.RunServerCommand(cmd);
                    usersdata.VKUsersData[player.userID].GiftRecived = true;
                    VKBData.WriteObject(usersdata);
                    PrintToChat(player, string.Format(GetMsg("НаградаПолученаКоманда", player), config.GrGifts.GiftCMDdesc));
                    if (config.GrGifts.GiftsBool) Server.Broadcast(string.Format(GetMsg("ПолучилНаграду", player), player.displayName, config.GrGifts.VKGroupUrl));
                }
            }
            else { PrintToChat(player, string.Format(GetMsg("НеВступилВГруппу", player), config.GrGifts.VKGroupUrl)); }
        }
        private void GiftNotifier()
        {
            if (config.GrGifts.VKGroupGifts)
            {
                foreach (var pl in BasePlayer.activePlayerList)
                {
                    if (!usersdata.VKUsersData.ContainsKey(pl.userID)) { PrintToChat(pl, string.Format(GetMsg("ОповещениеОПодарках", pl), config.GrGifts.VKGroupUrl)); }
                    else
                    {
                        if (!usersdata.VKUsersData[pl.userID].GiftRecived) PrintToChat(pl, string.Format(GetMsg("ОповещениеОПодарках", pl), config.GrGifts.VKGroupUrl));
                    }
                }
            }
        }
        void Update1ServerStatus()
        {
            string status = PrepareStatus(config.StatusStg.StatusText, "status");
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
                webrequest.Enqueue(url, null, (code, response) => {
                    if (response != null || code == 200)
                    {

                        var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
                        if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return;
                        if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return;
                        string online = jsonresponse3["players"].ToString();
                        string slots = jsonresponse3["maxplayers"].ToString();
                        if (config.MltServSet.EmojiStatus && target == "status") { online = EmojiCounters(online); slots = EmojiCounters(slots); }
                        string name = "1⃣: ";
                        if (target == "widget") name = "1: ";
                        if (config.MltServSet.Server1name != "none")
                        {
                            name = config.MltServSet.Server1name + " ";
                            if (target == "widget") name = config.MltServSet.Server1name;
                        }                        
                        server1 = name + online.ToString() + "/" + slots.ToString();
                        if (target == "widget")
                        {
                            SList.Add(1, new ServerInfo() { name = name, online = online, slots = slots, sleepers = jsonresponse3["sleepers"].ToString(), map = jsonresponse3["level"].ToString()});
                        }
                    }
                }, this);
            }
            if (config.MltServSet.Server2ip != "none")
            {
                var url = "http://" + config.MltServSet.Server2ip + "/status.json";
                webrequest.Enqueue(url, null, (code, response) => {
                    if (response != null || code == 200)
                    {

                        var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
                        if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return;
                        if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return;
                        string online = jsonresponse3["players"].ToString();
                        string slots = jsonresponse3["maxplayers"].ToString();
                        if (config.MltServSet.EmojiStatus && target == "status") { online = EmojiCounters(online); slots = EmojiCounters(slots); }
                        string name = ", 2⃣: ";
                        if (target == "widget") name = "2:";
                        if (config.MltServSet.Server2name != "none")
                        {
                            name = ", " + config.MltServSet.Server2name + " ";
                            if (target == "widget") name = config.MltServSet.Server2name;
                        }                        
                        server2 = name + online.ToString() + "/" + slots.ToString();
                        if (target == "widget")
                        {
                            SList.Add(2, new ServerInfo() { name = name, online = online, slots = slots, sleepers = jsonresponse3["sleepers"].ToString(), map = jsonresponse3["level"].ToString() });
                        }
                    }
                }, this);
            }
            if (config.MltServSet.Server3ip != "none")
            {
                var url = "http://" + config.MltServSet.Server3ip + "/status.json";
                webrequest.Enqueue(url, null, (code, response) => {
                    if (response != null || code == 200)
                    {

                        var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
                        if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return;
                        if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return;
                        string online = jsonresponse3["players"].ToString();
                        string slots = jsonresponse3["maxplayers"].ToString();
                        if (config.MltServSet.EmojiStatus && target == "status") { online = EmojiCounters(online); slots = EmojiCounters(slots); }
                        string name = ", 3⃣: ";
                        if (target == "widget") name = "3:";
                        if (config.MltServSet.Server3name != "none")
                        {
                            name = ", " + config.MltServSet.Server3name + " ";
                            if (target == "widget") name = config.MltServSet.Server3name;
                        }                        
                        server3 = name + online.ToString() + "/" + slots.ToString();
                        if (target == "widget")
                        {
                            SList.Add(3, new ServerInfo() { name = name, online = online, slots = slots, sleepers = jsonresponse3["sleepers"].ToString(), map = jsonresponse3["level"].ToString() });
                        }
                    }
                }, this);
            }
            if (config.MltServSet.Server4ip != "none")
            {
                var url = "http://" + config.MltServSet.Server4ip + "/status.json";
                webrequest.Enqueue(url, null, (code, response) => {
                    if (response != null || code == 200)
                    {

                        var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
                        if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return;
                        if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return;
                        string online = jsonresponse3["players"].ToString();
                        string slots = jsonresponse3["maxplayers"].ToString();
                        if (config.MltServSet.EmojiStatus && target == "status") { online = EmojiCounters(online); slots = EmojiCounters(slots); }
                        string name = ", 4⃣: ";
                        if (target == "widget") name = "4:";
                        if (config.MltServSet.Server4name != "none")
                        {
                            name = ", " + config.MltServSet.Server4name + " ";
                            if (target == "widget") name = config.MltServSet.Server4name;
                        }                        
                        server4 = name + online.ToString() + "/" + slots.ToString();
                        if (target == "widget")
                        {
                            SList.Add(4, new ServerInfo() { name = name, online = online, slots = slots, sleepers = jsonresponse3["sleepers"].ToString(), map = jsonresponse3["level"].ToString() });
                        }
                    }
                }, this);
            }
            if (config.MltServSet.Server5ip != "none")
            {
                var url = "http://" + config.MltServSet.Server5ip + "/status.json";
                webrequest.Enqueue(url, null, (code, response) => {
                    if (response != null || code == 200)
                    {

                        var jsonresponse3 = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
                        if (!(jsonresponse3 is Dictionary<string, object>) || jsonresponse3.Count == 0 || !jsonresponse3.ContainsKey("players") || !jsonresponse3.ContainsKey("maxplayers")) return;
                        if (target == "widget" && (!jsonresponse3.ContainsKey("sleepers") || !jsonresponse3.ContainsKey("level"))) return;
                        string online = jsonresponse3["players"].ToString();
                        string slots = jsonresponse3["maxplayers"].ToString();
                        if (config.MltServSet.EmojiStatus && target == "status") { online = EmojiCounters(online); slots = EmojiCounters(slots); }
                        string name = ", 5⃣: ";
                        if (target == "widget") name = "5:";
                        if (config.MltServSet.Server5name != "none")
                        {
                            name = ", " + config.MltServSet.Server5name + " ";
                            if (target == "widget") name = config.MltServSet.Server5name;
                        }                        
                        server5 = name + online.ToString() + "/" + slots.ToString();
                        if (target == "widget")
                        {
                            SList.Add(5, new ServerInfo() { name = name, online = online, slots = slots, sleepers = jsonresponse3["sleepers"].ToString(), map = jsonresponse3["level"].ToString() });
                        }
                    }
                }, this);
            }
            Puts("Обработка данных. Статус/обложка/виджет будет отправлен(а) через 10 секунд.");
            timer.Once(10f, () =>
            {
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
                        StatusCheck(text);
                        SendVkStatus(text);
                    }
                    if (target == "label")
                    {
                        text = text.Replace("⃣", "%23");
                        UpdateLabelMultiServer(text);
                    }
                }
                else { PrintWarning("Текст для статуса/обложки пуст, не заполнен конфиг или не получены данные с Rust:IO"); }
            });
        }
        private void MsgAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (arg.Args == null)
            {
                PrintWarning($"Текст сообщения отсутсвует, правильная команда |sendmsgadmin сообщение|.");
                return;
            }
            string[] args = arg.Args;
            if (args.Length > 0)
            {
                string text = null;
                if (config.MltServSet.MSSEnable) { text = $"[VKBot msgadmin] [Сервер {config.MltServSet.ServerNumber}] " + string.Join(" ", args.Skip(0).ToArray()); }
                else { text = $"[VKBot msgadmin] " + string.Join(" ", args.Skip(0).ToArray()); }
                SendVkMessage(config.AdmNotify.VkID, text);
                Log("Log", $"|sendmsgadmin| Отправлено новое сообщение администратору: ({text})");
            }
        }
        private void ReportAnswer(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) return;
            if (arg.Args == null || arg.Args.Count() < 2) { PrintWarning($"Использование команды - reportanswer 'ID репорта' 'текст ответа'"); return; }
            if (reportsdata.VKReportsData.Count == 0) { PrintWarning($"База репортов пуста"); return; }
            int reportid = 0;
            reportid = Convert.ToInt32(arg.Args[0]);
            if (reportid == 0 || !reportsdata.VKReportsData.ContainsKey(reportid)) { PrintWarning($"Указан неверный ID репорта"); return; }
            string answer = string.Join(" ", arg.Args.Skip(1).ToArray());
            if (usersdata.VKUsersData.ContainsKey(reportsdata.VKReportsData[reportid].UserID) && usersdata.VKUsersData[reportsdata.VKReportsData[reportid].UserID].Confirmed)
            {
                string msg = string.Format(GetMsg("ОтветНаРепортВК", 0)) + answer;
                SendVkMessage(usersdata.VKUsersData[reportsdata.VKReportsData[reportid].UserID].VkID, msg);
                PrintWarning($"Ваш ответ был отправлен игроку в ВК.");
                reportsdata.VKReportsData.Remove(reportid);
                ReportsData.WriteObject(reportsdata);
            }
            else
            {
                BasePlayer reciver = BasePlayer.FindByID(reportsdata.VKReportsData[reportid].UserID);
                if (reciver != null)
                {
                    PrintToChat(reciver, string.Format(GetMsg("ОтветНаРепортЧат", reciver)) + answer);
                    PrintWarning($"Ваш ответ был отправлен игроку в игровой чат.");
                    reportsdata.VKReportsData.Remove(reportid);
                    ReportsData.WriteObject(reportsdata);
                }
                else { PrintWarning($"Игрок отправивший репорт оффлайн. Невозможно отправить ответ."); }
            }
        }
        private void ReportList(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (reportsdata.VKReportsData.Count == 0) { PrintWarning($"База репортов пуста"); return; }
            foreach (var report in reportsdata.VKReportsData)
            {
                string status = "offline";
                if (BasePlayer.FindByID(report.Value.UserID) != null) status = "online";
                PrintWarning($"Репорт: ID {report.Key} от игрока {report.Value.Name} ({report.Value.UserID.ToString()}) ({status}). Текст: {report.Value.Text}");
            }
        }
        private void ReportClear(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (reportsdata.VKReportsData.Count == 0) { PrintWarning($"База репортов пуста"); return; }
            reportsdata.VKReportsData.Clear();
            ReportsData.WriteObject(reportsdata);
            statdata.Reports = 0;
            StatData.WriteObject(statdata);
            PrintWarning($"База репортов очищена");
        }
        private void GetUserInfo(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) return;
            if (arg.Args == null) { PrintWarning($"Введите команду userinfo ник/steamid/vkid для получения информации о игроке из базы vkbot"); return; }
            string[] args = arg.Args;
            if (args.Length > 0)
            {
                bool returned = false;
                int amount = usersdata.VKUsersData.Count;
                if (amount != 0)
                {
                    for (int i = 0; i < amount; i++)
                    {
                        if (usersdata.VKUsersData.ElementAt(i).Value.Name.ToLower().Contains(args[0]) || usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString() == (args[0]) || usersdata.VKUsersData.ElementAt(i).Value.VkID == (args[0]))
                        {
                            returned = true;
                            string text = "Никнейм: " + usersdata.VKUsersData.ElementAt(i).Value.Name + "\nSTEAM: steamcommunity.com/profiles/" + usersdata.VKUsersData.ElementAt(i).Value.UserID + "/";
                            if (usersdata.VKUsersData.ElementAt(i).Value.Confirmed) { text = text + "\nVK: vk.com/id" + usersdata.VKUsersData.ElementAt(i).Value.VkID; }
                            else { text = text + "\nVK: vk.com/id" + usersdata.VKUsersData.ElementAt(i).Value.VkID + " (не подтвержден)"; }
                            if (usersdata.VKUsersData.ElementAt(i).Value.Bdate != null && usersdata.VKUsersData.ElementAt(i).Value.Bdate != "noinfo") text = text + "\nДата рождения: " + usersdata.VKUsersData.ElementAt(i).Value.Bdate;
                            if (config.TopWPlayersPromo.TopWPlEnabled) text = text + "\nРазрушено строений: " + usersdata.VKUsersData.ElementAt(i).Value.Raids + "\nУбито игроков: " + usersdata.VKUsersData.ElementAt(i).Value.Kills + "\nНафармил: " + usersdata.VKUsersData.ElementAt(i).Value.Farm;
                            Puts(text);
                        }
                    }
                }
                if (!returned) Puts("Не найдено игроков с таким именем / steamid / vkid");
            }
        }
        private void SendConfCode(string reciverID, string msg, BasePlayer player)
        {
            string url = "https://api.vk.com/method/messages.send?user_ids=" + reciverID + "&message=" + msg + "&v=5.85&access_token=" + config.VKAPIT.VKToken;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Код подтверждения", player), this);
        }
        private void CheckPlugins()
        {
            var loadedPlugins = plugins.GetAll().Where(pl => !pl.IsCorePlugin).ToArray();
            var loadedPluginNames = new HashSet<string>(loadedPlugins.Select(pl => pl.Name));
            var unloadedPluginErrors = new Dictionary<string, string>();
            foreach (var loader in Interface.Oxide.GetPluginLoaders())
            {
                foreach (var name in loader.ScanDirectory(Interface.Oxide.PluginDirectory).Except(loadedPluginNames))
                {
                    string msg;
                    unloadedPluginErrors[name] = (loader.PluginErrors.TryGetValue(name, out msg)) ? msg : "Unloaded";
                }
            }
            if (unloadedPluginErrors.Count > 0)
            {
                string text = null;
                if (config.MltServSet.MSSEnable) { text = $"[VKBot] [Сервер {config.MltServSet.ServerNumber}] Произошла ошибка загрузки следующих плагинов:"; }
                else { text = $"[VKBot]  Произошла ошибка загрузки следующих плагинов:"; }
                foreach (var pluginerror in unloadedPluginErrors)
                {
                    text = text + " " + pluginerror.Key + ".";
                }
                if (config.ChNotify.ChNotfEnabled && config.ChNotify.ChNotfSet.Contains("plugins"))
                {
                    SendChatMessage(config.ChNotify.ChatID, text);
                    if (config.ChNotify.AdmMsg) SendVkMessage(config.AdmNotify.VkID, text);
                }
                else { SendVkMessage(config.AdmNotify.VkID, text); }
            }
        }
        private void PrepareWidgetCode(Dictionary<int, ServerInfo> Slist)
        {
            string code = @"return{""title"":""" + config.GrWgSet.WgTitle + @""",""head"":[{""text"":""Сервер""},{""text"":""Онлайн""},{""text"":""Спящие""},{""text"":""Слоты""},{""text"":""Карта""}],""body"":[";
            if (Slist.Count != 0)
            {
                foreach (var info in Slist) code = code + @"[{""text"":""" + info.Value.name + @"""},{""text"":""" + info.Value.online + @"""},{""text"":""" + info.Value.sleepers + @"""},{""text"":""" + info.Value.slots + @"""},{""text"":""" + info.Value.map + @"""}],";
            }
            else
            {
                string map = ConVar.Server.level;
                if (ConVar.Server.levelurl != string.Empty) map = "Custom Map";
                code = code + @"[{""text"":""" + ConVar.Server.hostname + @"""},{""text"":""" + BasePlayer.activePlayerList.Count.ToString() + @"""},{""text"":""" + BasePlayer.sleepingPlayerList.Count.ToString() + @"""},{""text"":""" + ConVar.Server.maxplayers.ToString() + @"""},{""text"":""" + map + @"""}],";
            }
            code = code + @"],";
            if (config.GrWgSet.URLTitle != "none") code = code + @"""more"":""" + config.GrWgSet.URLTitle + @""",""more_url"": """ + config.GrWgSet.URL + @""",";
            code = code + "};";
            SendWidget(code);
        }
        #endregion

        #region VKAPI
        private void SendWidget(string widget)
        {
            string url = "https://api.vk.com/method/appWidgets.update?type=table" + "&code=" + URLEncode(widget) + "&v=5.85&access_token=" + config.GrWgSet.WgToken;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Виджет"), this);
        }
        private void SendChatMessage(string chatid, string msg)
        {
            string url = "https://api.vk.com/method/messages.send?chat_id=" + chatid + "&message=" + URLEncode(msg) + "&v=5.85&access_token=" + config.ChNotify.ChNotfToken;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Сообщение в беседу"), this);
        }
        private void SendVkMessage(string reciverID, string msg)
        {
            string url = "https://api.vk.com/method/messages.send?user_ids=" + reciverID + "&message=" + URLEncode(msg) + "&v=5.85&access_token=" + config.VKAPIT.VKToken;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Сообщение"), this);
        }
        private void SendVkWall(string msg)
        {
            string url = "https://api.vk.com/method/wall.post?owner_id=-" + config.VKAPIT.GroupID + "&message=" + URLEncode(msg) + "&from_group=1&v=5.85&access_token=" + config.VKAPIT.VKTokenApp;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Пост"), this);
        }
        private void SendVkStatus(string msg)
        {
            StatusCheck(msg);
            string url = "https://api.vk.com/method/status.set?group_id=" + config.VKAPIT.GroupID + "&text=" + URLEncode(msg) + "&v=5.85&access_token=" + config.VKAPIT.VKTokenApp;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Статус"), this);
        }
        private void AddComentToBoard(string topicid, string msg)
        {
            string url = "https://api.vk.com/method/board.createComment?group_id=" + config.VKAPIT.GroupID + "&topic_id=" + URLEncode(topicid) + "&from_group=1&message=" + msg + "&v=5.85&access_token=" + config.VKAPIT.VKTokenApp;
            webrequest.Enqueue(url, null, (code, response) => GetCallback(code, response, "Комментарий в обсуждения"), this);
        }        
        #endregion

        #region VKBotAPI
        string GetUserVKId(ulong userid)
        {
            if (usersdata.VKUsersData.ContainsKey(userid) && usersdata.VKUsersData[userid].Confirmed)
            {
                var BannedUsers = ServerUsers.BanListString();
                if (!BannedUsers.Contains(userid.ToString())) { return usersdata.VKUsersData[userid].VkID; }
                else { return null; }
            }
            else { return null; }
        }
        string GetUserLastNotice(ulong userid)
        {
            if (usersdata.VKUsersData.ContainsKey(userid) && usersdata.VKUsersData[userid].Confirmed) { return usersdata.VKUsersData[userid].LastRaidNotice; }
            else { return null; }
        }
        string AdminVkID()
        {
            return config.AdmNotify.VkID;
        }
        private void VKAPIChatMsg(string text)
        {
            if (config.ChNotify.ChNotfEnabled) { SendChatMessage(config.ChNotify.ChatID, text); }
            else { PrintWarning($"Сообщение не отправлено в беседу. Данная функция отключена. Текст сообщения: {text}"); }
        }
        private void VKAPISaveLastNotice(ulong userid, string lasttime)
        {
            if (usersdata.VKUsersData.ContainsKey(userid))
            {
                usersdata.VKUsersData[userid].LastRaidNotice = lasttime;
                VKBData.WriteObject(usersdata);
            }
            else { return; }
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
                SendVkWall($"{text}");
                Log("vkbotapi", $"Отправлен новый пост на стену: ({text})");
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
                SendVkMessage(reciverID, $"{text}");
                Log("vkbotapi", $"Отправлено новое сообщение пользователю {reciverID}: ({text})");
            }
        }
        private void VKAPIStatus(string msg)
        {
            SendVkStatus(msg);
            Log("vkbotapi", $"Отправлен новый статус: {msg}");
        }
        #endregion

        #region Helpers
        void Log(string filename, string text)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text}", this);
        }
        void GetCallback(int code, string response, string type, BasePlayer player = null)
        {
            if (!response.Contains("error")) { Puts($"{type} отправлен(о): {response}"); if (type == "Код подтверждения" && player != null) StartCodeSendedGUI(player); }
            else
            {
                if (type == "Код подтверждения")
                {
                    if (response.Contains("Can't send messages for users without permission") && player != null) { StartVKBotHelpVKGUI(player); }
                    else { Log("errorconfcode", $"Ошибка отправки кода подтверждения. Ответ сервера ВК: {response}"); }
                }
                else
                {
                    PrintWarning($"{type} не отправлен(о). Файлы лога: /oxide/logs/VKBot/");
                    Log("Errors", $"{type} не отправлен(о). Ошибка: {response}");
                }
            }
        }
        private string EmojiCounters(string counter)
        {
            var chars = counter.ToCharArray();
            string emoji = "";
            for (int ctr = 0; ctr < chars.Length; ctr++)
            {
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
                if (digits.Contains(chars[ctr].ToString()))
                {
                    string replace = chars[ctr] + "⃣";
                    emoji = emoji + replace;
                }
                else { emoji = emoji + chars[ctr]; }
            }
            return emoji;
        }
        private string WipeDate()
        {
            DateTime LastWipe = SaveRestore.SaveCreatedTime.ToLocalTime();
            string LastWipeInfo = LastWipe.ToString("dd.MM");
            return LastWipeInfo;
        }
        private string GetOnline()
        {
            string onlinecounter = BasePlayer.activePlayerList.Count.ToString();
            if (config.StatusStg.OnlWmaxslots)
            {
                var slots = ConVar.Server.maxplayers.ToString();
                onlinecounter = onlinecounter + "/" + slots.ToString();
            }
            return onlinecounter;
        }
        private string URLEncode(string input)
        {
            if (input.Contains("#")) input = input.Replace("#", "%23");
            if (input.Contains("$")) input = input.Replace("$", "%24");
            if (input.Contains("+")) input = input.Replace("+", "%2B");
            if (input.Contains("/")) input = input.Replace("/", "%2F");
            if (input.Contains(":")) input = input.Replace(":", "%3A");
            if (input.Contains(";")) input = input.Replace(";", "%3B");
            if (input.Contains("?")) input = input.Replace("?", "%3F");
            if (input.Contains("@")) input = input.Replace("@", "%40");
            return input;
        }
        private void StatusCheck(string msg)
        {
            if (msg.Length > 140) PrintWarning($"Текст статуса слишком длинный. Измените формат статуса чтобы текст отобразился полностью. Лимит символов в статусе - 140. Длина текста - {msg.Length.ToString()}");
        }
        private bool IsNPC(BasePlayer player)
        {
            if (player is NPCPlayer)
                return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
            return false;
        }
        private void CheckAdminID()
        {
            if (config.AdmNotify.VkID.Contains("/"))
            {
                string id = config.AdmNotify.VkID.Trim(new char[] { '/' });
                config.AdmNotify.VkID = id;
                Config.WriteObject(config, true);
                PrintWarning("VK ID администратора исправлен. Инструкция по настройке плагина - goo.gl/xRkEUa");
            }
        }
        private static string GetColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) { hex = "#FFFFFFFF"; }
            var str = hex.Trim('#');
            if (str.Length == 6) { str += "FF"; }
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
        #endregion

        #region TopWipePlayersStatsAndPromo
        private string BannedUsers = ServerUsers.BanListString();
        private ulong GetTopRaider()
        {
            int max = 0;
            ulong TopID = 0;
            int amount = usersdata.VKUsersData.Count;
            if (amount != 0)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (usersdata.VKUsersData.ElementAt(i).Value.Raids > max && !BannedUsers.Contains(usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString()))
                    {
                        max = usersdata.VKUsersData.ElementAt(i).Value.Raids;
                        TopID = usersdata.VKUsersData.ElementAt(i).Value.UserID;
                    }
                }
            }
            if (max != 0) { return TopID; }
            else { return 0; }
        }
        private ulong GetTopKiller()
        {
            int max = 0;
            ulong TopID = 0;
            int amount = usersdata.VKUsersData.Count;
            if (amount != 0)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (usersdata.VKUsersData.ElementAt(i).Value.Kills > max && !BannedUsers.Contains(usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString()))
                    {
                        max = usersdata.VKUsersData.ElementAt(i).Value.Kills;
                        TopID = usersdata.VKUsersData.ElementAt(i).Value.UserID;
                    }
                }
            }
            if (max != 0) { return TopID; }
            else { return 0; }
        }
        private ulong GetTopFarmer()
        {
            int max = 0;
            ulong TopID = 0;
            int amount = usersdata.VKUsersData.Count;
            if (amount != 0)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (usersdata.VKUsersData.ElementAt(i).Value.Farm > max && !BannedUsers.Contains(usersdata.VKUsersData.ElementAt(i).Value.UserID.ToString()))
                    {
                        max = usersdata.VKUsersData.ElementAt(i).Value.Farm;
                        TopID = usersdata.VKUsersData.ElementAt(i).Value.UserID;
                    }
                }
            }
            if (max != 0) { return TopID; }
            else { return 0; }
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
                if (traider != 0) { text = text + "\nТоп рэйдер: " + usersdata.VKUsersData[traider].Name; check = true; }
                if (tkiller != 0) { text = text + "\nТоп киллер: " + usersdata.VKUsersData[tkiller].Name; check = true; }
                if (tfarmer != 0) { text = text + "\nТоп фармер: " + usersdata.VKUsersData[tfarmer].Name; check = true; }
                if (config.TopWPlayersPromo.TopPlPromoGift) text = text + "\nТоп игроки получают в качестве награды промокод на баланс в магазине.";
                if (check)
                {
                    if (config.TopWPlayersPromo.TopPlPostAtt != "none") text = text + "&attachments=" + config.TopWPlayersPromo.TopPlPostAtt;
                    SendVkWall(text);
                }
            }
            if (traider != 0 && config.TopWPlayersPromo.TopPlPromoGift && usersdata.VKUsersData.ContainsKey(traider) && usersdata.VKUsersData[traider].Confirmed)
            {
                string text = string.Format(GetMsg("СообщениеИгрокуТопПромо", 0), "рейдер", config.TopWPlayersPromo.TopRaiderPromo, config.TopWPlayersPromo.StoreUrl);
                if (config.TopWPlayersPromo.TopRaiderPromoAtt != "none") text = text + "&attachments=" + config.TopWPlayersPromo.TopRaiderPromoAtt;
                string reciver = usersdata.VKUsersData[traider].VkID;
                SendVkMessage(reciver, text);
            }
            if (tkiller != 0 && config.TopWPlayersPromo.TopPlPromoGift && usersdata.VKUsersData.ContainsKey(tkiller) && usersdata.VKUsersData[tkiller].Confirmed)
            {
                string text = string.Format(GetMsg("СообщениеИгрокуТопПромо", 0), "киллер", config.TopWPlayersPromo.TopKillerPromo, config.TopWPlayersPromo.StoreUrl);
                if (config.TopWPlayersPromo.TopKillerPromoAtt != "none") text = text + "&attachments=" + config.TopWPlayersPromo.TopKillerPromoAtt;
                string reciver = usersdata.VKUsersData[tkiller].VkID;
                SendVkMessage(reciver, text);
            }
            if (tfarmer != 0 && config.TopWPlayersPromo.TopPlPromoGift && usersdata.VKUsersData.ContainsKey(tfarmer) && usersdata.VKUsersData[tfarmer].Confirmed)
            {
                string text = string.Format(GetMsg("СообщениеИгрокуТопПромо", 0), "фармер", config.TopWPlayersPromo.TopFarmerPromo, config.TopWPlayersPromo.StoreUrl);
                if (config.TopWPlayersPromo.TopFarmerPromoAtt != "none") text = text + "&attachments=" + config.TopWPlayersPromo.TopFarmerPromoAtt;
                string reciver = usersdata.VKUsersData[tfarmer].VkID;
                SendVkMessage(reciver, text);
            }
        }
        private string PromoGenerator()
        {
            List<string> Chars = new List<string>() { "A", "1", "B", "2", "C", "3", "D", "4", "F", "5", "G", "6", "H", "7", "I", "8", "J", "9", "K", "0", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
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
            Config.WriteObject(config, true);
            string msg = "[VKBot]";
            if (config.MltServSet.MSSEnable) msg = msg + " [Сервер " + config.MltServSet.ServerNumber.ToString() + "]";
            msg = msg + " В настройки добавлены новые промокоды: \nТоп рейдер - " + config.TopWPlayersPromo.TopRaiderPromo + "\nТоп киллер - " + config.TopWPlayersPromo.TopKillerPromo + "\nТоп фармер - " + config.TopWPlayersPromo.TopFarmerPromo;
            SendVkMessage(config.AdmNotify.VkID, msg);
        }
        #endregion

        #region DynamicLabelVK
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
                else { url = url + "&t1=" + PrepareStatus(config.DGLSet.DLText1, "label"); }
            }
            if (config.DGLSet.DLText2 != "none")
            {
                if (count == 0)
                {
                    url = url + "t2=" + PrepareStatus(config.DGLSet.DLText2, "label");
                    count++;
                }
                else { url = url + "&t2=" + PrepareStatus(config.DGLSet.DLText2, "label"); }
            }
            if (config.DGLSet.DLText3 != "none")
            {
                if (count == 0)
                {
                    url = url + "t3=" + PrepareStatus(config.DGLSet.DLText3, "label");
                    count++;
                }
                else { url = url + "&t3=" + PrepareStatus(config.DGLSet.DLText3, "label"); }
            }
            if (config.DGLSet.DLText4 != "none")
            {
                if (count == 0)
                {
                    url = url + "t4=" + PrepareStatus(config.DGLSet.DLText4, "label");
                    count++;
                }
                else { url = url + "&t4=" + PrepareStatus(config.DGLSet.DLText4, "label"); }
            }
            if (config.DGLSet.DLText5 != "none")
            {
                if (count == 0)
                {
                    url = url + "t5=" + PrepareStatus(config.DGLSet.DLText5, "label");
                    count++;
                }
                else { url = url + "&t5=" + PrepareStatus(config.DGLSet.DLText5, "label"); }
            }
            if (config.DGLSet.DLText6 != "none")
            {
                if (count == 0)
                {
                    url = url + "t6=" + PrepareStatus(config.DGLSet.DLText6, "label");
                    count++;
                }
                else { url = url + "&t6=" + PrepareStatus(config.DGLSet.DLText6, "label"); }
            }
            if (config.DGLSet.DLText7 != "none")
            {
                if (count == 0)
                {
                    url = url + "t7=" + PrepareStatus(config.DGLSet.DLText7, "label");
                    count++;
                }
                else { url = url + "&t7=" + PrepareStatus(config.DGLSet.DLText7, "label"); }
            }
            if (config.TopWPlayersPromo.TopWPlEnabled && config.DGLSet.TPLabel)
            {
                var tr = GetTopRaider();
                var tk = GetTopKiller();
                var tf = GetTopFarmer();
                if (tf != 0) url = url + "&tfarmer=" + tf.ToString();
                if (tk != 0) url = url + "&tkiller=" + tk.ToString();
                if (tr != 0) url = url + "&traider=" + tr.ToString();
            }
            webrequest.Enqueue(url, null, (code, response) => DLResult(code, response), this);
        }
        private void DLResult(int code, string response)
        {
            if (response.Contains("good")) { Puts("Обложка группы обновлена"); }
            else { Puts("Прозошла ошибка обновления обложки, проверьте настройки."); }
        }
        private void ULabel(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin != true) { return; }
            if (config.DGLSet.DLEnable && config.DGLSet.DLUrl != "none")
            {
                if (config.DGLSet.DLMSEnable) { UpdateMultiServerStatus("label"); }
                else { UpdateVKLabel(); }
            }
            else { PrintWarning($"Функция обновления обложки отключена, или не указана ссылка на скрипт обновления."); }
        }
        private void UpdateLabelMultiServer(string text)
        {
            string url = config.DGLSet.DLUrl + "?t1=" + text;
            webrequest.Enqueue(url, null, (code, response) => DLResult(code, response), this);
        }
        #endregion

        #region GUIBuilder
        private CuiElement BPanel(string name, string color, string anMin, string anMax, string parent = "Hud", bool cursor = false, float fade = 1f)
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiImageComponent { Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade, Color = color },
                    new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            if (cursor) Element.Components.Add(new CuiNeedsCursorComponent());
            return Element;
        }
        private CuiElement Panel(string name, string color, string anMin, string anMax, string parent = "Hud", bool cursor = false, float fade = 1f)
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiImageComponent { FadeIn = fade, Color = color },
                    new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            if (cursor) Element.Components.Add(new CuiNeedsCursorComponent());
            return Element;
        }
        private CuiElement Text(string parent, string color, string text, TextAnchor pos, int fsize, string anMin = "0 0", string anMax = "1 1", string fname = "robotocondensed-bold.ttf", float fade = 3f)
        {
            var Element = new CuiElement()
            {
                Parent = parent,
                Components =
                {
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
                Components =
                {
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
                Components =
                {
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
                Components =
                {
                    new CuiInputFieldComponent
                        {
                            Align = pos,
                            CharsLimit = chlimit,
                            FontSize = fsize,
                            Command = command + text,
                            IsPassword = psvd,
                            Text = text
                        },
                    new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        private void UnloadAllGUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MainUI");
                CuiHelper.DestroyUi(player, "HelpUI");
                CuiHelper.DestroyUi(player, "AddVKUI");
                CuiHelper.DestroyUi(player, "CodeSendedUI");
                CuiHelper.DestroyUi(player, "ReportGUI");
                CuiHelper.DestroyUi(player, "PListGUI");
            }
        }
        #endregion

        #region MenuGUI
        private string UserName(string name)
        {
            if (name.Length > 15) name = name.Remove(12) + "...";
            return name;
        }
        private void StartVKBotAddVKGUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("AddVKUI", GetColor(config.GUISet.BgColor), config.GUISet.AnchorMin, config.GUISet.AnchorMax, "Hud", true));//Окно добавления профиля ВК        
            container.Add(Image("AddVKUI", config.GUISet.Logo, "0.01 0.6", "0.99 0.99"));//Лого сервера
            container.Add(Text("AddVKUI", "1 1 1 1", UserName(player.displayName), TextAnchor.MiddleCenter, 18, "0.01 0.5", "0.99 0.6"));//Никнейм игрока            
            container.Add(Text("AddVKUI", "1 1 1 1", "Укажите ссылку на страницу ВК\nв поле ниже и нажмите ENTER", TextAnchor.MiddleCenter, 18, "0.01 0.33", "0.99 0.5"));//Описание
            container.Add(Panel("back", "0 0.115 0 0.65", "0.01 0.13", "0.99 0.3", "AddVKUI"));//Подложка
            container.Add(Input("addvkinput", "back", 18, "vk.menugui addvkgui.addvk "));//Поле ввода
            container.Add(Button("AddVKcloseGUI", "AddVKUI", "vk.menugui addvkgui.close", GetColor(config.GUISet.bCloseColor), "0.01 0.01", "0.99 0.06"));//Кнопка закрыть
            container.Add(Text("AddVKcloseGUI", "1 1 1 1", "Закрыть", TextAnchor.MiddleCenter, 18));//Текст кнопки закрыть
            CuiHelper.AddUi(player, container);
        }
        private void StartVKBotMainGUI(BasePlayer player)
        {
            bool NeedRemove = false;
            string addvkbuutontext = "Добавить профиль ВК";
            string addvkbuttoncommand = "vk.menugui maingui.addvk";
            string addvkbuttoncolor = GetColor(config.GUISet.bMenuColor);
            string giftvkbuutontext = "Получить награду за\nвступление в группу ВК";
            string giftvkbuttoncommand = "vk.menugui maingui.gift";
            string giftvkbuttoncolor = GetColor(config.GUISet.bMenuColor);
            string addvkbuttonanmax = "0.99 0.5";
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("MainUI", GetColor(config.GUISet.BgColor), config.GUISet.AnchorMin, config.GUISet.AnchorMax, "Hud", true));//Главное окно             
            container.Add(Image("MainUI", config.GUISet.Logo, "0.01 0.6", "0.99 0.99"));//Лого сервера
            container.Add(Text("MainUI", "1 1 1 1", UserName(player.displayName), TextAnchor.MiddleCenter, 18, "0.01 0.5", "0.99 0.6"));//Никнейм игрока
            if (usersdata.VKUsersData.ContainsKey(player.userID))
            {
                if (!usersdata.VKUsersData[player.userID].Confirmed) { addvkbuutontext = "Подтвердить профиль"; addvkbuttoncommand = "vk.menugui maingui.confirm"; NeedRemove = true; addvkbuttonanmax = "0.49 0.5"; }
                else { addvkbuutontext = "Профиль добавлен"; addvkbuttoncommand = ""; addvkbuttoncolor = "0 0.115 0 0.65"; }
                if (usersdata.VKUsersData[player.userID].GiftRecived) { giftvkbuutontext = "Награда за вступление\nв группу ВК получена"; giftvkbuttoncommand = ""; giftvkbuttoncolor = "0 0.115 0 0.65"; }
            }
            container.Add(Button("VKAddButton", "MainUI", addvkbuttoncommand, addvkbuttoncolor, "0.01 0.43", addvkbuttonanmax));//Кнопка добавления профиля
            container.Add(Text("VKAddButton", "1 1 1 1", addvkbuutontext, TextAnchor.MiddleCenter, 18));//Текст кнопки добавления профиля
            if (NeedRemove)
            {
                container.Add(Button("VKRemoveButton", "MainUI", "vk.menugui maingui.removevk", addvkbuttoncolor, "0.51 0.43", "0.99 0.5"));//Кнопка удаления профиля
                container.Add(Text("VKRemoveButton", "1 1 1 1", "Удалить профиль", TextAnchor.MiddleCenter, 18));//Текст кнопки удалить профиль
            }
            if (config.GrGifts.VKGroupGifts)
            {
                container.Add(Button("VKGiftButton", "MainUI", giftvkbuttoncommand, giftvkbuttoncolor, "0.01 0.3", "0.99 0.42"));//Кнопка получения награды
                container.Add(Text("VKGiftButton", "1 1 1 1", giftvkbuutontext, TextAnchor.MiddleCenter, 18));//Текст кнопки получения награды
            }
            if (!config.WipeStg.WCMDIgnore)
            {
                string text = "Подписаться на\nоповещения о вайпе в ВК";
                if (usersdata.VKUsersData.ContainsKey(player.userID) && usersdata.VKUsersData[player.userID].WipeMsg) { text = "Отписаться от\nопвещений о вайпе в ВК"; }
                container.Add(Button("VKWipeAlertsButton", "MainUI", "vk.menugui maingui.walert", GetColor(config.GUISet.bMenuColor), "0.01 0.18", "0.99 0.29"));//Кнопка подписки на оповещения о вайпе
                container.Add(Text("VKWipeAlertsButton", "1 1 1 1", text, TextAnchor.MiddleCenter, 18));//Текст кнопки подписки на оповещения о вайпе
            }
            container.Add(Text("MainUI", "1 1 1 1", $"Группа сервера в ВК:\n<color=#049906>{config.GrGifts.VKGroupUrl}</color>", TextAnchor.MiddleCenter, 18, "0.01 0.06", "0.99 0.17"));//Ссылка на группу сервера          
            container.Add(Button("VKcloseGUI", "MainUI", "vk.menugui maingui.close", GetColor(config.GUISet.bCloseColor), "0.01 0.01", "0.99 0.06"));//Кнопка закрыть
            container.Add(Text("VKcloseGUI", "1 1 1 1", "Закрыть", TextAnchor.MiddleCenter, 18));//Текст кнопки закрыть
            CuiHelper.AddUi(player, container);
        }
        private void StartVKBotHelpVKGUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("HelpUI", GetColor(config.GUISet.BgColor), config.GUISet.AnchorMin, config.GUISet.AnchorMax, "Hud", true));//Основное окно
            container.Add(Image("HelpUI", config.GUISet.Logo, "0.01 0.6", "0.99 0.99"));//Лого сервера
            container.Add(Text("HelpUI", "1 1 1 1", UserName(player.displayName), TextAnchor.MiddleCenter, 18, "0.01 0.5", "0.99 0.6"));//Никнейм игрока
            container.Add(Text("HelpUI", "1 1 1 1", "Наш бот не может отправить вам сообщение.\nОтправьте в сообщения группы слово <color=#049906>ПОДАРОК</color>\nи нажмите кнопку <color=#049906>ПОЛУЧИТЬ КОД</color>", TextAnchor.MiddleCenter, 18, "0.01 0.23", "0.99 0.5"));//Текст
            container.Add(Button("VKsendGUI", "HelpUI", "vk.menugui helpgui.confirm", GetColor(config.GUISet.bSendColor), "0.01 0.08", "0.99 0.2"));//Кнопка получить код
            container.Add(Text("VKsendGUI", "1 1 1 1", "Получить код", TextAnchor.MiddleCenter, 18));//Текст кнопки получить код
            container.Add(Button("VKcloseGUI", "HelpUI", "vk.menugui helpgui.close", GetColor(config.GUISet.bCloseColor), "0.01 0.01", "0.99 0.06"));//Кнопка закрыть
            container.Add(Text("VKcloseGUI", "1 1 1 1", "Закрыть", TextAnchor.MiddleCenter, 18));//Текст кнопки закрыть
            CuiHelper.AddUi(player, container);
        }
        private void StartCodeSendedGUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("CodeSendedUI", GetColor(config.GUISet.BgColor), config.GUISet.AnchorMin, config.GUISet.AnchorMax, "Hud", true));//Основное окно
            container.Add(Image("CodeSendedUI", config.GUISet.Logo, "0.01 0.6", "0.99 0.99"));//Лого сервера
            container.Add(Text("CodeSendedUI", "1 1 1 1", UserName(player.displayName), TextAnchor.MiddleCenter, 18, "0.01 0.5", "0.99 0.6"));//Никнейм игрока
            container.Add(Text("CodeSendedUI", "1 1 1 1", "На вашу страницу ВК отправлено\nсообщение с дальнейшими инструкциями.", TextAnchor.MiddleCenter, 18, "0.01 0.23", "0.99 0.5"));//Текст
            container.Add(Button("VKcloseGUI", "CodeSendedUI", "vk.menugui csendui.close", GetColor(config.GUISet.bCloseColor), "0.01 0.01", "0.99 0.06"));//Кнопка закрыть
            container.Add(Text("VKcloseGUI", "1 1 1 1", "Закрыть", TextAnchor.MiddleCenter, 18));//Текст кнопки закрыть
            CuiHelper.AddUi(player, container);
        }
        [ConsoleCommand("vk.menugui")]
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
                case "maingui.removevk":
                    CuiHelper.DestroyUi(player, "MainUI");
                    if (usersdata.VKUsersData.ContainsKey(player.userID)) { usersdata.VKUsersData.Remove(player.userID); VKBData.WriteObject(usersdata); }
                    break;
                case "maingui.walert":
                    WAlert(player);
                    break;
                case "maingui.gift":
                    CuiHelper.DestroyUi(player, "MainUI");
                    FixedGifts(player);
                    break;
                case "maingui.confirm":
                    CuiHelper.DestroyUi(player, "MainUI");
                    SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /vk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player);
                    break;
                case "addvkgui.close":
                    CuiHelper.DestroyUi(player, "AddVKUI");
                    break;
                case "addvkgui.addvk":
                    string url = string.Join(" ", arg.Args.Skip(1).ToArray());
                    if (!url.Contains("vk.com/")) { PrintToChat(player, string.Format(GetMsg("НеправильнаяСсылка", player))); return; }
                    CuiHelper.DestroyUi(player, "AddVKUI");
                    CheckVkUser(player, url);
                    break;
                case "helpgui.close":
                    CuiHelper.DestroyUi(player, "HelpUI");
                    break;
                case "helpgui.confirm":
                    CuiHelper.DestroyUi(player, "HelpUI");
                    SendConfCode(usersdata.VKUsersData[player.userID].VkID, $"Для подтверждения вашего ВК профиля введите в игровой чат команду /vk confirm {usersdata.VKUsersData[player.userID].ConfirmCode}", player);
                    break;
                case "csendui.close":
                    CuiHelper.DestroyUi(player, "CodeSendedUI");
                    break;
            }
        }
        private void FixedGifts(BasePlayer player)
        {
            if (GiftsList.ContainsKey(player))
            {
                TimeSpan interval = DateTime.Now - GiftsList[player];
                if (interval.TotalSeconds < 15) { PrintToChat(player, "Слишком часто. Попробуйте позже."); return; }
                else { GiftsList[player] = DateTime.Now;  VKGift(player); }
            }
            else
            {
                GiftsList.Add(player, DateTime.Now);
                VKGift(player);
            }
        }
        #endregion

        #region ReportGUI
        private List<BasePlayer> OpenReportUI = new List<BasePlayer>();
        private void ReportGUI(BasePlayer player, BasePlayer target = null)
        {
            string chpl = "\nЕсли хотите отправить жалобу на игрока, сначала нажмите на кнопку <color=#ff0000>ВЫБРАТЬ ИГРОКА</color>";
            if (target != null) chpl = $"\nЖалоба на игрока <color=#ff0000>{target.displayName}</color>";
            string title = "<color=#ff0000>" + config.AdmNotify.ReportsNotify + "</color>" + chpl + "\nВведите ваше сообщение в поле ниже и нажмите <color=#ff0000>ENTER</color>";
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("ReportGUI", "0 0 0 0.75", "0.2 0.125", "0.8 0.9", "Hud", true));
            container.Add(Panel("header", "0 0 0 0.75", "0 0.93", "1 1", "ReportGUI"));
            container.Add(Text("header", "1 1 1 1", "Отправка сообщения администратору", TextAnchor.MiddleCenter, 20));
            container.Add(Button("close", "header", "vk.report close", "1 0 0 1", "0.94 0.01", "1.0 0.98"));
            container.Add(Text("close", "1 1 1 1", "X", TextAnchor.MiddleCenter, 20));
            container.Add(Panel("text", "0 0 0 0.75", "0 0.77", "1 0.93", "ReportGUI"));
            container.Add(Text("text", "1 1 1 1", title, TextAnchor.MiddleCenter, 18));
            if (target == null)
            {
                container.Add(Button("PlayerChoise", "ReportGUI", "vk.report choiceplayer", "0.7 1 0.6 0.4", "0.378 0.71", "0.628 0.76"));
                container.Add(Text("PlayerChoise", "1 1 1 1", "ВЫБРАТЬ ИГРОКА", TextAnchor.MiddleCenter, 18));
            }
            container.Add(Panel("inputbg", "0 0.115 0 0.65", "0 0", "1 0.698", "ReportGUI"));
            string command = "vk.report send ";
            if (target != null) command = command + target.userID + " ";
            container.Add(Input("reportinput", "inputbg", 18, command));
            OpenReportUI.Add(player);
            CuiHelper.AddUi(player, container);
        }
        [ConsoleCommand("vk.report")]
        private void ReportGUIChoose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (!config.AdmNotify.SendReports) { PrintToChat(player, string.Format(GetMsg("ФункцияОтключена", player))); return; }
            if (arg.Args == null) return;
            switch (arg.Args[0])
            {
                case "close":
                    if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player);
                    CuiHelper.DestroyUi(player, "ReportGUI");
                    break;
                case "choiceplayer":
                    if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player);
                    CuiHelper.DestroyUi(player, "ReportGUI");
                    PListUI(player);
                    break;
                case "send":
                    if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player);
                    CuiHelper.DestroyUi(player, "ReportGUI");
                    CheckReport(player, arg.Args);
                    break;
            }
        }
        private object OnServerCommand(ConsoleSystem.Arg arg) //блочим команды при наборе текста репорт
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.cmd == null) return null;
            if (OpenReportUI.Contains(player) && !arg.cmd.FullName.ToLower().StartsWith("vk.report")) return true;
            return null;
        }
        private object OnPlayerCommand(ConsoleSystem.Arg arg) //блочим команды при наборе текста репорт
        {
            var player = (BasePlayer)arg.Connection.player;
            if (player != null)
            {
                if (OpenReportUI.Contains(player) && !arg.cmd.FullName.ToLower().StartsWith("vk.report")) return true;
            }
            return null;
        }
        #endregion

        #region PlayersListGUI
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
                        else { PrintToChat(player, string.Format(GetMsg("ИгрокНеНайден", player))); return; }
                    }
                    else { PrintToChat(player, string.Format(GetMsg("ИгрокНеНайден", player))); return; }
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
            string text = null;
            text = "Выберите игрока на которого хотите пожаловаться.";
            List<BasePlayer> players = new List<BasePlayer>();
            foreach (var pl in BasePlayer.activePlayerList)
            {
                if (pl == player) continue;
                players.Add(pl);
            }
            if (players.Count == 0) { PrintToChat(player, "На сервере нет никого кроме вас."); if (OpenReportUI.Contains(player)) OpenReportUI.Remove(player); return; }
            players = players.OrderBy(x => x.displayName).ToList();
            int maxPages = CalculatePages(players.Count);
            string pageNum = (maxPages > 1) ? $" - {GUIManager.Get(player).Page}" : "";
            CuiElementContainer container = new CuiElementContainer();
            container.Add(BPanel("PListGUI", "0 0 0 0.75", "0.2 0.125", "0.8 0.9", "Hud", true));
            container.Add(Panel("header", "0 0 0 0.75", "0 0.93", "1 1", "PListGUI"));
            if (maxPages != 1) text = text + " Страница " + pageNum.ToString();
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
                    string playerName = players.ToArray()[playerCount].displayName;
                    string id = players.ToArray()[playerCount].UserIDString;
                    container.Add(Panel($"pn{id}", "0 0 0 0", AnchorMin, AnchorMax, "playerslist"));
                    container.Add(Button($"plbtn{id}", $"pn{id}", $"vk.pllist report {id}", "0 0 0 0.85", "0.05 0.05", "0.95 0.95"));
                    container.Add(Text($"plbtn{id}", "1 1 1 1", UserName(playerName), TextAnchor.MiddleCenter, 18));
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
                if (Players.ContainsKey(player)) return Players[player];
                Players.Add(player, new GUIManager());
                return Players[player];
            }
        }
        #endregion

        #region Langs
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ПоздравлениеИгрока", "<size=17><color=#049906>Администрация сервера поздравляет вас с днем рождения! В качестве подарка мы добавили вас в группу с рейтами x4 и китом bday!</color></size>"},
                {"ДеньРожденияИгрока", "<size=17>Администрация сервера поздравляет игрока <color=#049906>{0}</color> с Днем Рождения!</size>"},
                {"РепортОтправлен", "<size=17>Ваше сообщение было отправлено администратору.\n<color=#049906>ВНИМАНИЕ!</color>\n{0}</size>"},
                {"КомандаРепорт", "<size=17>Введите команду <color=#049906>/report сообщение</color>.\n<color=#049906>ВНИМАНИЕ!</color>\n{0}</size>"},
                {"ФункцияОтключена", "<size=17><color=#049906>Данная функция отключена администратором.</color>.</size>"},
                {"ПрофильДобавленИПодтвержден", "<size=17>Вы уже добавили и подтвердили свой профиль.</size>"},
                {"ПрофильДобавлен", "<size=17>Вы уже добавили свой профиль. Если вам не пришел код подтверждения, введите команду <color=#049906>/vk confirm</color></size>"},
                {"ДоступныеКоманды", "<size=17>Список доступных команд:\n /vk add ссылка на вашу страницу - добавление вашего профиля ВК в базу.\n /vk confirm - подтверждение вашего профиля ВК</size>"},
                {"НеправильнаяСсылка", "<size=17>Ссылка на страницу должна быть вида |vk.com/testpage| или |vk.com/id0000|</size>"},
                {"ПрофильПодтвержден", "<size=17>Вы подтвердили свой профиль! Спасибо!</size>"},
                {"ОповещениеОПодарках", "<size=17>Вы можете получить награду, если вступили в нашу группу <color=#049906>{0}</color> введя команду <color=#049906>/vk gift.</color></size>"},
                {"НеверныйКод", "<size=17>Неверный код подтверждения.</size>"},
                {"ПрофильНеДобавлен", "<size=17>Сначала добавьте и подтвердите свой профиль командой <color=#049906>/vk add ссылка на вашу страницу.</color> Ссылка на должна быть вида |vk.com/testpage| или |vk.com/id0000|</size>"},
                {"КодОтправлен", "<size=17>Вам был отправлен код подтверждения. Если сообщение не пришло, зайдите в группу <color=#049906>{0}</color> и напишите любое сообщение. После этого введите команду <color=#049906>/vk confirm</color></size>"},
                {"ПрофильНеПодтвержден", "<size=17>Сначала подтвердите свой профиль ВК командой <color=#049906>/vk confirm</color></size>"},
                {"НаградаУжеПолучена", "<size=17>Вы уже получили свою награду.</size>"},
                {"ПодпискаОтключена", "<size=17>Вы <color=#049906>отключили</color> подписку на сообщения о вайпах сервера. Что бы включить подписку снова, введите команду <color=#049906>/vk wipealerts</color></size>"},
                {"ПодпискаВключена", "<size=17>Вы <color=#049906>включили</color> подписку на сообщения о вайпах сервера. Что бы отключить подписку, введите команду <color=#049906>/vk wipealerts</color></size>"},
                {"НаградаПолучена", "<size=17>Вы получили свою награду! Проверьте инвентарь!</size>"},
                {"ПолучилНаграду", "<size=17>Игрок <color=#049906>{0}</color> получил награду за вступление в группу <color=#049906>{1}.</color>\nХочешь тоже получить награду? Введи в чат команду <color=#049906>/vk gift</color>.</size>"},
                {"НетМеста", "<size=17>Недостаточно места для получения награды.</size>"},
                {"НаградаПолученаКоманда", "<size=17>За вступление в группу нашего сервера вы получили {0}</size>"},
                {"НеВступилВГруппу", "<size=17>Вы не являетесь участником группы <color=#049906>{0}</color></size>"},
                {"ОтветНаРепортЧат", "<size=17><color=#049906>Администратор ответил на ваше сообщение:</color>\n</size>"},
                {"ОтветНаРепортВК", "<size=17><color=#049906>Администратор ответил на ваше сообщение:</color>\n</size>"},
                {"ИгрокНеНайден", "<size=17>Игрок не найден</size>"},
                {"СообщениеИгрокуТопПромо", "Поздравляем! Вы Топ {0} по результатам этого вайпа! В качестве награды, вы получаете промокод {1} на баланс в нашем магазине! {2}"},
                {"АвтоОповещенияОвайпе", "<size=17>Сервер рассылает оповещения о вайпе всем. Подписка не требуется</size>"}
            }, this);
        }
        string GetMsg(string key, BasePlayer player = null) => GetMsg(key, player.UserIDString);
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        #endregion
    }
}