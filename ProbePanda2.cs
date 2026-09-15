using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ProbePanda2
{
    internal sealed class HidDeviceInfo2 { public string Path; public int ProductId; public ushort UsagePage; public ushort Usage; }
    internal static class HidNative2
    {
        private const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1, FILE_SHARE_WRITE = 0x2, OPEN_EXISTING = 3;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);
        [StructLayout(LayoutKind.Sequential)] private struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] private struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }
        [StructLayout(LayoutKind.Sequential)] private struct HIDP_CAPS { public ushort Usage; public ushort UsagePage; public ushort InputReportByteLength; public ushort OutputReportByteLength; public ushort FeatureReportByteLength; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved; public ushort NumberLinkCollectionNodes; public ushort NumberInputButtonCaps; public ushort NumberInputValueCaps; public ushort NumberInputDataIndices; public ushort NumberOutputButtonCaps; public ushort NumberOutputValueCaps; public ushort NumberOutputDataIndices; public ushort NumberFeatureButtonCaps; public ushort NumberFeatureValueCaps; public ushort NumberFeatureDataIndices; }
        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("setupapi.dll", SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr detailData, int detailDataSize, out int requiredSize, IntPtr deviceInfoData);
        [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);
        [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);
        [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, int bytesToRead, out int bytesRead, IntPtr overlapped);
        public static SafeFileHandle OpenSync(string path) { return CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero); }
        public static void Write(SafeFileHandle handle, byte[] buffer) { int written; if (!WriteFile(handle, buffer, buffer.Length, out written, IntPtr.Zero) || written != buffer.Length) throw new IOException("write fail"); }
        public static byte[] Read(SafeFileHandle handle, int length) { var b = new byte[length]; int read; if (!ReadFile(handle, b, b.Length, out read, IntPtr.Zero)) throw new IOException("read fail"); if (read == length) return b; var r = new byte[read]; Buffer.BlockCopy(b, 0, r, 0, read); return r; }
        public static IEnumerable<HidDeviceInfo2> Enumerate(int vendorId, ushort usagePage, ushort usage)
        {
            Guid guid; HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == InvalidHandle) yield break;
            try
            {
                uint index = 0;
                while (true)
                {
                    var iface = new SP_DEVICE_INTERFACE_DATA(); iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index++, ref iface)) break;
                    int required; SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, required, out required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        using (var h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                        {
                            if (h.IsInvalid) continue;
                            var attr = new HIDD_ATTRIBUTES(); attr.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                            if (!HidD_GetAttributes(h, ref attr) || attr.VendorID != vendorId) continue;
                            IntPtr pp; if (!HidD_GetPreparsedData(h, out pp)) continue;
                            try { HIDP_CAPS caps; if (HidP_GetCaps(pp, out caps) < 0) continue; if (caps.UsagePage == usagePage && caps.Usage == usage) yield return new HidDeviceInfo2 { Path = path, ProductId = attr.ProductID, UsagePage = caps.UsagePage, Usage = caps.Usage }; }
                            finally { HidD_FreePreparsedData(pp); }
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }
    internal static class Program2
    {
        private static byte[] Build(byte cmd, int addr, byte[] data, int len) { var f = new byte[16]; f[0] = cmd; if (addr >= 0) { f[2] = (byte)((addr >> 8) & 0xFF); f[3] = (byte)(addr & 0xFF); f[4] = (byte)len; if (data != null) Buffer.BlockCopy(data, 0, f, 5, Math.Min(len, data.Length)); } int sum = 8; for (int i = 0; i < 15; i++) sum += f[i]; f[15] = (byte)((85 - (sum & 0xFF)) & 0xFF); return f; }
        private static byte[] Exchange(string path, byte[] frame, int timeoutMs)
        {
            using (var handle = HidNative2.OpenSync(path))
            {
                if (handle.IsInvalid) throw new IOException("open fail");
                var output = new byte[17]; output[0] = 8; Buffer.BlockCopy(frame, 0, output, 1, 16);
                HidNative2.Write(handle, output);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    try { byte[] input = HidNative2.Read(handle, 17); if (input.Length >= 17 && input[0] == 8 && input[1] == frame[0]) { var r = new byte[16]; Buffer.BlockCopy(input, 1, r, 0, 16); return r; } } catch { }
                    System.Threading.Thread.Sleep(10);
                }
                throw new TimeoutException("timeout");
            }
        }
        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            // Also probe the ATK (VID 0x373B) eeprom read to compare behavior
            foreach (var a in HidNative2.Enumerate(0x373B, 0xFF02, 2))
            {
                Console.WriteLine("ATK PATH=" + a.Path + " PID=0x" + a.ProductId.ToString("X4"));
                try { byte[] b = Exchange(a.Path, Build(4, -1, null, 0), 1500); Console.WriteLine("ATK BATTERY=" + BitConverter.ToString(b)); } catch (Exception ex) { Console.WriteLine("ATK BATTERY ERR " + ex.Message); }
            }
            foreach (var d in HidNative2.Enumerate(0x3554, 0xFF02, 2))
            {
                Console.WriteLine("PANDA PATH=" + d.Path + " PID=0x" + d.ProductId.ToString("X4"));
                // battery
                try { byte[] b = Exchange(d.Path, Build(4, -1, null, 0), 1500); Console.WriteLine("BATTERY=" + BitConverter.ToString(b)); } catch (Exception ex) { Console.WriteLine("BATTERY ERR " + ex.Message); }
                // eeprom dump 0..127
                for (int addr = 0; addr < 128; addr += 8)
                {
                    try
                    {
                        byte[] f = Build(8, addr, null, 8);
                        byte[] r = Exchange(d.Path, f, 1500);
                        if (r[1] == 0)
                        {
                            byte[] data = new byte[8]; Buffer.BlockCopy(r, 5, data, 0, 8);
                            Console.WriteLine(addr.ToString("X2") + ": " + BitConverter.ToString(data));
                        }
                        else Console.WriteLine(addr.ToString("X2") + ": status=" + r[1]);
                    }
                    catch (Exception ex) { Console.WriteLine(addr.ToString("X2") + ": ERR " + ex.Message); }
                }
            }
            Console.WriteLine("done");
        }
    }
}
