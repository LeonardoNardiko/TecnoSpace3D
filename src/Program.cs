using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TecnoSpace3D
{
    internal sealed class HubAppContext : ApplicationContext
    {
        private readonly MainForm _form;
        private readonly EventWaitHandle _showEvt;
        private readonly EventWaitHandle _quitEvt;
        private Thread _ipcThread;
        private volatile bool _alive = true;

        public HubAppContext(bool startMin, EventWaitHandle showEvt, EventWaitHandle quitEvt)
        {
            _showEvt = showEvt;
            _quitEvt = quitEvt;
            _form = new MainForm(startMin, ExitHub);
            if (!startMin)
                _form.Show();

            _ipcThread = new Thread(IpcWaitLoop) { IsBackground = true, Name = "SingleInstanceIpc" };
            _ipcThread.Start();
        }

        private void IpcWaitLoop()
        {
            WaitHandle[] handles = new WaitHandle[] { _showEvt, _quitEvt };
            while (_alive)
            {
                int idx = WaitHandle.WaitAny(handles, 500);
                if (!_alive) break;
                if (idx == 0)
                {
                    try { _form.BeginInvoke(new Action(_form.RestoreToFront)); } catch { }
                }
                else if (idx == 1)
                {
                    try { _form.BeginInvoke(new Action(_form.ForceQuit)); } catch { }
                    break;
                }
            }
        }

        private void ExitHub()
        {
            _alive = false;
            ExitThread();
        }
    }

    internal static class Program
    {
        private const string MutexName = "Local\\TecnoSpace3D_SingleInstance_Mutex";
        private const string ShowEventName = "Local\\TecnoSpace3D_ShowWindow_Event";
        private const string QuitEventName = "Local\\TecnoSpace3D_QuitApp_Event";

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatal(e.ExceptionObject);
            Application.ThreadException += (s, e) => LogFatal(e.Exception);

            bool startMin = false;
            bool reqQuit = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase))
                        startMin = true;
                    else if (string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase))
                        reqQuit = true;
                }
            }

            using (var showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
            using (var quitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName))
            {
                if (reqQuit)
                {
                    quitEvt.Set();
                    return;
                }

                bool createdNew;
                using (var mutex = new Mutex(true, MutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        // Ja esta rodando na bandeja -> traz a janela existente para frente!
                        showEvt.Set();
                        return;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new HubAppContext(startMin, showEvt, quitEvt));
                }
            }
        }

        private static void LogFatal(object ex)
        {
            try
            {
                if (!Directory.Exists(AppConfig.ConfigDir))
                    Directory.CreateDirectory(AppConfig.ConfigDir);
                File.AppendAllText(Path.Combine(AppConfig.ConfigDir, "crash.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
