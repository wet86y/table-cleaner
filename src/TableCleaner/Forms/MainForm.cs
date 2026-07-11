using System.Data;
using System.Text;
using TableCleaner.Models;
using TableCleaner.Services;

namespace TableCleaner.Forms;

/// <summary>主窗体：数据预览、清洗操作入口</summary>
public partial class MainForm : Form
{
    // Data
    private TableData? _currentTable;
    private TableData? _processedTable;
    public TableData? DisplayTable => _processedTable ?? _currentTable;

    // State
    private readonly List<CleanProfile> _profiles = new();
    private bool _hasKeepApplied;
    private bool _hasMergeApplied;
    private bool _hasReplaceApplied;
    private ColumnSelectorForm? _columnSelectorForm; // track open non-modal instance
    private ReplacementEditorForm? _replacementEditorForm;
    private TemplateEditorForm? _templateEditorForm; // track open non-modal instance

    // Undo / Reset
    private readonly Stack<TableData?> _operationHistory = new();
    private TableData? _originalData;

    // Controls
    private readonly MenuStrip _menuStrip;
    private readonly ToolStrip _toolStrip;
    private readonly DataGridView _dgvData;
    private readonly ListBox _lbSheetList;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _lblStatus;
    private readonly ToolStripStatusLabel _lblRowCol;
    private readonly ToolStripStatusLabel _lblProfile;

    // Filter
    private readonly Panel _filterPanel;
    private readonly ComboBox _cmbFilterColumn;
    private readonly TextBox _txtFilterKeyword;
    private readonly Button _btnFilterApply;
    private readonly Button _btnFilterClear;
    private readonly BindingSource _filterBindingSource;

    // Edit Mode
    private bool _editMode;
    private readonly ToolStripButton _btnEditMode;
    private readonly ContextMenuStrip _contextMenu;
    private string _cellEditOldValue = "";

    // Selection tracking for merge
    private readonly HashSet<int> _selectedColumns = new(); // DGV column indices (1-based, skip 序号)
    private readonly HashSet<int> _selectedRows = new();    // DGV row indices
    private int _lastSelectedCol = -1;
    private int _lastSelectedRow = -1;
    public MainForm()
    {
        Text = $"表格清洗工具 v{Application.ProductVersion} - 绿色便携版";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        // ---- Menu ----
        _menuStrip = new MenuStrip { Dock = DockStyle.Top };
        var fileMenu = new ToolStripMenuItem("文件(&F)");
        fileMenu.DropDownItems.Add("从剪切板导入(&C)", null, (_, _) => ImportClipboard());
        fileMenu.DropDownItems.Add("从文件导入(&O)...", null, (_, _) => ImportFile());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("导出 CSV(&S)...", null, (_, _) => ExportCsv());
        fileMenu.DropDownItems.Add("导出 XLSX(&X)...", null, (_, _) => ExportXlsx());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("导出配置包(&E)...", null, (_, _) => ExportConfig());
        fileMenu.DropDownItems.Add("导入配置包(&I)...", null, (_, _) => ImportConfig());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("退出(&Q)", null, (_, _) => Close());
        _menuStrip.Items.Add(fileMenu);

        var toolMenu = new ToolStripMenuItem("工具(&T)");
        toolMenu.DropDownItems.Add("列选择与合并规则(&K)...", null, (_, _) => OpenColumnSelector());
        toolMenu.DropDownItems.Add("替换库管理(&R)...", null, (_, _) => OpenReplacementEditor());
        toolMenu.DropDownItems.Add("创建样例数据(&S)...", null, (_, _) => CreateSampleData());
        toolMenu.DropDownItems.Add(new ToolStripSeparator());
        toolMenu.DropDownItems.Add("模板库管理(&T)...", null, (_, _) => OpenTemplateEditor());
        _menuStrip.Items.Add(toolMenu);

        // ---- Toolbar ----
        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System
        };
        AddToolBtn("📋 从剪切板导入", "从 Excel/WPS 复制表格后点击此按钮", (_, _) => ImportClipboard());
        AddToolBtn("📂 打开文件", "打开 CSV 或 Excel 文件", (_, _) => ImportFile());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("列选择", "选择保留列和合并规则", (_, _) => OpenColumnSelector());
        AddToolBtn("替换库", "编辑替换规则库", (_, _) => OpenReplacementEditor());
        AddToolBtn("📁 模板库", "管理清洗模板和筛选模板", (_, _) => OpenTemplateEditor());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("📥 导出 CSV", "导出为 CSV", (_, _) => ExportCsv());
        AddToolBtn("📥 导出 XLSX", "导出为 XLSX", (_, _) => ExportXlsx());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("📤 导出配置", "导出配置包 (zip)", (_, _) => ExportConfig());
        AddToolBtn("📥 导入配置", "导入配置包 (zip)", (_, _) => ImportConfig());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("💾 保存状态", "保存当前处理状态到撤销栈（可作为检查点）", (_, _) => { _operationHistory.Push((_processedTable??_currentTable)?.Clone()); SetStatus($"当前状态已保存为检查点，撤销栈共 {_operationHistory.Count} 个"); });
        AddToolBtn("↩ 撤销上一步", "撤销最近一次清洗/合并/替换操作", (_, _) => Undo());
        AddToolBtn("🔄 恢复原始数据", "恢复到导入时的原始数据", (_, _) => ResetToOriginal());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("🧹 一键清洗", "将竖线分隔的伪表格文本转换为真实单元格表格", (_, _) => OneClickClean());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("↕ 列合并", "将选中多列中离散数据归纳到数据最多的一列", (_, _) => ExecuteColumnMerge());
        AddToolBtn("↔ 行合并", "将选中多行中离散数据归纳到数据最多的一行", (_, _) => ExecuteRowMerge());
        _toolStrip.Items.Add(new ToolStripSeparator());
        AddToolBtn("🧹 清理空行空列", "删除所有全空行和全空列（忽略表头）", (_, _) => ExecuteRemoveEmptyRowsAndColumns());
        _toolStrip.Items.Add(new ToolStripSeparator());

        // Edit mode toggle
        _btnEditMode = new ToolStripButton("✏ 编辑模式", null, (_, _) => ToggleEditMode())
        {
            ToolTipText = "切换到编辑模式，可直接在表格中修改数据",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            CheckOnClick = true,
            Checked = false
        };
        _toolStrip.Items.Add(_btnEditMode);

        FormClosing += (_, args) =>
        {
            if (_templateEditorForm != null && !_templateEditorForm.IsDisposed)
            {
                ConfigService.SaveTemplates(_templateEditorForm.Templates);
                ConfigService.SaveFilters(_templateEditorForm.Filters);
            }
        };

        // ---- Sheet list (left, hidden by default) ----
        _lbSheetList = new ListBox
        {
            Location = new Point(0, 0), Size = new Size(180, 100),
            Visible = false, Font = new Font("Microsoft YaHei", 9F)
        };
        _lbSheetList.SelectedIndexChanged += LbSheetList_SelectedIndexChanged;

        // ---- Filter panel (DPI-safe: FlowLayoutPanel instead of absolute positioning) ----
        _filterBindingSource = new BindingSource();

        _filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 5, 6, 5)
        };

        var lblFilterCol = new Label { Text = "筛选列：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) };
        _cmbFilterColumn = new ComboBox
        {
            Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0)
        };
        _cmbFilterColumn.Items.Insert(0, "全部列");
        _cmbFilterColumn.SelectedIndex = 0;

        _txtFilterKeyword = new TextBox
        {
            Width = 200, PlaceholderText = "输入关键词筛选...", Margin = new Padding(0, 0, 4, 0)
        };
        _txtFilterKeyword.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyFilter(); e.SuppressKeyPress = true; } };

        _btnFilterApply = new Button { Text = "筛选", AutoSize = true, Margin = new Padding(0, 0, 3, 0) };
        _btnFilterApply.Click += (_, _) => ApplyFilter();

        _btnFilterClear = new Button { Text = "清除", AutoSize = true };
        _btnFilterClear.Click += (_, _) => ClearFilter();

        _filterPanel.Controls.AddRange(new Control[] { lblFilterCol, _cmbFilterColumn, _txtFilterKeyword, _btnFilterApply, _btnFilterClear });

        // ---- DataGrid ----
        _dgvData = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            ScrollBars = ScrollBars.Both,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            ColumnHeadersVisible = true,
            RowHeadersVisible = false,
            RowHeadersWidth = 55,
            RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.EnableResizing,
            AllowUserToOrderColumns = false,
            AllowUserToResizeColumns = true,
            AllowUserToResizeRows = true,
            BackgroundColor = Color.White,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei", 9F)
        };

        // Column header settings
        _dgvData.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        _dgvData.ColumnHeadersHeight = 32;
        _dgvData.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        // ---- Context Menu (moved after DataGridView initialization to avoid NRE) ----
        _contextMenu = new ContextMenuStrip();
        var miCopy = new ToolStripMenuItem("复制选中的单元格", null, (_, _) => CopySelection());
        miCopy.ShortcutKeys = Keys.Control | Keys.C;
        _contextMenu.Items.Add(miCopy);
        _contextMenu.Items.Add(new ToolStripSeparator());

        var miPaste = new ToolStripMenuItem("粘贴（覆盖）", null, (_, _) => PasteClipboard());
        miPaste.ShortcutKeys = Keys.Control | Keys.V;
        _contextMenu.Items.Add(miPaste);

        var miAppendClip = new ToolStripMenuItem("追加剪贴板为新行", null, (_, _) => AppendClipboardAsNewRows());
        miAppendClip.ShortcutKeys = Keys.Control | Keys.Shift | Keys.V;
        _contextMenu.Items.Add(miAppendClip);

        var miAppendBlank = new ToolStripMenuItem("追加空白行", null, (_, _) => AppendBlankRow());
        _contextMenu.Items.Add(miAppendBlank);
        _contextMenu.Items.Add(new ToolStripSeparator());

        var miDeleteRow = new ToolStripMenuItem("删除选中行", null, (_, _) => DeleteSelectedRows());
        _contextMenu.Items.Add(miDeleteRow);

        var miDeleteCol = new ToolStripMenuItem("删除选中列", null, (_, _) => DeleteSelectedColumn());
        _contextMenu.Items.Add(miDeleteCol);

        _contextMenu.Opening += (_, e) =>
        {
            bool editable = _editMode && DisplayTable != null;
            miPaste.Visible = editable;
            miAppendClip.Visible = editable;
            miAppendBlank.Visible = editable;
            miDeleteRow.Visible = editable && (_dgvData.SelectedRows.Count > 0 || _dgvData.SelectedCells.Count > 0);
            miDeleteCol.Visible = editable;
            if (!editable && _dgvData.SelectedCells.Count == 0)
                e.Cancel = true;
        };

        _dgvData.ContextMenuStrip = _contextMenu;
        _dgvData.CellBeginEdit += DgvData_CellBeginEdit;
        _dgvData.CellValueChanged += DgvData_CellValueChanged;
        _dgvData.ColumnHeaderMouseClick += DgvData_ColumnHeaderMouseClick;
        _dgvData.CellMouseClick += DgvData_CellMouseClickForRowSelect;
        _dgvData.ColumnHeaderMouseDoubleClick += DgvData_ColumnHeaderMouseDoubleClick;
        _dgvData.KeyDown += DgvData_KeyDown;

        // Row numbers are displayed by a frozen display-only column

        // ---- Status bar ----
        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblRowCol = new ToolStripStatusLabel("") { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(10, 0, 5, 0) };
        _lblProfile = new ToolStripStatusLabel("") { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(10, 0, 5, 0) };
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblRowCol, _lblProfile });

        // ---- Stable table layout: Menu -> Toolbar -> Filter -> DataGrid -> Status ----
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 180, Padding = new Padding(5) };
        var lblSheets = new Label { Text = "工作表列表", Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleLeft };
        _lbSheetList.Dock = DockStyle.Fill;
        leftPanel.Controls.Add(_lbSheetList);
        leftPanel.Controls.Add(lblSheets);
        leftPanel.Visible = false;

        var contentPanel = new Panel { Dock = DockStyle.Fill };
        contentPanel.Controls.Add(_dgvData);
        contentPanel.Controls.Add(leftPanel);

        _filterPanel.BorderStyle = BorderStyle.FixedSingle;
        _filterPanel.Margin = new Padding(0, 2, 0, 4);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.Controls.Add(_menuStrip, 0, 0);
        mainLayout.Controls.Add(_toolStrip, 0, 1);
        mainLayout.Controls.Add(_filterPanel, 0, 2);
        mainLayout.Controls.Add(contentPanel, 0, 3);
        mainLayout.Controls.Add(_statusStrip, 0, 4);
        Controls.Add(mainLayout);
        MainMenuStrip = _menuStrip;

        // ---- Init ----
        ConfigService.EnsureDirs();
        _profiles.AddRange(ConfigService.LoadProfiles());

        // Resize
        ResizeBegin += (_, _) => RefreshGrid();
    }

    #region Toolbar helpers

    private void AddToolBtn(string text, string tooltip, EventHandler onClick)
    {
        var btn = new ToolStripButton(text, null, onClick) { ToolTipText = tooltip, DisplayStyle = ToolStripItemDisplayStyle.Text };
        _toolStrip.Items.Add(btn);
    }

    #endregion

    #region Import

    private void ImportClipboard()
    {
        try
        {
            // 优先尝试伪表格解析（支持竖线/加号/分号/逗号/多空格等分隔符）
            if (Clipboard.ContainsText())
            {
                var clipText = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(clipText))
                {
                    var pseudoData = PseudoTableCleanService.ParsePseudoTableText(clipText);
                    if (pseudoData != null && pseudoData.ColumnCount > 1)
                    {
                        SetCurrentTable(pseudoData);
                        SetStatus($"已从剪切板导入（伪表格解析）{pseudoData.RowCount} 行 × {pseudoData.ColumnCount} 列");
                        return;
                    }
                }
            }

            // 回退：标准 TSV/CSV 剪切板导入
            var data = ClipboardImportService.Import();
            if (data == null || data.RowCount == 0)
            {
                MessageBox.Show(
                    "剪切板中没有可识别的表格数据。\n\n请先在 Excel / WPS / 网页中选中表格区域并复制 (Ctrl+C)，\n再点击此按钮。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SetCurrentTable(data);
            SetStatus($"已从剪切板导入 {data.RowCount} 行 × {data.ColumnCount} 列");
        }
        catch (Exception ex)
        {
            ShowError($"导入失败：{ex.Message}");
        }
    }

    private void ImportFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "数据文件|*.csv;*.xlsx;*.xls;*.txt|CSV 文件|*.csv|Excel 文件|*.xlsx;*.xls|文本文件|*.txt|所有文件|*.*",
            Title = "选择要导入的数据文件"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        var path = dialog.FileName;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            if (ext == ".csv")
            {
                var data = CsvService.Import(path);
                if (data != null)
                {
                    _filePath = path;
                    SetCurrentTable(data);
                    _lbSheetList.Visible = false;
                    SetStatus($"已从 CSV 导入 {data.RowCount} 行 × {data.ColumnCount} 列");
                }
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                var sheets = ExcelService.ImportAllSheets(path);
                if (sheets.Count == 0)
                {
                    // Show friendly error for encrypted/protected files
                    MessageBox.Show(
                        $"无法读取文件「{Path.GetFileName(path)}」。\n\n" +
                        "可能原因：公司加密软件加密、文件损坏、或格式不受支持。\n" +
                        "建议：在 Excel 中打开文件，复制表格内容后使用「从剪切板导入」。",
                        "文件导入提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _lbSheetList.Items.Clear();
                int idx = 0;
                foreach (var kvp in sheets)
                {
                    _lbSheetList.Items.Add(kvp.Key);
                    if (idx == 0) SetCurrentTable(kvp.Value);
                    idx++;
                }

                _filePath = path;
                _lbSheetList.Visible = sheets.Count > 1;
                UpdateSheetListLayout();
                SetStatus($"已导入 {path}，{sheets.Count} 个工作表，当前显示「{_lbSheetList.Items[0]}」");
            }
            else if (ext == ".txt")
            {
                // 文本文件：优先尝试伪表格解析，回退 CSV
                var text = File.ReadAllText(path, Encoding.UTF8);
                // 去除 BOM
                if (text.Length > 0 && text[0] == '\uFEFF')
                    text = text[1..];

                var pseudoData = PseudoTableCleanService.ParsePseudoTableText(text);
                if (pseudoData != null && pseudoData.ColumnCount > 1)
                {
                    _filePath = path;
                    SetCurrentTable(pseudoData);
                    _lbSheetList.Visible = false;
                    SetStatus($"已从文本文件导入（伪表格解析）{pseudoData.RowCount} 行 × {pseudoData.ColumnCount} 列");
                }
                else
                {
                    // 回退：按 CSV 解析
                    var csvData = CsvService.Import(path);
                    if (csvData != null)
                    {
                        _filePath = path;
                        SetCurrentTable(csvData);
                        _lbSheetList.Visible = false;
                        SetStatus($"已从文本文件导入（CSV解析）{csvData.RowCount} 行 × {csvData.ColumnCount} 列");
                    }
                    else
                    {
                        MessageBox.Show(
                            "无法识别此文本文件的表格格式。\n\n" +
                            "支持的格式：竖线|、TAB、加号+、分号;、逗号、多空格分隔的伪表格。",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            else
            {
                MessageBox.Show("不支持的文件格式。请使用 CSV、Excel 或 TXT 文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"读取文件失败：{ex.Message}\n\n" +
                "可能是公司加密文件。建议在 Excel 中打开后复制内容，使用「从剪切板导入」。",
                "文件导入提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LbSheetList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lbSheetList.SelectedIndex < 0) return;
        var sheetName = _lbSheetList.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(sheetName) || _filePath == null) return;

        // Re-import from file to get the selected sheet
        try
        {
            var sheets = ExcelService.ImportAllSheets(_filePath);
            if (sheets.TryGetValue(sheetName, out var data))
            {
                SetCurrentTable(data);
                SetStatus($"切换到工作表「{sheetName}」");
            }
        }
        catch { /* ignore */ }
    }

    private string? _filePath;

    #endregion

    #region Processing

    private void OpenColumnSelector()
    {
        if (_currentTable == null)
        {
            MessageBox.Show("请先导入数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // If already open, just bring to front
        if (_columnSelectorForm != null && !_columnSelectorForm.IsDisposed)
        {
            _columnSelectorForm.Activate();
            return;
        }

        var currentProfile = _profiles.Count > 0 ? _profiles[0] : null;
        var effectiveHeaders = _processedTable?.Headers ?? _currentTable.Headers;
        var form = new ColumnSelectorForm(
            effectiveHeaders, _profiles, currentProfile);

        form.KeepApplied += OnColumnSelectorKeepApplied;
        form.MergeApplied += OnColumnSelectorMergeApplied;
        form.FormClosed += (_, _) => _columnSelectorForm = null;

        _columnSelectorForm = form;
        form.Show(this);
    }

    private void OnColumnSelectorKeepApplied(object? sender, EventArgs e)
    {
        if (sender is not ColumnSelectorForm form) return;
        if (_currentTable == null || form.KeptColumns.Count == 0) return;

        _operationHistory.Push((_processedTable ?? _currentTable)?.Clone());
        var kept = form.KeptColumns;
        _processedTable = CleaningService.KeepColumns(_currentTable, kept);
        _hasKeepApplied = true;
        _hasMergeApplied = false;
        _hasReplaceApplied = false;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"列清洗完成：保留 {kept.Count} 列");
    }

    private void OnColumnSelectorMergeApplied(object? sender, EventArgs e)
    {
        if (sender is not ColumnSelectorForm form) return;
        if (_currentTable == null || form.GroupColumns.Count == 0) return;

        _operationHistory.Push((_processedTable ?? _currentTable)?.Clone());
        var source = _processedTable ?? _currentTable;
        var beforeRowCount = source.RowCount;
        _processedTable = MergeService.Merge(source, form.GroupColumns, form.SumColumns);
        _hasMergeApplied = true;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"合并完成：{_processedTable.RowCount} 行（合并前 {beforeRowCount} 行）");
    }

    private void OpenReplacementEditor()
    {
        if (_currentTable == null)
        {
            MessageBox.Show("请先导入数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_replacementEditorForm == null || _replacementEditorForm.IsDisposed)
        {
            var effectiveHeaders = _processedTable?.Headers ?? _currentTable.Headers;
            _replacementEditorForm = new ReplacementEditorForm(effectiveHeaders);
            _replacementEditorForm.ReplacementsApplied += ReplacementEditorForm_ReplacementsApplied;
            _replacementEditorForm.FormClosed += (_, _) => _replacementEditorForm = null;
            _replacementEditorForm.Show(this);
        }
        else
        {
            _replacementEditorForm.Activate();
        }
    }

    private void OpenTemplateEditor()
    {
        if (_currentTable == null)
        {
            MessageBox.Show("请先导入数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_templateEditorForm == null || _templateEditorForm.IsDisposed)
        {
            var effectiveHeaders = _processedTable?.Headers ?? _currentTable.Headers;
            _templateEditorForm = new TemplateEditorForm(effectiveHeaders);
            _templateEditorForm.TemplateApplied += TemplateEditorForm_TemplateApplied;
            _templateEditorForm.FormClosed += (_, _) => _templateEditorForm = null;
            _templateEditorForm.Templates = ConfigService.LoadTemplates();
            _templateEditorForm.Filters = ConfigService.LoadFilters();
            _templateEditorForm.Show(this);
        }
        else
        {
            _templateEditorForm.Activate();
        }
    }

    private void TemplateEditorForm_TemplateApplied(object? sender, EventArgs e)
    {
        if (sender is not TemplateEditorForm form) return;
        if (form.AppliedTemplateId == null) return;

        // Save templates/filters on apply
        ConfigService.SaveTemplates(form.Templates);
        ConfigService.SaveFilters(form.Filters);

        // Find the template
        var template = form.Templates.FirstOrDefault(t => t.Id == form.AppliedTemplateId);
        if (template == null) return;
        if (_currentTable == null) return;

        // Apply template via TemplateEngine.
        // 模板库必须与替换库解耦：应用模板时不加载、不传入替换分组。
        var sourceTable = _processedTable ?? _currentTable;
        if (sourceTable == null) return;

        var multiColumnWarnings = TemplateEngine.GetMultiSourceColumnWarnings(sourceTable, template);
        if (multiColumnWarnings.Count > 0)
        {
            var warningLines = multiColumnWarnings
                .Take(10)
                .Select(w => $"表头「{w.Header}」将会获取 {w.Count} 列数据");
            var more = multiColumnWarnings.Count > 10 ? $"\n另有 {multiColumnWarnings.Count - 10} 项未显示。" : "";
            var message = "当前数据存在重复表头，应用模板时会把同名表头的多列零散数据归并到模板列中：\n\n" +
                          string.Join("\n", warningLines) +
                          more +
                          "\n\n是否继续应用模板？";
            if (MessageBox.Show(message, "重复表头提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }

        var beforeApply = sourceTable.Clone();

        var result = template.Kind == TemplateKind.Filter
            ? TemplateEngine.ApplyFilterTemplate(sourceTable, template, form.Filters.FirstOrDefault(f => f.AppliedTemplateId == template.Id), null, null)
            : TemplateEngine.ApplyTemplate(sourceTable, template, null, null);
        if (result == null) return;

        // 模板应用也是一次真实处理操作：无论此前是否已有 _processedTable，都必须支持撤销。
        _operationHistory.Push(beforeApply);
        _processedTable = result;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"模板应用完成：{template.Name}");
    }

    private void ReplacementEditorForm_ReplacementsApplied(object? sender, EventArgs e)
    {
        if (sender is not ReplacementEditorForm form) return;

        var groups = ConfigService.LoadReplacementGroups();
        var groupName = form.AppliedGroupName;
        var group = groups.FirstOrDefault(g => g.Name == (groupName ?? "默认分组"));

        if (group == null)
        {
            SetStatus("未找到选中的替换分组");
            return;
        }

        if (group.Rules.Count == 0)
        {
            SetStatus("替换规则为空，未执行替换");
            return;
        }

        var source = _processedTable ?? _currentTable;
        if (source == null)
        {
            SetStatus("请先导入数据后再执行替换");
            return;
        }

        _operationHistory.Push(source.Clone());
        _processedTable = ReplacementService.ApplyGroup(source, group);
        _hasReplaceApplied = true;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"替换完成：已应用 {group.Rules.Count} 条规则（分组：{groupName ?? "默认"}）");
    }

    /// <summary>
    /// 一键清洗：将当前数据（单列含竖线或直接读剪贴板伪表格文本）转换为单元格表格。
    /// </summary>
    private void OneClickClean()
    {
        // Strategy 1: If current table has only one column with pipe-delimited content, parse it
        if (_currentTable != null && _processedTable == null)
        {
            var cleaned = PseudoTableCleanService.TryCleanOneColumnTable(_currentTable);
            if (cleaned != null)
            {
                _operationHistory.Push(_currentTable.Clone());
                _processedTable = cleaned;
                _hasKeepApplied = false;
                _hasMergeApplied = false;
                _hasReplaceApplied = false;
                ClearFilter();
                RefreshGrid();
                UpdateProfileLabel();
                SetStatus($"一键清洗完成：{cleaned.RowCount} 行 × {cleaned.ColumnCount} 列");
                return;
            }
        }

        // Strategy 2: Try reading clipboard text directly (any delimiter)
        if (Clipboard.ContainsText())
        {
            var clipText = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(clipText))
            {
                var cleaned = PseudoTableCleanService.ParsePseudoTableText(clipText);
                if (cleaned != null)
                {
                    // Save current state for undo
                    if (_currentTable != null)
                        _operationHistory.Push(_currentTable.Clone());

                    _currentTable = cleaned;
                    _processedTable = null;
                    _originalData = cleaned.Clone();
                    _operationHistory.Clear();
                    _hasKeepApplied = false;
                    _hasMergeApplied = false;
                    _hasReplaceApplied = false;
                    ClearFilter();
                    RefreshGrid();
                    UpdateProfileLabel();
                    SetStatus($"一键清洗完成（从剪贴板）：{cleaned.RowCount} 行 × {cleaned.ColumnCount} 列");
                    return;
                }
            }
        }

        // Strategy 3: If _processedTable exists, try cleaning it
        if (_processedTable != null && _currentTable != null)
        {
            var cleaned = PseudoTableCleanService.TryCleanOneColumnTable(_processedTable);
            if (cleaned != null)
            {
                _operationHistory.Push(_processedTable.Clone());
                _processedTable = cleaned;
                ClearFilter();
                RefreshGrid();
                UpdateProfileLabel();
                SetStatus($"一键清洗完成：{cleaned.RowCount} 行 × {cleaned.ColumnCount} 列");
                return;
            }
        }

        MessageBox.Show(
            "没有检测到可清洗的内容。\n\n" +
            "一键清洗用于将竖线 | 分隔的伪表格文本转换为真实单元格表格。\n" +
            "使用方式：\n" +
            "1) 先在剪贴板中复制伪表格文本（Ctrl+C），再点击此按钮；\n" +
            "2) 或先将伪表格文本通过「从剪切板导入」导入为单列数据，再点击此按钮。\n\n" +
            "示例伪表格格式：\n" +
            "| Q18408110 | 六角法兰面螺栓 | 0.000 | 27.000 |\n" +
            "| VG1500040023 | 气缸盖主螺栓 | 0.000 | 24.000 |",
            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CreateSampleData()
    {
        var data = new TableData();
        data.Headers.AddRange(new[] { "客户", "金额", "日期", "备注", "区域" });

        var samples = new[]
        {
            new[] { "A公司", "1500", "2025-01-15", "第一笔", "华东" },
            new[] { "A公司", "2000", "2025-02-20", "第二笔", "华东" },
            new[] { "B公司", "3500", "2025-01-10", "常规", "华南" },
            new[] { "B公司", "4200", "2025-03-05", "加急", "华南" },
            new[] { "C公司", "1000", "2025-02-28", "测试订单", "华北" },
            new[] { "A公司", "1800", "2025-03-15", "第三笔", "华东" },
            new[] { "B公司", "2100", "2025-04-01", "VIP客户", "华南" },
        };

        foreach (var s in samples)
            data.Rows.Add(new List<string>(s));

        SetCurrentTable(data);
        SetStatus($"已创建样例数据：{data.RowCount} 行 × {data.ColumnCount} 列");
    }

    #endregion

    #region Export

    private void ExportCsv()
    {
        var source = DisplayTable;
        if (source == null) { NoDataTip(); return; }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"表格导出_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (CsvService.Export(source, dialog.FileName))
        {
            SetStatus($"已导出 CSV：{dialog.FileName}");
            MessageBox.Show($"CSV 导出成功！\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            ShowError("CSV 导出失败，请重试。");
        }
    }

    private void ExportXlsx()
    {
        var source = DisplayTable;
        if (source == null) { NoDataTip(); return; }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 文件|*.xlsx",
            FileName = $"表格导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (ExcelService.ExportToXlsx(source, dialog.FileName))
        {
            SetStatus($"已导出 XLSX：{dialog.FileName}");
            MessageBox.Show($"XLSX 导出成功！\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            ShowError("XLSX 导出失败，请重试。");
        }
    }

    private void ExportConfig()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "配置包|*.zip",
            FileName = $"表格清洗配置_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (ConfigService.ExportPackage(dialog.FileName))
        {
            SetStatus($"配置包已导出：{dialog.FileName}");
            MessageBox.Show($"配置包导出成功！\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            ShowError("导出配置包失败。");
        }
    }

    private void ImportConfig()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "配置包|*.zip",
            Title = "导入配置包"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var pkg = ConfigService.ImportPackage(dialog.FileName);
            if (pkg == null)
            {
                MessageBox.Show("配置包格式不正确，无法导入。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _profiles.Clear();
            _profiles.AddRange(pkg.Profiles);
            SetStatus($"已导入配置包：{pkg.Profiles.Count} 个方案，{pkg.Replacements.Count} 条替换规则");
            MessageBox.Show($"配置包导入成功！\n{pkg.Profiles.Count} 个列方案\n{pkg.Replacements.Count} 条替换规则",
                "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError($"导入配置包失败：{ex.Message}");
        }
    }

    #endregion

    #region Helpers

    private void SetCurrentTable(TableData data)
    {
        _currentTable = data;
        _processedTable = null;
        _originalData = data.Clone();
        _operationHistory.Clear();
        _hasKeepApplied = false;
        _hasMergeApplied = false;
        _hasReplaceApplied = false;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
    }

    private void RefreshGrid()
    {
        var source = DisplayTable;
        if (source == null)
        {
            _dgvData.DataSource = null;
            _filterBindingSource.DataSource = null;
            _lblRowCol.Text = "";
            return;
        }

        // Release old DataSource before creating new DataTable to avoid DuplicateNameException
        _dgvData.DataSource = null;
        _filterBindingSource.DataSource = null;

        var dt = new DataTable();
        dt.Columns.Add("序号");
        var usedHeaders = new HashSet<string>();
        foreach (var h in source.Headers)
        {
            var name = h;
            if (!usedHeaders.Add(name))
            {
                // 防御性去重：旧数据可能携带重复列名
                for (int suffix = 2; ; suffix++)
                {
                    name = $"{h}_{suffix}";
                    if (usedHeaders.Add(name)) break;
                }
            }
            dt.Columns.Add(name);
        }

        for (int rowIndex = 0; rowIndex < source.Rows.Count; rowIndex++)
        {
            var row = source.Rows[rowIndex];
            var r = dt.NewRow();
            r[0] = (rowIndex + 1).ToString();
            for (int i = 0; i < row.Count && i < source.Headers.Count; i++)
                r[i + 1] = row[i] ?? "";
            dt.Rows.Add(r);
        }

        // Use BindingSource for filtering
        _filterBindingSource.DataSource = dt;
        _dgvData.DataSource = _filterBindingSource;
        foreach (DataGridViewColumn col in _dgvData.Columns)
        {
            col.Resizable = DataGridViewTriState.True;
            if (col.Name == "序号" || col.HeaderText == "序号")
            {
                col.Frozen = true;
                col.ReadOnly = true;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.Width = 60;
                col.MinimumWidth = 60;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            else
            {
                col.Width = 140;
                col.MinimumWidth = 80;
                col.ReadOnly = !_editMode;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }
        foreach (DataGridViewRow row in _dgvData.Rows)
        {
            row.Resizable = DataGridViewTriState.True;
        }
        _lblRowCol.Text = $"{source.RowCount} 行 × {source.ColumnCount} 列";

        // Refresh filter column dropdown
        _cmbFilterColumn.Items.Clear();
        _cmbFilterColumn.Items.Add("全部列");
        foreach (var h in source.Headers)
            _cmbFilterColumn.Items.Add(h);
        _cmbFilterColumn.SelectedIndex = 0;
    }

    private void ApplyFilter()
    {
        var keyword = _txtFilterKeyword.Text.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            ClearFilter();
            return;
        }

        var escaped = keyword.Replace("'", "''").Replace("%", "[%]").Replace("[", "[[]");
        var colName = _cmbFilterColumn.SelectedItem?.ToString();

        if (string.IsNullOrEmpty(colName) || colName == "全部列")
        {
            // Filter across all columns
            var dt = _filterBindingSource.DataSource as DataTable;
            if (dt == null) return;
            var filters = new List<string>();
            foreach (DataColumn col in dt.Columns)
            {
                if (col.ColumnName == "序号") continue;
                filters.Add($"[{col.ColumnName}] LIKE '%{escaped}%'");
            }
            _filterBindingSource.Filter = string.Join(" OR ", filters);
        }
        else
        {
            _filterBindingSource.Filter = $"[{colName}] LIKE '%{escaped}%'";
        }
    }

    private void ClearFilter()
    {
        _filterBindingSource.Filter = null;
        _txtFilterKeyword.Clear();
    }

    /// <summary>绘制行号</summary>
    private void DgvData_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (_dgvData.RowHeadersVisible)
        {
            using var brush = new SolidBrush(_dgvData.RowHeadersDefaultCellStyle.ForeColor);
            var rowNumber = (e.RowIndex + 1).ToString();
            e.Graphics.DrawString(rowNumber,
                _dgvData.Font ?? DefaultFont,
                brush,
                e.RowBounds.Location.X + 4,
                e.RowBounds.Location.Y + 4);
        }
    }

    private void SetStatus(string msg)
    {
        _lblStatus.Text = msg;
    }

    private void UpdateProfileLabel()
    {
        string info = "";
        if (_hasKeepApplied) info += " 列清洗";
        if (_hasMergeApplied) info += " 合并";
        if (_hasReplaceApplied) info += " 替换";
        _lblProfile.Text = string.IsNullOrEmpty(info) ? "" : $"已处理:{info}";
    }

    private void NoDataTip()
    {
        MessageBox.Show("请先导入数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowError(string msg)
    {
        MessageBox.Show(msg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void UpdateSheetListLayout()
    {
        var leftPanel = Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Left);
        if (leftPanel != null)
            leftPanel.Visible = _lbSheetList.Visible;
    }

    private void Undo()
    {
        if (_operationHistory.Count == 0)
        {
            SetStatus("没有可撤销的操作。");
            return;
        }
        var prev = _operationHistory.Pop();
        _processedTable = prev;
        // Reset flags based on actual state
        if (_processedTable == null)
        {
            _hasKeepApplied = false;
            _hasMergeApplied = false;
            _hasReplaceApplied = false;
        }
        else
        {
            // When _processedTable exists but undo stack is not empty, keep flags.
            // When undo stack is empty after pop (back to initial processed state),
            // preserve previous flag state since _processedTable is from a saved snapshot.
            if (_operationHistory.Count == 0)
            {
                // Last undo step: back to state before any operations
                _hasKeepApplied = false;
                _hasMergeApplied = false;
                _hasReplaceApplied = false;
            }
        }
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"已撤销上一步，剩余可撤销 {_operationHistory.Count} 步。");
    }

    private void ResetToOriginal()
    {
        if (_originalData == null)
        {
            SetStatus("没有原始数据可恢复。");
            return;
        }
        _operationHistory.Clear();
        _processedTable = null;
        _currentTable = _originalData.Clone();
        _hasKeepApplied = false;
        _hasMergeApplied = false;
        _hasReplaceApplied = false;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus("已恢复到原始数据。");
    }

    #endregion

    #region Edit Mode

    private void ToggleEditMode()
    {
        _editMode = !_editMode;
        _btnEditMode.Checked = _editMode;

        if (_editMode)
        {
            _btnEditMode.Text = "🔒 只读模式";
            _btnEditMode.ToolTipText = "恢复到只读模式，不可直接编辑";
            _dgvData.ReadOnly = false;
            // 序号列始终保持只读
            if (_dgvData.Columns.Count > 0)
                _dgvData.Columns[0].ReadOnly = true;
            // RefreshGrid will set per-column ReadOnly for data columns
            RefreshGrid();
            SetStatus("已切换到编辑模式，双击单元格可编辑");
        }
        else
        {
            _btnEditMode.Text = "✏ 编辑模式";
            _btnEditMode.ToolTipText = "切换到编辑模式，可直接在表格中修改数据";
            _dgvData.ReadOnly = true;
            // Ensure all columns (except 序号) respect ReadOnly
            if (_dgvData.DataSource != null)
            {
                foreach (DataGridViewColumn col in _dgvData.Columns)
                {
                    if (col.Name != "序号" && col.HeaderText != "序号")
                        col.ReadOnly = true;
                }
            }
            SetStatus("已切换到只读模式");
        }
    }

    private void DgvData_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        // Skip 序号列
        if (e.ColumnIndex == 0)
        {
            e.Cancel = true;
            return;
        }
        // Capture old value for undo
        if (_dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex].Value != null)
            _cellEditOldValue = _dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString() ?? "";
        else
            _cellEditOldValue = "";
    }

    private void DgvData_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        // Skip header and 序号列
        if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;

        var source = DisplayTable;
        if (source == null) return;

        // Push undo state BEFORE the edit
        _operationHistory.Push(source.Clone());

        // Get the DataTable
        var dt = _filterBindingSource.DataSource as DataTable;
        if (dt == null) return;

        // Get the current (new) value
        var newValue = _dgvData.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";

        // DGV column index → DataTable column index: colIndex - 1 (skip 序号列)
        int dataColIndex = e.ColumnIndex - 1;

        // Find the actual row index in _processedTable
        // Since the DGV may be sorted/filtered, we need to map back via DataTable
        if (e.RowIndex < dt.Rows.Count && dataColIndex < source.Headers.Count)
        {
            // The DataTable is the data source, so its rows match DGV display order (after sort/filter)
            // We update the DataTable directly - the data is already changed via binding
            // Now sync back to _processedTable by finding the right position

            // Get the row from DataTable (which has 序号 at column 0)
            var dataRow = dt.Rows[e.RowIndex];
            var originalRowKey = dataRow[0]?.ToString() ?? "";

            // Find matching row in _processedTable by 序号
            // But since data may be sorted, 序号 is row number, not content
            // The DataTable column index is e.ColumnIndex, but we already know the value

            // Actually the DataTable is a fresh snapshot copied in RefreshGrid.
            // The value in _processedTable hasn't been updated yet because it's not bound.
            // We need to update _processedTable.Rows accordingly.

            // For simplicity: update by DataTable row content
            // We can match by 序号 value since each row has a unique 序号
            if (int.TryParse(originalRowKey, out int rowNum))
            {
                // rowNum is 1-based from the 序号 column
                int procRowIndex = rowNum - 1;
                if (procRowIndex >= 0 && procRowIndex < source.RowCount && dataColIndex >= 0 && dataColIndex < source.Rows[procRowIndex].Count)
                {
                    source.Rows[procRowIndex][dataColIndex] = newValue;
                }
            }

            // Remove old value push and re-push with correct state
            // Actually we already pushed, and the value is now written.
            // The undo stack now has the state before the edit. That's correct.
        }

        SetStatus($"已更新：第 {e.RowIndex + 1} 行，{source.Headers[dataColIndex]}");
    }

    /// <summary>
    /// 编辑模式下按 Delete/Backspace 清空选中单元格内容。
    /// 跳过序号列（column 0），不删除行/列。
    /// </summary>
    private void ClearSelectedDgvCells()
    {
        if (!_editMode || DisplayTable == null)
            return;

        var source = DisplayTable;
        if (source == null || _dgvData.SelectedCells.Count == 0)
            return;

        // Collect unique (row, col) pairs, skip 序号列 (column 0)
        var cellsToClear = new List<(int row, int col)>();
        foreach (DataGridViewCell cell in _dgvData.SelectedCells)
        {
            if (cell.ColumnIndex <= 0)
                continue; // Skip 序号列
            if (cell.RowIndex < 0 || cell.RowIndex >= source.RowCount)
                continue;
            int dataCol = cell.ColumnIndex - 1;
            if (dataCol < 0 || dataCol >= source.ColumnCount)
                continue;
            cellsToClear.Add((cell.RowIndex, dataCol));
        }

        if (cellsToClear.Count == 0)
            return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        // Clear each cell using shared data-layer helper
        TableData.ClearCells(source, cellsToClear);

        RefreshGrid();
        SetStatus($"已清空 {cellsToClear.Count} 个单元格");
    }

    #endregion

    #region Context Menu Actions

    /// <summary>复制选中单元格/行/区域到系统剪贴板（Tab/新行分隔）</summary>
    private void CopySelection()
    {
        if (_dgvData.SelectedCells.Count == 0) return;

        var sb = new StringBuilder();

        // Check if entire rows are selected (via row header click)
        if (_dgvData.SelectedRows.Count > 0)
        {
            // Entire rows selected — copy each selected row
            var selectedRows = _dgvData.SelectedRows.Cast<DataGridViewRow>()
                .OrderBy(r => r.Index).ToList();

            foreach (var row in selectedRows)
            {
                var values = new List<string>();
                for (int c = 0; c < row.Cells.Count; c++)
                {
                    if (c == 0) continue; // skip 序号列
                    values.Add(row.Cells[c].Value?.ToString() ?? "");
                }
                sb.AppendLine(string.Join("\t", values));
            }
        }
        else
        {
            // Cell(s) selected — copy rectangular selection
            var cells = _dgvData.SelectedCells;

            // Determine the bounds of the selection
            int minRow = cells.Cast<DataGridViewCell>().Min(c => c.RowIndex);
            int maxRow = cells.Cast<DataGridViewCell>().Max(c => c.RowIndex);
            int minCol = cells.Cast<DataGridViewCell>().Min(c => c.ColumnIndex);
            int maxCol = cells.Cast<DataGridViewCell>().Max(c => c.ColumnIndex);

            for (int r = minRow; r <= maxRow; r++)
            {
                var values = new List<string>();
                for (int c = minCol; c <= maxCol; c++)
                {
                    if (c == 0) continue; // skip 序号列 in selection
                    values.Add(_dgvData.Rows[r].Cells[c].Value?.ToString() ?? "");
                }
                sb.AppendLine(string.Join("\t", values));
            }
        }

        var result = sb.ToString().TrimEnd();
        if (!string.IsNullOrEmpty(result))
        {
            Clipboard.SetText(result);
            SetStatus($"已复制 {_dgvData.SelectedCells.Count} 个单元格");
        }
    }

    /// <summary>从剪贴板粘贴数据（覆盖模式）</summary>
    private void PasteClipboard()
    {
        if (!_editMode || DisplayTable == null) return;

        var source = DisplayTable;
        if (source == null) return;

        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        // Determine the start cell — use ActiveCell or first selected cell
        int startRow = _dgvData.CurrentCell?.RowIndex ?? 0;
        int startCol = _dgvData.CurrentCell?.ColumnIndex ?? 1;
        if (startCol < 1) startCol = 1; // jump over 序号列

        for (int i = 0; i < lines.Length; i++)
        {
            var fields = lines[i].Split('\t');
            int rowIdx = startRow + i;
            if (rowIdx >= source.RowCount) break;

            for (int j = 0; j < fields.Length; j++)
            {
                int colIdx = startCol + j;
                // Skip if beyond grid, or if it's the 序号列
                if (colIdx <= 0) continue;
                int dataColIdx = colIdx - 1;
                if (dataColIdx >= source.Headers.Count) break;

                if (rowIdx < source.Rows.Count && dataColIdx < source.Rows[rowIdx].Count)
                    source.Rows[rowIdx][dataColIdx] = fields[j];
            }
        }

        RefreshGrid();
        SetStatus($"已粘贴 {lines.Length} 行");
    }

    /// <summary>从剪贴板追加多行为新行</summary>
    private void AppendClipboardAsNewRows()
    {
        if (!_editMode || DisplayTable == null) return;

        var source = DisplayTable;
        if (source == null) return;

        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            var newRow = new List<string>();
            for (int c = 0; c < source.Headers.Count; c++)
            {
                newRow.Add(c < fields.Length ? fields[c] : "");
            }
            source.Rows.Add(newRow);
        }

        RefreshGrid();
        SetStatus($"已追加 {lines.Length} 行");
    }

    /// <summary>追加空白行</summary>
    private void AppendBlankRow()
    {
        if (!_editMode || DisplayTable == null) return;

        var source = DisplayTable;
        if (source == null) return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        var newRow = new List<string>();
        for (int c = 0; c < source.Headers.Count; c++)
            newRow.Add("");
        source.Rows.Add(newRow);

        RefreshGrid();
        SetStatus("已追加空白行");
    }

    /// <summary>删除选中行</summary>
    private void DeleteSelectedRows()
    {
        if (!_editMode || DisplayTable == null) return;

        var source = DisplayTable;
        if (source == null) return;

        if (_dgvData.SelectedRows.Count == 0)
        {
            // Fall back: if cells are selected, delete the rows containing them
            if (_dgvData.SelectedCells.Count == 0) return;
            var rowsToDelete = _dgvData.SelectedCells.Cast<DataGridViewCell>()
                .Select(c => c.RowIndex)
                .Distinct()
                .OrderByDescending(r => r)
                .ToList();

            if (rowsToDelete.Count == 0) return;

            // Push undo state
            _operationHistory.Push(source.Clone());

            foreach (var rowIdx in rowsToDelete)
            {
                if (rowIdx >= 0 && rowIdx < source.RowCount)
                    source.Rows.RemoveAt(rowIdx);
            }
        }
        else
        {
            // Push undo state
            _operationHistory.Push(source.Clone());

            var rowsToDelete = _dgvData.SelectedRows.Cast<DataGridViewRow>()
                .OrderByDescending(r => r.Index)
                .ToList();

            foreach (var row in rowsToDelete)
            {
                if (row.Index >= 0 && row.Index < source.RowCount)
                    source.Rows.RemoveAt(row.Index);
            }
        }

        RefreshGrid();
        SetStatus($"已删除选中行，剩余 {source.RowCount} 行");
    }

    /// <summary>删除选中列</summary>
    private void DeleteSelectedColumn()
    {
        if (!_editMode || DisplayTable == null) return;

        var source = DisplayTable;
        if (source == null) return;

        // Determine which column to delete
        int colIndex = _dgvData.CurrentCell?.ColumnIndex ?? -1;
        if (colIndex <= 0)
        {
            SetStatus("请先选中要删除的数据列（不能删除序号列）");
            return;
        }

        int dataColIndex = colIndex - 1; // Skip 序号列
        if (dataColIndex < 0 || dataColIndex >= source.Headers.Count) return;

        var colName = source.Headers[dataColIndex];
        var result = MessageBox.Show(
            $"确定要删除列「{colName}」吗？\n\n" +
            "⚠ 此操作将移除该列及其所有数据。\n" +
            "如果该列被其他配置文件引用，配置可能失效。\n" +
            "删除后可撤销。",
            "确认删除列",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        // Remove column from data
        source.Headers.RemoveAt(dataColIndex);
        for (int r = 0; r < source.Rows.Count; r++)
        {
            if (dataColIndex < source.Rows[r].Count)
                source.Rows[r].RemoveAt(dataColIndex);
        }

        RefreshGrid();
        SetStatus($"已删除列「{colName}」");
    }

    #endregion

    #region Header Editing

    /// <summary>双击列头弹出重命名对话框</summary>
    private void DgvData_ColumnHeaderMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (!_editMode || DisplayTable == null)
            return;

        if (e.ColumnIndex <= 0)
            return; // Skip 序号列

        int dataColIndex = e.ColumnIndex - 1;
        var source = DisplayTable;
        if (source == null || dataColIndex < 0 || dataColIndex >= source.Headers.Count)
            return;

        var oldName = source.Headers[dataColIndex];

        using var inputDlg = new Form
        {
            Text = "重命名列",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(420, 150),
            MinimumSize = new Size(360, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var lbl = new Label { Text = "输入新列名：", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        var txt = new TextBox { Text = oldName, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12) };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnOk);
        layout.Controls.Add(lbl, 0, 0);
        layout.Controls.Add(txt, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        inputDlg.Controls.Add(layout);
        inputDlg.AcceptButton = btnOk;
        inputDlg.CancelButton = btnCancel;
        inputDlg.Shown += (_, _) => { txt.Focus(); txt.SelectAll(); };

        if (inputDlg.ShowDialog(this) != DialogResult.OK)
            return;

        var newName = txt.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            SetStatus("列名不能为空，未修改。");
            return;
        }

        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        // Update header
        source.Headers[dataColIndex] = newName;

        // Refresh grid to update header display
        RefreshGrid();
        SetStatus($"列「{oldName}」已重命名为「{newName}」");
    }

    #endregion

    #region Row/Column Selection

    /// <summary>点击列头选择/多选列</summary>
    private void DgvData_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (DisplayTable == null)
            return;

        if (e.ColumnIndex <= 0)
            return; // Skip 序号列

        int dataColIndex = e.ColumnIndex;

        bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

        if (ctrl)
        {
            // Toggle this column
            if (_selectedColumns.Contains(dataColIndex))
                _selectedColumns.Remove(dataColIndex);
            else
                _selectedColumns.Add(dataColIndex);

            // When manually Ctrl-clicking columns, clear row selection
            _selectedRows.Clear();
            _lastSelectedRow = -1;
        }
        else if (shift && _lastSelectedCol >= 0)
        {
            // Select range from last selected to current
            int min = Math.Min(_lastSelectedCol, dataColIndex);
            int max = Math.Max(_lastSelectedCol, dataColIndex);
            for (int i = min; i <= max; i++)
                if (i > 0) _selectedColumns.Add(i);

            _selectedRows.Clear();
            _lastSelectedRow = -1;
        }
        else
        {
            // Single column selection
            _selectedColumns.Clear();
            _selectedColumns.Add(dataColIndex);
            _selectedRows.Clear();
            _lastSelectedRow = -1;
            _lastSelectedCol = dataColIndex;
        }

        UpdateSelectionVisual();
        UpdateSelectionStatus();
    }

    /// <summary>点击序号列单元格或数据单元格选择行</summary>
    private void DgvData_CellMouseClickForRowSelect(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (DisplayTable == null || e.RowIndex < 0)
            return;

        // Only intercept clicks on 序号列 (column 0) for row selection
        if (e.ColumnIndex != 0)
            return;

        bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

        if (ctrl)
        {
            // Toggle this row
            if (_selectedRows.Contains(e.RowIndex))
                _selectedRows.Remove(e.RowIndex);
            else
                _selectedRows.Add(e.RowIndex);

            // When manually Ctrl-clicking rows, clear column selection
            _selectedColumns.Clear();
            _lastSelectedCol = -1;
        }
        else if (shift && _lastSelectedRow >= 0)
        {
            // Select range from last selected to current
            int min = Math.Min(_lastSelectedRow, e.RowIndex);
            int max = Math.Max(_lastSelectedRow, e.RowIndex);
            for (int i = min; i <= max; i++)
                _selectedRows.Add(i);

            _selectedColumns.Clear();
            _lastSelectedCol = -1;
        }
        else
        {
            // Single row selection
            _selectedRows.Clear();
            _selectedRows.Add(e.RowIndex);
            _selectedColumns.Clear();
            _lastSelectedCol = -1;
            _lastSelectedRow = e.RowIndex;
        }

        UpdateSelectionVisual();
        UpdateSelectionStatus();
    }

    /// <summary>键盘快捷键处理</summary>
    private void DgvData_KeyDown(object? sender, KeyEventArgs e)
    {
        // Delete/Backspace to clear selected cells in edit mode
        if ((e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) && _editMode && DisplayTable != null)
        {
            e.SuppressKeyPress = true;
            ClearSelectedDgvCells();
            return;
        }

        // Ctrl+A to select all
        if (e.Control && e.KeyCode == Keys.A && DisplayTable != null)
        {
            e.SuppressKeyPress = true;
            
            // Check if last action was selecting columns or rows
            // Default to selecting all data cells
            _dgvData.SelectAll();
            
            // But we're tracking at row/column level, so let's provide a simple approach:
            if (MessageBox.Show("请选择要全选的内容：\n\n是 = 全选所有行\n否 = 全选所有列", "全选",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _selectedRows.Clear();
                _selectedColumns.Clear();
                for (int i = 0; i < DisplayTable.RowCount; i++)
                    _selectedRows.Add(i);
                UpdateSelectionVisual();
                UpdateSelectionStatus();
            }
            else
            {
                _selectedRows.Clear();
                _selectedColumns.Clear();
                for (int i = 1; i <= DisplayTable.ColumnCount; i++)
                    _selectedColumns.Add(i);
                UpdateSelectionVisual();
                UpdateSelectionStatus();
            }
        }
    }

    /// <summary>更新 DGV 选择可视化</summary>
    private void UpdateSelectionVisual()
    {
        if (DisplayTable == null || _dgvData.Columns.Count == 0)
            return;

        // Reset cell selection mode
        _dgvData.ClearSelection();

        // Highlight selected rows (by marking cells in those rows)
        if (_selectedRows.Count > 0)
        {
            foreach (int ri in _selectedRows)
            {
                if (ri >= 0 && ri < _dgvData.Rows.Count)
                {
                    // Select all cells in this row
                    foreach (DataGridViewCell cell in _dgvData.Rows[ri].Cells)
                    {
                        cell.Selected = true;
                    }
                }
            }
        }
        // Highlight selected columns (by marking cells in those columns)
        else if (_selectedColumns.Count > 0)
        {
            // Select all cells in selected columns
            foreach (DataGridViewColumn col in _dgvData.Columns)
            {
                // DGV column index matches our _selectedColumns (1-based, 序号=col 0)
                if (col.Index > 0 && _selectedColumns.Contains(col.Index))
                {
                    foreach (DataGridViewRow row in _dgvData.Rows)
                    {
                        if (col.Index < row.Cells.Count)
                            row.Cells[col.Index].Selected = true;
                    }
                }
            }
        }
    }

    /// <summary>更新状态栏显示选中信息</summary>
    private void UpdateSelectionStatus()
    {
        if (_selectedRows.Count > 0)
            SetStatus($"已选中 {_selectedRows.Count} 行");
        else if (_selectedColumns.Count > 0)
            SetStatus($"已选中 {_selectedColumns.Count} 列");
    }

    /// <summary>清除行列选择</summary>
    private void ClearSelection()
    {
        _selectedColumns.Clear();
        _selectedRows.Clear();
        _lastSelectedCol = -1;
        _lastSelectedRow = -1;
        if (_dgvData != null)
            _dgvData.ClearSelection();
    }

    #endregion

    #region Column/Row Merge

    /// <summary>执行列合并</summary>
    private void ExecuteColumnMerge()
    {
        var source = DisplayTable;
        if (source == null) { NoDataTip(); return; }

        if (_selectedRows.Count > 0 && _selectedColumns.Count > 0)
        {
            MessageBox.Show("当前同时选中了行和列，请先只选择行或只选择列。\n\n" +
                "列合并：点击列头选择多列后再点击此按钮。\n" +
                "行合并：点击序号列选择多行后再点击此按钮。",
                "选择冲突", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // If no columns selected via column header, try to extract from cell selection
        if (_selectedColumns.Count < 2)
        {
            if (_dgvData.SelectedCells.Count > 0)
            {
                var cellCols = _dgvData.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(c => c.ColumnIndex)
                    .Where(ci => ci > 0) // skip 序号列
                    .Distinct()
                    .OrderBy(ci => ci)
                    .ToList();

                if (cellCols.Count >= 2)
                {
                    var result2 = MessageBox.Show(
                        $"将基于框选的 {cellCols.Count} 列执行列合并，是否继续？",
                        "基于框选区域合并",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (result2 != DialogResult.Yes)
                        return;

                    _selectedColumns.Clear();
                    foreach (var ci in cellCols)
                        _selectedColumns.Add(ci);
                }
                else
                {
                    MessageBox.Show("请先选择至少两列（按住 Ctrl/Shift 点击列头多选，或框选表格区域），再执行列合并。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                MessageBox.Show("请先选择至少两列（按住 Ctrl/Shift 点击列头多选，或框选表格区域），再执行列合并。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        // Get data column indices (DGV col index - 1)
        var dataColIndices = _selectedColumns
            .Select(ci => ci - 1)
            .Where(ci => ci >= 0 && ci < source.ColumnCount)
            .Distinct()
            .OrderBy(ci => ci)
            .ToList();

        if (dataColIndices.Count < 2)
        {
            SetStatus("选中列不足，无法合并。");
            return;
        }

        var colNames = dataColIndices.Select(ci => source.Headers[ci]).ToList();
        var confirmResult = MessageBox.Show(
            $"将以下 {dataColIndices.Count} 列中分散的数据归纳到数据最多的一列：\n{string.Join("、", colNames)}\n\n" +
            "其他列被归纳后将清空，结构保留。\n确定执行列合并？",
            "确认列合并",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmResult != DialogResult.Yes)
            return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        var merged = SelectionMergeService.MergeColumns(source, dataColIndices);
        if (merged == null)
        {
            SetStatus("列合并失败：无法处理。");
            return;
        }

        _processedTable = merged;
        ClearSelection();
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"列合并完成：{merged.RowCount} 行 × {merged.ColumnCount} 列");
    }

    /// <summary>执行行合并</summary>
    private void ExecuteRowMerge()
    {
        var source = DisplayTable;
        if (source == null) { NoDataTip(); return; }

        if (_selectedRows.Count > 0 && _selectedColumns.Count > 0)
        {
            MessageBox.Show("当前同时选中了行和列，请先只选择行或只选择列。\n\n" +
                "行合并：点击序号列选择多行后再点击此按钮。\n" +
                "列合并：点击列头选择多列后再点击此按钮。",
                "选择冲突", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // If no rows selected via row header, try to extract from cell selection
        if (_selectedRows.Count < 2)
        {
            if (_dgvData.SelectedCells.Count > 0)
            {
                var cellRows = _dgvData.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(c => c.RowIndex)
                    .Distinct()
                    .OrderBy(ri => ri)
                    .ToList();

                if (cellRows.Count >= 2)
                {
                    var result2 = MessageBox.Show(
                        $"将基于框选的 {cellRows.Count} 行执行行合并，是否继续？",
                        "基于框选区域合并",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (result2 != DialogResult.Yes)
                        return;

                    _selectedRows.Clear();
                    foreach (var ri in cellRows)
                        _selectedRows.Add(ri);
                }
                else
                {
                    MessageBox.Show("请先选择至少两行（按住 Ctrl/Shift 点击序号列多选，或框选表格区域），再执行行合并。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                MessageBox.Show("请先选择至少两行（按住 Ctrl/Shift 点击序号列多选，或框选表格区域），再执行行合并。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        // Get data row indices (DGV row indices)
        var dataRowIndices = _selectedRows
            .Where(ri => ri >= 0 && ri < source.RowCount)
            .Distinct()
            .OrderBy(ri => ri)
            .ToList();

        if (dataRowIndices.Count < 2)
        {
            SetStatus("选中行不足，无法合并。");
            return;
        }

        var confirmResult = MessageBox.Show(
            $"将以下 {dataRowIndices.Count} 行中分散的数据归纳到数据最多的一行：\n" +
            $"行号：{string.Join("、", dataRowIndices.Select(i => i + 1))}\n\n" +
            "其他行被归纳后将清空，结构保留。\n确定执行行合并？",
            "确认行合并",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmResult != DialogResult.Yes)
            return;

        // Push undo state
        _operationHistory.Push(source.Clone());

        var merged = SelectionMergeService.MergeRows(source, dataRowIndices);
        if (merged == null)
        {
            SetStatus("行合并失败：无法处理。");
            return;
        }

        _processedTable = merged;
        ClearSelection();
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();
        SetStatus($"行合并完成：{merged.RowCount} 行 × {merged.ColumnCount} 列");
    }

    /// <summary>执行清理空行空列（忽略表头）</summary>
    private void ExecuteRemoveEmptyRowsAndColumns()
    {
        var source = _processedTable ?? _currentTable;
        if (source == null) { NoDataTip(); return; }

        if (source.RowCount == 0)
        {
            SetStatus("表格为空，无需清理。");
            return;
        }

        // Push undo state
        _operationHistory.Push(source.Clone());

        var cleaned = SelectionMergeService.RemoveEmptyRowsAndColumns(source);
        if (cleaned == null)
        {
            SetStatus("清理失败。");
            return;
        }

        _processedTable = cleaned;
        ClearFilter();
        RefreshGrid();
        UpdateProfileLabel();

        if (cleaned.RowCount == 0)
        {
            SetStatus($"已清理空行空列，表格仅保留表头（{cleaned.ColumnCount} 列），无数据行。");
        }
        else
        {
            SetStatus($"已清理空行空列：{cleaned.RowCount} 行 × {cleaned.ColumnCount} 列");
        }
    }

    #endregion
}
