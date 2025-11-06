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
            //Bitmap testpic = new Bitmap(512, 424);
            //initialise tcp listener instance & set endpoint

            var listner = new TcpListener(IPAddress.Loopback, 3363);
            listner.Start();

            //initialize the camera sensor
            KinectSensor kinectSensor = null;          
            MultiSourceFrameReader reader = null;
            CoordinateMapper mapper = null;

            //create empty array to store 
            int pixel_count = 512 * 424;
            byte[] color_data = new byte[1920 * 1080 * 4];          
            ushort[] depth_data = new ushort[pixel_count];
            byte[] body_index_data = new byte[pixel_count];
            byte[] converted_color_data = new byte[pixel_count * 4];

            byte[] packet_buffer = new byte[(7 * pixel_count) + 8];
            ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[pixel_count];
           

            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

            //long sum = 0;
            //int count = 0;
            //Stopwatch sw = new Stopwatch();
            
            int header = 8;
            
            while (true)
            {
                Console.WriteLine("Waiting for client...");

                TcpClient client = listner.AcceptTcpClient();
                client.NoDelay = true;
                client.SendBufferSize = packet_buffer.Length;
                NetworkStream ns = client.GetStream();

                Console.WriteLine("Sending packages...");

                while (true)
                {
                    //sw.Start();
                    //getting depth&color&bodyIdx frame from kinect
                    MultiSourceFrame msf = reader.AcquireLatestFrame();
                    if (msf == null)
                    {
                        //Console.WriteLine("No MultiSourceFrame, skipping...");                

                        continue;
                    }

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

                    Buffer.BlockCopy(converted_color_data, 0, packet_buffer, 8, pixel_count * 4);
                    Buffer.BlockCopy(depth_data, 0, packet_buffer, (pixel_count * 4) + 8, pixel_count * 2);
                    Buffer.BlockCopy(body_index_data, 0, packet_buffer, (pixel_count * 6) + 8, pixel_count);


                    try
                    {
                        long timestamp = DateTime.UtcNow.Ticks;
                        byte[] time_bytes = BitConverter.GetBytes(timestamp);
                        Buffer.BlockCopy(time_bytes, 0, packet_buffer, 0, header);
                        ns.Write(packet_buffer, 0, packet_buffer.Length);
                        ns.Flush();
                       

                    }
                    catch (System.IO.IOException ex)
                    {
                        Console.WriteLine("Client disconnected.");
                        break;
                    }
                }

            }

        }
    }
}
