using System;
using System.Collections.Generic;
using System.Linq;
using ScriptedObjs;
using UnityEngine;

namespace Network
{
using UnityEngine;
using System.Collections.Generic;

public static class NetworkHelper
{
    /// <summary>
    /// Convert Vector3 to serializable dictionary
    /// </summary>
    public static Dictionary<string, object> SerializeVector3(Vector3 vec)
    {
        return new Dictionary<string, object>
        {
            { "x", vec.x },
            { "y", vec.y },
            { "z", vec.z }
        };
    }
    
    /// <summary>
    /// Convert dictionary back to Vector3
    /// </summary>
    public static Vector3 DeserializeVector3(Dictionary<string, object> data)
    {
        return new Vector3(
            Convert.ToSingle(data["x"]),
            Convert.ToSingle(data["y"]),
            Convert.ToSingle(data["z"])
        );
    }
    
    /// <summary>
    /// Convert Vector3 to list (more compact)
    /// </summary>
    public static List<float> Vector3ToList(Vector3 vec)
    {
        return new List<float> { vec.x, vec.y, vec.z };
    }
    
    /// <summary>
    /// Convert list back to Vector3
    /// </summary>
    public static Vector3 ListToVector3(List<object> list)
    {
        return new Vector3(
            Convert.ToSingle(list[0]),
            Convert.ToSingle(list[1]),
            Convert.ToSingle(list[2])
        );
    }
    
    /// <summary>
    /// Convert Quaternion to serializable format
    /// </summary>
    public static Dictionary<string, object> SerializeQuaternion(Quaternion quat)
    {
        return new Dictionary<string, object>
        {
            { "x", quat.x },
            { "y", quat.y },
            { "z", quat.z },
            { "w", quat.w }
        };
    }
    
    /// <summary>
    /// Sanitize data dictionary - convert Unity types to primitives
    /// </summary>
    public static Dictionary<string, object> SanitizeData(Dictionary<string, object> data)
    {
        var sanitized = new Dictionary<string, object>();
        
        foreach (var kvp in data)
        {
            if (kvp.Value is Vector3 vec3)
            {
                sanitized[kvp.Key] = Vector3ToList(vec3);
            }
            else if (kvp.Value is Vector2 vec2)
            {
                sanitized[kvp.Key] = new List<float> { vec2.x, vec2.y };
            }
            else if (kvp.Value is Quaternion quat)
            {
                sanitized[kvp.Key] = new List<float> { quat.x, quat.y, quat.z, quat.w };
            }
            else if (kvp.Value is Color color)
            {
                sanitized[kvp.Key] = new List<float> { color.r, color.g, color.b, color.a };
            }
            else
            {
                sanitized[kvp.Key] = kvp.Value;
            }
        }
        
        return sanitized;
    }
    
    public static int GetUnixTimeSeconds()
    {
        DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int currentEpochTime = (int)(DateTime.UtcNow - epochStart).TotalSeconds;
        return currentEpochTime;
    }
}
}

public class MatchData
{
    public MapObject Map;
    public int Difficulty;
    public bool Won;

    public int TasksCompleted;
    public int TasksToComplete;

    public int PointsWon;

    public static MatchData FromMpack(Dictionary<string, object> matchDataDict)
    {
        var matchData = new MatchData();
                
        Debug.Log(matchDataDict["map"].ToString());
        
        matchData.Map = ObjectRegistry.Instance.Maps.Find(o => o.MapId == matchDataDict["map"].ToString());
        matchData.Won = (bool)matchDataDict["won"];
        matchData.Difficulty = 0;
        matchData.TasksCompleted = int.Parse(matchDataDict["tasksCompleted"].ToString());
        matchData.TasksToComplete = int.Parse(matchDataDict["tasksToComplete"].ToString());
        matchData.PointsWon = int.Parse(matchDataDict["points_won"].ToString());
        
        return matchData;
    }
}