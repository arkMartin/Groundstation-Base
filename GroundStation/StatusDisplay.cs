using System;
using System.Windows.Forms;
using System.Drawing;

using System.Threading;

namespace Astrocast.GroundSegment.GroundStation
{
    public class Lamps: Form
    {
        private static System.Drawing.Graphics formGraphics;
        public enum State {free, occupied};
        private static State statusVHFUHF, statusSband;
        
        public delegate void DisplayUsage();

        public static void M1()
        {
            System.Drawing.SolidBrush myBrush;

            System.Drawing.SolidBrush myGreenBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Green);
            System.Drawing.SolidBrush myRedBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
            System.Drawing.SolidBrush myBlackBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

            Rectangle VHFUHFRectangle = new Rectangle(4, 4, 200, 200);
            if (statusVHFUHF == State.free)
            {
                myBrush = myGreenBrush;
            }
            else
            {
                myBrush = myRedBrush;
            }
            Lamps.formGraphics.FillRectangle(myBrush, VHFUHFRectangle);
            formGraphics.DrawRectangle(new Pen(Color.Black, 4.0f), VHFUHFRectangle);

            if (statusSband == State.free)
            {
                myBrush = myGreenBrush;
            }
            else
            {
                myBrush = myRedBrush;
            }
            Rectangle SbandRectangle = new Rectangle(200, 4, 200, 200);
            Lamps.formGraphics.FillRectangle(myBrush, SbandRectangle);
            formGraphics.DrawRectangle(new Pen(Color.Black, 4.0f), rect: SbandRectangle);

            Lamps.formGraphics.DrawString("\n\n     VHFUHF", new Font("Arial", 16), myBlackBrush, VHFUHFRectangle);
            Lamps.formGraphics.DrawString("\n\n     S-Band", new Font("Arial", 16), myBlackBrush, SbandRectangle);
        }

        public DisplayUsage Display = new DisplayUsage(M1);

        public State StatusVHFUHF { get => statusVHFUHF; set => statusVHFUHF = value; }
        public State StatusSband { get => statusSband; set => statusSband = value; }

        public Lamps()
        {
            StatusVHFUHF = State.free;
            StatusSband = State.free;
        }

        ~Lamps()
        {
            Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (this != null)
            {
                Display();
            }
        }

        public void StatusDisplay() {
            Text = "current use of groundstation equipment at HB9HSLU ";
            Width = 420;
            Height = 280;
            
            formGraphics = CreateGraphics();
            formGraphics = CreateGraphics();
            Application.Run(this);
        }
    }

    public class StatusDisplay
    {
        private Lamps lampe;
        ThreadStart threadDelegate;
        Thread newThread;

        public StatusDisplay()
        {
           lampe = new Lamps();
           threadDelegate = new ThreadStart(lampe.StatusDisplay);
           newThread = new Thread(threadDelegate);
           newThread.Start();
        }

        ~StatusDisplay()
        {
            newThread.Abort();
            lampe.Dispose();
        }

        public void SetVHFUHFstate(Lamps.State s) {
            lampe.StatusVHFUHF = s;
            lampe.Display();
        }

        public Lamps.State GetVHFUHFstate() => lampe.StatusVHFUHF;

        public void SetSbandstate(Lamps.State s) {
            lampe.StatusSband = s;
            lampe.Display();
        }

        public Lamps.State GetSbandstate() => lampe.StatusSband;

    }
}
