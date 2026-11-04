# 定制功能清单

本文是上游升级时必须逐项核对的功能目录。状态以 `custom/main` 为准。

| 功能 | 主要位置 | 升级风险 |
| --- | --- | --- |
| 新增订阅后的自动更新、真延迟测试、排序和激活 | `ProfilesViewModel.cs`、`StatusBarViewModel.cs` | 高 |
| 手动/定时订阅更新后的逐分组真延迟与升序排序 | `MainWindowViewModel.cs`、`ProfilesViewModel.cs` | 高 |
| 首页五色网络状态与一键设置网络 | `SubscriptionInfoViewModel.cs`、Windows SubscriptionInfo 视图 | 高 |
| 连续失败自动换点、直连更新和 Google 204 验证 | `MainWindowViewModel.cs`、`ConnectionHandler.cs` | 高 |
| 每订阅最后可用快照与失败恢复 | `SubscriptionSnapshotHandler.cs` | 高 |
| 首次启动自动打开新增订阅窗口 | 主窗口及 Profiles 事件链 | 高 |
| 延迟升序排序，失败节点排在最后 | `ProfilesViewModel.cs` | 中 |
| 自动流程中的无效节点清理范围限制 | `ProfilesViewModel.cs` | 高 |
| CloudflareST 最优节点生成与日志 | `CloudflareSpeedTestService.cs` | 高 |
| Cloudflare trace 地区探测 | 测速服务及节点模型 | 中 |
| 左侧纵向导航栏与由左向右三栏代理工作区布局 | `EMainWorkspacePage.cs`、`ConfigItems.cs`、`ConfigHandler.cs`、`MainWindowViewModel.cs`、Windows 与 Desktop `MainWindow` / `ProfilesView` | 高 |
| 订阅公告、流量、到期时间和跳转链接 | `SubscriptionInfoViewModel`、SubscriptionInfo 视图 | 中 |
| 订阅链接优先、自动生成别名 | 订阅编辑窗口及订阅更新流程 | 高 |
| 静态/GIF 背景、透明度和外观入口 | `WorkspaceBackgroundHandler`、主题和主窗口视图 | 中 |
| HWID 默认请求标识 | `DeviceInfoHelper.cs` 及请求处理 | 中 |
| Windows 与 macOS ARM64 完整运行时打包 | `package-osx.sh`、GitHub Actions | 高 |
| macOS/Linux TUN 环境变量传递 | `CoreAdminManager.cs` | 低，已同步官方修复 |
| TUN 全流量接管（双栈 FakeIP、DoH/DoT 旁路阻断、单核 `sing-box` 优选、系统代理自动协同） | `TunTakeoverPolicy.cs`、`CoreConfigContextBuilder.cs`、`Singbox*Service.cs`、`V2ray*Service.cs`、`StatusBarViewModel.cs`、`OptionSettingViewModel.cs` | 高 |

## 冲突热点

官方更新时优先审查以下文件，不允许只根据编译是否通过判断合并成功：

- `v2rayN/ServiceLib/Models/Configs/ConfigItems.cs`
- `v2rayN/ServiceLib/ViewModels/MainWindowViewModel.cs`
- `v2rayN/ServiceLib/ViewModels/ProfilesViewModel.cs`
- `v2rayN/ServiceLib/ViewModels/StatusBarViewModel.cs`
- `v2rayN/ServiceLib/ViewModels/SubscriptionInfoViewModel.cs`
- `v2rayN/ServiceLib/ViewModels/OptionSettingViewModel.cs`
- `v2rayN/ServiceLib/Services/TunTakeoverPolicy.cs`
- `v2rayN/ServiceLib/Handler/Builder/CoreConfigContextBuilder.cs`
- `v2rayN/ServiceLib/Manager/CoreManager.cs`
- `v2rayN/ServiceLib/Manager/TaskManager.cs`
- `v2rayN/ServiceLib/Handler/SubscriptionSnapshotHandler.cs`
- `v2rayN/ServiceLib/Handler/ConnectionHandler.cs`
- `v2rayN/ServiceLib/Manager/CoreAdminManager.cs`
- `v2rayN/v2rayN/Views/MainWindow.xaml`
- `v2rayN/v2rayN.Desktop/Views/MainWindow.axaml`
- Windows/Desktop 两套 Profiles、SubscriptionInfo 和主题视图
- `package-osx.sh`

## 维护规则

1. 官方已经实现的能力优先采用官方实现，并删除重复定制。
2. 共享逻辑放在 `ServiceLib`，Windows 和 Desktop 只保留平台视图。
3. 修改本清单后才能合并新增定制功能。
4. 删除功能时也要从本清单和对应设计文档中删除。
