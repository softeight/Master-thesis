using System.Collections;
using UnityEngine;
using System.Net.Sockets;
using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Runtime.InteropServices;

using Color = UnityEngine.Color;
using System.Drawing.Imaging;

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
        var cfg = ConfigLoader.Load();
        ClientIp = cfg.ip;
        ClientPort = cfg.port;
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
        UnityEngine.Graphics.DrawMeshInstancedIndirect(mesh, subMeshIndex, material, new Bounds(Vector3.zero, new Vector3(2000.0f, 2000.0f, 2000.0f)), argsBuffer);
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
        byte[] header = new byte[16]; // 4 * 4 (magic, packet size, offset to depth data, offset to body index data)

        while (true)
        {
            if (!client.Connected)
            {
                UnityEngine.Debug.Log($"Connecting to {ClientIp}:{ClientPort}...");
                client.Connect(ClientIp, ClientPort);
                //client.ReceiveBufferSize = bufferSize;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            // Read chunks of 8 bytes until we found a proper header.
            var result = stream.BeginRead(header, 0, 16, null, null);

            while (!result.IsCompleted)
                yield return new WaitForEndOfFrame();

            // Check if we read anything and if not, repeat the whole procedure.
            readBytes = stream.EndRead(result);

            if (readBytes == 16)
            {
                // Check if we are starting with a magic number and if not, discard the package.
                uint magicNumber = BitConverter.ToUInt32(header, 0);
                uint packetSize = BitConverter.ToUInt32(header, 4);

                if (magicNumber == 0x3032434B && packetSize > 0) // "KC20"
                {
                    uint depthDataOffset = BitConverter.ToUInt32(header, 8);
                    uint bodyIndexDataOffset = BitConverter.ToUInt32(header, 12);

                    // Allocate a buffer.
                    byte[] packet_buffer = new byte[packetSize];

                    // Read the packet buffer.
                    while (position < packetSize)
                    {
                        result = stream.BeginRead(packet_buffer, position, Convert.ToInt32(packetSize - position), null, null);

                        while (!result.IsCompleted)
                            yield return new WaitForEndOfFrame();

                        readBytes = stream.EndRead(result);

                        if (readBytes > 0)
                            position += readBytes;

                        yield return new WaitForEndOfFrame();
                    }

                    // Reset position.
                    position = 0;
                    
                    // Uncompress the packet.
                    using (var compressedStream = new MemoryStream(packet_buffer, false))
                    using (var compressionStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    using (var reader = new BinaryReader(compressionStream))
                    //using (var reader = new BinaryReader(compressedStream))
                    {
                        // Get offsets to depth and body index data.
                        var overallSize = bodyIndexDataOffset + pixel_count;

                        // Decompress the remaining bytes.
                        packet_buffer = reader.ReadBytes(Convert.ToInt32(overallSize));
                    }

                    //// Read the color image.
                    //Bitmap colorImage;

                    //using (var colorBufferStream = new MemoryStream(packet_buffer, false))
                    //    colorImage = new Bitmap(Image.FromStream(colorBufferStream));

                    Buffer.BlockCopy(packet_buffer, 0, original_color, 0, pixel_count * 4);
                    Buffer.BlockCopy(packet_buffer, Convert.ToInt32(depthDataOffset), depth_data, 0, pixel_count * 2);
                    Buffer.BlockCopy(packet_buffer, Convert.ToInt32(bodyIndexDataOffset), bodyIDs, 0, pixel_count);

                    //// Convert the color data.
                    //var imageData = colorImage.LockBits(new Rectangle(0, 0, cols, rows), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                    //try
                    //{
                    //    Marshal.Copy(imageData.Scan0, original_color, 0, pixel_count * 4);
                    //}
                    //finally
                    //{
                    //    colorImage.UnlockBits(imageData);
                    //}
                }
            }

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
