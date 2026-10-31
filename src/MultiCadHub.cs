using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TecnoSpace3D
{
    public sealed class MultiCadHub : IDisposable
    {
        private readonly SpaceBallHid _hid;
        private readonly AppConfig _cfg;
        private readonly ICadAdapter[] _adapters;
        private readonly ConcurrentQueue<string> _cmdQueue = new ConcurrentQueue<string>();

        private Thread _staThread;
        private volatile bool _running;
        private ICadAdapter _lastActiveAdapter;
        private UdpClient _udpBlender;
        private readonly IPEndPoint _udpEp = new IPEndPoint(IPAddress.Loopback, 39030);

        private IntPtr _cachedFgHwnd = IntPtr.Zero;
        private string _cachedFgName = "";
        private DateTime _lastIdleScanUtc = DateTime.MinValue;
        private double _sTx, _sTy, _sTz, _sRx, _sRy, _sRz;

        public volatile bool IsAnyCadAttached;
        public string ActiveStatusText = "Aguardando abrir CAD...";
        public double NormTx, NormTy, NormTz, NormRx, NormRy, NormRz;
        public event Action ConfigMutatedByButton;

        public MultiCadHub(SpaceBallHid hid, AppConfig cfg)
        {
            _hid = hid;
            _cfg = cfg;
            var list = new System.Collections.Generic.List<ICadAdapter>();
#if HAS_SW
            list.Add(new SwAdapter());
#endif
#if HAS_INVENTOR
            list.Add(new InventorAdapter());
#endif
#if HAS_SOLIDEDGE
            list.Add(new SolidEdgeAdapter());
#endif
#if HAS_AUTOCAD
            list.Add(new AutoCadAdapter());
#endif
            _adapters = list.ToArray();
            _hid.ButtonChanged += OnHidButton;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _udpBlender = new UdpClient();
            _staThread = new Thread(StaLoop)
            {
                IsBackground = true,
                Name = "MultiCadStaRouter120Hz",
                Priority = ThreadPriority.AboveNormal
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_staThread != null && _staThread.IsAlive)
                _staThread.Join(600);
            if (_udpBlender != null) { try { _udpBlender.Close(); } catch { } }
        }

        private void OnHidButton(int btnIndex, bool pressed)
        {
            if (!pressed || btnIndex < 0 || btnIndex >= 12) return;
            string act = _cfg.ButtonActions[btnIndex];
            if (!string.IsNullOrEmpty(act) && act != "None")
                _cmdQueue.Enqueue(act);
        }

        private void StaLoop()
        {
            timeBeginPeriod(1);
            try
            {
                Stopwatch clock = Stopwatch.StartNew();
                double prevSec = clock.Elapsed.TotalSeconds;

                while (_running)
                {
                    double nowSec = clock.Elapsed.TotalSeconds;
                    double dt = nowSec - prevSec;
                    prevSec = nowSec;
                    if (dt <= 0.0005 || dt > 0.12) dt = 0.008;

                    int rTx, rTy, rTz, rRx, rRy, rRz;
                    ushort btns;
                    _hid.GetCalibratedAxes(out rTx, out rTy, out rTz, out rRx, out rRy, out rRz, out btns);

                    double dz = _cfg.Deadzone;
                    double ex = _cfg.ExpoCurve;
                    double ms = _cfg.MasterSpeed;

                    double rawTx = CameraMath3D.ProcessAxis(rTx, 350.0, dz, ex, ms * _cfg.PanSpeed * (_cfg.InvertTx ? -1.0 : 1.0));
                    double rawTy = CameraMath3D.ProcessAxis(rTy, 350.0, dz, ex, ms * _cfg.PanSpeed * (_cfg.InvertTy ? -1.0 : 1.0));
                    double rawTz = CameraMath3D.ProcessAxis(rTz, 350.0, dz, ex, ms * _cfg.ZoomSpeed * (_cfg.InvertTz ? -1.0 : 1.0));
                    double rawRx = CameraMath3D.ProcessAxis(rRx, 350.0, dz, ex, ms * _cfg.RotateSpeed * (_cfg.InvertRx ? -1.0 : 1.0));
                    double rawRy = CameraMath3D.ProcessAxis(rRy, 350.0, dz, ex, ms * _cfg.RotateSpeed * (_cfg.InvertRy ? -1.0 : 1.0));
                    double rawRz = CameraMath3D.ProcessAxis(rRz, 350.0, dz, ex, ms * _cfg.RotateSpeed * (_cfg.InvertRz ? -1.0 : 1.0));

                    if (_cfg.SwapPanZoomYZ) { double tmp = rawTy; rawTy = rawTz; rawTz = tmp; }
                    if (!_cfg.RotationEnabled) { rawRx = rawRy = rawRz = 0.0; }
                    if (_cfg.DominantAxis) CameraMath3D.ApplyDominant(ref rawTx, ref rawTy, ref rawTz, ref rawRx, ref rawRy, ref rawRz);

                    double alpha = 1.0 - Math.Max(0.0, Math.Min(0.85, _cfg.Smoothing));
                    _sTx = SmoothAxis(_sTx, rawTx, alpha);
                    _sTy = SmoothAxis(_sTy, rawTy, alpha);
                    _sTz = SmoothAxis(_sTz, rawTz, alpha);
                    _sRx = SmoothAxis(_sRx, rawRx, alpha);
                    _sRy = SmoothAxis(_sRy, rawRy, alpha);
                    _sRz = SmoothAxis(_sRz, rawRz, alpha);

                    double tx = _sTx, ty = _sTy, tz = _sTz, rx = _sRx, ry = _sRy, rz = _sRz;
                    NormTx = tx; NormTy = ty; NormTz = tz;
                    NormRx = rx; NormRy = ry; NormRz = rz;

                    string fgProc = GetForegroundProcessNameFast();

                    if (string.Equals(fgProc, "FreeCAD", StringComparison.OrdinalIgnoreCase))
                    {
                        IsAnyCadAttached = true;
                        ActiveStatusText = "FreeCAD 1.1.4 (HID Nativo Direto Ativo)";
                        DrainLocalButtonsOnly(fgProc);
                        Thread.Sleep(20);
                        continue;
                    }

                    if (string.Equals(fgProc, "blender", StringComparison.OrdinalIgnoreCase))
                    {
                        IsAnyCadAttached = true;
                        ActiveStatusText = "Blender 5.1 (Stream UDP 120Hz Ativo)";
                        bool hasM = (tx != 0 || ty != 0 || tz != 0 || rx != 0 || ry != 0 || rz != 0);
                        if (hasM) SendBlenderMotion(tx, ty, tz, rx, ry, rz);
                        DrainBlenderButtons();
                        Thread.Sleep(hasM ? 8 : 20);
                        continue;
                    }

                    ICadAdapter target = ResolveActiveAdapter(fgProc);
                    bool hasMotion = (tx != 0.0 || ty != 0.0 || tz != 0.0 || rx != 0.0 || ry != 0.0 || rz != 0.0);

                    string cmd;
                    while (_cmdQueue.TryDequeue(out cmd))
                    {
                        if (!HandleGlobalAction(cmd) && target != null && target.IsConnected)
                            target.ExecuteAction(cmd);
                    }

                    if (target != null && target.IsConnected)
                    {
                        IsAnyCadAttached = true;
                        ActiveStatusText = target.StatusText;
                        target.ApplyMotion(tx, ty, tz, rx, ry, rz, dt, hasMotion);
                    }
                    else
                    {
                        IsAnyCadAttached = false;
                        ActiveStatusText = "Abra SolidWorks, Inventor, Solid Edge, AutoCAD, FreeCAD ou Blender";
                    }

                    Thread.Sleep(hasMotion ? 8 : 20);
                }
            }
            finally
            {
                timeEndPeriod(1);
            }
        }

        private static double SmoothAxis(double current, double target, double alpha)
        {
            if (target == 0.0 && Math.Abs(current) < 0.008) return 0.0;
            double next = current + (target - current) * (target == 0.0 ? Math.Min(1.0, alpha * 1.6) : alpha);
            return Math.Abs(next) < 0.002 ? 0.0 : next;
        }

        private ICadAdapter ResolveActiveAdapter(string fgProc)
        {
            if (!string.IsNullOrEmpty(fgProc))
            {
                for (int i = 0; i < _adapters.Length; i++)
                {
                    if (_adapters[i].MatchesProcess(fgProc) && _adapters[i].EnsureConnected())
                    {
                        _lastActiveAdapter = _adapters[i];
                        return _lastActiveAdapter;
                    }
                }
            }

            if (!_cfg.OnlyWhenSwFocused)
            {
                if (_lastActiveAdapter != null && _lastActiveAdapter.EnsureConnected())
                    return _lastActiveAdapter;

                if ((DateTime.UtcNow - _lastIdleScanUtc).TotalSeconds >= 2.0)
                {
                    _lastIdleScanUtc = DateTime.UtcNow;
                    for (int i = 0; i < _adapters.Length; i++)
                    {
                        if (_adapters[i].EnsureConnected())
                        {
                            _lastActiveAdapter = _adapters[i];
                            return _lastActiveAdapter;
                        }
                    }
                }
            }
            return null;
        }

        private bool HandleGlobalAction(string action)
        {
            if (action == "ZeroSensor") { _hid.ZeroSensor(); return true; }
            if (action == "ToggleDominant") { _cfg.DominantAxis = !_cfg.DominantAxis; _cfg.Save(); NotifyMutated(); return true; }
            if (action == "ToggleRotation") { _cfg.RotationEnabled = !_cfg.RotationEnabled; _cfg.Save(); NotifyMutated(); return true; }
            if (action == "SpeedUp") { _cfg.MasterSpeed = Math.Min(3.0, _cfg.MasterSpeed + 0.15); _cfg.Save(); NotifyMutated(); return true; }
            if (action == "SpeedDown") { _cfg.MasterSpeed = Math.Max(0.2, _cfg.MasterSpeed - 0.15); _cfg.Save(); NotifyMutated(); return true; }
            if (action == "Escape") { keybd_event(0x1B, 0, 0, UIntPtr.Zero); keybd_event(0x1B, 0, 2, UIntPtr.Zero); return true; }
            return false;
        }

        private void DrainLocalButtonsOnly(string fgProc) { string cmd; while (_cmdQueue.TryDequeue(out cmd)) HandleGlobalAction(cmd); }
        private void DrainBlenderButtons() { string cmd; while (_cmdQueue.TryDequeue(out cmd)) if (!HandleGlobalAction(cmd)) SendUdpLine("CMD " + cmd); }
        private void SendBlenderMotion(double tx, double ty, double tz, double rx, double ry, double rz)
        {
            SendUdpLine(string.Format(CultureInfo.InvariantCulture, "MOV {0:F4} {1:F4} {2:F4} {3:F4} {4:F4} {5:F4}", tx, ty, tz, rx, ry, rz));
        }
        private void SendUdpLine(string s) { try { byte[] b = Encoding.ASCII.GetBytes(s); _udpBlender.Send(b, b.Length, _udpEp); } catch { } }
        private void NotifyMutated() { if (ConfigMutatedByButton != null) try { ConfigMutatedByButton(); } catch { } }

        private string GetForegroundProcessNameFast()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return "";
            if (fg == _cachedFgHwnd) return _cachedFgName;

            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            if (pid == 0) return "";

            string resolved = "";
            IntPtr hProc = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(512);
                    uint sz = (uint)sb.Capacity;
                    if (QueryFullProcessImageNameW(hProc, 0, sb, ref sz))
                        resolved = Path.GetFileNameWithoutExtension(sb.ToString());
                }
                finally { CloseHandle(hProc); }
            }
            _cachedFgHwnd = fg;
            _cachedFgName = resolved ?? "";
            return _cachedFgName;
        }

        public void Dispose() { Stop(); }

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint uPeriod);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
