using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.Kinect;


namespace KinectClient
{
    class Program
    {

        static void Main(string[] args)
        {
            //initialise udp client instance & set endpoint
            UdpClient udpclient = new UdpClient();
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3363);
            udpclient.Connect(endPoint);

            //initialize the camera sensor
            KinectSensor kinectSensor = null;
            CameraSpacePoint[] cameraSpacePoints = new CameraSpacePoint[512 * 424];
            ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[512 * 424];
            MultiSourceFrameReader reader = null;
            CoordinateMapper mapper; 

           
            //create empty array to store 
            byte[] color_data = new byte[1920 * 1080 * 4];
            ushort[] depth_data = new ushort[512 * 424];
            byte[] body_index_data = new byte[512 * 424];

            //
            short point_max_count = 200;
            byte[] packet_buffer = new byte[6 * point_max_count];
            int count = 0;


            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

            Console.WriteLine("Sending packages...");
            while (true)
            {
                MultiSourceFrame msf = reader.AcquireLatestFrame();
                if (msf == null)
                {
                    //Console.WriteLine("No MultiSourceFrame, skipping...");                

                    continue;
                }             
                  
                using(var df = msf.DepthFrameReference.AcquireFrame())
                {
                    if(df != null)
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


                //mapping color data to 3d camera space coordinates
                mapper = kinectSensor.CoordinateMapper;
                //mapper.MapColorFrameToCameraSpace(depth_data, cameraSpacePoints);
                mapper.MapDepthFrameToCameraSpace(depth_data, cameraSpacePoints);
                //mapper.MapDepthFrameToColorSpace(depth_data, colorSpacePoints);

                //for (int i = 0; i < cameraSpacePoints.Length; i++)
                //{
                for(int x = 0; x < 512; x+=1)
                {
                    for(int y = 0; y < 424; y+=1)
                    {
                        
                        int i = y * 512 + x;

                        CameraSpacePoint p = cameraSpacePoints[i];                 

                        if (float.IsInfinity(p.X) || float.IsInfinity(p.Y) ||
                            float.IsInfinity(p.Z) || p.Z > 3 || body_index_data[i] > 5) continue;
                        short comp_x = (short)(p.X * 1000f);
                        byte[] posX = BitConverter.GetBytes(comp_x); //2 byte
                        Array.ConstrainedCopy(posX, 0, packet_buffer, count*6, 2);
                        short comp_y = (short)(p.Y * 1000f);
                        byte[] posY = BitConverter.GetBytes(comp_y); //2byte
                        Array.ConstrainedCopy(posY, 0, packet_buffer, count*6 + 2, 2);
                        short comp_z = (short)(p.Z * 1000f);
                        byte[] posZ = BitConverter.GetBytes(comp_z); //2byte
                        Array.ConstrainedCopy(posZ, 0, packet_buffer, count*6 + 4, 2);
                        
                        count++;


                        if(count == point_max_count)
                        {
                            try
                            {
                                udpclient.Send(packet_buffer, packet_buffer.Length);
                                //Console.WriteLine("send sucucess");
                                count = 0;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                       
                    }
                }                                    



            }

        }
    }
}
