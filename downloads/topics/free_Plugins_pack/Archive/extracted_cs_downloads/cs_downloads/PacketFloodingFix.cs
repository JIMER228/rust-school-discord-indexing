using System;

namespace Oxide.Plugins
{
    [Info("Packet Flooding Fix", "Chechen", "0.1.0")]
    class PacketFloodingFix : RustPlugin
    {
        #region Settings

        const int PARAMS_MULTIPLIER = 5;

        #endregion

        #region Fields

        int _maxpacketspersecond, _maxpacketspersecond_command, _maxpacketspersecond_rpc, _maxpacketspersecond_tick, _maxpacketspersecond_world;

        #endregion

        #region Hooks

        void Init()
        {
            _maxpacketspersecond = ConVar.Server.maxpacketspersecond;
            _maxpacketspersecond_command = ConVar.Server.maxpacketspersecond_command;
            _maxpacketspersecond_rpc = ConVar.Server.maxpacketspersecond_rpc;
            _maxpacketspersecond_tick = ConVar.Server.maxpacketspersecond_tick;
            _maxpacketspersecond_world = ConVar.Server.maxpacketspersecond_world;
        }

        void OnServerInitialized()
        {
            ConVar.Server.maxpacketspersecond *= PARAMS_MULTIPLIER; //1500
            ConVar.Server.maxpacketspersecond_command *= PARAMS_MULTIPLIER; //100
            ConVar.Server.maxpacketspersecond_rpc *= PARAMS_MULTIPLIER; //200
            ConVar.Server.maxpacketspersecond_tick *= PARAMS_MULTIPLIER; //300
            ConVar.Server.maxpacketspersecond_world *= PARAMS_MULTIPLIER; //1
            //Network.Server.MaxConnectionsPerIP = 100; //5
        }

        void Unload()
        {
            ConVar.Server.maxpacketspersecond = _maxpacketspersecond;
            ConVar.Server.maxpacketspersecond_command = _maxpacketspersecond_command;
            ConVar.Server.maxpacketspersecond_rpc = _maxpacketspersecond_rpc;
            ConVar.Server.maxpacketspersecond_tick = _maxpacketspersecond_tick;
            ConVar.Server.maxpacketspersecond_world = _maxpacketspersecond_world;
            //Network.Server.MaxConnectionsPerIP = 5;
        }

        #endregion
    }
}