using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PointCloudRenderer : MonoBehaviour
{
    // struct for Informations of each Point
    class PointInfo
    {
        public Vector3 position;
        public Color color;
        public bool active;
    }
    private List<PointInfo> pointInfos;

    // variables for TCP client
    private TcpClient client;
    private NetworkStream stream;
    public string serverIp = "127.0.0.1";
    public int serverPort = 3363;

    // variables for packet buffer
    private byte[] packet_buffer;
    public const int rows = 424;
    public const int cols = 512;
    private int bufferSize;
    public int header = 8;
    
    // variables for Particle system
    ParticleSystem system;
    ParticleSystem.Particle[] points;
    //bool pointsActivated = false;

    public float pointScale = 0.1f;




    // Start is called before the first frame update
    void Start()
    {
        system = GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        system.Play();

        //initialize packet buffer and tcp client
        bufferSize = (7 * rows * cols) + header;
        packet_buffer = new byte[bufferSize];
        client = new TcpClient();

        pointInfos = Enumerable.Range(0, rows * cols)
          .Select(_ => new PointInfo())
          .ToList();

        points = new ParticleSystem.Particle[rows * cols];

        this.StartCoroutine(this.ReadFrame());

    }


    // Update is called once per frame
    void Update()
    {

        //if (!pointsActivated) return;
        if (points == null || pointInfos == null) return;

        int validCount = 0;

        for (int x = 0; x < cols; x++)//cols = 512           
        {
            for (int y = 0; y < rows; y++) //rows = 424
            {
                int i = y * cols + x;

                if (!pointInfos[i].active) continue;

                points[validCount].position = pointInfos[i].position;
                points[validCount].startColor = pointInfos[i].color;
                points[validCount].startSize = pointScale;
                validCount++;

            }
        }

        system.SetParticles(points, validCount);
        Debug.Log($"particle counts: {system.particleCount}");

        //Debug.Log($"Rendering {validCount} particles. Example pos: {points[0].position}");


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
