# 鲸鱼娘 Mac 桌宠

使用 Swift + AppKit 实现的透明窗口桌宠，带有蓝色鲸鱼女仆的 3D 风格精灵图。

![六种表情](assets/states.png)

## 功能

- 角色保持原地，可手动拖动并保存位置、调整大小。
- 左键摸头：5 次害羞、20 次红脸嗔怒，15 秒不触碰恢复。
- 右键打开菜单：喂米饭、打开本机 DeepSeek 应用、表情预览、设置、退出。
- 键盘或鼠标闲置 15 秒：进入无聊状态。
- 每天北京时间 08:00、12:00、18:00：进入饥饿状态，选择喂米饭后恢复；支持休眠后补上错过的饭点和保存饥饿状态。
- 六种表情可即时预览，每次预览保持 4 秒。
- 可选登录自启动。所有互动本地运行，无声音或系统通知。

## 构建和运行

需要 macOS 26 或更新版本，以及 Xcode Command Line Tools。当前版本已在 Apple Silicon Mac 上验证。

```sh
xcode-select --install  # 未安装命令行工具时执行
chmod +x build.command enable-startup.command
./build.command
open WhalePet.app
```

构建脚本生成 WhalePet.app，并运行状态逻辑和表情预览回归检查。生成的 app 未经 Apple 公证。

## 登录自启动

构建后执行 `./enable-startup.command`。程序使用项目所在目录，请保持目录位置稳定；搬动后重新执行该脚本。

关闭自启动：右键菜单选择“关闭登录自启动”。退出桌宠只退出本次运行。

## 文件与实现

- `WhalePet.swift`：完整源码和验证入口。
- `assets/states.png`：透明背景六表情，正常、无聊、饥饿、害羞、嗔怒、吃饭。
- `Info.plist`：app 配置。
- `build.command`：构建并签名。
- `enable-startup.command`：生成当前用户的 LaunchAgent。

运行时生成的状态、日志和编译文件被 Git 忽略。角色采用二维图片和淡入淡出效果，并非三维骨骼模型；未接入 DeepSeek API。打开 DeepSeek 功能会查找 Desktop 或 Applications 中已有的 DeepSeek.app。

## 素材来源

这是个人制作的社区同人桌宠，不是 DeepSeek 官方产品。形象由用户提供角色参考，使用 imagegen 制作 Q 版和 3D 风格六表情素材。仓库不包含原始参考图。未为代码或角色素材授予额外的开源许可证。
