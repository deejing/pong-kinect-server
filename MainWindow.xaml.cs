using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.IO;
using System.Drawing;
using System.Web;
using Microsoft.Kinect;
using Fleck;
using System.Diagnostics;
using System.Threading;


namespace DepthViewer
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor;
        WriteableBitmap depthBitmap;
        WriteableBitmap colorBitmap;
        DepthImagePixel[] depthPixels;
        WebSocketServer server;
        static List<IWebSocketConnection> _theSocket;

        private Thread myThread;

        private JsonHelper.JSONBlobsCollection blobQueue = new JsonHelper.JSONBlobsCollection
        {
            Blobs = new Queue<JsonHelper.JSONBlobs>()
        }; 

        static bool _isSocket = false;
        byte[] colorPixels;
        int blobCount = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        // window onload
        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            //this.sensor = KinectSensor.KinectSensors.SingleOrDefault();
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            this.initKinect();
        }

        // wondow on close
        private void Window_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        { 
            if (this.sensor != null)
            {
                this.sensor.Stop();
            }


            if (_isSocket == true)
            {
                this.server.Dispose();
            }
            
            if (this.myThread != null)
            {
                this.myThread.Abort();
            }
        }

        private void initKinect() 
        { 
            
            if (this.sensor != null) 
            {
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                int fWidth = this.sensor.ColorStream.FrameWidth;
                int fHeight = this.sensor.ColorStream.FrameHeight;

                this.colorBitmap = new WriteableBitmap(fWidth, fHeight, 96, 96, PixelFormats.Bgr32, null);

                int dWidth = this.sensor.DepthStream.FrameWidth;
                int dHeight = this.sensor.DepthStream.FrameHeight;

                this.depthBitmap = new WriteableBitmap(dWidth, dHeight, 96, 96, PixelFormats.Bgr32, null);

                this.colorImage.Source = this.colorBitmap;

                this.sensor.AllFramesReady += this.sensor_AllFramesReady;

                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null; 
                }
            }
        }

        /**
         * 
         */
        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            BitmapSource dBmp = null;
            blobCount = 0;

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) 
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null) 
                    {
                        blobCount = 0;

                        dBmp = depthFrame.SliceDepthImage((int)this.sliderMin.Value, (int)this.sliderMax.Value);

                        Image<Bgr, Byte> openCVImg = new Image<Bgr, Byte>(dBmp.ToBitmap());
                        Image<Gray, Byte> greyImg = openCVImg.Convert<Gray, Byte>();

                        using (MemStorage stor = new MemStorage())
                        {
                            Contour<System.Drawing.Point> contours = greyImg.FindContours(
                                Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                                stor);

                            List<JsonHelper.JSONBlobs> data = new List<JsonHelper.JSONBlobs>();

                            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_SIMPLEX, 0.5, 0.5);

                            for (var i = 0; contours != null; contours = contours.HNext)
                            {
                                i++;
                                if ((contours.Area > Math.Pow((int)this.sliderMinBlob.Value, 2)) && (contours.Area < Math.Pow((int)this.sliderMaxBlob.Value, 2)))
                                {
                                    MCvBox2D box = contours.GetMinAreaRect();
                                    openCVImg.Draw(box, new Bgr(System.Drawing.Color.Magenta), 2);

                                    int x = contours.BoundingRectangle.X;
                                    int y = contours.BoundingRectangle.Y;
                                    int width = contours.BoundingRectangle.Width;
                                    int height = contours.BoundingRectangle.Height;

                                    openCVImg.Draw("id: " + blobCount + ", x: " + x.ToString() + ", y: " + y.ToString(), ref font, new System.Drawing.Point(x, y - 10), new Bgr(System.Drawing.Color.Green));

                                    var s = JsonHelper.BlobList(blobCount, x, y, width, height);
                                    this.blobQueue.Blobs.Enqueue(s);
                                    //data.Add(s);
                                    blobCount++;
                                }
                            }

                            // socket send blobs data
                            //if (_isSocket != false) 
                            //{ 
                            //    if (data.Count > 0) 
                            //    {
                            //        string json = data.SerializeBlob();

                            //        foreach (var socket in _theSocket)
                            //        {
                            //            socket.Send(json);
                            //        }
                            //    }
                            //}
                        }

                        this.outImage.Source = imgHelper.ToBitmapSource(openCVImg);
                        this.txtBlobs.Text = this.blobCount.ToString();

                    }
                }

                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        // info
        public void AddInfo(String text) 
        {
            this.inputInfo.AppendText("\r");
            this.inputInfo.AppendText(text);
            this.inputInfo.ScrollToEnd();
        }

        // close widow;
        private void closeBtn(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // drag window
        private void Window_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void threadFunction()
        {
            while(true)
            {
                if(this.blobQueue.Blobs.Count > 0)
                {
                    string json = JsonHelper._Serialize(this.blobQueue.Blobs.Dequeue());

                    foreach (var socket in _theSocket)
                    {
                        if (json != null)
                        {
                            socket.Send(json);
                        }
                    }
                }

            }
        }

        // start/stop socket
        private void socketBnt_Click(object sender, RoutedEventArgs e)
        {
            String socketAdress = "ws://" + this.inputIp.Text;

            if (_isSocket == false)
            {
                _theSocket = new List<IWebSocketConnection>();

                this.server = new WebSocketServer(socketAdress);

                this.server.Start(socket =>
                {
                    //on connect
                    socket.OnOpen = () => {
                        
                        //Console.WriteLine("socket open " + socket.ConnectionInfo.ClientIpAddress);
                        _theSocket.Add(socket);
                    };

                    socket.OnMessage = msg => {

                        Console.WriteLine("msg: " + msg);
                    };

                    // on client close
                    socket.OnClose = () =>
                    {
                        //Console.WriteLine("socket close " + socket.ConnectionInfo.ClientIpAddress);
                        _theSocket.Remove(socket);
                    };

                });

                _isSocket = true;
                AddInfo("socket start " + socketAdress);

                this.myThread = new Thread(this.threadFunction);

                this.myThread.Start();
            }
            else 
            {
                this.server.Dispose();
                
                _isSocket = false;

                AddInfo("socket stop");

                if (this.myThread != null) 
                { 
                    this.myThread.Abort();
                }
                
            }
        }
    }
}
