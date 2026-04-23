using System.Collections;
using UnityEngine;
using System.Net.Sockets;
using System;
using Unity.VisualScripting;
using System.Diagnostics.Eventing.Reader;

public class PointCloudCompute : MonoBehaviour
{
    public int subMeshIndex = 0;
    public float PointSize = 0.02f;
    public float Scale = 1.0f;

    // Initialize TCP related variables
    private TcpClient client;
    private NetworkStream stream;
    public string ClientIp;
    public ushort ClientPort;

    // Variables for TCP packet
    private byte[] packet_buffer;
    public const int rows = 512;
    public const int cols = 424;
    public const int pixel_count = rows*cols; // Total pixel count:512*424

    const int headerSize = 20; // header of the transmitting packet
    const int max_payload = pixel_count * 7 + headerSize;

    // Arrays for storing receiving data
    private ushort[] depth_data = new ushort[pixel_count];
    private byte[] bodyIDs = new byte[pixel_count];
    private byte[] original_color = new byte[pixel_count * 4];
    private int xmin,ymin,bbox_w,bbox_h,current_point_count; //? is that problematic to have int 32
    private DateTime? lastFrame = null;
    private int maxLatency = 100; // in milliseconds.

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
    public Texture2D pic;

    private long lastPacketDataSize = 0;
    private DateTime lastPacketDataSizeTime = DateTime.Now;

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
        ClientIp = cfg.ip;//"134.109.192.10";
        ClientPort = cfg.port;//3363;
        // Initialize TCP relavent Variables
        packet_buffer = new byte[max_payload];
        client = new TcpClient();
        lastFrame = null;

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
        float mean_x = 0.0f;//?

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
        

        for (int i = 0; i < bbox_h; i++)
        {
            for (int j = 0; j < bbox_w; j++)
            {
                int x_pixel = xmin + j;
                int y_pixel = ymin + i;

                int index = y_pixel * rows + x_pixel;

                if (bodyIDs[index] != 255)
                {

                    // Get the real x,y,z value
                    float z = Convert.ToSingle(depth_data[index]) * 0.001f; // Convert to meters.
                    float x = (x_pixel - cx) * -z / fx; // calibrate to the middle
                    float y = (y_pixel - cy) * -z / fy;

                    // Calculate factor value
                    meanDepth += z;
                    min_y = Mathf.Min(min_y, y); //calculate the min y value, so we can put the human always on the ground
                    mean_x += x;

                    // Write position and color data to the array
                    positions[instanceCount] = new Vector4(x * Scale, y * Scale, z, PointSize);
                    colors[instanceCount] = new Color(
                        original_color[index * 4] / 255.0f,
                        original_color[index * 4 + 1] / 255.0f,
                        original_color[index * 4 + 2] / 255.0f,
                        original_color[index * 4 + 3] / 255.0f);
                   
                    instanceCount++;


                }

            }
                                
               
        }


        //Debug.Log(instanceCount);
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

        while (true)
        {
            if (!client.Connected)
            {
                UnityEngine.Debug.Log($"Connecting to {ClientIp}:{ClientPort}...");
                client.Connect(ClientIp, ClientPort);
                client.ReceiveBufferSize = max_payload;
                stream = client.GetStream();
                UnityEngine.Debug.Log("Connected.");
            }

            // Reset bounding box, if no frame has arrived for more than 5 seconds.
            if (lastFrame.HasValue && (DateTime.Now - lastFrame.Value).TotalSeconds > 5)
            {
                bbox_w = bbox_h = 0;
                ymin = xmin = 0;
            }

            // read the header from the packet, to know how many valid total bytes there are
            yield return ReadExact(packet_buffer, 0, headerSize);
            var payloadSize = BitConverter.ToInt32(packet_buffer, 0);
            int offset = sizeof(int); // payloadSize
            int hxmin = BitConverter.ToInt16(packet_buffer, 4);
            offset += sizeof(ushort);
            int hymin = BitConverter.ToInt16(packet_buffer, 6);
            offset += sizeof(ushort);
            int hbbox_w = BitConverter.ToInt16(packet_buffer, 8);
            offset += sizeof(ushort);
            int hbbox_h = BitConverter.ToInt16(packet_buffer, 10);
            offset += sizeof(ushort);
            var timeStamp = DateTime.FromBinary(BitConverter.ToInt64(packet_buffer, 12));
            offset += sizeof(Int64);

#if UNITY_EDITOR
            lastPacketDataSize += payloadSize + 20;

            if (DateTime.Now - lastPacketDataSizeTime >= TimeSpan.FromSeconds(1))
            {
                Debug.Log(String.Format("Data received: {0} Mb", Convert.ToSingle(lastPacketDataSize) / 1024.0 / 1024.0));
                lastPacketDataSizeTime = DateTime.Now;
                lastPacketDataSize = 0;
            }
#endif

            if (payloadSize <= 0)
            {
                // No body submitted, so there's nothing to draw.
                bbox_w = bbox_h = 0;
                ymin = xmin = 0;
            }
            else 
            {
                // continue to read the rest bytes
                yield return ReadExact(packet_buffer, offset, payloadSize);

                if (lastFrame == null)
                    lastFrame = timeStamp; // NOTE: First frame is dropped. Should be fixed.
                else
                {
                    // Get time difference and discard frames that took too long.
                    var latency = timeStamp - lastFrame.Value;
                    lastFrame = timeStamp;

                    // Only parse frame if latency is below threshold.
                    if (latency.TotalMilliseconds <= maxLatency)
                    {
                        bbox_h = hbbox_h;
                        bbox_w = hbbox_w;
                        xmin = hxmin;
                        ymin = hymin;

                        // the real number of point that need to be draw (inside of the bbox)
                        current_point_count = bbox_h * bbox_w;

                        // this line below is for testing whether there is valid bbox data
                        //Debug.Log($"xmin = {xmin}, ymin = {ymin},w = {bbox_w},h = {bbox_h}, current_point = {current_point_count}");

                        //if (current_point_count == 0)
                        //{
                        //    args[1] = 0;
                        //    argsBuffer.SetData(args);
                        //    yield return null;
                        //    continue;
                        //   
                        //}         


                        for (int i = 0; i < bbox_h; i++)
                        {
                            // bc of the bbox, need to write data one line by line
                            int srcindex = ((ymin + i) * rows + xmin) * 2;
                            Buffer.BlockCopy(packet_buffer, offset, depth_data, srcindex, bbox_w * 2);
                            offset += bbox_w * 2;

                        }
                        for (int i = 0; i < bbox_h; i++)
                        {
                            int srcIndex = ((ymin + i) * rows + xmin) * 4;
                            Buffer.BlockCopy(packet_buffer, offset, original_color, srcIndex, bbox_w * 4);
                            offset += bbox_w * 4;
                        }
                        for (int i = 0; i < bbox_h; i++)
                        {
                            int srcIndex = (ymin + i) * rows + xmin;
                            Buffer.BlockCopy(packet_buffer, offset, bodyIDs, srcIndex, bbox_w);
                            offset += bbox_w;
                        }
                    }
                }
            }

            yield return new WaitForEndOfFrame();
        }
    }
    private IEnumerator ReadExact(byte[] buf, int offset, int count)
    {
        int readTotal = 0;
        while (readTotal < count)
        {
            var result = stream.BeginRead(buf, offset + readTotal, count - readTotal, null, null);
            while (!result.IsCompleted) yield return null;

            int n = stream.EndRead(result);
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
