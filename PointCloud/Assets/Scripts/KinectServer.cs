using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using static UnityEditor.PlayerSettings;


public class KinectServer : MonoBehaviour
{

    private TcpClient client;
    private NetworkStream stream;
    public string serverIp = "127.0.0.1";
    public int serverPort = 3363;

    public GameObject pointPrefab;
    public static List<GameObject> Points = new List<GameObject>();
    
    private byte[] packet_buffer;

    public int rows = 512;
    public int cols = 424;
    private int bufferSize;


    // Start is called before the first frame update
    void Start()
    {
        bufferSize = 7 * rows * cols;
        packet_buffer = new byte[bufferSize];
        client = new TcpClient();

        Points = Enumerable.Range(0, rows * cols)
            .Select(i => {
                var obj = Instantiate(pointPrefab, new Vector3(), Quaternion.identity, this.transform);
                obj.name = $"Point {i}";
                return obj;
            }).ToList();
    }

    void OnDisable()
    {
        client?.Dispose();

        foreach (GameObject obj in Points)
            Destroy(obj);
        Points.Clear();
    }

    // Update is called once per frame
    void Update()
    {

        if (!client.Connected)
        {
            Debug.Log("Connecting...");
            client.Connect(serverIp, serverPort);
            client.NoDelay = true;
            client.ReceiveBufferSize = bufferSize;
            stream = client.GetStream();
        }

        if(!stream.DataAvailable)
            return;

        //get the received data according to its format/sequence
        try
        {
            if (stream.Read(packet_buffer, 0, bufferSize) > 0)
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {

                        int i = y * cols + x;
                        var point = Points[i];
                        byte bodyId = packet_buffer[i * 7 + 6];
                        if (bodyId > 5)
                        {
                            point.SetActive(false);
                            continue;
                        }


                        byte r = packet_buffer[i * 7];
                        byte g = packet_buffer[i * 7 + 1];
                        byte b = packet_buffer[i * 7 + 2];
                        byte a = packet_buffer[i * 7 + 3];


                        float depth = (float)BitConverter.ToUInt16(packet_buffer, i * 7 + 4);

                        Vector3 pos = new Vector3(0.01f * (float)x, 0.01f * (float)y, 0.001f * (float)depth);
                        point.transform.position = pos;
                        point.SetActive(true);

                        //Renderer rend = newPoint.GetComponent<Renderer>();
                        //rend.material.color = new Color32(r, g, b, a);
                    }
                }
            }
        }
        catch (Exception e) {
            Debug.LogException(e);
            client.Dispose();
            client = new TcpClient();
            return;
        }
    }
}
