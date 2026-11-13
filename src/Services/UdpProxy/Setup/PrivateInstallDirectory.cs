using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;

namespace Game_Daily_Routine_Launcher;

internal sealed class PrivateInstallDirectory : IDisposable
{
    private const string Prefix = "MAAA-Udp-Setup-";
    public string Path { get; }

    public PrivateInstallDirectory()
    {
        Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Prefix + Guid.NewGuid().ToString("N"));
        var security = new DirectorySecurity();
        security.SetSecurityDescriptorSddlForm(
            "O:BAG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;RC;;;OW)");
        var pinned = GCHandle.Alloc(security.GetSecurityDescriptorBinaryForm(), GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = pinned.AddrOfPinnedObject()
            };
            // Atomically apply the ACL and refuse adoption of an existing directory.
            if (!CreateDirectory(Path, ref attributes)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { pinned.Free(); }
    }

    public void Dispose()
    {
        var directory = new DirectoryInfo(System.IO.Path.GetFullPath(Path));
        var parent = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!directory.Exists) return;
        if (!string.Equals(directory.Parent?.FullName, parent, StringComparison.OrdinalIgnoreCase) ||
            !directory.Name.StartsWith(Prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(directory.Name[Prefix.Length..], "N", out _) ||
            directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("拒绝清理不符合预期的 UDP 安装目录。");
        directory.Delete(recursive: true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, ref SecurityAttributes attributes);
}
