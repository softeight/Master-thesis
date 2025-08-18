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
            CameraSpacePoint[] cameraSpacePoints = new CameraSpacePoint[1920 * 1080];
            MultiSourceFrameReader reader = null;
            CoordinateMapper mapper; 
            byte[] color_data;
            ushort[] depth_data;
            byte[] data = new byte[9];


            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);

            while (true)
            {
                MultiSourceFrame msf = reader.AcquireLatestFrame();
                if (msf == null)
                {
                    Console.WriteLine("No MultiSourceFrame, skipping...");                

                    continue;
                }             
                                                                                 
                DepthFrame df = msf.DepthFrameReference.AcquireFrame();
                ColorFrame cf = msf.ColorFrameReference.AcquireFrame();
                
                if (df == null || cf == null)
                {
                    Console.WriteLine("Frame dropped or incomplete, skipping...");
                    continue;
                }


                //create empty array to store 
                color_data = new byte[1920 * 1080 * 4];
                depth_data = new ushort[512 * 424];

                //copy each pixel of the depth & color frame into array            
                df.CopyFrameDataToArray(depth_data);
                cf.CopyConvertedFrameDataToArray(color_data, ColorImageFormat.Rgba);

                //mapping color data to 3d camera space coordinates
                mapper = kinectSensor.CoordinateMapper;
                mapper.MapColorFrameToCameraSpace(depth_data, cameraSpacePoints);

                //for (int i = 0; i < cameraSpacePoints.Length; i+=100)
                //{
                for(int x = 0; x < 1920; x += 16)
                {
                    for(int y = 0; y < 1080; y += 16)
                    {
                        int i = y * 1920 + x;
                        CameraSpacePoint p = cameraSpacePoints[i];

                        if (float.IsInfinity(p.X) || float.IsInfinity(p.Y) ||
                            float.IsInfinity(p.Z) || p.Z > 3) continue;
                        short comp_x = (short)(p.X * 1000f);
                        byte[] posX = BitConverter.GetBytes(comp_x); //2 byte
                        Array.ConstrainedCopy(posX, 0, data, 0, 2);
                        short comp_y = (short)(p.Y * 1000f);
                        byte[] posY = BitConverter.GetBytes(comp_y); //2byte
                        Array.ConstrainedCopy(posY, 0, data, 2, 2);
                        short comp_z = (short)(p.Z * 1000f);
                        byte[] posZ = BitConverter.GetBytes(comp_z); //4byte
                        Array.ConstrainedCopy(posZ, 0, data, 4, 2);

                        int colorindex = i * 4;
                        data[6] = color_data[colorindex]; //r
                        data[7] = color_data[colorindex + 1]; //g
                        data[8] = color_data[colorindex + 2]; //b
                                                              // data[15] = color_data[colorindex + 3];  //a

                        try
                        {
                            udpclient.Send(data, 9);
                            Console.WriteLine("send sucucess");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                    }
                }

                                     

                //}
                if(df != null) df.Dispose();
                if(cf != null) cf.Dispose();


            }

        }
    }
}
