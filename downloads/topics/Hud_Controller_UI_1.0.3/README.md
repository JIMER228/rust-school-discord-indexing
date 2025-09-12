# Hud Controller UI 1.0.3

**Всего сообщений:** 7

---

## 💬 FreePlugins 
**Время:** 02.01.2025, 21:12:13

https://codefling.com/plugins/hud-controller-ui

**Вложения:**
- 📎 HudController.cs

---

## 💬 s463nnntr6 
**Время:** 02.01.2025, 21:13:09

<@1324360929093357621> AdminMenu залей

---

## 💬 Fallov 
**Время:** 14.05.2025, 05:31:53

void StartUpdateUI() => timer.Every(3, UpdateAllUI);
        void StartUpdateServerInfo() => timer.Every(30, GetServerInfo);

        void UpdateAllUI()
        {
            var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (!_oldEvents.SequenceEqual(_events) || currentTime - 30 > lastUpdated)
            {
                GetServerInfo();

                foreach (var player in BasePlayer.activePlayerList)
                {
                    ServerMgr.Instance.StartCoroutine(CreateMainUI(player, false));
                }

                lastUpdated = currentTime;
                _oldEvents = new Dictionary<string, bool>(_events);
            }
        }

нечего не смущает?

---

## 💬 $uicideboy$ 
**Время:** 14.05.2025, 07:17:22

чуть чуть не оптимизирован и что

---

## 💬 Fallov 
**Время:** 17.05.2025, 05:48:08

прям чуточку)))) допилевать нужно

---

## 💬 Skuli Dropek 
**Время:** 23.05.2025, 18:01:17

<#1175286209988276304> можно юзать и допилить что угодно

---

## 💬 Skuli Dropek 
**Время:** 23.05.2025, 18:01:20

или с нуля написать

---

