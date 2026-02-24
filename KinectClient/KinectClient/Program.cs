using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Microsoft.Kinect;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.IO.Compression;


namespace KinectClient
{
    public static class KinectExtensions
    {
        public static void ToBitmap(this ColorFrame colorFrame, ref Bitmap bitmap)
        {
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                throw new ArgumentException("Expected 32 bit ARGB pixel format.");

            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

            try
            {
                colorFrame.CopyRawFrameDataToIntPtr(data.Scan0, Convert.ToUInt32(bitmap.Height * bitmap.Width) * 4);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }

    public static class BitmapExtensions
    {
        public static void ToJpeg(this Bitmap bitmap, Stream stream)
        {
            //EncoderParameters encoderParameters = new EncoderParameters(1);
            //encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80);
            //bitmap.Save(stream, GetEncoder(ImageFormat.Jpeg), encoderParameters);
            bitmap.Save(stream, ImageFormat.Jpeg);
        }

        public static void ToPng(this Bitmap bitmap, Stream stream)
        {
            bitmap.Save(stream, ImageFormat.Png);
        }

        public static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders()
                .Single(codec => codec.FormatID == format.Guid);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int port = 3363;
            //string ip = "134.109.192.10";
            string ip = "127.0.0.1";

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
            int pixel_count = 512 * 424;
            byte[] color_data = new byte[1920 * 1080 * 4];
            ushort[] depth_data = new ushort[pixel_count];
            byte[] body_index_data = new byte[pixel_count];
            byte[] converted_color_data = new byte[pixel_count * 4];
            //Bitmap converted_color_data = new Bitmap(512, 424, PixelFormat.Format32bppArgb);
            int framesPerSecond = 24;

            // FourCC For marking the packet start.
            var magicNumber = BitConverter.ToUInt32(Encoding.UTF8.GetBytes(new char[4] { 'K', 'C', '2', '0' }), 0); // 0x3032434B

            //byte[] packet_buffer = new byte[(7 * pixel_count)];
            ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[pixel_count];

            //get thw kinectSensor object
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.Open();

            //open the reader for the depth & color frames
            reader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);
            ;

            while (true)
            {
                Console.WriteLine($"Waiting for client at port {port}...");

                TcpClient client = listner.AcceptTcpClient();
                client.NoDelay = true;

                using (NetworkStream ns = client.GetStream())
                {
                    Console.WriteLine("Sending packages...");

                    while (true)
                    {
                        // Don't block.
                        System.Threading.Thread.Sleep(0);

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

                        //writing each pixel data into packet buffer
                        Parallel.For(0, pixel_count, i =>
                        //for (int i = 0; i < pixel_count; ++i)
                        {
                            int converted_color_index = i * 4;
                            //int x = i % 512;
                            //int y = i / 512;

                            int color_x = (int)Math.Floor(colorSpacePoints[i].X + 0.5);
                            int color_y = (int)Math.Floor(colorSpacePoints[i].Y + 0.5);

                            if (color_x >= 0 && (color_x < 1920)
                                && (color_y >= 0) && (color_y < 1080))
                            {
                                int color_index = (color_y * 1920 + color_x) * 4;

                                //converted_color_data.SetPixel(x, y, Color.FromArgb(
                                //    color_data[color_index + 3],   // A
                                //    color_data[color_index + 0],   // R
                                //    color_data[color_index + 1],   // G
                                //    color_data[color_index + 2])); // B

                                converted_color_data[converted_color_index] = color_data[color_index];
                                converted_color_data[converted_color_index + 1] = color_data[color_index + 1];
                                converted_color_data[converted_color_index + 2] = color_data[color_index + 2];
                                converted_color_data[converted_color_index + 3] = color_data[color_index + 3];
                            }
                            else
                            {
                                //converted_color_data.SetPixel(x, y, Color.Black);

                                converted_color_data[converted_color_index] = 0;
                                converted_color_data[converted_color_index + 1] = 0;
                                converted_color_data[converted_color_index + 2] = 0;
                                converted_color_data[converted_color_index + 3] = 255;
                            }
                        });
                        //}

                        //Buffer.BlockCopy(converted_color_data, 0, packet_buffer, 0, pixel_count * 4);
                        //Buffer.BlockCopy(depth_data, 0, packet_buffer, (pixel_count * 4), pixel_count * 2);
                        //Buffer.BlockCopy(body_index_data, 0, packet_buffer, (pixel_count * 6), pixel_count);

                        // Create the packet. Start with enough capacity for at least the body index and depth data. We don't know the final jpeg size, so it
                        // will get increased at least once.
                        uint depthPosition = 0;
                        uint bodyIndexPosition = 0;
                        byte[] packet;

                        using (var packetStream = new MemoryStream(pixel_count * 3))
                        {
                            using (var writer = new BinaryWriter(packetStream))
                            {
                                // Convert color image into jpeg and write it to the packet stream.
                                //converted_color_data.ToJpeg(packetStream);
                                //converted_color_data.ToPng(packetStream);
                                packetStream.Write(converted_color_data, 0, converted_color_data.Length);
                                writer.Flush();

                                // Write the depth to the packet stream.
                                depthPosition = Convert.ToUInt32(packetStream.Position);

                                foreach (var depth in depth_data)
                                    writer.Write(depth);

                                writer.Flush();

                                // Write the body index data to the packet stream.
                                bodyIndexPosition = Convert.ToUInt32(packetStream.Position);
                                writer.Write(body_index_data);
                            }

                            // Save the packet.
                            packet = packetStream.ToArray();
                        }

#if DEBUG
                        var originalPacketSize = packet.Length;
#endif

                        // Compress the overall packet.
                        using (var compressedPacketStream = new MemoryStream(pixel_count))
                        {
                            using (var compressionStream = new DeflateStream(compressedPacketStream, CompressionLevel.Fastest))
                            using (var writer = new BinaryWriter(compressionStream))
                            {
                                // Write the depth and body index positions first.
                                writer.Write(depthPosition);
                                writer.Write(bodyIndexPosition);

                                // Next, write the package.
                                writer.Write(packet);
                            }

                            packet = compressedPacketStream.ToArray();
                        }

#if DEBUG
                        var compressedPacketSize = packet.Length;
                        float compressionRate = ((float)compressedPacketSize / (float)originalPacketSize) * 100.0f;
#endif

                        try
                        {
                            //ns.Write(packet_buffer, 0, packet_buffer.Length);
                            using (var writer = new BinaryWriter(ns, Encoding.Default, true))
                            {
                                // Write header.
                                writer.Write(magicNumber);
                                writer.Write(packet.Length);
                                writer.Write(depthPosition);
                                writer.Write(bodyIndexPosition);

                                // Write the compressed packet next.
                                writer.Write(packet);
                            }

                            ns.Flush();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Client disconnected:\r\n\t{ex}");
                            break;
                        }

                        System.Threading.Thread.Sleep(1000 / framesPerSecond);
                    }
                }
            }
        }
    }
}
