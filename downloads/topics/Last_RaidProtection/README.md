# Last RaidProtection

**Всего сообщений:** 10

---

## 💬 Pitto 
**Время:** 12.07.2025, 07:34:11

https://codefling.com/plugins/raid-protection

---

## 💬 Pitto 
**Время:** 12.07.2025, 07:34:26

Does anyone have the latest version?

---

## 💬 voldemar 
**Время:** 12.07.2025, 12:22:28

есть другой обновленный переделаный пофикшеный  в этом месяце чатом гпт  простой плагин антирейда на защиту стен  дверей Данный плагин добавляет на ваш сервер новое разрешение - antiraid.use.
Благодаря нему вы можете запретить рейдить постройки игроков, которым выдано это разрешение.
Также если игрок нанесет урон зданию игрока с антирейдом, то ему выведет в чат сообщение. Его содержание можно изменить в конфигурации.
Если каким либо объектам все равно наносится урон с привилегией антирейда - то вы можете добавить shortname этих объектов самостоятельно в конфигурацию плагина. Код:
{
  "Сообщение если у игрока есть антирейд": "У этого игрока активен антирейд, его невозможно зарейдить.",
  "Shortname дополнительных объектов, чтобы на них действовал антирейд": [
    "wall.frame.cell.gate",
    "wall.frame.cell",
    "wall.window.glass.reinforced",
    "wall.window.bars.toptier",
    "floor.grill",
    "floor.triangle.grill",
    "gates.external.high.stone",
    "wall.external.high.stone",
    "gates.external.high.wood",
    "wall.external.high"
  ]
}
протестируйте ничего лишнего простой код

**Вложения:**
- 📎 AntiRaid.cs

---

## 💬 voldemar 
**Время:** 16.07.2025, 11:59:31

фикс 2 антирейд первый с багом невозможно удалить стены 😎 теперь нормальный плагин, какие предметы надо на зашиту прописать в конфиг 😎

**Вложения:**
- 📎 AntiRaid.cs

---

## 💬 Puntofila 
**Время:** 28.07.2025, 23:49:46

Держи

**Вложения:**
- 📎 RaidProtection.cs

---

## 💬 Puntofila 
**Время:** 28.07.2025, 23:50:20

Еще такой есть сам пользуюсь

---

## 💬 acacus 
**Время:** 30.07.2025, 00:45:27

"Error while compiling RaidProtection: The type or namespace name 'Horse' could not be found (are you missing a using directive or an assembly reference?) | Line: 4067, Pos: 32"

---

## 💬 Puntofila 
**Время:** 30.07.2025, 01:15:05

Fix it using <#1175286209988276304>

---

## 💬 acacus 
**Время:** 30.07.2025, 01:21:20

older version .. but working

**Вложения:**
- 📎 RaidProtection.cs

---

## 💬 acacus 
**Время:** 30.07.2025, 01:22:29

newer version but .."Error while compiling RaidProtection: The type or namespace name 'RidableHorse2' could not be found (are you missing a using directive or an assembly reference?) | Line: 4113, Pos: 32"

**Вложения:**
- 📎 RaidProtection.cs

---

