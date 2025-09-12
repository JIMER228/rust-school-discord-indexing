using Oxide.Game.Rust.Cui;
using Rust;
using System.Collections.Generic;
using Obj = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("Summer Clean", "azp2033", "0.1")]
    public class SClean : RustPlugin
    {
        void Init()
        {
            DoClean(); 
        }
        private void MessageVK(string message)
        {
            int randomId = 0;
           

            string text = $"{message}";
            while (text.Contains("#"))
            {
                text = text.Replace("#", "%23");
            }

            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id=3&random_id={randomId}&message={text}&access_token=af73a9c477ae6e40d28a9b5d4ad80c3e85d35055dad45bff7e7147e83dd9ba1cc894b5185cf3db57c90bc&v=5.92", null, (code, response) => { }, this);
            randomId++;
        }

        void Broadcast(string text)
        {
            string message = $"<size=15><color=#F65050FF>[SUMMER CLEANER]:</color> {text}</size>";
            for(int x = 0; x < BasePlayer.activePlayerList.Count; x++)
            {
                BasePlayer player = BasePlayer.activePlayerList[x];
                if (player == null) continue;
                player.SendConsoleCommand("chat.add", 76561198325182612, message);
            }
        }
        [ConsoleCommand("clean.map.start")]
        void CleanMap(ConsoleSystem.Arg arg)
        {
        DoClean();
        PrintWarning("Вы запустили очистку карты!");
        }
        void DoClean()
        {
            MessageVK("Запущена очистка карты на сервере SUMMER MAIN");
            Broadcast("Запущена очистка карты!");
            Broadcast("Будут очищены (гниющие постройки, трупы, memcache)");
            Broadcast("Очистка будет произведена через 1 минуту!");

            timer.Once(30, () => { Broadcast("Очистка будет произведена через 30 cекунд!"); });
             
            timer.Once(50, () => { Broadcast("Очистка будет произведена через 10 cекунд!"); });
             
            timer.Once(55, () => { Broadcast("Очистка будет произведена через 5 cекунд!"); });
             
            timer.Once(60, () =>
            {
                Broadcast("Производится очистка карты..");
                MessageVK("Производится очистка карты..");
                foreach (PlayerCorpse corpse_a in Obj.FindObjectsOfType<PlayerCorpse>())
                    corpse_a?.Kill();
                foreach (LootableCorpse corpse_b in Obj.FindObjectsOfType<LootableCorpse>())
                    corpse_b?.Kill();
                foreach (WorldItem witems in Obj.FindObjectsOfType<WorldItem>())
                    witems?.Kill();
                foreach (BaseEntity entity in BaseEntity.serverEntities)
                {
                    if (entity != null && (entity.name.Contains("wall.external") || entity.name.Contains("barricades")) && entity.GetBuildingPrivilege() == null)
                        entity.Kill();
                }
                foreach (BuildingBlock build in Obj.FindObjectsOfType<BuildingBlock>())
                {
                    if (build.GetBuildingPrivilege() == null && build.Health() < 200)
                        build.Kill();
                }
                GC.Collect();
                Broadcast("Очистка успешно произведена!");
                MessageVK("Очистка успешно произведена!");
            });
        }
    }
}

