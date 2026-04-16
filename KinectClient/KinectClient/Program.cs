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
        //static int GetIndex(int x, int y, int width)
        //{
        //    return y * width + x;
        //}

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
            int xmin;
            int xmax;
            int ymin;
            int ymax;
            int pixel_count = rows * cols;
            byte[] color_data = new byte[1920 * 1080 * 4];          
            ushort[] depth_data = new ushort[pixel_count];
            byte[] body_index_data = new byte[pixel_count];
            byte[] converted_color_data = new byte[pixel_count * 4];

            byte[] packet = new byte[(7 * pixel_count) + 20]; // header size is 20 bytes.
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
                    xmin = int.MaxValue;
                    xmax = int.MinValue;
                    ymin = int.MaxValue;
                    ymax = int.MinValue;

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

                    // Get the xmin,max,ymin,max
                    for (int i = 0; i < pixel_count; i++)
                    {
                        if (body_index_data[i] != 255)
                        {

                            int x = i % rows;                          
                            int y = i / rows;
                            if (x < xmin) 
                            { 
                                xmin = x;                              
                            }

                            if (x > xmax) 
                            {
                                xmax = x;
                            }
                            
                            if (y < ymin) 
                            {
                                ymin = y;
                            }
                            
                            if (y > ymax) 
                            {
                                ymax = y;
                            }
                            
                        }
                    }

                    bool hasBody = (xmax >= xmin) && (ymax >= xmax);

                    if (hasBody)
                    {
                        int bbox_w = hasBody ? (xmax - xmin) : 0;
                        int bbox_h = hasBody ? (ymax - ymin) : 0;
                        int payloadBytes = bbox_w * bbox_h * 7;
                        int offset = 0;

                        // block copy the header data to the tcp packet
                        Buffer.BlockCopy(BitConverter.GetBytes(payloadBytes), 0, packet, 0, sizeof(int));
                        offset += sizeof(int);
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)xmin), 0, packet, 4, sizeof(ushort));
                        offset += sizeof(ushort);
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)ymin), 0, packet, 6, sizeof(ushort));
                        offset += sizeof(ushort);
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)bbox_w), 0, packet, 8, sizeof(ushort));
                        offset += sizeof(ushort);
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)bbox_h), 0, packet, 10, sizeof(ushort));
                        offset += sizeof(ushort);
                        Buffer.BlockCopy(BitConverter.GetBytes(DateTime.Now.ToBinary()), 0, packet, 12, sizeof(long));
                        offset += sizeof(long);
                        var totalBytes = offset + payloadBytes;

                        //convert color data from 1920*1080 to 525*424
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



                        // copy depth and color data inside of the bbox into the packet
                        for (int i = 0; i < bbox_h; i++)
                        {
                            int srcIndex = ((ymin + i) * rows + xmin) * 2;
                            Buffer.BlockCopy(depth_data, srcIndex, packet, offset, bbox_w * 2);
                            offset += bbox_w * 2;

                        }
                        for (int i = 0; i < bbox_h; i++)
                        {
                            int srcIndex = ((ymin + i) * rows + xmin) * 4;
                            Buffer.BlockCopy(converted_color_data, srcIndex, packet, offset, bbox_w * 4);
                            offset += bbox_w * 4;

                        }
                        for (int i = 0; i < bbox_h; i++)
                        {
                            int srcIndex = (ymin + i) * rows + xmin;
                            Buffer.BlockCopy(body_index_data, srcIndex, packet, offset, bbox_w);
                            offset += bbox_w;

                        }


#if DEBUG
                        Console.WriteLine($"hasbody={hasBody},bbox= {bbox_h}h,{bbox_w}w,xmin={xmin}, xmax={xmax}");
#endif

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
                    }

                    System.Threading.Thread.Sleep(1000 / 24);
                }

            }

        }
    }
}
