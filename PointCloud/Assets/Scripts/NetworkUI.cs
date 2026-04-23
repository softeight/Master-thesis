using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    public ushort port = 3363;
    public string ipAddress = "134.109.192.10";

    private void Start()
    {
        var cfg = ConfigLoader.Load();
        port = cfg.voiceHostPort;
        ipAddress = cfg.voiceHostIp;


    }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            GUI.Label(new Rect(20, 20, 300, 30), "No NetworkManager found.");
            return;
        }

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            GUI.Label(new Rect(20, 20, 100, 30), "IP:");
            ipAddress = GUI.TextField(new Rect(60, 20, 160, 25), ipAddress);

            GUI.Label(new Rect(230, 20, 50, 30), "Port:");
            string portString = GUI.TextField(new Rect(270, 20, 80, 25), port.ToString());
            ushort.TryParse(portString, out port);

            if (GUI.Button(new Rect(20, 60, 100, 40), "Start Host"))
            {
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData("0.0.0.0", port);
                }

                bool ok = NetworkManager.Singleton.StartHost();
                Debug.Log("StartHost: " + ok);
            }

            if (GUI.Button(new Rect(140, 60, 100, 40), "Start Client"))
            {
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData(ipAddress, port);
                }

                bool ok = NetworkManager.Singleton.StartClient();
                Debug.Log("StartClient: " + ok);
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "HOST" :
                          NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";

            GUI.Label(new Rect(20, 20, 300, 30), "Running as: " + mode);

            if (GUI.Button(new Rect(20, 60, 100, 40), "Shutdown"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }
}

