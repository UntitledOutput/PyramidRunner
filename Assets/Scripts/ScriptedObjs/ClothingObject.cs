using System.Collections.Generic;
using UnityEngine;

namespace ScriptedObjs
{
    [CreateAssetMenu(fileName = "New Clothing", menuName = "Pyramid/New Clothing", order = 0)]
    public class ClothingObject : ScriptableObject
    {
        public enum ClothingType : int
        {
            Suit = 0,
            Hat,
        }
        
        public string Name;
        public Sprite Icon;
        public ClothingType Type;
        
        public GameObject Prefab;
        
        public List<Material> MaterialOverrides;
    }
}