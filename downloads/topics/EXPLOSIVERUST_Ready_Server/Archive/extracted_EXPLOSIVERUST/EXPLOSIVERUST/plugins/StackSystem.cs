using System.Linq;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins 
{ 
    [Info("StackSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")] 
	class StackSystem : RustPlugin 
	{
        private Dictionary<string, int> Stack = new Dictionary<string, int>();

        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("StackSystem/ItemList"))
                Stack = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("StackSystem/ItemList");
            DataBaseCreate();
        }

        void DataBaseCreate()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("StackSystem/ItemList"))
            {
                foreach (var item in ItemManager.itemList)
                    Stack.Add(item.shortname, item.stackable);
                Puts("Дата стаков успешно создана!");
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("StackSystem/ItemList", Stack);  
            }
            StackLoad();
        }

        void StackLoad() 
		{
            foreach (var check in Stack)
            {
                var item = ItemManager.itemList.FirstOrDefault(z => z.shortname == check.Key);
                item.stackable = check.Value;
            }
		}
    }
}