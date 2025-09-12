using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("FastMenu", "Magistr", "0.0.2")]
    class FastMenu : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, ServerRewards, Economics;
        private void OnServerInitialized(){if (ImageLibrary == null)return;ImageLibrary.Call("AddImage", configData.uimage, "logotp");}
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Размер текста заголовка")]
            public int btsize;

            [JsonProperty(PropertyName = "Текстовое содержание заголовка")]
            public string textheader;

            [JsonProperty(PropertyName = "Минимальное смещение заголовка")]
            public string titlemin;

            [JsonProperty(PropertyName = "Максимальное смещение заголовка")]
            public string titlemax;

            [JsonProperty(PropertyName = "Минимальное смещение фона")]
            public string fmin;

            [JsonProperty(PropertyName = "Максимальное смещение фона")]
            public string fmax;

            [JsonProperty(PropertyName = "Фоновый цвет")]
            public string fys;

            [JsonProperty(PropertyName = "Дискорд")]
            public string Discord;

            [JsonProperty(PropertyName = "Левая колонка")]
            public string form;

            [JsonProperty(PropertyName = "средний титул 1")]
            public string average1;

            [JsonProperty(PropertyName = "средний титул 2")]
            public string average2;

            [JsonProperty(PropertyName = "средний титул 3")]
            public string average3;

            [JsonProperty(PropertyName = "Правая колонка")]
            public string form1;

            [JsonProperty(PropertyName = "ссылка фото")]
            public string uimage;

            [JsonProperty(PropertyName = "Минимальное смещение изображения")]
            public string biasmin;

            [JsonProperty(PropertyName = "Максимальное смещение изображения")]
            public string biasmax;

            [JsonProperty(PropertyName = "Цвет изображения")]
            public string pcolor;

            [JsonProperty(PropertyName = "размер текста кнопки закрытия")]
            public int guanbisize;

            [JsonProperty(PropertyName = "текст кнопки закрытия")]
            public string guanbiwz;

            [JsonProperty(PropertyName = "цвет кнопки закрытия")]
            public string guanbiys;

            [JsonProperty(PropertyName = "минимальное смещение кнопки закрытия")]
            public string guanbimin;

            [JsonProperty(PropertyName = "максимальное смещение кнопки закрытия")]
            public string guanbimax;

            [JsonProperty(PropertyName = "Функция средней кнопки")]
            public bool mbutton = true;

            [JsonProperty(PropertyName = "Игроки Онлайн")]
            public bool ponline = true;

            [JsonProperty(PropertyName = "Игровое время")]
            public bool yxtime = true;

            [JsonProperty(PropertyName = "Актуальное время")]
            public bool xstime = true;

            [JsonProperty(PropertyName = "Баланс монет")]
            public bool cobal = true;

            [JsonProperty(PropertyName = "Баланс баллов")]
            public bool pbal = true;

           [JsonProperty(PropertyName = "= = = = = = = = = = = = = [ настройки кнопки] = = = = = = = = = = = = =")]
            public List<Ndata> Installer;
            
        }

         class Ndata
        {
            [JsonProperty(PropertyName = "Размер текста кнопки")]
            public int ControlSize;

            [JsonProperty(PropertyName = "Цвет кнопки")]
            public string Colors;

            [JsonProperty(PropertyName = "Размер  текста")]
            public string TextSize;

            [JsonProperty(PropertyName = "Цвет текста кнопки")]
            public string Bcolor;

            [JsonProperty(PropertyName = "Минимальное смещение кнопки")]
            public string ButtonoffsetMin;

            [JsonProperty(PropertyName = "Максимальное смещение кнопки")]
            public string ButtonoffsetMax;

            [JsonProperty(PropertyName = "кнопка команды")]
            public string AddCommand;

        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                btsize = 45,
                textheader = "<color=#FFcc00>БыстроеМеню</color>",
                titlemin = "0.025 0.9",
                titlemax = "0.65 0.98",
                fmin = "0.025 0.05",
                fmax = "0.975 0.95",
                fys = "0 0 0 .5",
                Discord = "<size=26><color=#FFFFFFFF>Заходите ко мне в Дискорд</color></size>",
                form = "<size=20><color=#FFFFFFFF>Вип</color></size>",
                average1 = "<size=18><color=#FFFFFFFF>Вызов Транспорта</color></size>",
                average2 = "<size=20><color=#FFFFFFFF>Помощь</color></size>",
                average3 = "<size=20><color=#FFFFFFFF>Настройки</color></size>",
                form1 = "<size=20><color=#FFFFFFFF>Перемещения</color></size>",
                biasmin = "0.4 0.02",
                biasmax = "0.55 0.2",
                pcolor = "1 1 1 0.8",
                uimage = "",
                guanbisize = 26,
                guanbiwz = "Закрыть",
                guanbiys = "1 1 1 .7",
                guanbimin = "0.888 0.888",
                guanbimax = "0.99 0.99",
                Installer = new List<Ndata>        
			   {
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 14, TextSize = "горизонтальная линия",      ButtonoffsetMin = "0.025 0.899", ButtonoffsetMax = "0.35 0.9",AddCommand = ""},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "РТ",    ButtonoffsetMin = "0.02 0.76", ButtonoffsetMax = "0.16 0.82",AddCommand = "/remove"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Кит",   ButtonoffsetMin = "0.17 0.76", ButtonoffsetMax = "0.31 0.82",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "BP",    ButtonoffsetMin = "0.02 0.69", ButtonoffsetMax = "0.16 0.75",AddCommand = "/backpack"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Shop",  ButtonoffsetMin = "0.17 0.69", ButtonoffsetMax = "0.31 0.75",AddCommand = "/shop"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add1",  ButtonoffsetMin = "0.02 0.62", ButtonoffsetMax = "0.16 0.68",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add2",  ButtonoffsetMin = "0.17 0.62", ButtonoffsetMax = "0.31 0.68",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add3",  ButtonoffsetMin = "0.02 0.55", ButtonoffsetMax = "0.16 0.61",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add4",  ButtonoffsetMin = "0.17 0.55", ButtonoffsetMax = "0.31 0.61",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add5",  ButtonoffsetMin = "0.02 0.48", ButtonoffsetMax = "0.16 0.54",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add6",  ButtonoffsetMin = "0.17 0.48", ButtonoffsetMax = "0.31 0.54",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "add7",  ButtonoffsetMin = "0.02 0.41", ButtonoffsetMax = "0.31 0.47",AddCommand = "/kit"},
                    //меню вип настроек
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "горизонтальная линия",  ButtonoffsetMin = "0.02 0.349", ButtonoffsetMax = "0.15 0.35",AddCommand = ""},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип1",  ButtonoffsetMin = "0.02 0.28", ButtonoffsetMax = "0.16 0.34",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип2",  ButtonoffsetMin = "0.17 0.28", ButtonoffsetMax = "0.31 0.34",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип3",  ButtonoffsetMin = "0.02 0.21", ButtonoffsetMax = "0.16 0.27",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип4",  ButtonoffsetMin = "0.17 0.21", ButtonoffsetMax = "0.31 0.27",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип5",  ButtonoffsetMin = "0.02 0.14", ButtonoffsetMax = "0.16 0.20",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Вип6",  ButtonoffsetMin = "0.17 0.14", ButtonoffsetMax = "0.31 0.20",AddCommand = "/kit"},
                    //добавление разного транспорта
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "горизонтальная линия",  ButtonoffsetMin = "0.33 0.799", ButtonoffsetMax = "0.46 0.80",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Разблок авто",  ButtonoffsetMin = "0.52 0.80", ButtonoffsetMax = "0.63 0.84",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle1",  ButtonoffsetMin = "0.33 0.75", ButtonoffsetMax = "0.42 0.79",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.425 0.75", ButtonoffsetMax = "0.475 0.79",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle2",  ButtonoffsetMin = "0.485 0.75", ButtonoffsetMax = "0.575 0.79",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.58 0.75", ButtonoffsetMax = "0.63 0.79",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle3",  ButtonoffsetMin = "0.33 0.70", ButtonoffsetMax = "0.42 0.74",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.425 0.70", ButtonoffsetMax = "0.475 0.74",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle4",  ButtonoffsetMin = "0.485 0.70", ButtonoffsetMax = "0.575 0.74",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.58 0.70", ButtonoffsetMax = "0.63 0.74",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle5",  ButtonoffsetMin = "0.33 0.65", ButtonoffsetMax = "0.42 0.69",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.425 0.65", ButtonoffsetMax = "0.475 0.69",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle6",  ButtonoffsetMin = "0.485 0.65", ButtonoffsetMax = "0.575 0.69",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.58 0.65", ButtonoffsetMax = "0.63 0.69",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle7",  ButtonoffsetMin = "0.485 0.60", ButtonoffsetMax = "0.575 0.64",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.58 0.60", ButtonoffsetMax = "0.63 0.64",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "vehicle8",  ButtonoffsetMin = "0.33 0.60", ButtonoffsetMax = "0.42 0.64",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "kill",  ButtonoffsetMin = "0.425 0.60", ButtonoffsetMax = "0.475 0.64",AddCommand = "/kit"},
                    //колонка различной помощи
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "горизонтальная линия",  ButtonoffsetMin = "0.33 0.549", ButtonoffsetMax = "0.46 0.55",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Помощь 1",  ButtonoffsetMin = "0.33 0.5", ButtonoffsetMax = "0.475 0.54",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Помощь 2",  ButtonoffsetMin = "0.485 0.5", ButtonoffsetMax = "0.63 0.54",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Помощь 3",  ButtonoffsetMin = "0.33 0.45", ButtonoffsetMax = "0.475 0.49",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Помощь 4",  ButtonoffsetMin = "0.485 0.45", ButtonoffsetMax = "0.63 0.49",AddCommand = "/kit"},
                    //всякие разные настройки
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "горизонтальная линия",  ButtonoffsetMin = "0.33 0.399", ButtonoffsetMax = "0.46 0.40",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 1",  ButtonoffsetMin = "0.33 0.35", ButtonoffsetMax = "0.42 0.39",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 2",  ButtonoffsetMin = "0.425 0.35", ButtonoffsetMax = "0.525 0.39",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 3",  ButtonoffsetMin = "0.53 0.35", ButtonoffsetMax = "0.63 0.39",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 4",  ButtonoffsetMin = "0.33 0.3", ButtonoffsetMax = "0.42 0.34",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 5",  ButtonoffsetMin = "0.425 0.3", ButtonoffsetMax = "0.525 0.34",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "настройки 6",  ButtonoffsetMin = "0.53 0.3", ButtonoffsetMax = "0.63 0.34",AddCommand = "/kit"},
                    //телепорты сетхомы
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "горизонтальная линия",  ButtonoffsetMin = "0.65 0.799", ButtonoffsetMax = "0.77 0.8",AddCommand = "/kit"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "TP friend",  ButtonoffsetMin = "0.65 0.73", ButtonoffsetMax = "0.98 0.79",AddCommand = "/fmenu"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "Список домов",  ButtonoffsetMin = "0.65 0.68", ButtonoffsetMax = "0.98 0.72",AddCommand = "/home list"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.63", ButtonoffsetMax = "0.72 0.67",AddCommand = "/home add 1"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.63", ButtonoffsetMax = "0.785 0.67",AddCommand = "/home 1"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.63", ButtonoffsetMax = "0.81 0.67",AddCommand = "/home remove 1"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.63", ButtonoffsetMax = "0.89 0.67",AddCommand = "/home add 2"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.63", ButtonoffsetMax = "0.955 0.67",AddCommand = "/home 2"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.63", ButtonoffsetMax = "0.98 0.67",AddCommand = "/home remove 2"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.58", ButtonoffsetMax = "0.72 0.62",AddCommand = "/home add 3"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.58", ButtonoffsetMax = "0.785 0.62",AddCommand = "/home 3"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.58", ButtonoffsetMax = "0.81 0.62",AddCommand = "/home remove 3"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.58", ButtonoffsetMax = "0.89 0.62",AddCommand = "/home add 4"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.58", ButtonoffsetMax = "0.955 0.62",AddCommand = "/home 4"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.58", ButtonoffsetMax = "0.98 0.62",AddCommand = "/home remove 4"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.53", ButtonoffsetMax = "0.72 0.57",AddCommand = "/home add 5"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.53", ButtonoffsetMax = "0.785 0.57",AddCommand = "/home 5"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.53", ButtonoffsetMax = "0.81 0.57",AddCommand = "/home remove 5"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.53", ButtonoffsetMax = "0.89 0.57",AddCommand = "/home add 6"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.53", ButtonoffsetMax = "0.955 0.57",AddCommand = "/home 6"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.53", ButtonoffsetMax = "0.98 0.57",AddCommand = "/home remove 6"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.48", ButtonoffsetMax = "0.72 0.52",AddCommand = "/home add 7"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.48", ButtonoffsetMax = "0.785 0.52",AddCommand = "/home 7"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.48", ButtonoffsetMax = "0.81 0.52",AddCommand = "/home remove 7"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.48", ButtonoffsetMax = "0.89 0.52",AddCommand = "/home add 8"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.48", ButtonoffsetMax = "0.955 0.52",AddCommand = "/home 8"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.48", ButtonoffsetMax = "0.98 0.52",AddCommand = "/home remove 8"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.43", ButtonoffsetMax = "0.72 0.47",AddCommand = "/home add 9"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.43", ButtonoffsetMax = "0.785 0.47",AddCommand = "/home 9"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.43", ButtonoffsetMax = "0.81 0.47",AddCommand = "/home remove 9"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.43", ButtonoffsetMax = "0.89 0.47",AddCommand = "/home add 10"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.43", ButtonoffsetMax = "0.955 0.47",AddCommand = "/home 10"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.43", ButtonoffsetMax = "0.98 0.47",AddCommand = "/home remove 10"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.38", ButtonoffsetMax = "0.72 0.42",AddCommand = "/home add 11"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.38", ButtonoffsetMax = "0.785 0.42",AddCommand = "/home 11"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.38", ButtonoffsetMax = "0.81 0.42",AddCommand = "/home remove 11"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.38", ButtonoffsetMax = "0.89 0.42",AddCommand = "/home add 12"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.38", ButtonoffsetMax = "0.955 0.42",AddCommand = "/home 12"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.38", ButtonoffsetMax = "0.98 0.42",AddCommand = "/home remove 12"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.33", ButtonoffsetMax = "0.72 0.37",AddCommand = "/home add 13"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.33", ButtonoffsetMax = "0.785 0.37",AddCommand = "/home 13"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.33", ButtonoffsetMax = "0.81 0.37",AddCommand = "/home remove 13"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.33", ButtonoffsetMax = "0.89 0.37",AddCommand = "/home add 14"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.33", ButtonoffsetMax = "0.955 0.37",AddCommand = "/home 14"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.33", ButtonoffsetMax = "0.98 0.37",AddCommand = "/home remove 14"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.65 0.28", ButtonoffsetMax = "0.72 0.32",AddCommand = "/home add 15"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.725 0.28", ButtonoffsetMax = "0.785 0.32",AddCommand = "/home 15"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.79 0.28", ButtonoffsetMax = "0.81 0.32",AddCommand = "/home remove 15"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 10, TextSize = "Сохранить дом",  ButtonoffsetMin = "0.82 0.28", ButtonoffsetMax = "0.89 0.32",AddCommand = "/home add 16"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "ТП",  ButtonoffsetMin = "0.895 0.28", ButtonoffsetMax = "0.955 0.32",AddCommand = "/home 16"},
                    new Ndata {Colors = "0.8 0.8 0.8 0.6", Bcolor = "1 1 1 0.9", ControlSize = 19, TextSize = "X",  ButtonoffsetMin = "0.96 0.28", ButtonoffsetMax = "0.98 0.32",AddCommand = "/home remove 16"},
                }
            };
            SaveConfig(config);
        }
        private bool LoadConfigVariables()
        {try {configData = Config.ReadObject<ConfigData>();}
        catch {return false;} SaveConfig(configData); return true;}

        void SaveConfig(ConfigData config)
        {Config.WriteObject(config, true);}

        void Unload()
        {foreach (BasePlayer current in BasePlayer.activePlayerList){CuiHelper.DestroyUi(current, "interface");SaveData();}}

        void ui(BasePlayer player)
        {
            if (ImageLibrary == null)
            {
                PrintWarning("Плагин ImageLibrary не установлен, FastMenu не сможет работать нормально!");
                return;
            }
            CuiHelper.DestroyUi(player, "interface");
            var elements = new CuiElementContainer();
            var ponline = BasePlayer.activePlayerList.Count;
            var gtime = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");
            var ptime = System.DateTime.Now.ToString("HH:mm");
            var cobal = (Interface.Oxide.CallHook("Balance", player.UserIDString) ?? 0.0);
            var pbal = (Interface.Oxide.CallHook("CheckPoints", player.userID) ?? 0);
            var QuickUI = elements.Add(new CuiPanel {Image ={ FadeIn = 0.5f,Material = "assets/content/ui/uibackgroundblur.mat",Color = "0 0 0 0.5" },RectTransform = { AnchorMax = configData.fmax, AnchorMin = configData.fmin},CursorEnabled = true,}, "Overlay", "interface");
            
			elements.Add(new CuiLabel { Text = { Text = configData.textheader, FontSize = configData.btsize, Color = "1 1 1 1"}, RectTransform = {AnchorMin = configData.titlemin, AnchorMax = configData.titlemax} }, "interface");
            if (configData.ponline == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>Игроки онлайн: {ponline}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.17", AnchorMax = "0.98 0.20" } }, "interface");}
            if (configData.yxtime == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>Игровое время: {gtime}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.135", AnchorMax = "0.98 0.165" } }, "interface");}
            if (configData.yxtime == true){ elements.Add(new CuiLabel { Text = { Text =$"<b>Актуальное время: {ptime}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.09", AnchorMax = "0.98 0.130"} }, "interface");}
            if (Economics != null && configData.cobal == true){elements.Add(new CuiLabel { Text = { Text =$"<b>Баланс монет：{cobal}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.055", AnchorMax = "0.98 0.095"} }, "interface");}
            if (ServerRewards != null && configData.pbal == true){elements.Add(new CuiLabel { Text = { Text =$"<b>Баланс баллов：{pbal}</b>" , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.85 0.01", AnchorMax = "0.98 0.06"} }, "interface");}
            
			elements.Add(new CuiLabel { Text = { Text = configData.Discord , FontSize = 15, Color = "1 1 1 1" }, RectTransform = {AnchorMin = "0.025 0.8", AnchorMax = "0.5 0.89"} }, "interface");
            elements.Add(new CuiLabel { Text = { Text = configData.form , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.02 0.34", AnchorMax = "0.31 0.385"} }, "interface");
            elements.Add(new CuiLabel { Text = { Text = configData.average1 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.80", AnchorMax = "0.63 0.835"} }, "interface");
            elements.Add(new CuiLabel { Text = { Text = configData.average2 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.55", AnchorMax = "0.63 0.585"} }, "interface");
            elements.Add(new CuiLabel { Text = { Text = configData.average3 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.33 0.4", AnchorMax = "0.63 0.435"} }, "interface");
            elements.Add(new CuiLabel { Text = { Text = configData.form1 , FontSize = 15, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.65 0.80", AnchorMax = "0.98 0.84"} }, "interface");
            elements.Add(new CuiElement{Parent = "interface",Components ={new CuiRawImageComponent{Color = configData.pcolor, Png = (string) ImageLibrary.Call("GetImage", "logotp"),},new CuiRectTransformComponent { AnchorMin = configData.biasmin, AnchorMax = configData.biasmax },}});
            elements.Add(new CuiButton { Button = { Command = "shutdown", Color = configData.guanbiys }, RectTransform = { AnchorMax = configData.guanbimax, AnchorMin = configData.guanbimin }, Text = { Text = configData.guanbiwz, Color = "1 1 1 0.7", FontSize = configData.guanbisize, Align=TextAnchor.MiddleCenter } }, QuickUI);
            
            int Max_sl = configData.Installer.Count;
            for (int i = 0; i < Max_sl; i++)
            {var data = configData.Installer[i];{elements.Add(new CuiButton { Button = { Command = $"shutdown1 chat.say \"{data.AddCommand}\"", Color = data.Colors }, RectTransform = { AnchorMin = data.ButtonoffsetMin, AnchorMax = data.ButtonoffsetMax }, Text = { Text = data.TextSize, Color = data.Bcolor, FontSize = data.ControlSize, Align=TextAnchor.MiddleCenter } }, QuickUI);}}
            CuiHelper.AddUi(player, elements);
        }

        private StoredData _storedData; 
        void Init()
        {if (!LoadConfigVariables()) {PrintError("В файле конфигурации есть ошибка, проверьте файл конфигурации и исправьте ее! ! !"); return;} _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("FastMenu");}

        private class StoredData
        {public Hash<ulong, bool> MiddleMouseButtonEnable = new Hash<ulong, bool>();} void OnPlayerConnected(BasePlayer player) { if (!_storedData.MiddleMouseButtonEnable.ContainsKey(player.userID)) { _storedData.MiddleMouseButtonEnable[player.userID] = true;}}

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("FastMenu", _storedData);

		void OnPlayerInput(BasePlayer player, InputState input)
        {
		    if (!input.WasJustReleased(BUTTON.FIRE_THIRD) || player == null) return;if (_storedData.MiddleMouseButtonEnable[player.userID] == false) return;if (configData.mbutton == false) return;ui(player);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        [ChatCommand("q")]
        void callui(BasePlayer player)
        {
            ui(player);
        }

        [ChatCommand("z")]
        void zjgn(BasePlayer player)
        {
            if (configData.mbutton == false)
            {
                player.ChatMessage($"<color=#FFCC00>【FastMenu】</color>Администратор отключил функцию средней кнопки мыши");
                return;
            } 

            if (_storedData.MiddleMouseButtonEnable[player.userID] == true)
            {
                _storedData.MiddleMouseButtonEnable[player.userID] = false;
                player.ChatMessage($"<color=#FFCC00>【FastMenu】</color>Функция средней кнопки отключена");
            }
            else
            {
                _storedData.MiddleMouseButtonEnable[player.userID] = true;
                player.ChatMessage($"<color=#FFCC00>【FastMenu】</color>Функция средней кнопки включена");
            }
        }
        
        [ConsoleCommand("shutdown")]
        void callclose(ConsoleSystem.Arg args)
        {
            if (args.Player() == null) return;
            CuiHelper.DestroyUi(args.Player(), "interface");
        }

        [ConsoleCommand("shutdown1")]
        private void callclose1(ConsoleSystem.Arg arg)
        {
            var cmd = "";
            var player = arg.Connection.player as BasePlayer;

            if (player == null)return;
            CuiHelper.DestroyUi(player, "interface");
            cmd = string.Join(" ", arg.Args.Skip(0).ToArray());
            player.Command(cmd);
            Effect.server.Run("assets/prefabs/tools/flashlight/effects/turn_on.prefab", (BaseEntity)player, 0U, Vector3.zero, Vector3.zero);
        }
    }
}