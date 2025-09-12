using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

namespace Oxide.Plugins
{
    [Info("UnsafeRecycler", "Bad Cop", "1.0.1")]
    [Description("Treats all safe zone recyclers as radtown recyclers. No more yellow recyclers with reduced efficiency")]
    public class UnsafeRecycler : RustPlugin
    {
        private List<Recycler> recyclers = new List<Recycler>();

        private void Init()
        {
            permission.RegisterPermission("unsaferecycler.use", this);
        }

        private void OnServerInitialized()
        {
            recyclers = BaseNetworkable.serverEntities.OfType<Recycler>().ToList();  // should be better than findobjectsbytype in this scenario
            foreach (Recycler recycler in recyclers)
            {
                ApplyRadtownSettings(recycler);
            }
        }

        private void OnEntitySpawned(Recycler recycler) 
        {
            if (recycler == null) return; // just a sanity check duh, we hate nulls

            recyclers.Add(recycler);
            ApplyRadtownSettings(recycler);
        }

        private void OnEntityKill(Recycler recycler)
        {
            if (recycler == null) return;

            recyclers.Remove(recycler);
        }

        private void ApplyRadtownSettings(Recycler recycler)  // make all the recyclers think theyre NOT in the safe zone, standardising the rate across all recyclers and removing the ugly piss yellow color from those in safe zone.
        {
            recycler.SetFlag(BaseEntity.Flags.Reserved9, false, false); // Treat as non-safe zone recycler. Surprised there wasn't more to it other than zone check but hey. Happy days.

            Puts($"Recycler at {recycler.transform.position} has been forced to radtown recycler settings."); // Let's give it console feedback for visual confirmation it ran and location of recyclers so can visually confirm if necessary
        }

        [ConsoleCommand("unsaferecycler")]  // not necessary to run on start, but if for any reason above doesn't run, or used the other command to make them safezone recyclers again, can force it unsafe manually
        private void ForceRadtownRecyclers(ConsoleSystem.Arg arg)
        {
            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), "unsaferecycler.use"))
            {
                arg.ReplyWith("You do not have permission to use this command.");
                return;
            }

            foreach (var recycler in recyclers)
            {
                ApplyRadtownSettings(recycler);
            }

            arg.ReplyWith("All recyclers have been set to radtown settings.");
        }

        [ConsoleCommand("saferecycler")]  // Allows reversing to default safe zone settings if needed, eg if i want to test something midgame.
        private void ForceSafeZoneRecyclers(ConsoleSystem.Arg arg)
        {
            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), "unsaferecycler.use"))
            {
                arg.ReplyWith("You do not have permission to use this command.");
                return;
            }

            foreach (var recycler in recyclers)
            {
                ApplySafeZoneSettings(recycler);
            }

            arg.ReplyWith("All recyclers have been set to safe zone settings.");
        }

        private void ApplySafeZoneSettings(Recycler recycler)  // Revert the recycler to safe zone settings.
        {
            recycler.SetFlag(BaseEntity.Flags.Reserved9, true, false); // Treat as safe zone recycler again

            Puts($"Recycler at {recycler.transform.position} has been reverted to safe zone settings."); // Console feedback for visual confirmation
        }

        private void Unload() // I guess some people might want it to revert on unload so here we go 
        {
            foreach (var recycler in recyclers)
            {
                ApplySafeZoneSettings(recycler);
            }
            Puts("Plugin Unloaded - All recyclers have been reverted to safe zone settings."); 
        }
    }
}
 