using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StormUI
{
    public class Factory
    {
        public static T GetAsset<T>(string path) where T : UnityEngine.Object
        {
            if (m_bundle == null)
                m_bundle = AssetBundle.LoadFromFile("Bundles\\shared\\storm.bundle");

            return m_bundle.LoadAsset<T>(path);
        }

        private static AssetBundle m_bundle;
    }
}
