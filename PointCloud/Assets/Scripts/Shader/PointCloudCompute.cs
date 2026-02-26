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
    public string ClientIp;
    public int ClientPort;

    // Variables for TCP packet
    private byte[] packet_buffer;
    public const int rows = 424;
    public const int cols = 512;
    public const int pixel_count = rows*cols;
    //private int bufferSize;
    const int header = 12;
    const int max_payload = pixel_count * 6 + 12;

    // Arrays for storing receiving data
    private ushort[] depth_data = new ushort[pixel_count];
    //private byte[] bodyIDs = new byte[pixel_count];
    private byte[] original_color = new byte[pixel_count * 4];
    private byte[] lenBuf = new byte[4];
    private int xmin,ymin,bbox_w,bbox_h,current_point_count;

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
        var cfg = ConfigLoader.Load();
        ClientIp = cfg.ip;
        ClientPort = cfg.port;
        // Initialize TCP relavent Variables
        packet_buffer = new byte[max_payload];
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

        if (current_point_count == 0)
        {
            // nothing to draw
            if (mesh != null)
            {
                args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
                args[1] = 0;
                args[2] = mesh.GetIndexStart(subMeshIndex);
                args[3] = mesh.GetBaseVertex(subMeshIndex);
                argsBuffer.SetData(args);
            }
            return;
        }

        for (int i = 0; i < current_point_count; i++)
        {
            
                // Recalculate the x and y index
                int x_pixel = (i % cols) + xmin;
                int y_pixel = (i / cols) + ymin;
              
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
        // Set factor and position/color buffer, so it can be give to the shader
        material.SetVector("factor", new Vector4(min_y, mean_x / Convert.ToSingle(instanceCount), 0.0f, meanDepth / Convert.ToSingle(instanceCount)));
        positionsBuffer.SetData(positions,0,0,instanceCount);
        colorsBuffer.SetData(colors,0,0,instanceCount);


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
        //int position = 0;
        //int readBytes = 0;

        while (true)
        {
            if (!client.Connected)
            {
                UnityEngine.Debug.Log($"Connecting to {ClientIp}:{ClientPort}...");
                client.Connect(ClientIp, ClientPort);
                client.ReceiveBufferSize = bufferSize;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            yield return ReadExact(lenBuf, 0, 4);
            int totalBytes = BitConverter.ToInt32(lenBuf, 0);
            int remaining = totalBytes - 4;

            yield return ReadExact(packet_buffer, 0, remaining);

            xmin = BitConverter.ToUInt16(packet_buffer, 0); 
            yminin = BitConverter.ToUInt16(packet_buffer, 2); 
            bbox_w = BitConverter.ToUInt16(packet_buffer, 4); 
            bbox_h = BitConverter.ToUInt16(packet_buffer, 6); 

            current_point_count = bbox_h * bbox_w;
            if (current_point_count == 0)
            {
                // instanceCount=0
                args[1] = 0;
                argsBuffer.SetData(args);
                yield return null;
                continue;
            }

            int depth_bytes = current_point_count * 2;
            int color_bytes = current_point_count * 4;
            int expected_remaining = header + depth_bytes + color_bytes;

            if (remaining != expected_remaining)
                throw new Exception($"Size mismatch remaining={remaining}, expected={expected_remaining}");

            //while (position < bufferSize)
            //{            
            //    var result = stream.BeginRead(packet_buffer, position, bufferSize - position, null, null);

            //    while (!result.IsCompleted)
            //        yield return new WaitForEndOfFrame();
            //    readBytes = stream.EndRead(result);
            //    if (readBytes == 0)
            //    {
            //        yield return null;
            //    }

            //    position += readBytes;
            //    yield return null;

            //}

            // position zuruecksetzen
            //position = 0;
            Buffer.BlockCopy(packet_buffer, 8, depth_data, 0, depth_bytes);
            Buffer.BlockCopy(packet_buffer, 8 + depth_bytes, original_color, 0, color_bytes);
            //Buffer.BlockCopy(packet_buffer, 0, original_color, 0, pixel_count * 4);
            //Buffer.BlockCopy(packet_buffer, (pixel_count * 4), depth_data,0, pixel_count*2);
            //Buffer.BlockCopy(packet_buffer, (pixel_count * 6), bodyIDs, 0, pixel_count);


            yield return new WaitForEndOfFrame();
        }
    }
    private IEnumerator ReadExact(byte[] buf, int offset, int count)
    {
        int readTotal = 0;
        while (readTotal < count)
        {
            var ar = stream.BeginRead(buf, offset + readTotal, count - readTotal, null, null);
            while (!ar.IsCompleted) yield return null;

            int n = stream.EndRead(ar);
            if (n <= 0) throw new Exception("TCP disconnected");

            readTotal += n;
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
