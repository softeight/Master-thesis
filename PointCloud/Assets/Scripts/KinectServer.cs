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
using System.Diagnostics;


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

    public int header = 8;
    //public long timestamp;
    //public DateTime receivedTime;

    //public Texture2D texture;


    // Start is called before the first frame update
    void Start()
    {
        if (!Application.isPlaying)
            return;

        bufferSize = (7 * rows * cols) + header;
        packet_buffer = new byte[bufferSize];
        client = new TcpClient();

        Points = Enumerable.Range(0, rows * cols)
            .Select(i =>
            {
                var obj = Instantiate(pointPrefab, new Vector3(), Quaternion.identity, this.transform);
                obj.name = $"Point {i}";
                return obj;
            }).ToList();

        //pointInfos = new List<PointInfo>(Points.Count);
        pointInfos = Enumerable.Range(0, rows * cols)
            .Select(_ => new PointInfo())
            .ToList();

        this.StartCoroutine(this.ReadFrame());
    }

    void OnDisable()
    {
        this.StopAllCoroutines();
        client?.Dispose();

        foreach (GameObject obj in Points)
            Destroy(obj);
        Points.Clear();
    }

    // Update is called once per frame
    void Update()
    {
       
        //texture = new Texture2D(512, 424, TextureFormat.RGBA32, false);
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
              
                point.transform.position = pointInf.position;
                point.SetActive(true);

                Renderer rend = point.GetComponent<Renderer>();
                rend.material.color = pointInf.color;
            }
        }
       
        //DateTime now = DateTime.UtcNow;
        //long elapsedTicks = now.Ticks - receivedTime.Ticks;
        //TimeSpan latency = new TimeSpan(elapsedTicks);
        //UnityEngine.Debug.Log($"from receiving to points created:{latency.TotalMilliseconds}ms"); //100-200ms

    }

    private IEnumerator ReadFrame()
    {
        while (true)
        {
            if (!client.Connected)
            {
                UnityEngine.Debug.Log("Connecting...");
                client.Connect(serverIp, serverPort);
                client.NoDelay = true;
                client.ReceiveBufferSize = bufferSize;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            while (!stream.DataAvailable)
                yield return new WaitForEndOfFrame();

            //get the received data according to its format/sequence
            var result = stream.BeginRead(packet_buffer, 0, bufferSize, null, null);

            while (!result.IsCompleted)
                yield return new WaitForEndOfFrame();

            int readBytes = stream.EndRead(result);

            if (readBytes != bufferSize)
            {
                UnityEngine.Debug.LogErrorFormat("Expected {0} bytes but only received {1}.", bufferSize, readBytes);
                yield return new WaitForEndOfFrame();
            }
            else
            {
                Parallel.For(0, 512, x =>
                {
                    for (int y = 0; y < 424; y++) //rows = 424
                    {
                        int i = y * 512 + x;

                        byte r = packet_buffer[(i * 7) + header];
                        byte g = packet_buffer[(i * 7) + 1 + header];
                        byte b = packet_buffer[(i * 7) + 2 + header];
                        byte a = packet_buffer[(i * 7) + 3 + header];
                        float depth = (float)BitConverter.ToUInt16(packet_buffer, (i * 7) + 4 + header);
                        byte bodyId = packet_buffer[(i * 7) + 6 + header];

                        pointInfos[i].active = (bodyId <= 5);
                        pointInfos[i].position = new Vector3(-0.01f * (float)x, -0.01f * (float)y, 0.001f * (float)depth);
                        pointInfos[i].color = new Color(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
                    }
                });

                //timestamp = BitConverter.ToInt64(packet_buffer, 0);
                //DateTime sentTime = new DateTime(timestamp, DateTimeKind.Utc);//change sent time to ticks(long)
                //receivedTime = DateTime.UtcNow;
                //TimeSpan elapsedTicks = receivedTime - sentTime;
                //UnityEngine.Debug.Log($"receiving and writing in buffer:{elapsedTicks.TotalMilliseconds}ms");

                yield return new WaitForEndOfFrame();
            }
        }
    }
}
