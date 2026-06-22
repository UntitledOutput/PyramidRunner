using System.Collections.Generic;
using System.Linq;

public static class MessagePackHelper
{
    public static Dictionary<string, object> ToStringDict(object obj)
    {
        if (obj is Dictionary<string, object> stringDict)
            return stringDict;
        
        if (obj is Dictionary<object, object> objDict)
        {
            return objDict.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            );
        }
        
        return null;
    }
    
    public static List<Dictionary<string, object>> ToStringDictList(object obj)
    {
        if (obj is List<object> list)
        {
            return list.Select(item => ToStringDict(item)).ToList();
        }
        
        return null;
    }
    
    public static List<object> ToList(object obj)
    {
        if (obj is object[] list)
        {
            return list.ToList();
        }
        
        return null;
    }
}