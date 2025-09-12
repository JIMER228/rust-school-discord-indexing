// Requires: ZoneManager



using Oxide.Core.Plugins;
using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("No Safe Zone Sleepers", "NooBlet", "1.3")]
    [Description("Automaticly Creates a Zone to remove sleeping players from Outpost and Bandit Camp")]
    public class NoSafeZoneSleepers : CovalencePlugin
    {
        List<string> createdZones = new List<string>();
        int number = 0;


        #region Hooks

        [PluginReference]
        private Plugin ZoneManager;

        #region Config

        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file");
            Config["OupostZoneRadius"] = 150;
            Config["BanditCampZoneRadius"] = 150;
            Config["Use Enter and Leave Messages?"] = false;
            Config["Enter Message"] = "You are now entering a No Sleep Zone";
            Config["Leave Message"] = "You are now Leaving a No Sleep Zone";
        }



        #endregion config

        private void OnServerInitialized()
        {
            LoadDefaultConfig();
            AddZones(); 
        }

        void Unload()
        {
            ClearZones();
        }

        #endregion Hooks

      

        #region Methods

        private void ClearZones()
        {
            if (createdZones != null)
            {
                foreach (string zone in createdZones)
                {
                    ZoneManager?.Call("EraseZone", zone);
                    Puts($"{zone} has been Removed");
                }
            }
           
        }

        private void AddZones()
        {
           

            foreach (var current in TerrainMeta.Path.Monuments)
            {
                if (current.name == "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab" || current.name.Contains("assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab"))
                {
                    string[] messages = new string[6];
                    string name = "";
                    if ((bool)Config["Use Enter and Leave Messages?"])
                    {
                         messages = new string[10];
                    }
                    else
                    {
                         messages = new string[6];
                    }

                    if (current.displayPhrase.english.StartsWith("Bandit"))
                    {
                        name = "BanditCamp";
                    }
                    else
                    {
                        name = "OutPost";
                    }
                   
                    string zoneId = $"{name}.{number}";
                    string friendlyname = name;
                    string ID = zoneId;

                    
                    messages[0] = "name";
                    messages[1] = friendlyname;
                    messages[2] = "ejectsleepers";
                    messages[3] = "true";
                    messages[4] = "radius";
                    if (name == "OutPost")
                    {
                        messages[5] = Config["OupostZoneRadius"].ToString();
                    }
                    else
                    {
                        messages[5] = Config["BanditCampZoneRadius"].ToString();
                    }

                    if ((bool)Config["Use Enter and Leave Messages?"])
                    {
                       
                        messages[6] = "enter_message";
                        messages[7] = Config["Enter Message"].ToString();
                        messages[8] = "leave_message";
                        messages[9] = Config["Leave Message"].ToString();
                    }

                    ZoneManager?.CallHook("CreateOrUpdateZone", ID, messages, current.transform.position);
                    number++;
                    createdZones.Add(zoneId);
                    Puts($"{ID} has been created");
                }
            }
        }

      

        #endregion Methods
    }
}