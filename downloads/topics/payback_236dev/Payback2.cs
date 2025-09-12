
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using Rust;
using Rust.Ai;
using Network;
using Facepunch;
using Rust.Workshop.Editor;
using VLB;
using ProtoBuf;
using Epic.OnlineServices.P2P;

using static ConsoleSystem;
using Rust.UI.ServerAdmin;
using static FreeImage;





//using ConVar;

namespace Oxide.Plugins
{
	[Info("Payback2", "1928Tommygun", "1.6.1")]
	[Description("Special Admin Commands To Mess With Cheaters")]
	class Payback2 : RustPlugin
	{
        //| =============================================
        //| CHANGELOG
        //| =============================================

        //| 1.6.1
        //| - update for Oct 3 patch

        //| 1.6.0
        //| Added AirStrike - Annahilate target player with an F15 fighter.
        //| Added Loudfoot - Target player triggers a tin can alarm whever they crouch-move

        //| 1.5.9
        //| fixed recycler seated position

        //| 1.5.8
        //| fixed hogwild positioning
        //| fixed roast spears

        //| 1.5.7
        //| fixed gestures
        //| fixed sack
        //| fixed roast spear positioning

        //| 1.5.6
        //| - Fixed Recycler
        //| - Added 'force' command to WOODS so that you don't have to wait for the player to farm
        //| - Added SLOWBURN command - Target player is burned slowly over time
        //| - Added flashbang command - target player is flashbanged continously
        //| - Added NoPickup - Target player always drops items when they try to pick them up


        //| 1.5.5
        //| - Fixed HigherGround 

        //| 1.5.4
        //| - Added OverCompensation - Target player is over compensating for their lack of bulge and ends up shooting into the ground
        //| - Added LagSwitch - Target player rubberbands constantly as if experiencing a ton of lag


        //| 1.5.3
        //| - Fixed roast command rotation / size issues
        //| - Upgraded potato to accept a mulitplier on the speed of the effect (add 1 - 100 after the command)


        //| 1.5.2
        //| - Updated for Sept 2023 patch.
        //| - Prevent players from moving items around in recyclers that are eating people
        //| - Added optional 'kill' parameter to Batman, always kills players on hit


        //| 1.5.1
        //| - Added Batman : Whack players with a bat, aim for that home run!  Use this on the batter to give them the power of the bat. Sprint to triple the power, headshots will kill the target!


        //| 1.4.13
        //| - Changed recycle command to recycleplayer
        //| - Horse dung now stacks correctly while vd is off

        //| 1.4.12
        //| - Added Reycle command - Recycle's target cheater -- works like the sit command with a sick twist!


        //| 1.4.11
        //| - Fixed missing image links

        //| 1.4.10
        //| - Fixed console and chat binds when targeting a player with raycast

        //| 1.4.9
        //| - Added 'fast' parameter to SPITROAST
        //| - Added POOPROCKET command

        //| 1.4.8
        //| - Updated missing image urls


        //| 1.4.7
        //| - Added verbal diarrhea command : Target player starts spewing increasing amounts of horse dung when they use voice chat


        //| 1.4.6
        //| - Sit command now uses a toilet chair by default! Includes options for using other chairs
        //| - Added config to disable showing UI to admins while commands are active
        //| - fixed POTATO command
        //| - Updated License : you are allowed to use this plugin on multiple servers as long as you are the owner of the servers without purchasing additional copies.  Please consider tipping!

        //| 1.4.5
        //| - Restored rocketman to its former glory with the return of the invisible chair!
        //| - Updated for multithreaded networking

        //| 1.4.4
        //| - Added a temporary fix for rocketman and other broken commands, next month Facepunch will update the game and it will work just like before!
        //| - Added ability to for commands to persist between server restarts

        //| 1.4.3
        //| - Jan 2023 patch basemounteable

        //| 1.4.2
        //| - added TRAIN command

        //| 1.4.1
        //| - added MINEFIELD command


        //| 1.4.0
        //| - Added BONK - Bonk that cheater right into the ground | use bonk on yourself to make anyone you hit bonkable
        //| - BuggedGun - Makes target's weapon bug out and randomly unequip
        //| - Added Radiation - Applies a ton of rads to target player 
        //| - Added UI - Shows which commands are currently active
        //| - Added ability to log admins using payback to discord
        //| - Improved support for conflicting commands

        //| 1.3.8
        //| - Added CRUCIFY command

        //| 1.3.7
        //| - Added SPITROAST command

        //| 1.3.6
        //| - Updated for Jan 28 new year content

        //| 1.3.5
        //| - attempt to fix wounded NRE in hog

        //| 1.3.4
        //| - added INTERROGATE command
        //| - hog now attempts to keep player's wearables in their inventory



        [PluginReference] Plugin Payback;//| reference to the original payback plugin

		[PluginReference] private Plugin ImageLibrary;//| Optional reference to ImageLibrary

        //| Hello!

        //| This is Payback 2!  To make sure you can use this plugin on its own, it comes with some essential features from Payback 1!
        //| If you want to have classic admin commands like ROCKETMAN, JAWS, and BSOD, please purchase Payback 1 at https://payback.fragmod.com


        //| TUTORIAL:
        //| admins require the permission payback.admin to use this plugin
        //| this oxide command works for that: oxide.grant group admin payback.admin
        //| put 'payback' in the F1 or server console to see the tutorial!



        //| =========

        //| ===================

        //| ==============================================================================
        //| TOMMYGUN'S EULA - BY USING THIS PLUGIN YOU AGREE TO THE FOLLOWING!
        //| ==============================================================================
        //| 
        //| Code contained in this file is not licensed to be copied, shared, resold, or modified in any way.
        //| You may copy the plugin freely to each server instance that your organization owns.  I ask that you include an optional tip if you are a large server organization, the suggested amount is one copy for every 10 servers you are running. 
        //| Do not share this plugin with other server organizations, they must purchase their own licenses.
        //|
        //| =======================================

        //| ===================

        //| =========

        //| TOMMYGUN
        //MMMMMMMMMMMMMMMMMMMMMMMMWNO::loll::kKKXXXXXXXXXXKXXKx;o000000KKKKNWWWWWWWWWWWWWWWWWWMMMMMMWWXxkNNNNN
        //MMMMMMMMMMMMMMMMMMMMMMMMKc,.......................... ...........'',,,,,,,,,,,,,,,,,;;;;;;;;:..,'''.
        //MMMMMMMMMMMMMMMMMMMMMMMMXc',,;;;;;;;;;;;;;;;;;;;,.         ',''',;:::::::::::::clldxxxxxxxxxxooocccc
        //MMMMMMMMMMMMMMNXK00KXKOdc;'',;:;,,;lxddl::,;::;'.',..''   .kWNNNK:...',,........'oXMMMMMMMMMMMMMMMMM
        //MMMMWNX0Oxol:;'.......       :Oc  .xMMMKc....','.;ool,..  .OMMMMNxcdO0KKo.      lNMMMMMMMMMMMMMMMMMM
        //Odlc;'..                     .:,';xNMMWk.     ;xoxXXx'..   cXMMMMMMMMMWO:     .;OMMMMMMMMMMMMMMMMMMM
        //.                     ';:clodxO0XWMMMXo.    .:x000KK0Ol.   .xMMMMMMMW0:.    .:0WWMMMMMMMMMMMMMMMMMMM
        //.                 .,lONMMMMMMMMMMMMMX:     ,0WMMMMMMMMx.cXMMMMMMMK,     'kNWMMMMMMMMMMMMMMMMMMMMMMMM
        //'              .;o0NMMMMMMMMMMMMMMMMk.    .oWMMMMMMMMMx.  .OMMMMMMMMNl    ,OWMMMMMMMMMMMMMMMMMMMMMMM
        //'           .:xKWMMMMMMMMMMMMMMMMMMMNd.  .oNMMMMMMMMMMx.  .OMMMMMMMMMNkl::kWMMMMMMMMMMMMMMMMMMMMMMMM
        //.       .,lkXMMMMMMMMMMMMMMMMMMMMMMMMMXOdxXMMMMMMMMMMMx.  .OMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
        //.   .,lkKWMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMXOkkONMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
        //: .l0NMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM




        //| ==============================================================
        //| Definitions
        //| ==============================================================

        #region FNUTIL
        List<Item> PooledList = new List<Item>();
        List<Item> GetAllItems(BasePlayer player)
        {
            player.inventory.GetAllItems(PooledList);
            return PooledList;
        }
        #endregion


        public enum Card
		{
			Pacifism = 0,//zeros all outgoing damage from target player
			InstantKarma = 4,//reflects damage back to the player
			Dud = 5,//prevents damage to non-player entities
			Sit = 8,//force player to sit
			HigherGround = 11,//target player is teleported 100m into the air
			NoRest, // No Rest For The Wicked! Force the player to respawn!
			ViewLoot, // View target entities loot
			Hammer, // hammer - gives target player a hammer that will destroy all the entities owned by the hammer's target
			Bag, // Bag - Print all players that have bagged target player in, and print all players that have been bagged.  Include "discord" after the command to log the results to discord

			//| Payback 2
			Woods, // Woods - If you go down to the woods today, be ready for big surprise!
			PotatoMode, // PotatoMode - Target's frame rate will decrease dramatically over time - Warning, this is designed to eventually crash Rust
			Hogwild, // Hogwild - Ride the cheater around like a pig! REEE!
			Interrogate, // Interrogate - Throw a hood over the cheater's head, force them to spill their secrets! They can't see anything unless they speak!!
			Spitroast, // Spitroast - Stick 'em with the pointy end and feast on their tears.  Roast the cheater roticery style over the fire!
			Crucify, // Crucify - It's like the 'sit' command but taken to biblical proportions!
			Radiation, // Radiation - Applies a ton of rads to target player
			BuggedGun, // BuggedGun - Makes target's weapon bug out and randomly unequip
			Bonk, // Bonk - Bonk that cheater right into the ground
			Minefield, // Minefield - Continuously generates a minefield around target player (can only hurt the target)
			Train, // Train - Hogtie target player to the ground then run em over with a train!
			VerbalDiarrhea, // VerbalDiarrhea - Target player starts spewing increasing amounts of horse dung when they use voice chat
            PoopRocket, // PoopRocket - Propells a player through the air using organic fuel systems
            RecyclePlayer, // Recycle - Recycle target cheater
            Batman, // Bat - home runs
            OverCompensation, // MegaRecoil - Target player has massive amounts of recoil when they shoot
            LagSwitch, // LagSwitch - Target player rubberbands constantly as if experiencing a ton of lag
			SlowBurn, // SlowBurn - Target player is burned slowly over time
			Flash, // Flash - Flashbang target player repeatedly
			NoPickup, // NoPickup - Target player always drops items when they try to pick them up
			AirStrike, // AirStrike - Annahilate target player with an F15 fighter
			Loudfoot, // Loudfoot - Target player triggers a tin can alarm whever they crouch-move
        }

		Dictionary<Card, string> descriptions = new Dictionary<Card, string>() {
			{Card.Dud, "target player deals no damage to NON-PLAYER entities.  Also prevents farming / tool use" },
			{Card.InstantKarma, "target player deals no damage to enemies and 35% of the damage is reflected back to them" },
			{Card.Pacifism, "target player deals no player damage to non-teammates; add 'silent' to not send a message about it to other players." },
			{Card.Sit, "spawns a chair in front of you and forces the cheater to sit.  Doesn't let them get up and will place them back in if they die. add 'beach' or 'gamer' to the end of the command to change the chair prefab!" },
			{Card.HigherGround, "target player is teleported 100m into the air" },
			{Card.NoRest, "No Rest For The Wicked! Force the player to respawn when they die!" },
			{Card.ViewLoot, "View target player's loot" },
			{Card.Hammer, "Gives admin a hammer that will destroy all the entities owned by the hammer's target.  Add -noloot to also delete the loot" },
			{Card.Bag, "Print all players that have bagged target player in, and print all players that have been bagged.  Include \"discord\" after the command to log the results to discord" },
			
			//| Payback 2
			{Card.Woods, "Target will get attacked by a bear if he's farming wood or stone.  Add 'landmine' to strap a bunch of landmines to the bear's face. Add 'force' to instantly spawn the bear" },
			{Card.PotatoMode, "Target's frame rate will decrease dramatically over time. -- *Warning, this is designed to crash Rust* | add a number 1-100 after the command to make it faster" },
			{Card.Hogwild, "Ride the cheater around like a pig! REEE!" },
			{Card.Interrogate, "Throw a hood over the cheater's head, force them to spill their secrets! They can't see anything unless they speak!! Supports spectate mode!" },
			{Card.Spitroast, "Stick 'em with the pointy end and feast on their tears.  Roast the cheater roticery style over the fire! Add 'fast' after the command for good taste: roast <player> fast" },
			{Card.Crucify, "It's like the 'sit' command but taken to biblical proportions!" },
			{Card.Radiation, "Applies a ton of rads to target player" },
			{Card.BuggedGun, "Prevents target player from using their weapons" },
			{Card.Bonk, "Bonk that cheater right into the ground | use bonk on yourself to make anyone you hit bonkable" },
			{Card.Minefield, "Continuously generates a minefield around target player (can only hurt the target) -- /mine <targetplayer> <1 to 100> changes the density of the minefield" },
			{Card.Train, "Hogtie target player to the ground then run em over with a train!" },
			{Card.VerbalDiarrhea, "Target player starts spewing increasing amounts of horse dung when they use voice chat" },
			{Card.PoopRocket, "Propells a player through the air using organic fuel systems" },
			{Card.RecyclePlayer, "Recycle target cheater" },
			{Card.Batman, "Whack players with a bat, aim for that home run!  Use this on the batter to give them the power of the bat. Sprint to triple the power, headshots will kill the target! Add -kill parameter to always kill on hit." },
			{Card.OverCompensation, "Target player is over compensating for their lack of bulge and ends up shooting into the ground" },
			{Card.LagSwitch, "Target player rubberbands constantly as if experiencing a ton of lag.  Try different numbers after the command to change the ping" },
			{Card.SlowBurn, "Target player is burned slowly over time" },
			{Card.Flash, "Target player is flashbanged repeatedly" },
			{Card.NoPickup, "Target player always drops items when they try to pick them up" },
			{Card.AirStrike, "Annahilate target player with an F15 fighter.  add 'unsafe' to enable damage to everything.  Add 'precision' to target what you're looking at (great for bases)" },
			{Card.Loudfoot, "Target player triggers a tin can alarm whever they crouch-move" },
		};

		Dictionary<string, Card> cardAliases = new Dictionary<string, Card>() {
			{ "dud", Card.Dud },
			{ "in", Card.InstantKarma},
			{ "pf", Card.Pacifism},
			{ "hg", Card.HigherGround},
			{ "nr", Card.NoRest},
			{ "res", Card.NoRest},
			{ "loot", Card.ViewLoot},
			{ "ham", Card.Hammer},
			{ "bg", Card.Bag},

			//| Payback 2
			{ "bear", Card.Woods},
			{ "potato", Card.PotatoMode},
			{ "hog", Card.Hogwild},
			{ "sack", Card.Interrogate},
			{ "roast", Card.Spitroast},
			{ "cr", Card.Crucify},
			{ "rad", Card.Radiation},
			{ "bug", Card.BuggedGun},
			{ "bk", Card.Bonk},
			{ "mine", Card.Minefield},
			{ "tr", Card.Train},
			{ "vd", Card.VerbalDiarrhea},
			{ "shitter", Card.VerbalDiarrhea},
			{ "poop", Card.PoopRocket},
			{ "pr", Card.PoopRocket},
			{ "re", Card.RecyclePlayer},
			{ "bat", Card.Batman},
			{ "recoil", Card.OverCompensation},
			{ "lag", Card.LagSwitch},
			{ "sb", Card.SlowBurn},
			{ "fl", Card.Flash},
			{ "np", Card.NoPickup},
			{ "as", Card.AirStrike},
			{ "lf", Card.Loudfoot},
		};

		HashSet<Card> cardsWhichCauseConflicts = new HashSet<Card>() {
			Card.Sit,
			Card.Crucify,
			Card.Hogwild,
			Card.Spitroast,
			Card.Train,
			Card.PoopRocket,
			Card.RecyclePlayer,
			Card.Batman,
			Card.LagSwitch,
		};

		HashSet<Card> cardsWhichCanPersist = new HashSet<Card>() {
            //Card.Butterfingers,
            Card.Dud,
			Card.InstantKarma,
			Card.Pacifism,
            //Card.DogDoo,
            //Card.BSOD,
            //Card.Thirsty,
            //Card.DrNo,
            Card.NoRest,
            //Card.ChickenChaser,
            //Card.Masochist,
            Card.Woods,
			Card.PotatoMode,
			Card.Interrogate,
			Card.Radiation,
			Card.BuggedGun,
			Card.Minefield,
			Card.VerbalDiarrhea,
			Card.OverCompensation,
            Card.LagSwitch,
            Card.SlowBurn,
            Card.Flash,
            Card.NoPickup,
            Card.Loudfoot,
        };

        HashSet<Card> cardsThatNeedNoTarget = new HashSet<Card>() {
            Card.AirStrike,
        };

        //| ==============================================================
        //| Giving
        //| ==============================================================
        public void GiveCard(ulong userID, Card card, string[] args = null, BasePlayer admin = null)
		{
			//Puts($"Payback card {card} given to {userID}");

			BasePlayer player = BasePlayer.FindByID(userID);

			if (cardsWhichCauseConflicts.Contains(card))
			{
				ResolveConflictingCommands(player, admin, (int)card);
			}

			HashSet<Card> cards;
			if (!cardMap.TryGetValue(userID, out cards))
			{
				cards = new HashSet<Card>();
				cardMap[userID] = cards;
			}
			cards.Add(card);


			if (player != null)
			{


				if (card == Card.Sit)
				{
					DoSitCommand(player, admin, args);
				}
				else if (card == Card.HigherGround)
				{
					DoHigherGround(player);
				}
				else if (card == Card.Pacifism)
				{
					silentPacifism = false;
					if (args != null)
					{
						if (args.Contains("silent"))
						{
							silentPacifism = true;
						}
					}
				} else if (card == Card.NoRest)
				{
					if (player.IsDead())
					{
						player.Respawn();
					}
				} else if (card == Card.ViewLoot)
				{
					ViewTargetPlayerInventory(player, admin);
					TakeCard(player, Card.ViewLoot);
				} else if (card == Card.Hammer)
				{
					GiveAdminHammer(player);
					if (args.Contains("noloot"))
					{
						flag_kill_no_loot = true;
						PrintToPlayer(admin, $"Hammer set to remove loot!");
					} else
					{
						flag_kill_no_loot = false;
					}
				} else if (card == Card.Woods)
				{
					if (args.Contains("landmine"))
					{
						woodsHasLandmines.Add(player.userID);
					} else
					{
						woodsHasLandmines.Remove(player.userID);
					}

					if (args.Contains("force"))
					{
						DoWoods(player);
					}

				} else if (card == Card.PotatoMode)
				{
					DoPotato(player, admin, args);
				} else if (card == Card.Hogwild)
				{
					DoHog(player, admin, args);
				} else if (card == Card.Interrogate)
				{
					DoInterrogate(player, admin, args);
				} else if (card == Card.Spitroast)
				{
					DoSpitroastCommand(player, admin, args);
				} else if (card == Card.Crucify)
				{
					DoCrucifyCommand(player, admin);
				} else if (card == Card.Radiation) {
					DoRadiation(player, admin);
				} else if (card == Card.BuggedGun) {
					DoBuggedGun(player, admin);
				} else if (card == Card.Bonk)
				{
					DoBonk(player, admin);
				} else if (card == Card.Minefield)
				{
					DoMinefield(player, admin, args);
				} else if (card == Card.Train)
				{
					DoTrain(player, admin, args);
				} else if (card == Card.VerbalDiarrhea)
				{
					DoVerbalDiarrhea(player, admin, args);
				} else if (card == Card.PoopRocket)
				{
					DoPoopRocket(player, admin, args);
				} else if (card == Card.RecyclePlayer)
				{
					DoRecycle(player, admin, args);
				} else if (card == Card.Batman)
				{
					DoBat(player, admin, args);
				} else if (card == Card.OverCompensation)
				{
					DoOverCompensation(player, admin, args);
				} else if (card == Card.LagSwitch)
				{
					DoLagSwitch(player, admin, args);
				} else if (card == Card.SlowBurn)
				{
					DoSlowBurn(player, admin, args);
				} else if (card == Card.Flash)
				{
					DoFlash(player, admin, args);
				} else if (card == Card.AirStrike)
				{
					DoAirstrike(player, admin, args);
				} else if (card == Card.Loudfoot)
				{
					DoLoudfoot(player, admin, args);
				}


				if (config.logPaybackCommands)
				{
					if (config.logPaybackCommands)
					{
						SendToDiscordWebhook(new Dictionary<string, string>() {
						{ "Admin", $"{admin?.displayName} | {admin?.userID}" },
						{ $"{card.ToString()}", $"{player.displayName} | {player?.userID}" },
						}, $"{ConVar.Server.hostname}");
					}
				}

				//| if we are saving commands and this command can be saved, do the saving
				if (config.persistentCommands && cardsWhichCanPersist.Contains(card))
				{

					HashSet<Card> persistentCards = null;
					if (!paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
					{
						persistentCards = new HashSet<Card>();
						paybackData.persistentCommandMap[player.userID] = persistentCards;
					}
					persistentCards.Add(card);

				}

			}


		}

		bool silentPacifism = false;



        #region VISUALIZATION

        string url_payback_logo = "http://na.fragmod.com/fragimages/dec2023/payback_logo_broad.png";
        string url_payback_logo_raised = "http://na.fragmod.com/fragimages/dec2023/payback_logo_broad_down.png";


        List<BasePlayer> admins = new List<BasePlayer>();
		float ts_admin = 0;


		//| ===================================================
		List<ulong> adminsNoUI = new List<ulong>();
		[ConsoleCommand("paybacknoui2")]
		void Console_Payback_NoUI2(ConsoleSystem.Arg arg)
		{
			var player = arg.Connection?.player as BasePlayer;
			if (player != null)
			{
				if (!IsAdmin(player)) return;
				if (!adminsNoUI.Contains(player.userID))
				{
					adminsNoUI.Add(player.userID);
					PrintToPlayer(player, "paybackui disabled");
					UpdateAdminUI(player);
				}
				else
				{
					adminsNoUI.Remove(player.userID);
					PrintToPlayer(player, "paybackui enabled");
					UpdateAdminUI(player);
				}
			}
		}
		//| ===================================================


		void CacheAdmins()
		{
			if (Time.realtimeSinceStartup - ts_admin > 3)
			{
				ts_admin = Time.realtimeSinceStartup;
				admins.Clear();

				foreach (var player in BasePlayer.activePlayerList)
				{
					if (IsAdmin(player))
					{
						admins.Add(player);
					}
				}
			}
		}

		int currentActiveCardCount = 0;


		IEnumerator AdminVisualizationCo()
		{
			while (config.showUI && Payback == null)
			{
				var allCards = GetActiveCards();

				currentActiveCardCount = allCards.Count;
				//if (Payback2 != null)
				//{
				//    var otherCards = Payback2.Call("GetActiveCardsStrings") as List<string>;
				//    currentActiveCardCount += otherCards.Count;
				//}

				if (cachedCards != currentActiveCardCount)
				{
					animState = 1;
					animationFlag = true;
				}
				else
				{
					if (animState != 0)
					{
						animationFlag = true;
					}
					animState = 0;
				}
				foreach (var admin in admins)
				{
					if (admin == null) continue;
					UpdateAdminUI(admin);
				}
				animationFlag = false;
				cachedCards = currentActiveCardCount;

				CacheAdmins();

				yield return new WaitForSeconds(0.3f);
			}
		}

		HashSet<Card> GetActiveCards()
		{
			HashSet<Card> allCards = new HashSet<Card>();

			foreach (var userid in cardMap.Keys.ToArray())
			{
				var target = BasePlayer.FindByID(userid);
				if (target == null) continue;
				var cards = cardMap[userid];
				if (cards == null || cards.Count == 0) continue;
				allCards.UnionWith(cards);
			}
			return allCards;
		}

		//| Bridge to other Payback
		List<string> GetActiveCardsStrings()
		{
			List<string> cards = new List<string>();
			foreach (var card in GetActiveCards())
			{
				cards.Add(card.ToString());
			}
			return cards;
		}


		int cachedCards = 0;
		int animState = 0;
		bool animationFlag = false;

		HashSet<ulong> activePaybackUIS = new HashSet<ulong>();
		void UpdateAdminUI(BasePlayer player)
		{

			//| disable showing the UI
			if (!config.showUI)
			{
				return;
			}

			//| ===================================
			//| BASE UI SETUP
			//| ===================================
			if (player.net.connection == null) return;

			string guid = "guid_admin";
			UI2.guids.Add(guid);

			var elements = new CuiElementContainer();


			if (currentActiveCardCount == 0 || adminsNoUI.Contains(player.userID))
			{
				CuiHelper.DestroyUi(player, guid);
				activePaybackUIS.Remove(player.userID);
				return;
			}


			if (animState == 1)
			{
				CuiHelper.DestroyUi(player, "payback_logo");
			}
			if (animState == 0)
			{
				CuiHelper.DestroyUi(player, "payback_logo_raised");
			}

			//| refresh the commands
			CuiHelper.DestroyUi(player, "commands");


			//| ===================================
			//| Bounds definitions
			//| ===================================
			float width = 0.4f;
			//float height = 0.2f;
			float logoWidth = 0.125f;

			Vector4 mainBounds = new Vector4(0.5f - width / 2f, 0.5f, 0.5f + width / 2f, 0.9f);
			Vector4 logoBounds = new Vector4(0.5f - logoWidth / 2f, 0.90f, 0.5f + logoWidth / 2f, 1f);

			string output = "";
			var allCards = GetActiveCards();
			foreach (var card in allCards)
			{
				output += $"{UI2.ColorText("•", "#B00101")} {card.ToString()}" + "\n";
			}

			//if (Payback2 != null)
			//{
			//    var otherCards = Payback2.Call("GetActiveCardsStrings") as List<string>;
			//    foreach (var card in otherCards)
			//    {
			//        output += $"{UI2.ColorText("•", "#B00101")} {card.ToString()}" + "\n";
			//    }
			//}


			bool updateLogo = false;
			if (!activePaybackUIS.Contains(player.userID))
			{
				UI2.CreatePanel(elements, "Under", guid, "1 1 1 0", UI2.vectorFullscreen, null, false);
				activePaybackUIS.Add(player.userID);
				updateLogo = true;
			}
			if (animationFlag)
			{
				updateLogo = true;
			}
			if (updateLogo)
			{
				if (ImageLibrary != null)
				{
					if (animState == 0)
					{
						UI2.CreatePanel(elements, guid, "payback_logo", "1 1 1 1", logoBounds, GetImage(url_payback_logo), false, 0.0f, 0.0f, true);
					}
					else if (animState == 1)
					{
						UI2.CreatePanel(elements, guid, "payback_logo_raised", "1 1 1 1", logoBounds, GetImage(url_payback_logo_raised), false, 0.0f, 0.0f, true);
					}
				}
				else
				{
					if (animState == 0)
					{
						UI2.CreatePanel(elements, guid, "payback_logo", "1 1 1 1", logoBounds, GetImage(url_payback_logo), false, 0.0f, 0.0f, false);
					}
					else if (animState == 1)
					{
						UI2.CreatePanel(elements, guid, "payback_logo_raised", "1 1 1 1", logoBounds, GetImage(url_payback_logo_raised), false, 0.0f, 0.0f, false);
					}
				}
			}


			UI2.CreateOutlineLabel(elements, guid, "commands", output, "1 1 1 1", 28, mainBounds, TextAnchor.UpperCenter);

			//| ===================================
			//| CONDITIONAL UI UPDATES
			//| ===================================

			//send the ui updates
			if (elements.Count > 0)
			{
				CuiHelper.AddUi(player, elements);
			}
		}

        #endregion


        //| ==============================================================
        //| COMMAND Implementation
        //| ==============================================================

        string prefab_tincan = "assets/prefabs/deployable/playerioents/detectors/tincanalarm/tincan.alarm.deployed.prefab";

		Dictionary<BasePlayer, BaseEntity> tinCanMap = new Dictionary<BasePlayer, BaseEntity>();
        void DoLoudfoot(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
		{
			Worker.StaticStartCoroutine(TinCanTarget(targetPlayer));
        }
		IEnumerator TinCanTarget(BasePlayer targetPlayer)
		{
            var canEntity = GameManager.server.CreateEntity(prefab_tincan, targetPlayer.transform.position);
            canEntity.transform.position = targetPlayer.transform.position + Vector3.up * -2f;
            canEntity.Spawn();
			DestroyGroundCheck(canEntity);

            entitiesThatDealNoDamage.RemoveWhere(x => x == null || x.IsDestroyed);
            entitiesThatDealNoDamage.Add(canEntity);

			bool crouchFlag = false;
			Vector3 lastPlayerPosition = targetPlayer.transform.position;
			float tsLastAlarm = Time.time;
			float minimumTimeBetweenAlarms = 2f;

            while (targetPlayer != null && HasCard(targetPlayer.userID, Card.Loudfoot) && canEntity != null)
			{
				if (targetPlayer.IsDucked() && crouchFlag && Time.time - tsLastAlarm > minimumTimeBetweenAlarms + UnityEngine.Random.Range(0, 2))
				{
					float d = Vector3.Distance(targetPlayer.transform.position, lastPlayerPosition);
					if (d > 0.1f)
					{
                        canEntity.ClientRPC(RpcTarget.NetworkGroup("RPC_TriggerAlarm"));
						tsLastAlarm = Time.time;
                        lastPlayerPosition = targetPlayer.transform.position;
                    }
                }
				if (targetPlayer.IsDucked() && !crouchFlag)
				{
					crouchFlag = true;
					lastPlayerPosition = targetPlayer.transform.position;
                    tsLastAlarm = Time.time;
                }
                if (!targetPlayer.IsDucked())
				{
					crouchFlag = false;
                }

                canEntity.transform.position = targetPlayer.transform.position + Vector3.up * -4f;
                canEntity.SendNetworkUpdate();

                yield return new WaitForFixedUpdate();

            }

            if (targetPlayer != null)
			{
                TakeCard(targetPlayer, Card.Loudfoot);
            }

            canEntity?.Kill();

        }

        string jetfighterprefab = "assets/scripts/entity/misc/f15/f15e.prefab";
		bool airstrikeIsUnsafe = false;
		string sound_mlrs = $"assets/prefabs/gamemodes/objects/capturepoint/effects/capturepoint_progress_beep.prefab";
		string sound_mlrs_2 = $"assets/prefabs/gamemodes/objects/capturepoint/effects/capturepoint_progress_complete.prefab";
        void DoAirstrike(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
        {
            TakeCard(targetPlayer, Card.AirStrike);

            Vector2 offset = UnityEngine.Random.insideUnitCircle;
			offset.Normalize();
			offset = offset * 1200;

            F15 fighter = GameManager.server.CreateEntity(jetfighterprefab, targetPlayer.transform.position + new Vector3(offset.x, 0, offset.y)) as F15;
            fighter.enabled = false;
            fighter.Spawn();
            airstrikeIsUnsafe = false;

            if (args.Contains("unsafe"))
			{
				airstrikeIsUnsafe = true;
            }

			Vector3 precisionStrikeLocation = Vector3.zero;
			bool usePrecisionStrike = false;
			if (args.Contains("precision")) {
				airstrikeIsUnsafe = true;
                precisionStrikeLocation = RaycastStatic(adminPlayer.eyes.HeadRay());
                targetPlayer = adminPlayer;
				usePrecisionStrike = true;
            }

			Worker.StaticStartCoroutine(FighterCo(fighter, targetPlayer, adminPlayer, precisionStrikeLocation, usePrecisionStrike));

		}

		string mlrs_rocket_prefab = "assets/content/vehicles/mlrs/rocket_mlrs.prefab";
		List<BaseEntity> airstrikeRockets = new List<BaseEntity>();

        HashSet<ulong> airstrikeTargetPlayers = new HashSet<ulong>();

        IEnumerator FighterCo(F15 fighter, BasePlayer targetPlayer, BasePlayer adminPlayer, Vector3 precisionStrike, bool usePrecisionStrike)
		{


            entitiesThatDealNoDamage.RemoveWhere(x => x == null || x.IsDestroyed);
            entitiesThatDealNoDamage.Add(fighter);

            airstrikeRockets.RemoveAll(x => x == null || x.IsDestroyed);

			if (!usePrecisionStrike)
			{
                airstrikeTargetPlayers.Add(targetPlayer.userID);
            }

            float flyoverDuration = 15;
			float dipStartTime = 0.3f;
			float dipEndTime = 0.6f;
			float dipHeight = 75f;

			float tsStart = Time.time;

			float height = 100;



			Vector3 flightVector = targetPlayer.transform.position - fighter.transform.position;
			flightVector.y = 0;
			float flightDistance = 2 * flightVector.magnitude;

			flightVector.Normalize();


			Vector3 targetPosition = Vector3.zero;
			Vector3 startPosition = fighter.transform.position;
			Vector3 endPosition = startPosition + flightVector * flightDistance;
			Vector3 lastPosition = fighter.transform.position;

			
			float p = 0;

			Vector3 anchor = (startPosition + endPosition) / 2f;
			startPosition = startPosition - anchor;
            endPosition = endPosition - anchor;
			startPosition.y = 0;
			endPosition.y = 0;

			Vector3 lastKnownTargetPosition = targetPlayer.transform.position;
			if (usePrecisionStrike)
			{
				lastKnownTargetPosition = precisionStrike;
			}


			//| combat

			int rockets = 6;
			float rocketCooldown = 0.15f;
			float lastRocketTs = Time.time;
			float payloadStartTime = 0.42f;

			//| precision hud targeting

			float tsLastHudUpdate = Time.time;
			float hudUpdateDuration = 0.3f;



			while (fighter != null && !fighter.IsDead() && p < 1) {

				p = (Time.time - tsStart) / flyoverDuration;

				targetPosition = Vector3.Lerp(startPosition, endPosition, p);

				if (p > dipStartTime && p < dipEndTime) {
					float x = (p - dipStartTime) / (dipEndTime - dipStartTime);

                    targetPosition.y = startPosition.y - Mathf.Lerp(0, dipHeight, (float) Mathf.Sin(x * (float) Math.PI) );
                }

				if (!usePrecisionStrike && targetPlayer != null && !targetPlayer.IsDead()) {
					lastKnownTargetPosition = targetPlayer.transform.position;
				}
				targetPosition += lastKnownTargetPosition + Vector3.up * height;

				fighter.transform.position = targetPosition;
				fighter.transform.LookAt(targetPosition + (targetPosition - lastPosition), Vector3.up);

				lastPosition = targetPosition;
				fighter.SendNetworkUpdate();


				//| combat

				if (rockets > 0 && (Time.time - lastRocketTs) > rocketCooldown && p > payloadStartTime) {

					lastRocketTs = Time.time;
                    rockets--;

					Vector3 capturedLastPos = lastPosition;
					Vector3 capturedLastKnownPos = lastKnownTargetPosition + Vector3.up * 5;

					// delay for visual impact
                    timer.Once(1f, () => {

                        var rocket = GameManager.server.CreateEntity(mlrs_rocket_prefab, capturedLastPos, fighter.transform.rotation);
                        rocket.Spawn();

                        var rb = rocket.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = false;
							rb.useGravity = false;
                        }

                        MLRSRocket mRocket = rocket as MLRSRocket;
						if (mRocket != null)
						{
                            mRocket.enabled = false;
                        }

                        var targetVelocity = (capturedLastKnownPos - capturedLastPos);
                        targetVelocity.Normalize();
						//targetVelocity += Vector3.up * 0.1f;
                        targetVelocity *= 150;


                        rocket.SetVelocity(targetVelocity);
                        rocket.transform.LookAt(capturedLastKnownPos);
						rocket.creatorEntity = rocket;

                        airstrikeRockets.Add(rocket);

                    });



                }

				if (airstrikeRockets.Count > 0)
				{
					foreach (var r in airstrikeRockets)
					{
						if (r != null)
                        {
                            var rb = r.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                r.transform.LookAt(r.transform.position + rb.velocity);
                            }
                        }
                    }
                }

				if (p < 0.6f && Time.time - tsLastHudUpdate > hudUpdateDuration)
				{
					tsLastHudUpdate = Time.time;

                    ShowText(adminPlayer.userID, "precisionstrike", $"{ColorText($"◯", "red")}", hudUpdateDuration, lastKnownTargetPosition, 64, true);

					if (p < 0.42f)
					{
                        PlaySFX(adminPlayer, sound_mlrs);

                    }
                    else
					{
                        PlaySFX(adminPlayer, sound_mlrs_2);
                    }
                }

				yield return new WaitForFixedUpdate();
			}

			fighter?.Kill();

			if (!usePrecisionStrike)
			{
                ulong id = targetPlayer.userID;
                timer.Once(5f, () => {
                    airstrikeTargetPlayers.Remove(id);
                });
            }


            yield return null;

        }


        object OnItemPickup(Item item, BasePlayer player)
        {
			if (HasCard(player.userID, Card.NoPickup))
			{
				timer.Once(0.1f, () => { 
					if (player != null && GetAllItems(player).Contains(item))
					{
                        var velocity = player.eyes.HeadRay().direction * 5;
                        item.Drop(player.transform.position + Vector3.up * 1.5f, velocity);
                    }
				});

			}
            return null;
        }

        void DoFlash(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
        {

            Worker.StaticStartCoroutine(DoFlashCo(targetPlayer));
        }
        IEnumerator DoFlashCo(BasePlayer player)
        {


			var flashbang2 = GameManager.server.CreateEntity("assets/prefabs/weapons/flashbang/grenade.flashbang.deployed.prefab", player.transform.position) as Flashbang;
			flashbang2.Spawn();
			flashbang2.SetFuse(float.MaxValue);
			
			
			while (player != null && HasCard(player.userID, Card.Flash) && player.net.connection != null)
            {
                yield return new WaitForSeconds(1.0f + Random() * 1.75f);

				flashbang2?.ClientRPCEx(new SendInfo(new List<Connection>() { player.Connection }), null, "Client_DoFlash", player.transform.position);
				PlaySound("assets/prefabs/weapons/flashbang/effects/fx-flashbang-boom.prefab", player, false);
			}

			flashbang2?.Kill();
        }

        void DoSlowBurn(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
        {

            Worker.StaticStartCoroutine(DoSlowBurnCo(targetPlayer));
        }
		IEnumerator DoSlowBurnCo(BasePlayer player)
		{

            while (player != null && !player.IsDead() && HasCard(player.userID, Card.SlowBurn) && player.net.connection != null)
			{
				HitInfo info = new HitInfo(null, player, DamageType.Heat, 1.0f);
				player.Hurt(info);

				PlaySound("assets/bundled/prefabs/fx/fire/fire_v2.prefab", player);

				DoScreaming(player, null, true);

				Effect.server.Run("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", player, 0u, new Vector3(0f, 1f, 0f), Vector3.up);

                yield return new WaitForSeconds(1);
			}
			TakeCard(player, Card.SlowBurn);
		}
        void DoLagSwitch(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args) {

			float multiplier = 1;
			if (args != null &&  args.Length > 0)
			{
				if (!float.TryParse(args[0], out multiplier))
				{
					multiplier = 1;
				}

			}
			Worker.StaticStartCoroutine(LagSwitchCo(targetPlayer, multiplier));


		}

		IEnumerator LagSwitchCo(BasePlayer player, float multiplier = 10f) {
			yield return null;

			if (multiplier == 0)
			{
				multiplier = 1f;
			}

			Vector3 lastLagPosition = player.transform.position;
			bool detectedDead = false;

			while (player != null && HasCard(player.userID, Card.LagSwitch) && player.net.connection != null) {

				if (player.IsDead())
				{
					detectedDead = true;
				} else
				{
					if (detectedDead)
					{
                        detectedDead = false;
                        lastLagPosition = player.transform.position;
                    }
				}

				if (!player.isMounted && !detectedDead)
				{
                    player.Teleport(lastLagPosition);
                }

                float r = UnityEngine.Random.Range(0f, 2f);

				float t = 1.2f + r;

				t = t / multiplier;

                yield return new WaitForSeconds(t / 2f);

				if (player != null)
				{
                    lastLagPosition = player.transform.position;
                }

                yield return new WaitForSeconds(t / 2f);
			}

		}



        void DoOverCompensation(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
		{
			if (CompensationCo == null)
			{
				CompensationCo = Worker.StaticStartCoroutine(CheckForRecoilMods());
			}
			RecoilPlayers.Add(targetPlayer.userID);
		}
		Coroutine CompensationCo = null;
		HashSet<ulong> RecoilPlayers = new HashSet<ulong>();
		float RecoilAngle = -5f;
		IEnumerator CheckForRecoilMods()
		{
			yield return null;

            Subscribe("OnWeaponFired");

            while (RecoilPlayers.Any(x => HasCard(x, Card.OverCompensation))) {

				RecoilPlayers.RemoveWhere(x => !HasCard(x, Card.OverCompensation));

                yield return new WaitForSeconds(1);
			}
			
            Unsubscribe("OnWeaponFired");

			CompensationCo = null;
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
			if (HasCard(player.userID, Card.OverCompensation))
			{
				var lookVector = player.eyes.HeadForward();

                lookVector = Vector3.RotateTowards(lookVector, Vector3.up, Mathf.Deg2Rad * RecoilAngle, 0);

				if (lookVector.y > 0)
				{
					lookVector.y = 0;
				}

				lookVector.Normalize();
				
                player.ClientRPCPlayer<Vector3>(null, player, "ForceViewAnglesTo", lookVector);

			}

        }


        int batitemid = -2026042603;
		bool batAlwaysKills = false;
        void DoBat(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
        {
			//| this should just make the player able to hit others with the bat

			GiveItemOrDrop(targetPlayer, ItemManager.CreateByItemID(batitemid));

			if (args != null && args.Contains("kill"))
			{
				batAlwaysKills = true;
			} else
			{
				batAlwaysKills = false;
			}
        }

        public static Vector3 RaycastStatic(Ray ray, float dist = 500f)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray.origin, ray.direction, out hit, dist, visibleLayer, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return ray.origin;
        }
		
        void ApplyRagdoll(BasePlayer player, Vector3 direction, float force, bool useGravity, List<string> impactSFX, bool killOnImpact = true) {

			ResolveConflictingCommands(player, null, (int)Card.Batman, false);

			if (player.IsNpc) return;

            player.Teleport(player.transform.position + Vector3.up * 0.1f);
			
            var chair = InvisibleSit(player);

            //| orient the chair to the direction of the hit
            chair.transform.LookAt(chair.transform.position + direction * -1f);



            var playerLookDir = player.eyes.HeadForward();
            playerLookDir.y = 0;
            var rot = Quaternion.FromToRotation(Vector3.forward, playerLookDir);

            Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
            var dropped = muzzle.Drop(player.transform.position + Vector3.up * 1.5f, Vector2.zero);
            DroppedItem droppedItem = dropped as DroppedItem;
            dropped.transform.Rotate(Vector3.up, rot.eulerAngles.y);

            droppedItem.allowPickup = false;
            var body = droppedItem.GetComponent<Rigidbody>();
            droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            droppedItem.GetComponent<Rigidbody>().isKinematic = false;
            droppedItem.GetComponent<Rigidbody>().useGravity = true;

            if (droppedItem != null)
            {
                SetDespawnDuration(droppedItem, float.MaxValue);
            }
            DroppedItem muzzleBrake = droppedItem;

			chair.SetParent(muzzleBrake, true);

            var collision = muzzleBrake.GetOrAddComponent<CollisionReporter>();
			bool destroyed = false;

			timer.Once(0.5f, () => {

                player.ClientRPCPlayer<Vector3>(null, player, "ForceViewAnglesTo", chair.transform.position + direction * -1f);

				
                if (collision != null)
				{
                    collision.OnCollisionEnterCallbacks.Add(() => {

                        if (destroyed)
                        {
                            return;
                        }

                        destroyed = true;
						chair?.Kill();

						GameObject.Destroy(collision);

						if (player != null)
						{
							if (killOnImpact)
							{
                                player.Die(new HitInfo(player, player, DamageType.Suicide, 10000));
                            } else
							{
								//| prevent terrain violations
								var pos = RaycastStatic(new Ray(player.transform.position + Vector3.up, Vector3.down), 10);
								player.Teleport(pos);
                            }
                        }

						muzzleBrake?.Kill();

					});
                }
			});


            force = UnityEngine.Random.Range(force * 1.0f, force * 1.3f);
            body.velocity = direction * force;
			body.angularVelocity = new Vector3(UnityEngine.Random.Range(-1, 1f), UnityEngine.Random.Range(-1, 1f), UnityEngine.Random.Range(-1, 1f)) * force * 0.1f;

            Physics.IgnoreCollision(chair.GetComponentInChildren<Collider>(), muzzleBrake.GetComponentInChildren<Collider>());
            Physics.IgnoreCollision(player.GetComponentInChildren<Collider>(), muzzleBrake.GetComponentInChildren<Collider>());


        }



        public static int mask_static = 1 << 15 | 1 << 16 | 1 << 17 | 1 << 23 | 1 << 27 | 1 << 8 | 1 << 21 | 1 << 12 | 1 << 0 | 1 << 30;

        HashSet<ulong> currentlyScreamingPlayers = new HashSet<ulong>();
        public const string sound_scream = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
        void DoRecycle(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args) {



            Worker.StaticStartCoroutine(RecycleCo(targetPlayer, adminPlayer, args));
        }


        List<string> gibs = new List<string>() {
                "humanmeat.cooked",
                "humanmeat.cooked",
                "bone.club",
                "bone.fragments",
                "humanmeat.cooked",
            };

        IEnumerator RecycleCo(BasePlayer player, BasePlayer adminPlayer, string[] args) {

            Subscribe("CanMoveItem");
            Subscribe("CanLootEntity");

            Vector3 targetPosition = player.transform.position;
            RaycastHit hitinfo;
            if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 150, mask_static))
            {

                //hitinfo.point
                targetPosition = hitinfo.point;


            } else
			{
				yield break;
			}

            yield return null;

			//         var lookDir = player.eyes.HeadRay().direction;
			//lookDir.y = 0;
			var lookDir = adminPlayer.transform.position - targetPosition;
			lookDir.y = 0;

			
            var rot = Quaternion.LookRotation(lookDir);
            var recyclerEntity = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", targetPosition + Vector3.down, rot);
			recyclerEntity.Spawn();
			
            var chair = InvisibleSit(player);

            Recycler recycler = recyclerEntity as Recycler;


            Vector3 chairStartPos = recycler.transform.position + Vector3.up * 0.85f + recycler.transform.right * -0.25f + recycler.transform.forward * -0.05f;
            Vector3 chairEndPos = chairStartPos + Vector3.up * -1f + recycler.transform.forward * 0.35f;



            Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
            var dropped = muzzle.Drop(chairStartPos, Vector2.zero);
            DroppedItem droppedItem = dropped as DroppedItem;
            dropped.transform.Rotate(Vector3.up, rot.eulerAngles.y);

            droppedItem.allowPickup = false;
            var body = droppedItem.GetComponent<Rigidbody>();
            droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            droppedItem.GetComponent<Rigidbody>().isKinematic = false;
            droppedItem.GetComponent<Rigidbody>().useGravity = false;
            droppedItem.GetComponent<Rigidbody>().detectCollisions = false;
            droppedItem.GetComponent<Rigidbody>().freezeRotation = true;

            if (droppedItem != null)
            {
                SetDespawnDuration(droppedItem, float.MaxValue);
            }

			chair.transform.rotation = rot;
			chair.transform.position = droppedItem.transform.position;
			chair.SetParent(droppedItem, true, true);

			//chair.transform.Rotate(recycler.transform.right, -10);
			
            droppedItem.transform.position = chairStartPos;

            float duration = 10f;

			float forward = 0.2f;


			//| auto start
            //Effect.server.Run(recycler.startSound.resourcePath, recycler, 0u, Vector3.zero, Vector3.zero);
            //recycler.SetFlag(BaseEntity.Flags.On, b: true);
			
			Item rope = ItemManager.CreateByName("rope", 1, 0);
			rope.MoveToContainer(recycler.inventory);
			rope.SetFlag(global::Item.Flag.IsLocked, true);

            invulnerableEntities.Add(dropped);
            invulnerableEntities.Add(recycler);

            while (player != null && HasCard(player.userID, Card.RecyclePlayer) && !recycler.HasFlag(BaseEntity.Flags.On))
			{
				chair.SendNetworkUpdate();
				yield return new WaitForSeconds(1f);
			}

            recycler.CancelInvoke(recycler.RecycleThink);

            Effect.server.Run(recycler.startSound.resourcePath, recycler, 0u, Vector3.zero, Vector3.zero);
			recycler.SetFlag(BaseEntity.Flags.On, b: true);

			rope?.RemoveFromContainer();
			rope?.Remove();
			
            var ts = Time.realtimeSinceStartup;



            float vert = 0.38f;

            for (int i = 0; i < 6; i++)
			{
                PlaySFX(recycler.transform.position + Vector3.up * vert + recycler.transform.right * i * 0.1f + recycler.transform.right * -0.42f + recycler.transform.forward * 0.1f, "assets/bundled/prefabs/fx/player/beartrap_blood.prefab", true);
            }

			float meatCooldown = UnityEngine.Random.Range(1f, 1.5f);
			float lastMeatTS = Time.realtimeSinceStartup;


            while (player != null && HasCard(player.userID, Card.RecyclePlayer) && Time.realtimeSinceStartup - ts < duration)
			{

				DoScreaming(player, adminPlayer, true);

				var pos = Vector3.Lerp(chairStartPos, chairEndPos, (Time.realtimeSinceStartup - ts) / duration);
                droppedItem.transform.position = pos;
				droppedItem.SendNetworkUpdate();

				if (Time.realtimeSinceStartup - lastMeatTS > meatCooldown)
				{
                    meatCooldown = UnityEngine.Random.Range(0.35f, 0.5f);
                    lastMeatTS = Time.realtimeSinceStartup;
					gibs.Shuffle((uint)UnityEngine.Random.Range(1, 10000));
					var meat = ItemManager.CreateByName(gibs.First());
					var droppedMeat = meat.Drop(recycler.transform.position + Vector3.up * 1.8f + recycler.transform.right * -0.25f, (Vector3.up + UnityEngine.Random.insideUnitSphere * 0.2f) * 5f, Quaternion.Euler(UnityEngine.Random.Range(-180, 180), UnityEngine.Random.Range(-180, 180), UnityEngine.Random.Range(-180, 180)));
					timer.Once(3f, () => {
						meat?.RemoveFromContainer();
						meat?.Remove();
					});

                    droppedMeat.GetComponent<Rigidbody>().AddForceAtPosition(Vector3.forward, droppedMeat.transform.position + UnityEngine.Random.onUnitSphere);

                }



                if (!isListening)
                {
                    isListening = true;
                    Subscribe("CanLootEntity");
                    Subscribe("CanCombineDroppedItem");
                }
                if (listenTimer != null)
                {
                    listenTimer.Destroy();
                }
                listenTimer = timer.Once(60 * 15, () =>
                {
                    isListening = false;
                    this.Unsubscribe("CanCombineDroppedItem");
                    this.Unsubscribe("CanLootEntity");
                });



                yield return new WaitForSeconds(1 / 20f);
			}

            //



            var skull = ItemManager.CreateByName("skull.human");
            var droppedSkull = skull.Drop(recycler.transform.position + Vector3.up * 1.8f + recycler.transform.right * -0.25f, (Vector3.up + UnityEngine.Random.insideUnitSphere * 0.2f) * 5f, Quaternion.Euler(UnityEngine.Random.Range(-180, 180), UnityEngine.Random.Range(-180, 180), UnityEngine.Random.Range(-180, 180)));
            timer.Once(3f, () => {
                skull?.RemoveFromContainer();
                skull?.Remove();
            });
            droppedSkull.GetComponent<Rigidbody>().AddForceAtPosition(Vector3.forward, droppedSkull.transform.position + UnityEngine.Random.onUnitSphere);

            chair?.Kill();
			player?.Die();
			recycler?.Kill();
            droppedItem?.Kill();

            this.Unsubscribe("CanLootEntity");
            this.Unsubscribe("CanMoveItem");

			invulnerableEntities.RemoveWhere(x => x == null || x.IsDestroyed);

            TakeCard(player, Card.RecyclePlayer);
        }
		
        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            var player = playerLoot.GetComponent<BasePlayer>();
			if (player == null) return null;
			
            var sourceEntity = player.inventory.loot.entitySource;

			if (sourceEntity == null) return null;

            var recycler = sourceEntity as Recycler;

            if (recycler != null)
			{
                if (invulnerableEntities.Contains(sourceEntity as Recycler))
				{
					return false;
				}
			}

            return null;
        }


        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
			if (HasCard(player.userID, Card.RecyclePlayer) && container is Recycler && !IsAdmin(player))
			{
				return false;
			}
            return null;
        }

        void DoScreaming(BasePlayer player, BasePlayer attacker, bool screamSourceIsTarget = false)
        {
            if (!currentlyScreamingPlayers.Contains(player.userID))
            {

                PlayGesture(player, "friendly");
                timer.Once(2f, () =>
                {
                    if (player != null)
                        PlayGesture(player, "friendly");
                });
                timer.Once(4f, () =>
                {
                    if (player != null)
                        PlayGesture(player, "friendly");
                });

                if (screamSourceIsTarget)
                {
                    PlaySound(sound_scream, player, false);
                }
                else
                {
                    PlaySound(sound_scream, attacker, false);
                }

                currentlyScreamingPlayers.Add(player.userID);

                timer.Once(5f, () =>
                {
                    if (player != null)
                        currentlyScreamingPlayers.Remove(player.userID);
                });
            }
        }



		

        void DoPoopRocket(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args) {
            Worker.StaticStartCoroutine(PoopRocketCo(targetPlayer, adminPlayer, args));
        }
		IEnumerator PoopRocketCo(BasePlayer player, BasePlayer adminPlayer, string[] args)
		{
            if (player.isMounted)
            {
                var mount = player.GetMounted();
                mount.DismountPlayer(player, true);
            }

			
            yield return null;


			var playerLookDir = player.eyes.HeadForward();
            playerLookDir.y = 0;
			var rot = Quaternion.FromToRotation(Vector3.forward, playerLookDir);

            Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
            var dropped = muzzle.Drop(player.transform.position + Vector3.up * 0.25f, Vector2.zero);
            DroppedItem droppedItem = dropped as DroppedItem;
			dropped.transform.Rotate(Vector3.up, rot.eulerAngles.y);

            droppedItem.allowPickup = false;
            var body = droppedItem.GetComponent<Rigidbody>();
            droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            droppedItem.GetComponent<Rigidbody>().isKinematic = false;
            droppedItem.GetComponent<Rigidbody>().useGravity = true;
			
            if (droppedItem != null)
            {
                SetDespawnDuration(droppedItem, float.MaxValue);
            }
            DroppedItem muzzleBrake = droppedItem;



            Item muzzle2 = ItemManager.CreateByPartialName("muzzleboost");
            var dropped2 = muzzle2.Drop(player.transform.position + Vector3.up * 0.25f, Vector2.zero);
            DroppedItem droppedItem2 = dropped2 as DroppedItem;

            droppedItem2.allowPickup = false;
            var body2 = droppedItem2.GetComponent<Rigidbody>();
            droppedItem2.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            droppedItem2.GetComponent<Rigidbody>().isKinematic = true;
            droppedItem2.GetComponent<Rigidbody>().useGravity = false;
            droppedItem2.GetComponentInChildren<Collider>().enabled = false;


            if (droppedItem2 != null)
            {
                SetDespawnDuration(droppedItem2, float.MaxValue);
            }
            DroppedItem muzzleBrake2 = droppedItem2;

            muzzleBrake2.SetParent(muzzleBrake, true);


            var chair = InvisibleSit(player);
            chair.transform.position = muzzleBrake.transform.position + Vector3.down * 0.5f;
            chair.SetParent(muzzleBrake2, true);
			chair.GetComponentInChildren<Collider>().enabled = false;


            AnimationCurve thrustCurve = new AnimationCurve();

            thrustCurve.AddKey(0, 1f);
            thrustCurve.AddKey(1, 1);


            float startTime = Time.realtimeSinceStartup;
			float fuelDuration = 10f;

			float maxThrust = 185f;
			float p = 0;
			float thrustPercentage = 0;


            Vector3 defaultThrustDirection = Vector3.up;
            Vector3 resultingThrust = Vector3.up;


			// poop fx related
			float tsLastPoopFired = Time.realtimeSinceStartup;

			//float poopPerSecond = 5f;
			float poopPerSecond = 20f;

			var collision = muzzleBrake.GetOrAddComponent<CollisionReporter>();
			collision.OnCollisionEnterCallbacks.Add(() => {
				muzzleBrake?.Kill();
			});


            //float turningAngle = 20f;
            float turningAngle = 40f;

			body.velocity = playerLookDir * 1 + Vector3.up * 2f;

			Physics.IgnoreCollision(chair.GetComponentInChildren<Collider>(), muzzleBrake.GetComponentInChildren<Collider>());
			Physics.IgnoreCollision(muzzleBrake.GetComponentInChildren<Collider>(), muzzleBrake2.GetComponentInChildren<Collider>());
            Physics.IgnoreCollision(chair.GetComponentInChildren<Collider>(), muzzleBrake2.GetComponentInChildren<Collider>());


            Vector3 lastPosition = Vector3.zero;

			float lastVoiceLine = Time.realtimeSinceStartup;
            float voiceLineInterval = 0.25f;

            while (player != null && !player.IsDead() && muzzleBrake != null && p <= 1 && HasCard(player.userID, Card.PoopRocket)) {
				
                lastPosition = muzzleBrake.transform.position;

                p = (Time.realtimeSinceStartup - startTime) / fuelDuration;

                thrustPercentage = thrustCurve.Evaluate(p);


				defaultThrustDirection = Vector3.up;

                resultingThrust = defaultThrustDirection * maxThrust * thrustPercentage;

                body.AddForce(resultingThrust * Time.deltaTime, ForceMode.Force);

				muzzleBrake2.transform.LookAt(body.transform.position + body.velocity.normalized);


				muzzleBrake.SendNetworkUpdate_Position();
				muzzleBrake2.SendNetworkUpdate_Position();
				chair.SendNetworkUpdate_Position();

                var position = body.transform.position + chair.transform.up * -0.05f + chair.transform.forward * -0.25f;

                PlaySFX(body.transform.position + body.transform.up * -1f + muzzleBrake2.transform.forward * -0.5f, "assets/bundled/prefabs/fx/impacts/jump-start/barefoot/sand/jump-land-sand.prefab", false);

				if (Time.realtimeSinceStartup - lastVoiceLine > voiceLineInterval)
				{
					lastVoiceLine = Time.realtimeSinceStartup;
                    PlaySFX(body.transform.position, "assets/bundled/prefabs/fx/player/gutshot_scream.prefab", false);
                    PlaySFX(player, "assets/prefabs/weapons/hammer/effects/strike_screenshake.prefab", false);
                }

                for (int i = 0; i < 5; i++)
                {


                    tsLastPoopFired = Time.realtimeSinceStartup;

                    var item = ItemManager.CreateByName("horsedung", 1, 0);


                    //var direction = (chair.transform.up * -1 * 5 + UnityEngine.Random.onUnitSphere * 2) * 0.2f * 2;
                    var direction = (body.velocity.normalized * -1 * 5 + UnityEngine.Random.onUnitSphere * 2) * 0.2f * 2;


                    var d2 = item.Drop(position, direction, Quaternion.Euler(UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180)));


                    var b2 = d2.GetComponentInChildren<Rigidbody>();
                    if (b2 != null)
                    {
                        float power = 10;
                        b2.angularVelocity = new Vector3(UnityEngine.Random.value * power, UnityEngine.Random.value * power, UnityEngine.Random.value * power);
                    }

                    Physics.IgnoreCollision(muzzleBrake.GetComponentInChildren<Collider>(), d2.GetComponentInChildren<Collider>());
                    Physics.IgnoreCollision(muzzleBrake2.GetComponentInChildren<Collider>(), d2.GetComponentInChildren<Collider>());
                    Physics.IgnoreCollision(chair.GetComponentInChildren<Collider>(), d2.GetComponentInChildren<Collider>());
                    d2.GetComponentInChildren<Collider>().enabled = false;
                    //literalShit.Add(item);

                    //PrintToChat($"Spew | sum: {sum} p: {p} Plim {1f / maxRate * 60} rate: {currentRate} ms ");


                    timer.Once(2f, () =>
                    {
                        if (item == null) return;

                        item.RemoveFromContainer();
                        item.RemoveFromWorld();
                        item.Remove();

                    });


                }



                if (!isListening)
                {
                    isListening = true;
                    Subscribe("CanCombineDroppedItem");
                }
                if (listenTimer != null)
                {
                    listenTimer.Destroy();
                }
                listenTimer = timer.Once(60 * 15, () =>
                {
                    isListening = false;
                    this.Unsubscribe("CanCombineDroppedItem");
                });


                yield return new WaitForSeconds(1/60f);
			}

			
            PlaySFX(lastPosition, "assets/content/effects/explosions/underwater/explosion_underwater.prefab", false);

            muzzleBrake?.Kill();
			muzzleBrake2?.Kill();
			
			if (player != null)
			{
				player.Die();
			}

			TakeCard(player, Card.PoopRocket);

		}

		class CollisionReporter : MonoBehaviour {

			public List<System.Action> OnCollisionEnterCallbacks = new List<Action>();
			void OnCollisionEnter(Collision collision)
			{
				var ent = collision.GetEntity();
				if (ent != null && ent is DroppedItem) return;
				
				OnCollisionEnterCallbacks.ForEach(x => x());

            }
        }

        void DoVerbalDiarrhea(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args) {
			Worker.StaticStartCoroutine(DiarrheaCo(targetPlayer, adminPlayer, args));
		}

        Timer listenTimer = null;


        IEnumerator DiarrheaCo(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args) {

			
            float sum = 0;

			float tsLastSpew = 0;

			float maxRate = 1 / 60f;
			float minRate = 1f;

			float currentRate = minRate;

			//List<Item> literalShit = new List<Item>();



			while (targetPlayer != null && HasCard(targetPlayer.userID, Card.VerbalDiarrhea))
			{

				float timestampSpoken = 0;
                //get the time since last voice data

				if (recentPlayerVoiceTimestamps.TryGetValue(targetPlayer.userID, out timestampSpoken))
				{
                    var durationSinceLastSpoken = Time.realtimeSinceStartup - timestampSpoken;
					var durationSinceLastSpew = Time.realtimeSinceStartup - tsLastSpew;
					
					
					// did we speak recently
					if (durationSinceLastSpoken < 0.15f && durationSinceLastSpew >= currentRate) {

						//| continue spew

						sum++;

						float p = sum / (35);

                        currentRate = Mathf.Lerp(maxRate, minRate, 1-p);

						tsLastSpew = Time.realtimeSinceStartup;

						if (sum < 20)
						{
							tsLastSpew += UnityEngine.Random.value * .25f;
						}

                        var item = ItemManager.CreateByName("horsedung", 1, 0);


                        var direction = (targetPlayer.eyes.HeadRay().direction * 5 + UnityEngine.Random.onUnitSphere * 2) * 0.2f * 2;
						var position = targetPlayer.eyes.HeadRay().origin + targetPlayer.eyes.HeadRay().direction * 0.15f + Vector3.down * 0.02f;

						if (targetPlayer.isMounted)
						{
							position += Vector3.down * 0.28f - targetPlayer.eyes.HeadRay().direction * 0.15f;
						}


						if (sum > 100)
						{
							float powerLerp = Mathf.Lerp(1, 7.5f, sum / 5000f);
							direction *= powerLerp;
						}

                        var dropped = item.Drop(position, direction, Quaternion.Euler(UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180), UnityEngine.Random.Range(0, 180)));


                        var body = dropped.GetComponentInChildren<Rigidbody>();
                        if (body != null)
                        {
                            float power = 10;
							body.angularVelocity = new Vector3(UnityEngine.Random.value * power, UnityEngine.Random.value * power, UnityEngine.Random.value * power);
                        }


                        //literalShit.Add(item);

						//PrintToChat($"Spew | sum: {sum} p: {p} Plim {1f / maxRate * 60} rate: {currentRate} ms ");
						

						timer.Once(2f, () => {
							if (item == null) return;

							item.RemoveFromContainer();
							item.RemoveFromWorld();
                            item.Remove();
							
                        });
                    }
                }


                yield return new WaitForSeconds(maxRate);

                if (!isListening)
                {
                    isListening = true;
                    Subscribe("OnPlayerVoice");
                    Subscribe("CanCombineDroppedItem");
                }

				
                if (listenTimer != null)
                {
                    listenTimer.Destroy();
                }
                listenTimer = timer.Once(60 * 15, () =>
                {
                    isListening = false;
                    this.Unsubscribe("OnPlayerVoice");
                    this.Unsubscribe("CanCombineDroppedItem");
                });
            }

			isListening = false;
            this.Unsubscribe("OnPlayerVoice");
            this.Unsubscribe("CanCombineDroppedItem");
        }

		int horseDungItemId = -1579932985;

        object CanCombineDroppedItem(DroppedItem dropped, DroppedItem di) { 
			if (dropped.item.info.itemid == horseDungItemId)
			{
				return false;
			}
			if (gibs.Contains(dropped.item.info.shortname)) {
				return false;
			}
            
			
            return null;
		}


        bool isListening = false;

        Dictionary<ulong, float> recentPlayerVoiceTimestamps = new Dictionary<ulong, float>();


        void DoTrain(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
		{
			if (!targetPlayer.IsSleeping()) PlayNetworkAnimation(targetPlayer, $"cinematic_play idle_stand_handcuff {targetPlayer.UserIDString}");


            RaycastHit hitinfo;
			if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 50))
			{
				//hitinfo.normal

				//var pos = hitinfo.point + hitinfo.normal * -0.35f;
				var pos = hitinfo.point + hitinfo.normal * -0.25f;

                MicrophoneStand microphone = null;

                var stand = GameManager.server.CreateEntity("assets/prefabs/voiceaudio/microphonestand/microphonestand.deployed.prefab",
                        pos
                        );
                
				stand.Spawn();
				stand.transform.LookAt(pos + hitinfo.normal * -1f);


				var lookDir = adminPlayer.eyes.HeadRay().direction;
				lookDir.y = 0;
				var y = Vector3.Angle(lookDir, Vector3.forward);

				stand.transform.Rotate(Vector3.forward, y);
                
                targetPlayer.Teleport(stand.transform.position);
                
                microphone = stand as MicrophoneStand;
                microphone.SpawnChildEntity();

                //microphone.MountPlayer(targetPlayer as BasePlayer);

                
                invulnerableEntities.Add(stand);





				var chair = microphone as BaseMountable;
                chairsPreventingDismount.Add(chair);

                GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
                GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

                if (targetPlayer.isMounted)
                {
                    targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);
                }

                Timer t = null;
                t = timer.Every(0.25f, () => {
                    if (chair == null || chair.IsDestroyed)
                    {
                        t.Destroy();
                        return;
                    }
                    if (targetPlayer != null && HasCard(targetPlayer.userID, Card.Train) && !targetPlayer.IsDead())
                    {
                        if (!targetPlayer.isMounted)
                        {
                            targetPlayer.Teleport(chair.transform.position);
                            targetPlayer.SendNetworkUpdateImmediate();

                            chair.MountPlayer(targetPlayer);
                            chair.SendNetworkUpdateImmediate();
                        }

                        if (Vector3.Distance(targetPlayer.transform.position, chair.transform.position) > 1)
                        {
                            targetPlayer.Teleport(chair.transform.position);
                            targetPlayer.SendNetworkUpdateImmediate();
                        }

                        //PrintToChat($"D: {}");
                    }
                    else
                    {
                        //Puts("Attempted to mount player to chair, but they were null!");
                        chair.Kill();
                        t.Destroy();
                    }

                });



                //| ================================
                //| HACK TO SPAWN A TRAIN ANYWHERE
                //| ================================
                var go = new GameObject();
                var spline = go.AddComponent<TrainTrackSpline>();
				var sphere = go.AddComponent<SphereCollider>();
				sphere.isTrigger = false;
				go.layer = 16;
				go.transform.position = stand.transform.position;

				//timer.Once(1f, () => {
    //                GameObject.Destroy(go);
				//	//PrintToChat($"Cleanup spline");
				//});
                //| ================================



                //| ================================
                //| NEEDS A DELAY SO THE TRAIN SPLINE KICKS IN
                //| ================================
                timer.Once(0.1f, () => {
                    if (targetPlayer == null) return;

                    //| ================================
                    //| TEST THE SPLINE IS WORKING
                    //| ================================
                    //TrainTrackSpline t1 = null;
                    //float dist = 0;
                    //bool tracks = global::TrainTrackSpline.TryFindTrackNear(stand.transform.position, 15f, out t1, out dist);
                    //PrintToChat($"Track found: {t1} dist {dist}");





					Worker.StaticStartCoroutine(AnimateTrainCo(targetPlayer, microphone, go));


                   
                });
                //| ================================




            }
            else
			{
				PrintToPlayer(adminPlayer, "missed the ground");
			}

            
		}

        IEnumerator AnimateTrainCo(BasePlayer targetPlayer, MicrophoneStand stand, GameObject trainspline)
		{



            var moveDir = stand.transform.right * -1f;
            float moveDist = 250f;
			trainspline.transform.position = stand.transform.position + moveDir * -1 * moveDist;

            //var loco = GameManager.server.CreateEntity("assets/content/vehicles/locomotive/locomotive.entity.prefab", stand.transform.position + moveDir * -1 * moveDist);
            var loco = GameManager.server.CreateEntity("assets/content/vehicles/trains/locomotive/locomotive.entity.prefab", stand.transform.position + moveDir * -1 * moveDist);



            //trains.Add(loco as TrainEngine);
            loco.Spawn();
            var train = loco.GetComponentInChildren<TrainEngine>();

            invulnerableEntities.Add(loco);
            entitiesThatDealNoDamage.Add(loco);


            CreateGameTip("LOOK OUT!! ------>>>>", targetPlayer, 5, true);

			var hurts = train.GetComponentsInChildren<TriggerHurtNotChild>();
			//PrintToChat($"HURTS: {hurts.Length}");
			foreach (var h in hurts) {


                //PrintToChat($"HURTS: {h.name} DPS: {h.DamagePerSecond}");
				h.SetActive(false);
				h.DamagePerSecond = 0;
            }


            
            train.SetFlag(TrainEngine.Flag_Horn, true, false, true);

			train.collisionDamageDivide = float.MaxValue;

            var triggers = train.GetComponentsInChildren<TriggerBase>();
			foreach (var t in triggers)
			{
				t.gameObject.SetActive(false);
				t.interestLayers = 0;
			}




            Vector3 startPos = stand.transform.position + moveDir * -1 * moveDist + stand.transform.up * 1.5f + Vector3.up * 0.25f;
            Vector3 endPos = startPos + moveDir * 2 * moveDist;

            
            float travelTime = 16;
			float timestamp = Time.realtimeSinceStartup;


			bool didKillPlayer = false;


            while (train != null && targetPlayer != null && Time.realtimeSinceStartup - timestamp < travelTime * 0.75f) {
                
				float p = (Time.realtimeSinceStartup - timestamp) / travelTime;
				train.transform.position = Vector3.Lerp(startPos, endPos, p);
                			
				train.transform.LookAt(endPos + moveDir * 2 * moveDist);

                if ( p > 0.47f && !didKillPlayer)
				{
					targetPlayer.Die();
					didKillPlayer = true;
                    
					PlaySound("assets/bundled/prefabs/fx/hit_notify.prefab", targetPlayer, false);
					PlaySound("assets/bundled/prefabs/fx/impacts/blunt/flesh/fleshbloodimpact.prefab", targetPlayer, false);
                    
				}

				yield return new WaitForFixedUpdate();
			}

            train?.Kill();

            TakeCard(targetPlayer, Card.Train);

            GameObject.Destroy(trainspline);//| kill off later to avoid error spam
		}






		HashSet<BaseEntity> mines = new HashSet<BaseEntity>();
        void DoMinefield(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
		{
			Worker.StaticStartCoroutine(PlaceMinefield(targetPlayer, args));
		}
        IEnumerator PlaceMinefield(BasePlayer targetPlayer, string[] args)
		{
			int maxMines = 600;
            
            int density = 25;

			int range = 5;
            
            if (args.Length > 1)
			{
				int.TryParse(args[1], out density);
				//PrintToChat($"Mine Argument: {args[1]}");
			}

			int targetMines = (int) (maxMines * (density / 100f));

			int minDistToPlayer = 5;
            
			while (targetPlayer != null && HasCard(targetPlayer.userID, Card.Minefield)) {

				var colliders = Physics.OverlapSphere(targetPlayer.transform.position, range + minDistToPlayer);

				var mineCount = colliders.Count(x => x.gameObject.GetComponentInParent<Landmine>() != null);

				int minesToPlace = targetMines - mineCount;

                //PrintToChat($"Mines: {mines.Count} Mines Nearby: {mineCount} Mines to place: {minesToPlace}");
                
                for (int i = 0; i < minesToPlace; i ++)
				{
					if (targetPlayer == null || !HasCard(targetPlayer.userID, Card.Minefield)) continue;

					var position = targetPlayer.transform.position;


                    var facing = targetPlayer.eyes.HeadRay().direction;
                    facing.y = 0;
                    facing.Normalize();

					position += facing * minDistToPlayer;


                    var direction = UnityEngine.Random.onUnitSphere;
					direction.y = 0;
                    
                    position = position + direction * 2 + direction * UnityEngine.Random.Range(0f, range + minDistToPlayer);

                    var distanceToPlayer = Vector3.Distance(position, targetPlayer.transform.position);
                    
					if (distanceToPlayer < minDistToPlayer) continue;

                    RaycastHit hit;
                    if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out hit, 2f, visibleLayer, QueryTriggerInteraction.Ignore))
                    {
                        position = hit.point;
                    }
                    else
                    {
                        continue;
                    }
                

                    var mine = GameManager.server.CreateEntity("assets/prefabs/deployable/landmine/landmine.prefab", position);
                    Landmine landmine = mine as Landmine;
                    mine.Spawn();
					mines.Add(mine);

                    //Line(targetPlayer, position, position + Vector3.up * 10, Color.red, 5f);
                    //PrintToChat($"Spawn Mine");


                    timer.Once(15f + UnityEngine.Random.Range(-5f, 5f), () =>
                    {
                        if (mine != null)
                        {
                            mine.Kill();
                        }
                    });


                    entitiesThatDealNoDamage.Add(mine);

                    yield return new WaitForSeconds(0.02f);


                }

                yield return new WaitForSeconds(0.15f);
			}
            foreach (var mine in mines)
			{
				mine.Kill();
			}
		}


		uint payback_hammer_skin = 2860424516;

        HashSet<ulong> bonkingAdmins = new HashSet<ulong>();
        void DoBonk(BasePlayer targetPlayer, BasePlayer adminPlayer)
		{
			if (adminPlayer.inventory.FindItemByItemID("hammer.salvaged") == null) {
                var hammer = ItemManager.CreateByName("hammer.salvaged", 1, payback_hammer_skin);
                GiveItemOrDrop(adminPlayer, hammer, false);
            }


            if (targetPlayer == adminPlayer)
			{
				bonkingAdmins.Add(targetPlayer.userID);
            }
		}
		HashSet<ulong> bonkedPlayers = new HashSet<ulong>();
		HashSet<ulong> recentlyBonkedPlayers = new HashSet<ulong>();
		Dictionary<ulong, List<BaseEntity>> bonkedEntities = new Dictionary<ulong, List<BaseEntity>>();
        //private const int visibleLayer = Layers.Mask.Deployed | Layers.Mask.Default | Layers.Mask.Construction | Layers.Mask.World | Layers.Mask.Terrain;
        private const int visibleLayer = Layers.Mask.Default | Layers.Mask.Construction | Layers.Mask.World | Layers.Mask.Terrain;

		Dictionary<BaseEntity, ulong> craterMap = new Dictionary<BaseEntity, ulong>();

        void ToggleBonk(BasePlayer targetPlayer, BasePlayer adminPlayer, bool forceRemove = false)
		{

			if (targetPlayer == null) return;
            
            
            if (bonkedPlayers.Contains(targetPlayer.userID) || forceRemove)
			{

                recentlyBonkedPlayers.Add(targetPlayer.userID);

				ulong userid = targetPlayer.userID;
				timer.Once(60 * 5, () => {
					recentlyBonkedPlayers.Remove(userid);
				});

                bonkedPlayers.Remove(targetPlayer.userID);

				if (bonkedEntities.ContainsKey(targetPlayer.userID))
				{
                    var entities = bonkedEntities[targetPlayer.userID];
                    foreach (var ent in entities)
                    {
                        ent?.Kill();
                    }
                }
                
            }
            else
			{

                ResolveConflictingCommands(targetPlayer, adminPlayer, (int)Card.Bonk);

                bonkedPlayers.Add(targetPlayer.userID);

                Vector3 spawnPos = targetPlayer.transform.position;
				RaycastHit hit;
				if (Physics.Raycast(targetPlayer.transform.position + Vector3.up, Vector3.down, out hit, 2f, visibleLayer, QueryTriggerInteraction.Ignore))
				{
					spawnPos = hit.point;
				}

                var chair = InvisibleSit(targetPlayer);

				chair.transform.position = spawnPos;

                //chair.transform.Rotate(chair.transform.right * -1, 45);
                chair.transform.Rotate(chair.transform.right * -1, 35);


                chair.transform.position = chair.transform.position + Vector3.down * 0.75f;


                var adminPos = adminPlayer.transform.position;
                adminPos.y = chair.transform.position.y;


                //chair.transform.LookAt(adminPos);
				chair.transform.position += (adminPos - chair.transform.position).normalized * 0.75f;


				var directionToHit = adminPlayer.transform.position - chair.transform.position;
				directionToHit.y = 0;
				directionToHit.Normalize();


				Quaternion q = Quaternion.LookRotation(directionToHit);
				chair.transform.Rotate(Vector3.up, q.eulerAngles.y, Space.World);



				bonkedEntities[targetPlayer.userID] = new List<BaseEntity>();
                bonkedEntities[targetPlayer.userID].Add(chair);

                invulnerableEntities.Add(chair);


				var horz = chair.transform.up;
				horz.y = 0;
				horz.Normalize();

                var crater = GameManager.server.CreateEntity("assets/prefabs/tools/surveycharge/survey_crater.prefab", chair.transform.position + chair.transform.up + horz * 0.15f + Vector3.down * 0.05f);
                crater.Spawn();

                invulnerableEntities.Add(crater);
                bonkedEntities[targetPlayer.userID].Add(crater);

				craterMap[crater] = targetPlayer.userID;

            }

        }


        void OnAnalysisComplete(SurveyCrater surveyCrater, BasePlayer player)
        {
            if (craterMap.ContainsKey(surveyCrater))
            {
                var userid = craterMap[surveyCrater];
                //| crater is part of payback
                var bonkedPlayer = BasePlayer.FindByID(userid);
                if (bonkedPlayer != null)
                {
                    CreateGameTip($"ANALYSIS COMPLETE\n\n{ColorText(bonkedPlayer.displayName, "white")}\n\nIS HUMAN TRASH", player, 5, false);
                    CreateGameTip($"ANALYSIS COMPLETE\n\n{ColorText(bonkedPlayer.displayName, "white")}\n\nIS HUMAN TRASH", bonkedPlayer, 10, false);
                }
            }
        }


        void DoBuggedGun(BasePlayer targetPlayer, BasePlayer adminPlayer)
		{
			Subscribe("OnSignalBroadcast");
			Worker.StaticStartCoroutine(CheckBuggedGunSubscriptionCo(targetPlayer));
        }
		IEnumerator CheckBuggedGunSubscriptionCo(BasePlayer targetPlayer)
		{
            while (targetPlayer != null && HasCard(targetPlayer.userID, Card.BuggedGun))
			{
				yield return new WaitForSeconds(1);
			}
			Unsubscribe("OnSignalBroadcast");
        }

        object OnSignalBroadcast(BaseEntity entity)
		{
			//PrintToChat($"OnSignalBroadcast : is base proj: {entity is BaseProjectile}");

			if (entity is BaseProjectile)
			{

				var baseProj = entity as BaseProjectile;
				if (baseProj.primaryMagazine.contents == 0) return null;

				var player = entity.GetComponentInParent<BasePlayer>();

				if (player != null)
				{
					if (!HasCard(player.userID, Card.BuggedGun)) return null;

					float chance = 0;

					chance = Mathf.Lerp(0f, 1f, 1 / (baseProj.primaryMagazine.capacity / 10f) );

					//PrintToChat($"Chance for {baseProj.ShortPrefabName} -> {chance}");

					if (UnityEngine.Random.value > chance )
					{
						return null;//skipping to make it feel more natural
					}

                    var activeItem = player.GetActiveItem();
					int index = 0;
					for (int i = 0; i < 6; i++)
					{
						if (player.inventory.containerBelt.GetSlot(i) == activeItem)
						{
							index = i;
						}
					}

                    activeItem.MoveToContainer(player.inventory.containerMain);

					player.SendNetworkUpdate();
					activeItem.MarkDirty();

                    timer.Once(0.1f, () =>
					{
						if (player != null && activeItem != null && player.inventory.containerMain.itemList.Contains(activeItem))
						{
							activeItem.MoveToContainer(player.inventory.containerBelt, index);

						}
                    });
				}

				return null;
			}
            return null;
		}


        void DoRadiation(BasePlayer targetPlayer, BasePlayer adminPlayer) {
			Worker.StaticStartCoroutine(RadsCo(targetPlayer));
        }

		IEnumerator RadsCo(BasePlayer target)
		{
			while (target != null && HasCard(target.userID, Card.Radiation))
			{
                if (target.metabolism.radiation_poison.value < 500)
                {
                    target.metabolism.radiation_level.SetValue(100);
                    if (target.metabolism.radiation_poison.value < 500)
                    {
                        target.metabolism.radiation_poison.SetValue((target.metabolism.radiation_poison.value + 1) * 1.05f);
                    }
                    target.SendNetworkUpdate();
                }
				yield return new WaitForSeconds(0.5f);
            }
		}

        public Dictionary<ulong, HashSet<BaseEntity>> crucifyEntities = new Dictionary<ulong, HashSet<BaseEntity>>();

		string beachchairprefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab";
		void DoCrucifyCommand(BasePlayer targetPlayer, BasePlayer adminPlayer)
		{
			if (targetPlayer == null) return;

			if (HasCard(targetPlayer.userID, Card.Crucify))
			{
				if (adminPlayer == null) return;


				HashSet<BaseEntity> entities = null;
				if (!crucifyEntities.TryGetValue(targetPlayer.userID, out entities))
				{
					entities = new HashSet<BaseEntity>();
					crucifyEntities[targetPlayer.userID] = entities;
				}

				if (targetPlayer.isMounted)
				{
					targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);

					var car = targetPlayer.GetMountedVehicle();
					if (car != null)
					{
						car.Kill(BaseNetworkable.DestroyMode.Gib);
					}

					BaseEntity chair = null;
					if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
					{
						chair?.Kill();
					}
				}

				RaycastHit hitinfo;
				if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 50))
				{


					var chair = GameManager.server.CreateEntity(beachchairprefab, hitinfo.point + Vector3.up);
					var mount = chair as BaseMountable;
					chair.Spawn();

					

					sitChairMap[targetPlayer.userID] = chair;
					//targetPlayer.Teleport(chair.transform.position + chair.transform.forward * 0.5f);
					targetPlayer.EndSleeping();

					GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
					GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

					Vector3 lookAtPosition = adminPlayer.transform.position;
					lookAtPosition.y = mount.transform.position.y;

					Vector3 lookAtDir = (lookAtPosition - mount.transform.position).normalized;
					lookAtDir = Vector3.RotateTowards(lookAtDir, Vector3.down, Mathf.Deg2Rad * 50f, 1000);

					Vector3 lookForwardDir = adminPlayer.transform.position - mount.transform.position;


					Vector3 lookRightDir = Quaternion.Euler(0, 90, 0) * lookForwardDir.normalized;
					
					//| CROSS


					//| Stick em with the spear
					var spearItem = ItemManager.CreateByName("spear.stone");
					var droppedSpear = spearItem.Drop(chair.transform.position + Vector3.down * 0.25f, Vector3.zero) as DroppedItem;
					//droppedSpear.transform.position += chair.transform.forward * -0.05f;
					//droppedSpear.transform.position += lookForwardDir.normalized * 0.05f;
					droppedSpear.transform.position += lookForwardDir.normalized * 0.1f;
					droppedSpear.GetComponent<Rigidbody>().isKinematic = true;
					droppedSpear.GetComponent<Rigidbody>().useGravity = false;
					droppedSpear.GetComponent<Rigidbody>().velocity = Vector3.zero;
					droppedSpear.allowPickup = false;
					SetDespawnDuration(droppedSpear, 1000000);
					entities.Add(droppedSpear);
					droppedSpear.transform.Rotate(droppedSpear.transform.right, -90);
					droppedSpear.transform.position += Vector3.up * 2;


					var spearItem2 = ItemManager.CreateByName("spear.stone");
					var droppedSpear2 = spearItem2.Drop(chair.transform.position + Vector3.down * 0.25f, Vector3.zero) as DroppedItem;
					//droppedSpear.transform.position += chair.transform.forward * -0.05f;
					droppedSpear2.transform.position = droppedSpear.transform.position + Vector3.down * 3f;
					droppedSpear2.GetComponent<Rigidbody>().isKinematic = true;
					droppedSpear2.GetComponent<Rigidbody>().useGravity = false;
					droppedSpear2.GetComponent<Rigidbody>().velocity = Vector3.zero;
					droppedSpear2.allowPickup = false;
					SetDespawnDuration(droppedSpear2, 1000000);
					entities.Add(droppedSpear2);
					droppedSpear2.transform.Rotate(droppedSpear2.transform.right, 90);


					var spearItem3 = ItemManager.CreateByName("spear.stone");
					var droppedSpear3 = spearItem3.Drop(chair.transform.position + Vector3.down * 0.25f, Vector3.zero) as DroppedItem;
					//droppedSpear.transform.position += chair.transform.forward * -0.05f;
					//droppedSpear3.transform.position = droppedSpear.transform.position + Vector3.down * 0.65f;
					//droppedSpear3.transform.position += chair.transform.forward * 1;
					droppedSpear3.transform.position = chair.transform.position;
					droppedSpear3.GetComponent<Rigidbody>().isKinematic = true;
					droppedSpear3.GetComponent<Rigidbody>().useGravity = false;
					droppedSpear3.GetComponent<Rigidbody>().velocity = Vector3.zero;
					droppedSpear3.allowPickup = false;
					SetDespawnDuration(droppedSpear3, 1000000);
					entities.Add(droppedSpear3);


					droppedSpear3.transform.LookAt(droppedSpear3.transform.position + lookForwardDir);
					droppedSpear3.transform.Rotate(droppedSpear2.transform.forward, 90);

					droppedSpear3.transform.position += droppedSpear3.transform.forward * 0.85f;
					droppedSpear3.transform.position += Vector3.up * 1.2f;

					droppedSpear3.transform.position += lookForwardDir.normalized * 0.1f;



     //               droppedSpear3.transform.position += Vector3.up * 3.2f;
					//droppedSpear3.SendNetworkUpdateImmediate();


                    timer.Once(0.25f, () => {

						if (targetPlayer != null)
						{
							mount.MountPlayer(targetPlayer);


							//chair.transform.LookAt(lookAtPosition);
							chair.transform.LookAt(mount.transform.position + lookAtDir);
							

							Worker.StaticStartCoroutine(SitCo(targetPlayer));


							chair.transform.position += lookForwardDir.normalized * -0.5f;

							chair.SendNetworkUpdateImmediate();


							//| weird reposition issue
							foreach (var ent in entities)
							{
								ent.transform.position += Vector3.down * 0.15f;
								ent.transform.position += Vector3.back * 0.36f;

								ent.transform.position += lookForwardDir.normalized * 0.2f;
							}

							timer.Once(0.15f, () =>
							{
								if (targetPlayer == null) return;
								if (chair == null) return;

								//| Kill off the chair for everyone but the target
								List<Connection> cons = new List<Connection>();

								foreach (var c in chair.GetSubscribers())
								{
									if (c.userid != targetPlayer.userID)
									{
										cons.Add(c);
									}
								}

								var netwrite = Net.sv.StartWrite();

                                netwrite.PacketID(Message.Type.EntityDestroy);
                                netwrite.EntityID(chair.net.ID);
                                netwrite.UInt8((byte)BaseNetworkable.DestroyMode.None);
                                netwrite.Send(new SendInfo(cons));
								

							});


						}
						else
						{
							//Puts("Attempted to mount player to chair, but they were null!");
							chair.Kill();
						}

					});

				}

			}
			else
			{
				BaseEntity chair = null;
				if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
				{
					if (chair != null)
					{
						chair.Kill();
					}
				}

				HashSet<BaseEntity> entities = null;
				if (crucifyEntities.TryGetValue(targetPlayer.userID, out entities))
				{
					foreach (var r in entities)
					{
						r?.Kill();
					}
					crucifyEntities.Remove(targetPlayer.userID);
					entities.Clear();
				}

			}
		}


		

		public void Line(BasePlayer player, Vector3 from, Vector3 to, Color color, float duration)
		{
			player.SendConsoleCommand("ddraw.line", duration, color, from, to);
		}

		#region Spitroast

		public Dictionary<ulong, HashSet<BaseEntity>> roastEntities = new Dictionary<ulong, HashSet<BaseEntity>>();
		void DoSpitroastCommand(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args)
		{
			if (targetPlayer == null) return;


			float speed = 1f;
			if (args != null && args.Length > 1)
			{
				float.TryParse(args[1], out speed);
				speed = 55;//overide 55 because who wants less than full power amiright
			}
			//if (!HasCard(targetPlayer.userID, Card.Spitroast))
			//{
			//    ResolveConflictingCommands(targetPlayer, adminPlayer);
			//}

			if (HasCard(targetPlayer.userID, Card.Spitroast))
			{
				if (adminPlayer == null) return;

				if (targetPlayer.isMounted)
				{
					targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);

					var car = targetPlayer.GetMountedVehicle();
					if (car != null)
					{
						car.Kill(BaseNetworkable.DestroyMode.Gib);
					}

					BaseEntity chair = null;
					if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
					{
						chair?.Kill();
					}
				}

				RaycastHit hitinfo;
				if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 50))
				{
					Worker.StaticStartCoroutine(RoastCo(targetPlayer, adminPlayer, hitinfo, speed));
				}

			}
			else
			{
				BaseEntity chair = null;
				if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
				{
					if (chair != null)
					{
						chair.Kill();
					}
				}

				HashSet<BaseEntity> roasted = null;
				if (roastEntities.TryGetValue(targetPlayer.userID, out roasted))
				{
					foreach (var r in roasted)
					{
						r?.Kill();
					}
					roastEntities.Remove(targetPlayer.userID);
					roasted.Clear();
				}
			}
		}

		IEnumerator RoastCo(BasePlayer targetPlayer, BasePlayer adminPlayer, RaycastHit hitinfo, float speed)
		{

			HashSet<BaseEntity> roasted = null;
			if (!roastEntities.TryGetValue(targetPlayer.userID, out roasted))
			{
				roasted = new HashSet<BaseEntity>();
				roastEntities[targetPlayer.userID] = roasted;
			}
			Vector3 lookAtPosition = adminPlayer.transform.position;

			targetPlayer.Teleport(hitinfo.point);

			var chair = InvisibleSit(targetPlayer);
			sitChairMap[targetPlayer.userID] = chair;
			targetPlayer.EndSleeping();

			GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
			GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

			lookAtPosition.y = chair.transform.position.y;

			chair.transform.LookAt(lookAtPosition);

			//| Stick em with the spear
			var spearItem = ItemManager.CreateByName("spear.stone");
			var droppedSpear = spearItem.Drop(targetPlayer.transform.position + Vector3.down * 0.25f, Vector3.zero) as DroppedItem;
			droppedSpear.transform.position += chair.transform.forward * -0.05f;
			droppedSpear.GetComponent<Rigidbody>().isKinematic = true;
			droppedSpear.GetComponent<Rigidbody>().useGravity = false;
			droppedSpear.GetComponent<Rigidbody>().velocity = Vector3.zero;
			droppedSpear.allowPickup = false;
			SetDespawnDuration(droppedSpear, 1000000);
			roasted.Add(droppedSpear);
			droppedSpear.transform.Rotate(droppedSpear.transform.right, 90);
			if (droppedSpear.GetComponent<Collider>())
			{
				var c = droppedSpear.GetComponent<Collider>();
				c.enabled = false;
			}
			//droppedSpear.transform.LookAt(droppedSpear.transform.position + Vector3.down);



			Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
			var droppedMuzzle = muzzle.Drop(targetPlayer.transform.position + Vector3.up * 0.75f, Vector2.zero);
			DroppedItem droppedItem = droppedMuzzle as DroppedItem;

			droppedItem.allowPickup = false;
			droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			droppedItem.GetComponent<Rigidbody>().isKinematic = true;
			droppedItem.GetComponent<Rigidbody>().useGravity = false;
			droppedItem.GetComponent<Rigidbody>().mass = 0;
			if (droppedItem != null)
			{
				SetDespawnDuration(droppedItem, float.MaxValue);
			}
			roasted.Add(droppedMuzzle);

			//droppedMuzzle.transform.LookAt(lookAtPosition);

			//droppedSpear.SetParent(chair, true, true);
			chair.SetParent(droppedMuzzle, true, true);
			droppedSpear.SetParent(droppedMuzzle, true, true);

			droppedMuzzle.transform.Rotate(droppedMuzzle.transform.forward, 90);

			//| come up off the ground a bit
			droppedMuzzle.transform.position += Vector3.up * 0.3f;


			//| spawn the campfire.
			var campfire = GameManager.server.CreateEntity("assets/prefabs/deployable/campfire/campfire.prefab", hitinfo.point);
			campfire.Spawn();
			roasted.Add(campfire);
			//campfire.GetComponent<StabilityEntity>().grounded = true;
			//var campfireContainer = campfire as StorageContainer;
			campfire.SetFlag(BaseEntity.Flags.Locked, true, true, true);
			var oven = campfire as BaseOven;
			oven.StartCooking();

			yield return null;


			//| extra bits
			float width = 1.8f;
			var spearItem2 = ItemManager.CreateByName("spear.stone");
			var droppedSpear2 = spearItem2.Drop(droppedSpear.transform.position + Vector3.down * 1.8f + droppedSpear.transform.forward * -0.45f, Vector3.zero) as DroppedItem;
			droppedSpear2.GetComponent<Rigidbody>().isKinematic = true;
			droppedSpear2.GetComponent<Rigidbody>().useGravity = false;
			droppedSpear2.GetComponent<Rigidbody>().velocity = Vector3.zero;
			droppedSpear2.allowPickup = false;
			SetDespawnDuration(droppedSpear2, 1000000);
			roasted.Add(droppedSpear2);
			droppedSpear2.transform.LookAt(droppedSpear2.transform.position + Vector3.down);

            yield return null;


            var spearItem3 = ItemManager.CreateByName("spear.stone");
			var droppedSpear3 = spearItem3.Drop(droppedSpear.transform.position + Vector3.down * 1.8f + droppedSpear.transform.forward * -1 * 1.4f, Vector3.zero) as DroppedItem;
			droppedSpear3.GetComponent<Rigidbody>().isKinematic = true;
			droppedSpear3.GetComponent<Rigidbody>().useGravity = false;
			droppedSpear3.GetComponent<Rigidbody>().velocity = Vector3.zero;
			droppedSpear3.allowPickup = false;
			SetDespawnDuration(droppedSpear3, 1000000);
			roasted.Add(droppedSpear3);
			droppedSpear3.transform.LookAt(droppedSpear3.transform.position + Vector3.down);

			droppedSpear3.transform.position += Vector3.up;




			droppedSpear2.transform.position += Vector3.up;

			var target = droppedMuzzle;
			float roastSpeed = 1.5f;

			// Experimentally determined max speed
			speed = Mathf.Min(speed, 55);
			
			float targetRoastSpeed = roastSpeed * speed;
			
			float startTime = Time.realtimeSinceStartup;
			float rampUpTime = 10f;


            droppedSpear.transform.position += droppedSpear.transform.forward * -0.5f;

			droppedSpear2.transform.position += Vector3.down;
			droppedSpear3.transform.position += Vector3.down;
            //droppedSpear3.transform.position += Vector3.down;

            droppedSpear.SetParent(null);


			droppedSpear.Kill();
			//droppedSpear.transform.SetPositionAndRotation(droppedMuzzle.transform.position + Vector3.up * 6, droppedMuzzle.transform.rotation * Quaternion.Euler(0, 0, 0));


			var rot = droppedMuzzle.transform.rotation * Quaternion.Euler(90,0,0);

            var spearItem4 = ItemManager.CreateByName("spear.stone");
            var droppedSpear4 = spearItem4.Drop(campfire.transform.position + Vector3.up + droppedMuzzle.transform.up * -1f, Vector3.zero, rot ) as DroppedItem;
            droppedSpear4.transform.position += chair.transform.forward * -0.05f;
            droppedSpear4.GetComponent<Rigidbody>().isKinematic = true;
            droppedSpear4.GetComponent<Rigidbody>().useGravity = false;
            droppedSpear4.GetComponent<Rigidbody>().velocity = Vector3.zero;
            droppedSpear4.allowPickup = false;
            SetDespawnDuration(droppedSpear4, 1000000);
            roasted.Add(droppedSpear4);

			//droppedSpear4.transform.rotation = ;

			////droppedSpear4.transform.Rotate(Vector3.up, 45);
			//droppedSpear4.transform.Rotate(droppedMuzzle.transform.up, 45);
			//droppedSpear4.transform.Rotate(Vector3.up, 90);
			//droppedSpear4.transform.position = droppedSpear4.transform.forward * -1.0f;


			while (target != null && targetPlayer != null && HasCard(targetPlayer.userID, Card.Spitroast))
			{


				float anglePerFrame = Mathf.Lerp(roastSpeed, targetRoastSpeed, (Time.realtimeSinceStartup - startTime) / rampUpTime);

				target.transform.Rotate(target.transform.up, anglePerFrame, Space.World);

				oven.StartCooking();


				target.SendNetworkUpdate();





				droppedSpear2.SendNetworkUpdate();
				droppedSpear3.SendNetworkUpdate();
				droppedSpear4.SendNetworkUpdate();
                droppedMuzzle.SendNetworkUpdate();

				yield return new WaitForFixedUpdate();
			}

			droppedSpear?.Kill();
			chair?.Kill();
			droppedSpear2?.Kill();
			droppedSpear3?.Kill();
			droppedMuzzle?.Kill();
            droppedSpear3?.Kill();
			campfire?.Kill();

			if (targetPlayer != null)
			{
				TakeCard(targetPlayer.userID, Card.Spitroast);
			}

		}

		#endregion

		#region Interrogate


		string interrogate_open_url = "http://na.fragmod.com/fragimages/dec2023/payback_hood_open.png";
		string interrogate_closed_url = "http://na.fragmod.com/fragimages/dec2023/payback_hood_closed.png";
		string guid_interrogate = "guid_interrogate";
		Dictionary<ulong, bool> interrogationState = new Dictionary<ulong, bool>();
		Dictionary<ulong, Item> interrogationMasks = new Dictionary<ulong, Item>();
		Dictionary<ulong, HashSet<ulong>> interrogationSpectators = new Dictionary<ulong, HashSet<ulong>>();
		void DoInterrogate(BasePlayer player, BasePlayer admin = null, string[] args = null, bool removeCard = false, bool open = false) {

			//| ===================================
			//| BASE UI SETUP
			//| ===================================
			if (player.net.connection == null) return;


			//| don't update image if we're in the correct state.
			bool existingState = GetInterrogationState(player.userID);
			if (open && existingState == open && !removeCard) return;

			interrogationState[player.userID] = open;

			if (!open)
			{
				bool existingMask = false;
				foreach (var item in player.inventory.containerWear.itemList.ToArray())
				{
					if (item.info.itemid == ItemManager.FindItemDefinition("mask.balaclava").itemid && item.skin == 10139)
					{
						existingMask = true;
						interrogationMasks[player.userID] = item;
						break;
					}
					var x = item.info.GetComponent<global::ItemModWearable>();
					bool headGear = x.ProtectsArea(HitArea.Head);
					if (headGear)
					{
						bool success = item.MoveToContainer(player.inventory.containerMain);
						if (!success)
						{
							item.Drop(player.transform.position + Vector3.up, Vector3.up);
						}
					}

				}

				if (!existingMask)
				{
					var mask = ItemManager.CreateByName("mask.balaclava", 1, 10139);
					GiveItemOrDrop(player, mask, false);
					interrogationMasks[player.userID] = mask;
				}

			} else {

				if (interrogationMasks.ContainsKey(player.userID))
				{
					var mask = interrogationMasks[player.userID];
					if (mask != null)
					{
						mask.RemoveFromContainer();
						mask.Remove();
						interrogationMasks.Remove(player.userID);
					}
				}
			}
			

			if (removeCard)
			{
				Unsubscribe("OnPlayerVoice");
				interrogationState.Remove(player.userID);

				if (interrogationMasks.ContainsKey(player.userID))
				{
					var mask = interrogationMasks[player.userID];
					if (mask != null)
					{
						mask.RemoveFromContainer();
						mask.Remove();
						interrogationMasks.Remove(player.userID);
					}
				}

			} else
			{
				Subscribe("OnPlayerVoice");
			}

			UpdateInterrogateUI(player, open, removeCard);

			var spectators = player.GetComponentsInChildren<BasePlayer>();
			foreach (var spectator in spectators)
			{
				if (spectator != player)
				{
					UpdateInterrogateUI(spectator, open, removeCard);
				}
			}
		}
		bool GetInterrogationState(ulong userid)
		{
			bool existingState;
			interrogationState.TryGetValue(userid, out existingState);
			return existingState;
		}
		void UpdateInterrogateUI(BasePlayer player, bool open, bool remove = false)
		{
            string guid = guid_interrogate;
			UI2.guids.Add(guid);

			var elements = new CuiElementContainer();
			CuiHelper.DestroyUi(player, guid);

			if (remove) return;

			//| ===================================
			//| Bounds definitions
			//| ===================================
			float fade = 0.2f;

			Vector4 mainBounds = UI2.vectorFullscreen;
			UI2.CreatePanel(elements, "Overlay", guid, "1 1 1 0", mainBounds, null, false, 0, 0);

			//| ===================================
			//| Static elements
			//| ===================================
			string url = interrogate_closed_url;
			if (open)
			{
				url = interrogate_open_url;
			}
			if (ImageLibrary == null)
			{
				UI2.CreatePanel(elements, guid, "bg", "1 1 1 1", UI2.vectorFullscreen, url, false, fade, fade, false, false);
			}
			else
			{
				UI2.CreatePanel(elements, guid, "bg", "1 1 1 1", UI2.vectorFullscreen, GetImage(url), false, fade, fade, true, false);
			}

			//send the ui updates
			if (elements.Count > 0)
			{
				CuiHelper.AddUi(player, elements);
			}
		}

		object OnPlayerSpectate(BasePlayer spectator, string targetDisplayName) {

			//Puts($"OnPlayerSpectate | {spectator.displayName} -> {targetDisplayName}");

			var player = FindPlayer(targetDisplayName);
			if (player != null)
			{

				//Puts($"OnPlayerSpectate 2 | {spectator.displayName} -> {targetDisplayName}");

				BasePlayer targetplayer = player.Object as BasePlayer;
				if (HasCard(targetplayer.userID, Card.Interrogate))
				{
					//Puts($"OnPlayerSpectate 3 | {spectator.displayName} -> {targetDisplayName}");

					bool existingState = GetInterrogationState(targetplayer.userID);
					UpdateInterrogateUI(spectator, existingState, false);
				}
			}
			return null; 
		}
		//object CanSpectateTarget(BasePlayer spectator, string targetDisplayName) {
		//    return null;   
		//}      
		object OnPlayerSpectateEnd(BasePlayer spectator, string targetDisplayName) {

			//Puts($"OnPlayerSpectateEnd | {targetDisplayName}");

			var player = FindPlayer(targetDisplayName);
			if (player != null)
			{
				BasePlayer targetplayer = player.Object as BasePlayer;
				if (HasCard(targetplayer.userID, Card.Interrogate))
				{
					CuiHelper.DestroyUi(spectator, guid_interrogate);
				}
			}
			return null;   
		}

		object OnPlayerRecover(BasePlayer player)
		{
			if (HasCard(player.userID, Card.Hogwild))
			{
				return false;
			}
			return null;
		}

		Dictionary<ulong, Coroutine> interrogationCooldowns = new Dictionary<ulong, Coroutine>();
		object OnPlayerVoice(BasePlayer player, Byte[] data)
		{
			
            recentPlayerVoiceTimestamps[player.userID] = Time.realtimeSinceStartup;


            if (HasCard(player.userID, Card.Interrogate))
			{
				DoInterrogate(player, null, null, false, true);

				Coroutine co = null;
				if (interrogationCooldowns.TryGetValue(player.userID, out co))
				{
					Worker.GetSingleton().StopCoroutine(co);
				}
				co = Worker.StaticStartCoroutine(InterrogationCo(player));
				interrogationCooldowns[player.userID] = co;
			}

			return null;
		}
		IEnumerator InterrogationCo(BasePlayer player)
		{
			yield return new WaitForSeconds(0.45f);

			if (HasCard(player.userID, Card.Interrogate))
			{
				DoInterrogate(player, null, null, false, false);
			}
		}
#endregion

		#region HOG

		object CanLootPlayer(BasePlayer target, BasePlayer looter)
		{
			if (HasCard(target.userID, Card.Hogwild))
			{
				return false;
			}
			return null;
		}

		Dictionary<ulong, List<BaseEntity>> cowboynetworkables = new Dictionary<ulong, List<BaseEntity>>();

		float HogChairHeight = 0.15f;
		void DoHog(BasePlayer player, BasePlayer admin = null, string[] args = null, bool removeCard = false) {

			if (removeCard)
			{
				List<BaseEntity> existing;
				if (cowboynetworkables.TryGetValue(player.userID, out existing))
				{

					if (existing != null)
					{
						existing.ForEach(x => {
							x.Kill();
						});
						existing.Clear();
					}
					cowboynetworkables.Remove(player.userID);
				}

				if (player != null)
				{
					player.StopWounded();
				}

				return;
			}
			cowboynetworkables[player.userID] = new List<BaseEntity>() { };


			//down target player
			//disabled for testing
			player.BecomeWounded(new HitInfo());
			player.ProlongWounding(100000000000);

			//|====================================================
			//| create the chair for the person to ride

			var chair = GameManager.server.CreateEntity(targetChairPrefab, player.transform.position + Vector3.up * HogChairHeight);
			var mount = chair as BaseMountable;
			chair.Spawn();

			GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
			GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

			var collider = chair.GetComponentInChildren<Collider>();
			if (collider != null)
			{
				collider.enabled = false;
			}

			cowboynetworkables[player.userID].Add(chair);


			//| muzzlebreak

			Item muzzle = ItemManager.CreateByPartialName("muzzlebrake");
			var dropped = muzzle.Drop(player.transform.position + Vector3.up * 2, Vector2.zero);
			DroppedItem droppedItem = dropped as DroppedItem;

			droppedItem.allowPickup = false;
			droppedItem.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			droppedItem.GetComponent<Rigidbody>().isKinematic = true;
			droppedItem.GetComponent<Rigidbody>().useGravity = false;
			if (droppedItem != null)
			{
				SetDespawnDuration(droppedItem, float.MaxValue);
			}

			Worker.StaticStartCoroutine(DropFollowPlayer(player, droppedItem));

			cowboynetworkables[player.userID].Add(droppedItem);


			//chair.SetParent(player, true, true);
			chair.SetParent(droppedItem, true, true);
			chair.transform.rotation = Quaternion.identity;
			chair.transform.position = droppedItem.transform.position;
			chair.SendNetworkUpdate();

			//| make it look like the seated player is above the target
			//Worker.StaticStartCoroutine(UpdateSeatedPlayerCo(chair as BaseMountable));

			//| make sure the target doesn't clip into the chair collider
			Worker.StaticStartCoroutine(AlwaysKill(chair, player));

			//| make the dropped object follow the player without parenting (networking issues)
			//Worker.StaticStartCoroutine(ContinuousTP(player, chair, droppedItem));

			//| make the chair face the players direction
			Worker.StaticStartCoroutine(ChairFacingCo(player, droppedItem));

			//| show the game tip about being ridden
			Worker.StaticStartCoroutine(HogGameTipCo(player, chair as BaseMountable));




			var boarEntity = GameManager.server.CreateEntity("assets/rust.ai/agents/boar/boar.prefab", player.transform.position);
			boarEntity.Spawn();
			cowboynetworkables[player.userID].Add(boarEntity);

			Worker.StaticStartCoroutine(HogSFX(player, boarEntity));


			//| look the part
			foreach (var item in player.inventory.containerWear.itemList.ToArray())
			{
				bool success = item.MoveToContainer(player.inventory.containerMain);
				if (!success)
				{
					item.Drop(player.transform.position + Vector3.up, Vector3.zero);
				}
			}

			//GiveItemOrDrop(player, ItemManager.CreateByName("attire.hide.skirt", 1, 793180528), false);
			GiveItemOrDrop(player, ItemManager.CreateByName("mask.balaclava", 1, 10139), false);

		}

		IEnumerator HogSFX(BasePlayer player, BaseEntity boarEntity) {



			float ts = float.NegativeInfinity;

			var boar = boarEntity as Boar;
			boar.enabled = false;
			//boar.StopAllCoroutines();
			boar.NavAgent.enabled = false;
			while (player != null && HasCard(player.userID, Card.Hogwild) && boarEntity != null && !boarEntity.IsDestroyed)
			{
				if (Time.realtimeSinceStartup - ts > 5f)
				{
					boar.gameObject.SetActive(true);
					boar.SignalBroadcast(BaseEntity.Signal.Attack);
					ts = Time.realtimeSinceStartup;
					boar.gameObject.SetActive(false);

					PlayGesture(player, "hurry");

				}

				boar.transform.position = player.transform.position + Vector3.down * 3f;
				yield return new WaitForFixedUpdate();
			}
		}

		IEnumerator HogGameTipCo(BasePlayer player, BaseMountable mount) {
			
			while (player != null && mount != null && !mount.IsDestroyed)
			{
				if (mount.GetMounted() != null)
				{
					CreateGameTip($"{mount.GetMounted().displayName} is riding you like a pig! REEE!!", player, 5, true);
					//CreateGameTip($"{mount.GetMounted().displayName} is riding you like a pig!", mount.GetMounted(), 5, true);

					yield return new WaitForSeconds(4.9f);
				}
				else
				{
					yield return new WaitForSeconds(0.1f);
				}

			}
		}
		IEnumerator UpdateSeatedPlayerCo(BaseMountable mount)
		{
			while (mount != null)
			{
				if (mount.GetMounted() != null)
				{
					//mount.GetMounted().Teleport(mount.transform.position + Vector3.up * 0.75f);
					//mount.GetMounted().Teleport(mount.transform.position + Vector3.up * 0.8f);
					mount.GetMounted().Teleport(mount.transform.position + Vector3.up * 0.95f);
					//mount.GetMounted().Teleport(mount.transform.position + Vector3.up * 5.95f);
					mount.SendNetworkUpdate();
				}
				yield return new WaitForFixedUpdate();
				//yield return new WaitForSeconds(1/60f);
			}
		}
		public void CreateGameTip(string text, BasePlayer player, float length = 30f, bool redColor = false)
		{
			if (player == null)
				return;

			if (redColor)
			{
				player.SendConsoleCommand($"gametip.showtoast {1} \"{text}\"  ");
			}
			else
			{
				player.SendConsoleCommand("gametip.hidegametip");
				player.SendConsoleCommand("gametip.showgametip", text + "  ");
				timer.Once(length, () =>
				{
					if (player != null)
					{
						player.SendConsoleCommand("gametip.hidegametip");
					}
				}
				);
			}
		}

		IEnumerator DropFollowPlayer(BasePlayer player, DroppedItem item)
		{
			while (player != null && item != null && !item.IsDestroyed)
			{
				//item.transform.position = player.transform.position + Vector3.down * 0.1f;
				item.transform.position = player.transform.position + Vector3.up * HogChairHeight;
				item.SendNetworkUpdateImmediate();
				yield return new WaitForFixedUpdate();
			}
		}

		IEnumerator ChairFacingCo(BasePlayer player, BaseEntity chair)
		{
			while (player != null && chair != null && !chair.IsDestroyed)
			{

				Vector3 facing = player.eyes.HeadForward();
				facing.y = 0;
				facing.Normalize();

				chair.transform.LookAt(chair.transform.position + facing);
				//chair.transform.Rotate(Vector3.up, 72);
				chair.transform.Rotate(Vector3.up, 1);
				chair.SendNetworkUpdateImmediate();

				yield return new WaitForFixedUpdate();
			}
		}

		IEnumerator ContinuousTP(BasePlayer player, BaseEntity entity, DroppedItem droppedItem)
		{
			while (player != null && entity != null) {


				if (player.isMounted) {
                    player.Teleport(entity.transform.position + Vector3.up * 1f);
                    player.SendNetworkUpdateImmediate();
                }

				
				yield return new WaitForSeconds(0.25f);
			}
		}

		IEnumerator AlwaysKill(BaseEntity entity, BasePlayer player)
		{
			List<Network.Connection> cons = new List<Network.Connection>(1);

			while (entity != null && player != null && player.net != null && player.net.connection != null)
			{
				cons.Clear();
				cons.Add(player.net.connection);

				var netwrite = Net.sv.StartWrite();


                netwrite.PacketID(Message.Type.EntityDestroy);
                netwrite.EntityID(entity.net.ID);
                netwrite.UInt8((byte)BaseNetworkable.DestroyMode.None);
                netwrite.Send(new SendInfo(cons));
				

				yield return new WaitForSeconds(0.1f);
			}
		}
		#endregion

		#region POTATO

		string guid_potato = "guid_potato";
		Dictionary<ulong, int> currentFrameMap = new Dictionary<ulong, int>();
		Dictionary<ulong, Coroutine> potatoCoRoutines = new Dictionary<ulong, Coroutine>();
		void DoPotato(BasePlayer player, BasePlayer admin = null, string[] args = null, bool doRemove = false)
		{
			Coroutine routine;
			potatoCoRoutines.TryGetValue(player.userID, out routine);


			if (doRemove)
			{
				if (routine != null)
				{
					Worker.GetSingleton().StopCoroutine(routine);
					return;
				}
				//if (args.Contains("add")) { 
				//    //| contiue to add to the lag
				//} else
				//{
				//    int existing = 0;
				//    currentFrameMap.TryGetValue(player.userID, out existing);
				//    for (int i = 0; i <= existing; i ++)
				//    {
				//        CuiHelper.DestroyUi(player, guid_BSOD + i);
				//    }

				//    currentFrameMap.Remove(player.userID);
				//    CuiHelper.DestroyUi(player, guid_BSOD);
				//    return;
				//}
			}

			//if (!currentFrameMap.ContainsKey(player.userID))
			//{
			//    UI2.guids.Add(guid_BSOD);
			//    var elements = new CuiElementContainer();
			//    UI2.CreatePanel(elements, "Under", guid_BSOD, "1 1 1 0", UI2.vectorFullscreen, null, false, 0, 0, false);
			//    CuiHelper.AddUi(player, elements);
			//}


			UI2.guids.Add(guid_potato);
			var elements = new CuiElementContainer();
			UI2.CreatePanel(elements, "Under", guid_potato, "1 1 1 0", UI2.vectorFullscreen, null, false, 0, 0, false);
			CuiHelper.AddUi(player, elements);

			routine = Worker.StaticStartCoroutine(DoPotatoCo(player, args));
			potatoCoRoutines[player.userID] = routine;
		}
		IEnumerator DoPotatoCo(BasePlayer player, string[] args)
		{


			int batch = 10;

			//int totalFrames = 600;
			int currentFrames = 0;

			float rate = 50 / 1f;

			//if (args.Contains("crash"))
			//{
			//    intensity = 5;
			//    totalFrames = 5000;
			//}

			float multiplier = 1;
			if (args != null && args.Length > 1)
			{
				float.TryParse(args[1], out multiplier);
				multiplier = Mathf.Min(multiplier, 100);
			}

			batch *= (int)multiplier;
			rate *= multiplier;

			float ts = Time.realtimeSinceStartup;
			float startTime = ts;
			float elapsed;

			float currentRate;


			var bounds = new Vector4(0, 0, 0.01f, 0.01f);

            while (player != null && player.net != null &&  player.net.connection != null && player.net.connection.connected && HasCard(player.userID, Card.PotatoMode))
			{

				elapsed = Time.realtimeSinceStartup - startTime + Mathf.Epsilon;
				currentRate = currentFrames / elapsed;

				if (currentRate > rate)
				{
					//Puts($"Paused: Current Frames: {currentFrames} rate {currentRate}");
					yield return null;
				} else
				{
					for (int j = 0; j < batch; j++)
					{

                        UI2.guids.Add(guid_potato + currentFrames);
                        var elements = new CuiElementContainer();
                        UI2.CreatePanel(elements, guid_potato, guid_potato + currentFrames, "1 1 1 0.25", bounds, "http://na.fragmod.com/fragimages/dec2023/payback_logo_broad.png", false, 0, 0, false);
                        CuiHelper.AddUi(player, elements);
						currentFrames++;

					}
				}

				//Puts($"Current Frames: {currentFrames} rate {currentRate}");

				//| Ensure we take a break at least every 1/30s
				if (Time.realtimeSinceStartup - ts > 1/30f)
				{
					yield return null;
                    ts = Time.realtimeSinceStartup;
                }

            }
		}



		Dictionary<ulong, float> woodsTimestamps = new Dictionary<ulong, float>();
		HashSet<ulong> woodsHasLandmines = new HashSet<ulong>();
		void DoWoods(BasePlayer target)
		{
			float ts = 0;
			woodsTimestamps.TryGetValue(target.userID, out ts);
			if (Time.realtimeSinceStartup - ts < 15)
			{
				return;
			}
			woodsTimestamps[target.userID] = Time.realtimeSinceStartup;
			//TakeCard(target, Card.Woods);

			int layermask = 1 << 15 | 1 << 16 | 1 << 17 | 1 << 23 | 1 << 27 | 1 << 8 | 1 << 21 | 1 << 12 | 1 << 0 | 1 << 30;

			bool foundSpot = false;
			int iterations = 0;
			while (!foundSpot && iterations < 100)
			{
				var pos = target.transform.position + target.eyes.HeadRay().direction * -1 * 20;
				pos += Vector3.up * 100;
				var ray = new Ray(pos, Vector3.down);
				RaycastHit hit;
				if (Physics.Raycast(ray, out hit, 1000, layermask))
				{
					//var hits = Physics.SphereCastAll(hit.point, 1, Vector3.up);
					var collliders = Physics.OverlapSphere(hit.point, 1);
					bool tooClose = false;
					if (collliders != null)
					{
						foreach (var c in collliders)
						{

							var ent = c.gameObject.GetComponent<BaseEntity>();
							if (ent != null)
							{
								tooClose = true;
							}
						}
					}

					if (!tooClose)
					{
						foundSpot = true;
						if (woodsHasLandmines.Contains(target.userID))
						{
							Worker.StaticStartCoroutine(AnimalAttackCo(target, hit.point + Vector3.up, new string[] { "bear", "landmine" }));
						}
						else
						{
							Worker.StaticStartCoroutine(AnimalAttackCo(target, hit.point + Vector3.up, new string[] { "bear"}));
						}
					}
				}
				iterations++;
			}
		}

		HashSet<BaseEntity> animals = new HashSet<BaseEntity>();
		HashSet<BaseEntity> entitiesThatDealNoDamage = new HashSet<BaseEntity>();
		HashSet<BaseEntity> invulnerableEntities = new HashSet<BaseEntity>();

		IEnumerator AnimalAttackCo(BasePlayer player, Vector3 spawnposition, string[] args)
		{
			//Ray ray = new Ray(UnityEngine.Random.Range(-5f, 5f) * Vector3.forward + UnityEngine.Random.Range(-5f, 5f) * Vector3.left + spawnposition + Vector3.up * 20, Vector3.down);
			//Ray ray = new Ray(UnityEngine.Random.Range(-5f, 5f) * Vector3.forward + UnityEngine.Random.Range(-5f, 5f) * Vector3.left + spawnposition + Vector3.up * 20, Vector3.down);
			//if (Physics.Raycast(ray, out hit))
			//{
			string aiPrefab = "assets/rust.ai/agents/chicken/chicken.prefab";
			if (args.Contains("bear"))
			{
				aiPrefab = "assets/rust.ai/agents/bear/bear.prefab";
			}
			else if (args.Contains("boar"))
			{
				aiPrefab = "assets/rust.ai/agents/boar/boar.prefab";

			}
			else if (args.Contains("wolf"))
			{
				aiPrefab = "assets/rust.ai/agents/wolf/wolf.prefab";

			}
			else if (args.Contains("stag"))
			{
				aiPrefab = "assets/rust.ai/agents/stag/stag.prefab";
			}
			//assets/rust.ai/agents/wolf/wolf.prefab
			var entity = GameManager.server.CreateEntity(aiPrefab, spawnposition + Vector3.up * 0.2f);
			//var entity = GameManager.server.CreateEntity("assets/rust.ai/agents/wolf/wolf.prefab", hit.point + Vector3.up * 0.2f);
			//var entity = GameManager.server.CreateEntity("assets/rust.ai/agents/bear/bear.prefab", hit.point + Vector3.up * 0.2f);

			BaseAnimalNPC npc = entity as BaseAnimalNPC;
			entity.Spawn();

			var stats = npc.Stats;

			npc.AttackRange = 3;
			HashSet<BaseEntity> forceNetworkUpdates = new HashSet<BaseEntity>();

			if (args.Contains("landmine"))
			{
				for (int i = 0; i < 5; i ++)
				{
					var mine = GameManager.server.CreateEntity("assets/prefabs/deployable/landmine/landmine.prefab", npc.transform.position + Vector3.up * 0.6f + npc.transform.forward * (1.3f - 0.1f * i));
					Landmine landmine = mine as Landmine;
					mine.Spawn();
					landmine.Arm();
					landmine.SendNetworkUpdateImmediate();
					mine.transform.LookAt(npc.transform.position + npc.transform.up * 100);
					mine.SetParent(entity, true);
					entitiesThatDealNoDamage.Add(mine);
					forceNetworkUpdates.Add(mine);
				}
				npc.AttackRange = 0.01f;
				stats.Speed *= 2.4f;
				npc.TargetSpeed *= 2.4f;
			}

			//chicken.Stats.Speed = 20;
			//npc.Stats.Speed = 200;
			stats.TurnSpeed = 100;
			//npc.Stats.Acceleration = 50;
			//npc.AttackDamage *= 2;
			stats.VisionRange = 300;

			animals.Add(npc);

			npc.AttackTarget = player;
			npc.ChaseTransform = player.transform;


			stats.AggressionRange = 100000;
			stats.DeaggroRange = 100000;
			stats.IsAfraidOf = new BaseNpc.AiStatistics.FamilyEnum[0];
			npc.Destination = player.transform.position;

			stats.VisionCone = -1;

			npc.Stats = stats;

			yield return new WaitForSeconds(0.25f);
			//chicken.LegacyNavigation = true;
			//chicken.Stats.DistanceVisibility = AnimationCurve.Linear(0, 0, 1, 1);
			npc.LegacyNavigation = true;

			bool doLoop = true;
			while (doLoop)
			{

				foreach (var ent in forceNetworkUpdates)
				{
					if (ent != null)
					{
						if (ent.net.group.ID != npc.net.group.ID)
						{
							ent.net.SwitchGroup(npc.net.group);
							ent.SendNetworkGroupChange();
						}
					}
				}

				if (npc != null && player != null)
				{
					if (player.IsDead())
					{
						if (npc != null)
						{
							npc.Kill();
						}
						doLoop = false;
					}
					else
					{
						if (npc.NavAgent != null && npc.NavAgent.isOnNavMesh)
						{
							npc.ChaseTransform = player.transform;
							npc.AttackTarget = player;
							npc.Destination = player.transform.position;
							npc.TargetSpeed = npc.Stats.Speed;

						}
					}
					//Puts($"Attack target: {chicken.AttackTarget} Chase: {chicken.ChaseTransform} ARate: {chicken.AttackRate} CombatTarget: {chicken.CombatTarget}");
					//chicken.TickNavigation();

				}
				else
				{
					if (npc != null)
					{
						npc.Kill();
					}
					doLoop = false;
				}
				yield return null;
				//yield return new WaitForSeconds(0.25f);
			}


			timer.Once(120, () => {
				if (npc != null)
				{
					npc.Kill();
				}
			});

			timer.Once(130f, () => {
				animals.RemoveWhere(x => x == null);
			});
			//}
		}
		#endregion

		#region OxideHooks
		//| ==============================================================
		//| OXIDE HOOKS
		//| ==============================================================
		private object OnPlayerViolation(BasePlayer player, AntiHackType type)
		{
			if (type == AntiHackType.InsideTerrain && HasAnyCard(player.userID)) return false;
			if (bonkedPlayers.Contains(player.userID)) return false;
			if (recentlyBonkedPlayers.Contains(player.userID)) return false;
			return null;
		}

		object OnPlayerDeath(BasePlayer player, HitInfo hitinfo)
		{

			if (HasAnyCard(player.userID))
			{

				if (player.isMounted)
				{
					player.GetMounted().DismountPlayer(player, true);
					//player.DismountObject();//for some reason this was required
				}

				if (HasCard(player.userID, Card.NoRest) || HasCard(player.userID, Card.Bonk))
				{
					timer.Once(3f, () => {
						if (player != null)
						{
							if (player.IsDead())
							{
								player.Respawn();
							}
						}
					});
				}
				if (HasCard(player.userID, Card.Hogwild))
				{
					TakeCard(player, Card.Hogwild, null, null);
				}
			}

			return null;
		}




		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player != null && HasCard(player.userID, Card.Sit))
			{
				TakeCard(player, Card.Sit);
			}
			if (player != null && HasCard(player.userID, Card.Crucify))
			{
				TakeCard(player, Card.Crucify);
			}
			if (player != null && HasCard(player.userID, Card.Hogwild))
			{
				TakeCard(player, Card.Hogwild);
			}
			if (player != null && HasCard(player.userID, Card.Interrogate))
			{
				TakeCard(player, Card.Hogwild);
			}

			//if the player have the card spitroast, take the card
			if (player != null && HasCard(player.userID, Card.Spitroast))
			{
				TakeCard(player, Card.Spitroast);
			}

            if (player != null && HasCard(player.userID, Card.Bonk))
            {
                TakeCard(player, Card.Bonk);
            }

            activePaybackUIS.Remove(player.userID);

        }
        void OnPlayerBanned(Network.Connection connection, string reason)
		{
			if (connection != null)
			{
				var player = connection.player as BasePlayer;
				if (player != null)
				{
					OnPlayerBanned(player.displayName, player.userID, connection.ipaddress, reason);
				}
			}
		}
		void OnPlayerBanned(string name, ulong id, string address, string reason)
		{
			//force the banned player dead and out of any chairs, else the model seems to stay behind
			var player = BasePlayer.FindByID(id);
			if (player != null)
			{
				if (sitChairMap.ContainsKey(id))
				{
					player.GetMounted().DismountPlayer(player, true);
					player.Die();
				}
			}
		}
		void OnPlayerKicked(BasePlayer player, string reason)
		{
			if (player == null) return;

			if (sitChairMap == null) return;
			//force the banned player dead and out of any chairs, else the model seems to stay behind
			if (sitChairMap.ContainsKey(player.userID))
			{
				if (player.GetMounted() == null) return;
				player.GetMounted().DismountPlayer(player, true);
				player.Die();
			}
		}

		private void OnEntityTakeDamage(BaseEntity entity, HitInfo hitinfo)
		{




            if (entity == null || hitinfo == null) return;
			if (cardMap.Count == 0 && airstrikeRockets.Count == 0) return;//early out for maximum perf

			//PrintToChat($"OnEntityTakeDamage: {entity} : dmg: {hitinfo.damageTypes.GetMajorityDamageType()} initiator: {hitinfo.Initiator} initPlayer: {hitinfo.InitiatorPlayer} dmg: {hitinfo.damageTypes.Total()}");
			//Puts($"{Environment.StackTrace}");

			if (hitinfo != null)
			{
				
                if (invulnerableEntities.Contains(entity))
				{

					if (craterMap.ContainsKey(entity))
					{
                        var p = BasePlayer.FindByID(craterMap[entity]);

						if (p == null) return;

                        var attackingPrefab = hitinfo.WeaponPrefab?.prefabID;

                        if (attackingPrefab == 1744180387)
                        {

                            hitinfo.damageTypes.Clear();
                            hitinfo.DoHitEffects = false;

                            ToggleBonk(p, hitinfo.InitiatorPlayer);
                        }

                    }

                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                    return;
				}


				


                if (sitChairMap.Values.Contains(entity))
				{
					hitinfo.damageTypes.Clear();
					hitinfo.DoHitEffects = false;
				}

				var player = entity as BasePlayer;
				var attacker = hitinfo.InitiatorPlayer;

				if (hitinfo.Initiator != null && entitiesThatDealNoDamage.Contains(hitinfo.Initiator))
				{
					if (player != null && HasAnyCard(player.userID) || entity is BaseNpc) {
						//| damage ok
					} else
					{
						//| no damage
						hitinfo.damageTypes.Clear();
						hitinfo.DoHitEffects = false;
					}
				}

				//| prevent suicide damage from terrain violation
				if (player != null && bonkedPlayers.Contains(player.userID) && hitinfo.damageTypes.GetMajorityDamageType() == DamageType.Suicide)
				{
                    hitinfo.damageTypes.Clear();
                    hitinfo.DoHitEffects = false;
                }


				// airstrike?
				if (!airstrikeIsUnsafe && airstrikeRockets.Contains(hitinfo.Initiator))
				{
					if (player != null)
					{
						if (!airstrikeTargetPlayers.Contains(player.userID))
						{
							hitinfo.damageTypes.Clear();
							hitinfo.DoHitEffects = false;
						}
					}
					else
					{
                        hitinfo.damageTypes.Clear();
                        hitinfo.DoHitEffects = false;
                    }
                }

                if (player != null && !IsNPC(player) && hitinfo.InitiatorPlayer != null
					&& (HasCard(player.userID, Card.Bonk) || bonkingAdmins.Contains(hitinfo.InitiatorPlayer.userID)))
				{

                    var attackingPrefab = hitinfo.WeaponPrefab?.prefabID;

					if (attackingPrefab == 1744180387) {

                        hitinfo.damageTypes.Clear();
                        hitinfo.DoHitEffects = false;

						ToggleBonk(player, hitinfo.InitiatorPlayer);

					}
                }

				if (player != null && hitinfo.InitiatorPlayer != null 
					&& HasCard(hitinfo.InitiatorPlayer.userID, Card.Batman) 
					&& entity != hitinfo.InitiatorPlayer
					&& hitinfo.Weapon?.GetItem()?.info?.itemid == batitemid) 
				{

                    hitinfo.damageTypes.Clear();
                    var dir = hitinfo.InitiatorPlayer.eyes.HeadRay().direction;
                    if (dir.y < 0.2f)
                    {
                        dir = new Vector3(dir.x, dir.y * -1f, dir.z);
                    }
                    dir += Vector3.up * 0.25f;
                    dir.Normalize();


					bool lethal = false;
                    float power = 15;
                    if (hitinfo.boneArea == HitArea.Head || batAlwaysKills)
                    {
						lethal = true;
                    }

					if (hitinfo.InitiatorPlayer.serverInput.IsDown(BUTTON.SPRINT))
					{
						power *= 3;
					}

					hitinfo.Weapon.GetItem().conditionNormalized = 1;
					hitinfo.Weapon.GetItem().MarkDirty();

                    ApplyRagdoll(entity as BasePlayer, dir, power, true, null, lethal);

                }

				

                if (attacker != null && HasAnyCard(attacker.userID))
				{
					var members = GetPlayerTeam(attacker.userID);
					members.Remove(attacker.userID);

					bool friendlyFire = false;
					if (player != null)
					{
						friendlyFire = members.Contains(player.userID);
					}

					bool isSuicide = hitinfo.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide;


					if (player != null && attacker != null && attacker != player)
					{

						if (HasCard(attacker.userID, Card.InstantKarma))
						{

							if (!friendlyFire)
							{

								float newHealth = attacker.health - hitinfo.damageTypes.Total() * 0.35f;
								if (newHealth < 5)
								{
									attacker.Die();
								}
								else
								{
									attacker.SetHealth(newHealth);
									attacker.metabolism.SendChangesToClient();
									attacker.SendNetworkUpdateImmediate();
									//PlaySound("assets/bundled/prefabs/fx/headshot.prefab", attacker, false);
									PlaySound("assets/bundled/prefabs/fx/headshot_2d.prefab", attacker, true);
								}

								hitinfo.damageTypes.Clear();
								hitinfo.DoHitEffects = false;

							}

						}

					}

					if (HasCard(attacker.userID, Card.Pacifism) && attacker != player && player != null)
					{

						if (!friendlyFire)
						{
							hitinfo.damageTypes.Clear();
							hitinfo.DoHitEffects = false;

							if (config.notifyCheaterAttacking && !silentPacifism)
							{
								SendPlayerLimitedMessage(player.userID, $"You are being attacked by [{UI2.ColorText(attacker.displayName, "yellow")}] a known cheater!\n{UI2.ColorText("Tommygun's Payback Plugin", "#7A2E30")} has prevented all damage to you.");
							}
							//Puts($"{player.displayName} attacked by [{attacker.displayName}] a known cheater! Tommygun's Payback has prevented all damage from the cheater");

						}

					}

					//
					if (attacker != null && HasCard(attacker.userID, Card.Woods))
					{
						if (hitinfo.CanGather)
						{
							DoWoods(attacker);
						}
					}

					//prevent damage to non-player entities
					if (HasCard(attacker.userID, Card.Dud) && player == null)
					{
						hitinfo.damageTypes.Clear();
						//hitinfo.DoHitEffects = false;
						hitinfo.gatherScale = 0;
					}


				}



			}
		}


		#endregion

		#region PaybackIO
		//| ==============================================================
		//| INPUT OUTPUT FUNCTIONALITY
		//| ==============================================================

		Dictionary<ulong, float> playerMessageTimestamps = new Dictionary<ulong, float>();
		void SendPlayerLimitedMessage(ulong userID, string message, float rate = 5)
		{
			float ts = float.NegativeInfinity;
			if (playerMessageTimestamps.TryGetValue(userID, out ts))
			{
				if (Time.realtimeSinceStartup - ts > rate)
				{
					ts = Time.realtimeSinceStartup;
					playerMessageTimestamps[userID] = ts;
					SendReply(BasePlayer.FindByID(userID), message);
				}
			}
			else
			{
				playerMessageTimestamps[userID] = ts;
				SendReply(BasePlayer.FindByID(userID), message);
			}
		}


		void AdminCommandToggleCard(BasePlayer admin, Card card, string[] args)
		{

			//| Special Commands
			if (card == Card.Bag)
			{
				ulong userID;
				if (!ulong.TryParse(args[0], out userID))
				{
					PrintToPlayer(admin, "usage: /bag <steamid>");
					return;
				}
				DoBagSearch(userID, args, admin);
			}

			//| Requires target commands
			if ( (args.Length == 0 && admin != null) || (args.Length == 1 && args[0].ToLower() == "true"))
			{

				var entity = RaycastFirstEntity(admin.eyes.HeadRay(), 100);
				if (entity is BasePlayer)
				{
					var targetPlayer = entity as BasePlayer;
					AdminToggleCard(admin, targetPlayer, card, args);
				}
				else
				{
					//raycast target in front of you
					//SendReply(admin, "did not find player from head raycast, either look at your target or do /<cardname> <playername>");
					PrintToPlayer(admin, "did not find player from head raycast, either look at your target or do /<cardname> <playername>");
				}

				return;
			}

			if (args.Length >= 1)
			{
				var targetPlayer = GetPlayerWithName(args[0]);
				if (targetPlayer != null)
				{

					if (args.Length == 2 && args[1] == "team")
					{

						var members = GetPlayerTeam(targetPlayer.userID);

						string teamMatesPrintout = "";
						foreach (var member in members)
						{
							BasePlayer p = BasePlayer.FindByID(member);
							if (p != null && p.IsConnected)
							{
								teamMatesPrintout += p.displayName + " ";
							}
						}
						PrintToPlayer(admin, $"Giving {card} to team {targetPlayer.displayName}  - {members.Count} team mates: {teamMatesPrintout}");

						foreach (var member in members)
						{
							BasePlayer p = BasePlayer.FindByID(member);
							if (p != null && p.IsConnected)
							{
								AdminToggleCard(admin, p, card, args);
							}

						}

					}
					else
					{
						AdminToggleCard(admin, targetPlayer, card, args);
					}
				}
				else
				{

					ulong userID;
					if (ulong.TryParse(args[0], out userID))
					{
						targetPlayer = BasePlayer.FindByID(userID);
						if (targetPlayer != null)
						{

							if (args.Length == 2 && args[1] == "team")
							{

								var members = GetPlayerTeam(targetPlayer.userID);
								PrintToPlayer(admin, $"Giving {card} to team {targetPlayer.displayName} has {members.Count} team mates");
								foreach (var member in members)
								{
									BasePlayer p = BasePlayer.FindByID(member);
									if (p != null && p.IsConnected)
									{
										AdminToggleCard(admin, p, card, args);
									}

								}

							}
							else
							{
								AdminToggleCard(admin, targetPlayer, card, args);
							}


							return;
						}
						else
						{

						}

					}
					else
					{
						// no player name parse and no id parsed
						if (cardsThatNeedNoTarget.Contains(card))
						{
							// target self in this case
                            AdminToggleCard(admin, admin, card, args);
							return;
						}
                    }

					PrintToPlayer(admin, $"could not find player : {args[0]}");
				}
			}
		}
		void AdminToggleCard(BasePlayer admin, BasePlayer targetPlayer, Card card, string[] args)
		{
			if (HasCard(targetPlayer.userID, card))
			{
				TakeCard(targetPlayer.userID, card, args, admin);
				PrintToPlayer(admin, $"Removed {card} from {targetPlayer.displayName}");
			}
			else
			{
				GiveCard(targetPlayer.userID, card, args, admin);
				PrintToPlayer(admin, $"Gave {card} to {targetPlayer.displayName}");
			}
		}



		[ConsoleCommand("payback2")]
		void Console_Payback(ConsoleSystem.Arg arg)
		{
			var player = arg.Connection?.player as BasePlayer;
			if (player != null) {
				if (!IsAdmin(player)) return;
			}
			CommandPayback(player, "", arg.Args);
		}

		[ChatCommand("payback2")]
		void ChatCommandPayback(BasePlayer player, string cmd, string[] args)
		{
			if (!IsAdmin(player)) return;
			SendReply(player, "Check Payback2 output in F1 console!");
			CommandPayback(player, cmd, args);
		}
		void CommandPayback(BasePlayer player, string cmd, string[] args)
		{
			if (player != null && !IsAdmin(player)) return;
			// list all cards

			if (args == null || args.Length == 0)
			{
				DoPaybackPrintout(player, args);
				return;
			}

			List<string> argsList = new List<string>(args);
			if (argsList.FirstOrDefault(x => x == "show") != null)
			{
				string output = "Active Cards:\n";
				// show all active cards and players
				foreach (var userid in cardMap.Keys)
				{
					var targetPlayer = BasePlayer.FindByID(userid);
					string playername = "";
					if (targetPlayer != null)
					{
						playername = targetPlayer.displayName;
					}
					HashSet<Card> cards = cardMap[userid];
					output += $"{userid} : {playername}\n";
					foreach (var card in cards)
					{
						output += $"\n{card.ToString()} : {UI2.ColorText(descriptions[card], "white")}";
					}
					output += "\n\n";
				}
				PrintToPlayer(player, output);

			}

			if (argsList.FirstOrDefault(x => x == "clear") != null)
			{

				foreach (var userid in new List<ulong>(cardMap.Keys))
				{
					var targetPlayer = BasePlayer.FindByID(userid);
					string playername = "";
					if (targetPlayer != null)
					{
						playername = targetPlayer.displayName;
					}

					if (player != null)
					{
						HashSet<Card> cards = cardMap[userid];
						foreach (var card in new HashSet<Card>(cards))
						{
							TakeCard(player, card);
						}
					}

				}

				cardMap.Clear();
				PrintToPlayer(player, "removed all cards from all players");
			}

		}

		const string PAYBACK_VERSION = "Payback2";
		void DoPaybackPrintout(BasePlayer player, string[] args)
		{


			Dictionary<Card, List<string>> cardToAliases = new Dictionary<Card, List<string>>();
			foreach (var alias in cardAliases.Keys)
			{
				Card c = cardAliases[alias];
				List<string> aliases;
				if (!cardToAliases.TryGetValue(c, out aliases))
				{
					aliases = new List<string>();
					cardToAliases[c] = aliases;
				}
				aliases.Add(alias);
			}

			var cards = Enum.GetValues(typeof(Card));
			string output = "";

			output += "\n" + "Add \"team\" after a command to apply the effect to target player's team as well as them.  Example: /bear <steamid> team";
			////output += "\n" + "/setdroppercent <1-100>% to change the chance butterfingers would drop";
			output += "\n" + $"admins require the permisison {permission_admin} to use these commands!";
			output += "\n" + $"use '/{PAYBACK_VERSION} show' to see which players have which cards";
			output += "\n" + $"use '/{PAYBACK_VERSION} clear' to remove all cards from all players.";
			output += "\n" + $"It is NOT necessary to remove effects from players when finished.";
			//output += "\n" + $"Whitelist temp banned players with: bancheckexception <id>";

			output += $"\n\nPayback 2 Commands:";

			foreach (Card card in cards)
			{
				string desc;
				descriptions.TryGetValue(card, out desc);

				List<string> aliases = cardToAliases[card];
				string aliasesTogether = "";
				aliases.ForEach(x => aliasesTogether += $"[ {UI2.ColorText(x, "yellow")} ] ");


				output += "\n\n" + $"{aliasesTogether}: { UI2.ColorText(desc, "white")}";
			}

			if (Payback == null)
			{
				output += "\n\n " + UI2.ColorText("Payback (the original) not detected, did you know there's even more Payback available at https://payback.fragmod.com?", "white");
			}

            output += "\n\n " + UI2.ColorText("CHADIUS.IO :", "purple") + UI2.ColorText(" Are you protected by Rust's #1 AI Admin? Eliminate cheaters on autopilot at chadius.io", "white");


            PrintToPlayer(player, output);
		}

		//| ==============================================================
		//| PAYBACK OPTIONS
		//| ==============================================================

		Dictionary<ulong, HashSet<Card>> cardMap = new Dictionary<ulong, HashSet<Card>>();
		public bool HasAnyCard(ulong userID)
		{
			HashSet<Card> cards = null;
			if (cardMap.TryGetValue(userID, out cards))
			{
				if (cards.Count > 0)
				{
					return true;
				}
			}
			return false;
		}
		public bool HasCard(ulong userID, Card card)
		{
			HashSet<Card> cards;
			if (cardMap.TryGetValue(userID, out cards))
			{
				if (cards.Contains(card))
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}

		public void TakeCard(BasePlayer player, Card card, string[] args = null, BasePlayer admin = null)
		{
			TakeCard(player.userID, card, args, admin);
		}
		public void TakeCard(ulong userID, Card card, string[] args = null, BasePlayer admin = null)
		{
			HashSet<Card> cards;
			if (!cardMap.TryGetValue(userID, out cards))
			{
				cards = new HashSet<Card>();
				cardMap[userID] = cards;
			}
			cards.Remove(card);

			var player = BasePlayer.FindByID(userID);

			if (card == Card.Sit)
			{
				if (player != null)
				{
					DoSitCommand(player, admin, args);
				}
			} else if (card == Card.PotatoMode)
			{
				DoPotato(player, null, args, true);
			} else if (card == Card.Hogwild)
			{
				DoHog(player, null, args, true);
			} else if (card == Card.Interrogate)
			{
				DoInterrogate(player, null, args, true);
			} else if (card == Card.Spitroast)
			{
				DoSpitroastCommand(player, null, args);
			} else if (card == Card.Crucify)
			{
				if (player != null)
				{
					DoCrucifyCommand(player, admin);
				}
			} else if (card == Card.Bonk)
			{
				if (player != null)
				{
					ToggleBonk(player, admin, true);
				}

				bonkingAdmins.Remove(player.userID);
            }

            //| remove the card from any persistence
            if (cardsWhichCanPersist.Contains(card))
            {

                HashSet<Card> persistentCards = null;
                if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                {
                    persistentCards.Remove(card);
                }

            }

        }

		#endregion

		#region DiscordEmbeds
		void SendToDiscordWebhook(Dictionary<string, string> messageData, string title = "TEMP GAME BAN DETECTED")
		{
			if (config.webhooks == null || config.webhooks.Count == 0)
			{
				Puts($"Could not send Discord Webhook: webhook not configured");
				return;
			}

			string discordEmbedTitle = title;


			List<object> fields = new List<object>();

			foreach (var key in messageData.Keys)
			{
				string data = messageData[key];
				fields.Add(new { name = $"{key}", value = $"{data}", inline = false });
			}

			object f = fields.ToArray();


			foreach (var webhook in config.webhooks)
			{
				SendWebhook(webhook, (string)discordEmbedTitle, f);
			}
		}

		private void SendWebhook(string WebhookUrl, string title, object fields)
		{
			if (string.IsNullOrEmpty(WebhookUrl))
			{
				Puts("Error: Someone tried to use a command but the WebhookUrl is not set!");
				return;
			}

			//test
			string json = new SendEmbedMessage(13964554, title, fields).ToJson();

			webrequest.Enqueue(WebhookUrl, json, (code, response) =>
			{
				if (code == 429)
				{
					Puts("Sending too many requests, please wait");
					return;
				}

				if (code != 204)
				{
					Puts(code.ToString());
				}
				if (code == 400)
				{
					Puts(response + "\n\n" + json);
				}
			}, this, Oxide.Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
		}

		private class SendEmbedMessage
		{
			public SendEmbedMessage(int EmbedColour, string discordMessage, object _fields)
			{
				object embed = new[]
				{
					new
					{
						title = discordMessage,
						fields = _fields,
						color = EmbedColour,
						thumbnail = new Dictionary<object, object>() { { "url", "http://na.fragmod.com/fragimages/dec2023/payback_hammer_icon_2.png" } },
					}
				};
				Embeds = embed;
			}

			[JsonProperty("embeds")] public object Embeds { get; set; }

			public string ToJson() => JsonConvert.SerializeObject(this);
		}
		#endregion

		#region Initialize
		
		//| ==============================================================
		//| INIT
		//| ==============================================================
		void Initialize()
		{
			Unsubscribe("OnPlayerVoice");
			Unsubscribe($"OnEntityKill");
			Unsubscribe($"OnSignalBroadcast");


            timer.Once(0.1f, () => {

				LoadData();

				permission.RegisterPermission(permission_admin, this);

				var cards = Enum.GetValues(typeof(Card));

				foreach (Card card in cards)
				{
					cardAliases[card.ToString().ToLower()] = card;
				}
				foreach (var alias in cardAliases.Keys)
				{
					//| Payback1 will handle all commands it can if it exists.
					if (Payback != null)
					{
						if (cardsInPayback1.Contains(cardAliases[alias]))
						{
							continue;
						}
					}

					//| add commands for this version
					cmd.AddChatCommand(alias, this, nameof(GenericChatCommand));
					cmd.AddConsoleCommand(alias, this, nameof(GenericConsoleCommand));
				}

                Worker.StaticStartCoroutine(AdminVisualizationCo());

                ImageLibrary?.CallHook("AddImage", url_payback_logo, url_payback_logo, (ulong)0);
                ImageLibrary?.CallHook("AddImage", url_payback_logo_raised, url_payback_logo_raised, (ulong)0);

            });
			
		}
		void GenericChatCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!IsAdmin(player)) return;
			string argsTogether = "";
			foreach (var arg in args)
			{
				argsTogether += arg + " ";
			}
			//SendReply(player, $"cmd: {cmd} args {argsTogether}");
			Card card;
			if (cardAliases.TryGetValue(cmd.ToLower(), out card))
			{
				AdminCommandToggleCard(player, card, args);
			}
		}
		void GenericConsoleCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Connection?.player as BasePlayer;

			if (player != null)
			{
				if (!IsAdmin(player)) return;
			}
			if (arg == null) return;
			if (arg.cmd == null) return;

			string argsTogether = "";

			if (arg.Args != null)
			{
				foreach (var param in arg.Args)
				{
					argsTogether += param + " ";
				}
			}

			string cmd = string.Empty;
			if (arg.cmd.Name != null)
			{
				cmd = arg.cmd.Name;
			}

			Card card;
			if (cardAliases.TryGetValue(cmd.ToLower(), out card))
			{
				if (arg.Args == null)
				{
					arg.Args = new string[0];
				}
				AdminCommandToggleCard(player, card, arg.Args);
			}
		}
		void OnServerInitialized(bool serverIsNOTinitialized)
		{
			bool serverHasInitialized = !serverIsNOTinitialized;
			Initialize();


			//| preload images
			timer.Once(10, () => {

				if (ImageLibrary == null)
				{
					Puts($"[Payback2] (Optional) Please install the ImageLibrary plugin for optimal performance in Payback2 [https://umod.org/plugins/image-library]");
				} else
				{
					AddImage(interrogate_closed_url);
					AddImage(interrogate_open_url);
				}
			});
		}

		#endregion

		#region ViewInventoryCommands

		//| ==============================================================
		//| ViewInventory - Copied from Whispers88 and modified here
		//| ==============================================================
		private static List<string> _viewInventoryHooks = new List<string> { "OnLootEntityEnd", "CanMoveItem", "OnEntityDeath" };

		void ViewTargetPlayerInventory(BasePlayer target, BasePlayer admin)
		{
			if (admin == null) return;
			if (admin.IsSpectating())
			{
				PrintToPlayer(admin, $"{UI2.ColorText($"[PAYBACK WARNING] ", "yellow") } : {UI2.ColorText($"cannot open target's inventory while spectating! you must respawn", "white")}");
				return;
			}
			PrintToPlayer(admin, $"{UI2.ColorText($"[PAYBACK WARNING] ", "yellow") } : {UI2.ColorText($"you must exit the F1 console immediately after using the command to view inventory", "white")}");

			ViewInvCmd(admin.IPlayer, "ViewInvCmd", new string[] { $"{target.userID}" });
		}

		private void ViewInvCmd(IPlayer iplayer, string command, string[] args)
		{
			BasePlayer player = iplayer.Object as BasePlayer;
			if (player == null) return;

			//if (!HasPerm(player.UserIDString, permission_admin))
			//{
			//    ChatMessage(iplayer, GetLang("NoPerms"));
			//    return;
			//}


			if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
			{
				RaycastHit hitinfo;
				if (!Physics.Raycast(player.eyes.HeadRay(), out hitinfo, 3f, (int)Layers.Server.Players))
				{
					ChatMessage(iplayer, "NoPlayersFoundRayCast");
					return;
				}
				BasePlayer targetplayerhit = hitinfo.GetEntity().ToPlayer();
				if (targetplayerhit == null)
				{
					ChatMessage(iplayer, "NoPlayersFoundRayCast");
					return;
				}
				//ChatMessage(iplayer, "ViewingPLayer", targetplayerhit.displayName);
				ViewInventory(player, targetplayerhit);
				return;
			}
			IPlayer target = FindPlayer(args[0]);
			if (target == null)
			{
				//ChatMessage(iplayer, "NoPlayersFound", args[0]);
				return;
			}
			BasePlayer targetplayer = target.Object as BasePlayer;
			if (targetplayer == null)
			{
				//ChatMessage(iplayer, "NoPlayersFound", args[0]);
				return;
			}
			//ChatMessage(iplayer, "ViewingPLayer", targetplayer.displayName);
			ViewInventory(player, targetplayer);
		}

		#endregion Commands

		#region Methods
		private List<LootableCorpse> _viewingcorpse = new List<LootableCorpse>();
		private void ViewInventory(BasePlayer player, BasePlayer targetplayer)
		{
			if (_viewingcorpse.Count == 0)
				SubscribeToHooks();

			player.EndLooting();

			var corpse = GetLootableCorpse(targetplayer.displayName);
			corpse.SendAsSnapshot(player.Connection);

			timer.Once(1f, () =>
			{
				StartLooting(player, targetplayer, corpse);
			});
		}

		LootableCorpse GetLootableCorpse(string title = "")
		{
			LootableCorpse corpse = GameManager.server.CreateEntity(StringPool.Get(2604534927), Vector3.zero) as LootableCorpse;
			corpse.CancelInvoke("RemoveCorpse");
			corpse.syncPosition = false;
			corpse.limitNetworking = true;
			//corpse.playerName = targetplayer.displayName;
			corpse.playerName = title;
			corpse.playerSteamID = 0;
			corpse.enableSaving = false;
			corpse.Spawn();
			corpse.SetFlag(BaseEntity.Flags.Locked, true);
			Buoyancy bouyancy;
			if (corpse.TryGetComponent<Buoyancy>(out bouyancy))
			{
				UnityEngine.Object.Destroy(bouyancy);
			}
			Rigidbody ridgidbody;
			if (corpse.TryGetComponent<Rigidbody>(out ridgidbody))
			{
				UnityEngine.Object.Destroy(ridgidbody);
			}
			return corpse;
		}

		private void StartLooting(BasePlayer player, BasePlayer targetplayer, LootableCorpse corpse)
		{
			player.inventory.loot.AddContainer(targetplayer.inventory.containerMain);
			player.inventory.loot.AddContainer(targetplayer.inventory.containerWear);
			player.inventory.loot.AddContainer(targetplayer.inventory.containerBelt);
			player.inventory.loot.entitySource = corpse;
			player.inventory.loot.PositionChecks = false;
			player.inventory.loot.MarkDirty();
			player.inventory.loot.SendImmediate();
			player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
			_viewingcorpse.Add(corpse);
		}
		private void StartLootingContainer(BasePlayer player, ItemContainer container, LootableCorpse corpse) {
			player.inventory.loot.AddContainer(container);
			player.inventory.loot.entitySource = corpse;
			player.inventory.loot.PositionChecks = false;
			player.inventory.loot.MarkDirty();
			player.inventory.loot.SendImmediate();
			player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
			_viewingcorpse.Add(corpse);
		}

		#endregion Methods

		#region Hooks
		private void OnLootEntityEnd(BasePlayer player, LootableCorpse corpse)
		{
			if (!_viewingcorpse.Contains(corpse)) return;

			_viewingcorpse.Remove(corpse);
			if (corpse != null)
				corpse.Kill();

			if (_viewingcorpse.Count == 0)
				UnSubscribeFromHooks();

		}


		void OnEntityDeath(LootableCorpse corpse, HitInfo info)
		{
			if (!_viewingcorpse.Contains(corpse)) return;
			_viewingcorpse.Remove(corpse);
			if (corpse != null)
				corpse.Kill();
			if (_viewingcorpse.Count == 0)
				UnSubscribeFromHooks();
		}
		#endregion Hooks

		#region Helpers

		private IPlayer FindPlayer(string nameOrId)
		{
			return BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == nameOrId || x.displayName.Contains(nameOrId, System.Globalization.CompareOptions.IgnoreCase))?.IPlayer;
		}

		private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

		private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);
		private void ChatMessage(IPlayer player, string langKey, params object[] args)
		{
			if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
		}

		private void UnSubscribeFromHooks()
		{
			foreach (var hook in _viewInventoryHooks)
				Unsubscribe(hook);
		}

		private void SubscribeToHooks()
		{
			foreach (var hook in _viewInventoryHooks)
				Subscribe(hook);
		}
		#endregion

		#region IMAGELIBRARY

		void AddImage(string url)
		{
			if (ImageLibrary == null) return;
			var obj = ImageLibrary.Call("HasImage", url, (ulong)0);
			if (obj != null && (bool)obj == false)
			{
				ImageLibrary.CallHook("AddImage", url, url, (ulong)0);
			}
		}

        private string GetImage(string url)
        {
            if (ImageLibrary == null) return url;
            var obj = ImageLibrary?.Call("GetImage", url);
            if (obj == null || obj.ToString() == null) return url;
            return obj?.ToString();
        }


        #endregion

        #region UTILITIES


        //| ==============================================================
        //| UTILITIES
        //| ==============================================================

        public static bool IsNPC(BasePlayer player)
        {
            return player.net.connection == null;
        }
        public static string ColorText(string input, string color)
        {
            return "<color=" + color + ">" + input + "</color>";
        }

        public Dictionary<ulong, Dictionary<string, float>> textMap = new Dictionary<ulong, Dictionary<string, float>>();
        public void ShowText(ulong userID, string textId, string textContent, float duration, Vector3 position, int fontSize = 56, bool override_cooldown = false)
        {

            Dictionary<string, float> userTextMap = null;
            textMap.TryGetValue(userID, out userTextMap);
            if (userTextMap == null)
            {
                userTextMap = new Dictionary<string, float>();
                textMap[userID] = userTextMap;
            }

            float lastTextTimestamp = -duration * 2;
            userTextMap.TryGetValue(textId, out lastTextTimestamp);

            if (Time.realtimeSinceStartup - lastTextTimestamp > (duration - Time.fixedDeltaTime) || override_cooldown)
            {

                userTextMap[textId] = Time.realtimeSinceStartup;
                BasePlayer player = BasePlayer.FindByID(userID);

                if (player == null) return;

                if (player.Connection.authLevel == 0)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }


                player.SendConsoleCommand("ddraw.text", duration, Color.white, position, $"<size={fontSize}>{textContent}</size>");

                if (player.Connection.authLevel == 0)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }

            }
        }
        private void PlayNetworkAnimation(BasePlayer bpr, string command)
        {
            if (bpr.IsWounded() || bpr.IsSleeping()) return;

            Network.Visibility.Group nGroup = Net.sv.visibility.GetGroup(bpr.transform.position);

            foreach (var bp2 in BasePlayer.activePlayerList)
            {
                if (!Net.sv.visibility.IsInside(nGroup, bp2.transform.position)) continue;

				var netwrite = Net.sv.StartWrite();
                netwrite.PacketID(Message.Type.ConsoleCommand);
                netwrite.String(ConsoleSystem.BuildCommand(command));
                netwrite.Send(new SendInfo(bp2.net.connection));
            }
        }

        private void PlayNetworkAnimation(string id, string command)
        {
            BasePlayer bpr = null;
            foreach (BasePlayer p1 in BasePlayer.allPlayerList)
            {
                if (p1.UserIDString == id)
                {
                    bpr = p1;
                    break;
                }
            }
            if (bpr == null) return;

            PlayNetworkAnimation(bpr, command);
        }

        float Random()
		{
			return UnityEngine.Random.Range(0f, 1f);
		}
		string TryGetDisplayName(ulong userID)
		{
			return covalence.Players.FindPlayerById(userID.ToString())?.Name;
		}


		public BasePlayer GetPlayerWithName(string displayName)
		{
			foreach (var p in BasePlayer.allPlayerList)
			{
				if (p.displayName.ToLower().Contains(displayName.ToLower()))
				{
					return p;
				}
			}
			return null;
		}
		BaseEntity RaycastFirstEntity(Ray ray, float distance)
		{
			RaycastHit hit;
			if (Physics.Raycast(ray.origin, ray.direction, out hit, distance))
			{
				return hit.GetEntity();
			}
			return null;
		}

		void SetDespawnDuration(DroppedItem dropped, float seconds)
		{
			dropped.Invoke(new Action(dropped.IdleDestroy), seconds);//prevent dropped item from despawn
		}
		void DestroyGroundCheck(BaseEntity entity)
		{
			GameObject.DestroyImmediate(entity.GetComponentInChildren<DestroyOnGroundMissing>());
			GameObject.DestroyImmediate(entity.GetComponentInChildren<GroundWatch>());
		}

		[ChatCommand("sound")]
		void SoundCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!IsAdmin(player)) return;

			if (args.Length == 0)
			{
				SendReply(player, "/sound <asset>");
				return;
			}
			for (int i = 0; i < args.Length; i++)
			{
				string sound = args[i];
				PlaySound(sound, player, false);
			}
		}

		void PrintToPlayer(BasePlayer player, string text)
		{
			if (player == null) {
				Puts($"{text}");
				return;
			}
			//SendReply(player, text);
			player.SendConsoleCommand($"echo {text}");
		}
		public HashSet<ulong> GetPlayerTeam(ulong userID)
		{
			BasePlayer player = BasePlayer.FindByID(userID);

			RelationshipManager.PlayerTeam existingTeam = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
			if (existingTeam != null)
			{
				return new HashSet<ulong>(existingTeam.members);
			}
			return new HashSet<ulong>() { userID };
		}

        void PlaySFX(Vector3 position, string fx, bool globalBroadcast = false)
        {
            global::Effect.server.Run(fx, position + Vector3.up, Vector3.up, null, globalBroadcast);
        }
        void PlaySFX(BasePlayer player, string fx, bool isPublic = false)
        {
            PlaySound(fx, player, !isPublic);
        }

        public void PlaySound(List<string> effects, BasePlayer player, Vector3 worldPosition, bool playlocal = true)
		{
			if (player == null) return;//ai
			foreach (var effect in effects)
			{
				//var sound = new Effect(effect, player, 0, localPosition, localPosition.normalized);
				var sound = new Effect(effect, worldPosition, Vector3.up);
				if (playlocal)
				{
					EffectNetwork.Send(sound, player.net.connection);
				}
				else
				{
					EffectNetwork.Send(sound);
				}
			}
		}

		public void PlaySound(List<string> effects, BasePlayer player, bool playlocal = true)
		{
			if (player == null) return;//ai
			foreach (var effect in effects)
			{
				var sound = new Effect(effect, player, 0, Vector3.zero + Vector3.up * 0.5f, Vector3.forward);
				if (playlocal)
				{
					EffectNetwork.Send(sound, player.net.connection);
				}
				else
				{
					EffectNetwork.Send(sound);
				}
			}
		}
		public void PlaySound(string effect, ListHashSet<BasePlayer> players, bool playlocal = true)
		{
			//all players
			foreach (var player in players)
			{
				PlaySound(effect, player, playlocal);
			}
		}

		bool test = false;

		public void PlaySound(string effect, BasePlayer player, bool playlocal = true, Vector3 posLocal = default(Vector3))
		{
			if (player == null) return;//ai

			var sound = new Effect(effect, player, 0, Vector3.zero, Vector3.forward);
			
			if (posLocal != Vector3.zero)
			{
				sound = new Effect(effect, player.transform.position + posLocal, Vector3.forward);
			}


			if (playlocal)
			{
				EffectNetwork.Send(sound, player.net.connection);
			}
			else
			{
				EffectNetwork.Send(sound);
			}
		}

        public void PlayGesture(BasePlayer target, string gestureName, bool canCancel = false)
        {
            if (target == null) return;
            var gesture = GestureCollection.Instance.StringToGesture(gestureName);
             if (gesture == null)
            {
                return;
            }
            bool saveCanCancel = gesture.canCancel;
            gesture.canCancel = canCancel;
            //target.SendMessage("Server_StartGesture", gesture);
            target.Server_StartGesture(gesture, BasePlayer.GestureStartSource.ServerAction);
            gesture.canCancel = saveCanCancel;
        }
        public class Worker : MonoBehaviour
		{
			public static Worker GetSingleton()
			{
				if (_singleton == null)
				{
					GameObject worker = new GameObject();
					worker.name = "Worker Singleton";
					_singleton = worker.AddComponent<Worker>();
				}
				return _singleton;
			}
			static Worker _singleton;
			public static Coroutine StaticStartCoroutine(IEnumerator c)
			{
				return Worker.GetSingleton().StartCoroutine(c);
			}

		}
		#endregion

		#region ESSENTIALPAYBACK
		//| ==============================================================
		//| ESSENTIAL PAYBACK FUNCTIONS
		//| ==============================================================

		HashSet<BaseNetworkable> entitiesWatchingForKilledMounts = new HashSet<BaseNetworkable>();

        public const string gamerChairPrefab = "assets/prefabs/deployable/secretlab chair/secretlabchair.deployed.prefab";
        public const string invisibleChairPrefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
        public const string toiletChairPrefab = "assets/bundled/prefabs/static/toilet_b.static.prefab";
        public const string beachChairPrefab = "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab";

        public static string targetChairPrefab = invisibleChairPrefab;

        HashSet<BaseMountable> chairsPreventingDismount = new HashSet<BaseMountable>();


		//| normal chair or secretlabs
		//string chairPrefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
		string chairPrefab = "assets/prefabs/deployable/secretlab chair/secretlabchair.deployed.prefab";

		Dictionary<ulong, BaseEntity> sitChairMap = new Dictionary<ulong, BaseEntity>();

		BaseEntity InvisibleSit(BasePlayer targetPlayer)
		{
			var chair = GameManager.server.CreateEntity(targetChairPrefab, targetPlayer.transform.position);
			var mount = chair as BaseMountable;
			chair.Spawn();

			chairsPreventingDismount.Add(mount);

			GameObject.DestroyImmediate(chair.GetComponentInChildren<DestroyOnGroundMissing>());
			GameObject.DestroyImmediate(chair.GetComponentInChildren<GroundWatch>());

			if (targetPlayer.isMounted)
			{
				targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);
			}

			Timer t = null;
			t = timer.Every(0.25f, () => {
				if (chair == null || chair.IsDestroyed)
				{
					t.Destroy();
					return;
				}
				if (targetPlayer != null)
				{
					if (!targetPlayer.isMounted)
					{
						targetPlayer.Teleport(chair.transform.position);
						targetPlayer.SendNetworkUpdateImmediate();

                        mount.MountPlayer(targetPlayer);
						chair.SendNetworkUpdateImmediate();
					}

					if (Vector3.Distance(targetPlayer.transform.position, chair.transform.position) > 1)
					{
                        targetPlayer.Teleport(chair.transform.position);
                        targetPlayer.SendNetworkUpdateImmediate();
                    }

					//PrintToChat($"D: {}");
				}
				else
				{
					//Puts("Attempted to mount player to chair, but they were null!");
					chair.Kill();
					t.Destroy();
				}

			});
			return chair;
		}


		void DoBagSearch(ulong userID, string[] args, BasePlayer admin = null)
		{
			if (userID == 0) return;
			TakeCard(userID, Card.Bag);

			if (args.Contains("discord"))
			{
				Worker.StaticStartCoroutine(BagSearchCo(userID, true, admin));
			}
			else
			{
				Worker.StaticStartCoroutine(BagSearchCo(userID, false, admin));
			}
		}
		IEnumerator BagSearchCo(ulong userID, bool logToDiscord = false, BasePlayer admin = null)
		{
			yield return null;


			float timestamp = Time.realtimeSinceStartup;
			float maxTimeBetweenFrames = 1 / 20f;

			//| Get bags owned by player

			var allBags = BaseNetworkable.serverEntities.OfType<SleepingBag>();
			//var deployedByTargetBags = new List<SleepingBag>();

			var useridsBaggedByTarget = new HashSet<ulong>();
			var useridsWhoBaggedTarget = new HashSet<ulong>();

			//find the bags that target placed
			foreach (var bag in allBags)
			{
				//| ==============================================================
				if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames)
				{
					yield return null;
					timestamp = Time.realtimeSinceStartup;
				}
				//| ==============================================================

				ulong ownerid = 0;
				var creator = bag.creatorEntity;
				if (creator != null)
				{
					var player = creator as BasePlayer;
					if (player != null)
					{
						ownerid = player.userID;
					}
				}
				else
				{
					ownerid = bag.OwnerID;
				}

				//target bagged someone else
				if (ownerid == userID && bag.deployerUserID != userID)
				{
					//deployedByTargetBags.Add(bag);
					useridsBaggedByTarget.Add(bag.deployerUserID);
				}

				//someone bagged in target
				if (userID == bag.deployerUserID && ownerid != userID)
				{
					useridsWhoBaggedTarget.Add(ownerid);
				}
			}

			var messageData = new Dictionary<string, string>();
			string targetInfo = $"{TryGetDisplayName(userID)}";
			string baggedByString = "";
			string output = $"Players bagged by {targetInfo}:";
			foreach (var userid in useridsBaggedByTarget)
			{
				var displayname = TryGetDisplayName(userid);
				output += $"\n{userid} : {displayname}";

				baggedByString += $"{userid} : {displayname}\n";
			}
			if (baggedByString.Length > 0)
			{
				messageData.Add($"Players bagged by {targetInfo}", baggedByString);
			}
			else
			{
				messageData.Add($"Players bagged by {targetInfo}", "none");
			}

			output += $"\nSteamids who bagged in {targetInfo}:";
			string baggedInString = "";
			foreach (var userid in useridsWhoBaggedTarget)
			{
				var displayname = TryGetDisplayName(userid);
				output += $"\n{userid} : {displayname}";
				baggedInString += $"\n{userid} : {displayname}";
			}
			if (baggedInString.Length > 0)
			{
				messageData.Add($"Players who bagged in {targetInfo}", baggedInString);
			}
			else
			{
				messageData.Add($"Players who bagged in {targetInfo}", "none");
			}

			PrintToPlayer(admin, $"{output}");

			if (logToDiscord)
			{
				SendToDiscordWebhook(messageData, $"Bag Search [{userID}]");
			}

		}

		bool flag_kill_no_loot = false;

		void GiveAdminHammer(BasePlayer admin)
		{
			if (admin == null) return;
			var item = ItemManager.CreateByName("hammer", 1, 2375073548);
			if (item != null)
			{
				GiveItemOrDrop(admin, item, false);
			}
		}
		object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
		{

			if (HasCard(player.userID, Card.Hammer))
			{
				Worker.StaticStartCoroutine(DeleteByCo(entity.OwnerID, player.transform.position, player));
			}
			return null;
		}

		IEnumerator DeleteByCo(ulong steamid, Vector3 position, BasePlayer admin = null)
		{
			yield return null;
			if (steamid == 0UL)
			{
				yield break;
			}


			float maxTimeBetweenFrames = 1 / 60f;
			int maxEntitiesPerFrame = 1;
			float delayBetweenFrames = 1 / 20f;
			float timestamp = Time.realtimeSinceStartup;

			var entities = new List<BaseNetworkable>(BaseNetworkable.serverEntities);

			float fxTimestamp = Time.realtimeSinceStartup;
			float fxCooldown = 0.75f;
			//float fxCooldown = 0.2f;

			var ownedEntities = new List<BaseEntity>();
			foreach (var x in entities)
			{
				var entity = x as BaseEntity;
				if (!(entity == null) && entity.OwnerID == steamid)
				{
					ownedEntities.Add(entity);
				}
				if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames)
				{
					yield return null;
					timestamp = Time.realtimeSinceStartup;
				}
			}

			ownedEntities.Sort((x, y) => Vector3.Distance(x.transform.position, position).CompareTo(Vector3.Distance(y.transform.position, position)));

			timestamp = Time.realtimeSinceStartup;

			int i = 0;

			int count = 0;

			bool playSound = true;

			if (admin != null)
				PlaySound("assets/bundled/prefabs/fx/headshot.prefab", admin, false);

			Vector3 lastPosition = Vector3.zero;


			//| LOOT REMOVAL PASS
			if (flag_kill_no_loot)
			{
				foreach (var baseEntity in ownedEntities)
				{
					var storage = baseEntity as StorageContainer;
					if (storage != null)
					{
						foreach (var item in new List<Item>(storage.inventory.itemList))
						{
							//PrintToPlayer(admin, $"Removing: {item.info.displayName.english}");
							item.GetHeldEntity()?.KillMessage();
							//item.DoRemove();
							//item.Remove();
							ItemManager.RemoveItem(item);
						}
						ItemManager.DoRemoves();
						//storage.inventory.Clear();
					}
				}
			}


			while (i < ownedEntities.Count)
			{
				if (Time.realtimeSinceStartup - timestamp > maxTimeBetweenFrames || count >= maxEntitiesPerFrame)
				{
					yield return new WaitForSeconds(delayBetweenFrames);
					timestamp = Time.realtimeSinceStartup;
					count = 0;
				}

				var baseEntity = ownedEntities[i];
				if (!(baseEntity == null) && baseEntity.OwnerID == steamid)
				{

					if (admin != null && playSound)
					{
						if (Time.realtimeSinceStartup - fxTimestamp > fxCooldown)
						{
							PlaySound("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", admin, true);
							fxTimestamp = Time.realtimeSinceStartup;
						}
					}
					lastPosition = baseEntity.transform.position;

					baseEntity.Kill(BaseNetworkable.DestroyMode.Gib);

					count++;
				}
				i++;

			}
			if (admin != null)
			{
				PlaySound("assets/prefabs/locks/keypad/effects/lock.code.lock.prefab", admin, true);

				timer.Once(0.75f, () => {
					PlaySound("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", admin, true);

					if (!flag_kill_no_loot)
					{
						var effect = GameManager.server.CreateEntity("assets/prefabs/deployable/fireworks/mortarred.prefab", lastPosition);
						effect.Spawn();
						var firework = effect as BaseFirework;
						firework.fuseLength = 0;
						firework.Ignite(firework.transform.position - Vector3.down);
					}

				});
			}
			yield return null;
		}
		void ResolveConflictingCommands(BasePlayer player, BasePlayer admin = null, int ignoreCard = (int) Card.NoRest, bool fromExternalPlugin = false)
        {
            //Puts($"Payback 2 | ResolveConflictingCommands | HasAnyCard : {HasAnyCard(player.userID)} | Payback : {Payback != null} | IgnoreCard: {(int)ignoreCard}");

            if (HasCard(player.userID, Card.Sit) && ignoreCard != (int) Card.Sit)
			{
				TakeCard(player, Card.Sit);
			}
			if (HasCard(player.userID, Card.Crucify) && ignoreCard != (int) Card.Crucify)
			{
				TakeCard(player, Card.Crucify);
			}
			if (HasCard(player.userID, Card.Spitroast) && ignoreCard != (int) Card.Spitroast) {
				TakeCard(player, Card.Spitroast);
			}
			if (HasCard(player.userID, Card.Hogwild) && ignoreCard != (int) Card.Hogwild)
			{
				TakeCard(player, Card.Hogwild);
			}

			//Puts($"Payback 2 | ResolveConflictingCommands | Payback : {Payback != null} | IgnoreCard: {(Card)ignoreCard} | Bonked: {bonkedPlayers.Contains(player.userID)}");

			if (bonkedPlayers.Contains(player.userID) && ignoreCard != (int) Card.Bonk)
			{
				ToggleBonk(player, admin, true);
			}


            if (Payback != null && !fromExternalPlugin)
            {
                Payback.Call("ResolveConflictingCommands", player, admin, ignoreCard, true);
            }
            
            if (player.isMounted)
            {
                player.GetMounted().DismountPlayer(player, true);
                var car = player.GetMountedVehicle();
                if (car != null)
                {
                    car.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                var mount = player.GetMounted();
                if (mount != null)
				{
                    mount.DismountPlayer(player, true);
                }
            }


        }

		void OnPlayerRespawned(BasePlayer player)
		{
			if (HasCard(player.userID, Card.NoRest) || HasCard(player.userID, Card.Bonk))
			{
				player.EndSleeping();
				player.SendNetworkUpdate();
			}
		}

		void OnEntityKill(BaseNetworkable entity, HitInfo info)
		{
			if (entity == null) return;
			if (entitiesWatchingForKilledMounts.Contains(entity))
			{
				var chair = entity.GetComponentInChildren<BaseMountable>();
				if (chair.GetMounted() != null)
				{
					var player = chair.GetMounted();
					player.GetMounted().DismountPlayer(player, true);
					player.Teleport(chair.transform.position);
					player.Die();
				}
				entitiesWatchingForKilledMounts.Remove(entity);

				timer.Once(0.5f, () => {
					if (entitiesWatchingForKilledMounts.Count == 0)
						Unsubscribe($"OnEntityKill");
				});
			}
		}

		void GiveItemOrDrop(BasePlayer player, Item item, bool stack = false)
		{
			bool success = item.MoveToContainer(player.inventory.containerWear, -1, stack);
            if (!success)
            {
                success = item.MoveToContainer(player.inventory.containerBelt, -1, stack);
            }
            if (!success)
			{
				success = item.MoveToContainer(player.inventory.containerMain, -1, stack);
			}
			if (!success)
			{
				item.Drop(player.transform.position + Vector3.up, Vector3.zero);
			}
		}
		void DoHigherGround(BasePlayer player)
		{
			player.Teleport(player.transform.position + Vector3.up * 20);
			TakeCard(player, Card.HigherGround);
		}


		void DoSitCommand(BasePlayer targetPlayer, BasePlayer adminPlayer, string[] args = null)
		{
			if (targetPlayer == null) return;

			if (HasCard(targetPlayer.userID, Card.Sit))
			{
				if (adminPlayer == null) return;

				if (targetPlayer.isMounted)
				{
					targetPlayer.GetMounted().DismountPlayer(targetPlayer, true);

					var car = targetPlayer.GetMountedVehicle();
					if (car != null)
					{
						car.Kill(BaseNetworkable.DestroyMode.Gib);
					}

					BaseEntity chair = null;
					if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
					{
						chair?.Kill();
					}
				}

				RaycastHit hitinfo;
				if (Physics.Raycast(adminPlayer.eyes.HeadRay(), out hitinfo, 50))
				{
                    var targetPrefab = toiletChairPrefab;

                    if (args != null && args.Contains("beach"))
                    {
                        targetPrefab = beachChairPrefab;
                    }
                    else if (args != null && args.Contains("gamer"))
                    {
                        targetPrefab = gamerChairPrefab;
                    }

                    var chair = GameManager.server.CreateEntity(targetPrefab, hitinfo.point);
					var mount = chair as BaseMountable;
					chair.Spawn();
					sitChairMap[targetPlayer.userID] = chair;
					//targetPlayer.Teleport(chair.transform.position + chair.transform.forward * 0.5f);
					targetPlayer.EndSleeping();

					DestroyGroundCheck(chair);

					Vector3 lookAtPosition = adminPlayer.transform.position;
					lookAtPosition.y = mount.transform.position.y;

					timer.Once(0.25f, () => {

						if (targetPlayer != null)
						{
							mount.MountPlayer(targetPlayer);


							chair.transform.LookAt(lookAtPosition);
							chair.SendNetworkUpdateImmediate();

							Worker.StaticStartCoroutine(SitCo(targetPlayer));
						}
						else
						{
							//Puts("Attempted to mount player to chair, but they were null!");
							chair.Kill();
						}

					});

				}

			}
			else
			{
				BaseEntity chair = null;
				if (sitChairMap.TryGetValue(targetPlayer.userID, out chair))
				{
					if (chair != null)
					{
						chair.Kill();
					}
				}

			}
		}

		IEnumerator SitCo(BasePlayer player)
		{
			yield return new WaitForSeconds(0.25f);
			BaseEntity chair;
			sitChairMap.TryGetValue(player.userID, out chair);
			BaseMountable mount = chair as BaseMountable;

			while (player != null && chair != null 
				&& ( HasCard(player.userID, Card.Sit) || HasCard(player.userID, Card.Crucify) )
				)
			{
				if (player != null)
				{
					if (player.IsSleeping())
					{
						player.EndSleeping();
					}
					if (player.isMounted)
					{
						var playerMount = player.GetMounted();
						if (playerMount != mount)
						{
							playerMount.DismountPlayer(player, true);
							//PrintToChat($"Dismount player for sit: {playerMount}");

						}
					}

					var dist = Vector3.Distance(chair.transform.position, player.transform.position);
					if (dist > 2)
					{
						player.Teleport(chair.transform.position + chair.transform.forward * 0.5f);
						//yield return new WaitForSeconds(1);
					}
					if (!player.isMounted && dist < 2)
					{

						//mount.AttemptMount(player, false);
						//player.MountObject(mount);
						player.SetMounted(mount);

						//PrintToChat($"Attempt mount: {mount} pmount:  {player.GetMounted()}");
						//yield return new WaitForSeconds(0.25f);
					}

				}
				else
				{
					chair.Kill();
				}
				yield return new WaitForSeconds(0.25f);
			}
			if (chair != null)
			{
				chair.Kill();
			}

		}

		object CanDismountEntity(BasePlayer player, BaseMountable entity)
		{
			if (cardMap.Count == 0 && chairsPreventingDismount.Count == 0) return null;//early out for maximum perf

			if (HasCard(player.userID, Card.Sit))
			{
				return false;
			}	
			
			if (HasCard(player.userID, Card.Crucify))
			{
				return false;
			}

			if (HasCard(player.userID, Card.Spitroast))
			{
				return false;
			}

			//cleanup dead chairs
			foreach (var chair in new HashSet<BaseMountable>(chairsPreventingDismount))
			{
				if (chair == null || chair.IsDestroyed)
				{
					chairsPreventingDismount.Remove(chair);
				}
			}

			if (chairsPreventingDismount.Contains(entity))
			{
				return false;
			}

			return null;
		}

		public HashSet<Card> cardsInPayback1 = new HashSet<Card>() {
			Card.Pacifism,
			Card.InstantKarma,
			Card.Dud,
			Card.Sit,
			Card.HigherGround,
			Card.NoRest,
			Card.ViewLoot,
			Card.Hammer,
			Card.Bag,
		};

		void OnPlayerConnected(BasePlayer player)
		{

			//| re-apply saved cards if we're using persistent data
			if (config.persistentCommands)
			{

				HashSet<Card> persistentCards = null;
				if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
				{
					foreach (var card in persistentCards)
					{
						timer.Once(2f, () =>
						{
							if (player == null) return;
                            GiveCard(player.userID, card, new string[0], null);
                        });
					}
				}

			}
		}
        
        #endregion

        #region Config

        private void Init()
		{
			LoadConfig();

			//| unsub hooks that are not needed without the command running
            this.Unsubscribe("CanLootEntity");
            this.Unsubscribe("CanCombineDroppedItem");

        }

        private PluginConfig config;

		protected override void LoadConfig()
		{
			base.LoadConfig();
			config = Config.ReadObject<PluginConfig>();
			SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig()
		{
			config = new PluginConfig
			{

			};
			SaveConfig();
		}

		private class PluginConfig
		{

			[JsonProperty("These discord webhooks will get notified. Dont forget the [\"\"] Format: \"webhooks\" : [\"hook\"],")]
			public List<string> webhooks = new List<string>();

			[JsonProperty("Notify player when attacked by cheater")]
			public bool notifyCheaterAttacking = true;

			[JsonProperty("report payback command usage to discord")]
			public bool logPaybackCommands = false;

            [JsonProperty("commands persist : save commands and re-apply them players between server restarts and plugin reloads")]
            public bool persistentCommands = false;

            [JsonProperty("Show Payback UI to admins while commands are active")]
            public bool showUI = false;

        }



        #endregion Config

        #region Data
        //| ==============================================================
        //| DATA
        //| ==============================================================


        string filename_data {
			get
			{
				return $"{PAYBACK_VERSION}/{PAYBACK_VERSION}.dat";
			}
		}


		DynamicConfigFile file_payback_data;

		public PaybackData paybackData = new PaybackData();

		public class PaybackData
		{
            public Dictionary<ulong, HashSet<Card>> persistentCommandMap = new Dictionary<ulong, HashSet<Card>>();

        }

        void Unload()
		{
			//Puts("Unload Tommygun's Payback");




			Worker.GetSingleton()?.StopAllCoroutines();
			GameObject.Destroy(Worker.GetSingleton());

            SaveData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				UI2.ClearUI(player);

				if (HasAnyCard(player.userID))
				{
					HashSet<Card> cards = cardMap[player.userID];
					foreach (var card in new HashSet<Card>(cards))
					{
						TakeCard(player, card);
					}
				}
			}
			foreach (var npc in animals)
			{
				npc?.Kill();
			}
			foreach (var ent in entitiesThatDealNoDamage)
			{
				ent?.Kill();
			}			
			foreach (var ent in invulnerableEntities)
			{
				ent?.Kill();
			}
			foreach (var list in cowboynetworkables.Values)
			{
				foreach (var ent in list)
				{
					ent?.Kill();
				}
			}
			foreach (var item in interrogationMasks.Values)
			{
				item?.Remove();
			}
			foreach (var rocket in airstrikeRockets)
			{
				rocket?.Kill();
			}

		}



		private void SaveData()
		{
			//| WRITE SERVER FILE
			file_payback_data.WriteObject(paybackData);
		}
		private void LoadData()
		{
			//Puts("Load Data");

			ReadDataIntoDynamicConfigFiles();
			LoadFromDynamicConfigFiles();
		}
		void ReadDataIntoDynamicConfigFiles()
		{
			file_payback_data = Interface.Oxide.DataFileSystem.GetFile(filename_data);
		}
		void LoadFromDynamicConfigFiles()
		{
			try
			{
				paybackData = file_payback_data.ReadObject<PaybackData>();
                
                if (paybackData.persistentCommandMap == null)
                {
                    paybackData.persistentCommandMap = new Dictionary<ulong, HashSet<Card>>();
                }

            }
            catch (Exception e)
			{
				paybackData = new PaybackData();
				//Puts($"Creating new data {e}");
			}
			Worker.StaticStartCoroutine(ApplyPersistentCommandsFromReloadCo());
		}
		IEnumerator ApplyPersistentCommandsFromReloadCo()
		{
            if (!config.persistentCommands)
			{
				yield break;
			}

			yield return null;
			
			foreach (var player in BasePlayer.activePlayerList.ToArray()) { 
				if (player != null)
				{

                    HashSet<Card> persistentCards = null;
                    if (paybackData.persistentCommandMap.TryGetValue(player.userID, out persistentCards))
                    {
                        foreach (var card in persistentCards)
                        {
                            GiveCard(player.userID, card, new string[0], null);
                        }
                    }
                    yield return null;
                }
			}

        }

		public const string permission_admin = "payback.admin";

		public bool IsAdmin(BasePlayer player)
		{
			if (permission.UserHasPermission(player.Connection.userid.ToString(), permission_admin))
			{
				return true;
			}
			return false;
		}
		#endregion

		#region UICODE

		//| ===================

		//| =======================================
		//| TOMMYGUN'S PROPRIETARY UI CLASSES
		//| =======================================
		//| 
		//| Code contained below this line is not licensed to be used, copied, or modified.
		//| 
		//| 
		//| =======================================

		//| ===================
		public class UI2
		{
			public static Vector4 vectorFullscreen = new Vector4(0, 0, 1, 1);

			public static string ColorText(string input, string color)
			{
				return "<color=" + color + ">" + input + "</color>";
			}

			public static void ClearUI(BasePlayer player)
			{
				foreach (var guid in UI2.guids)
				{
					CuiHelper.DestroyUi(player, guid);
				}
			}

			//| =============================
			//| DIRT 
			//| =============================
			public static Dictionary<ulong, HashSet<string>> dirtyMap = new Dictionary<ulong, HashSet<string>>();
			public static HashSet<string> GetDirtyBitsForPlayer(BasePlayer player)
			{
				if (player == null) return new HashSet<string>();
				if (!dirtyMap.ContainsKey(player.userID))
				{
					dirtyMap[player.userID] = new HashSet<string>();
				}
				return dirtyMap[player.userID];
			}

			//| =============================
			//| LAYOUT 
			//| =============================

			public class Layout
			{

				public Vector2 startPosition;

				public Vector4 cellBounds;
				public Vector2 padding;
				public Vector4 cursor;
				public int maxRows;

				public int row = 0;
				public int col = 0;

				public void Init(Vector2 _startPosition, Vector4 _cellBounds, int _maxRows, Vector2 _padding = default(Vector2))
				{
					startPosition = _startPosition;
					cellBounds = _cellBounds;
					maxRows = _maxRows;
					padding = _padding;
					row = 0;
					col = 0;
				}

				public void NextCell(System.Action<Vector4, int, int> populateAction)
				{
					float cellX = startPosition.x + (col * (cellBounds.z + padding.x)) + padding.x / 2f;
					float cellY = startPosition.y - (row * (cellBounds.w + padding.y)) - cellBounds.w - padding.y;

					cursor = new Vector4(cellX, cellY, cellX, cellY);

					populateAction(cursor, row, col);

					//move to next element
					row++;
					if (row == maxRows)
					{
						row = 0;
						col++;
					}

				}

				public void Reset()
				{
					row = 0;
					col = 0;
				}
			}



			//| =============================
			//| COLOR FUNCTIONS
			//| =============================

			public static string ColorToHex(Color color)
			{
				return ColorUtility.ToHtmlStringRGB(color);
			}
			public static string HexToRGBAString(string hex)
			{
				Color color = Color.white;
				ColorUtility.TryParseHtmlString("#" + hex, out color);
				string c = $"{String.Format("{0:0.000}", color.r)} {String.Format("{0:0.000}", color.g)} {String.Format("{0:0.000}", color.b)} {String.Format("{0:0.000}", color.a)}";
				return c;
			}


			//| =============================
			//| RECT FUNCTIONS
			//| =============================
			public static Vector4 GetOffsetVector4(Vector2 offset)
			{
				return new Vector4(offset.x, offset.y, offset.x, offset.y);
			}
			public static Vector4 GetOffsetVector4(float x, float y)
			{
				return new Vector4(x, y, x, y);
			}

			public static Vector4 SubtractPadding(Vector4 input, float padding)
			{
				float verticalPadding = GetSquareFromWidth(padding);
				return new Vector4(input.x + padding / 2f, verticalPadding / 2f, input.z - padding / 2f, input.w - verticalPadding / 2f);
			}

			public static float GetSquareFromWidth(float width, float aspect = 16f / 9f)
			{
				//return width * 1f / aspect;
				return width * aspect;
			}
			public static float GetSquareFromHeight(float height, float aspect = 16f / 9f)
			{
				//return height * aspect;
				return height * 1f / aspect;
			}

			//specify the screen-space x1, x2, y1 and it will populate y2
			public static Vector4 MakeSquareFromWidth(Vector4 bounds, float aspect = 16f / 9f)
			{
				return new Vector4(bounds.x, bounds.y, bounds.z, bounds.y + GetSquareFromWidth(bounds.z - bounds.x));
			}
			//specify the screen-space x1, y1, and y2 and it will populate the x2
			public static Vector4 MakeSquareFromHeight(Vector4 bounds, float aspect = 16f / 9f)
			{
				return new Vector4(bounds.x, bounds.y, bounds.x + GetSquareFromHeight(bounds.z - bounds.y), bounds.w);
			}
			//make any sized rect from x1, x2, and y1
			public static Vector4 MakeRectFromWidth(Vector4 bounds, float ratio, float aspect = 16f / 9f)
			{
				Vector4 square = MakeSquareFromWidth(bounds, aspect);
				return new Vector4(square.x, square.y, square.z, square.y + (square.w - square.y) * ratio);
			}
			//make any sized rect from y1, y2 and x1
			public static Vector4 MakeRectFromHeight(Vector4 bounds, float ratio, float aspect = 16f / 9f)
			{
				Vector4 square = MakeSquareFromHeight(bounds, aspect);
				return new Vector4(square.x, square.y, square.x + (square.z - square.x) * ratio, square.w);
			}


			//| =============================
			//| UI PANELS
			//| =============================
			public static HashSet<string> guids = new HashSet<string>();

			public static string GetMinUI(Vector4 panelPosition)
			{
				return panelPosition.x.ToString("0.####") + " " + panelPosition.y.ToString("0.####");
			}
			public static string GetMaxUI(Vector4 panelPosition)
			{
				return panelPosition.z.ToString("0.####") + " " + panelPosition.w.ToString("0.####");
			}
			public static string GetColorString(Vector4 color)
			{
				return color.x.ToString("0.####") + " " + color.y.ToString("0.####") + " " + color.z.ToString("0.####") + " " + color.w.ToString("0.####");
			}
			public static CuiElement CreateInputField(CuiElementContainer container, string parent, string panelName, string message, int textSize, string color, Vector4 bounds, string command)
			{

				CuiElement element = new CuiElement
				{
					Name = panelName,
					Parent = parent,
					Components = {
						new CuiInputFieldComponent {
							Align = TextAnchor.MiddleLeft,
							Color = color,
							Command = command,
							//Text = message,
							FontSize = textSize,
						},
						new CuiRectTransformComponent
						{
							AnchorMin = GetMinUI(bounds),
							AnchorMax = GetMaxUI(bounds),
						}
					}
				};
				container.Add(element
				);

				return element;
			}

			public static void CreateOutlineLabel(CuiElementContainer container, string parent, string panelName, string message, string color, int size, Vector4 bounds, TextAnchor textAlignment = TextAnchor.MiddleCenter, float fadeOut = 0, float fadeIn = 0, string outlineColor = "0 0 0 0.8", string outlineDistance = "0.7 -0.7")
			{

				container.Add(new CuiElement
				{
					Name = panelName,
					Parent = parent,
					FadeOut = fadeOut,
					Components = {

						new CuiTextComponent {
							Align = textAlignment,
							Color = color,
							FadeIn = fadeIn,
							FontSize = size,
							Text = message
						},
						new CuiOutlineComponent {
							Color = outlineColor,
							Distance = outlineDistance,
						},
						new CuiRectTransformComponent
						{
							AnchorMin = GetMinUI(bounds),
							AnchorMax = GetMaxUI(bounds),
						}
					}
				});
			}

			public static void CreateLabel(CuiElementContainer container, string parent, string panelName, string message, string color, int size, string aMin, string aMax, TextAnchor textAlignment = TextAnchor.MiddleCenter, float fadeIn = 0, float fadeOut = 0)
			{


				CuiLabel label = new CuiLabel();
				label.Text.Text = message;
				label.RectTransform.AnchorMin = aMin;
				label.RectTransform.AnchorMax = aMax;
				label.Text.Align = textAlignment;
				label.Text.Color = color;
				label.Text.FontSize = size;
				label.Text.FadeIn = fadeIn;
				label.FadeOut = fadeOut;

				container.Add(label, parent, panelName);

			}
			public static CuiButton CreateButton(CuiElementContainer container, string parent, string panelName, string color, string text, int size, Vector4 bounds, string command, TextAnchor align = TextAnchor.MiddleCenter, string textColor = "1 1 1 1")
			{

				container.Add(new CuiElement
				{
					Name = panelName,
					Parent = parent,
					Components = {


							new CuiButtonComponent {
								Color = color,
								Command = command,
							},

							new CuiRectTransformComponent
							{
								AnchorMin = GetMinUI(bounds),
								AnchorMax = GetMaxUI(bounds),
							}
						}
				});

				CreateOutlineLabel(container, panelName, "text", text, textColor, size, new Vector4(0, 0, 1, 1), align);

				return null;

			}


			public static CuiPanel CreatePanel(CuiElementContainer container, string parent, string panelName, string color, Vector4 bounds, string imageUrl = "", bool cursor = false, float fadeOut = 0, float fadeIn = 0, bool png = false, bool blur = false, bool outline = true)
			{

				if (!string.IsNullOrEmpty(imageUrl))
				{
					//hack to get images working
					if (png)
					{
						if (outline)
						{
							container.Add(new CuiElement
							{
								Name = panelName,
								Parent = parent,
								FadeOut = fadeOut,
								Components = {
																
								//new CuiRawImageComponent { Color = "0 0 0 0.5", Sprite = "assets/content/materials/highlight.png", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },

								new CuiRawImageComponent
								{
									Color = color,
									Png = imageUrl,
									FadeIn = fadeIn
								},
								new CuiRectTransformComponent
								{
									AnchorMin = GetMinUI(bounds),
									AnchorMax = GetMaxUI(bounds),
								},
								new CuiOutlineComponent {
									Color = "0 0 0 0.9",
									Distance = "0.7 -0.7",
								},
							}
							});
						}
						else
						{
							container.Add(new CuiElement
							{
								Name = panelName,
								Parent = parent,
								FadeOut = fadeOut,
								Components = {
																
								//new CuiRawImageComponent { Color = "0 0 0 0.5", Sprite = "assets/content/materials/highlight.png", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },

								new CuiRawImageComponent
								{
									Color = color,
									Png = imageUrl,
									FadeIn = fadeIn
								},
								new CuiRectTransformComponent
								{
									AnchorMin = GetMinUI(bounds),
									AnchorMax = GetMaxUI(bounds),
								}
							}
							});
						}


					}
					else
					{
						container.Add(new CuiElement
						{
							Name = panelName,
							Parent = parent,
							FadeOut = fadeOut,
							Components = {


								new CuiRawImageComponent
								{
									Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                    Color = color,
									Url = imageUrl,
									FadeIn = fadeIn
								},
								new CuiRectTransformComponent
								{
									AnchorMin = GetMinUI(bounds),
									AnchorMax = GetMaxUI(bounds),
								}
							}
						});
					}


					return null;

				}
				else
				{

					if (blur)
					{

						//BLURS
						//assets/content/ui/uibackgroundblur-ingamemenu.mat
						//assets/content/ui/uibackgroundblur-notice.mat
						//assets/content/ui/uibackgroundblur.mat
						// dirty bg blur, can't stretch large
						string mat = "assets/content/ui/uibackgroundblur-ingamemenu.mat";// MEDIUM BLURRY 
																						 //string mat = "assets/content/ui/uibackgroundblur.mat";//VERY BLURRY

						//string sprite = "assets/content/ui/ui.white.tga";//kind of boxy outline
						//string sprite = "assets/content/ui/ui.white.tga";//


						container.Add(new CuiElement
						{
							Name = panelName,
							Parent = parent,
							FadeOut = fadeOut,
							Components = {
									new CuiImageComponent {
										Color = color,
										Material = mat,
										FadeIn = fadeIn
									},
									new CuiRectTransformComponent
									{
										AnchorMin = GetMinUI(bounds),
										AnchorMax = GetMaxUI(bounds),
									}
								}
						});

					}
					else
					{

						CuiPanel element = new CuiPanel();
						element.RectTransform.AnchorMin = GetMinUI(bounds);
						element.RectTransform.AnchorMax = GetMaxUI(bounds);
						//element.FadeOut = 1f;
						element.Image.Color = color;
						element.CursorEnabled = cursor;
						element.Image.FadeIn = fadeIn;
						element.FadeOut = fadeOut;

						container.Add(element, parent, panelName);
						return element;

					}

					return null;

				}

			}

		}

		#endregion
	}
}
