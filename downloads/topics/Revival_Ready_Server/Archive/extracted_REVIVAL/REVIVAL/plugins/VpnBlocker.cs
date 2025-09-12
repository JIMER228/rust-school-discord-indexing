using System;
using System.Collections.Generic;
using Network;
using Oxide.Core;
using Newtonsoft.Json.Linq;

//	Author info:
//   * NameTAG
//   * https://vk.com/kbmisha

namespace Oxide.Plugins
{
    [Info("VPNblocker", "Developer x SwUn ", "1.0.0")]
    class VpnBlocker : RustPlugin
    {
        private Dictionary<string,MainClass> Connections = new Dictionary<string, MainClass>();
        private string Ip;
        private int reCheck;
        private string xKey;
        private string gDomain;
        private string gMail;

        class MainClass
        {
            public bool VPN;
            public int Connects;
        }

        void Init()
        {
            LoadData();
            LoadDefaultConfig();
            permission.RegisterPermission("vpnblocker.bypass", this);
        }
        
        void OnClientAuth(Connection connection)
        {
            if (permission.UserHasPermission(connection.userid.ToString(), "vpnblocker.bypass"))
            {
                return;
            }
            
            Ip = connection.ipaddress.Split(':')[0];

            if (Connections.ContainsKey(Ip))
            {
                if(Connections[Ip].VPN)
                {
                    ConnectionAuth.Reject(connection, "К нам нельзя с VPN, если у вас его нет - пишите в личные сообщения группы!");
                }
                else
                {
                    if (Connections[Ip].Connects >= reCheck)
                    {
                        VPNCheck(Ip, connection);
                        Connections[Ip].Connects = 0;
                    }
                    else
                    {
                        Connections[Ip].Connects += 1;
                    }
                }
            }
            else
            {
                VPNCheck(Ip, connection);
            }
        }

        private void VPNCheck(string Ip, Connection connection)
        {
            if(!Connections.ContainsKey(Ip))
                Connections.Add(Ip, new  MainClass());
            
            webrequest.EnqueueGet($"http://v2.api.iphub.info/ip/" + Ip,(code, response) => IpHub(code, response, connection), this, new Dictionary<string, string> { { "X-Key", $"{xKey}" } });
        }

        private void IpHub(int code, string response, Connection connect)
        {
            Ip = connect.ipaddress.Split(':')[0];
            Puts(response);
            if (response == null)
            {
                ConnectionAuth.Reject(connect, "Нет ответа от сервера [IPHUB]!");
                return;
            }
            JObject res = JObject.Parse(response);
            if (Convert.ToInt32((int) res["block"]) > 0)
            {
                ConnectionAuth.Reject(connect, "К нам нельзя с VPN, если у вас его нет - пишите в личные сообщения группы!");
                Connections[Ip].VPN = true;
            }
            else
            {
                webrequest.EnqueueGet($"{gDomain}/check.php?ip=" + Ip + $"&contact={gMail}&flags=m",(codes, responses) => GetIpIntel(codes, responses, connect), this);
            }
        }

        private void GetIpIntel(int codes, string responses, Connection connect)
        {
            Ip = connect.ipaddress.Split(':')[0];
            Puts(responses);
            if (responses == null)
            {
                ConnectionAuth.Reject(connect, "Нет ответа от сервера [GET.IP.INTEL]!");
                return;
            }

            if (Convert.ToDouble(responses) < 0.95)
            {
                Connections[Ip].VPN = false;
            }
            else
            {
                ConnectionAuth.Reject(connect, "К нам нельзя с VPN, если у вас его нет - пишите в личные сообщения группы!");
                Connections[Ip].VPN = true;
            }
        }
        
        #region Config

        protected override void LoadDefaultConfig()
        {
            Config["Количество подключений после которых идет перепроверка"] = reCheck = GetConfig("Количество подключений после которых идет перепроверка", 10);
            Config["[IPHUB] X-Key"] = xKey = GetConfig("[IPHUB] X-Key", "null");
            Config["[GetIpIntel] E-mail"] = gMail = GetConfig("[GetIpIntel] E-mail", "null");
            Config["[GetIpIntel] Domain"] = gDomain = GetConfig("[GetIpIntel] Domain", "http://check.getipintel.net/");
            SaveConfig();
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
        
        #region Data
		
        private void LoadData() => Connections = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, MainClass>>("VPN");
		
        void OnServerSave() => SaveData();
		
        void Unload() => SaveData();
		
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("VPN", Connections);
		
        #endregion
    }
}