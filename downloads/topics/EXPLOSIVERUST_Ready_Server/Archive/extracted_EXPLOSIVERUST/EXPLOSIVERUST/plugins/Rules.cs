using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Rules", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class Rules : RustPlugin
    {
        #region Вар
        string Layer = "Rules_UI";

        [PluginReference] Plugin ImageLibrary;
        #endregion

        #region Правила
        List<string> rules = new List<string>()
        {
            "<b><size=12>Информация</size></b>\n\n· Не знание правил не освобождает Вас от ответственности.\n\n· Зайдя на сервер Вы автоматически соглашаетесь со всеми нижеперечисленными пунктами правил.\n\n· Вы несете ответственность за все свои аккаунты. Получив бан за нарушение на одном аккаунте, вы получите его и на последующих. То же самое будет если на одном из ваших аккаунтах имеется EAC блокировка.\n\n· Если Вы уже были замечены с читами / макросами на другом сервере / проекте и на вас есть пруфы - мы имеем право забанить Вас без проверки.\n\n· Администрация не обязуется компенсировать игровые ценности, утраченные по причине вашей ошибки, багов игры или технических проблем на сервере.\n\n· Запрещена продажа или реклама Читов/Макросов.\n\n· Запрещено выдавать себя за Администратора, модератора или проверяющего.\n\n· Администрация сама выбирает наказание для игрока в зависимости от степени нарушения и обстоятельств. Игрок может получить просто предупреждение, а может получить и перманентный бан.",
            "<b><size=12>Геймплей</size></b>\n\n· Запрещено использовать/хранить читы/макросы или любой другой софт дающий преимущество перед честными игроками.\n\n· Запрещена игра с читерами/макросниками.\n\n· Запрещено использование услуг читеров.\n\n· Запрещено использование любых видов багов с целью или без цели получения преимущества над другими игроками.\n\n\n<b><size=12>Нарушение лимита игроков в команде</size></b>\n\n· Нельзя жить больше положенного максимума в одном доме\n\n· Нельзя устраивать альянсы и перемирия с соседями если в сумме вас больше указанного в названии сервера максимума\n\n· Частая смена тиммейта будет считаться за нарушения правила о лимите\n\n· Нельзя рейдить и антирейдить в 2+ (подсад, доп.люди на обороне)",
            "<b><size=12>Игровой Чат</size></b>\n\n· Запрещены ссылки в чате на сторонние сервисы и сайты.\n\n· Запрещен флуд (многократное повторение бессмысленных фраз, символов) или многократное отправление одинаковых фраз за короткий промежуток времени.\n\n· Запрещены провокационные сообщения, по типу - ''я читер, проверь меня''.\n\n\n<b><size=12>Проверки</size></b>\n\n· Вы имеете полное право отказаться проходить проверку, но в этом случае вы и ваши тиммейты получат блокировку на всех наших серверах.\n\n· При согласии на проверку вы разрешаете устанавливать сторонние программы нужные администрации для проверки вашего PC.\n\n· Проверки проходят только через программы «DISCORD» и «SKYPE». Каждый игрок на нашем проекте, в обязательном порядке должен иметь одну из данных программ на своём пк (или хотя-бы аккаунт в дискорде).\n\n· Выход с сервера во время вызова на проверку увенчается блокировкой."
        };
        #endregion

        #region Команда
        [ConsoleCommand("skips")]
        void ConsoleSkips(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            RulesUI(player, int.Parse(args.Args[0]));
        }
        #endregion

        #region Интерфейс
        void RulesUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Правила сервера</b></size>\nЗдесь вы можете узнать об основных правилах сервера.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            foreach (var check in rules.Skip(page).Take(1))
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 0.83", OffsetMax = "0 0" },
                    Text = { Text = check, Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
                }, Layer);
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.94 0", AnchorMax = "1 0.06", OffsetMax = "0 0" },
                Button = { Color = "0.10 0.13 0.19 1", Command = rules.Count() > (page + 1) * 1 ? $"skips {page + 1}" : "" },
                Text = { Text = $">", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.875 0", AnchorMax = "0.935 0.06", OffsetMax = "0 0" },
                Button = { Color = "0.10 0.13 0.19 1", Command = page != 0 ? $"skips {page - 1}" : "" },
                Text = { Text = $"<", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}