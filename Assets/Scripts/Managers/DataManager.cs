using System;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Managers
{
    public class DataManager : MonoBehaviour
    {
        [MessagePack.MessagePackObject, Serializable]
        public class DataSave
        {
            [Key(0)] public int version = 0;
            
            [Key(1)] public string username;
            [Key(2)] public int suitIndex = 0;
            [Key(3)] public int hatIndex = 0;

            [Key(4)] public int money;
            [Key(5)] public float levelProgress;
            [Key(6)] public int level = 1;
        }
        
        public static DataManager Instance;
        public DataSave CurrentSave;

        public bool TriedLoading { get; private set; } = false;
        
        private async void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            var loaded = await Load();
            TriedLoading = true;
            
            if (!loaded)
            {
                CurrentSave = new DataSave
                {
                    username = "test_user" + Random.Range(0, 10000)
                };
            }
        }

        private async void Start()
        {

        }

        private void Update()
        {
            Instance = this;
        }

        public async Task Save()
        {
            var stream = new MemoryStream();
            
            await MessagePackSerializer.SerializeAsync(stream, CurrentSave);
            
            stream.Close();

            string data = Convert.ToBase64String(stream.ToArray());
            PlayerPrefs.SetString("data", data);
        }

        public async Task<bool> Load()
        {
            if (PlayerPrefs.HasKey("data"))
            {
                string strData = PlayerPrefs.GetString("data");
                byte[] data = Convert.FromBase64String(strData);

                var stream = new MemoryStream(data);

                CurrentSave = await MessagePackSerializer.DeserializeAsync<DataSave>(stream);
                
                stream.Close();
                return true;
            }
            else
            {
                return false;
            }
            
            
        }
    }
}