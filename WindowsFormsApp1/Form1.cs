using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gst;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {

        internal enum videosinktype { glimagesink, d3dvideosink, dshowvideosink, directdrawsink }

        public Control videodisplay;
        public Button startVideoButton;

        static System.Threading.Thread mMainGlibThread;
        static GLib.MainLoop mMainLoop;  // GLib's Main Loop

        private const videosinktype mCfgVideosinkType = videosinktype.d3dvideosink;
        private ulong mHandle;
        private Gst.Video.VideoSink mGlImageSink;
        private Gst.Pipeline mCurrentPipeline = null;
        public Form1()
        {
            InitializeComponent();
            videodisplay = new Control() { BackColor = Color.Black };
            startVideoButton = new Button() { Text = "Start", AutoSize = true };

            this.SuspendLayout();
            this.Controls.Add(videodisplay);
            this.Controls.Add(startVideoButton);
            this.ResumeLayout();

            startVideoButton.Click += new System.EventHandler(startVideo_Click);

            ResizeControls();
        }
        protected override void OnShown(System.EventArgs e)
        {
            //    Let Form paint Controls before long idle time of gstreamerinitialisation
            System.Windows.Forms.Application.DoEvents();
            //    Assign Handle to prevent Cross-Threading Access
            mHandle = (ulong)videodisplay.Handle;
            //    Initialize Gstreamer
            InitializeGstreamer();
            //    Initialize Mainloop
            InitializeMainloop();

            base.OnShown(e);
        }
        private void startVideo_Click(object sender, System.EventArgs e)
        {
            InitGStreamerPipeline();
        }
        private void ResizeControls()
        {
            //    Terrible, i know. But: No real-world application, just forbugreporting

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            int displayH = h - (startVideoButton.Height * 2);
            int buttonT = displayH + (startVideoButton.Height / 2);
            if (displayH < 0) { displayH = 0; }

            if (videodisplay.Top != 0) { videodisplay.Top = 0; }
            if (videodisplay.Left != 0) { videodisplay.Left = 0; }
            if (videodisplay.Width != w) { videodisplay.Width = w; }
            if (videodisplay.Height != displayH)
            {
                videodisplay.Height = displayH;
            };

            if (startVideoButton.Top != buttonT)
            {
                startVideoButton.Top = buttonT;
            }
            if (startVideoButton.Left != 0) { startVideoButton.Left = 0; }
        }
        private void InitializeGstreamer()
        {
            //    gst_init
            Gst.Application.Init();
            //    initialize objectmanager. otherwise bus subscription will fail
            GtkSharp.GstreamerSharp.ObjectManager.Initialize();
        }
        private void InitializeMainloop()
        {
            mMainLoop = new GLib.MainLoop();
            mMainGlibThread = new System.Threading.Thread(mMainLoop.Run);
            mMainGlibThread.Start();
        }
        private void InitGStreamerPipeline()
        {
            //#region BuildPipeline   
            switch (mCfgVideosinkType)
            {
                case videosinktype.glimagesink:
                    mGlImageSink = (Gst.Video.VideoSink)Gst.ElementFactory.Make("glimagesink", "glimagesink");
                    break;
                case videosinktype.d3dvideosink:
                    mGlImageSink = (Gst.Video.VideoSink)Gst.ElementFactory.Make("d3dvideosink", "d3dvideosink");
                    //mGlImageSink = (Gst.Video.VideoSink)Gst.ElementFactory.Make("dshowvideosink", "dshowvideosink");
                    break;
                case videosinktype.dshowvideosink:
                    mGlImageSink = (Gst.Video.VideoSink)Gst.ElementFactory.Make("dshowvideosink", "dshowvideosink");
                    break;
                case videosinktype.directdrawsink:
                    mGlImageSink = (Gst.Video.VideoSink)Gst.ElementFactory.Make("directdrawsink", "directdrawsink");
                    break;
                default:
                    break;
            }

            Gst.Element pipeline;
            //string command = "videotestsrc pattern=ball ! queue ! x264enc ! rtph264pay ! queue ! decodebin ! autovideosink";
            //string command = "rtspsrc location = rtsp://140.130.20.168:8554/RTSP0001 latency=0 ! application/x-rtp,encoding-name=H264,payload=96 ! rtph264depay ! decodebin ! autovideosink";
            string command = "rtspsrc location = rtsp://140.130.20.168:8554/RTSP0001 latency=0 ! application/x-rtp,encoding-name=H264,payload=96 ! rtph264depay ! decodebin ! autovideosink";
            pipeline = Gst.Parse.Launch(command);

            mCurrentPipeline = new Gst.Pipeline("pipeline");
            mCurrentPipeline.Add(pipeline);


            //subscribe to bus & bussync msgs

            SubscribeBusMessage();
            SubscribeBusSyncMessage();

            //play the stream
            var setStateRet = mCurrentPipeline.SetState(State.Null);
            System.Diagnostics.Debug.WriteLine("SetStateNULL returned: " + setStateRet.ToString());
            setStateRet = mCurrentPipeline.SetState(State.Ready);
            System.Diagnostics.Debug.WriteLine("SetStateReady returned: " + setStateRet.ToString());
            setStateRet = mCurrentPipeline.SetState(Gst.State.Playing);
        }
        private void SubscribeBusMessage()
        {
            Bus bus = mCurrentPipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += HandleMessage;
        }
        private void SubscribeBusSyncMessage()
        {
            Bus bus = mCurrentPipeline.Bus;
            bus.EnableSyncMessageEmission();
            bus.SyncMessage += new SyncMessageHandler(bus_SyncMessage);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Indeed the application needs to set its Window identifier at the right time to avoid internal Window creation
        /// from the video sink element. To solve this issue a GstMessage is posted on the bus to inform the application
        /// that it should set the Window identifier immediately.
        /// 
        /// API: http://gstreamer.freedesktop.org/data/doc/gstreamer/head/gst-plugins-base-libs/html/gst-plugins-base-libs-gstvideooverlay.html
        /// </remarks>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void bus_SyncMessage(object o, SyncMessageArgs args)
        {
            //Convenience function to check if the given message is a "prepare-window-handle" message from a GstVideoOverlay.

            System.Diagnostics.Debug.WriteLine("bus_SyncMessage: " + args.Message.Type.ToString());
            if (Gst.Video.Global.IsVideoOverlayPrepareWindowHandleMessage(args.Message))
            {
                Element src = (Gst.Element)args.Message.Src;

#if DEBUG
                System.Diagnostics.Debug.WriteLine("Message'prepare-window-handle' received by: " + src.Name + " " + src.ToString());
#endif

                if (src != null && (src is Gst.Video.VideoSink | src is Gst.Bin))
                {
                    //    Try to set Aspect Ratio
                    try
                    {
                        src["force-aspect-ratio"] = true;
                    }
                    catch (PropertyNotFoundException) { }

                    //    Try to set Overlay
                    try
                    {
                        Gst.Video.VideoOverlayAdapter overlay_ = new Gst.Video.VideoOverlayAdapter(src.Handle);
                        overlay_.WindowHandle = (IntPtr)mHandle;
                        overlay_.HandleEvents(true);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Exception thrown: " + ex.Message); }
                }
            }
        }

        private void HandleMessage(object o, MessageArgs args)
        {
            var msg = args.Message;
            //System.Diagnostics.Debug.WriteLine("HandleMessage received msg of type: {0}", msg.Type);
            switch (msg.Type)
            {
                case MessageType.Error:
                    //
                    GLib.GException err;
                    string debug;
                    System.Diagnostics.Debug.WriteLine("Error received: " + msg.ToString());
                    //msg.ParseError (out err, out debug);
                    //if(debug == null) { debug = "none"; }
                    //System.Diagnostics.Debug.WriteLine ("Error received from element {0}: {1}", msg.Src, err.Message);
                    //System.Diagnostics.Debug.WriteLine ("Debugging information: "+ debug);
                    break;
                case MessageType.StreamStatus:
                    Gst.StreamStatusType status;
                    Element theOwner;
                    msg.ParseStreamStatus(out status, out theOwner);
                    System.Diagnostics.Debug.WriteLine("Case SteamingStatus: status is: " + status + " ; Ownder is: " + theOwner.Name);
                    break;
                case MessageType.StateChanged:
                    //System.Diagnostics.Debug.WriteLine("Case StateChanged: " + args.Message.ToString());
                    State oldState, newState, pendingState;
                    msg.ParseStateChanged(out oldState, out newState, out pendingState);
                    if (newState == State.Paused)
                        args.RetVal = false;
                    System.Diagnostics.Debug.WriteLine("Pipeline state changed from {0} to {1}: ; Pending: {2}", Element.StateGetName(oldState), Element.StateGetName(newState), Element.StateGetName(pendingState));
                    break;
                case MessageType.Element:
                    System.Diagnostics.Debug.WriteLine("Element message: {0}", args.Message.ToString());
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine("HandleMessage received msg of type: {0}", msg.Type);
                    break;
            }
            args.RetVal = true;
        }


        /*private void button1_Click(object sender, EventArgs e)
        {
            Gst.Application.Init();
            Gst.Element pipeline;
            //string command = "videotestsrc pattern=ball ! queue ! x264enc ! rtph264pay ! queue ! decodebin ! autovideosink";
            //string command = "rtspsrc location = rtsp://140.130.20.168:8554/RTSP0001 latency=0 ! application/x-rtp,encoding-name=H264,payload=96 ! rtph264depay ! decodebin ! autovideosink";
            string command = "rtspsrc location = rtsp://140.130.20.168:8554/RTSP0001 latency=0 ! application/x-rtp,encoding-name=H264,payload=96 ! rtph264depay ! decodebin ! autovideosink"; 
            pipeline = Gst.Parse.Launch(command);
            pipeline.SetState(Gst.State.Playing);
        } */
    }
}
