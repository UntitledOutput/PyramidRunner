using System;
using External;
using Managers;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineMenuMgr : MonoBehaviour
{
    private RectTransform roomTemplate;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    private void OnEnable()
    {
        roomTemplate = transform.RecursiveFind<RectTransform>("RoomTemplate");
        RefreshOnlineMenu();
    }

    public async void CreateRoom()
    {

        FindAnyObjectByType<HomeMgr>().CloseRoom((async () =>
        {
            var room_id = await NetworkMgr.Instance.CreateRoom();
            await NetworkMgr.Instance.JoinRoom(room_id);

        }));
    }

    public async void RefreshOnlineMenu()
    {
        roomTemplate.parent.RemoveAllChildrenExcept("RoomTemplate");

        var rooms = await NetworkMgr.Instance.ListRooms();
        if (rooms.Length > 0)
        {
            foreach (var room_predict in rooms)
            {
                var room = MessagePackHelper.ToStringDict(room_predict);
                var name = room["name"].ToString();
                
                var player_count = int.Parse(room["player_count"].ToString());
                var max_players =  int.Parse(room["max_players"].ToString());
                
                var roomObj = Instantiate(roomTemplate, roomTemplate.parent);
                roomObj.Find("RoomName").GetComponent<TMP_Text>().text = name;
                roomObj.Find("RoomCount").GetComponent<TMP_Text>().text = $"{player_count}/{max_players}";
                roomObj.GetComponent<Button>().onClick.AddListener(async () =>
                {
                    FindAnyObjectByType<HomeMgr>().CloseRoom((async () =>
                    {
                        await NetworkMgr.Instance.JoinRoom(room["room_id"].ToString());
                    }));
                });
                
                roomObj.gameObject.SetActive(true);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
