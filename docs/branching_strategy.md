# v2rayN Pro 分支管理方案

## 长期分支

| 分支 | 用途 | 是否直接开发 |
| --- | --- | --- |
| `upstream/master` | Git 远程跟踪的官方 v2rayN | 否 |
| `custom/main` | 可发布的定制版本 | 只接受已验证的合并 |
| `feature/<name>` | 单项定制功能 | 是 |
| `upgrade/<version>` | 合并指定上游版本并解决冲突 | 是 |
| `release/<version>` | 发布前冻结、修复和打包 | 仅发布修复 |

`master` 保留为历史基线，不再承载新的定制开发。GitHub 默认分支使用
`custom/main`。该分支启用保护规则，禁止强制推送和删除；功能与上游升级
仍应通过 Pull Request 合并。

## 远程仓库

```text
origin    https://github.com/Hao-Monster/v2rayN_Pro.git
upstream  https://github.com/2dust/v2rayN.git
```

禁止向 `upstream` 推送。官方代码只通过 `git fetch upstream` 获取。

## 日常功能开发

```powershell
git switch custom/main
git pull --ff-only origin custom/main
git switch -c feature/<name>
```

每项功能使用独立提交。完成测试后通过 Pull Request 合并回
`custom/main`，避免在同一个提交中混合 UI、核心逻辑和打包改动。

## 合并官方更新

```powershell
.\scripts\New-UpstreamUpgrade.ps1 -UpstreamRef 7.24.1
```

脚本会检查工作区、拉取官方标签、创建 `upgrade/7.24.1`，并执行
`--no-commit` 合并。冲突必须逐项理解后解决，禁止批量选择 ours/theirs。

合并完成后：

```powershell
.\scripts\Test-CustomBuild.ps1 -Target all
git commit -m "chore: merge upstream 7.24.1"
git push -u origin upgrade/7.24.1
```

通过验收并合并到 `custom/main` 后，更新 `.custom/upstream.json`。

## 发布命名

定制发布标签采用：

```text
v<upstream-version>-custom.<revision>
```

例如 `v7.24.1-custom.1`。标签只能指向通过完整发布检查的提交。
