using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.Kinect;
using System.Drawing;
using System.Diagnostics;



namespace KinectClient
{
    class Program
    {      
        static void Main(string[] args)
        {
            int port = 3363;
            string ip = "134.109.192.10";

            if (args.Length == 2)
            {
                ip = args[0];
                port = int.Parse(args[1]);

            }
            
            IPAddress ipAddress = IPAddress.Parse(ip);

            var listner = new TcpListener(ipAddress, port);
            listner.Start();

            //initialize the camera sensor
            KinectSensor kinectSensor = null;          
            MultiSourceFrameReader reader = null;
            CoordinateMapper mapper = null;

            //create empty array to store 
            const int rows = 512;
            const int cols = 424;
            int xmin = int.MaxValue;
            int xmax = int.MinValue;
            int ymin = int.MaxValue;
            int ymax = int.MinValue;
            int header = 12;
            int pixel_count = rows * cols;
            byte[] color_data = new byte[1920 * 1080 * 4];          
            ushort[] depth_data = new ushort[pixel_count];
            byte[] body_index_data = new byte[pixel_count];
            byte[] converted_color_data = new byte[pixel_count * 4];

            byte[] packet = new byte[(6 * pixel_count) + 12];
            ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[pixel_count];
           

            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

           // byte[] packet;
;

            while (true)
            {
                Console.WriteLine($"Waiting for client at port {port}...");

                TcpClient client = listner.AcceptTcpClient();
                client.NoDelay = true;
                client.SendBufferSize = packet.Length;
                NetworkStream ns = client.GetStream();

                Console.WriteLine("Sending packages...");

                while (true)
                {
                    //getting depth&color&bodyIdx frame from kinect
                    MultiSourceFrame msf = reader.AcquireLatestFrame();
                    if (msf == null)
                        continue;

                    using (var df = msf.DepthFrameReference.AcquireFrame())
                    {
                        if (df != null)
                        {
                            df.CopyFrameDataToArray(depth_data);
                        }
                    }

                    using (var cf = msf.ColorFrameReference.AcquireFrame())
                    {
                        if (cf != null)
                        {
                            cf.CopyConvertedFrameDataToArray(color_data, ColorImageFormat.Rgba);
                        }
                    }

                    using (var bif = msf.BodyIndexFrameReference.AcquireFrame())
                    {
                        if (bif != null)
                        {
                            bif.CopyFrameDataToArray(body_index_data);
                        }
                    }

                    mapper = kinectSensor.CoordinateMapper;
                    mapper.MapDepthFrameToColorSpace(depth_data, colorSpacePoints);

                    //bool hasBody = false;
                    Parallel.For(0, pixel_count, i =>
                    {
                        if (body_index_data[i] != 255)
                        {
                            //hasBody = true;
                            int x = i % rows;
                            int y = i / rows;
                            if (x < xmin) xmin = x;
                            if (x > xmax) xmax = x;
                            if (y < ymin) ymin = y;
                            if (y > ymax) ymax = y;
                        }
                    });
                    int bbox_w = xmax - xmin + 1;
                    int bbox_h = ymax - ymin + 1;
                    int payloadBytes = bbox_w * bbox_h * 6;
                    int totalBytes = header + payloadBytes;
        

                    Buffer.BlockCopy(BitConverter.GetBytes(totalBytes), 0, packet, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)xmin), 0, packet, 4, 2); 
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)ymin), 0, packet, 6, 2); 
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)bbox_w), 0, packet, 8, 2); 
                    Buffer.BlockCopy(BitConverter.GetBytes((ushort)bbox_h), 0, packet, 10, 2);

                    for (int i = 0; i < bbox_h; i++)
                    {
                        int srcIndex = (ymin + i) * rows + xmin;
                        Buffer.BlockCopy(depth_data, srcIndex * 2, packet, 12, bbox_w * 2);
            
                    }

                    //writing each pixel data into packet buffer
                    Parallel.For(0, pixel_count, i =>
                    {
                        int converted_color_index = i * 4;

                        int color_x = (int)Math.Floor(colorSpacePoints[i].X + 0.5);
                        int color_y = (int)Math.Floor(colorSpacePoints[i].Y + 0.5);

                        if (color_x >= 0 && (color_x < 1920)
                            && (color_y >= 0) && (color_y < 1080))
                        {
                            int color_index = (color_y * 1920 + color_x) * 4;

                            converted_color_data[converted_color_index] = color_data[color_index];
                            converted_color_data[converted_color_index + 1] = color_data[color_index + 1];
                            converted_color_data[converted_color_index + 2] = color_data[color_index + 2];
                            converted_color_data[converted_color_index + 3] = color_data[color_index + 3];
                        }
                        else
                        {
                            converted_color_data[converted_color_index] = 0;
                            converted_color_data[converted_color_index + 1] = 0;
                            converted_color_data[converted_color_index + 2] = 0;
                            converted_color_data[converted_color_index + 3] = 255;

                        }
                    });

                    for (int i = 0; i < bbox_h; i++)
                    {
                        int srcByte = ((ymin + i) * rows + xmin) * 4;
                        Buffer.BlockCopy(converted_color_data, srcByte, packet, (12 + bbox_w * 2), bbox_w * 4);
                    }

                    //Buffer.BlockCopy(converted_color_data, 0, packet_buffer, 0, pixel_count * 4);
                    //Buffer.BlockCopy(depth_data, 0, packet_buffer, (pixel_count * 4), pixel_count * 2);
                    //Buffer.BlockCopy(body_index_data, 0, packet_buffer, (pixel_count * 6), pixel_count);


                    try
                    {
                        ns.Write(packet, 0, totalBytes);
                        ns.Flush();
                    }
                    catch (System.IO.IOException ex)
                    {
                        Console.WriteLine("Client disconnected.");
                        break;
                    }

                    System.Threading.Thread.Sleep(1000 / 24);
                }

            }

        }
    }
}
