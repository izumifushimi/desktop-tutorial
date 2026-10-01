using System;

namespace WhalePet;

public static class LaunchCommand
{
    // DLL deployment: login startup must pass the application assembly to dotnet.
    public static string Create(string host, string assembly)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(assembly)
            || host.Contains('"') || assembly.Contains('"'))
            throw new ArgumentException("启动路径无效");
        return "\"" + host + "\" \"" + assembly + "\"";
    }
}
