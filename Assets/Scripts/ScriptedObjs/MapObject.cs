using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ScriptedObjs
{
    [CreateAssetMenu(fileName = "New Map", menuName = "Pyramid/New Map", order = 0)]
    public class MapObject : ScriptableObject
    {
        public string MapId;
        
        public string DisplayName;
        public Sprite PreviewImage;

        public GameObject MapPrefab;
        public GameObject PreviewPrefab;
        public NavMeshData MapNavMeshData;
    }
}