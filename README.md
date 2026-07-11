# 表格工具

Windows 轻量绿色小工具——从剪贴板或文件导入表格数据，快速清洗、去重合并、替换、应用模板。

## 功能

| 功能 | 说明 |
|------|------|
| 剪贴板导入 | 从 Excel / WPS / 网页复制表格后直接导入，支持 TSV/CSV 风格数据 |
| 文件导入 | 导入 CSV、XLS/XLSX 文件；多 Sheet Excel 可分别切换处理 |
| 一键清洗 | 将竖线/TAB/逗号/分号/多空格分隔的文本伪表格转换为真实单元格表格 |
| 去重合并 | 分组列去重合并 + 求和列自动加总 + 其他列合并连接 |
| 模板库 | 普通模板（列映射输出）+ 筛选模板（匹配值分拆拼接），支持包含/精确匹配 |
| 替换库 | 分组规则管理，支持普通替换、拓展覆盖、拓展插值三种模式 |
| 编辑模式 | 单元格编辑、行列增删、列头重命名，全链路接入撤销栈 |
| 离散合并 | 列合并 / 行合并——把离散在多列/行中的非空数据归纳到目标列/行 |
| 导出 | CSV / XLSX 导出；配置包导入/导出 |
| 绿色免装 | 单 exe 运行，不依赖 Office/WPS COM，不要求管理员权限 |

## 构建

项目只维护 Release 源码路径；调试运行版和打包发布版都从同一套 Release 配置产出。

项目根目录固定为 `D:\项目开发\表格工具`。源码位于 `src\TableCleaner`，脚本位于 `scripts`，构建生成物统一位于 `build`，正式发布包位于 `artifacts`。

| 版本 | 命令 | 产物位置 | 用途 |
|------|------|----------|------|
| 调试运行版 | `dotnet build .\src\TableCleaner\TableCleaner.csproj -c Release` | `build\bin\Release\表格工具.exe` | 本地测试 |
| 打包发布版 | `scripts\build-release.ps1` | `artifacts\表格工具-win-x64\表格工具.exe` | 自包含单文件，分发用 |

## 脚本

| 脚本 | 用途 |
|------|------|
| `scripts\run-dev.ps1` | 快速 restore + 调试运行 |
| `scripts\build-release.ps1` | 打包自包含单文件发布版 |
| `scripts\run-self-check.ps1` | 构建自检 |

## 项目文档

- `docs/BRANCH_PROJECT.md`：分支项目目标、边界、维护纪律和合入条件。
- `docs/BUILD_OUTPUT_CONTRACT.md`：调试运行版与正式打包版的唯一维护路径和产物约束。
- `docs/DESIGN_NOTES.md`：关键设计决策和历史取舍。
- `docs/CHANGELOG.md`：变更历史。
- `docs/TEST_REPORT.md`：测试报告。

## 技术栈

- .NET 8
- WinForms
- ClosedXML 0.104.2
- ExcelDataReader 3.7.0

## 已知注意点

- 发布 exe 是 Windows x64 自包含单文件，体积较大是正常现象。
- 不依赖 Office / WPS COM，不要求管理员权限。
- 公司安全软件可能拦截未知 exe，需要用户手动允许。
