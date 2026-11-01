# TUN 全流量接管设计说明

## 背景与目标

默认 `v2rayN` 开启 TUN 后仍存在四类旁路与不一致问题，尤其在桌面端 Electron 应用（Chromium 网络进程 + Node.js / Go / Rust / Python 子进程混合通信）中表现明显：

1. **IPv6 直连逃逸**：旧配置默认 `EnableIPv6Address = false` 且 `BlockAAAAQuery = false`，拥有公网/SLAAC IPv6 的宿主机会通过物理网卡直连 AAAA 记录。
2. **DNS 污染与 FakeIP 缺失**：`SimpleDNSItem.FakeIP` 默认关闭，且仅配置 IPv4 池；当旧版 TUN IPv6 段 `fc00::172:18:0:1/126` 与 `fc00::/18` FakeIPv6 段同时启用时，甚至会触发 `sing-box` CIDR 重叠启动失败。
3. **应用层加密 DNS（DoH / DoT）绕过**：主流白名单分流将 `223.5.5.5`、`119.29.29.29` 等公共 DNS IP 放行直连，导致浏览器或客户端内置 DoH 直接向明文/境内外 DoH 端点查询并返回污染 IP，且 QUIC/ECH 下流量嗅探不稳定。
4. **双入口撕裂与双核级联开销**：TUN 与系统代理同时处于开启状态，使浏览器走系统 HTTP 代理而子进程走三层 TUN；同时未显式绑定核心的标准节点默认走 `sing-box (TUN) -> 127.0.0.1 SOCKS -> Xray` 双进程串联。

## 核心策略（`TunTakeoverPolicy`）

共享策略集中于 `v2rayN/ServiceLib/Services/TunTakeoverPolicy.cs`，不向平台 UI 层散落判断：

1. **一次性平滑迁移（`ApplyTunConfigMigration`）**
   - 通过 `TunModeItem.TunTakeoverSchemaVersion`（当前 `1`）将历史未修改默认值一次性升级为：`EnableIPv6Address = true`、`Stack = "mixed"`、`Mtu = 1500`，并将与 `fc00::/18` 重叠的旧默认 `fc00::172:18:0:1/126` 迁移至非重叠 ULA 段 `fdfe:dcba:9876::1/126`。
   - 迁移完成后保留用户后续在设置界面的手动调整。
   - 若用户手动关闭 `EnableIPv6Address`，`ShouldSuppressIpv6Dns` 自动在 TUN DNS 规则首部拦截 AAAA（`NOERROR` 空响应），阻断物理网卡 IPv6 逃逸。

2. **TUN 作用域双栈 FakeIP 与子网防冲突**
   - `AutoFakeIPInTun = true` 时，仅在 `IsTunEnabled`（且未启用自定义 Raw DNS）期间激活 `198.18.0.0/15` + `fc00::/18` 双栈 FakeIP，并开启 `experimental.cache_file.store_fakeip = true`，不影响纯系统代理模式行为。
   - `ResolveNonOverlappingFakeIpv6Range` 使用 `IPNetwork2.Overlap` 校验 TUN 接口 IPv6 地址与 FakeIPv6 地址池，冲突时自动回退至 `fd00:198:18::/48`。
   - Xray TUN 兼容路径对 `tunInbound.sniffing` 执行深拷贝，并在 `RouteOnly = true` 时将 `destOverride` 修正为 `["fakedns+others"]`，同时将 `FakeDns4Ray.poolSize` 上限固定为 `65535`。

3. **加密 DNS（DoH / DoT）与 DDR 旁路阻断**
   - `BlockEncryptedDnsBypass = true` 时，在 DNS 层对 `tun` 入站发起的公共 DoH 域名及 RFC 9462 DDR（`resolver.arpa` / `_dns.resolver.arpa`）查询直接返回 0-RTT 短路响应（`sing-box` 返回 `NXDOMAIN`，`Xray` 非核心上游域名映射 `0.0.0.0`），同时自动豁免 `ProtectDomainList` 与用户自定义 `Hosts`，避免无谓分配 FakeIP 或触发冗余 443 握手。
   - 在 `sing-box` 与 `Xray` 路由规则中（核心自身进程 `process_path` 直连规则之后、用户白名单规则之前）插入仅限 `tun` 入站的三条独立阻断规则：`port = 853`、`port = 443 + TunEncryptedDnsGuardIps`、`port = 443 + GetDnsGuardBlockedDomains`，迫使硬编码 IP/DoH 客户端瞬间回退到被 `hijack-dns` 接管的系统 DNS，同时不影响核心自身使用 DoH 上游；`Clash/Mihomo` TUN 配置同步写入 `inet6-address` 与 `route-exclude-address`。

4. **TUN 单核 `sing-box` 自动优选与安全门禁**
   - `PreferSingboxInTun = true` 时，在 `CoreConfigContextBuilder.Build` 中通过 `CanRunSingleCoreSingboxInTun` 检查节点、策略组/代理链子节点及当前 Xray 模板状态：凡未显式强制指定 `Xray`、未启用自定义 Xray `FullConfigTemplateItem` / `DNSItem` / 非 `tlshello` TCP 分片，且协议/传输/证书固定属性受 `sing-box` 完整支持的节点（含 `CoreType = null` 的 `sing-box` 专属协议如 `TUIC`/`Anytls`/`Naive`），在 TUN 模式下直接以 `sing-box` 单核运行，省去 `PreSocks` 本地串联。
   - 使用 `xhttp`、`kcp`、`Finalmask`、非 Shadowsocks `raw+http` 头部伪装、Xray 专属证书/抗量子校验（`CertSha` / `Mldsa65Verify` / `VerifyPeerCertByName`）、`SOCKS+TLS`、自定义 `HTTP` 请求头、Xray 专属 VLESS Encryption 或显式指定 `Xray` 的节点自动回退至既有 `sing-box + Xray PreSocks` 双核保护链路。

5. **TUN 与系统代理协同及不变量保护**
   - `ClearSysProxyWhenTun = true` 时，`StatusBarViewModel` 在开启 TUN 成功后记录原系统代理状态（含原本即为 `ForcedClear` 的情形）并切换为 `ForcedClear`；关闭 TUN、在设置界面实时切换 `ClearSysProxyWhenTun`、或下次以非管理员启动无法拉起 TUN 时按原始状态精确恢复（原本为 `ForcedClear` 时绝不误开启系统代理）；Windows UAC 取消或 macOS/Linux 授权取消时自动回滚配置与 UI 开关状态。
   - `MainWindowViewModel` 与 `ProfilesViewModel` 统一调用 `IsEffectiveTakeoverActive` 与 `GetDesiredSysProxyForWorkflow`：当 `EnableTun == false` 时严格保持 `ESysProxyType.ForcedChange`（不破坏首次启动引导与网络自愈不变量）；当 `EnableTun == true` 时保持系统代理清理状态且不丢失激活节点。

