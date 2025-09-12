using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FUN", "azp2033", "0.1")]
    public class FUN : RustPlugin
    {
        Dictionary<BasePlayer, int> active_now = new Dictionary<BasePlayer, int>();

        [ChatCommand("funoff")]
        void FunOffCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player))
                active_now.Remove(player);
            player.ChatMessage("Выключено!");
        }

        [ChatCommand("funfreeze")]
        void FunFreezeCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player)) { active_now.Remove(player); }
            player.ChatMessage("Включена заморозка игроков!");
            active_now.Add(player, 0);
        }

        [ChatCommand("funsc")]
        void FunScCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player)) { active_now.Remove(player); }
            player.ChatMessage("Включена выключение экранов игроков!");
            active_now.Add(player, 1);
        }

        [ChatCommand("fundrop")]
        void FunDropCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player)) { active_now.Remove(player); }
            player.ChatMessage("Включена дропов игроков!");
            active_now.Add(player, 2);
        }

        [ChatCommand("funpizda")]
        void FunPizdaCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player)) { active_now.Remove(player); }
            player.ChatMessage("Включена пизда игрокам!");
            active_now.Add(player, 3);
        }

        [ChatCommand("funpolnayapizda")]
        void FunPolnayaPizdaCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (active_now.ContainsKey(player)) { active_now.Remove(player); }
            player.ChatMessage("Включена полная пизда игрокам!");
            active_now.Add(player, 4);
        }

        [ChatCommand("funzalupa")]
        void FunZalupa(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if(args.Length < 1) { player.ChatMessage("/funzalupa <кол-во>"); return; }
            int a = 1;
            Int32.TryParse(args[0], out a);
            for(int x = 0; x < a; x++)
            {
                Vector3 launchPos = player.transform.position + new Vector3(GetRandom(), 25, GetRandom());
                Vector3 newTarget = Quaternion.Euler(GetRandom(), GetRandom(), GetRandom()) * player.transform.position;
                BaseEntity rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", launchPos, new Quaternion(), true);
                Vector3 newDirection = (newTarget - launchPos);
                rocket.SendMessage("InitializeVelocity", (newDirection));
                rocket.Spawn();
            }
        }   

        private float GetRandom() => UnityEngine.Random.Range(-1.5f * 0.2f, 1.5f * 0.2f);

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || !active_now.ContainsKey(attacker) || !(info.HitEntity is BasePlayer)) return;
            BasePlayer target = info.HitEntity as BasePlayer;
            if (target == null) return;
            int type = active_now[attacker];
            if(type == 0)
            {
                Vector3 pos = target.transform.position;
                Timer tmr = timer.Every(0.05f, () =>
                {
                    target?.Teleport(pos);
                });
                timer.Once(5f, () => { tmr.Destroy(); });
            }
            if (type == 1)
            {
                CuiHelper.DestroyUi(target, "fun.ui");
                var ui = new CuiElementContainer();
                ui.Add(new CuiPanel()
                {
                    Image = { Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, "Hud", "fun.ui");
                CuiHelper.AddUi(target, ui);
                timer.Once(5f, () => { if(target != null) CuiHelper.DestroyUi(target, "fun.ui"); });
            }
            if (type == 2)
            {
                foreach (var a in target.inventory.AllItems())
                    a.Drop(target.transform.position, Vector3.up);
            }
            if (type == 3)
            {
                target.Teleport(target.transform.position + new Vector3(0, 999, 0));
            }
            if (type == 4)
            {
                Timer tmr = timer.Every(0.1f, () =>
                {
                    target.Teleport(target.transform.position + new Vector3(0, 1.5f, 0));
                });
                timer.Once(5f, () => { tmr.Destroy(); });
            }
        }
    }
}