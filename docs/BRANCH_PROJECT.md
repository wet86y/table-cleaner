# 分支项目说明

当前分支：`master`

## 目标

本项目为笨蛋表格（TableCleaner / 笨蛋表格），是一个 Windows 桌面小工具，用于对表格数据做快速清洗。

主要入口优先支持从 Excel / WPS / 网页表格复制后通过剪贴板导入，兼顾文件导入（CSV/XLS/XLSX/TXT）。

## 维护口径

- 唯一维护源码：`src/TableCleaner`
- 调试运行版：`dotnet build .\src\TableCleaner\TableCleaner.csproj -c Release`
- 打包发布版：`scripts\build-release.ps1`
- 唯一发布现场：`artifacts\笨蛋表格-win-x64\笨蛋表格.exe`
- 不维护单独 Debug 版本，不手工编辑 `build/` 或 `artifacts/` 中的编译产物

## 工作纪律

1. 每轮改动先确认当前分支和 `git status`。
2. 代码改动保持小步，优先修当前问题，不做顺手重构。
3. 影响导入、清洗、替换、合并、导出链路时，必须同步更新相关文档和验收项。
4. 发布前必须跑 Release 编译；涉及分发时再跑打包脚本。
5. 打包产物只用于交付验证，不纳入 git。
6. 提交前检查 diff，确保每个改动都能对应当前任务。

## 高风险区域

- `CleaningService`：去重合并、求和聚合逻辑会影响数据完整性。
- `ReplacementService`：替换/拓展替换匹配和写入逻辑会影响大面积数据。
- `PseudoTableCleanService`：自动检测分隔符和表头识别规则，边界 case 多。
- `TemplateEngine`：模板匹配、筛选分组、重复表头归并逻辑复杂。
- `MainForm`：表格预览与内部 `TableData` 同步，筛选状态下编辑/删除/粘贴不能错位。

## 验收基线

每次功能性改动至少覆盖：

- 剪贴板导入 TSV/CSV 正常。
- CSV/XLS/XLSX 文件导入正常。
- 多 Sheet Excel 切换正常。
- 一键清洗伪表格正常（竖线/TAB/逗号/多空格等）。
- 列选择保留、去重合并、求和聚合正常。
- 模板库应用正常（普通模板 + 筛选模板）。
- 替换库规则执行正常（普通/拓展覆盖/拓展插值）。
- 导出 CSV/XLSX 正常。
- 撤销/恢复正常。

## 分支合入条件

- Release 编译通过。
- 必要时打包发布版已重新生成。
- 文档与代码行为一致。
- `git status` 干净。
- 提交信息能说明实际行为变化。
