# 浏览器多开管理器（BrowserMulti）

> 基于 Chromium `--user-data-dir` 的浏览器多开与 Cookie 隔离工具，内置 UserAgent 伪装与内核级指纹防护。
>
> **作者：jtxu9527** | 版本 v1.6.0 | 仅支持 Windows

## 这是什么

一个绿色单文件的 Windows 桌面工具，用于在同一台电脑上开出多个**互相隔离、登录态持久**的浏览器实例。每个实例拥有独立的数据目录，Cookie、LocalStorage、扩展、缓存、登录状态完全隔离且跨会话保留（非无痕模式）。

## 核心特性

### 多开与隔离
- 每实例独立 `--user-data-dir`，Cookie/登录态彻底隔离且**持久保存**
- 自动检测系统中的 Edge / Chrome（注册表 App Paths + 常规路径双探测）
- 每实例可配置独立代理（`http` / `https` / `socks5`），留空即直连
- 桌面快捷方式一键生成，日常启动无需打开管理器
- WMI 实时检测实例运行状态，防重复启动确认
- 批量启动（带内存占用提示）、批量删除、批量生成快捷方式
- 托盘常驻，关闭窗口不退出

### 身份伪装
- **UserAgent 预设**：跟随默认 / Chrome·Windows / Chrome·macOS / Edge·Windows / Safari·iPhone / Chrome·Android / 自定义，自动附带 `--disable-features=UserAgentClientHint` 防 UA-CH 泄露
- **指纹防护**（每个实例独立种子，跨会话稳定、实例间互不相同）：

| 指纹项 | 处理方式 |
|--------|---------|
| Canvas | getImageData / toDataURL / toBlob 像素噪声（确定性，肉眼不可见） |
| WebGL | 厂商/渲染器伪装（RTX 3060 / Apple M2 / Adreno 740 按画像切换）+ readPixels 噪声 |
| AudioContext | OfflineAudioContext 采样噪声 |
| 字体枚举 | document.fonts.check 按种子过滤，Win/mac/Linux 三套字体名单 |
| 语言/时区 | navigator.language(s) 按画像改写，时区锁定 Asia/Shanghai |
| 设备信息 | platform / hardwareConcurrency / deviceMemory / maxTouchPoints / plugins |
| WebRTC | SDP 候选地址改写，防内网 IP 泄露 |
| 移动端屏幕 | Screen 尺寸与 devicePixelRatio 按画像伪装 |

- 指纹画像自动跟随 UA 预设（iPhone UA → iPhone 的 platform/屏幕/GPU），保证一致性

### 管理能力
- **实例分组**：给实例打分组标签（工作号 / 小号），支持按分组筛选
- **搜索**：实时过滤实例名称、分组、备注、启动页（`Ctrl+F`）
- **排序**：点击列头按名称/分组/浏览器/UA/指纹/上次启动时间排序
- **导入导出**：配置可导出为 `.bmconfig` 文件，跨设备迁移；导入时可选保留或重新生成指纹种子
- **设置持久化**：默认浏览器、窗口位置尺寸、托盘行为、日志开关全部记忆
- **磁盘占用统计**：后台线程统计，不卡界面
- **一键清缓存**：仅清理 Cache / Code Cache / GPUCache / ShaderCache / Service Worker 存储，登录态原封不动
- **操作日志**：`logs/` 目录按天记录，自动清理 30 天前的日志
- **全局异常捕获**：任何未处理异常都会记录日志并提示，不会静默崩溃

### 快捷键

| 按键 | 功能 |
|------|------|
| `Ctrl+N` | 新建实例 |
| `Ctrl+F` | 搜索实例 |
| `F2` | 编辑选中 |
| `Delete` | 删除选中 |
| `Enter` | 启动选中 |
| `F5` | 刷新状态 |
| `F1` | 使用说明 |

## 使用

双击 `浏览器多开管理器.exe` → 新建实例 → 双击列表行或点「启动选中」。

数据全部存放在程序目录下，绿色便携：

```
程序目录\
├── 浏览器多开管理器.exe
├── instances.json      配置（名称/UA/指纹种子/分组/备注）
├── Data\<实例ID>\      各实例完整用户数据（Cookie/登录态/扩展/缓存）
└── logs\               运行日志
```

## 从源码构建

需要 .NET 10 SDK（VS2026 自带）。仓库根目录已包含解决方案文件 `BrowserMulti.sln`，可直接用 Visual Studio 打开（双击 .sln），或在命令行构建：

```bash
# 方式一：编译整个解决方案
dotnet build BrowserMulti.sln -c Release

# 方式二：直接发布为单文件 exe
dotnet publish BrowserMulti/BrowserMulti.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

产物为单文件 exe（约 620KB，需目标机器装有 .NET 10 桌面运行时）。

> 说明：`bin/`、`obj/`、`.vs/` 均为本地构建产物与 IDE 缓存，已在 `.gitignore` 中排除，不会进入版本库。

## 重要说明与已知限制

1. **指纹防护仅对 Microsoft Edge 有效**。Chrome 137 起已移除 `--load-extension` 命令行参数，用 Chrome 启动时防护扩展不会加载（Cookie 隔离、UA 伪装不受影响）。界面会在默认浏览器设为 Chrome 时给出提示。
2. **WebRTC 防护是 JS 层拦截**：若浏览器设置中关闭了「停用非代理 UDP」，公网 IP 仍可能通过 STUN 暴露。
3. **时区固定为中国**（Asia/Shanghai）：若代理出口在境外，时区与 IP 会不一致。使用境外代理时建议同时把 UA 与代理地区对齐。
4. **跨电脑迁移**：数据目录可整体拷贝，书签/历史/扩展/自动填充完整保留；但 **Cookie 与保存的密码**受 Windows DPAPI 绑定加密，换机后需重新登录一次。
5. **字体防护通过 FontFaceSet 拦截实现**，无法覆盖基于测量宽度的底层字体探测，这是纯扩展方案的边界。
6. **本程序不含浏览器内核**，调用系统已安装的 Edge / Chrome。

## 防关联能力边界（诚实声明）

本工具提供「Cookie 隔离 + UA 伪装 + 硬件指纹伪装 + 每实例独立代理」四层能力。是否被平台关联，取决于这些信号的整体一致性：

- **同 IP 多账号**会被平台标记为同网段账号簇。配合指纹伪装可塑造"同一网络下的不同设备"人设，社交/内容类平台风险较低；电商/广告类场景强烈建议为每个实例配置独立代理。
- **代理出口地区与 UA/时区需一致**，否则"境外 IP + 中国时区"本身就是异常特征。
- 行为模式（上线时间、操作节奏）同样是关联信号，建议避免所有实例同步操作。

## 目录结构

```
BrowserMulti.sln              Visual Studio 解决方案（双击打开）
README.md                     本文件
LICENSE                       MIT 许可证
BrowserMulti/                 C# WinForms 源码
├── BrowserMulti.csproj       项目文件
├── Program.cs               入口 + 全局异常捕获
├── MainForm.cs              主界面（菜单/列表/托盘/启动/快捷方式/导入导出）
├── InstanceDialog.cs        实例编辑（基本/身份与环境/高级 三个选项卡）
├── SettingsDialog.cs        设置对话框
├── AboutDialog.cs           关于对话框
├── Models.cs                数据模型、UA 预设、应用元信息
├── InstanceManager.cs       配置持久化、导入导出
├── BrowserDetector.cs       浏览器检测
├── FingerprintExtension.cs  指纹防护扩展生成器
├── Log.cs                   日志模块
└── app.ico                  应用图标
```

## 许可证

MIT License — Copyright (c) 2026 jtxu9527，详见 [LICENSE](LICENSE)。
