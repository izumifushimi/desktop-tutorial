using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WhalePet;

public static class Platform
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupName = "WhaleMaidPet";
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size; public uint Tick; }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    public static void InitializeDpi()
    {
        // DLL launch cannot embed an apphost manifest. Set DPI before WPF starts.
        SetProcessDpiAwarenessContext(new IntPtr(-4));
    }
    public static double IdleSeconds()
    {
        LastInputInfo info = new() { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.Tick) / 1000.0;
    }
    public static bool StartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(StartupName) is string value && value == StartupCommand();
    }
    private static string StartupCommand()
    {
        string host = Environment.ProcessPath ?? throw new InvalidOperationException("找不到 .NET 主程序路径");
        return LaunchCommand.Create(host, typeof(Program).Assembly.Location);
    }
    public static void SetStartup(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(StartupName, StartupCommand(), RegistryValueKind.String);
        else key.DeleteValue(StartupName, false);
    }
    public static bool OpenDeepSeek()
    {
        string[] candidates = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeepSeek.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "DeepSeek.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeepSeek.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DeepSeek.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "DeepSeek.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DeepSeek", "DeepSeek.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DeepSeek", "DeepSeek.exe")
        };
        foreach (string path in candidates)
            if (File.Exists(path)) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return true; }
        // Find app shortcuts in named Start Menu subfolders, without launching a browser URL.
        foreach (string menu in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
        {
            string programs = Path.Combine(menu, "Programs");
            if (!Directory.Exists(programs)) continue;
            try
            {
                foreach (string shortcut in Directory.EnumerateFiles(programs, "*DeepSeek*.lnk", SearchOption.AllDirectories))
                { Process.Start(new ProcessStartInfo(shortcut) { UseShellExecute = true }); return true; }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return false;
    }
}
