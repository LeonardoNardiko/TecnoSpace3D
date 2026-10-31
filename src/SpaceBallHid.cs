using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TecnoSpace3D
{
    public sealed class SpaceBallHid : IDisposable
    {
        public event Action<int, bool> ButtonChanged;

        private Thread _worker;
        private volatile bool _running;

        private readonly object _stateLock = new object();
        private short _rawTx, _rawTy, _rawTz;
        private short _rawRx, _rawRy, _rawRz;
        private short _offTx, _offTy, _offTz;
        private short _offRx, _offRy, _offRz;
        private ushort _buttonsMask;

        public volatile bool IsConnected;
        public string ConnectedDeviceName = "Desconectado";

        public void Start()
        {
            if (_running) return;
            _running = true;
            _worker = new Thread(HidLoop) { IsBackground = true, Name = "SpaceBallHidWorker" };
            _worker.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_worker != null && _worker.IsAlive)
                _worker.Join(600);
        }

        public void ZeroSensor()
        {
            lock (_stateLock)
            {
                _offTx = _rawTx; _offTy = _rawTy; _offTz = _rawTz;
                _offRx = _rawRx; _offRy = _rawRy; _offRz = _rawRz;
            }
        }

        public void GetCalibratedAxes(out int tx, out int ty, out int tz, out int rx, out int ry, out int rz, out ushort buttons)
        {
            lock (_stateLock)
            {
                tx = _rawTx - _offTx;
                ty = _rawTy - _offTy;
                tz = _rawTz - _offTz;
                rx = _rawRx - _offRx;
                ry = _rawRy - _offRy;
                rz = _rawRz - _offRz;
                buttons = _buttonsMask;
            }
        }

        private void HidLoop()
        {
            while (_running)
            {
                string devPath, devName;
                if (!FindSpaceBallPath(out devPath, out devName))
                {
                    IsConnected = false;
                    ConnectedDeviceName = "Aguardando USB (SpaceBall 5000)...";
                    Thread.Sleep(1000);
                    continue;
                }

                IntPtr hFile = CreateFileW(devPath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);

                if (hFile == INVALID_HANDLE_VALUE || hFile == IntPtr.Zero)
                {
                    IsConnected = false;
                    ConnectedDeviceName = "Erro ao abrir porta HID";
                    Thread.Sleep(1000);
                    continue;
                }

                IsConnected = true;
                ConnectedDeviceName = devName;
                ReadDeviceLoop(hFile);

                CloseHandle(hFile);
                IsConnected = false;
                lock (_stateLock)
                {
                    _rawTx = _rawTy = _rawTz = _rawRx = _rawRy = _rawRz = 0;
                    _buttonsMask = 0;
                }
            }
        }

        private void ReadDeviceLoop(IntPtr hFile)
        {
            IntPtr hEvent = CreateEvent(IntPtr.Zero, true, false, null);
            IntPtr bufPtr = Marshal.AllocHGlobal(16);
            try
            {
                byte[] packet = new byte[16];
                while (_running)
                {
                    ResetEvent(hEvent);
                    NativeOverlapped ov = new NativeOverlapped { EventHandle = hEvent };
                    uint bytesRead = 0;
                    bool ok = ReadFile(hFile, bufPtr, 7, out bytesRead, ref ov);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err != ERROR_IO_PENDING)
                            break; // Device unplugged

                        uint wait = WaitForSingleObject(hEvent, 120);
                        if (wait == WAIT_TIMEOUT)
                        {
                            CancelIo(hFile);
                            continue;
                        }
                        if (!GetOverlappedResult(hFile, ref ov, out bytesRead, false))
                            break;
                    }

                    if (bytesRead >= 3)
                    {
                        Marshal.Copy(bufPtr, packet, 0, (int)Math.Min(bytesRead, 16));
                        ParsePacket(packet, (int)bytesRead);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bufPtr);
                if (hEvent != IntPtr.Zero) CloseHandle(hEvent);
            }
        }

        private void ParsePacket(byte[] r, int len)
        {
            byte reportId = r[0];
            if (reportId == 1 && len >= 7)
            {
                short x = (short)(r[1] | (r[2] << 8));
                short z = (short)(r[3] | (r[4] << 8)); // Frente/Tras na mesa -> Zoom
                short y = (short)-(short)(r[5] | (r[6] << 8)); // Puxar/Empurrar vertical -> Pan Y
                lock (_stateLock) { _rawTx = x; _rawTy = y; _rawTz = z; }
            }
            else if (reportId == 2 && len >= 7)
            {
                short rx = (short)(r[1] | (r[2] << 8));
                short rz = (short)(r[3] | (r[4] << 8));
                short ry = (short)-(short)(r[5] | (r[6] << 8));
                lock (_stateLock) { _rawRx = rx; _rawRy = ry; _rawRz = rz; }
            }
            else if (reportId == 3 && len >= 3)
            {
                ushort newMask = (ushort)(r[1] | (r[2] << 8));
                ushort prevMask;
                lock (_stateLock)
                {
                    prevMask = _buttonsMask;
                    _buttonsMask = newMask;
                }
                ushort diff = (ushort)(prevMask ^ newMask);
                if (diff != 0 && ButtonChanged != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        if ((diff & (1 << i)) != 0)
                        {
                            bool pressed = (newMask & (1 << i)) != 0;
                            try { ButtonChanged(i, pressed); } catch { }
                        }
                    }
                }
            }
        }

        private static bool FindSpaceBallPath(out string foundPath, out string friendly)
        {
            foundPath = null;
            friendly = null;
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr hDevInfo = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (hDevInfo == INVALID_HANDLE_VALUE) return false;

            try
            {
                SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                uint idx = 0;
                while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, idx, ref ifData))
                {
                    idx++;
                    uint reqSize = 0;
                    SetupDiGetDeviceInterfaceDetailW(hDevInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    if (reqSize == 0) continue;

                    IntPtr detailBuf = Marshal.AllocHGlobal((int)reqSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 6);
                        if (SetupDiGetDeviceInterfaceDetailW(hDevInfo, ref ifData, detailBuf, reqSize, out reqSize, IntPtr.Zero))
                        {
                            string path = Marshal.PtrToStringUni(new IntPtr(detailBuf.ToInt64() + 4));
                            if (path == null) continue;
                            string low = path.ToLowerInvariant();
                            if (low.Contains("vid_046d&pid_c621") || low.Contains("vid_046d&pid_c622"))
                            {
                                foundPath = path;
                                friendly = "SpaceBall 5000 USB (046D:C621)";
                                return true;
                            }
                            if (low.Contains("vid_046d&pid_c62") || low.Contains("vid_256f&pid_c6"))
                            {
                                foundPath = path;
                                friendly = "3Dconnexion 6DoF Controller";
                                return true;
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(detailBuf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(hDevInfo); }
            return false;
        }

        public void Dispose() { Stop(); }

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x01;
        private const uint FILE_SHARE_WRITE = 0x02;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const int ERROR_IO_PENDING = 997;
        private const uint WAIT_TIMEOUT = 0x00000102;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr detailData, uint detailDataSize, out uint requiredSize, IntPtr deviceInfoData);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, ref NativeOverlapped lpOverlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(IntPtr hFile, ref NativeOverlapped lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(IntPtr hFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ResetEvent(IntPtr hEvent);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
