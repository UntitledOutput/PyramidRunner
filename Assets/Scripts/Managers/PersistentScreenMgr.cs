using External;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PersistentScreenMgr : MonoBehaviour
{
    RoomScreenMgr _roomMenu;
    HomeMgr _homeMgr;

    private RectTransform _dataContainer;
    private TMP_Text _dataMoneyText, _dataLevelValue, _dataLevelPoints;
    private Slider _dataLevelSlider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _roomMenu = FindAnyObjectByType<RoomScreenMgr>(FindObjectsInactive.Include);
        _homeMgr = FindAnyObjectByType<HomeMgr>();
        
        _dataContainer = transform.Find("DataContainer") as RectTransform;
        
        _dataMoneyText = _dataContainer.RecursiveFind<TMP_Text>("MoneyValue");
        _dataLevelValue = _dataContainer.RecursiveFind<TMP_Text>("LevelText");
        _dataLevelPoints = _dataContainer.RecursiveFind<TMP_Text>("LevelValue");
        _dataLevelSlider = _dataContainer.RecursiveFind<Slider>("LevelSlider");


    }

    // Update is called once per frame
    void Update()
    {
        if (_roomMenu != null)
        {
            _dataContainer.gameObject.SetActive(!_roomMenu.ShowMatchInfoMenu && !_homeMgr.OnlineMenu.activeSelf && !_homeMgr.OutfitMenu.activeSelf);
        }
        
        _dataMoneyText.text = DataManager.Instance.CurrentSave.money.ToString("000000");
        _dataLevelValue.text = "Lv. " + DataManager.Instance.CurrentSave.level;
        _dataLevelPoints.text = DataManager.Instance.CurrentSave.levelProgress * 1000 + "/1000";
        _dataLevelSlider.value = DataManager.Instance.CurrentSave.levelProgress;
    }
}
