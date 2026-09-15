using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ProbePanda
{
    internal sealed class HidDeviceInfo { public string Path; public int ProductId; public ushort UsagePage; public ushort Usage; }
    internal static class HidNative
    {
        private const uint DIGCF_PRESENT = 0x2;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 3;
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
        public static IEnumerable<HidDeviceInfo> Enumerate(int vendorId)
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
                            try { HIDP_CAPS caps; if (HidP_GetCaps(pp, out caps) < 0) continue; yield return new HidDeviceInfo { Path = path, ProductId = attr.ProductID, UsagePage = caps.UsagePage, Usage = caps.Usage }; }
                            finally { HidD_FreePreparsedData(pp); }
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }
    internal static class Program
    {
        private static byte[] BuildRead(byte cmd) { var f = new byte[16]; f[0] = cmd; int sum = 8; for (int i = 0; i < 15; i++) sum += f[i]; f[15] = (byte)((85 - (sum & 0xFF)) & 0xFF); return f; }
        private static byte[] Exchange(string path, byte cmd, int timeoutMs)
        {
            using (var handle = HidNative.OpenSync(path))
            {
                if (handle.IsInvalid) throw new IOException("open fail");
                var output = new byte[17]; output[0] = 8; Buffer.BlockCopy(BuildRead(cmd), 0, output, 1, 16);
                HidNative.Write(handle, output);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    try { byte[] input = HidNative.Read(handle, 17); if (input.Length >= 17 && input[0] == 8 && input[1] == cmd) { var r = new byte[16]; Buffer.BlockCopy(input, 1, r, 0, 16); return r; } } catch { }
                    System.Threading.Thread.Sleep(10);
                }
                throw new TimeoutException("timeout");
            }
        }
        private static void Main()
        {
            Console.WriteLine("Enumerating VID 0x3554...");
            foreach (var d in HidNative.Enumerate(0x3554))
            {
                Console.WriteLine("PATH=" + d.Path);
                Console.WriteLine("  PID=0x" + d.ProductId.ToString("X4") + " UsagePage=0x" + d.UsagePage.ToString("X4") + " Usage=0x" + d.Usage.ToString("X4"));
                foreach (byte cmd in new byte[] { 3, 4, 16 })
                {
                    try { byte[] r = Exchange(d.Path, cmd, 1500); Console.WriteLine("  cmd " + cmd + " => " + BitConverter.ToString(r)); }
                    catch (Exception ex) { Console.WriteLine("  cmd " + cmd + " => ERR " + ex.Message); }
                }
            }
            Console.WriteLine("done");
        }
    }
}
