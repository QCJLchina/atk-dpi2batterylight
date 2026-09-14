using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

[assembly: System.Reflection.AssemblyTitle("U2 电量灯")]
[assembly: System.Reflection.AssemblyDescription("ATK U2 Ultimate 2.4G battery indicator using the DPI LED")]
[assembly: System.Reflection.AssemblyCompany("Portable Utility")]
[assembly: System.Reflection.AssemblyProduct("U2 Battery Light")]
[assembly: System.Reflection.AssemblyVersion("1.0.3.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.3.0")]

namespace U2BatteryLight
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (var p = new U2Protocol())
                    {
                        MouseSnapshot s = p.FindAndReadAsync().GetAwaiter().GetResult();
                        File.WriteAllText(args[1],
                            "OK\r\nPID=" + s.ProductId.ToString("X4") +
                            "\r\nCIDMID=" + s.Cid + "," + s.Mid +
                            "\r\nBattery=" + s.Battery.Percent +
                            "\r\nCharging=" + s.Battery.Charging +
                            "\r\nMillivolts=" + s.Battery.Millivolts +
                            "\r\nDpiCount=" + s.DpiCount +
                            "\r\nCurrentDpi=" + s.CurrentDpi +
                            "\r\nPath=" + s.Path, new UTF8Encoding(false));
                    }
                }
                catch (Exception ex) { File.WriteAllText(args[1], "ERROR\r\n" + ex, new UTF8Encoding(false)); Environment.ExitCode = 2; }
                return;
            }
            if (args.Length >= 2 && args[0].Equals("--color-cycle-test", StringComparison.OrdinalIgnoreCase))
            {
                var report = new StringBuilder();
                using (var p = new U2Protocol())
                {
                    byte[] original = null;
                    try
                    {
                        MouseSnapshot s = p.FindAndReadAsync().GetAwaiter().GetResult();
                        original = p.ReadEepromAsync(44, 8).GetAwaiter().GetResult();
                        report.AppendLine("PID=" + s.ProductId.ToString("X4"));
                        report.AppendLine("ORIGINAL=" + BitConverter.ToString(original));
                        Thread.Sleep(5000);
                        byte[] green = U2Protocol.SetPairColor(original, 0, Color.FromArgb(0, 208, 96));
                        green = U2Protocol.SetPairColor(green, 1, Color.FromArgb(0, 208, 96));
                        p.WriteEepromAsync(44, green).GetAwaiter().GetResult();
                        byte[] changed = p.ReadEepromAsync(44, 8).GetAwaiter().GetResult();
                        report.AppendLine("CHANGED=" + BitConverter.ToString(changed));
                        report.AppendLine("CHANGE_OK=" + changed.SequenceEqual(green));
                        Thread.Sleep(5000);
                    }
                    catch (Exception ex) { report.AppendLine("ERROR=" + ex); Environment.ExitCode = 2; }
                    finally
                    {
                        if (original != null)
                        {
                            try
                            {
                                p.WriteEepromAsync(44, original).GetAwaiter().GetResult();
                                byte[] restored = p.ReadEepromAsync(44, 8).GetAwaiter().GetResult();
                                report.AppendLine("RESTORED=" + BitConverter.ToString(restored));
                                report.AppendLine("RESTORE_OK=" + restored.SequenceEqual(original));
                            }
                            catch (Exception ex) { report.AppendLine("RESTORE_ERROR=" + ex); Environment.ExitCode = 3; }
                        }
                    }
                }
                File.WriteAllText(args[1], report.ToString(), new UTF8Encoding(false));
                return;
            }
            if (args.Length >= 5 && args[0].Equals("--set-effect", StringComparison.OrdinalIgnoreCase))
            {
                var report = new StringBuilder();
                int mode = int.Parse(args[1], CultureInfo.InvariantCulture);
                int brightness = int.Parse(args[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int speed = int.Parse(args[3], CultureInfo.InvariantCulture);
                bool on = args[4] != "0";
                using (var p = new U2Protocol())
                {
                    try
                    {
                        p.FindAndReadAsync().GetAwaiter().GetResult();
                        p.WriteEffectAsync(mode, brightness, speed, on).GetAwaiter().GetResult();
                        U2Protocol.EffectConfig eff = p.ReadEffectAsync().GetAwaiter().GetResult();
                        report.AppendLine("WRITE_OK mode=" + eff.Mode + " bright=0x" + eff.Brightness.ToString("X2") + " speed=" + eff.Speed + " on=" + (eff.On ? 1 : 0));
                    }
                    catch (Exception ex) { report.AppendLine("ERROR=" + ex.Message); Environment.ExitCode = 2; }
                }
                File.WriteAllText(args[5], report.ToString(), new UTF8Encoding(false));
                return;
            }
            bool created;
            if (args.Length >= 2 && args[0].Equals("--eeprom-dump", StringComparison.OrdinalIgnoreCase))
            {
                var report = new StringBuilder();
                using (var p = new U2Protocol())
                {
                    try
                    {
                        MouseSnapshot s = p.FindAndReadAsync().GetAwaiter().GetResult();
                        report.AppendLine("PID=" + s.ProductId.ToString("X4"));
                        for (int addr = 0; addr < 128; addr += 10)
                        {
                            byte[] data = p.ReadEepromAsync(addr, 10).GetAwaiter().GetResult();
                            report.AppendLine(addr.ToString("X2") + ": " + BitConverter.ToString(data));
                        }
                    }
                    catch (Exception ex) { report.AppendLine("ERROR=" + ex); Environment.ExitCode = 2; }
                }
                File.WriteAllText(args[1], report.ToString(), new UTF8Encoding(false));
                return;
            }
            using (var mutex = new Mutex(true, "ATK-U2-Battery-Light-v1.0.3-{BF18F777-281E-4E22-9C59-92CE07187BCB}", out created))
            {
                if (!created)
                {
                    MessageBox.Show("U2 电量灯已经在运行。", "U2 电量灯", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args.Any(x => x.Equals("--minimized", StringComparison.OrdinalIgnoreCase))));
            }
        }
    }

    internal sealed class AppSettings
    {
        public int LowThreshold = 15;
        public int HighThreshold = 75;
        public int PollSeconds = 45;
        public string Green = "00D060";
        public string Yellow = "FFD000";
        public string Red = "FF2020";
        public bool AutoMonitor = false;
        public bool RunAtStartup = false;
        public bool MinimizeToTray = true;
        public bool RestoreOnExit = true;
        public int EffectMode = 1;
        public int EffectBrightness = 16;
        public int EffectSpeed = 1;
        public bool EffectOn = true;
        public string DeviceSignature = "";
        public readonly Dictionary<int, string> Backups = new Dictionary<int, string>();

        private static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "U2BatteryLight.ini"); }
        }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            if (!File.Exists(ConfigPath)) return s;
            try
            {
                foreach (var raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    string k = line.Substring(0, p).Trim();
                    string v = line.Substring(p + 1).Trim();
                    int n;
                    bool b;
                    if (k == "LowThreshold" && int.TryParse(v, out n)) s.LowThreshold = n;
                    else if (k == "HighThreshold" && int.TryParse(v, out n)) s.HighThreshold = n;
                    else if (k == "PollSeconds" && int.TryParse(v, out n)) s.PollSeconds = n;
                    else if (k == "Green") s.Green = v;
                    else if (k == "Yellow") s.Yellow = v;
                    else if (k == "Red") s.Red = v;
                    else if (k == "AutoMonitor" && bool.TryParse(v, out b)) s.AutoMonitor = b;
                    else if (k == "RunAtStartup" && bool.TryParse(v, out b)) s.RunAtStartup = b;
                    else if (k == "MinimizeToTray" && bool.TryParse(v, out b)) s.MinimizeToTray = b;
                    else if (k == "RestoreOnExit" && bool.TryParse(v, out b)) s.RestoreOnExit = b;
                    else if (k == "EffectMode" && int.TryParse(v, out n)) s.EffectMode = n;
                    else if (k == "EffectBrightness" && int.TryParse(v, out n)) s.EffectBrightness = n;
                    else if (k == "EffectSpeed" && int.TryParse(v, out n)) s.EffectSpeed = n;
                    else if (k == "EffectOn" && bool.TryParse(v, out b)) s.EffectOn = b;
                    else if (k == "DeviceSignature") s.DeviceSignature = v;
                    else if (k.StartsWith("Backup."))
                    {
                        int addr;
                        if (int.TryParse(k.Substring(7), out addr)) s.Backups[addr] = v;
                    }
                }
            }
            catch { }
            s.LowThreshold = Math.Max(1, Math.Min(50, s.LowThreshold));
            s.HighThreshold = Math.Max(s.LowThreshold + 1, Math.Min(99, s.HighThreshold));
            s.PollSeconds = Math.Max(15, Math.Min(600, s.PollSeconds));
            return s;
        }

        public void Save()
        {
            var lines = new List<string>();
            lines.Add("# U2 Battery Light portable settings");
            lines.Add("LowThreshold=" + LowThreshold);
            lines.Add("HighThreshold=" + HighThreshold);
            lines.Add("PollSeconds=" + PollSeconds);
            lines.Add("Green=" + Green);
            lines.Add("Yellow=" + Yellow);
            lines.Add("Red=" + Red);
            lines.Add("AutoMonitor=" + AutoMonitor);
            lines.Add("RunAtStartup=" + RunAtStartup);
            lines.Add("MinimizeToTray=" + MinimizeToTray);
            lines.Add("RestoreOnExit=" + RestoreOnExit);
            lines.Add("EffectMode=" + EffectMode);
            lines.Add("EffectBrightness=" + EffectBrightness);
            lines.Add("EffectSpeed=" + EffectSpeed);
            lines.Add("EffectOn=" + EffectOn);
            lines.Add("DeviceSignature=" + DeviceSignature);
            foreach (var kv in Backups.OrderBy(x => x.Key)) lines.Add("Backup." + kv.Key + "=" + kv.Value);
            File.WriteAllLines(ConfigPath, lines.ToArray(), new UTF8Encoding(false));
        }

        public static Color ParseColor(string hex)
        {
            try
            {
                string h = hex.Trim().TrimStart('#');
                return Color.FromArgb(int.Parse(h.Substring(0, 2), NumberStyles.HexNumber),
                    int.Parse(h.Substring(2, 2), NumberStyles.HexNumber),
                    int.Parse(h.Substring(4, 2), NumberStyles.HexNumber));
            }
            catch { return Color.White; }
        }

        public static string ColorHex(Color c)
        {
            return c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
    }

    internal sealed class BatteryInfo
    {
        public int Percent;
        public bool Charging;
        public int Millivolts;
    }

    internal sealed class MouseSnapshot
    {
        public string Path;
        public int ProductId;
        public int Cid;
        public int Mid;
        public int DpiCount;
        public int CurrentDpi;
        public BatteryInfo Battery;
        public string Signature { get { return ProductId.ToString("X4") + "-" + Cid + "-" + Mid; } }
    }

    internal sealed class U2Protocol : IDisposable
    {
        private const int Vid = 0x373B;
        private const ushort CommandUsagePage = 0xFF02;
        private const ushort CommandUsage = 2;
        private const byte ReportId = 8;
        private string _path;

        public string DevicePath { get { return _path; } }

        public async Task<MouseSnapshot> FindAndReadAsync()
        {
            Exception last = null;
            var candidates = HidNative.Enumerate(Vid, CommandUsagePage, CommandUsage)
                .OrderBy(x => x.ProductId == 0x111A ? 0 : x.ProductId == 0x1087 ? 1 : 2)
                .ToList();
            if (candidates.Any(x => x.ProductId == 0x111A))
                candidates = candidates.Where(x => x.ProductId == 0x111A).ToList();
            foreach (var dev in candidates)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        _path = dev.Path;
                        byte[] online = await ExchangeAsync(BuildReadCommand(3), 1800);
                        if (online[1] != 0 || online[4] < 1 || online[5] != 1) break;
                        byte[] cid = await ExchangeAsync(BuildReadCommand(16), 1800);
                        if (cid[1] != 0 || cid[4] < 2) break;
                        int c = cid[5];
                        int m = cid[6];
                        if (c != 2 || m != 65) break;
                        byte[] cfg = await ReadEepromAsync(0, 10);
                        var bat = await ReadBatteryAsync();
                        return new MouseSnapshot
                        {
                            Path = _path,
                            ProductId = dev.ProductId,
                            Cid = c,
                            Mid = m,
                            DpiCount = Math.Max(1, Math.Min(8, (int)cfg[2])),
                            CurrentDpi = Math.Max(0, Math.Min(7, (int)cfg[4])),
                            Battery = bat
                        };
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                    }
                    if (attempt == 0) await Task.Delay(450);
                }
            }
            _path = null;
            if (last != null) throw new IOException("找到 ATK 接口，但无法确认 U2 Ultimate：" + last.Message, last);
            throw new IOException("未找到已连接的 ATK U2 Ultimate。请确认鼠标处于 2.4G 模式且接收器已连接。");
        }

        public async Task<BatteryInfo> ReadBatteryAsync()
        {
            EnsurePath();
            byte[] f = await ExchangeAsync(BuildReadCommand(4), 1600);
            if (f[1] != 0 || f[4] < 2) throw new IOException("电量查询被设备拒绝，status=" + f[1]);
            return new BatteryInfo
            {
                Percent = Math.Max(0, Math.Min(100, (int)f[5])),
                Charging = f[6] != 0,
                Millivolts = (f[7] << 8) | f[8]
            };
        }

        public async Task<byte[]> ReadEepromAsync(int address, int length)
        {
            EnsurePath();
            byte[] f = await ExchangeAsync(BuildEeprom(8, address, null, length), 1800);
            if (f[1] != 0) throw new IOException("EEPROM 读取失败，status=" + f[1] + "，address=" + address);
            var data = new byte[length];
            Buffer.BlockCopy(f, 5, data, 0, Math.Min(length, 10));
            return data;
        }

        public async Task WriteEepromAsync(int address, byte[] data)
        {
            EnsurePath();
            byte[] f = await ExchangeAsync(BuildEeprom(7, address, data, data.Length), 1800);
            if (f[1] != 0) throw new IOException("EEPROM 写入失败，status=" + f[1] + "，address=" + address);
        }

        public void ClearPath() { _path = null; }
        private void EnsurePath()
        {
            if (String.IsNullOrEmpty(_path)) throw new InvalidOperationException("尚未连接设备");
        }

        private async Task<byte[]> ExchangeAsync(byte[] frame, int timeoutMs)
        {
            string path = _path;
            if (String.IsNullOrEmpty(path)) throw new InvalidOperationException("缺少设备路径");
            using (var handle = HidNative.OpenSync(path))
            {
                if (handle.IsInvalid) throw new IOException("无法打开 HID 命令通道，Win32=" + Marshal.GetLastWin32Error());
                var output = new byte[17];
                output[0] = ReportId;
                Buffer.BlockCopy(frame, 0, output, 1, 16);
                Task<byte[]> io = Task.Run(delegate
                {
                    HidNative.Write(handle, output);
                    while (true)
                    {
                        byte[] input = HidNative.Read(handle, 17);
                        if (input.Length >= 17 && input[0] == ReportId && input[1] == frame[0])
                        {
                            var result = new byte[16];
                            Buffer.BlockCopy(input, 1, result, 0, 16);
                            return result;
                        }
                    }
                });
                Task winner = await Task.WhenAny(io, Task.Delay(timeoutMs));
                if (winner != io)
                {
                    HidNative.Cancel(handle);
                    throw new TimeoutException("设备响应超时");
                }
                return await io;
            }
        }

        private static byte[] BuildReadCommand(byte cmd)
        {
            var f = new byte[16];
            f[0] = cmd;
            ApplyChecksum(f);
            return f;
        }

        private static byte[] BuildEeprom(byte cmd, int address, byte[] data, int length)
        {
            if (length < 1 || length > 10) throw new ArgumentOutOfRangeException("length");
            var f = new byte[16];
            f[0] = cmd;
            f[2] = (byte)((address >> 8) & 0xFF);
            f[3] = (byte)(address & 0xFF);
            f[4] = (byte)length;
            if (data != null) Buffer.BlockCopy(data, 0, f, 5, Math.Min(length, data.Length));
            ApplyChecksum(f);
            return f;
        }

        private static void ApplyChecksum(byte[] f)
        {
            int sum = 8;
            for (int i = 0; i < 15; i++) sum += f[i];
            f[15] = (byte)((85 - (sum & 0xFF)) & 0xFF);
        }

        public const int EffectAddress = 76; // 0x4C: 灯效配置区（4 组 2 字节记录：值 + 校验 0x55-value）

        public sealed class EffectConfig
        {
            public int Mode;        // 1=常亮 2=呼吸
            public int Brightness;  // 0x10=最暗 0x80=中等 0xFF=最亮
            public int Speed;       // 1=最慢 3=中等 5=最快
            public bool On;
        }

        private static byte EffectChecksum(byte value) { return (byte)((0x55 - value) & 0xFF); }

        public async Task<EffectConfig> ReadEffectAsync()
        {
            byte[] f = await ReadEepromAsync(EffectAddress, 8);
            return new EffectConfig
            {
                Mode = f[0],
                Brightness = f[2],
                Speed = f[4],
                On = f[6] != 0
            };
        }

        public async Task WriteEffectAsync(int mode, int brightness, int speed, bool on)
        {
            byte[] f = new byte[8];
            f[0] = (byte)mode; f[1] = EffectChecksum(f[0]);
            f[2] = (byte)brightness; f[3] = EffectChecksum(f[2]);
            f[4] = (byte)speed; f[5] = EffectChecksum(f[4]);
            f[6] = (byte)(on ? 1 : 0); f[7] = EffectChecksum(f[6]);
            await WriteEepromAsync(EffectAddress, f);
        }

        public static byte[] SetPairColor(byte[] block, int slot, Color color)
        {
            var b = (byte[])block.Clone();
            int o = slot * 4;
            b[o] = color.R;
            b[o + 1] = color.G;
            b[o + 2] = color.B;
            b[o + 3] = (byte)((85 - ((color.R + color.G + color.B) & 0xFF)) & 0xFF);
            return b;
        }

        public static bool PairHasColor(byte[] block, int slot, Color color)
        {
            int o = slot * 4;
            return block.Length >= o + 4 && block[o] == color.R && block[o + 1] == color.G && block[o + 2] == color.B;
        }

        public void Dispose() { }
    }

    internal sealed class HidDeviceInfo
    {
        public string Path;
        public int ProductId;
    }

    internal static class HidNative
    {
        private const uint DIGCF_PRESENT = 0x2;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr detailData, int detailDataSize, out int requiredSize, IntPtr deviceInfoData);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, int bytesToRead, out int bytesRead, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        public static SafeFileHandle OpenSync(string path)
        {
            return CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        }

        public static void Write(SafeFileHandle handle, byte[] buffer)
        {
            int written;
            if (!WriteFile(handle, buffer, buffer.Length, out written, IntPtr.Zero) || written != buffer.Length)
                throw new IOException("HID 写入失败，Win32=" + Marshal.GetLastWin32Error());
        }

        public static byte[] Read(SafeFileHandle handle, int length)
        {
            var buffer = new byte[length];
            int read;
            if (!ReadFile(handle, buffer, buffer.Length, out read, IntPtr.Zero))
                throw new IOException("HID 读取失败，Win32=" + Marshal.GetLastWin32Error());
            if (read == buffer.Length) return buffer;
            var result = new byte[read];
            Buffer.BlockCopy(buffer, 0, result, 0, read);
            return result;
        }

        public static void Cancel(SafeFileHandle handle)
        {
            try { CancelIoEx(handle, IntPtr.Zero); } catch { }
        }

        public static IEnumerable<HidDeviceInfo> Enumerate(int vendorId, ushort usagePage, ushort usage)
        {
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == InvalidHandle) yield break;
            try
            {
                uint index = 0;
                while (true)
                {
                    var iface = new SP_DEVICE_INTERFACE_DATA();
                    iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index++, ref iface)) break;
                    int required;
                    SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, required, out required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        using (var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                        {
                            if (h.IsInvalid) continue;
                            var attr = new HIDD_ATTRIBUTES();
                            attr.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                            if (!HidD_GetAttributes(h, ref attr) || attr.VendorID != vendorId) continue;
                            IntPtr pp;
                            if (!HidD_GetPreparsedData(h, out pp)) continue;
                            try
                            {
                                HIDP_CAPS caps;
                                if (HidP_GetCaps(pp, out caps) < 0) continue;
                                if (caps.UsagePage == usagePage && caps.Usage == usage)
                                    yield return new HidDeviceInfo { Path = path, ProductId = attr.ProductID };
                            }
                            finally { HidD_FreePreparsedData(pp); }
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly U2Protocol protocol = new U2Protocol();
        private readonly System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Label lblStatus = new Label();
        private readonly Label lblBattery = new Label();
        private readonly Label lblDetail = new Label();
        private readonly Label lblColorState = new Label();
        private readonly Button btnToggle = new Button();
        private readonly Button btnRefresh = new Button();
        private readonly Button btnRestore = new Button();
        private readonly Button btnRecapture = new Button();
        private readonly NumericUpDown numLow = new NumericUpDown();
        private readonly NumericUpDown numHigh = new NumericUpDown();
        private readonly TextBox txtInterval = new TextBox();
        private readonly CheckBox chkStartup = new CheckBox();
        private readonly CheckBox chkTray = new CheckBox();
        private readonly CheckBox chkRestoreExit = new CheckBox();
        private readonly Button btnGreen = new Button();
        private readonly Button btnYellow = new Button();
        private readonly Button btnRed = new Button();
        private readonly ComboBox cboEffectMode = new ComboBox();
        private readonly ComboBox cboBrightness = new ComboBox();
        private readonly ComboBox cboSpeed = new ComboBox();
        private readonly Button btnApplyEffect = new Button();
        private readonly Label lblEffectState = new Label();
        private bool suppressEffectEvents;
        private readonly System.Windows.Forms.Timer reconnectTimer = new System.Windows.Forms.Timer();
        private Icon batteryTrayIcon;
        private bool monitoring;
        private bool busy;
        private bool allowExit;
        private bool initializing = true;
        private string appliedZone = "";

        private static readonly Color Bg = Color.FromArgb(246, 248, 252);
        private static readonly Color Card = Color.White;
        private static readonly Color Ink = Color.FromArgb(31, 41, 55);
        private static readonly Color Muted = Color.FromArgb(107, 114, 128);
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);

        public MainForm(bool startMinimized)
        {
            settings = AppSettings.Load();
            Text = "U2 电量灯 1.0.3";
            ClientSize = new Size(520, 824);
            MinimumSize = new Size(536, 854);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = SystemIcons.Information;

            BuildUi();
            LoadSettingsIntoUi();
            initializing = false;
            BuildTray();

            pollTimer.Tick += async delegate { await PollAsync(false); };
            reconnectTimer.Interval = 5000;
            reconnectTimer.Tick += async delegate
            {
                if (!monitoring && String.IsNullOrEmpty(protocol.DevicePath) && !busy)
                    await RefreshDeviceAsync();
            };
            FormClosing += OnFormClosing;
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && settings.MinimizeToTray)
                {
                    Hide();
                    tray.ShowBalloonTip(900, "U2 电量灯", "程序仍在托盘运行。", ToolTipIcon.Info);
                }
            };
            Shown += async delegate
            {
                if (startMinimized)
                {
                    Hide();
                    ShowInTaskbar = false;
                }
                else
                {
                    ShowInTaskbar = true;
                    Show();
                    WindowState = FormWindowState.Normal;
                    Activate();
                }
                await RefreshDeviceAsync();
                reconnectTimer.Start();
                if (settings.AutoMonitor) await StartMonitoringAsync();
            };
        }

        private void BuildUi()
        {
            var title = new Label { Text = "U2 电量灯", Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold), ForeColor = Ink, AutoSize = true, Location = new Point(26, 20) };
            var sub = new Label { Text = "让 DPI 指示灯显示 2.4G 无线电量", ForeColor = Muted, AutoSize = true, Location = new Point(29, 62) };
            Controls.Add(title); Controls.Add(sub);

            var statusCard = MakeCard(new Rectangle(22, 92, 476, 170));
            lblStatus.Text = "● 正在查找设备";
            lblStatus.ForeColor = Color.FromArgb(217, 119, 6);
            lblStatus.Font = new Font(Font, FontStyle.Bold);
            lblStatus.AutoSize = true; lblStatus.Location = new Point(20, 17);
            lblBattery.Text = "--%"; lblBattery.Font = new Font("Segoe UI", 38F, FontStyle.Bold); lblBattery.ForeColor = Ink; lblBattery.AutoSize = true; lblBattery.Location = new Point(18, 45);
            lblDetail.Text = "等待连接"; lblDetail.ForeColor = Muted; lblDetail.AutoSize = true; lblDetail.Location = new Point(23, 118);
            lblColorState.Text = "未应用颜色"; lblColorState.TextAlign = ContentAlignment.MiddleRight; lblColorState.ForeColor = Muted; lblColorState.Location = new Point(260, 70); lblColorState.Size = new Size(190, 42);
            statusCard.Controls.Add(lblStatus); statusCard.Controls.Add(lblBattery); statusCard.Controls.Add(lblDetail); statusCard.Controls.Add(lblColorState);

            var thresholdCard = MakeCard(new Rectangle(22, 278, 476, 165));
            thresholdCard.Controls.Add(MakeSectionTitle("电量颜色", 18, 14));
            thresholdCard.Controls.Add(MakeLabel("低电量 ≤", 20, 55, 70));
            SetupNumeric(numLow, 15, 1, 50, 92, 50, 72);
            thresholdCard.Controls.Add(numLow);
            thresholdCard.Controls.Add(MakeLabel("%", 168, 55, 24));
            thresholdCard.Controls.Add(MakeLabel("中等 ≤", 210, 55, 68));
            SetupNumeric(numHigh, 75, 2, 99, 282, 50, 72);
            thresholdCard.Controls.Add(numHigh);
            thresholdCard.Controls.Add(MakeLabel("%", 358, 55, 24));
            thresholdCard.Controls.Add(MakeLabel("颜色", 20, 103, 45));
            SetupColorButton(btnRed, settings.Red, 70, 94, "低");
            SetupColorButton(btnYellow, settings.Yellow, 116, 94, "中");
            SetupColorButton(btnGreen, settings.Green, 162, 94, "高");
            thresholdCard.Controls.Add(btnRed); thresholdCard.Controls.Add(btnYellow); thresholdCard.Controls.Add(btnGreen);
            var writeTip = MakeLabel("只在颜色区间改变或被覆盖时写入，避免反复写 EEPROM。", 220, 94, 230);
            writeTip.Size = new Size(230, 48);
            thresholdCard.Controls.Add(writeTip);

            var effectCard = MakeCard(new Rectangle(22, 459, 476, 132));
            effectCard.Controls.Add(MakeSectionTitle("灯效设置", 18, 14));
            SetupCombo(cboEffectMode, new Rectangle(20, 50, 92, 28));
            cboEffectMode.Items.AddRange(new object[] { "常亮", "呼吸", "关闭" });
            SetupCombo(cboBrightness, new Rectangle(130, 50, 92, 28));
            cboBrightness.Items.AddRange(new object[] { "亮度最暗", "亮度中等", "亮度最亮" });
            SetupCombo(cboSpeed, new Rectangle(240, 50, 92, 28));
            cboSpeed.Items.AddRange(new object[] { "速度最慢", "速度中等", "速度最快" });
            effectCard.Controls.Add(cboEffectMode); effectCard.Controls.Add(cboBrightness); effectCard.Controls.Add(cboSpeed);
            btnApplyEffect.Text = "应用灯效";
            StyleSecondary(btnApplyEffect, new Rectangle(348, 49, 112, 32));
            btnApplyEffect.Cursor = Cursors.Hand;
            btnApplyEffect.Click += async delegate { await ApplyEffectAsync(); };
            effectCard.Controls.Add(btnApplyEffect);
            lblEffectState.Text = "连接设备后显示当前灯效；修改后点“应用灯效”写入。";
            lblEffectState.ForeColor = Muted; lblEffectState.AutoSize = true; lblEffectState.Location = new Point(20, 96);
            effectCard.Controls.Add(lblEffectState);
            cboEffectMode.SelectedIndexChanged += delegate { UpdateEffectControlState(); };

            var optionsCard = MakeCard(new Rectangle(22, 607, 476, 132));
            optionsCard.Controls.Add(MakeSectionTitle("运行设置", 18, 14));
            optionsCard.Controls.Add(MakeLabel("刷新间隔", 20, 52, 65));
            SetupIntervalTextBox();
            optionsCard.Controls.Add(txtInterval);
            optionsCard.Controls.Add(MakeLabel("秒", 168, 52, 24));
            SetupCheck(chkStartup, "开机启动", 215, 49);
            SetupCheck(chkTray, "最小化到托盘", 315, 49);
            SetupCheck(chkRestoreExit, "退出时恢复原色", 20, 88);
            optionsCard.Controls.Add(chkStartup); optionsCard.Controls.Add(chkTray); optionsCard.Controls.Add(chkRestoreExit);

            btnToggle.Text = "开始监控"; StylePrimary(btnToggle, new Rectangle(22, 755, 180, 46));
            btnToggle.Click += async delegate { if (monitoring) await StopMonitoringAsync(true); else await StartMonitoringAsync(); };
            btnRefresh.Text = "刷新"; StyleSecondary(btnRefresh, new Rectangle(214, 755, 88, 46));
            btnRefresh.Click += async delegate { await RefreshDeviceAsync(); };
            btnRestore.Text = "恢复原色"; StyleSecondary(btnRestore, new Rectangle(312, 755, 88, 46));
            btnRestore.Click += async delegate { await RestoreAsync(true); };
            btnRecapture.Text = "重录"; StyleSecondary(btnRecapture, new Rectangle(410, 755, 88, 46));
            btnRecapture.Click += async delegate { await RecaptureAsync(); };
            Controls.Add(btnToggle); Controls.Add(btnRefresh); Controls.Add(btnRestore); Controls.Add(btnRecapture);

            numLow.ValueChanged += SettingsChanged;
            numHigh.ValueChanged += SettingsChanged;
            txtInterval.Leave += delegate { ValidateInterval(); SaveUiSettings(); };
            txtInterval.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            txtInterval.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { ValidateInterval(); SaveUiSettings(); e.SuppressKeyPress = true; }
            };
            chkStartup.CheckedChanged += SettingsChanged;
            chkTray.CheckedChanged += SettingsChanged;
            chkRestoreExit.CheckedChanged += SettingsChanged;
        }

        private Panel MakeCard(Rectangle bounds)
        {
            var p = new Panel { BackColor = Card, Bounds = bounds };
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235))) e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            Controls.Add(p);
            return p;
        }

        private static Label MakeSectionTitle(string text, int x, int y)
        {
            return new Label { Text = text, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), ForeColor = Ink, AutoSize = true, Location = new Point(x, y) };
        }
        private static Label MakeLabel(string text, int x, int y, int width)
        {
            return new Label { Text = text, ForeColor = Muted, Location = new Point(x, y), Size = new Size(width, 26), AutoEllipsis = true };
        }
        private static Label MakeLabel(string text, int x, int y) { return MakeLabel(text, x, y, 120); }
        private static void SetupNumeric(NumericUpDown n, int value, int min, int max, int x, int y, int width)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            n.Location = new Point(x, y);
            n.AutoSize = false;
            n.Size = new Size(width, 28);
            n.BorderStyle = BorderStyle.FixedSingle;
            n.BackColor = Color.White;
            n.ForeColor = Ink;
            n.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            n.TextAlign = HorizontalAlignment.Center;
            n.DecimalPlaces = 0;
            n.ThousandsSeparator = false;
            // Some Windows themes do not propagate ForeColor to UpDownBase's edit child.
            if (n.Controls.Count > 1)
            {
                n.Controls[1].BackColor = Color.White;
                n.Controls[1].ForeColor = Ink;
                n.Controls[1].Font = n.Font;
            }
        }
        private static void SetupCheck(CheckBox c, string text, int x, int y)
        {
            c.Text = text; c.AutoSize = true; c.Location = new Point(x, y); c.ForeColor = Ink;
        }
        private void SetupIntervalTextBox()
        {
            txtInterval.Location = new Point(92, 47);
            txtInterval.Size = new Size(70, 28);
            txtInterval.BorderStyle = BorderStyle.FixedSingle;
            txtInterval.BackColor = Color.White;
            txtInterval.ForeColor = Ink;
            txtInterval.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            txtInterval.TextAlign = HorizontalAlignment.Center;
            txtInterval.MaxLength = 3;
            txtInterval.Text = settings.PollSeconds.ToString(CultureInfo.InvariantCulture);
        }
        private void SetupColorButton(Button b, string value, int x, int y, string tag)
        {
            b.Text = tag; b.Bounds = new Rectangle(x, y, 38, 34); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            b.BackColor = AppSettings.ParseColor(value); b.ForeColor = Contrast(b.BackColor); b.Cursor = Cursors.Hand;
            b.Click += delegate
            {
                using (var dialog = new ColorDialog { Color = b.BackColor, FullOpen = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) { b.BackColor = dialog.Color; b.ForeColor = Contrast(dialog.Color); SaveUiSettings(); }
            };
        }
        private static Color Contrast(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 145 ? Color.Black : Color.White; }
        private static void StylePrimary(Button b, Rectangle r)
        {
            b.Bounds = r; b.BackColor = Accent; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); b.Cursor = Cursors.Hand;
        }
        private static void StyleSecondary(Button b, Rectangle r)
        {
            b.Bounds = r; b.BackColor = Color.White; b.ForeColor = Ink; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219); b.Cursor = Cursors.Hand;
        }

        private void LoadSettingsIntoUi()
        {
            numLow.Value = settings.LowThreshold;
            numHigh.Value = Math.Max(settings.LowThreshold + 1, settings.HighThreshold);
            txtInterval.Text = settings.PollSeconds.ToString(CultureInfo.InvariantCulture);
            chkStartup.Checked = settings.RunAtStartup;
            chkTray.Checked = settings.MinimizeToTray;
            chkRestoreExit.Checked = settings.RestoreOnExit;
            btnGreen.BackColor = AppSettings.ParseColor(settings.Green); btnGreen.ForeColor = Contrast(btnGreen.BackColor);
            btnYellow.BackColor = AppSettings.ParseColor(settings.Yellow); btnYellow.ForeColor = Contrast(btnYellow.BackColor);
            btnRed.BackColor = AppSettings.ParseColor(settings.Red); btnRed.ForeColor = Contrast(btnRed.BackColor);
            suppressEffectEvents = true;
            cboEffectMode.SelectedIndex = !settings.EffectOn ? 2 : (settings.EffectMode == 2 ? 1 : 0);
            cboBrightness.SelectedIndex = BrightnessToIndex(settings.EffectBrightness);
            cboSpeed.SelectedIndex = SpeedToIndex(settings.EffectSpeed);
            suppressEffectEvents = false;
            UpdateEffectControlState();
        }

        private static void SetupCombo(ComboBox c, Rectangle bounds)
        {
            c.Bounds = bounds;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.White;
            c.ForeColor = Ink;
            c.Font = new Font("Microsoft YaHei UI", 9F);
        }

        private void UpdateEffectControlState()
        {
            if (suppressEffectEvents || cboEffectMode.SelectedIndex < 0) return;
            bool off = cboEffectMode.SelectedIndex == 2;
            bool breathing = cboEffectMode.SelectedIndex == 1;
            cboBrightness.Enabled = !off;
            cboSpeed.Enabled = breathing;
            btnApplyEffect.Enabled = !off || cboEffectMode.Enabled;
        }

        private static int BrightnessToIndex(int value) { return value >= 0xC0 ? 2 : (value >= 0x40 ? 1 : 0); }
        private static int BrightnessFromIndex(int index) { return index <= 0 ? 0x10 : (index == 1 ? 0x80 : 0xFF); }
        private static int SpeedToIndex(int value) { return value >= 4 ? 2 : (value >= 2 ? 1 : 0); }
        private static int SpeedFromIndex(int index) { return index <= 0 ? 1 : (index == 1 ? 3 : 5); }

        private async Task LoadEffectFromDeviceAsync()
        {
            try
            {
                U2Protocol.EffectConfig eff = await protocol.ReadEffectAsync();
                suppressEffectEvents = true;
                cboEffectMode.SelectedIndex = !eff.On ? 2 : (eff.Mode == 2 ? 1 : 0);
                cboBrightness.SelectedIndex = BrightnessToIndex(eff.Brightness);
                cboSpeed.SelectedIndex = SpeedToIndex(eff.Speed);
                suppressEffectEvents = false;
                UpdateEffectControlState();
                settings.EffectMode = eff.Mode;
                settings.EffectBrightness = eff.Brightness;
                settings.EffectSpeed = eff.Speed;
                settings.EffectOn = eff.On;
                try { settings.Save(); } catch { }
                lblEffectState.Text = "当前设备灯效：" + (!eff.On ? "关闭" : (eff.Mode == 2 ? "呼吸" : "常亮"));
            }
            catch { }
        }

        private async Task ApplyEffectAsync()
        {
            if (busy) return;
            if (cboEffectMode.SelectedIndex < 0 || cboBrightness.SelectedIndex < 0 || cboSpeed.SelectedIndex < 0) return;
            busy = true; SetControls(false);
            try
            {
                int modeIndex = cboEffectMode.SelectedIndex;
                bool on = modeIndex != 2;
                int mode = modeIndex == 1 ? 2 : 1;
                int brightness = BrightnessFromIndex(cboBrightness.SelectedIndex);
                int speed = SpeedFromIndex(cboSpeed.SelectedIndex);
                if (String.IsNullOrEmpty(protocol.DevicePath)) await protocol.FindAndReadAsync();
                await protocol.WriteEffectAsync(mode, brightness, speed, on);
                settings.EffectMode = mode;
                settings.EffectBrightness = brightness;
                settings.EffectSpeed = speed;
                settings.EffectOn = on;
                try { settings.Save(); } catch { }
                lblEffectState.Text = "已写入：" + (on ? (mode == 2 ? "呼吸" : "常亮") : "关闭");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "灯效写入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { busy = false; SetControls(true); }
        }

        private void SettingsChanged(object sender, EventArgs e)
        {
            if (initializing) return;
            if (numHigh.Value <= numLow.Value)
            {
                if (sender == numLow) numHigh.Value = Math.Min(numHigh.Maximum, numLow.Value + 1);
                else numLow.Value = Math.Max(numLow.Minimum, numHigh.Value - 1);
            }
            SaveUiSettings();
            if (monitoring)
            {
                pollTimer.Interval = settings.PollSeconds * 1000;
                appliedZone = "";
            }
        }

        private void ValidateInterval()
        {
            int value;
            if (!Int32.TryParse(txtInterval.Text, out value)) value = settings.PollSeconds;
            value = Math.Max(15, Math.Min(600, value));
            txtInterval.Text = value.ToString(CultureInfo.InvariantCulture);
        }

        private void SaveUiSettings()
        {
            settings.LowThreshold = (int)numLow.Value;
            settings.HighThreshold = (int)numHigh.Value;
            int interval;
            if (!Int32.TryParse(txtInterval.Text, out interval)) interval = settings.PollSeconds;
            settings.PollSeconds = Math.Max(15, Math.Min(600, interval));
            settings.RunAtStartup = chkStartup.Checked;
            settings.MinimizeToTray = chkTray.Checked;
            settings.RestoreOnExit = chkRestoreExit.Checked;
            settings.Red = AppSettings.ColorHex(btnRed.BackColor);
            settings.Yellow = AppSettings.ColorHex(btnYellow.BackColor);
            settings.Green = AppSettings.ColorHex(btnGreen.BackColor);
            try { settings.Save(); } catch { }
            ApplyStartup(settings.RunAtStartup);
        }

        private static void ApplyStartup(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enabled) key.SetValue("U2BatteryLight", "\"" + Application.ExecutablePath + "\" --minimized");
                    else key.DeleteValue("U2BatteryLight", false);
                }
            }
            catch { }
        }

        private void BuildTray()
        {
            UpdateTrayNumber("--");
            tray.Text = "U2 电量灯";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowWindow(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示", null, delegate { ShowWindow(); });
            menu.Items.Add("立即刷新", null, async delegate { await PollAsync(true); });
            menu.Items.Add("开始/停止", null, async delegate { if (monitoring) await StopMonitoringAsync(true); else await StartMonitoringAsync(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, async delegate
            {
                allowExit = true;
                pollTimer.Stop();
                if (monitoring && settings.RestoreOnExit) await RestoreAsync(false);
                monitoring = false;
                Close();
            });
            tray.ContextMenuStrip = menu;
        }

        private void ShowWindow()
        {
            ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate();
        }

        private async Task RefreshDeviceAsync()
        {
            if (busy) return;
            busy = true; SetControls(false);
            try
            {
                SetStatus("● 正在连接", Color.FromArgb(217, 119, 6));
                protocol.ClearPath();
                MouseSnapshot snap = await protocol.FindAndReadAsync();
                UpdateBatteryUi(snap.Battery);
                lblDetail.Text = "U2 Ultimate · 8K 接收器 PID " + snap.ProductId.ToString("X4") + " · DPI " + (snap.CurrentDpi + 1) + "/" + snap.DpiCount;
                SetStatus("● 已连接", Color.FromArgb(22, 163, 74));
                await LoadEffectFromDeviceAsync();
            }
            catch (Exception ex)
            {
                SetStatus("● 未连接", Color.FromArgb(220, 38, 38));
                lblBattery.Text = "--%"; lblDetail.Text = ex.Message; lblColorState.Text = "等待设备";
            }
            finally { busy = false; SetControls(true); }
        }

        private async Task StartMonitoringAsync()
        {
            if (busy || monitoring) return;
            SaveUiSettings();
            busy = true; SetControls(false);
            try
            {
                MouseSnapshot snap = await protocol.FindAndReadAsync();
                await EnsureBackupsAsync(snap);
                monitoring = true;
                settings.AutoMonitor = true; settings.Save();
                btnToggle.Text = "停止监控";
                btnToggle.BackColor = Color.FromArgb(220, 38, 38);
                pollTimer.Interval = settings.PollSeconds * 1000;
                pollTimer.Start();
                appliedZone = "";
                await ApplyForBatteryAsync(snap);
                SetStatus("● 正在监控", Color.FromArgb(22, 163, 74));
            }
            catch (Exception ex)
            {
                monitoring = false;
                settings.AutoMonitor = false; try { settings.Save(); } catch { }
                MessageBox.Show(ex.Message, "无法开始监控", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("● 启动失败", Color.FromArgb(220, 38, 38));
            }
            finally { busy = false; SetControls(true); }
        }

        private async Task StopMonitoringAsync(bool restore)
        {
            if (busy) return;
            pollTimer.Stop(); monitoring = false; settings.AutoMonitor = false; try { settings.Save(); } catch { }
            btnToggle.Text = "开始监控"; btnToggle.BackColor = Accent;
            if (restore) await RestoreAsync(false);
            SetStatus("● 已停止", Muted);
        }

        private async Task PollAsync(bool force)
        {
            if (busy || (!monitoring && !force)) return;
            busy = true;
            try
            {
                MouseSnapshot snap;
                if (String.IsNullOrEmpty(protocol.DevicePath)) snap = await protocol.FindAndReadAsync();
                else
                {
                    BatteryInfo b = await protocol.ReadBatteryAsync();
                    snap = new MouseSnapshot { Battery = b, DpiCount = Math.Max(1, settings.Backups.Count * 2), CurrentDpi = 0, Cid = 2, Mid = 65 };
                }
                UpdateBatteryUi(snap.Battery);
                if (monitoring)
                {
                    if (settings.Backups.Count == 0) { snap = await protocol.FindAndReadAsync(); await EnsureBackupsAsync(snap); }
                    await ApplyForBatteryAsync(snap);
                    SetStatus("● 正在监控", Color.FromArgb(22, 163, 74));
                }
                else SetStatus("● 已连接", Color.FromArgb(22, 163, 74));
            }
            catch (Exception ex)
            {
                protocol.ClearPath();
                SetStatus("● 连接中断", Color.FromArgb(220, 38, 38));
                lblDetail.Text = ex.Message;
            }
            finally { busy = false; SetControls(true); }
        }

        private async Task EnsureBackupsAsync(MouseSnapshot snap)
        {
            if (settings.DeviceSignature.Length > 0 && settings.DeviceSignature != snap.Signature)
            {
                settings.Backups.Clear();
                settings.DeviceSignature = "";
            }
            int blocks = Math.Max(1, (snap.DpiCount + 1) / 2);
            int[] addresses = { 44, 52, 60, 68 };
            for (int i = 0; i < blocks; i++)
            {
                int addr = addresses[i];
                if (!settings.Backups.ContainsKey(addr))
                {
                    byte[] data = await protocol.ReadEepromAsync(addr, 8);
                    settings.Backups[addr] = Convert.ToBase64String(data);
                }
            }
            settings.DeviceSignature = snap.Signature;
            settings.Save();
        }

        private async Task ApplyForBatteryAsync(MouseSnapshot snap)
        {
            BatteryInfo b = snap.Battery;
            string zone;
            Color color;
            if (b.Percent <= settings.LowThreshold) { zone = "low"; color = btnRed.BackColor; }
            else if (b.Percent <= settings.HighThreshold) { zone = "medium"; color = btnYellow.BackColor; }
            else { zone = "high"; color = btnGreen.BackColor; }

            bool changed = zone != appliedZone;
            foreach (var kv in settings.Backups.OrderBy(x => x.Key))
            {
                byte[] current = await protocol.ReadEepromAsync(kv.Key, 8);
                bool correct = U2Protocol.PairHasColor(current, 0, color) && U2Protocol.PairHasColor(current, 1, color);
                if (changed || !correct)
                {
                    byte[] desired = U2Protocol.SetPairColor(current, 0, color);
                    desired = U2Protocol.SetPairColor(desired, 1, color);
                    await protocol.WriteEepromAsync(kv.Key, desired);
                }
            }
            appliedZone = zone;
            lblColorState.Text = (zone == "high" ? "高电量" : zone == "medium" ? "中等电量" : "低电量") + "\r\nRGB " + color.R + ", " + color.G + ", " + color.B;
            lblColorState.ForeColor = color.GetBrightness() < .4 ? Color.FromArgb(Math.Min(255, color.R + 60), Math.Min(255, color.G + 60), Math.Min(255, color.B + 60)) : color;
            UpdateBatteryUi(b);
        }

        private async Task RestoreAsync(bool showMessage)
        {
            if (busy && showMessage) return;
            bool wasBusy = busy; busy = true; SetControls(false);
            try
            {
                if (settings.Backups.Count == 0) throw new InvalidOperationException("尚未保存原始颜色。");
                if (String.IsNullOrEmpty(protocol.DevicePath)) await protocol.FindAndReadAsync();
                foreach (var kv in settings.Backups.OrderBy(x => x.Key))
                    await protocol.WriteEepromAsync(kv.Key, Convert.FromBase64String(kv.Value));
                appliedZone = "";
                lblColorState.Text = "已恢复原色";
                if (showMessage) MessageBox.Show("原始 DPI 颜色已恢复。", "U2 电量灯", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (showMessage) MessageBox.Show(ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { busy = wasBusy; SetControls(true); }
        }

        private async Task RecaptureAsync()
        {
            if (monitoring)
            {
                MessageBox.Show("请先停止监控并设置好你想保留的 DPI 原色，再重新记录。", "U2 电量灯", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("重新记录会替换现有原色备份。继续吗？", "重新记录", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            busy = true; SetControls(false);
            try
            {
                settings.Backups.Clear(); settings.DeviceSignature = "";
                MouseSnapshot snap = await protocol.FindAndReadAsync();
                await EnsureBackupsAsync(snap);
                lblColorState.Text = "已记录原色";
                MessageBox.Show("已记录 " + settings.Backups.Count * 2 + " 个 DPI 档位的原始颜色。", "U2 电量灯", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "记录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { busy = false; SetControls(true); }
        }

        private void UpdateBatteryUi(BatteryInfo b)
        {
            UpdateTrayNumber(b.Percent.ToString(CultureInfo.InvariantCulture));
            lblBattery.Text = b.Percent + "%";
            lblDetail.Text = (b.Charging ? "充电中" : "使用电池") + " · " + b.Millivolts + " mV · " + DateTime.Now.ToString("HH:mm:ss");
            tray.Text = ("U2 电量灯 · " + b.Percent + "%").Substring(0, Math.Min(63, ("U2 电量灯 · " + b.Percent + "%").Length));
        }
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        private void UpdateTrayNumber(string number)
        {
            bool light = true;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (key != null) light = Convert.ToInt32(key.GetValue("SystemUsesLightTheme", 1)) != 0;
            }
            catch { }
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            using (var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", size * (number.Length >= 3 ? .58F : .72F), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(light ? Color.FromArgb(30, 30, 30) : Color.White))
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                graphics.Clear(Color.Transparent);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                graphics.DrawString(number, font, brush, new RectangleF(0, 0, size, size), format);
                IntPtr handle = bitmap.GetHicon();
                Icon next;
                try { using (var borrowed = Icon.FromHandle(handle)) next = (Icon)borrowed.Clone(); }
                finally { DestroyIcon(handle); }
                Icon previous = batteryTrayIcon;
                tray.Icon = next;
                batteryTrayIcon = next;
                if (previous != null) previous.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                if (batteryTrayIcon != null) { batteryTrayIcon.Dispose(); batteryTrayIcon = null; }
            }
            base.Dispose(disposing);
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = text; lblStatus.ForeColor = color;
            if (text.Contains("未连接") || text.Contains("连接中断")) UpdateTrayNumber("--");
        }
        private void SetControls(bool enabled)
        {
            btnToggle.Enabled = enabled; btnRefresh.Enabled = enabled; btnRestore.Enabled = enabled; btnRecapture.Enabled = enabled;
            btnApplyEffect.Enabled = enabled;
        }

        private async void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; Hide(); ShowInTaskbar = false; return;
            }
            pollTimer.Stop();
            reconnectTimer.Stop();
            if (monitoring && settings.RestoreOnExit)
            {
                e.Cancel = true;
                await RestoreAsync(false);
                monitoring = false;
                allowExit = true;
                tray.Visible = false;
                Close();
            }
            else tray.Visible = false;
        }
    }
}
