# Windows 版本

使用 C#、WPF 和 .NET 10。支持 Windows 11；提供 x64 和 ARM64 两种独立可执行文件。

## 直接运行

1. 普通 Intel / AMD 电脑使用 `whale-maid-windows-x64.zip`；Windows ARM 电脑使用 ARM64 包。
2. 解压到固定文件夹，再双击 `WhalePet.exe`。运行包包含 .NET 桌面运行时，无需额外安装 .NET。
3. 左键点击摸头、按住拖动；右键打开菜单。喂饭放在菜单里面，双击不会喂饭。
4. Windows 系统托盘也提供菜单；若角色不在视野内，从托盘选择“回到右下角”。

首次运行默认启用当前用户的登录自启动。可在右键菜单开启或关闭，不需要管理员权限。移动程序文件夹后，在新位置运行并重新启用自启动。

## 与 Mac 一致的互动

- 原地待着，不自行上下左右移动。
- 无键盘、鼠标操作达到 15 秒，托腮无聊；恢复操作后回到正常。
- 每天北京时间 08:00、12:00、18:00 进入饥饿，选择“喂米饭”后吃饭 5 秒。饭点判断固定为 UTC+8，与 Windows 系统时区无关。
- 休眠后补最近一个错过的饭点，饥饿状态和位置、大小会在重启后保留。
- 连续摸头 5 次害羞、20 次红脸嗔怒，15 秒不摸恢复。
- 表情预览即时切换、保持 4 秒，然后恢复实际状态。
- 饥饿状态优先于无聊和摸头表情，预览仅短暂展示，不会修改饥饿记录。
- 查找桌面和开始菜单的 DeepSeek 快捷方式或常见路径的 DeepSeek.exe，打开本机应用。未找到时在桌宠气泡中提示；不会自动转为浏览器页面。

## 本地数据

运行状态和错误日志：`%LOCALAPPDATA%\WhaleMaidPet\`。
自启动：当前用户注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 中的 `WhaleMaidPet`。
退出只关闭本次运行；要停用下次登录自启动，请使用右键菜单。

程序无声音、无系统提醒弹窗，不采集或保存键盘输入内容。PNG 和界面全部打包在 exe 内。

## 从源码构建

安装 .NET 10 SDK，在仓库根目录用 PowerShell 执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\windows\build.ps1 -Runtime win-x64
# Windows ARM 版
powershell -ExecutionPolicy Bypass -File .\windows\build.ps1 -Runtime win-arm64
```

构建脚本先运行状态检查，再发布到 `dist/win-x64` 或 `dist/win-arm64`。发布文件是自包含的单文件，未使用商业代码签名证书。

## 验证范围

Windows 状态逻辑 35 项检查通过，x64 和 ARM64 版本已在 Mac 上交叉编译。检查覆盖饭点边界、休眠跨日、摸头阈值、闲置阈值、预览到期、时区换算和保存状态。

当前没有 Windows 实机，因此透明窗口、系统托盘、DPI、拖动、自启动和 DeepSeek 应用启动尚未完成 Windows 实机验证。这些 Windows API 路径已实现并通过编译。

## 实现依据

- [Microsoft：单文件自包含发布](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- [Microsoft：GetLastInputInfo 空闲检测](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getlastinputinfo)
