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
        public Form1()
        {
            InitializeComponent();
        }
      

        private void button1_Click(object sender, EventArgs e)
        {
            Gst.Application.Init();
            Gst.Element pipeline;
            //string command = "videotestsrc pattern=ball ! queue ! x264enc ! rtph264pay ! queue ! decodebin ! autovideosink";
            string command = "rtspsrc location = rtsp://140.130.20.168:8555/RTSP0002 latency=0 ! application/x-rtp,encoding-name=H264,payload=96 ! rtph264depay ! decodebin ! autovideosink"; 
            pipeline = Gst.Parse.Launch(command);
            pipeline.SetState(Gst.State.Playing);
        }
    }
}
