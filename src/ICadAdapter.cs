using System;

namespace TecnoSpace3D
{
    public interface ICadAdapter
    {
        string CadName { get; }
        bool MatchesProcess(string processName);
        bool EnsureConnected();
        bool IsConnected { get; }
        string StatusText { get; }
        void ApplyMotion(double tx, double ty, double tz, double rx, double ry, double rz, double dt, bool hasMotion);
        void ExecuteAction(string action);
        void Detach();
    }
}
