# TEST_REPORT

## 2026-07-13 v1.1.1 更新热修复

- 复现并确认 v1.1.0 正式 EXE 缺少 `DesktopUpdateKit.Resources.UpdaterStub.exe`，失败原因是增量发布复用了此前普通 Release 构建生成的不含 Stub 程序集。
- 发布入口新增 Release 清理，最终 EXE 的 `--verify-release` 检查可验证内嵌 Stub 存在且具有有效 PE 文件头。
- 最终 EXE 的 `--verify-ui-layout` 检查覆盖下载中和下载完成状态，验证状态、进度、按钮及更新说明的垂直边界不重叠。
- `scripts\build-release.ps1` 已通过，输出 `Release executable verification passed: --verify-release, --verify-ui-layout`。
- 共享 `DesktopUpdateKit` 会在构建后和资产准备前统一执行该验证，并拒绝不合法的验证开关。

## 2026-07-13 v1.1.0 结果正确性基线

- 新增独立 `TableCleaner.Core` 项目边界和可执行核心回归测试。
- 纯处理源码已物理迁移到 Core 的模块目录，WinForms 项目不再 linked compile 旧目录文件。
- 修复筛选状态下编辑、清空、粘贴、删除和行合并的源行映射。
- 修复保留列操作丢弃前序处理结果。
- 修复分隔文本空字段、引用逗号和引用字段内换行解析。
- 修复扩展替换插入列名/值错位及后续规则索引漂移。
- 修复分组合并键碰撞，求和列非法非空值改为阻断操作。
- 配置包补齐模板和筛选模板。
- 导出前新增表结构自检。
- 分隔记录构造不再截断比表头更宽的数据行，模板矩阵剪切板导入保留第一行。
- 保留列重排、分组/求和列冲突、筛选特殊字符和筛选后旧选择均已加固。
- CSV/TXT 覆盖 UTF-8、UTF-16、GBK；配置写入改为原子替换，配置 ZIP 限制为受控 `config.json`。
- 当前验证入口：`.\scripts\run-self-check.ps1`；历史仓库外测试记录不再作为当前版本通过依据。
- Core 回归测试 `16/16` 通过，Release 构建 0 警告、0 错误。

## 历史测试记录（不代表当前发布）

- 测试日期：2026-06-11（v2.7.2）
- 当前版本：`1.0.0`
- 测试项目路径：独立测试项目
- 测试结果：本次未重新运行单元测试；已完成 Windows `dotnet publish` 与用户手动样例回归。
- 构建：Windows dotnet publish 成功
- FileVersion：`1.0.0.0`
- 发布目录：`/home/lenovo/.openclaw/workspace/table-cleaner/publish/TableCleaner-v2.7.2/`
- 发布 ZIP：`/home/lenovo/.openclaw/workspace/table-cleaner/publish/TableCleaner-v2.7.2.zip`
- 同步 exe：`/home/lenovo/.openclaw/workspace/table-cleaner/publish/TableCleaner.exe`
- EXE SHA256：`a0652b333c0f5178f767ec7fc38e0f761bad5e30b34b6663f0fb0fce445c615f`
- ZIP SHA256：`d50135564226edd36139489ad0531acc875d7b281caef543fc65cb447b45ac42`

### v2.7.2 伪表格 / 文本分隔表验证点

- 多分隔符自动检测：`|`、TAB、`+`、`;`、`#`、`,`、多空格。
- 表头识别：全满数据可通过“首行非数值文本 + 后续含数值”识别表头。
- 剪贴板导入：优先走伪表格解析，避免先导成单列后再点击两次清洗。
- 一键清洗：不再只认 `|`，`+`、`;`、多空格等分隔符也可从剪贴板直接解析。
- 文件导入：新增 `.txt` 文件直接导入，优先伪表格解析，失败时回退 CSV/TAB。
- 编码：解析前去除 UTF-8 BOM，避免 BOM 附着在首个表头。

### v2.6.9 UI/DPI 兼容验证点

- `ColumnSelectorForm` 三栏布局已改为 `TableLayoutPanel`，避免不同 DPI/字体下标题、按钮、列表错位。
- `ReplacementEditorForm` 顶部 RadioButton 组改为 `FlowLayoutPanel`，避免二级选项挤压。
- `ScopeColumnDialog`、重命名列弹窗、命名扩展列弹窗改为自适应布局。

### v2.6.8 模板专项验证点

- 多匹配值筛选模板：按筛选项目分组，独立筛选后横向拼接。
- 重复输出表头：保留原名，不自动重命名。
- 重复源表头：应用前可提示；应用时按行取第一个非空值归并到模板列。
- Excel 合并表头：ClosedXML 读取合并区域，空表头继承合并单元格名称。
- 剪贴板空表头：向左继承最近非空表头；重复表头保留。

> 注意：该报告用于交接新会话。继续开发前建议重新运行测试，避免只看历史结论。

## v2.4.2 新增/修改

### 修复 1：Excel 合并单元格表头填充

- `ExcelService.ImportAllSheets` 中新增空表头回溯填充逻辑。
- 表头迭代中若第 i 列为空，回退找最近非空列 j，Headers[j] 赋给 Headers[i]。
- 仅处理表头行，不影响数据行。
- 全部表头为空时跳过。

### 修复 2：行列合并不再自动删除列/行

- `SelectionMergeService.MergeColumns`：删除尾部 colsToRemove 逻辑。
- `SelectionMergeService.MergeRows`：删除尾部 rowsToRemove 逻辑。
- `MainForm.cs` 合并确认对话框文案更新。

### 新增 3：全局清洗空行空列

- `SelectionMergeService.RemoveEmptyRowsAndColumns(TableData)`：删除全空行和全空列。
  - 空行：一行所有数据列为空（不含序号列）。表头行不检查。
  - 空列：一列所有数据行为空。即使表头非空也会被删。
  - 若全部被删则保留空 TableData（仅 headers）。
- `MainForm.cs`：工具栏新增"🧹 清理空行空列"按钮。
- 执行前 PushUndo。
- 表格全空时提示用户。

## 覆盖范围

测试项目 `table-cleaner-tests/Program.cs` 覆盖以下模块：

1. TSV / CSV 解析
2. CSV 导入 / 导出
3. 列保留清洗
4. 分组合并
5. 替换规则
6. 配置保存 / 加载
7. 配置包 ZIP 导入 / 导出
8. 配置目录创建
9. XLSX 导出
10. 编辑模式数据操作
11. `TableData` 基础操作
12. 伪表格清洗（v2.3.0 新增，v2.3.1 表头修复，v2.7.x 多分隔符与 TXT 导入增强）
13. SelectionMergeService 离散数据合并（v2.4.0 新增）
14. TableData.ClearCells 单元格清空（v2.4.0 新增）
15. Extended Replacement Tests（v2.5.0 新增）
16. Template Library / Matrix Tests（v2.6.x 新增）
17. HeaderNormalizationService / merged header import（v2.6.8 新增）

## 重点验证项

### 导入导出

- TSV 风格剪贴板数据可解析。
- CSV 普通字段可解析。
- CSV 带逗号引号字段可往返。
- CSV 导出后可重新导入。
- XLSX 导出可生成文件。

### 清洗与合并

- 保留指定列。
- 保留单列。
- 保留全部列。
- 非存在列场景安全处理。
- 分组列合并。
- 求和列加总。

### 替换库（v2.2.0 + v2.2.1）

- 替换规则可执行。
- 替换作用列通过分组级配置，支持精确/模糊匹配。
- 分组级作用列（ScopeColumns）+ 匹配模式（MatchMode）保存/加载不丢。
- 配置包 ZIP 往返包含分组级字段。
- 旧扁平替换规则兼容加载为默认分组。
- v2.2.1：作用列 UI 从窄 CheckedListBox 改为按钮 + 弹窗（ScopeColumnDialog）。
- v2.2.1：选择作用列的 RadioButton"包含匹配（模糊替换）"全文本不再截断。
- v2.2.1：作用列选择弹窗增加实时筛选输入框，按关键字过滤列名；勾选状态筛选前后不丢失。
- 替换执行使用 `ReplacementService.ApplyGroup()`，公式级作用列+匹配模式，不使用规则行末尾文本。

### 编辑模式

- 单元格编辑需要同步回内部 `TableData`。
- 追加空白行。
- 从剪贴板追加一行或多行。
- 删除行。
- 删除列。
- 修改操作进入撤销栈。

### 一键清洗伪表格表头（v2.3.1 新增）

- 表头识别：第一条 pipe 行后紧跟分隔线 → 表头。
- 备选识别：第一行所有内层单元格非空且后续有空单元格 → 表头。
- 有表头时：使用表头作为 TableData.Headers。
- 有表头时：只删除"表头为空且所有数据也为空"的全空列；表头非空即使数据全空也保留。
- 分隔线增强：支持 `|---|---|` / `|-------|-------|` 等 pipe 分隔线。
- 无表头时：沿用 v2.3.0 逻辑。

### 表头编辑 + 行列选择 + 离散数据合并 + Delete 清空（v2.4.0 新增，v2.4.1 修复）

- **表头可编辑（v2.4.0）**：
  - 编辑模式下双击列头弹出重命名对话框。
  - 修改后同步到 TableData.Headers。
  - 防空表头，支持撤销。
  - **v2.4.1 修复**：修改表头不再引发 RefreshGrid 崩溃（DuplicateNameException）。

- **行列选择**：
  - 点击列头选整列（Ctrl/Shift 多选）。
  - 点击序号列选整行（Ctrl/Shift 多选）。

- **离散数据合并（SelectionMergeService，v2.4.0 / v2.4.1）**：
  - Column merge：多列离散值归并到非空最多列（trim 值），其他列清空/删除。
  - Row merge：多行离散值归并到非空最多行，其他行清空/删除。
  - 并列时选择最左列/最上行。
  - 一次只能执行列合并或行合并。
  - 合并前 PushUndo，支持撤销。
  - **v2.4.1 改进**：支持从框选单元格区域（SelectedCells）提取列/行范围触发合并。

- **编辑模式下 Delete/Backspace 清空单元格（TableData.ClearCells）**：
  - 纯数据层静态方法，接收 (row, col) 列表，清空对应单元格。
  - 跳过无效索引。
  - null/空列表安全处理。
  - UI 层：编辑模式下 DgvData_KeyDown 拦截 Delete/Backspace，收集选中单元格（跳过序号列），调用 ClearCells，PushUndo。

### v2.5.0 重要验证项

- Extended 替换（Overwrite）：命中单元格替换 + 右侧 ExtraValues 覆盖。
- Extended 替换（Insert）：命中列右侧插入空列 + ExtraValues 写入。
- Empty ExtraValues 退化到 Normal 行为。
- 禁用规则不生效。
- 配置保存/加载（ReplacementType、ExtendWriteMode、ExtraValues 不丢）。
- 配置包 ZIP 往返 v2.5 字段不丢。
- 分组级作用列与扩展替换组合。

### v2.4.2 重要验证项

- 清理空行空列：
  - 有数据的行保留、全空行删除。
  - 有数据的列保留、全空列删除（表头非空的列也被删）。
  - 空表安全处理（不崩溃）。
- 合并后列/行结构不变（列数/行数不变）。
- Excel 合并单元格表头填充：
  - 空表头被前一个非空表头填充。
  - 全部表头为空时跳过。

## 已知注意点

1. **CSV 多行引号字段需谨慎**

   单元格内部真的包含换行时，需要重新用真实业务数据验证。

2. **替换语义是包含替换**

   例如规则 `AB -> X` 会影响 `ABC`。如需业务上精确匹配，后续可增加"精确匹配"开关。

3. **特殊数字格式需谨慎**

   常见中文 / 英文数字格式正常；欧式金额格式如 `1.500,50` 需要单独验证。

4. **筛选状态下的编辑需重点回归**

   因为界面可能是过滤后的视图，继续开发右键删除 / 粘贴时要防止行索引错位。

5. **发布结果必须核验 exe 版本**

   以前曾出现源码已升级但发布目录 exe 仍是旧版本的情况。以后发布后必须检查：

   - exe 修改时间
   - `FileVersion`
   - 发布路径是否为最新版本目录

6. **表头检测启发式可能误判**

   极端场景：所有行均全单元格非空且第一行无分隔线跟进时，不被判定为表头。此时数据行全部保留，自动生成列名。此场景极少出现于真实伪表格。

## 建议回归命令

在 WSL 中运行测试项目：

```bash
cd /home/lenovo/.openclaw/workspace/table-cleaner-tests
dotnet run
```

如果需要发布 Windows 单文件 exe，发布后必须确认版本和路径，不要只相信构建日志。

## 当前发布产物

```text
/home/lenovo/.openclaw/workspace/table-cleaner/publish/TableCleaner-v2.5.3/TableCleaner.exe  (最新，FileVersion=2.5.3.0)
/home/lenovo/.openclaw/workspace/table-cleaner/publish/TableCleaner.exe                       (同步)
```

## 2026-05-19 v2.5.0 替换库 v2.5 重构（ExtendMode）复核记录

### 新增文件

- `Models/ReplacementExtendMode.cs`：含 ReplacementType（Normal/Extended）和 ExtendWriteMode（Overwrite/Insert）。

### 模型变更

- `ReplacementRule.ExtraValues`（`List<string>`）：扩展替换时的附加替换值。
- `ReplacementRule.ReplacementType`：Normal 或 Extended。
- 配置序列化/反序列化支持 ExtraValues、ReplacementType、ExtendWriteMode。

### 替换服务变更

- `ReplacementService.Apply()` 根据 ReplacementType 分发：
  - **Normal**：原有替换逻辑，`before` → `after`。
  - **Extended + Overwrite**：命中单元格写替换值，右侧列就地覆盖 ExtraValues。
  - **Extended + Insert**：命中列右侧插入空列，写入 ExtraValues，不覆盖原始数据。
- 禁用规则（Enabled = false）跳过。

### 测试

- 新增 82 条 Extended Replacement Tests（第 14 节）：
  - ExtOv（Overwrite）：Fuzzy/Exact 两种匹配模式。
  - ExtIns（Insert）：基础插值、多行命中、多列命中。
  - 空 ExtraValues 退化行为等价 Normal。
  - 禁用规则不生效。
  - 配置保存/加载（ExtendWriteMode、ReplacementType、ExtraValues 不丢）。
  - 分组级作用列与扩展替换的组合。
  - 模糊部分匹配下的扩展替换。
  - 配置包 ZIP 往返不丢扩展字段。

## 2026-05-19 v2.6.0 模板库重做复核记录

### 模型变更

- `CleanTemplate` 新增 `Kind`（TemplateKind.Basic/Filter）和 `TargetHeaders`（List&lt;TemplateColumn&gt;）
- 新增 `TemplateColumn`：Header（输出列名）、SourceHeader（源列名）、Fallback（默认值）
- 新增 `FilterMatchItem`：Field + Value（等值匹配）
- `CleanTemplateFilter`：Rules→MatchItems
- 旧字段（KeptColumns/GroupColumns/SumColumns/ReplacementGroupName）已移除

### UI 变更

- `TemplateEditorForm` 完全重写为 SplitContainer 弹性布局：
  - 左面板 35%：模板列表（图标区分 Basic/Filter）+ 操作按钮
  - 右上面板 60%：模板编辑区（TargetHeaders DataGridView + 筛选面板条件显示）
  - 右下面板 40%：原始数据预览
- 旧三列（保留列/分组列/求和列）CheckedListBox 已移除

### 引擎变更

- `TemplateEngine.ApplyTemplate` 新增按 TargetHeaders 匹配源列 + Fallback 填充逻辑
- `TemplateEngine.MatchFilter` 使用 FilterMatchItem（仅等值匹配）
- 旧 ApplyTemplate 标记为 `[Obsolete]`

### 回归测试

- 测试项目：`444/444` 通过。
- 新增模板库专项测试：基础模板目标表头/源列模糊匹配/fallback；筛选模板等值保留行；多余列删除。
- Windows .NET publish 成功，产物已写入 `publish/TableCleaner-v2.6.0/TableCleaner.exe` 并同步到 `publish/TableCleaner.exe`。
- FileVersion：`2.6.0.0`。
- SHA256：`7c2f2825ad9ea03089c3761a5c9e423642fb86cce0ad685cadabc61726757183`。
- 构建警告：3 个既有 nullable/未使用字段警告，未阻塞发布。

## 2026-05-19 v2.3.1 一键清洗伪表格表头修复复核记录

### 问题

用户反馈一键清洗将表头行当作普通数据行丢弃，表头非空列被错误删除。

### 修复内容

1. **表头识别**：`DetectHeader()` + `DetectSeparatorAfterFirstPipe()`
2. **分隔线增强**：`IsSeparatorLine()` 支持 pipe 分隔线
3. **表头保护列**：只删"表头为空且全数据空"的列
4. **使用表头做列名**：有表头时 headers 来自第一行内容

### 代码变更

- `Services/PseudoTableCleanService.cs`：重写为 header-aware 版本。

### 测试变化

- 修复 3 条受影响的旧测试断言：
  - `withSep`：RowCount 3→2（因首行变表头）
  - `partialCol`：RowCount 3→2（因首行变表头）
  - `withTrailingEmpty`：RowCount 3→2（原测试断言错误）
- 新增 6 条专项测试：
  a) 用户表头+分隔线+数据场景 ✅
  b) 表头非空但数据全空列保留 ✅
  c) 表头空且数据全空列删除 ✅
  d) 无表头旧逻辑不回退 ✅
  e) pipe 分隔线忽略 ✅
