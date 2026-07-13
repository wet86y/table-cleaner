# 发布检查清单

## 仓库与依赖

- [ ] 当前分支和 `git status` 符合本次发布范围。
- [ ] `git submodule status --recursive` 未出现 `-`、`+` 或 `U`。
- [ ] `shared\DesktopUpdateKit` 指向已经推送到公开远程仓库的提交。
- [ ] 仓库不依赖本机绝对路径或仓库外兄弟目录。
- [ ] 根目录 `LICENSE`、`NOTICE` 和第三方归属文档已同步。

## 编译与自检

- [ ] 执行 `.\scripts\run-self-check.ps1`。
- [ ] 执行 `.\scripts\build-release.ps1`。
- [ ] 构建末尾出现 `Release executable verification passed`；共享工具会按 `releaseVerificationArguments` 验证最终单文件 EXE 内嵌更新器及更新窗口关键状态无控件重叠。
- [ ] 正式 EXE 位于 `artifacts\笨蛋表格-win-x64\笨蛋表格.exe`。
- [ ] 正式目录不包含 PDB 或本地配置数据。
- [ ] 剪贴板、文件导入、清洗、模板、替换、合并和导出完成核心手测。
- [ ] “关于”页的检查更新、下载、暂停/继续、取消和安装流程正常。

## Release 资产

- [ ] `Directory.Build.props`、tag 和发布版本一致。
- [ ] `release.config.json` 的仓库、资产名、目录和 `downloadNodes` 正确。
- [ ] 执行 `.\scripts\prepare-release-assets.ps1 -Version <version>` 后，`update.json` 的版本、大小和 SHA-256 与 EXE 一致。
- [ ] Release 资产包含主项目许可证、`NOTICE`、第三方声明和 DesktopUpdateKit MIT 文本。
- [ ] 主仓库和 submodule 均已提交且工作区干净，再执行发布脚本。
- [ ] GitHub Release 资产发布与源码 `git push` 分开确认。
