#if HAS_SOLIDEDGE
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidEdge.Framework.Interop;

namespace TecnoSpace3D
{
    public sealed class SolidEdgeAdapter : ICadAdapter
    {
        private SolidEdge.Framework.Interop.Application _seApp;
        private bool _inDynamics;
        private DateTime _lastActiveUtc = DateTime.MinValue;
        private DateTime _lastTryUtc = DateTime.MinValue;

        public string CadName { get { return "Siemens Solid Edge 2026"; } }
        public bool IsConnected { get; private set; }
        public string StatusText { get; private set; }

        public SolidEdgeAdapter()
        {
            StatusText = "Solid Edge (Fechado)";
        }

        public bool MatchesProcess(string processName)
        {
            return string.Equals(processName, "Edge", StringComparison.OrdinalIgnoreCase);
        }

        public bool EnsureConnected()
        {
            if (_seApp != null)
            {
                try
                {
                    if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 2.0) return IsConnected;
                    _lastTryUtc = DateTime.UtcNow;
                    if (_seApp.Visible) { IsConnected = true; return true; }
                    Detach();
                }
                catch { Detach(); }
            }

            if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 1.5) return false;
            _lastTryUtc = DateTime.UtcNow;

            try
            {
                object obj = Marshal.GetActiveObject("SolidEdge.Application");
                _seApp = obj as SolidEdge.Framework.Interop.Application;
                if (_seApp != null && _seApp.Visible)
                {
                    IsConnected = true;
                    StatusText = "Solid Edge " + _seApp.Version + " (Ativo)";
                    return true;
                }
            }
            catch { }

            Detach();
            return false;
        }

        public void ApplyMotion(double tx, double ty, double tz, double rx, double ry, double rz, double dt, bool hasMotion)
        {
            if (_seApp == null) return;
            if (!hasMotion && !_inDynamics) return;

            try
            {
                Window win = _seApp.ActiveWindow as Window;
                if (win == null)
                {
                    // SheetWindow (2D Draft) fallback
                    if (hasMotion)
                    {
                        dynamic sheetWin = _seApp.ActiveWindow;
                        if (sheetWin != null)
                        {
                            if (tx != 0.0 || ty != 0.0)
                                sheetWin.Pan((int)Math.Round(tx * 450 * dt), (int)Math.Round(-ty * 450 * dt));
                        }
                    }
                    _inDynamics = false;
                    return;
                }

                View view = win.View;
                if (view == null) { _inDynamics = false; return; }

                if (hasMotion)
                {
                    _lastActiveUtc = DateTime.UtcNow;
                    if (!_inDynamics)
                    {
                        view.BeginCameraDynamics();
                        _inDynamics = true;
                    }

                    double ex, ey, ez, gx, gy, gz, ux, uy, uz, scaleOrAngle;
                    bool persp;
                    view.GetCamera(out ex, out ey, out ez, out gx, out gy, out gz, out ux, out uy, out uz, out persp, out scaleOrAngle);

                    double dist = Math.Sqrt((gx - ex) * (gx - ex) + (gy - ey) * (gy - ey) + (gz - ez) * (gz - ez));
                    double refSpan = (!persp && scaleOrAngle > 1e-6) ? (0.35 / scaleOrAngle) : Math.Max(0.05, dist * 0.6);

                    CameraMath3D.OrbitAndPan(
                        ref ex, ref ey, ref ez,
                        ref gx, ref gy, ref gz,
                        ref ux, ref uy, ref uz,
                        tx * refSpan * dt, ty * refSpan * dt,
                        rx * 2.2 * dt, ry * 2.2 * dt, rz * 2.0 * dt);

                    if (tz != 0.0)
                    {
                        double zf = Math.Exp(-tz * 1.55 * dt);
                        if (!persp) scaleOrAngle *= zf;
                        else
                        {
                            ex = gx + (ex - gx) / zf;
                            ey = gy + (ey - gy) / zf;
                            ez = gz + (ez - gz) / zf;
                        }
                    }

                    view.SetCamera(ex, ey, ez, gx, gy, gz, ux, uy, uz, persp, scaleOrAngle);
                }
                else if (_inDynamics && (DateTime.UtcNow - _lastActiveUtc).TotalMilliseconds > 120)
                {
                    view.EndCameraDynamics();
                    view.Update();
                    _inDynamics = false;
                }
            }
            catch { _inDynamics = false; }
        }

        public void ExecuteAction(string action)
        {
            if (_seApp == null) return;
            try
            {
                Window win = _seApp.ActiveWindow as Window;
                if (win != null && win.View != null)
                {
                    if (action == "ZoomToFit") { win.View.Fit(); return; }
                }

                switch (action)
                {
                    case "Isometric": _seApp.StartCommand((SolidEdgeCommandConstants)32823); break;
                    case "Front": _seApp.StartCommand((SolidEdgeCommandConstants)32824); break;
                    case "Top": _seApp.StartCommand((SolidEdgeCommandConstants)32826); break;
                    case "Right": _seApp.StartCommand((SolidEdgeCommandConstants)32827); break;
                    case "Undo": _seApp.StartCommand((SolidEdgeCommandConstants)57643); break;
                }
            }
            catch { }
        }

        public void Detach()
        {
            _seApp = null;
            _inDynamics = false;
            IsConnected = false;
        }
    }
}
#endif
