using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AAlertRaid", "fermens", "1.0.0")]
    public class AAlertRaid : RustPlugin
    {
        public WItem DefaultBlock = new WItem("Ваш", "Строительный блок");
        
        public Dictionary<string, WItem> InfoBlocks = new Dictionary<string, WItem>()
        {
            {"floor.grill", new WItem("Ваш", "Решетчатый настил")},
            {"floor.triangle.grill", new WItem("Ваш", "Треугольный решетчатый настил")},
            {"door.hinged.toptier", new WItem("Вашу", "Бронированную дверь")},
            {"door.double.hinged.toptier", new WItem("Вашу", "Двойную бронированную дверь")},
            {"gates.external.high.stone", new WItem("Ваши", "Высокие внешние каменные ворота")},
            {"wall.external.high.stone", new WItem("Вашу", "Высокую внешнюю каменную стену")},
            {"gates.external.high.wood", new WItem("Ваши", "Высокие внешние деревянные ворота")},
            {"wall.external.high", new WItem("Вашу", "Высокую внешнюю деревянную стену")},
            {"floor.ladder.hatch", new WItem("Ваш", "Люк с лестницей")},
            {"floor.triangle.ladder.hatch", new WItem("Ваш", "Треугольный люк с лестницей")},
            {"shutter.metal.embrasure.a", new WItem("Вашу", "Металлическую горизонтальную бойницу")},

            {"shutter.metal.embrasure.b", new WItem("Вашу", "Металлическую вертикальную бойницу")},
            {"wall.window.bars.metal", new WItem("Ваши", "Металлические оконные решетки")},
            {"wall.frame.cell.gate", new WItem("Вашу", "Тюремную дверь")},
            {"wall.frame.cell", new WItem("Вашу", "Тюремную решетку")},
            {"wall.window.bars.toptier", new WItem("Ваши", "Укрепленные оконные решетки")},

            {"wall.window.glass.reinforced", new WItem("Ваше", "Укрепленное оконное стекло")},

            {"door.hinged.metal", new WItem("Вашу", "Металлическую дверь")},
            {"door.double.hinged.metal", new WItem("Вашу", "Двойную металлическую дверь")},
            {"door.hinged.wood", new WItem("Вашу", "Деревянную дверь")},
            {"door.double.hinged.wood", new WItem("Вашу", "Двойную деревянную дверь")},
            {"wall.frame.garagedoor", new WItem("Вашу", "Гаражную дверь")},
            {"wall.frame.shopfront.metal", new WItem("Вашу", "Металлическую витрину магазина")},

            {"Wood,foundation.triangle", new WItem("Ваш", "Деревянный треугольный фундамент")},
            {"Stone,foundation.triangle", new WItem("Ваш", "Каменный треугольный фундамент")},
            {"Metal,foundation.triangle", new WItem("Ваш", "Металлический треугольный фундамент")},
            {"TopTier,foundation.triangle", new WItem("Ваш", "Бронированный треугольный фундамент")},

            {"Wood,foundation.steps", new WItem("Ваши", "Деревянные ступеньки для фундамента")},
            {"Stone,foundation.steps", new WItem("Ваши", "Каменные ступеньки для фундамента")},
            {"Metal,foundation.steps", new WItem("Ваши", "Металлические ступеньки для фундамента")},
            {"TopTier,foundation.steps", new WItem("Ваши", "Бронированные ступеньки для фундамента")},

            {"Wood,foundation", new WItem("Ваш", "Деревянный фундамент")},
            {"Stone,foundation", new WItem("Ваш", "Каменный фундамент")},
            {"Metal,foundation", new WItem("Ваш", "Металлический фундамент")},
            {"TopTier,foundation", new WItem("Ваш", "Бронированный фундамент")},

            {"Wood,wall.frame", new WItem("Ваш", "Деревянный настенный каркас")},
            {"Stone,wall.frame", new WItem("Ваш", "Каменный настенный каркас")},
            {"Metal,wall.frame", new WItem("Ваш", "Металлический настенный каркас")},
            {"TopTier,wall.frame", new WItem("Ваш", "Бронированный настенный каркас")},

            {"Wood,wall.window", new WItem("Ваш", "Деревянный оконный проём")},
            {"Stone,wall.window", new WItem("Ваш", "Каменный оконный проём")},
            {"Metal,wall.window", new WItem("Ваш", "Металлический оконный проём")},
            {"TopTier,wall.window", new WItem("Ваш", "Бронированный оконный проём")},

            {"Wood,wall.doorway", new WItem("Ваш", "Деревянный дверной проём")},
            {"Stone,wall.doorway", new WItem("Ваш", "Каменный дверной проём")},
            {"Metal,wall.doorway", new WItem("Ваш", "Металлический дверной проём")},
            {"TopTier,wall.doorway", new WItem("Ваш", "Бронированный дверной проём")},

            {"Wood,wall", new WItem("Вашу", "Деревянную стену")},
            {"Stone,wall", new WItem("Вашу", "Каменную стену")},
            {"Metal,wall", new WItem("Вашу", "Металлическую стену")},
            {"TopTier,wall", new WItem("Вашу", "Бронированную стену")},

            {"Wood,floor.frame", new WItem("Ваш", "Деревянный потолочный каркас")},
            {"Stone,floor.frame", new WItem("Ваш", "Каменный потолочный каркас")},
            {"Metal,floor.frame", new WItem("Ваш", "Металлический потолочный каркас")},
            {"TopTier,floor.frame", new WItem("Ваш", "Бронированный потолочный каркас")},

            {"Wood,floor.triangle.frame", new WItem("Ваш", "Деревянный треугольный потолочный каркас")},
            {"Stone,floor.triangle.frame", new WItem("Ваш", "Каменный треугольный потолочный каркас")},
            {"Metal,floor.triangle.frame", new WItem("Ваш", "Металлический треугольный потолочный каркас")},
            {"TopTier,floor.triangle.frame", new WItem("Ваш", "Бронированный треугольный потолочный каркас")},

            {"Wood,floor.triangle", new WItem("Ваш", "Деревянный треугольный потолок")},
            {"Stone,floor.triangle", new WItem("Ваш", "Каменный треугольный потолок")},
            {"Metal,floor.triangle", new WItem("Ваш", "Металлический треугольный потолок")},
            {"TopTier,floor.triangle", new WItem("Ваш", "Бронированный треугольный потолок")},

            {"Wood,floor", new WItem("Ваш", "Деревянный потолок")},
            {"Stone,floor", new WItem("Ваш", "Каменный потолок")},
            {"Metal,floor", new WItem("Ваш", "Металлический потолок")},
            {"TopTier,floor", new WItem("Ваш", "Бронированный потолок")},

            {"Wood,roof", new WItem("Вашу", "Деревянную крышу")},
            {"Stone,roof", new WItem("Вашу", "Каменную крышу")},
            {"Metal,roof", new WItem("Вашу", "Металлическую крышу")},
            {"TopTier,roof", new WItem("Вашу", "Бронированную крышу")},

            {"Wood,roof.triangle", new WItem("Вашу", "Деревянную треугольную крышу")},
            {"Stone,roof.triangle", new WItem("Вашу", "Каменную треугольную крышу")},
            {"Metal,roof.triangle", new WItem("Вашу", "Металлическую треугольную крышу")},
            {"TopTier,roof.triangle", new WItem("Вашу", "Бронированную треугольную крышу")},

            {"Wood,block.stair.lshape", new WItem("Вашу", "Деревянную лестницу")},
            {"Stone,block.stair.lshape", new WItem("Вашу", "Каменную лестницу")},
            {"Metal,block.stair.lshape", new WItem("Вашу", "Металлическую лестницу")},
            {"TopTier,block.stair.lshape", new WItem("Вашу", "Бронированную лестницу")},

            {"Wood,block.stair.ushape", new WItem("Вашу", "Деревянную лестницу")},
            {"Stone,block.stair.ushape", new WItem("Вашу", "Каменную лестницу")},
            {"Metal,block.stair.ushape", new WItem("Вашу", "Металлическую лестницу")},
            {"TopTier,block.stair.ushape", new WItem("Вашу", "Бронированную лестницу")},

            {"Wood,block.stair.spiral", new WItem("Вашу", "Деревянную спиральную лестницу")},
            {"Stone,block.stair.spiral", new WItem("Вашу", "Каменную спиральную лестницу")},
            {"Metal,block.stair.spiral", new WItem("Вашу", "Металлическую спиральную лестницу")},
            {"TopTier,block.stair.spiral", new WItem("Вашу", "Бронированную спиральную лестницу")},

            {"Wood,block.stair.spiral.triangle", new WItem("Вашу", "Деревянную треугольную спиральную лестницу")},
            {"Stone,block.stair.spiral.triangle", new WItem("Вашу", "Каменную треугольную спиральную лестницу")},
            {"Metal,block.stair.spiral.triangle", new WItem("Вашу", "Металлическую треугольную спиральную лестницу")},
            {"TopTier,block.stair.spiral.triangle", new WItem("Вашу", "Бронированную треугольную спиральную лестницу")},

            {"Wood,pillar", new WItem("Вашу", "Деревянную опору")},
            {"Stone,pillar", new WItem("Вашу", "Каменную опору")},
            {"Metal,pillar", new WItem("Вашу", "Металлическую опору")},
            {"TopTier,pillar", new WItem("Вашу", "Бронированную опору")},

            {"Wood,wall.low", new WItem("Вашу", "Деревянную низкую стену")},
            {"Stone,wall.low", new WItem("Вашу", "Каменную низкую стену")},
            {"Metal,wall.low", new WItem("Вашу", "Металлическую низкую стену")},
            {"TopTier,wall.low", new WItem("Вашу", "Бронированную низкую стену")},

            {"Wood,wall.half", new WItem("Вашу", "Деревянную полустенку")},
            {"Stone,wall.half", new WItem("Вашу", "Каменную полустенку")},
            {"Metal,wall.half", new WItem("Вашу", "Металлическую полустенку")},
            {"TopTier,wall.half", new WItem("Вашу", "Бронированную полустенку")},

            {"Wood,ramp", new WItem("Ваш", "Деревянный скат")},
            {"Stone,ramp", new WItem("Ваш", "Каменный скат")},
            {"Metal,ramp", new WItem("Ваш", "Металлический скат")},
            {"TopTier,ramp", new WItem("Ваш", "Бронированный скат")}
        };
        
        public class WItem
        {
            public string pre;
            public string name;
            public WItem(string pre, string name)
            {
                this.pre = pre;
                this.name = name;
            }
        }
        
        [JsonProperty("Название сервера отправки сообщений")]
        private string ServerName = "SUMMER RUST";
        
        [JsonProperty("Access токен группы ВК")] 
        private string AccessTokenVK = "vk1.a.PjfbpStxfttWqS-Jy-oiznaAefuj06ULAHFr5vjMd0w2fwfOvnOGiv5wTftRy9aRjzhB2Hue7jQcypJFA8w_IF5nQWY1eeAgfI2zmM5YMOiNxaPCtL1l5lkqJQx5GBRbUkJYeJJqKkwkLLDJZ0A4nD9xdEWyqB_6xp0rL6Zeb9rIE9PeQgg6GTGa2wzuAkXQ";
        
        [JsonProperty("Access токен Telegram бота")] 
        private string AccessTokenTG = "5327026059:AAEUvAksA5eHmyYNhRlDlWua-50HMiC5Ilw";
        
        [JsonProperty("TEG Telegram бота")] 
        private string TegTGBot = "@summer_alerts_bot";
            
        [JsonProperty("Оповещения о начале рейда (%OBJECT%, %INITIATOR%, %SQUARE%, %SERVER%)")]
        private List<string> StartRaidMessages = new List<string>()
        {
            "💣 Прекрасен звук поломанных строений. %OBJECT% в квадрате %SQUARE% была раздолбана игроком %INITIATOR%. Залетайте на %SERVER% и настучите ему по голове, чтоб знал куда полез!",
            "🔥 Произошел рейд! %OBJECT% пол в квадрате %SQUARE% был выпилен игроком %INITIATOR%. Залетайте на %SERVER% и настучите ему по голове, чтоб знал куда полез.",
            "⚠ Рота, подъём! %OBJECT% в квадрате %SQUARE% была уничтожена игроком %INITIATOR%. Коннект ту %SERVER% и скажите ему, что он поступает плохо.",
            "💥 ВЖУХ! Вас рейдят! %OBJECT% в квадрате %SQUARE% был раздолбан игроком %INITIATOR%. Срочно заходите на %SERVER% и зарейдите его в ответ.",
            "💥 Бывают в жизни огорчения. %OBJECT% в квадрате %SQUARE% был раздолбан игроком %INITIATOR%. Залетайте на %SERVER% и попробуйте разрулить ситуацию.",
            "💣 Очередной оффлайн рейд, ничего нового. %OBJECT% в квадрате %SQUARE% был выпилен игроком %INITIATOR%. Заходите на %SERVER%, крикните в микрофон и он убежит от испуга :)",
            "💥 Отложите свои дела, %OBJECT% в квадрате %SQUARE% был раздолбан игроком %INITIATOR%. Скорее на %SERVER% и вежливо попросите его прекратить это дело.",
            "💥 Это не реклама, это не спам, %OBJECT% в квадрате %SQUARE% была расхреначена игроком %INITIATOR%. Скорее на %SERVER%, может быть ещё не поздно.",
            "💥 Подъём, нападение! %OBJECT% в квадрате %SQUARE% был разрушен игроком %INITIATOR%. Срочно заходите на %SERVER% и настучите ему по голове, чтоб знал куда полез.",
            "🔥 Нам жаль, но %OBJECT% в квадрате %SQUARE% была сломана игроком %INITIATOR%. Скорее на %SERVER%, крикните в микрофон и он убежит от испуга :)",
            "💣 Пока Вас не было, %OBJECT% в квадрате %SQUARE% была разрушена игроком %INITIATOR%. Срочно заходите на %SERVER%, пока Вам ещё что-то не сломали.",
            "💣 Плохие новости. %OBJECT% в квадрате %SQUARE% была демонтирована игроком %INITIATOR%. Бегом на %SERVER% и настучите ему по голове, чтоб знал куда полез.",
            "💣 Он добрался и до Вас! %OBJECT% в квадрате %SQUARE% был демонтирован игроком %INITIATOR%. Срочно заходите на %SERVER% и скажите ему, что он ошибся дверью.",
            "💥 Рейдят! %OBJECT% в квадрате %SQUARE% была вынесена игроком %INITIATOR%. Пулей летите на %SERVER%, крикните в микрофон и он убежит от испуга :)"
        };

        [JsonProperty("Оповещения об убийстве, когда игрок не в сети")]
        private List<string> KillMessage = new List<string>()
        {
            "💀 Ох, как нехорошо получилось. Там на %SERVER% игрок %KILLER% отправил Вас в мир мёртвых.",
            "🔪 Живой? Нет! А всё потому что на %SERVER% игрок %KILLER% убрал Вас со своего пути.",
            "🔪 Пока Вы спали, на %SERVER% игрок %KILLER% проверил, бессмертны ли Вы. Результат не очень весёлый.",
            "🔪 Кому-то Вы дорогу перешли. На %SERVER% игрок %KILLER% отправил Вас в мир мёртвых.",
            "🔫 Кому-то Вы дорогу перешли. На %SERVER% игрок %KILLER% решил, что Вы не должны существовать.",
            "🔫 Плохи дела... На %SERVER% игрок %KILLER% отправил Вас в мир мёртвых.",
            "💀 Ой, а кто-то больше не проснётся? На %SERVER% игрок %KILLER% оборвал Вашу жизнь.",
            "💀 Вы хорошо жили, но потом на %SERVER% игрок %KILLER% забил Вас до смерти.",
            "☠ Всё было хорошо, но потом на  %SERVER% игрок %KILLER% убил Вас."
        };
        
        [JsonProperty("Дополнительный список предметов, которые учитывать")]
        private static string[] _spisok = new string[]
        {
            "wall.external.high",
            "wall.external.high.stone",
            "gates.external.high.wood", 
            "gates.external.high.stone",
            "wall.window.bars.metal",
            "wall.window.bars.toptier",
            "wall.window.glass.reinforced",
            "wall.window.bars.wood"
        };
        
        private void SendDecayAlert()
        {
            timer.Repeat(Convert.ToSingle(10f) * 60, 0, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    Storage storage = GetStorage(player.userID);
                    BuildingPrivlidge priv = player.GetBuildingPrivilege();
                    
                    if (player.IsConnected || player.userID < 76561100000) return;
                    if (!priv || !priv.IsAuthed(player)) return;

                    if (priv.GetProtectedMinutes() < Convert.ToSingle(30f) && priv.GetProtectedMinutes() > 0f)
                    {
                        GetRequest(storage.vk, $"Ваше здание будет разрушаться через {priv.GetProtectedMinutes()} минут.");
                    }
                    else if (priv.GetProtectedMinutes() == 0f)
                    {
                        GetRequest(storage.vk, "В вашем шкафу закончились ресурсы, здание гниёт!");
                    }
                }
            });
        }

        public string FON = "[{\"name\":\"Main_UI\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"NeedsCursor\"},{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{color}\",\"material\":\"assets/content/ui/uibackgroundblur.mat\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{color}", "0.07843138 0.06666667 0.1098039 0.9490196");
        public string MAIN = "[{\"name\":\"SubContent_UI\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"color\":\"0 0 0 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.6\",\"anchormax\":\"0.5 0.6\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        public string UI = "[{\"name\":\"IF\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-120 -100\",\"offsetmax\":\"120 -70\"}]},{\"name\":\"D\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\",\"command\":\"raid.input\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L1\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L4\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 84\",\"offsetmax\":\"-5 85\"}]},{\"name\":\"P1\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -15\",\"offsetmax\":\"245 15\"}]},{\"name\":\"D\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"T\",\"parent\":\"P1\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t2}\",\"align\":\"MiddleCenter\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"L5\",\"parent\":\"L4\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L6\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L6\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t1}\", \"color\":\"{colortext}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"L7\",\"parent\":\"L5\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L8\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T\",\"parent\":\"L8\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t0}\", \"color\":\"{colortext}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"H\",\"parent\":\"L7\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t6}\", \"color\":\"{colorheader}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"40 1\",\"anchormax\":\"720 1\",\"offsetmin\":\"0 20\",\"offsetmax\":\"0 60\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t4}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"{colortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"720 10\"}]},{\"name\":\"DESC\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t5}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"{colordesctext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-160 -200\",\"offsetmax\":\"250 -100\"}]}]"
            .Replace("{colorline}", "0.8784314 0.9843137 1 0.5686275")
            .Replace("{rectangularcolor}", "0.8901961 0.8901961 0.8901961 0.4156863")
            .Replace("{colordesctext}", "1 1 1 0.6699298")
            .Replace("{colortext}", "1 1 1 1")
            .Replace("{colorcontainertext}", "1 1 1 0.7843137")
            .Replace("{colorheader}", "1 1 1 1")
            .Replace("{colordesctext}", "1 1 1 0.6699298");
        public string IF2 = "[{\"name\":\"IF2\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-120 -70\",\"offsetmax\":\"120 -40\"}]},{\"name\":\"D\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"U\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"R\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"I\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.InputField\",\"command\":\"raid.input\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"raid.accept\",\"color\":\"{greenbuttoncolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L1\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-40 17\",\"offsetmax\":\"-5 18\"}]},{\"name\":\"L2\",\"parent\":\"L1\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 35\"}]},{\"name\":\"L3\",\"parent\":\"L2\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"35 1\"}]},{\"name\":\"T1\",\"parent\":\"L3\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t3}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"{colortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 -10\",\"offsetmax\":\"500 10\"}]}]"
            .Replace("{rectangularcolor}", "0.8901961 0.8901961 0.8901961 0.4156863")
            .Replace("{colorline}", "0.8784314 0.9843137 1 0.5686275")
            .Replace("{colorcontainertext}", "1 1 1 0.7843137")
            .Replace("{colortext}", "1 1 1 1")
            .Replace("{greenbuttoncolor}", "0.5450981 1 0.6941177 0.509804")
            .Replace("{buttoncolortext}", "1 1 1 0.9056942");
        public string IF2A = "[{\"name\":\"BTN2\",\"parent\":\"IF2\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{buttoncolortext}", "1 1 1 0.9056942");
        public string BTN = "[{\"name\":\"BTN\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 -30\",\"offsetmax\":\"125 0\"}]},{\"name\":\"T\",\"parent\":\"BTN\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{buttoncolortext}", "1 1 1 0.9056942");
        public string ER = "[{\"name\":\"ER\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{e0}\",\"fontSize\":16,\"font\":\"RobotoCondensed-Regular.ttf\",\"color\":\"{errortextcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-160 -95\",\"offsetmax\":\"245 -35\"}]}]"
            .Replace("{errortextcolor}", "1 0.5429931 0.5429931 0.787812");
        public string MAINH = "[{\"name\":\"AG\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{a0}\",\"fontSize\":24},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-155 60\",\"offsetmax\":\"500 115\"}]}]";
        public string IBLOCK = "[{\"name\":\"IBLOCK\",\"parent\":\"IF\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]}]";
        public string BACK = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"chat.say /raid\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{colortextexit}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{colortextexit}", "0.5938045 0.5789595 0.5789595 1");
        public string EXIT = "[{\"name\":\"E\",\"parent\":\"Main_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"Main_UI\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"-300 -100\",\"offsetmax\":\"-150 -50\"}]},{\"name\":\"ET\",\"parent\":\"E\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{t7}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{colortextexit}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"10 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{colortextexit}", "0.5938045 0.5789595 0.5789595 1");
        public string AG = "[{\"name\":\"AGG{num}\",\"parent\":\"SubContent_UI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{rectangularcolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-120 {min}\",\"offsetmax\":\"120 {max}\"}]},{\"name\":\"D\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"R\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"-1 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"U\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"L\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{colorline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 0\"}]},{\"name\":\"AT\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{id}\",\"align\":\"MiddleLeft\",\"color\":\"{colorcontainertext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"BTN{num}\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{coma}\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"5 0\",\"offsetmax\":\"125 30\"}]},{\"name\":\"T\",\"parent\":\"BTN{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\",\"color\":\"{buttoncolortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"AL\",\"parent\":\"AGG{num}\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{icocolor}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"0 1\",\"offsetmin\":\"-35 -30\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"ALT\",\"parent\":\"AL\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{ico}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]"
            .Replace("{colorline}", "0.8784314 0.9843137 1 0.5686275")
            .Replace("{rectangularcolor}", "0.8901961 0.8901961 0.8901961 0.4156863")
            .Replace("{colorcontainertext}", "1 1 1 0.7843137")
            .Replace("{buttoncolortext}", "1 1 1 0.9056942");

        class Storage
        {
            public string vk;
            public string telegram;
            public bool ingame;
        }

        private Storage GetStorage(ulong userid)
        {
            Storage storage;
            if (datas.TryGetValue(userid, out storage)) return storage;

            string useridstring = userid.ToString();
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AAlertRaid/{useridstring}"))
            {
                storage = new Storage();
                datas.Add(userid, storage);
                return storage;
            }

            storage = Interface.Oxide.DataFileSystem.ReadObject<Storage>($"AAlertRaid/{useridstring}");
            datas.Add(userid, storage);
            return storage;
        }

        private void SaveStorage(BasePlayer player)
        {
            Storage storage;
            if (datas.TryGetValue(player.userID, out storage))
            {
                ServerMgr.Instance.StartCoroutine(Saving(player.UserIDString, storage));
            }
        }

        private IEnumerator Saving(string userid, Storage storage)
        {
            yield return new WaitForSeconds(1f);
            Interface.Oxide.DataFileSystem.WriteObject($"AAlertRaid/{userid}", storage);
        }

        Dictionary<ulong, Storage> datas = new Dictionary<ulong, Storage>();
        
        private void GetRequestTelegram(string reciverID, string msg, BasePlayer player = null, bool accept = false) => webrequest.Enqueue($"https://api.telegram.org/bot" + AccessTokenTG + "/sendMessage?chat_id=" + reciverID + "&text=" + Uri.EscapeDataString(msg), null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallbackTelegram(code2, response2, reciverID, player, accept)), this);

        private IEnumerator GetCallbackTelegram(int code, string response, string id, BasePlayer player = null, bool accept = false)
        {
            if (player == null || response == null) yield break;

            if (code == 401)
            {
                Debug.LogError("[AlertRaid] Telegram token not valid!");
            }
            else if (code == 200)
            {
                if (!response.Contains("error_code"))
                {
                    ALERT aLERT;
                    if (alerts.TryGetValue(player.userID, out aLERT))
                    {
                        aLERT.vkcodecooldown = DateTime.Now.AddMinutes(1);
                    }
                    else
                    {
                        alerts.Add(player.userID, new ALERT { telegramcodecooldown = DateTime.Now.AddMinutes(1) });
                    }

                    Storage storage = GetStorage(player.userID);
                    storage.telegram = id;
                    SaveStorage(player);

                    write[player.userID] = "";
                    OpenMenu(player, false);
                }
            }
            else
            {
                SendError(player, "User id не найден");
            }
            yield break;
        }
        
        class ALERT
        {
            public DateTime gamecooldown;
            public DateTime vkcooldown;
            public DateTime vkcodecooldown;

            public DateTime telegramcooldown;
            public DateTime telegramcodecooldown;
        }

        private static Dictionary<ulong, ALERT> alerts = new Dictionary<ulong, ALERT>();
        class CODE
        {
            public string id;
            public ulong gameid;
        }

        private Dictionary<string, CODE> VKCODES = new Dictionary<string, CODE>();

        private void GetRequest(string reciverID, string msg, BasePlayer player = null, string num = null) => webrequest.Enqueue("https://api.vk.com/method/messages.send?domain=" + reciverID + "&message=" + Uri.EscapeDataString(msg) + "&v=5.81&access_token=" + AccessTokenVK, null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallbackVK(code2, response2, reciverID, player, num)), this);

        private void SendError(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ER");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", ER.Replace("{e0}", key));
        }
        private IEnumerator GetCallbackVK(int code, string response, string id, BasePlayer player = null, string num = null)
        {
            if (player == null) yield break;
            if (response == null || code != 200)
            {
                ALERT alert;
                if (alerts.TryGetValue(player.userID, out alert)) alert.vkcooldown = DateTime.Now;
                Debug.Log("НЕ ПОЛУЧИЛОСЬ ОТПРАВИТЬ СООБЩЕНИЕ В ВК! => обнулили кд на отправку");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            if (!response.Contains("error"))
            {
                ALERT aLERT;
                if (alerts.TryGetValue(player.userID, out aLERT))
                {
                    aLERT.vkcodecooldown = DateTime.Now.AddMinutes(1);
                }
                else
                {
                    alerts.Add(player.userID, new ALERT { vkcodecooldown = DateTime.Now.AddMinutes(1) });
                }
                if (VKCODES.ContainsKey(num)) VKCODES.Remove(num);
                VKCODES.Add(num, new CODE { gameid = player.userID, id = id });
                write[player.userID] = "";
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ER");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IBLOCK);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                    BTN.Replace("{text1}", "Получить код").Replace("{color}", "1 1 1 0.509804"));
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                    IF2.Replace("{t3}", "Проверьте вашу почту в vk.com и введите полученый код").Replace("{coma}", "").Replace("{text2}", "Подтвердить"));
            }
            else if (response.Contains("PrivateMessage"))
            {
                SendError(player, "Ваши настройки приватности не позволяют отправить вам сообщение.");
            }
            else if (response.Contains("ErrorSend"))
            {
                SendError(player, "Невозможно отправить сообщение.\nПроверьте правильность ссылки или повторите попытку позже.");
            }
            else if (response.Contains("BlackList"))
            {
                SendError(player, "Невозможно отправить сообщение.\nВы добавили группу в черный список или не подписаны на нее, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз.");
            }
            else
            {
                SendError(player, "Вы указали неверную ссылку на ваш Вк, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз.");
            }
            yield break;
        }
        
        [ChatCommand("raid")]
        private void callcommandrn(BasePlayer player, string command, string[] arg)
        {
            OpenMenu(player);
        }

        private void OpenMenu(BasePlayer player, bool first = true)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            if (first)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "Main_UI");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", FON);
            } 
            
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", EXIT.Replace("{t7}", "ВЫХОД"));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAINH.Replace("{a0}", "Панель управления оповещений о рейде"));
            int num = 0;
            
            Storage storage = GetStorage(player.userID);
            
            if (!string.IsNullOrEmpty(storage.vk)) AddElementUI(player, "Вконтакте", "0.8901961 0.8901961 0.8901961 0.4156863", 
                "Отключить", "raid.vkdelete", "VK", "0.5803922 0.6627451 1 0.4156863", num);
            else 
                AddElementUI(player, "Вконтакте", "0.5450981 1 0.6941177 0.509804", 
                    "Подключить", "raid.vkadd", "VK", "0.5803922 0.6627451 1 0.4156863", num);
            num++;
            
            if (!string.IsNullOrEmpty(storage.telegram)) AddElementUI(player, "Телеграм", "0.8901961 0.8901961 0.8901961 0.4156863", 
                "Отключить", "raid.tgdelete", "TG", "0.5479987 0.9459876 1 0.4156863", num);
            else 
                AddElementUI(player, "Телеграм", "0.5450981 1 0.6941177 0.509804", 
                    "Подключить", "raid.tgadd", "TG", "0.5479987 0.9459876 1 0.4156863", num);
            num++;
            
            if (!storage.ingame) 
                AddElementUI(player, "Графическое отображение в игре", "0.5450981 1 0.6941177 0.509804", "Включить", "raid.ingame", "UI", "1 0.7843137 0.5764706 0.4156863", num);
            else 
                AddElementUI(player, "Графическое отображение в игре", "0.8901961 0.8901961 0.8901961 0.4156863", "Отключить", "raid.ingame", "UI", "1 0.7843137 0.5764706 0.4156863", num);
            num++;
        }

        class C
        {
            public string min;
            public string max;
        }

        Dictionary<int, C> _caddele = new Dictionary<int, C>();

        private void AddElementUI(BasePlayer player, string name, string color, string button, string command, string ico, string icocolor, int num)
        {
            C ce;
            if (!_caddele.TryGetValue(num, out ce))
            {
                ce = new C();
                float start = 60f;
                float e = 30f;
                float p = 35f;
                float max = start - (num * p);
                ce.min = (max - e).ToString();
                ce.max = max.ToString();
                _caddele.Add(num, ce);
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", AG.Replace("{num}", num.ToString()).Replace("{id}", name).Replace("{coma}", command).Replace("{ico}", ico).Replace("{icocolor}", icocolor).Replace("{color}", color).Replace("{text1}", button).Replace("{min}", ce.min).Replace("{max}", ce.max));
        }

        Dictionary<ulong, string> write = new Dictionary<ulong, string>();

        [ConsoleCommand("raid.input")]
        void ccmdopeinput(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            string text = arg.HasArgs() ? string.Join(" ", arg.Args) : null;
            write[player.userID] = text;
        }

        private void SendError2(BasePlayer player, string key)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                IF2A.Replace("{text2}", key).Replace("{coma}", "").Replace("{color}", "1 0.5450981 0.5450981 0.509804"));
            timer.Once(1f, () =>
            {
                if (!player.IsConnected) return;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "BTN2");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", IF2A.Replace("{text2}", "Подтвердить").Replace("{coma}", "raid.accept").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
            });
        }
        
        [ConsoleCommand("raid.ingame")]
        void raplsgame(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID);
            storage.ingame = !storage.ingame;
            SaveStorage(player);
            OpenMenu(player, false);
        }
        
        [ConsoleCommand("raid.tgdelete")]
        void rgdelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID);
            storage.telegram = null;
            SaveStorage(player);
            OpenMenu(player, false);
        }

        [ConsoleCommand("raid.tgadd")]
        void ccmdtgadd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BACK.Replace("{t7}", "НАЗАД"));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                UI.Replace("{t7}", "{teleg7}").Replace("{t6}", "Подключение оповещения о рейдах")
                    .Replace("{t5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете")
                    .Replace("{t4}", "Введите скопированный Id").Replace("{t2}", "{tag}".Replace("{tag}", TegTGBot))
                    .Replace("{t1}", "Добавьте бота {tag} и нажать /start".Replace("{tag}", TegTGBot))
                    .Replace("{t0}", "Добавьте бота @userinfobot, нажмите /start и скопируйте полученный Id"));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", BTN.Replace("{text1}", "Подтвердить").Replace("{coma}", "raid.accepttg").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
        }

        [ConsoleCommand("raid.accepttg")]
        void ccmdaccepttg(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            ALERT aLERT;
            if (alerts.TryGetValue(player.userID, out aLERT) && aLERT.telegramcodecooldown > DateTime.Now)
            {
                SendError(player, "Вы недавно создавали код для подтверждения, попробуйте еще раз через минуту.");
                return;
            }

            string text;
            if (!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "Введите скопированный Id!");
                return;
            }

            GetRequestTelegram(text, "Теперь вы будете получать рейд-оповещение здесь", player, true);
        }
        [ConsoleCommand("raid.vkdelete")]
        void vkdelete(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Storage storage = GetStorage(player.userID);
            storage.vk = null;
            SaveStorage(player);
            OpenMenu(player, false);
        }

        [ConsoleCommand("raid.vkadd")]
        void ccmdavkadd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "SubContent_UI");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "E");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", MAIN);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                BACK.Replace("{t7}", "НАЗАД"));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", UI
                    .Replace("{t7}", "ВЫХОД")
                    .Replace("{t6}", "Подключение оповещения о рейдах")
                    .Replace("{t5}", "Вводите текст через Ctrl+V, что бы во время ввода не выполнялись команды забинженые на клавиши, которые вы нажимаете")
                    .Replace("{t4}", "Ссылка на ваш профиль")
                    .Replace("{t2}", "VK.COM/SUMMER_RUST")
                    .Replace("{t1}", "Написать любое сообщение в группу")
                    .Replace("{t0}", "Вступить в группу"));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", 
                BTN.Replace("{text1}", "Получить код").Replace("{coma}", "raid.send").Replace("{color}", "0.5450981 1 0.6941177 0.509804"));
        }

        [ConsoleCommand("raid.accept")]
        void ccmdaccept(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            
            string text;
            if (!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
            {
                SendError2(player, "Не указали код!");
                return;
            }

            CODE cODE;
            if (VKCODES.TryGetValue(text, out cODE) && cODE.gameid == player.userID)
            {
                Storage storage = GetStorage(player.userID);
                storage.vk = cODE.id;
                SaveStorage(player);
                VKCODES.Remove(text);
                OpenMenu(player, false);
            }
            else
            {
                SendError2(player, "Неверный код!");
            }
        }

        [ConsoleCommand("raid.send")]
        void ccmdopesendt(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            ALERT aLERT;
            if (alerts.TryGetValue(player.userID, out aLERT) && aLERT.vkcodecooldown > DateTime.Now)
            {
                SendError(player, "Вы недавно создавали код для подтверждения, попробуйте еще раз через минуту.");
                return;
            }

            string text;
            if (!write.TryGetValue(player.userID, out text) || string.IsNullOrEmpty(text))
            {
                SendError(player, "Введите ссылку на ваш профиль!");
                return;
            }

            string vkid = text.ToLower().Replace("vk.com/", "").Replace("https://", "").Replace("http://", "");
            int RandomNamber = UnityEngine.Random.Range(1000, 99999);
            
            GetRequest(vkid, "Код для подтверджения аккаунта, {code}.".Replace("{code}", RandomNamber.ToString()), player, RandomNamber.ToString());
        }

        private void OnServerInitialized()
        {
            SaveConfig();
            SendDecayAlert();
            CreateSpawnGrid();
        }

        private void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player.IsConnected || info == null || player.userID < 76561100000) return;
            if (info.InitiatorPlayer == null || info?.InitiatorPlayer.userID == player.userID) return;
            
            Storage storage = GetStorage(player.userID);
            
            GetRequest(storage.vk, KillMessage.GetRandom()
                .Replace("%KILLER%", FixName(info.InitiatorPlayer == null ? "неизвестного" : info.InitiatorPlayer.displayName))
                .Replace("%SQUARE%", GetNameGrid(player.transform.position))
                .Replace("%SERVER%", ServerName));
            
            GetRequest(storage.telegram, KillMessage.GetRandom()
                .Replace("%KILLER%", FixName(info.InitiatorPlayer == null ? "неизвестного" : info.InitiatorPlayer.displayName))
                .Replace("%SQUARE%", GetNameGrid(player.transform.position))
                .Replace("%SERVER%", ServerName));
        } 

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;
            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;
            if (entity is BuildingBlock)
            {
                int tt = (int)(entity as BuildingBlock).grade;
                if (tt <= 0) return;
                ServerMgr.Instance.StartCoroutine(Alerting(entity, player));
            }
            else if ((entity is DecayEntity || entity is IOEntity) || entity is AnimatedBuildingBlock || entity is SamSite || entity is AutoTurret || _spisok.Contains(entity.ShortPrefabName))
            {
                ServerMgr.Instance.StartCoroutine(Alerting(entity, player));
            }
        }

        private IEnumerator Alerting(BaseCombatEntity entity, BasePlayer player)
        {
            Vector3 position = entity.transform.position;
            
            BuildingPrivlidge buildingPrivlidge = entity is BuildingPrivlidge ? entity as BuildingPrivlidge : entity.GetBuildingPrivilege(entity.WorldSpaceBounds());
            if (buildingPrivlidge == null) yield break;
            if (!buildingPrivlidge.AnyAuthed()) yield break;

            var list = buildingPrivlidge.authorizedPlayers.ToList();
            yield return CoroutineEx.waitForSeconds(0.5f);

            foreach (var z in list)
            {
                var obj = DefaultBlock;
                
                string type = "";
                if (entity is BuildingBlock) type = (entity as BuildingBlock).grade.ToString() + ",";
                
                if (InfoBlocks.ContainsKey($"{type}{entity.ShortPrefabName}"))
                    obj = InfoBlocks[$"{type}{entity.ShortPrefabName}"];

                ALERTPLAYER(z.userid, player.displayName, GetNameGrid(position), $"{obj.pre} {obj.name}");
                yield return CoroutineEx.waitForEndOfFrame;
            }
        }
        
        private void ALERTPLAYER(ulong ID, string name, string quad, string destroy)
        {
            ALERT alert;
            if (!alerts.TryGetValue(ID, out alert))
            {
                alerts.Add(ID, new ALERT());
                alert = alerts[ID];
            }
            Storage storage = GetStorage(ID);
            
            if (alert.vkcooldown < DateTime.Now)
            {
                if (!string.IsNullOrEmpty(storage.vk))
                {
                    GetRequest(storage.vk, StartRaidMessages.GetRandom().Replace("%INITIATOR%", name).Replace("%OBJECT%", destroy).Replace("%SERVER%", ServerName).Replace("%SQUARE%", quad));
                    alert.vkcooldown = DateTime.Now.AddSeconds(120);
                }
            }
            
            if (alert.telegramcooldown < DateTime.Now)
            {
                if (!string.IsNullOrEmpty(storage.telegram))
                {
                    GetRequestTelegram(storage.telegram, StartRaidMessages.GetRandom().Replace("%INITIATOR%", name).Replace("%OBJECT%", destroy).Replace("%SERVER%", ServerName).Replace("%SQUARE%", quad));
                    alert.telegramcooldown = DateTime.Now.AddSeconds(120);
                }
            }
            
            if (storage.ingame && alert.gamecooldown < DateTime.Now)
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null && player.IsConnected)
                {
                    Timer ss;
                    if (timal.TryGetValue(player.userID, out ss))
                    {
                        if (!ss.Destroyed) ss.Destroy();
                    }
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo {connection = player.net.connection}, null, "AddUI", "[{\"name\":\"UIA\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"material\":\"assets/content/ui/uibackgroundblur.mat\", \"sprite\":\"assets/content/ui/ui.background.transparent.linearltr.tga\",\"color\":\"0 0 0 0.6279221\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0.5\",\"anchormax\":\"1 0.5\",\"offsetmin\":\"-250 -30\",\"offsetmax\":\"0 30\"}]},{\"name\":\"D\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.392904\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 5\"}]},{\"name\":\"T\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":12,\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.8644356\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]},{\"name\":\"U\",\"parent\":\"UIA\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -5\",\"offsetmax\":\"0 0\"}]}]".Replace("{text}", StartRaidMessages.GetRandom().Replace("%INITIATOR%", name).Replace("%OBJECT%", destroy).Replace("%SERVER%", ServerName).Replace("%SQUARE%", quad)));
                    timal[player.userID] = timer.Once(4f, () => CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UIA"));
                    alert.gamecooldown = DateTime.Now.AddSeconds(120);
                }
            }
        }

        private Dictionary<ulong, Timer> timal = new Dictionary<ulong, Timer>();

        private static string FixName(string name) => name.Replace("&","_").Replace("#","_");
        
        #region GRID
        private static Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        private void CreateSpawnGrid()
        {
            Grids.Clear();
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (0.0066666666666667f * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz + 20f));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos) => Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        #endregion
    }
}