using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Grid", "fermens", "0.0.1")]
    [Description("На костылях")]
    public class Grid : RustPlugin
    {
        Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        const float calgon = 0.0066666666666667f;
        void CreateSpawnGrid()
        {
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (calgon * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos)
        {
            return Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        }

        private void OnServerInitialized()
        {
            CreateSpawnGrid();
        }
    }
}