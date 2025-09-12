# Запрет посадки семян ткани

**Всего сообщений:** 20

---

## 💬 𐌓𐌄Ꮤ𐌊𐌀 
**Время:** 07.01.2025, 16:00:46

Есть ли такой плагин который запрещает сажать семена ткани на земле? Если да скиньте пожалуйста

---

## 💬 ! cheese 
**Время:** 07.01.2025, 16:06:56

вырежи семена с сервера

---

## 💬 𐌓𐌄Ꮤ𐌊𐌀 
**Время:** 07.01.2025, 20:29:06

В грядки сажать можно а на землю нельзя, так что не вариант

---

## 💬 Darkwing Duck 
**Время:** 07.01.2025, 23:34:55

Are you looking for a plugin that only prevents them in the ground, but allows them in the planters?

---

## 💬 ! v///nokurovv.extended 
**Время:** 08.01.2025, 02:04:48

ну на попробуй, может и работает, написал за 2 минутки

**Вложения:**
- 📎 NoPlantingSeeds.cs

---

## 💬 𐌓𐌄Ꮤ𐌊𐌀 
**Время:** 08.01.2025, 14:28:47

Не работает через фикс не фиксится)

---

## 💬 ! v///nokurovv.extended 
**Время:** 08.01.2025, 14:38:29

ну ща тогда так уж и быть перепишу, я пока занят немного, минут через 20 сделаю

---

## 💬 ! cheese 
**Время:** 08.01.2025, 14:39:04

джуниор си шарп девелопер занят, нужно подождать

---

## 💬 ! v///nokurovv.extended 
**Время:** 08.01.2025, 15:11:45

я не блядский долбаеб на "дизайнере" который трясется за 200 рублей

---

## 💬 ! v///nokurovv.extended 
**Время:** 08.01.2025, 15:11:52

гетай хуйца моего сынок

---

## 💬 ! cheese 
**Время:** 08.01.2025, 16:10:00

**Вложения:**
- 📎 image.png

---

## 💬 ! cheese 
**Время:** 08.01.2025, 16:10:04

🙂

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:11:10

Вот AI вам плагин пофиксил
```cs
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Planting Seeds", "RustGPT", "1.0.0")]
    class NoPlantingSeeds : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is global::BaseEntity)
            {
                var plant = entity as global::BaseEntity;
                if (plant != null)
                {
                    if (plant is global::GrowableEntity)
                    {
                        var growable = plant as global::GrowableEntity;
                        if (growable.State == PlantProperties.State.Seedling)
                        {
                            growable.Kill();
                        }
                    }
                }                               
            }
        }
    }
}
```

---

## 💬 𐌓𐌄Ꮤ𐌊𐌀 
**Время:** 08.01.2025, 16:15:21

Не работает )

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:15:30

что не работает

---

## 💬 𐌓𐌄Ꮤ𐌊𐌀 
**Время:** 08.01.2025, 16:15:38

То что ты скинул)

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:15:49

ну сядь с ии и сделай тогда
Попроси его логи в код добавить

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:16:01

и давай ему логи пока не выйдет нужный тебе результат

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:16:22

https://www.youtube.com/watch?v=ZOBhYGuzt7w&t

---

## 💬 Skuli Dropek 
**Время:** 08.01.2025, 16:16:24

Вот тебе видео

---

