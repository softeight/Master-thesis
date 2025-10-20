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
using System.Threading.Tasks;


public class KinectServer : MonoBehaviour
{
    class PointInfo
    {
        public Vector3 position;
        public Color color;
        public bool active;
    }

    private TcpClient client;
    private NetworkStream stream;
    public string serverIp = "127.0.0.1";
    public int serverPort = 3363;

    public GameObject pointPrefab;
    public static List<GameObject> Points = new List<GameObject>();
    private List<PointInfo> pointInfos;
    
    private byte[] packet_buffer;

    public const int rows = 424;
    public const int cols = 512;
    private int bufferSize;
    private bool isReading = false;
    private object _lock = new object();

    //public Texture2D texture;


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

        //pointInfos = new List<PointInfo>(Points.Count);
        pointInfos = Enumerable.Range(0, rows * cols)
            .Select(_ => new PointInfo())
            .ToList();
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
            Debug.Log("Connected.");
        }

        if(isReading || !stream.DataAvailable)
            return;

        //get the received data according to its format/sequence
        try
        {
            isReading = true;
            stream.BeginRead(packet_buffer, 0, bufferSize, UpdatePointsCallback, null);
        }
        catch (Exception e) {
            Debug.LogException(e);
            client.Dispose();
            client = new TcpClient();
            return;
        }

        //texture = new Texture2D(512, 424, TextureFormat.RGBA32, false);
        lock (_lock)
        {
            for (int x = 0; x < cols; x++)//cols = 512           
            {
                for (int y = 0; y < rows; y++) //rows = 424
                {
                    int i = y * cols + x;
                    var point = Points[i];
                    var pointInf = pointInfos[i];
                    

                    if (!pointInf.active)
                    {
                        point.SetActive(false);
                        continue;
                    }


                    Color color = pointInf.color;
                    Vector3 pos = pointInf.position;
                    //texture.SetPixel(x, y, color);

                    point.transform.position = pos;
                    point.SetActive(true);

                    Renderer rend = point.GetComponent<Renderer>();
                    rend.material.color = color;
                }
            }
            //byte[] bytes = texture.EncodeToPNG();
            //System.IO.File.WriteAllBytes(Application.dataPath + "/output_cm.png", bytes);
        }

    }

    private void UpdatePointsCallback(IAsyncResult result)
    {
        if (!result.IsCompleted)
        {
            Debug.LogError("TCP stream read did not complete.");
            client.Dispose();
            client = new TcpClient();
            return;
        }

        lock (_lock)
        {
            int readBytes = stream.EndRead(result);
            isReading = false;

            if (readBytes != bufferSize)
            {
                Debug.LogErrorFormat("Expected {0} bytes but only received {1}.", bufferSize, readBytes);
                return;
            }
            Parallel.For(0, 512, x =>
            {
                for (int y = 0; y < 424; y++) //rows = 424
                {
                    int i = y * 512 + x;

                    byte r = packet_buffer[(i * 7)];
                    byte g = packet_buffer[(i * 7) + 1];
                    byte b = packet_buffer[(i * 7) + 2];
                    byte a = packet_buffer[(i * 7) + 3];
                    float depth = (float)BitConverter.ToUInt16(packet_buffer, (i * 7) + 4);
                    byte bodyId = packet_buffer[(i * 7) + 6];



                    if (bodyId > 5)
                    {
                        pointInfos[i].active = false;
                    }
                    else
                    {
                        pointInfos[i].active = true;
                    }
                    pointInfos[i].position = new Vector3(-0.01f * (float)x, -0.01f * (float)y, 0.001f * (float)depth);
                    pointInfos[i].color = new Color(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
                    //testpic.SetPixel(x,y,System.Drawing.Color.FromArgb(r,g,b));


                }
            });
            //for (int x = 0; x < 512; x++)//cols = 512           
            //{
                //for (int y = 0; y < 424; y++) //rows = 424
                //{
                //    int i = y * 512 + x;

                //    byte r = packet_buffer[(i * 7)];
                //    byte g = packet_buffer[(i * 7) + 1];
                //    byte b = packet_buffer[(i * 7) + 2];
                //    byte a = packet_buffer[(i * 7) + 3];
                //    float depth = (float)BitConverter.ToUInt16(packet_buffer, (i * 7) + 4);
                //    byte bodyId = packet_buffer[(i * 7) + 6];

                    
                    
                //    if (bodyId > 5)
                //    {
                //        pointInfos[i].active = false;
                //    }
                //    else
                //    {
                //        pointInfos[i].active = true;
                //    }
                //    pointInfos[i].position = new Vector3(-0.01f * (float)x, -0.01f * (float)y, 0.001f * (float)depth);
                //    pointInfos[i].color = new Color(r/255.0f, g / 255.0f, b/255.0f, a/255.0f);
                //    //testpic.SetPixel(x,y,System.Drawing.Color.FromArgb(r,g,b));

                   
                //}
           // }
            //byte[] bytes = texture.EncodeToPNG();
            //System.IO.File.WriteAllBytes(Application.dataPath + "/output.png", bytes);
            //testpic.Save(@"C:\Users\yiyu\Documents\uni\Master\test.png", System.Drawing.Imaging.ImageFormat.Png);
        }
        
    }
}
