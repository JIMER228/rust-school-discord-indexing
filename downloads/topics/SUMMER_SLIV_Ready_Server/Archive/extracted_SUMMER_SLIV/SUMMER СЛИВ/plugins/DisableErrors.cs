using System;
using Oxide.Core;
using Oxide.Core.Libraries;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DisableErrors", "", "0.0.1")]
    class DisableErrors : RustPlugin
    {
        void Loaded()
        {
            var errors = Game.Rust.RustExtension.Filter.ToList();
            errors.Add("Steam gave us a OK ticket response for unconnected id"); 
            errors.Add("Steam gave us a AuthTicketCanceled ticket response for unconnected id");
            errors.Add("Server Exception: Building Manager"); 
            errors.Add("OnPlayerRespawned"); 
            errors.Add("Image failed to download! Error: Unknown Error - Image Name:"); 
            errors.Add("NullReferenceException: Object reference not set to an instance of an object.");
            errors.Add("NullReferenceException: Object reference not set to an instance of an object");
            errors.Add("Steam gave us a AuthTicketInvalidAlreadyUsed ticket response for unconnected id");
            errors.Add("Calling hook OnLootSpawn resulted in a conflict between the following plugins");
            errors.Add("NullReferenceException");
            Game.Rust.RustExtension.Filter = errors.ToArray();

        }
    }
}