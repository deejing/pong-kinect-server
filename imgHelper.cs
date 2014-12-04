using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Emgu.CV;

namespace DepthViewer
{
    public static class imgHelper
    {
        private const int MaxDepthDistance = 4000;
        private const int MinDepthDistance = 850;
        private const int MaxDepthDistanceOffset = 3150;

        public static BitmapSource SliceDepthImage( this DepthImageFrame image, int min = 20, int max = 1000 ) 
        {
            int width = image.Width;
            int height = image.Height;

            var pixels = new byte[height * width * 4];

            const int B = 0;
            const int G = 1;
            const int R = 2;

            short[] rawDepthData = new short[image.PixelDataLength];
            image.CopyPixelDataTo(rawDepthData);
            

            for (int depthIndex = 0, colorIndex = 0; 
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length; 
                depthIndex++, colorIndex += 4) 
            {
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                var intensity = CalculateIntensityFromDistance(depth);

                if (depth > min && depth < max)
                {
                    pixels[colorIndex + B] = intensity;
                    pixels[colorIndex + G] = intensity;
                    pixels[colorIndex + R] = intensity;
                }
            }

            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        }

        public static Byte CalculateIntensityFromDistance(int distance)
        {
            int newMax = distance - MinDepthDistance;

            if (newMax > 0)
            {
                return (byte)(255 - (255 * newMax / (MaxDepthDistanceOffset)));
            }
            else
            {
                return (byte)255;
            }
        }

        public static System.Drawing.Bitmap ToBitmap(this BitmapSource src) 
        {
            System.Drawing.Bitmap bitmap;
            using (var outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(src));
                encoder.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
                return bitmap;
            }
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        /**
         * 
         */
        public static BitmapSource ToBitmapSource(IImage img)
        {
            using (System.Drawing.Bitmap src = img.Bitmap) 
            {
                IntPtr ptr = src.GetHbitmap();

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr);
                return bs;
            }
        }
    }

    
}
