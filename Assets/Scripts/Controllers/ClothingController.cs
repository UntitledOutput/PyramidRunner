using System.Collections.Generic;
using External;
using ScriptedObjs;
using UnityEngine;

public class ClothingController : MonoBehaviour
{
    public ClothingObject SuitObject;
    public ClothingObject HatObject;

    private Dictionary<string, Transform> ClothingTransforms = new  Dictionary<string, Transform>();

    private bool _isReady = false;

    public void CalcTransforms()
    {
        var armature = transform.Find("Armature");
        var children = armature.RecursiveGetAllChildren();
            
        foreach (var child in children)
        {
            ClothingTransforms.Add(child.name, child);
        }

        _isReady = true;
    }

    public void DeriveFromPlayerInfo(Dictionary<string, object> data)
    {
        SuitObject = ObjectRegistry.Instance.Clothing[int.Parse(data["suit"].ToString())];
        HatObject = ObjectRegistry.Instance.HatClothing[int.Parse(data["hat"].ToString())];
        
        RecreateClothing();
    }
    
    private void Start()
    {
        RecreateClothing();
    }

    private Dictionary<ClothingObject, List<Transform>> ClothingObjects = new  Dictionary<ClothingObject, List<Transform>>();

    public void ConformClothing(ClothingObject clothingObj)
    {
        List<Transform> clothing = new List<Transform>();
        var obj = Instantiate(clothingObj.Prefab, transform, true);
        obj.transform.localScale  = Vector3.one;
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        clothing.Add(obj.transform);
            
        var transforms = obj.transform.Find("Armature").RecursiveGetAllChildren();
        foreach (var transform1 in transforms)
        {
            transform1.SetParent(ClothingTransforms[transform1.name], false);
            transform1.localPosition = Vector3.zero;
            transform1.localRotation = Quaternion.identity;
            transform1.localScale = Vector3.one;
            clothing.Add(transform1);
        }

        var renderers = obj.GetComponentsInChildren<Renderer>();
            
        for (var i = 0; i < clothingObj.MaterialOverrides.Count; i++)
        {
            Renderer renderer = renderers[0];
            var materials = renderer.materials;
                
            materials[i] = clothingObj.MaterialOverrides[i];

            renderer.materials = materials;
        }

        ClothingObjects[clothingObj] = clothing;
    }
    
    public void RecreateClothing()
    {
        if (!_isReady) 
            CalcTransforms();
        
        foreach (var keyValuePair in ClothingObjects)
        {
            foreach (var transform1 in keyValuePair.Value)
            {
                Destroy(transform1.gameObject);
            }
        }
        ClothingObjects.Clear();
            
        // suit
        if (SuitObject != null) {
            ConformClothing(SuitObject);
        }
        if (HatObject != null && HatObject.Prefab != null)
        {
            ConformClothing(HatObject);
        }
    }
}