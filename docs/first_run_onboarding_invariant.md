# 首次安装流程维护约束

## 不可修改的首次安装流程

首次启动时，如果订阅和节点配置都为空，必须继续使用既有流程：

1. 主窗口通过 `AppEvents.AddSubscriptionRequested` 打开原订阅弹窗。
2. 用户确认订阅后发布 `CurrentSubscriptionUpdateRequested`。
3. 使用既有 `SubscriptionHandler.UpdateProcess` 更新订阅。
4. 更新成功后发布 `SubscriptionAutoSpeedtestRequested`。
5. 使用既有真延迟测试、延迟排序、活动节点选择和
   `ESysProxyType.ForcedChange` 系统代理设置流程。

不得用网络快照、一键设置网络或自动修复工作流替代、包裹或提前触发上述流程。

## 自动检查启用边界

网络健康巡检和自动修复只在软件启动时同时满足以下条件时启用：

- 至少存在一个已启用且带订阅链接的订阅；
- 至少存在一个节点配置。

首次安装、空订阅和空节点状态不启动网络健康巡检，也不发布自动修复状态。
首次安装完成后的当前进程继续沿用首次安装流程；新增巡检从下次正常启动开始生效。

## 回归检查

- `NetworkRecoveryEligibilityTests` 必须覆盖无订阅、无节点以及两者同时存在的情况。
- Windows 首次弹窗继续通过 `AppEvents.AddSubscriptionRequested` 触发。
- 首次订阅更新成功后继续发布 `SubscriptionAutoSpeedtestRequested`。
- 首次安装流程不得进入 `UpdateSubscriptionProcessCore`。
