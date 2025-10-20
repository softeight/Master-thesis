using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.Kinect;
using System.Drawing;



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
            byte[] color_data = new byte[1920 * 1080 * 4];
            ushort[] depth_data = new ushort[512 * 424];
            byte[] body_index_data = new byte[512 * 424];

            byte[] packet_buffer = new byte[7 * 512 * 424];
            ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[512*424];
           

            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

            
            while (true)
            {
                Console.WriteLine("Waiting for client...");

                TcpClient client = listner.AcceptTcpClient();
                NetworkStream ns = client.GetStream();

                Console.WriteLine("Sending packages...");

                while (true)
                {
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
                    //for (int x = 0; x < 512; x++)                      
                    //{
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
                                packet_buffer[(i * 7)] = r;
                                packet_buffer[(i * 7) + 1] = g;
                                packet_buffer[(i * 7) + 2] = b;
                                packet_buffer[(i * 7) + 3] = a;
                                Array.ConstrainedCopy(d, 0, packet_buffer, (i * 7) + 4, 2);
                                packet_buffer[(i * 7) + 6] = bodyID;

                                //testpic.SetPixel(x, y, Color.FromArgb(r, g, b));
                            }
                            else
                            {
                                packet_buffer[i * 7 + 0] = 0;
                                packet_buffer[i * 7 + 1] = 0;
                                packet_buffer[i * 7 + 2] = 0;
                                packet_buffer[i * 7 + 3] = 255;
                                packet_buffer[i * 7 + 4] = 0;
                                packet_buffer[i * 7 + 5] = 0;
                                packet_buffer[i * 7 + 6] = 255;
                                //testpic.SetPixel(x, y, Color.Black);
                            }
                        }
                    });
                       
                    //}
                    //testpic.Save(@"C:\Users\yiyu\Documents\uni\Master\output_cm.png", System.Drawing.Imaging.ImageFormat.Png);
                    try
                    {
                        ns.Write(packet_buffer, 0, 7 * 512 * 424);
                        ns.Flush();
                        //Console.WriteLine("Write success");
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
