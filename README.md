# 浏览器多开管理器（BrowserMulti）

> 基于 Chromium `--user-data-dir` 的浏览器多开与 Cookie 隔离工具，内置 UserAgent 伪装与内核级指纹防护。
>
> **作者：jtxu9527** | 版本 v1.5.0 | 仅支持 Windows

## 这是什么

一个绿色单文件的 Windows 桌面工具，用于在同一台电脑上开出多个**互相隔离、登录态持久**的浏览器实例。每个实例拥有独立的数据目录，Cookie、LocalStorage、扩展、缓存、登录状态完全隔离且跨会话保留（非无痕模式）。

## 核心特性

### 多开与隔离
- 每实例独立 `--user-data-dir`，Cookie/登录态彻底隔离且**持久保存**
- 自动检测系统中的 Edge / Chrome（注册表 App Paths + 常规路径双探测）
- 双击启动 / Ctrl 多选批量启动 / 一键全部启动
- 桌面快捷方式一键生成，日常启动无需打开管理器
- WMI 实时检测实例运行状态，防重复启动确认
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

### 实例管理
- 实例增删改、启动页配置、磁盘占用统计（后台线程不卡 UI）
- 一键清缓存（仅 Cache/Code Cache/GPUCache/Service Worker 存储，登录态原封不动）
- 配置存于 exe 同目录 `instances.json`，数据存于 `Data/`，绿色便携

## 使用

双击 `浏览器多开管理器.exe` → 新建实例 → 双击列表行或点「启动选中」。

## 从源码构建

需要 .NET 10 SDK（VS2026 自带）：

```bash
cd BrowserMulti
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

产物为单文件 exe（约 580KB，需目标机器装有 .NET 10 桌面运行时）。

## 重要说明与已知限制

1. **指纹防护仅对 Microsoft Edge 有效**。Chrome 137 起已移除 `--load-extension` 命令行参数，用 Chrome 启动时防护扩展不会加载（Cookie 隔离、UA 伪装不受影响）。界面会在选择 Chrome 时提示。
2. **WebRTC 防护是 JS 层拦截**：若浏览器设置中关闭了「停用非代理 UDP」，公网 IP 仍可能通过 STUN 暴露。
3. **时区固定为中国**：如代理出口在境外，时区与 IP 会不一致，代理功能上线时将改为跟随代理地区。
4. **代理**：当前版本代理字段已预留（`ProxyServer`），不填写即为直连，下版本开放每实例独立代理配置。
5. **跨电脑迁移**：数据目录可整体拷贝，书签/历史/扩展/自动填充完整保留；但 **Cookie 与保存的密码**受 Windows DPAPI 绑定加密，换机后需重新登录一次。
6. 字体防护通过 FontFaceSet 拦截实现，无法覆盖基于测量宽度的底层字体探测，这是纯扩展方案的边界。

## 防关联能力边界（诚实声明）

本工具提供「Cookie 隔离 + UA + 硬件指纹」两层，**不包含 IP 隔离**。同 IP 多账号会被平台标记为同网段账号簇——配合指纹伪装可塑造"同一网络下的不同设备"人设，社交/内容类平台风险较低；电商/广告类场景强烈建议配合独立代理使用。

## 目录结构

```
BrowserMulti/          C# WinForms 源码
├── BrowserMulti.csproj
├── Program.cs         入口
├── MainForm.cs        主界面（列表/托盘/启动/快捷方式）
├── InstanceDialog.cs  实例编辑对话框
├── Models.cs          数据模型与 UA 预设
├── InstanceManager.cs 配置持久化
├── BrowserDetector.cs 浏览器检测
├── FingerprintExtension.cs  指纹防护扩展生成器
└── app.ico            应用图标
```

## 许可证

MIT License — Copyright (c) 2026 jtxu9527，详见 [LICENSE](LICENSE)。
