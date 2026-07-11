# CHANGELOG

## v1.0.0 - 2026-07-11

### 项目重构：架构向 D:\项目开发\超级中键 看齐

- 源码移入 `src/TableCleaner/`，构建产物统一到 `build/`，发布产物统一到 `artifacts/`。
- 项目名称确定为「表格工具」，AssemblyName 改为 `表格工具`。
- 新增 `Directory.Build.props`：集中管理版本号和输出路径。
- 版本重置为 1.0.0。
- 新增 `scripts/run-dev.ps1`、`scripts/build-release.ps1`、`scripts/run-self-check.ps1`。
- 新增 `docs/BRANCH_PROJECT.md`、`docs/BUILD_OUTPUT_CONTRACT.md`、`docs/DESIGN_NOTES.md`。
- 移除旧构建脚本 `build-windows.bat` / `build-windows.sh`。
- 初始化 git 仓库。

---

## v2.7.2 - 2026-06-11

### 多分隔符伪表格导入与表头识别修复

- **多分隔符自动检测**：伪表格解析从单一竖线 `|` 扩展为支持 `|`、TAB、`+`、`;`、`#`、`,`、多空格（2+ 连续空格）。
- **整列均匀性校验**：通过一致性、行覆盖率、最小列数阈值识别真实分隔符，降低单元格内偶然出现符号导致的误拆风险。
- **表头识别增强**：新增“第一行全非数值文本 + 后续行含数值型单元格”启发式，修复全满数据中第一行表头被识别为普通数据的问题。
- **剪贴板导入修复**：剪贴板导入优先走伪表格解析，不再需要先导成单列后再点击两次清洗。
- **一键清洗修复**：从剪贴板直接清洗时不再只检查 `|`，`+` / `;` / 多空格等分隔符同样可直接识别。
- **TXT 文件导入**：从文件导入支持 `.txt`，优先按伪表格解析，失败时回退 CSV/TAB 解析。
- **BOM 处理**：解析前去除 UTF-8 BOM，避免 BOM 附着在第一个表头单元格。
- **发布**：Windows .NET publish 成功；FileVersion `2.7.2.0`；发布目录 `publish/TableCleaner-v2.7.2/`；同步文件 `publish/TableCleaner.exe`。

## v2.6.9 - 2026-05-20

### 二级窗口 DPI / 字体缩放兼容修复

- **问题**：在不同电脑上二级窗口仍可能错位，典型表现为「列选择与合并规则」三栏标题、按钮和列表位置不齐；替换库顶部二级选项在字体缩放下可能挤压。
- **修复**：统一检查所有 Form/Dialog，减少固定 `Location = new Point(...)` / 固定按钮坐标；将关键二级窗口改为自适应布局。
- **涉及窗口**：
  - `ColumnSelectorForm`：三栏区域改为 `TableLayoutPanel`，每栏内部 `Dock` 填充。
  - `ReplacementEditorForm`：匹配方式、替换类型、写入模式 RadioButton 组改为 `FlowLayoutPanel`。
  - `ScopeColumnDialog`：作用列选择弹窗改为 `TableLayoutPanel + FlowLayoutPanel`。
  - `MainForm` 重命名列弹窗、`ReplacementEditorForm` 命名扩展列弹窗改为自适应布局。
- **验证**：主项目构建 0 error；测试 `488/488` 通过；FileVersion `2.6.9.0`。

## v2.6.8 - 2026-05-20

### 筛选模板分组逻辑、重复表头归并与合并表头导入

- **筛选模板核心逻辑调整**：多个「匹配值」会被视为多个筛选分组；每组先独立筛选，再在组内做表头映射，最终横向拼接。
- **重复输出表头**：模板输出允许重复表头，保持用户命名，不自动追加 `_2` / `_3`。
- **重复源表头归并**：模板列命中多个同名源表头时，应用前提示「该表头将获取 N 列数据」；继续后按行从左到右取第一个非空值，归并到该模板列。
- **合并单元格表头导入**：新增 `HeaderNormalizationService`；Excel 合并表头拆分后的空表头继承合并单元格名称；剪贴板空表头向左继承最近非空表头；重复表头不自动重命名。
- **Excel 导入增强**：`.xlsx/.xlsm` 优先用 ClosedXML 读取，以便识别合并区域；失败时回退 ExcelDataReader。
- **验证**：新增 `- / +` 筛选模板横向拼接、重复源表头归并、合并 XLSX 表头导入等回归；测试 `488/488` 通过；FileVersion `2.6.8.0`。

## v2.6.7 - 2026-05-20

### 模板库与替换库彻底解耦、模板应用撤销修复

- **问题 1**：模板应用仍会加载并传入替换库分组，导致应用模板时自动执行替换规则。
- **修复 1**：`MainForm` 模板应用路径不再加载替换库；`TemplateEngine` 即使旧调用误传 replacement groups，也不再隐式执行替换。
- **问题 2**：直接从原始数据应用模板时，`_processedTable == null` 导致应用前没有进入撤销栈，撤销上一步无效。
- **修复 2**：模板应用前无条件保存当前数据快照，确保可撤销。
- **验证**：新增模板/筛选模板忽略替换分组、精确匹配边界等回归；测试 `475/475` 通过；FileVersion `2.6.7.0`。

## v2.6.6 - 2026-05-20

### 模板库 UI 与构建警告清理

- 模板编辑矩阵改为固定列宽，确保横向滚动条可出现。
- 模板类型区域改为流式布局，避免遮挡下方按钮。
- 修复 3 个代码 warning：空值 `ToString()`、替换 source 可能为 null、无用字段 `_selectionChangedByCode`。
- **验证**：测试 `472/472` 通过；主项目构建 0 个代码 warning；FileVersion `2.6.6.0`。

## v2.6.5 - 2026-05-19

### 热修：模板库保存无效和点击旧模板越界

- **问题 1**：MainForm 在创建 TemplateEditorForm 后才注入已保存模板，但 TemplateEditorForm 没有在 `Templates` 属性赋值后刷新左侧列表，导致下次打开模板库看起来是空的。
- **问题 2**：加载模板/切换普通或筛选模板时，TextChanged / CheckedChanged / SelectedIndexChanged 事件重入，矩阵尚未按新行数重建完成就执行 `SaveMatrixToTemplate()`，可能访问不存在的第 4 行并抛出 `ArgumentOutOfRangeException`。
- **修复**：`Templates` 改为带刷新逻辑的属性；新增 `_loadingUi` 防止加载 UI 时误触发保存/重建；`SaveMatrixToTemplate()` 增加行数保护；保存按钮继续落盘到 `config/templates.json` 和 `config/templateFilters.json`。
- **验证**：Windows 冒烟验证：注入 2 个模板后列表显示正常，普通模板 3 行，筛选模板 4 行，选中筛选模板并保存矩阵不再越界，保存/读取模板数量正确。

## v2.6.4 - 2026-05-19

### 修正：模板库矩阵行语义

- **问题**：v2.6.3 把“输出表头”和“首要匹配表头”拆成了两行，导致普通模板变成 4 行、筛选模板变成 5 行，不符合需求。
- **修复**：第 1 行改为“输出/首要表头”，既作为输出表头，也作为第一优先级源表头匹配；第 2、3 行为备用匹配表头；筛选模板仅额外增加第 4 行“匹配值”。
- **当前矩阵**：普通模板 3 行；筛选模板 4 行。
- **兼容**：旧配置中若 `SourceHeader` 与 `Header` 不同，会作为隐藏备用匹配项保留兼容。

## v2.6.2 - 2026-05-19

### 二次热修：TemplateEditorForm 构造阶段 SplitContainer 仍崩溃

- **根因**：v2.6.1 只延后了 `SplitterDistance` 赋值，但 `Panel1MinSize` / `Panel2MinSize` 仍在 `SplitContainer` 对象初始化器中设置；WinForms 设置 `Panel2MinSize` 时会立即校验当前 `SplitterDistance`，在构造期默认宽度较小时仍会抛出 `InvalidOperationException`。
- **修复**：构造函数中不再设置 `Panel1MinSize` / `Panel2MinSize`；等 `Load` 后 SplitContainer 有真实尺寸时，通过 `ApplySafeSplit()` 先设置安全 `SplitterDistance`，再收紧 min size，并对小窗口/DPI 场景做动态夹取。
- **验证**：新增 Windows 侧构造冒烟：`new TemplateEditorForm(...)` 成功返回，不再在构造函数崩溃。

## v2.6.1 - 2026-05-19

### 热修：TemplateEditorForm 打开即崩溃

- **根因**：构造函数中设置 `SplitContainer.SplitterDistance` 时窗口宽度尚未确定（DPIAware 下 ClientSize 可能随缩放变化），Panel2MinSize 设置后 `ApplyPanel2MinSize` 重新计算 SplitterDistance 超出 [Panel1MinSize, Width - Panel2MinSize] 范围。
- **修复**：`mainSplit.SplitterDistance` 从构造函数移至 `Load` 事件，此时窗体宽高已最终确定；使用 `Math.Clamp` 确保在安全范围内。

### 热修：替换库拓展模式「添加拓展列」按钮无效

- **根因**：`RefreshGrid()` 内部有一行冗余的 `CaptureFromGrid()` 调用。当 `BtnAddExtraColumn_Click` 先执行 `CaptureFromGrid()` + `rule.ExtraValues.Add("")` 再调用 `RefreshGrid()` 时，`RefreshGrid()` 内部的第二次 `CaptureFromGrid()` 会从旧的 DataTable（尚无扩展列）重新读取并覆写 rules，导致刚添加的 ExtraValues 被丢弃。
- **修复**：移除 `RefreshGrid()` 中冗余的 `CaptureFromGrid()`；各调用方已在调用前自行处理数据捕获。同时修复了「添加行」「删除选中」「从剪切板导入替换表」按钮在相同场景下规则被覆盖的问题。

### 版本
- 版本升为 2.6.1
- 构建脚本改为自动从 csproj 读取版本号

## v2.6.0 - 2026-05-19

### 新增 / 改进

- 模板库 UI 全面重做：SplitContainer 弹性布局，移除旧固定像素布局。
- 模型重构：CleanTemplate 新增 Kind（Basic/Filter）和 TargetHeaders（List&lt;TemplateColumn&gt;）。
- 新增 TemplateColumn：Header（输出列名）、SourceHeader（源列名）、Fallback（默认值）。
- 筛选模板使用 MatchItems（等值匹配）替代旧 Rules（多运算符）。
- TemplateEngine 新增 TargetHeaders 匹配引擎。
- 筛选模板现在可直接应用：先按筛选项目等值保留匹配行，再按目标表头输出列，多余列删除。
- 旧字段（KeptColumns/GroupColumns/SumColumns/ReplacementGroupName）已移除。
- 验证：测试项目 `444/444` 通过；Windows .NET publish 成功；FileVersion `2.6.0.0`；SHA256 `7c2f2825ad9ea03089c3761a5c9e423642fb86cce0ad685cadabc61726757183`。

## v2.5.3 - 2026-05-19
### DPI 兼容修复 — 自适应布局

- **根因**：所有窗体控件使用硬编码像素位置（`Location = new Point(x, y)`、`Size = new Size(w, h)`），不同 DPI 下文字缩放比例不同但控件位置/容器尺寸不变，必然错位、裁切。
- **方案**：三个窗体全面替换为自适应布局：
  - `ReplacementEditorForm`：4 行 FlowLayoutPanel（Dock=Top, AutoSize）替代固定 Panel；DataGridView Dock=Fill；底部按钮 Dock=Bottom。
  - `MainForm`：筛选面板改为 FlowLayoutPanel；ToolStrip 启用 `AutoSize + System 渲染` 提升 DPI 表现；`Size` → `ClientSize`。
  - `ColumnSelectorForm`：方案区改为 FlowLayoutPanel（Dock=Top）；三列 CheckedListBox 改为 Anchor 内嵌于 Dock=Fill 面板；底部操作按钮改为 FlowLayoutPanel；`Size` → `ClientSize`。
- **验证**：`414/414` 测试通过；Windows UI 冒烟通过。


### 热修：替换类型与写入模式联动

- **根因**：匹配模式、替换类型、写入模式的 RadioButton 都直接放在同一个 toolbar Panel 下，WinForms 会把同一父容器内的 RadioButton 当成同一组选项，导致点击「覆盖/插值」会取消「拓展替换」。
- **修复**：三组选项分别放入独立 Panel：匹配模式、替换类型、写入模式互不干扰。
- **表单差异**：拓展替换模式下至少显示 1 个扩展列，避免普通/拓展表单看起来完全一样。
- **扩展列维护**：新增「添加扩展列」「删除扩展列」按钮。
- **剪贴板导入**：拓展替换模式下，替换后之后的列会导入为 ExtraValues。
- **验证**：`414/414` 测试通过；Windows 侧 ReplacementEditorForm radio/grid 冒烟通过。
- **版本**：源码版本 `2.5.3`，文件版本 `2.5.3.0`，发布目录 `publish/TableCleaner-v2.5.3/`。

## v2.5.2 - 2026-05-19

### 替换库 UI/架构修订

#### 替换类型改为分组级顶层选项

- 「替换类型」从 DataGridView 行内下拉移除，改为分组级 RadioButton（普通替换 / 拓展替换）。
- 仅选择「拓展替换」时显示写入模式（覆盖 / 插值）RadioButton。
- DataGridView 列结构随类型切换：普通 → 启用/替换前/替换后；拓展 → 启用/替换前/替换后/扩展1/扩展2...
- ReplacementService.ApplyGroup 读取 `group.Type` 而非 `rule.ReplacementType`。

#### 窗口改为非模态

- `MainForm.OpenReplacementEditor` 改用 `Show(this)` 打开，不再阻塞主窗口拖动。
- 新增 `ReplacementsApplied` 事件，主窗口订阅以执行替换。
- 关闭窗口自动清理引用，重复打开复用已存在窗口。

#### RadioButton 宽度修正

- 所有 RadioButton 宽度加大（“包含匹配” 115→、“精确匹配” 95→、“普通替换”/“拓展替换” 105→、“覆盖”/“插值” 72→），防止 DPI 缩放文字截断。

#### 版本

- 源码版本：`2.5.2`，文件版本：`2.5.2.0`
- 发布目录：`publish/TableCleaner-v2.5.2/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.5.1 - 2026-05-19

### 热修：替换库窗口打开崩溃

- **问题**：v2.5.0 点击「替换库」时，`ReplacementEditorForm.RefreshGrid()` 在窗口构造过程中设置 `DataGridViewColumn.Width`，触发 `DataGridViewBand.set_Thickness` 的 `NullReferenceException`，导致替换库无法打开。
- **修复**：移除构造/刷新早期对替换库 DataGridView 列宽的强制设置，保留自动列宽布局，避免 WinForms 句柄尚未初始化时访问内部 band 状态。
- **验证**：测试项目 `414/414` 通过；新增 Windows 侧替换库窗体构造冒烟验证。
- **版本**：源码版本 `2.5.1`，文件版本 `2.5.1.0`，发布目录 `publish/TableCleaner-v2.5.1/`。

## v2.5.0 - 2026-05-19

### 替换库 v2.5 重构

- 新增 `ReplacementExtendMode.cs`，包含 ReplacementType（Normal/Extended）和 ExtendWriteMode（Overwrite/Insert）枚举。
- 新增 `ReplacementRule.ExtraValues` 列表属性，支持多值扩展替换。
- `ReplacementService.Apply()` 重构，支持普通替换、拓展替换（覆盖模式）、拓展替换（插值模式）。
- `ReplacementEditorForm` 新增扩展值列 UI，支持分组编辑保存。
- 新增分组保存/加载机制，支持多组替换规则管理。

## v2.4.2 - 2026-05-19

### 三处小修

#### 修复 1：Excel 合并单元格表头填充

- **问题**：导入含合并单元格的 Excel 时，合并区域第一个单元格之外的其他单元格为空，导致表头行出现空列。
- **修复**：Excel 导入完成后检测相邻空表头并自动填充为同一内容。遍历 Headers，对于第 i 列为空时，回退找到最近一个非空列 j，把 Headers[j] 赋值给 Headers[i]。
- **范围**：仅对表头行处理，不涉及数据行。如果全部表头为空则跳过。

#### 修复 2：行列合并后不自动删除被清空的列/行

- **问题**：列合并/行合并后，被清空的列/行被自动删除。用户要求先保留结构。
- **修复**：
  - `SelectionMergeService.MergeColumns` 中删除自动清理空列的逻辑。
  - `SelectionMergeService.MergeRows` 中删除自动清理空行的逻辑。
  - 合并后只清空非目标列/行的单元格，不动行/列结构。
  - 合并确认对话框文案中删除"清空列会被自动删除"/"清空行会被自动删除"等提示。

#### 新增 3：全局清洗空行空列（忽略表头）

- **需求**：清洗逻辑更正，若无伪表格，则检测并清理整个表的空行、空列并删除（此时忽略表头）。
- **实现**：
  - 新增 `SelectionMergeService.RemoveEmptyRowsAndColumns(TableData)` 方法。
  - **空行**：一行中所有数据列都为空，则删除该行。表头行本身不检查。
  - **空列**：一列中所有数据行都为空，则删除该列。即使表头非空也可删除（表头非空的列也被删）。
  - 工具栏新增按钮"🧹 清理空行空列"。
  - 执行前 PushUndo。
  - 如果执行后表格为空则保留表头行提示用户。

#### 版本

- 源码版本：`2.4.2`
- 文件版本：`2.4.2.0`
- 发布目录：`publish/TableCleaner-v2.4.2/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.4.1 - 2026-05-19

### Bug 修复 & 改进

#### Bug 1：修改表头崩溃 (P0)

- **根因**：`RefreshGrid()` 在重建 DataTable 时未释放旧的 `DataGridView.DataSource`，导致列名冲突（`DuplicateNameException`）。
- **修复**：在创建新 DataTable 前先设置 `_dgvData.DataSource = null;` 和 `_filterBindingSource.DataSource = null;`，释放旧绑定后再重建。
- **效果**：双击列头改名后不再崩溃，已选行/筛选状态正常。

#### Bug 2：点击表头改变排序 (P0)

- **根因**：`RefreshGrid()` 中数据列设置了 `SortMode = Automatic`，点击列头触发自动排序。
- **修复**：所有数据列的 `SortMode` 改为 `NotSortable`，彻底禁止列头点击排序。
- **效果**：点击任何列表头都不再改变行序。

#### 改进 3：行列合并支持手动框选

- **改进**：列合并/行合并按钮现在支持从选中单元格区域（框选）提取列/行范围。
- **逻辑**：优先使用列头选中（`_selectedColumns`）/行选中（`_selectedRows`）；无列头/行选中时，如果存在 `SelectedCells`，提取涉及列/行并提示用户确认。
- **效果**：不点击列头，只框选 3 列 × N 行区域，点列合并按钮也能执行。

#### 版本

- 源码版本：`2.4.1`
- 文件版本：`2.4.1.0`
- 发布目录：`publish/TableCleaner-v2.4.1/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.4.0 - 2026-05-19

### 新增：表头可编辑 + 行列选择 + 离散数据合并

#### 功能 A：表头编辑

- 在编辑模式下，双击列头弹出重命名对话框，可修改列名/表头。
- 修改表头后同步到内部 `TableData.Headers`。
- 修改前 PushUndo，可通过撤销恢复到原表头。
- 防止空表头：用户输入空白时提示并拒绝修改。
- 筛选列下拉、导出 CSV/XLSX、清洗逻辑使用新表头。

#### 功能 B：行列选择

- 点击列头选中整列（Ctrl 多选 / Shift 范围选）。
- 点击序号列单元格选中整行（Ctrl 多选 / Shift 范围选）。
- 选中状态通过高亮显示，后续合并功能可识别。
- Ctrl+A 提示选择全选行或全选列。

#### 功能 C：离散数据合并

- 新增两个独立按钮：「列合并」和「行合并」，位于工具栏。
- **列合并**：选中多列后，自动找出选中列中"非空单元格最多"的目标列（并列时取最左列）。每一行取选中范围内第一个非空值，写入目标列，其他选中列单元格清空。清空的列自动删除。
- **行合并**：选中多行后，自动找出选中行中"非空单元格最多"的目标行（并列时取最上行）。每一列取选中范围内第一个非空值，写入目标行，其他选中行单元格清空。清空的行自动删除。
- **互斥锁定**：一次只能执行列合并或行合并；同时选中行列时提示用户先只选择一种。
- 所有合并前 PushUndo，支持撤销。
- 无有效选择或选择不足时友好提示，不崩溃。

#### 功能 D：编辑模式下 Delete/Backspace 清空单元格

- 编辑模式下选中一个或多个单元格后按 Delete/Backspace，清空选中单元格内容。
- 跳过序号列（column 0），不修改辅助列数据。
- 清空前 PushUndo，支持撤销。
- 非编辑模式下 Delete/Backspace 忽略，不改数据。
- 底层使用 `TableData.ClearCells()` 作为纯数据层可测方法。

#### 新增文件

- `Services/SelectionMergeService.cs`：离散数据合并核心服务（纯逻辑，可单元测试）。

#### 代码变更

- `Forms/MainForm.cs`：添加列头双击重命名、行列选择事件处理、两按钮调用合并服务、Delete/Backspace 清空单元格。
- `Models/TableData.cs`：新增 `ClearCells()` 静态方法，纯数据层清空指定位置单元格。
- `TableCleaner.csproj`：版本升为 `2.4.0` / `2.4.0.0`。

#### 测试

- 新增 18 条 SelectionMergeService 专项测试。
  - 列合并：多列离散值归并到非空最多列。
  - 列合并：trim 值处理。
  - 行合并：多行离散值归并到非空最多行。
  - 并列时选择最左列/最上行。
  - 空选择/单列/单行/无效索引安全失败。
  - 源数据不被修改（clone 验证）。
- 新增 10 条 TableData.ClearCells 专项测试。
  - 单个/多个单元格清空。
  - 无效索引安全忽略。
  - null/空列表安全处理。
  - null data 安全处理。
- 旧测试全部通过（283/283）。
- 总计：322/322 通过。

#### 版本

- 源码版本：`2.4.0`
- 文件版本：`2.4.0.0`
- 发布目录：`publish/TableCleaner-v2.4.0/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.3.1 - 2026-05-19

### 修复：一键清洗伪表格表头识别与空列保护

#### 问题描述

用户反馈（2026-05-19 12:31）：\n- 一键清洗会把表头/空列清洗掉。\n- 例如输入含表头的伪表格时，表头行被当作普通数据行，自动生成的列名（列1、列2...）覆盖了原始表头信息。\n- 表头非空但数据全空的列不应被删除，但之前被错误移除。\n
#### 修复内容

1. **表头识别**：新增 `DetectHeader()` 检测逻辑。
   - 主策略：第一条有效 pipe 行后紧跟分隔线（`---`、`|-----|-----|` 等）→ 识别为表头。
   - 辅策略：第一行内层单元格全部非空且后续至少一行有空单元格 → 识别为表头。
   - 无表头时沿用 v2.3.0 逻辑（自动生成列名）。

2. **分隔线增强**：新增 `IsSeparatorLine()` 检测函数，除纯分隔线（`---` / `===`）外，还支持 pipe 分隔线（`|---|---|` / `|-------|-------|`）。

3. **表头保护列**：有表头时，只删除"表头为空且所有数据也为空"的全空列；表头非空的列即使数据全空也保留。

4. **使用表头作为列名**：有表头时使用表头内容作为 `TableData.Headers`，不再自动生成列名。

#### 代码变更

- `Services/PseudoTableCleanService.cs`：重写为 header-aware 版本，新增 `DetectHeader`、`DetectSeparatorAfterFirstPipe`、`IsSeparatorLine`、`GetMeaningfulCells` 方法。
- v2.3.1 二次修复（2026-05-19）：表头检测后当列数为表头内层单元格宽度。修复 uneven 行场景（如 `|A|B|C|` + `|3|4|5|6|`）列数被最长数据行撑大的问题。

#### 测试

- 修复 3 条受影响的旧测试断言（withSep、partialCol、withTrailingEmpty）。
- 新增 6 条专项测试：
  a) 用户提供的表头 + 分隔线 + 数据场景。
  b) 表头非空但数据全空列被保留。
  c) 表头空且数据全空列被删除。
  d) 无表头旧逻辑不回退。
  e) pipe 分隔线被正确忽略。
- 二次修复（2026-05-19 T12:42）：uneven 行列宽对齐问题，表头内层列数决定最终列宽。
- 最终测试结果：283 通过，0 失败。

#### 版本

- 源码版本：`2.3.1`
- 文件版本：`2.3.1.0`
- 发布目录：`publish/TableCleaner-v2.3.1/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.3.0 - 2026-05-19

### 新增：一键清洗伪表格

- 新增「一键清洗」按钮，位于工具栏导入按钮附近，🧹 图标。
- 将竖线 `|` 分隔的伪表格文本/单列单元格内容解析为多列真实单元格表格。
- 支持两种使用方式：
  1. 先在剪贴板中复制伪表格文本（Ctrl+C），再点击按钮，自动从剪贴板读取并转换。
  2. 先将伪表格通过「从剪切板导入」导入为单列数据，再点击按钮转换。
- 自动忽略纯空行和 `---` / `===` 分隔线。
- 自动去除行首/行尾全空列及中间全空列。
- 不规整行自动补空格填充至最大列数。
- 自动删除所有空行（一行所有单元格均为空/空白）。
- 自动删除所有空列（一列所有单元格均为空/空白）。
  - 仅含空白字符的单元格视为空。
  - 中间有效数据所在列中部分空白单元格不算空列，仅整列全空才删。
  - 空行/空列删除在列对齐/trim 处理之后进行。
- 自动生成列名：`列1、列2、列3...`。
- 转换后自动刷新表格预览和筛选列。
- 支持撤销上一步（Undo）。
- 无可用清洗内容时弹出友好提示，不崩溃。

#### 新增文件

- `Services/PseudoTableCleanService.cs`：核心解析逻辑，含 `ParsePseudoTableText(string)` 和 `TryCleanOneColumnTable(TableData)`。

#### 测试

- 新增 33 条 PseudoTableCleanService 专项测试。
  - 空行删除验证：`| | | | |` 纯分隔符空行被移除、纯空白行被移除。
  - 空列删除验证：仅含空白字符的列被移除、部分空白列保留。
  - 部分有值列不被错误删除。
- 旧测试全部通过（186/186）。
- 总计：224/224 通过。

#### 参考源码

- 参考了用户提供的 `C:\Users\Lenovo\OneDrive\桌面\伪表格清洗工具_v2_源码与打包脚本\table_cleaner_tool_v2.py`。
- 借鉴了竖线分割解析、分隔线忽略、空白列修剪等核心思路。
- 在 C# 环境下重新实现，适配现有 TableData 模型和 WinForms 架构。

#### 版本

- 源码版本：`2.3.0`
- 文件版本：`2.3.0.0`
- 发布目录：`publish/TableCleaner-v2.3.0/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.2.1 - 2026-05-19

### 修订：替换库 UI 体验优化

#### 作用列控件改进

- 替换库中作用列选择从窄 `CheckedListBox`（22px 高的狭窄列表条）改为"选择作用列..."按钮 + 摘要标签交互形式：
  - 主界面显示作用列摘要 `[全表]` 或 `[3列：A、B、C]`（蓝色文字）。
  - 点击"选择作用列..."按钮弹出 `ScopeColumnDialog` 弹窗，内含 `CheckedListBox` 多选、全选/取消全选/确定/取消按钮。
  - 弹窗关闭前不生效，取消不改。
  - 不选任何列仍表示全表。
- `ColumnSelectorForm` 无变更，不在替换库中再引入。

#### 匹配模式文字修复

- RadioButton 文本从 `"包含匹配（模糊）"` 恢复为完整的 `"包含匹配（模糊替换）"`（9 个中文字）。
- RadioButton 宽度从 140px 增加到 170px，确保所有文字完整可见，不再被截断。
- 布局位置不再依赖作用列列表动态宽度，改为固定坐标，防止溢出。

#### 新增文件

- `Forms/ScopeColumnDialog.cs`：作用列多选弹窗，支持全选/取消全选/确定/取消操作。
- 弹窗顶部增加筛选输入框，实时过滤列名；勾选状态在筛选前后不丢失。
- 筛选时隐藏的已选列仍然保留，确定后全部已选列生效。
- 过滤不区分大小写，清空筛选恢复完整列表及勾选状态。

#### 测试

- 旧测试全部通过（186/186）。
- 作用列和匹配模式测试不变（业务逻辑无变更）。

#### 版本

- 源码版本：`2.2.1`
- 文件版本：`2.2.1.0`
- 发布目录：`publish/TableCleaner-v2.2.1/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.2.0 - 2026-05-19

### 修订：替换库专项修订

#### 替换库 UI 重构

- 替换库表格中删除"作用列"列展示，不再展示/编辑每条规则的 Scope 字段。
- 作用列改为分组级 CheckedListBox 多选维护（不选=全表）。
- 添加匹配模式 RadioButton（精确/模糊），保存到 `ReplacementGroup.MatchMode`。
- 每个替换分组记录自己的 `ScopeColumns` 和 `MatchMode`。
- `ColumnSelectorForm` 中不再展示替换库入口/按钮/分组下拉（此前已移除，本次确认）。

#### 模型链路

- 新增 `ReplacementMatchMode` 枚举（Fuzzy/Exact）。
- `ReplacementGroup` 扩展 `ScopeColumns`（`List<string>?`）+ `MatchMode`。
- `ReplacementService.ApplyGroup()` 使用分组级作用列+匹配模式，不使用规则行末尾文本。
- 旧 `ReplacementRule.Scope` 保留字段但不再作为新 UI 主要输入。
- 旧扁平配置兼容：加载时按旧格式包裹为默认分组，默认 Fuzzy+全表。

#### 测试

- 新增 40 条替换库专项测试，覆盖：
  - 精确替换：规则 AB->X 时，AB→X，ABC 不变。
  - 模糊替换：规则 AB->X 时，ABC→XC。
  - 分组级作用列：只勾选列 A 时只改 A，不改 B。
  - 空作用列/全表替换。
  - 分组配置保存/加载：ScopeColumns 和 MatchMode 不丢。
  - 配置包往返：这些字段不丢。
  - 旧扁平配置兼容加载。
- 总测试数：`186/186` 通过。

#### 版本

- 源码版本：`2.2.0`
- 文件版本：`2.2.0.0`
- 发布目录：`publish/TableCleaner-v2.2.0/`
- 同步 exe：`publish/TableCleaner.exe`

## v2.1.0 - 2026-05-18

### 新增

- 新增"编辑模式"开关。
- 默认预览模式保持只读，避免误操作。
- 编辑模式下支持单元格手动编辑。
- 主表右键菜单支持：
  - 复制
  - 粘贴
  - 追加剪贴板为新行
  - 追加空白行
  - 删除行
  - 删除列
- 表格数据修改接入撤销栈。

### 修复

- 修复替换库中选择作用列不起效果的问题。
- 替换作用列从"列选择"界面移出，集中到"替换库管理"界面，避免两层 UI 冲突。
- 修复预览表格横向滚动条消失的问题。
- 调整主表列宽策略，避免多列被强行压缩成线条。

### 发布

- 源码版本：`2.1.0`
- 文件版本：`2.1.0.0`
- 最新 exe：`publish/TableCleaner-v2.1.0/TableCleaner.exe`
- 同步 exe：`publish/TableCleaner.exe`

### 验证

- 测试项目：`/home/lenovo/.openclaw/workspace/table-cleaner-tests`
- 最近一次回归：`146/146` 通过。

## v1.2.0 - 2026-05-18

### 新增 / 改进

- 主界面 UI 调整为更稳定布局：菜单、工具栏、筛选栏、表格、状态栏。
- 新增顶部筛选功能：选择列 + 输入关键字 + 筛选 / 清除。
- 新增左侧序号列，仅作为显示辅助，不参与导出。
- 列选择窗口放大，增加全选 / 取消全选按钮。
- 支持撤销上一步。
- 支持恢复原始数据。

### 修复

- 修复 DataGridView 左侧黑三角/行头显示问题，改为隐藏行头并使用固定序号列。
- 修复替换库空数据场景的崩溃风险。
- 修复替换库 `NullReferenceException` / JIT 调试弹窗问题。

## v1.1.x - 2026-05-18

### 新增 / 改进

- 替换作用列改为多选。
- 不选择作用列时默认全表替换。
- 列清洗窗口改为应用后不自动关闭。
- 替换库支持独立分组。
- 配置包导入/导出包含列配置、替换库分组与规则。
- 旧扁平替换规则兼容到默认分组。

### 验证

- 自动化测试曾达到 `98/98` 通过。
- 发现并记录低风险注意点：CSV 特殊多行字段、包含替换语义、欧式数字格式。

## v1.0.0 - 2026-05-18

第一版 MVP。

### 已实现

- 剪贴板导入 TSV / CSV 风格表格。
- CSV / XLS / XLSX 文件导入。
- 多 Sheet Excel 导入和切换。
- 表格预览。
- 勾选保留列清洗。
- 保存 / 加载清洗方案。
- 分组列合并。
- 求和列自动加总。
- 其他列相同保留一个，不同内容合并连接。
- 替换库管理。
- CSV / XLSX 导出。
- 配置包导入 / 导出。
- 自包含绿色版 exe 发布。

### 交付

- 初始发布 exe：`publish/TableCleaner.exe`
- 目标：Windows x64，绿色运行，不依赖 Office COM，不要求管理员权限。
