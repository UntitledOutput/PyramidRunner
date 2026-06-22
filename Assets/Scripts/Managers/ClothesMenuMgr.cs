using System.Collections.Generic;
using External;
using Managers;
using ScriptedObjs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClothesMenuMgr : MonoBehaviour
{
    public Sprite UnknownIcon;
    
    private RectTransform buttonTemplate;
    private RectTransform itemPreview;

    private int currentTab = -1;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buttonTemplate = transform.RecursiveFind<RectTransform>("ButtonTemplate");
        itemPreview = transform.RecursiveFind<RectTransform>("ItemPreview");
        OpenClothingMenu();
    }

    public void SwitchTab(int type)
    {
        OpenClothingMenu(type);
    }

    public void AttemptBuy(ClothingObject obj)
    {
        PlayerController.Instance.clothingController.SuitObject = obj;
        DataManager.Instance.CurrentSave.suitIndex = ObjectRegistry.Instance.Clothing.IndexOf(obj);
        
        PlayerController.Instance.clothingController.RecreateClothing();
        if (NetworkMgr.Instance.IsConnectedToRoom)
        {
            NetworkMgr.Instance.SendPlayerState("cosmetic_update", new Dictionary<string, object>()
            {
                
                { "suit", DataManager.Instance.CurrentSave.suitIndex },
                { "hat", DataManager.Instance.CurrentSave.hatIndex },
                { "send_to_self", true }
            });
        }

        OpenClothingMenu(currentTab);
    } 
    
    public void AttemptEquip(ClothingObject obj)
    {
        if (currentTab == 0)
        {
            PlayerController.Instance.clothingController.SuitObject = obj;
            DataManager.Instance.CurrentSave.suitIndex = ObjectRegistry.Instance.Clothing.IndexOf(obj);
        }
        else
        {
            PlayerController.Instance.clothingController.HatObject = obj;
            DataManager.Instance.CurrentSave.hatIndex = ObjectRegistry.Instance.HatClothing.IndexOf(obj);
        }

        PlayerController.Instance.clothingController.RecreateClothing();
        if (NetworkMgr.Instance.IsConnectedToRoom)
        {
            NetworkMgr.Instance.SendPlayerState("cosmetic_update", new Dictionary<string, object>()
            {
                
                { "suit", DataManager.Instance.CurrentSave.suitIndex },
                { "hat", DataManager.Instance.CurrentSave.hatIndex },
                { "send_to_self", true }
            });
        }

        OpenClothingMenu(currentTab);
    }
    
    private void HandleIconClick(ClothingObject obj)
    {
        itemPreview.Find("ItemName").GetComponent<TMP_Text>().text = obj.Name;
        itemPreview.Find("ItemDesc").GetComponent<TMP_Text>().text = "Descriptions not finished.";
        itemPreview.RecursiveFind("ItemIcon").GetComponent<Image>().sprite = obj.Icon;
        
        itemPreview.Find("BuyButton").gameObject.SetActive(false);
        itemPreview.Find("BuyButton").GetComponent<Button>().onClick.RemoveAllListeners();
        itemPreview.Find("BuyButton").GetComponent<Button>().onClick.AddListener((() => AttemptBuy(obj)));
        
        itemPreview.Find("EquipButton").gameObject.SetActive(true);
        itemPreview.Find("EquipButton").GetComponent<Button>().onClick.RemoveAllListeners();
        itemPreview.Find("EquipButton").GetComponent<Button>().onClick.AddListener((() => AttemptEquip(obj)));

    } 

    public void OpenClothingMenu(int type = 0)
    {
        buttonTemplate.parent.RemoveAllChildrenExcept("ButtonTemplate");

        currentTab = type;

        List<ClothingObject> clothingList = null;

        ClothingObject currentObj = null;

        if (currentTab == 0)
        {
            clothingList = ObjectRegistry.Instance.Clothing;
            currentObj = PlayerController.Instance.clothingController.SuitObject;
        }
        else if (currentTab == 1)
        {
            clothingList = ObjectRegistry.Instance.HatClothing;
            currentObj = PlayerController.Instance.clothingController.HatObject;
        }

        foreach (var clothingObject in clothingList)
        {
            if (clothingObject.Type != (ClothingObject.ClothingType)type)
            {
                continue;
            }
            
            var button = Instantiate(buttonTemplate, buttonTemplate.parent);
            button.gameObject.SetActive(true);
            button.GetChild(0).GetComponent<Image>().sprite = clothingObject.Icon == null ? UnknownIcon : clothingObject.Icon;
            button.GetComponent<Button>().onClick.AddListener(() => HandleIconClick(clothingObject));

            if (clothingObject == currentObj)
            {
                button.transform.Find("SelectedOverlay").gameObject.SetActive(true);
                HandleIconClick(clothingObject);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
