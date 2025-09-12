# Есть у кого-то TPCHAT на 261 dev?

**Всего сообщений:** 7

---

## 💬 𓆩♡𓆪 Ямори Ко 𓆩♡𓆪 
**Время:** 12.06.2025, 14:01:43

У меня не работает плагн, мб что с ним, раньше работал может шарит кто-то что не так?

**Вложения:**
- 📎 image.png
- 📎 TPChat.cs

---

## 💬 h3ds 
**Время:** 12.06.2025, 14:13:52

cursor пофиксит

---

## 💬 𓆩♡𓆪 Ямори Ко 𓆩♡𓆪 
**Время:** 12.06.2025, 14:17:07

Не горю желанием с ним работать, мб может быть кто-то скинут уже норм версию

---

## 💬 raighenlovee1 
**Время:** 12.06.2025, 14:30:16

нихуя ты ахуел

---

## 💬 𓆩♡𓆪 Ямори Ко 𓆩♡𓆪 
**Время:** 12.06.2025, 14:31:03

)

---

## 💬 Waymall 
**Время:** 12.06.2025, 15:42:00

private void OnServerInitialized()
        {
            _ = this;
            Handler = DataBase.LoadData();
             
            BasePlayer.activePlayerList.ToList().ForEach((player) =>
            {
                if (!Handler.Settingses.ContainsKey(player.userID))
                    Handler.Settingses.Add(player.userID, Settings.Generate());
            });  
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Logs"))
                MessagesLogs = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Message>>($"Logs");
            if (MessagesLogs.Count > 1000)
            {
                MessagesLogs.Clear();
                PrintError("Data messages was cleared!"); 
            } 

            permission.RegisterPermission("TPChat.mute", this);
              
            Settingses.Colors.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Types.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Sizes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Prefixes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});

            timer.Every(Settingses.BroadcastInterval, () =>
            {
                var message = Settingses.Broadcaster.GetRandom();
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    var settings = Handler.Settingses[check.userID];
                    if (!settings.Chatters.Tips)
                        continue;

                    check.SendConsoleCommand("chat.add", 0, Settingses.ImageID, message);
                    check.SendConsoleCommand($"echo [<color=white>ЧАТ</color>] {message}");
                }
            }).Callback();
            timer.Every(10, AntiSpamFilter.Clear);
            timer.Every(60, Handler.SaveData);
            timer.Every(300, () => BasePlayer.activePlayerList.ToList().ForEach(p => FetchStatus(p)));    
        }
эту часть закинь чатугпт и он те пофиксит

---

## 💬 𓆩♡𓆪 Ямори Ко 𓆩♡𓆪 
**Время:** 12.06.2025, 18:04:53

Спасибо тебе добрый человек

---

