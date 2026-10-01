using System;
using System.Threading;
using System.Windows;

namespace WhalePet;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using Mutex singleton = new(true, @"Local\WhaleMaidPet.Windows", out bool firstInstance);
        if (!firstInstance) return;
        Platform.InitializeDpi();
        Storage storage = new();
        Application app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { storage.Log(e.Exception); };
        try { app.Run(new PetWindow(storage)); }
        catch (Exception e) { storage.Log(e); MessageBox.Show("桌宠启动失败，日志位于：\n" + storage.DirectoryPath, "鲸鱼娘桌宠", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { singleton.ReleaseMutex(); }
    }
}
