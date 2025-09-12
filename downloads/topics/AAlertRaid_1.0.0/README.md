# AAlertRaid 1.0.0

**Всего сообщений:** 37

---

## 💬 Skuli Dropek 
**Время:** 21.11.2024, 22:47:18

https://codefling.com/plugins/aalertraid

**Вложения:**
- 📎 AAlertRaid.cs

---

## 💬 Orizonov 
**Время:** 22.11.2024, 16:07:26

Не работает он к сожалению.

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 16:19:30

Что пишет?

---

## 💬 Orizonov 
**Время:** 22.11.2024, 17:01:19

NullReferenceException: Object reference not set to an instance of an object

---

## 💬 Orizonov 
**Время:** 22.11.2024, 17:02:00

Он вроде как работает, но по команде /raid - неизвестная команда.
Если релодаешь плагин, выгружает и загружает, но выдает эту надпись

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 17:15:24

А фул логи можешь скинуть?

---

## 💬 Orizonov 
**Время:** 22.11.2024, 18:10:15

хм


NullReferenceException: Object reference not set to an instance of an object
  at Oxide.Plugins.AAlertRaid.ALERTPLAYER (System.UInt64 ID, System.String name, System.String quad, System.String connect, System.String destroy, System.String attackerid) [0x00366] in <c975a88621184438acaa4dd31ba2ec83>:0 
  at Oxide.Plugins.AAlertRaid+<Alerting>d__88.MoveNext () [0x00236] in <c975a88621184438acaa4dd31ba2ec83>:0 
  at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00026] in <470ec865e9cd405cbc45cdbc22bb3c0c>:0

---

## 💬 FuN¢¥       0x86 
**Время:** 22.11.2024, 19:04:10

Это у всех так, хз что вы там наломали, у меня стоит этот плагин и работает шикарно

---

## 💬 FuN¢¥       0x86 
**Время:** 22.11.2024, 19:04:46

Мне много типов пишут, что не работает, мб всё дело в discordExt.dll

---

## 💬 Orizonov 
**Время:** 22.11.2024, 19:31:30

ну если старый discord dll то может и работает

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 22:04:26

Прямо тот которым я тут поделился ?

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 22:06:29

<@238921094201999361> Это прямо при запуске плагина случается?

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 22:19:54

**Вложения:**
- 📎 AAlertRaid.cs

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 22:19:57

ПРоверите как ворк ?

---

## 💬 Skuli Dropek 
**Время:** 22.11.2024, 22:19:59

<@238921094201999361>

---

## 💬 Orizonov 
**Время:** 22.11.2024, 23:57:17

Завтра отпишусь

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 03:48:26

У меня робит

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 03:48:38

И мой у меня работает

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 03:48:59

А вот если кому-то кидаю его, то не работает

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 03:49:12

Руки из жопы мб растут

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 03:49:38

Консолька жалуется у них на дискорд чё-то там

---

## 💬 Raxtruzze 
**Время:** 23.11.2024, 03:52:11

Это называется алкад

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 11:48:35

У моего клиента своя машина, у него не работает тоже

---

## 💬 Orizonov 
**Время:** 23.11.2024, 12:22:25

Вроде заработало, ty

---

## 💬 Skuli Dropek 
**Время:** 23.11.2024, 12:35:42

А что пишет ?

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 13:32:41

Loaded plugin AAlertRaidEN v1.0.1 by FuNcy
Failed to call hook 'OnEntityDeath' on plugin 'AAlertRaidEN v1.0.1' (NullReferenceException: Object reference not set to an instance of an object)
  at Oxide.Plugins.ExtensionMethods.Contains[T] (T[] array, T value) [0x00029] in <beb2b64691c64e2b95b99491bd85442c>:0 
  at Oxide.Plugins.AAlertRaidEN.OnEntityDeath (BaseCombatEntity entity, HitInfo info) [0x00090] in <29867963668f4d0984b4ef19c311688f>:0 
  at Oxide.Plugins.AAlertRaidEN.DirectCallHook (System.String name, System.Object& ret, System.Object[] args) [0x002d0] in <29867963668f4d0984b4ef19c311688f>:0 
  at Oxide.Plugins.CSharpPlugin.InvokeMethod (Oxide.Core.Plugins.HookMethod method, System.Object[] args) [0x00079] in <206a0f2c6ee141f38e2ad549cde44d70>:0 
  at Oxide.Core.Plugins.CSPlugin.OnCallHook (System.String name, System.Object[] args) [0x000de] in <beb2b64691c64e2b95b99491bd85442c>:0 
  at Oxide.Core.Plugins.Plugin.CallHook (System.String hook, System.Object[] args) [0x00060] in <beb2b64691c64e2b95b99491bd85442c>:0 

но прикол в том, что у меня он работает исправно

---

## 💬 Skuli Dropek 
**Время:** 23.11.2024, 14:17:16

Возможно у него какие-то старые конфиги стоят?

---

## 💬 Skuli Dropek 
**Время:** 23.11.2024, 15:28:29

А что за AAlertRaidEN  ?
Он же называется без EN

---

## 💬 Orizonov 
**Время:** 23.11.2024, 16:13:02

ну типо 2 разных плагина, ру версия и en версия

---

## 💬 Skuli Dropek 
**Время:** 23.11.2024, 16:13:29

так я не давал таких
Я давал только с название AAlertRaid

---

## 💬 Orizonov 
**Время:** 23.11.2024, 16:14:05

Ну ты да, а они возможно другой плагин юзают

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 16:33:15

Другой юзаем

---

## 💬 FuN¢¥       0x86 
**Время:** 23.11.2024, 16:33:26

Твой тоже работает у меня

---

## 💬 Dopler 
**Время:** 25.01.2025, 02:44:44

Error while compiling AAlertRaid: The type or namespace name 'Discord' does not exist in the namespace 'Oxide.Ext' (are you missing an assembly reference?) | Line: 10, Pos: 17

---

## 💬 Skuli Dropek 
**Время:** 25.01.2025, 11:59:12

Поставь расширение дискорда

---

## 💬 Dopler 
**Время:** 25.01.2025, 11:59:45

пасиб

---

## 💬 Translator 
**Время:** 31.01.2025, 23:59:00

---

