using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditor.Build.Profile; // New in Unity 6


public class AutomatedBuild
{
    // This method can be called from the command line
    public static void PerformBuild()
    {
        // Set WebGL template to Default
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

        string profilePath = "Assets/Settings/Build Profiles/WebProfile.asset";
        
        BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
        if (profile == null)
        {
            throw new System.Exception($"Failed to load Build Profile at: {profilePath}");
        }
        
        var options = new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = "Builds/testnetwork03"
        };
        
        BuildPipeline.BuildPlayer(options);
    }
}
