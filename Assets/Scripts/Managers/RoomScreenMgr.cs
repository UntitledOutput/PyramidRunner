using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using External;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RoomScreenMgr : MonoBehaviour
{

    private RectTransform _roomMenu;
    private RectTransform _usernameContainer;
    private RectTransform _startGameButton;
    private RectTransform _playerTemplate;

    
    
    private RectTransform _matchInfoMenu;
    private Image _matchMapImage;
    private TMP_Text _matchFailText, _matchWinText;
    private TMP_Text _matchName;
    
    private TMP_Text _matchMoneyText, _matchLevelValue, _matchLevelPoints;
    private Slider _matchLevelSlider;
    
    public bool ShowMatchInfoMenu = false;
    public bool ShowRoomMenu = true;

    public List<string> loadedPlayers;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _roomMenu = transform.RecursiveFind<RectTransform>("RoomMenu");
        _playerTemplate = _roomMenu.RecursiveFind<RectTransform>("UsernameTemplate");
        _usernameContainer = _roomMenu.RecursiveFind<RectTransform>("UsernameContainer");
        _startGameButton = _roomMenu.RecursiveFind<RectTransform>("StartGameButton");
        
        
        _matchInfoMenu = transform.RecursiveFind<RectTransform>("MatchInfoMenu");
        _matchMapImage = _matchInfoMenu.RecursiveFind<Image>("MapImage");
        _matchFailText  = _matchInfoMenu.RecursiveFind<TMP_Text>("FailText");
        _matchWinText = _matchInfoMenu.RecursiveFind<TMP_Text>("WinText");
        _matchName = _matchInfoMenu.RecursiveFind<TMP_Text>("MapName");
        
        _matchMoneyText = _matchInfoMenu.RecursiveFind<TMP_Text>("MoneyValue");
        _matchLevelValue = _matchInfoMenu.RecursiveFind<TMP_Text>("LevelText");
        _matchLevelPoints = _matchInfoMenu.RecursiveFind<TMP_Text>("LevelValue");
        _matchLevelSlider = _matchInfoMenu.RecursiveFind<Slider>("LevelSlider");

        _matchMoneyText.text = DataManager.Instance.CurrentSave.money.ToString("000000");
        _matchLevelValue.text = "Lv. " + DataManager.Instance.CurrentSave.level;
        _matchLevelPoints.text = DataManager.Instance.CurrentSave.levelProgress * 1000 + "/1000";
        _matchLevelSlider.value = DataManager.Instance.CurrentSave.levelProgress;

        if (NetworkMgr.Instance.IsConnectedToRoom)
        {
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_startGameButton.gameObject.activeSelf != NetworkMgr.Instance.IsRoomHost)
        {
            _startGameButton.gameObject.SetActive(NetworkMgr.Instance.IsRoomHost);
        }
        
        if (_matchInfoMenu.gameObject.activeSelf != ShowMatchInfoMenu)
        {
            _matchInfoMenu.gameObject.SetActive(ShowMatchInfoMenu);
        }
        
        if (_roomMenu.gameObject.activeSelf != (ShowRoomMenu &&  NetworkMgr.Instance.IsConnectedToRoom))
        {
            _roomMenu.gameObject.SetActive(ShowRoomMenu &&  NetworkMgr.Instance.IsConnectedToRoom);
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame && NetworkMgr.Instance.IsRoomHost)
        {
            StartGame();
        }
    }

    public async void OnPlayerJoined(string playerId)
    {
        loadedPlayers.Add(playerId);
        
        await UniTask.Delay(50,ignoreTimeScale:true);
        
        var player_info = await NetworkMgr.Instance.GetPlayerInfo(playerId);
        
        var plr = Instantiate(_playerTemplate,_playerTemplate.transform.parent);
        plr.name = playerId;
        plr.gameObject.SetActive(true);
        
        await OnPlayerCosmeticChange(playerId);
        
        plr.Find("Username").GetComponent<TMP_Text>().text = player_info["username"].ToString();

        
        
    }

    public async Task OnPlayerCosmeticChange(string playerId)
    {
        var slot = _playerTemplate.transform.parent.Find(playerId);
        
        var player_info = await NetworkMgr.Instance.GetPlayerInfo(playerId);

        var cap = Instantiate(Resources.Load<GameObject>("PlayerCaptureObject"));
        var cam = cap.GetComponentInChildren<Camera>();
        
        cap.GetComponentInChildren<ClothingController>().DeriveFromPlayerInfo(player_info);
        
        cam.Render();
        RenderTexture.active = cam.targetTexture;
        Texture2D screenShot = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGBA32, false, cam.targetTexture.isDataSRGB);
        // Read the pixels from the active render texture
        screenShot.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
        screenShot.Apply();
        RenderTexture.active = null; // Revert the active render texture
        
        

        
        Destroy(cap);


        if (slot)
        {
            slot.Find("Icon").GetComponent<Image>().sprite = Sprite.Create(screenShot,
                new Rect(0, 0, screenShot.width, screenShot.height), Vector2.zero);
        }
    }
    
    public async void OnPlayerLeft(string playerId)
    {
       Destroy( _playerTemplate.transform.parent.Find(playerId).gameObject);
       loadedPlayers.Remove(playerId);
    }

    public async void ShowOnlineMenu(MatchData match)
    {
        ShowMatchInfoMenu = true;
        ShowRoomMenu = false;
        _matchMapImage.sprite = match.Map.PreviewImage;
        _matchName.text = match.Map.DisplayName;
        _matchFailText.gameObject.SetActive(!match.Won);
        _matchWinText.gameObject.SetActive(match.Won);

        bool finished = false;
        IEnumerator MatchFinishCoroutine()
        {
            {
                int moneyAdd = (int)(match.PointsWon * 3.3f);
                int temp = DataManager.Instance.CurrentSave.money;
                int target = DataManager.Instance.CurrentSave.money + moneyAdd;

                float maxTime = 3 * (Time.fixedDeltaTime * 1000);
                int itr = (int)(moneyAdd / maxTime);

                while (temp < target)
                {
                    temp += itr;
                    _matchMoneyText.text = temp.ToString("000000");

                    yield return new WaitForFixedUpdate();
                }

                DataManager.Instance.CurrentSave.money = target;
            }

            {

                int levelMax = 1000;
                int levelAdd = (int)(match.PointsWon * 3.3f);
                int levelProg = (int)(DataManager.Instance.CurrentSave.levelProgress * levelMax);

                int temp = levelProg;
                int tempLvl = DataManager.Instance.CurrentSave.level;
                int target = levelProg + levelAdd;

                float maxTime = 3 * (Time.fixedDeltaTime * 1000);
                int itr = (int)(levelAdd / maxTime);

                while (temp < target)
                {
                    temp += itr;
                    _matchLevelPoints.text = temp + "/"+levelMax;
                    _matchLevelSlider.value = (float)temp / levelMax;
                    _matchLevelValue.text = "Lv. "+tempLvl;

                    if (temp >= levelMax)
                    {
                        target -= temp;
                        temp = 0;
                        
                        // change to next level max
                        levelMax = 1000;
                        tempLvl++;
                    }

                    yield return new WaitForFixedUpdate();
                }

                DataManager.Instance.CurrentSave.levelProgress = (float)target / levelMax;
                DataManager.Instance.CurrentSave.level = tempLvl;
            }

            finished = true;

        }

        StartCoroutine(MatchFinishCoroutine());
        
        await UniTask.WaitUntil(() => finished);
        await UniTask.Delay(1000 * 5);
        ShowMatchInfoMenu = false;
        ShowRoomMenu = true;
        await DataManager.Instance.Save();
    }

    public async void StartGame()
    {
        await NetworkMgr.Instance.StartGame();
    }
}
