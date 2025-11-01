using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;

namespace Av1ador
{
    public static class Globals
    {
        public static double overhead = 1.025;
    }

    internal static class Program
    {
        public static bool Log { get; set; }
        private static Mutex mutex = null;

        [STAThread]
        static void Main()
        {
            const string appMutex = "Av1adorSingleInstance";
            mutex = new Mutex( true, appMutex, out bool createdNew );

            if( !createdNew )
            {
                MessageBox.Show("Av1ador is already running! Only one instance is allowed.", "Error: Application already running",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            if( !Debugger.IsAttached )
            {
                AppDomain.CurrentDomain.UnhandledException += AllUnhandledExceptions;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            GC.KeepAlive( mutex );
        }
        private static void AllUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            if (Log)
                System.IO.File.WriteAllText("log_" + string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now) + ".txt", ex.ToString());
            Environment.Exit(System.Runtime.InteropServices.Marshal.GetHRForException(ex));
        }
    }
}
