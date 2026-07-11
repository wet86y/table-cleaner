# 构建产物维护约定

本项目只有两类可运行产物。它们都从 `Release` 配置生成，但用途和维护位置不同。

项目根目录为 `D:\项目开发\表格工具`。`Directory.Build.props` 将所有编译输出集中到根目录 `build`，因此源码目录下不应再出现 `bin` 或 `obj`。

| 类型 | 唯一维护目录 | 生成方式 | 用途 |
|---|---|---|---|
| 调试运行版 | `build\bin\Release` | `dotnet build .\src\TableCleaner\TableCleaner.csproj -c Release` | 本机功能验证与调试 |
| 正式打包版 | `artifacts\表格工具-win-x64` | `scripts\build-release.ps1` | 对外分发 |

## 强制约束

1. 调试只维护 `build\bin\Release`，不要创建 `build\bin\Debug` 输出，也不要创建调试分发目录。
2. 正式包只维护 `artifacts\表格工具-win-x64`，目录中只应包含 `表格工具.exe`；不得保留 `.pdb` 或其他运行产物。
3. 不得创建、更新或交付任何 `artifacts\*-debug-*` 目录。
4. `build/` 和 `artifacts/` 均为构建产物；只能通过构建命令或发布脚本更新，不能手工编辑。
5. 完成功能修改后，先更新调试运行版并测试；只有需要对外分发时才重新运行正式打包脚本。
6. 清理时可以删除所有 `Debug` 目录和未被上述两类产物使用的临时发布目录；不得误删两个约定目录。
