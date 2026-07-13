# Git submodule 共享更新架构

本项目通过 `shared\DesktopUpdateKit` Git submodule 接入独立维护的共享更新组件。主仓库只记录经过验证的组件提交 SHA，不跟随远程分支自动漂移。

## 初始化

```powershell
git clone --recurse-submodules https://github.com/wet86y/table-cleaner.git
git submodule update --init --recursive
```

## 项目边界

- `src\TableCleaner\TableCleaner.csproj` 链接 submodule 中的更新客户端、模型、下载会话和启动器源码。
- `shared\DesktopUpdateKit\src\UpdaterStub` 构建 NativeAOT 更新器并嵌入正式单 EXE。
- `release.config.json` 保存本项目的仓库、资产名、项目文件、产物目录和下载节点。
- `scripts\build-release.ps1`、`prepare-release-assets.ps1` 和 `publish-release.ps1` 是宿主入口，实际发布逻辑来自 submodule。
- submodule 缺失时，项目和脚本明确提示初始化，不回退到本机兄弟目录。
- “关于”页面的布局和项目介绍由笨蛋表格维护；共享组件只负责更新状态、下载控制、校验和安装。

## 更新 DesktopUpdateKit

submodule 初始化后通常处于 detached HEAD。修改共享组件前，应进入其独立分支：

```powershell
Set-Location .\shared\DesktopUpdateKit
git switch main
git pull --ff-only
```

共享组件修改必须先在其仓库提交并推送，然后回到本仓库更新 gitlink：

```powershell
Set-Location ..\..
git add .\shared\DesktopUpdateKit
git commit -m "build: update DesktopUpdateKit submodule"
```

不要让主仓库引用远程尚不存在的共享组件提交，也不要对已经被宿主引用的共享组件历史执行强制推送。

## 验证与发布

```powershell
.\scripts\run-self-check.ps1
.\scripts\build-release.ps1
.\scripts\prepare-release-assets.ps1 -Version 1.0.0
.\scripts\publish-release.ps1 -Version 1.0.0
```

最后一个命令才访问 GitHub Release；源码 `git push` 是独立动作。正式发布前，主仓库和 submodule 工作区都应干净，且 `git submodule status --recursive` 不得出现 `-`、`+` 或 `U`。

## 许可边界

- `DesktopUpdateKit` 采用 MIT License，完整文本位于 submodule 的 `LICENSE`。
- Release 资产应包含主项目许可证、`NOTICE`、`THIRD-PARTY-NOTICES.md` 和 `DesktopUpdateKit-LICENSE.txt`。
- 笨蛋表格采用 Apache License 2.0；完整文本位于仓库根目录 `LICENSE`。
