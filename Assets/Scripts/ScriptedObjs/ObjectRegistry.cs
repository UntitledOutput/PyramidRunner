using System.Collections.Generic;
using Network;
using UnityEngine;

namespace ScriptedObjs
{
    [CreateAssetMenu(fileName = "Registry", menuName = "Pyramid/Registry", order = 0)]
    public class ObjectRegistry : ScriptableObject
    {
        public static ObjectRegistry Instance {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ObjectRegistry>("ObjectRegistry");
                }

                return _instance;
            }
            
        }
        private static ObjectRegistry _instance;

        public List<ClothingObject> Clothing;
        public List<ClothingObject> HatClothing;
        public List<NetworkBehaviour> NetworkPrefabs;
        public List<MapObject> Maps;
    }
}