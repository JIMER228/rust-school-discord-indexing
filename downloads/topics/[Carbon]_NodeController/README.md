# [Carbon] NodeController

**Всего сообщений:** 3

---

## 💬 Black_Wolf 
**Время:** 22.03.2025, 16:35:14

Controlls nodes, collectables, forests spawn rates and distances control to spawn .Uses [Carbon] TimeAPI and works with [Carbon]SmartFPS

---

## 💬 Black_Wolf 
**Время:** 22.03.2025, 23:16:26

**Вложения:**
- 📎 NodeController.cs

---

## 💬 Black_Wolf 
**Время:** 22.03.2025, 23:19:00

Checks every 300 seconds (5 minutes)
Removes 20% of excess nodes when limits are exceeded
These values are good defaults because:
Resource nodes (metal, sulfur) have larger minimum distances to prevent clustering
Trees and collectibles have smaller distances since they're meant to be more common
The maximum limits prevent server lag from too many nodes
The cleanup interval and percentage are balanced to maintain server performance
You can use these as-is, or adjust them in the config file if you want different values for your server.

---

