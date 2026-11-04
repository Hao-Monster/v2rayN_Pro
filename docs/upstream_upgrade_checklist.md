# 上游升级检查表

## 准备

- [ ] `custom/main` 工作区干净并已推送
- [ ] `origin` 和 `upstream` 地址正确
- [ ] 已执行 `git fetch upstream --prune --tags`
- [ ] 已阅读目标版本 Release Notes
- [ ] 已创建 `upgrade/<version>` 分支
- [ ] 已记录升级前 Windows 与 macOS 可用安装包

## 合并审查

- [ ] 不使用全局 ours/theirs 处理冲突
- [ ] 对照 `docs/customizations.md` 检查全部冲突热点
- [ ] 优先采用官方已有修复，删除重复实现
- [ ] 配置模型新增字段具备合理默认值
- [ ] 旧配置能够无损升级
- [ ] Windows 和 Desktop 平台行为保持一致
- [ ] 更新 `.custom/upstream.json`

## 功能回归

- [ ] 首次启动自动打开新增订阅窗口
- [ ] 新增订阅后自动更新、真延迟测试和升序排序
- [ ] 手动、托盘和定时更新均逐订阅执行真延迟与排序
- [ ] 活动节点与自动系统代理判断正确
- [ ] 首页五色网络状态与“一键设置网络”正常
- [ ] 连续失败能够先换现有节点，再直连更新全部订阅
- [ ] 新订阅全部失效时能够恢复最后可用快照
- [ ] 核心启动失败和软件退出时系统代理能够清理
- [ ] 手动延迟测试不会触发自动清理
- [ ] 最优节点生成、CFST 日志和清理范围正确
- [ ] 国家/地区探测失败不会阻断测速
- [ ] 首页、代理页、订阅流量和公告正常
- [ ] GIF 背景、透明度和持久化正常
- [ ] Windows 系统代理和 TUN 正常
- [ ] macOS ARM64 系统代理和 TUN 正常

## 工程验证

- [ ] `.\scripts\Test-CustomBuild.ps1 -Target all` 通过
- [ ] Windows x64 测试包完成实机验证
- [ ] macOS ARM64 包含完整 geodata 和核心文件
- [ ] macOS 所有 Mach-O 文件架构正确
- [ ] macOS 应用签名验证通过
- [ ] 安装包 SHA256 已记录
- [ ] Pull Request 已附升级差异和测试证据
