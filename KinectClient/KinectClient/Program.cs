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
                    
                    
                    Parallel.For(0, 512, x =>
                    {
                        for (int y = 0; y < 424; y++)
                        {

                            float v = y / (float)424;

                            int i = y * 512 + x;

                            int color_x = (int)Math.Floor(colorSpacePoints[i].X + 0.5);
                            int color_y = (int)Math.Floor(colorSpacePoints[i].Y + 0.5);

                            if (color_x >= 0 && (color_x < 1920)
                                && (color_y >= 0) && (color_y < 1080))
                            {
                                int color_index = (color_y * 1920 + color_x) * 4;
                                //get rgba & d & bodyIndex value
                                byte r = color_data[color_index];
                                byte g = color_data[color_index + 1];
                                byte b = color_data[color_index + 2];
                                byte a = color_data[color_index + 3];

                                ushort depth_value = depth_data[i];
                                byte[] d = BitConverter.GetBytes(depth_value);

                                byte bodyID = body_index_data[i];

                                //write rgba & d & bodyIndex into the buffer
                                packet_buffer[(i * 7)+header] = r;
                                packet_buffer[(i * 7) + 1+header] = g;
                                packet_buffer[(i * 7) + 2+header] = b;
                                packet_buffer[(i * 7) + 3+header] = a;
                                Array.ConstrainedCopy(d, 0, packet_buffer, (i * 7) + 4+header, 2);
                                packet_buffer[(i * 7) + 6+header] = bodyID;

                                //testpic.SetPixel(x, y, Color.FromArgb(r, g, b));
                            }
                            else
                            {
                                packet_buffer[i * 7 + header] = 0;
                                packet_buffer[i * 7 + 1 + header] = 0;
                                packet_buffer[i * 7 + 2 + header] = 0;
                                packet_buffer[i * 7 + 3 + header] = 255;
                                packet_buffer[i * 7 + 4 + header] = 0;
                                packet_buffer[i * 7 + 5 + header] = 0;
                                packet_buffer[i * 7 + 6 + header] = 255;
                                //testpic.SetPixel(x, y, Color.Black);
                            }
                        }
                    });

                   



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
