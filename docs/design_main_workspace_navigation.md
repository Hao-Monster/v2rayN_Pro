# 主窗口左侧导航栏与由左向右三栏工作区设计

## 目标

在不增删或改动任何按钮命令、事件链、快捷键与右键菜单的前提下，将主窗口与代理页改造为由左向右递进的三栏布局（Windows WPF 与 Desktop Avalonia 双端同步）：

- **Col 0（左侧纵向导航栏，`96px`）**：承载页面级工作区切换、向右展开的原顶栏下拉菜单以及底部系统工具入口，兼容中英文等多语言标签完整显示与自动换行。
- **Col 1（订阅分组卡片栏，代理页内默认 `280px`，支持 `GridSplitter` 在 `210px`~`420px` 调整宽度）**：将原顶部横向包裹的订阅标签栏改为纵向滚动的富信息订阅卡片列表（`SubscriptionCardItem`）。
- **Col 2（主内容与节点工作区，`*`）**：代理页顶部放置标题与搜索/列宽工具，紧接由左向右的步骤胶囊栏，下方全高承载原生节点 `DataGrid`。

## 左侧纵向导航栏（Col 0）

原顶部水平工具栏拆分为三组纵向排列的导航元素：

1. **主工作区切换组**：`首页 (btnHome)`、`代理 (btnProxy)`、`分流 (btnClashProxies)`、`连接 (btnClashConnections)`、`日志 (btnLogs)`。其中 `分流` 与 `连接` 继续绑定 `ShowClashUI`，仅在 Clash / Mihomo / sing-box 等支持分流面板的内核下显示。
2. **下拉菜单组**：`配置文件 (menuServers)`、`订阅分组 (menuSubscription)`、`设置 (menuSetting)`。保持原有全部子菜单层级与绑定，弹出方向改为向右展开（WPF `PlacementMode.Right` / Avalonia `RightEdgeAlignedTop`）。
3. **底部系统组**：`新版本提示 (btnNewUpdate)`、`外观 (conTheme / popupAppearance，向右上方弹出)`、`帮助 (menuHelp，向右上边缘对齐弹出)`、`重载 (menuReload)`、`官网 (menuPromotion)`、`关闭 (menuClose)`。

底部 `StatusBarView` 保持跨主内容区底部通栏展示，所有入站信息、系统代理切换、路由规则、TUN 开关与实时流量速率逻辑不变。

## 代理工作区由左向右布局（Col 1 + Col 2）

1. **顶部页头栏**：左侧显示“代理”标题与当前选中订阅分组胶囊标签（分组名为空时自动隐藏）；右侧放置节点过滤输入框（`txtServerFilter`）与自动调整列宽按钮（`btnAutofitColumnWidth`）。
2. **左侧订阅分组富信息卡片栏（Col 1）**：移除原 `lstGroup` 最高占屏幕高度 20% 的横向限制，改为全高纵向滚动富信息卡片列表（绑定 `ProfilesViewModel.SubscriptionCards` 与 `SelectedSubscriptionCard`，并与原有 `SubItems` / `SelectedSub` 双向同步）。每张卡片常驻展示完整订阅与节点健康度信息：
   - **卡片头部与内联更新**：多行自适应分组标题 + 右上角内联胶囊按钮 `更新订阅` / `更新全部`（绑定 `UpdateSubscriptionCardCmd`，复用 `AppEvents.CurrentSubscriptionUpdateRequested` / `SubscriptionsUpdateRequested`）。
   - **状态与扩展徽章**：`活动配置`（当前活动节点所属订阅实时联动）、`自动更新 Xh/Xm` / `手动更新` / `本地分组` / `汇总 N 个订阅`、`已禁用`，以及存在有效外链时按需显示的 `官网` / `客服` 快捷胶囊。
   - **流量配额与上下行明细**：有限额套餐展示高亮配额盒、剩余百分比与动态进度条（按剩余比例自动切换绿/橙/红状态）；不限额套餐展示不限流量标识与累计已用、上行（`↑`）、下行（`↓`）流量明细。
   - **有效期、同步时间与公告备注**：展示到期日期及剩余天数/小时倒计时（或长期有效/已过期高亮）、最近同步时间，以及内嵌圆角卡片展示机场公告（`Announce`）与用户备注（`Memo`，悬停 `ToolTip` 展示完整长文本）和过滤/指定内核/转换目标/附加源摘要。
   - **节点健康度与三色比例条**：常驻展示 `可用 / 已测 / 总数`、可用节点 `中位延迟`、`最近测速` 时间戳与超 30 分钟过时提示，以及底部按 `低延迟绿 / 较高延迟橙 / 失败红 / 未测灰` 比例切分的健康条；在实时测速过程中原地无闪烁刷新。Desktop 端同时保留原有右键分组菜单（`menuSubEdit / menuSubAdd / menuSubDelete`）。
3. **右侧步骤流与节点列表（Col 2）**：
   - 步骤胶囊栏按操作动线由左向右排列：`① 添加订阅 (btnAddSub)` ──→ `② 修改订阅 (btnEditSub)` ──→ `③ 延迟测试 (btnFastRealPing)`（保留隐藏热键入口 `menuMixedTestServer`）。
   - 下方全高放置原生节点表格（`lstProfiles`），完整保留全部 14 列、列头点击排序、拖拽排序、双击编辑、回车设为活动节点及完整右键上下文菜单。

## 无感切换与按需挂载

- 主窗口启动时常驻挂载首页（`SubscriptionInfoView`）、代理页（`ProfilesView`）与日志页（`MsgView`），切换工作区仅修改容器显隐状态，不销毁控件、不重读数据库、不中断后台测速或订阅更新任务；`分流 (ClashProxiesView)` 与 `连接 (ClashConnectionsView)` 在 `Tab` 单页模式下按需挂载并在离开该页面时卸载视图内容，避免未打开分流/连接页面时在后台持续轮询 Clash API。
- `EMainWorkspacePage` 枚举定义为 `Home`、`Proxy`、`ClashProxies`、`ClashConnections`、`Logs`，持久化于 `UIItem.MainWorkspacePage`（首次启动默认 `Home`）。
- 在 `Tab` 单页全高模式下，4 个子视图通过独立显隐容器按当前选中的 `EMainWorkspacePage` 精确切换，避免内核重载重置 `TabMainSelectedIndex` 时将用户从 `日志` 页误切回 `代理` 页；当 `ShowClashUI` 变为 `false` 或切换回分栏模式且当前处于临时子页时，自动回退并同步持久化为 `Proxy` 页。

## 布局兼容与存量配置迁移

- `UIItem.MainGirdOrientation` 新配置默认值设为 `EGirdOrientation.Tab`，并在 `UIItem` 中新增布尔标记 `LeftRailLayoutMigrated`。
- `ConfigHandler.ApplyUiLayoutMigration` 在 `ConfigHandler.LoadConfig` 中检测到 `!LeftRailLayoutMigrated` 时，自动将存量 `MainGirdOrientation` 迁移为 `EGirdOrientation.Tab`、置位标记并原子持久化回配置文件，同时对异常或临时工作区枚举值做安全归一，使老用户升级后直接获得全高左侧导航切换体验。
- 用户仍可在设置窗口中手动切换回 `Horizontal` 或 `Vertical` 传统分栏布局；处于分栏模式时，左侧导航栏自动隐藏独立的 `分流 / 连接 / 日志` 页面按钮，交由分栏内嵌标签页承载。

## 添加订阅入口协同

- **首页顶栏**：在首页主内容区右上角保留独立顶栏与 `添加订阅 (btnAddSubscription)` 按钮，通过 `ProfilesViewModel.AddSubscription()` 触发新增订阅流程。
- **代理页步骤栏**：步骤 `① 添加订阅 (btnAddSub)` 直接绑定 `ProfilesViewModel.AddSubCmd`。
- 新增订阅弹窗、自动选中新分组、自动更新、真延迟测速与升序排序逻辑均保持不变。
