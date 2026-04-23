using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

[System.Serializable]
public class NetworkConfig 
{
    public string ip = "134.109.192.80";
    public ushort port = 3363;
    public string voiceHostIp = "134.109.192.10";
    public ushort voiceHostPort = 7777;

}

public static class ConfigLoader 
{
    public static NetworkConfig Load() 
    {
        string path = Path.Combine(Application.dataPath, "../network_config.json");

        if (File.Exists(path)) 
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<NetworkConfig>(json);
        }

        Debug.LogWarning("Config file not found, using default value");
        return new NetworkConfig();
    }
}