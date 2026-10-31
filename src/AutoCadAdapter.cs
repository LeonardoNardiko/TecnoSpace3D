#if HAS_AUTOCAD
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Interop;
using Autodesk.AutoCAD.Interop.Common;

namespace TecnoSpace3D
{
    public sealed class AutoCadAdapter : ICadAdapter
    {
        private IAcadApplication _acadApp;
        private DateTime _lastTryUtc = DateTime.MinValue;

        public string CadName { get { return "Autodesk AutoCAD 2026"; } }
        public bool IsConnected { get; private set; }
        public string StatusText { get; private set; }

        public AutoCadAdapter()
        {
            StatusText = "AutoCAD (Fechado)";
        }

        public bool MatchesProcess(string processName)
        {
            return string.Equals(processName, "acad", StringComparison.OrdinalIgnoreCase);
        }

        public bool EnsureConnected()
        {
            if (_acadApp != null)
            {
                try
                {
                    if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 2.0) return IsConnected;
                    _lastTryUtc = DateTime.UtcNow;
                    if (_acadApp.Visible) { IsConnected = true; return true; }
                    Detach();
                }
                catch { Detach(); }
            }

            if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 1.5) return false;
            _lastTryUtc = DateTime.UtcNow;

            string[] progIds = { "AutoCAD.Application.25.1", "AutoCAD.Application.25", "AutoCAD.Application" };
            foreach (string pid in progIds)
            {
                try
                {
                    object obj = Marshal.GetActiveObject(pid);
                    _acadApp = obj as IAcadApplication;
                    if (_acadApp != null && _acadApp.Visible)
                    {
                        IsConnected = true;
                        StatusText = "AutoCAD 2026 (Ativo)";
                        return true;
                    }
                }
                catch { }
            }

            Detach();
            return false;
        }

        public void ApplyMotion(double tx, double ty, double tz, double rx, double ry, double rz, double dt, bool hasMotion)
        {
            if (!hasMotion || _acadApp == null) return;
            try
            {
                AcadDocument doc = _acadApp.ActiveDocument;
                if (doc == null) return;

                double[] ctr = (double[])doc.GetVariable("VIEWCTR");
                double vsize = Convert.ToDouble(doc.GetVariable("VIEWSIZE"));
                if (vsize < 1e-9) vsize = 100.0;

                if (rx != 0.0 || ry != 0.0)
                {
                    AcadViewport vp = doc.ActiveViewport;
                    if (vp != null)
                    {
                        double[] dir = (double[])vp.Direction;
                        double dx = dir[0], dy = dir[1], dz = dir[2];
                        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (len < 1e-6) { dz = 1.0; len = 1.0; }

                        // Yaw around global Z & Pitch around horizontal right
                        CameraMath3D.RotateAroundAxis(ref dx, ref dy, ref dz, 0, 0, 1, -ry * 2.0 * dt);
                        double rvx = -dy, rvy = dx, rvz = 0;
                        double rlen = Math.Sqrt(rvx * rvx + rvy * rvy);
                        if (rlen > 1e-6)
                        {
                            rvx /= rlen; rvy /= rlen;
                            CameraMath3D.RotateAroundAxis(ref dx, ref dy, ref dz, rvx, rvy, rvz, -rx * 1.8 * dt);
                        }
                        vp.Direction = new double[] { dx, dy, dz };
                        doc.ActiveViewport = vp;
                    }
                }

                if (tx != 0.0 || ty != 0.0 || tz != 0.0)
                {
                    ctr[0] -= tx * vsize * 0.65 * dt;
                    ctr[1] -= ty * vsize * 0.65 * dt;
                    double newVsize = vsize * Math.Exp(tz * 1.55 * dt);
                    _acadApp.ZoomCenter(ctr, newVsize);
                }
            }
            catch { }
        }

        public void ExecuteAction(string action)
        {
            if (_acadApp == null) return;
            try
            {
                AcadDocument doc = _acadApp.ActiveDocument;
                if (doc == null) return;
                switch (action)
                {
                    case "ZoomToFit": _acadApp.ZoomExtents(); break;
                    case "Top": doc.SendCommand("_-VIEW _TOP\n"); break;
                    case "Front": doc.SendCommand("_-VIEW _FRONT\n"); break;
                    case "Right": doc.SendCommand("_-VIEW _RIGHT\n"); break;
                    case "Left": doc.SendCommand("_-VIEW _LEFT\n"); break;
                    case "Back": doc.SendCommand("_-VIEW _BACK\n"); break;
                    case "Bottom": doc.SendCommand("_-VIEW _BOTTOM\n"); break;
                    case "Isometric": doc.SendCommand("_-VIEW _SWISO\n"); break;
                    case "Measure": doc.SendCommand("_DIST\n"); break;
                    case "Undo": doc.SendCommand("_U\n"); break;
                    case "Rebuild": doc.Regen(AcRegenType.acAllViewports); break;
                }
            }
            catch { }
        }

        public void Detach()
        {
            _acadApp = null;
            IsConnected = false;
        }
    }
}
#endif
