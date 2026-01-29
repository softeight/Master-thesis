using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using Unity.VisualScripting;

public class PointCloudCompute : MonoBehaviour
{
    public int subMeshIndex = 0;
    public int instanceCount = 0;
    public float PointSize = 0.005f;
    public int scale = 3;

    // Initialize TCP related variables
    private TcpClient client;
    private NetworkStream stream;
    public string serverIp = "127.0.0.1";
    public int serverPort = 3363;

    // Variables for TCP packet
    private byte[] packet_buffer;
    public const int rows = 424;
    public const int cols = 512;
    public const int pixel_count = rows*cols;
    private int bufferSize;
    public int header = 8;

    //
    public ushort[] depth_data = new ushort[pixel_count];
    public byte[] bodyIDs = new byte[pixel_count];
    byte[] original_color = new byte[pixel_count * 4];

    // Variables for Callibration of Kinect Camera
    float cx = 262.72f;
    float cy = 207.01f;
    float fx = 365.77f;
    float fy = 365.77f;


    // Initialize GPU buffer
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer colorsBuffer;
    //private ComputeBuffer BodyIDBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    public Material material = default;
    public Mesh mesh;
    public Vector4[] positions = new Vector4[pixel_count];
    public Color[] colors = new Color[pixel_count];

    public Transform pointCloudRoot;

    //private Vector3 centerOffset = Vector3.zero;
    //private Vector3 lockedCenterOffset = Vector3.zero;
    //private bool offsetLocked = false;

    public void OnValidate()
    {
        if (material == null)
        {
            Debug.LogError("Material not assigned! Please drag a material into the script.");
            return;
        }

        if (mesh == null)
        {
            Debug.LogError("Mesh not assigned! Please drag a mesh into the script.");
            return;
        }
    }

    // Start is called before the first frame update
    void Start()
    {      
        // Initialize TCP relavent Variables
        bufferSize = (7 * pixel_count) + header;
        packet_buffer = new byte[bufferSize];
        client = new TcpClient();

        // Create args, color, positions Buffer
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        if (positionsBuffer != null) { positionsBuffer.Release(); }
        positionsBuffer = new ComputeBuffer(pixel_count, 4 * sizeof(float));
        if (colorsBuffer != null) { colorsBuffer.Release(); }
        colorsBuffer = new ComputeBuffer(pixel_count, 4 * sizeof(float));

        // Bind GPU Buffer with names
        material.SetBuffer("_Positions", positionsBuffer);
        material.SetBuffer("_Colors", colorsBuffer);

        // Start Reading Frame
        this.StartCoroutine(this.ReadFrame());
    }

    // Update is called once per frame
    void Update()
    {
        material.SetMatrix("_RootMatrix", pointCloudRoot.localToWorldMatrix);
        UpdateBuffers();
        Graphics.DrawMeshInstancedIndirect(mesh, subMeshIndex, material, new Bounds(Vector3.zero, new Vector3(2000.0f, 2000.0f, 2000.0f)), argsBuffer);
    }

    void UpdateBuffers()
    {
        instanceCount = 0;
        float z_sum = 0.0f;
        int count = 0;

        for (int i = 0; i < pixel_count; i++)
        {

            if (bodyIDs[i] != 255)
            {
                int x_pixel = i % cols;
                int y_pixel = i / cols;

                float z = depth_data[i] * 0.001f;
                float x = (x_pixel - cx) * z / fx;
                float y = (y_pixel - cy) * z / fy;
                //Vector3 p = new Vector3(-x * scale, -y * scale, z * scale);
                positions[instanceCount] = new Vector4(-x * scale, -y * scale, z * scale, PointSize * scale);
                //min = Vector3.Min(min, p);
                //max = Vector3.Max(max, p);
                colors[instanceCount] = new Color(original_color[i * 4] / 255.0f, original_color[i * 4 + 1] / 255.0f, original_color[i * 4 + 2] / 255.0f, original_color[i * 4 + 3] / 255.0f);

                instanceCount++;

                z_sum += z;
                count++;
            }
        }

        float average = 0.0f;

        if (count > 0)
            average = z_sum / (float)count;

        material.SetVector("average_z", new Vector4(average, average, average, average));

        //Vector3 center = (min + max) * 0.5f;
        //Vector3 centerOffset = -center;
        //Debug.Log("center offset: " + centerOffset);

        //if (!offsetLocked)
        //{
        //    lockedCenterOffset = centerOffset;
        //    offsetLocked = true;
        //}
        //material.SetVector("_CenterOffset", (Vector4)lockedCenterOffset);


        positionsBuffer.SetData(positions);
        colorsBuffer.SetData(colors);


        if (mesh != null)
        {
            args[0] = (uint)mesh.GetIndexCount(subMeshIndex); // index count per instance
            args[1] = (uint)instanceCount; // instance count
            args[2] = mesh.GetIndexStart(subMeshIndex); // start index location
            args[3] = mesh.GetBaseVertex(subMeshIndex); // base vertex location
            //start instance location : default 0
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }

        argsBuffer.SetData(args);


    }

    private IEnumerator ReadFrame()
    {
        int position = 0;
        int readBytes = 0;

        while (true)
        {
            if (!client.Connected)
            {
                UnityEngine.Debug.Log("Connecting...");
                client.Connect(serverIp, serverPort);
                //client.NoDelay = true;
                client.ReceiveBufferSize = bufferSize;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            while (position < bufferSize)
            {
                //while (!stream.DataAvailable)
                //    yield return new WaitForEndOfFrame();

                var result = stream.BeginRead(packet_buffer, position, bufferSize - position, null, null);

                while (!result.IsCompleted)
                    yield return new WaitForEndOfFrame();
                readBytes = stream.EndRead(result);
                if (readBytes == 0)
                {
                    yield return null;
                }

                position += readBytes;
                yield return null;

            }

                // position zuruecksetzen
                position = 0;

                Buffer.BlockCopy(packet_buffer, header, original_color, 0, pixel_count * 4);
                Buffer.BlockCopy(packet_buffer, (pixel_count * 4) + header, depth_data,0, pixel_count*2);
                Buffer.BlockCopy(packet_buffer, (pixel_count * 6) + header, bodyIDs, 0, pixel_count);


                yield return new WaitForEndOfFrame();
            //while (!stream.DataAvailable)
            //    yield return new WaitForEndOfFrame();

            //var result = stream.BeginRead(packet_buffer, totalBytesRead, bufferSize - totalBytesRead, null, null);

            //while (!result.IsCompleted)
            //    yield return new WaitForEndOfFrame();

            //totalBytesRead += stream.EndRead(result);

            //if (totalBytesRead >= bufferSize)
            //{
            //    totalBytesRead = 0;

            //    Buffer.BlockCopy(packet_buffer, header, original_color, 0, pixel_count * 4);
            //    Buffer.BlockCopy(packet_buffer, (pixel_count * 4) + header, depth_data, 0, pixel_count * 2);
            //    Buffer.BlockCopy(packet_buffer, (pixel_count * 6) + header, bodyIDs, 0, pixel_count);
            //}

            //yield return new WaitForEndOfFrame();

        }
    }

    void OnDisable()
    {
        if (positionsBuffer != null)
            positionsBuffer.Release();
        positionsBuffer = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;

        if (colorsBuffer != null)
            colorsBuffer.Release();
        colorsBuffer = null;
    }
}
