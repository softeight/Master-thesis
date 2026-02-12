using System.Collections;
using UnityEngine;
using System.Net.Sockets;
using System;


public class PointCloudCompute : MonoBehaviour
{
    public int subMeshIndex = 0;
    public float PointSize = 0.02f;
    public float Scale = 1.0f;

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

    // Arrays for storing receiving data
    private ushort[] depth_data = new ushort[pixel_count];
    private byte[] bodyIDs = new byte[pixel_count];
    private byte[] original_color = new byte[pixel_count * 4];

    // Variables for Callibration of Kinect Camera
    float cx = 262.72f;
    float cy = 207.01f;
    float fx = 365.77f;
    float fy = 365.77f;

    // Initialize GPU buffer
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer colorsBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    public Material material = default;
    public Mesh mesh;
    private Vector4[] positions = new Vector4[pixel_count];
    private Color[] colors = new Color[pixel_count];

    public Transform pointCloudRoot;


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
        bufferSize = (7 * pixel_count);
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
        int instanceCount = 0;
        float meanDepth = 0.0f;
        float min_y = float.MaxValue;
        float mean_x = 0.0f;


        for (int i = 0; i < pixel_count; i++)
        {
            if (bodyIDs[i] != 255)
            {
                // Recalculate the x and y index
                int x_pixel = i % cols;
                int y_pixel = i / cols;
              
                // Get the real x,y,z value
                float z = Convert.ToSingle(depth_data[i]) * 0.001f; // Convert to meters.
                float x = (x_pixel - cx) * -z / fx; // calibrate to the middle
                float y = (y_pixel - cy) * -z / fy;

                // Calculate factor value
                meanDepth += z;
                min_y = Mathf.Min(min_y, y); //calculate the min y value, so we can put the human always on the ground
                mean_x += x;
                
                // Write position and color data to the array
                positions[instanceCount] = new Vector4(x * Scale, y * Scale, z, PointSize);
                colors[instanceCount] = new Color(
                    original_color[i * 4] / 255.0f, 
                    original_color[i * 4 + 1] / 255.0f, 
                    original_color[i * 4 + 2] / 255.0f, 
                    original_color[i * 4 + 3] / 255.0f);

                instanceCount++;
            }
        }
        // Set factor and position/color buffer, so it can be give to the shader
        material.SetVector("factor", new Vector4(min_y, mean_x / Convert.ToSingle(instanceCount), 0.0f, meanDepth / Convert.ToSingle(instanceCount)));
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
                client.ReceiveBufferSize = bufferSize;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            while (position < bufferSize)
            {            
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

                Buffer.BlockCopy(packet_buffer, 0, original_color, 0, pixel_count * 4);
                Buffer.BlockCopy(packet_buffer, (pixel_count * 4), depth_data,0, pixel_count*2);
                Buffer.BlockCopy(packet_buffer, (pixel_count * 6), bodyIDs, 0, pixel_count);


                yield return new WaitForEndOfFrame();
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
