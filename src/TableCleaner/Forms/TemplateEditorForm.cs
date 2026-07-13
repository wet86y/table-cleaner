using System.Data;
using System.Text;
using TableCleaner.Models;
using TableCleaner.Services;

namespace TableCleaner.Forms;

/// <summary>模板库管理窗体（非模态，类似替换库的矩阵选择 + 保存逻辑）</summary>
public class TemplateEditorForm : Form
{
    // ═══ Type selector ═══
    private readonly Panel _pnlTypeSelect;
    private readonly RadioButton _rbTypeBasic;
    private readonly RadioButton _rbTypeFilter;

    // ═══ Template list ═══
    private readonly ListBox _lstTemplates;
    private readonly Button _btnAddBasic;
    private readonly Button _btnAddFilter;
    private readonly Button _btnDeleteTemplate;
    private readonly TextBox _txtTemplateDesc;

    // ═══ Matrix DataGridView ═══
    private readonly TextBox _txtTemplateName;
    private readonly DataGridView _dgvMatrix;
    private readonly Button _btnAddColumn;
    private readonly Button _btnDeleteColumn;
    private readonly Button _btnImportClipboard;
    private readonly ComboBox _cmbMatchMode;

    // ═══ Bottom buttons ═══
    private readonly Button _btnSave;
    private readonly Button _btnApply;
    private readonly Button _btnClose;

    // ═══ Data ═══
    private readonly List<string>? _allColumns;
    private bool _refreshing;
    private bool _loadingUi;
    private bool _uiReady;
    private List<CleanTemplate> _templates = new();
    private List<CleanTemplateFilter> _filters = new();

    /// <summary>模板列表（供 MainForm 传入/获取）</summary>
    public List<CleanTemplate> Templates
    {
        get => _templates;
        set
        {
            _templates = value ?? new List<CleanTemplate>();
            if (_uiReady)
            {
                RefreshTemplateList();
                if (_templates.Count > 0) _lstTemplates.SelectedIndex = 0;
                else LoadTemplateToUI();
            }
        }
    }

    /// <summary>筛选模板列表（供 MainForm 传入/获取）— 保持兼容，新逻辑直接读 TemplateColumn.MatchValue</summary>
    public List<CleanTemplateFilter> Filters
    {
        get => _filters;
        set => _filters = value ?? new List<CleanTemplateFilter>();
    }

    /// <summary>用户点击"应用选中模板"时触发</summary>
    public event EventHandler? TemplateApplied;

    /// <summary>最后应用的模板 ID</summary>
    public string? AppliedTemplateId { get; private set; }

    public TemplateEditorForm(List<string>? allColumns = null)
    {
        Icon = AppVisuals.WindowIcon;
        _allColumns = allColumns;

        Text = "模板库管理";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1120, 720);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        // ═══ Helpers ═══
        static FlowLayoutPanel ToolbarRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 1, 0, 1)
            };
        }
        static void Pad(Control c, int left = 5, int right = 5)
        {
            c.Margin = new Padding(left, 0, right, 0);
            c.AutoSize = true;
        }
        static Label BoldLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        static Label Sep() => new() { Text = "│", AutoSize = true, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(8, 0, 8, 0) };

        // ═══════════════════════════════════════════════════════════════
        //  Left panel: template list + description
        // ═══════════════════════════════════════════════════════════════
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 4) };

        var lblTemplates = BoldLabel("清洗模板");
        lblTemplates.Dock = DockStyle.Top;
        lblTemplates.Padding = new Padding(0, 2, 0, 2);

        // Button row
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 4)
        };

        _btnAddBasic = new Button { Text = "添加基础模板", AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
        _btnAddBasic.Click += BtnAddBasicTemplate_Click;

        _btnAddFilter = new Button { Text = "添加筛选模板", AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
        _btnAddFilter.Click += BtnAddFilterTemplate_Click;

        _btnDeleteTemplate = new Button { Text = "删除模板", AutoSize = true };
        _btnDeleteTemplate.Click += BtnDeleteTemplate_Click;

        btnRow.Controls.AddRange(new Control[] { _btnAddBasic, _btnAddFilter, _btnDeleteTemplate });

        // Description textbox at bottom
        var lblDesc = new Label
        {
            Text = "描述：",
            Dock = DockStyle.Bottom,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 0)
        };
        _txtTemplateDesc = new TextBox
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Padding = new Padding(0, 2, 0, 2)
        };
        _txtTemplateDesc.TextChanged += (_, _) =>
        {
            if (_loadingUi) return;
            var t = GetSelectedTemplate();
            if (t != null) t.Description = _txtTemplateDesc.Text;
        };

        // Template list
        _lstTemplates = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2)
        };
        _lstTemplates.SelectedIndexChanged += LstTemplates_SelectedIndexChanged;

        leftPanel.Controls.Add(_lstTemplates);
        leftPanel.Controls.Add(_txtTemplateDesc);
        leftPanel.Controls.Add(lblDesc);
        leftPanel.Controls.Add(btnRow);
        leftPanel.Controls.Add(lblTemplates);

        // ═══════════════════════════════════════════════════════════════
        //  Right panel: template editing area
        // ═══════════════════════════════════════════════════════════════
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 4) };

        // -- Row 0: Type selector --
        _pnlTypeSelect = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 8),
            Margin = new Padding(0, 0, 0, 4)
        };
        var lblType = new Label { Text = "模板类型：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 4, 0), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        _rbTypeBasic = new RadioButton { Text = "普通模板", AutoSize = true, Checked = true, Margin = new Padding(0, 1, 24, 0) };
        _rbTypeBasic.CheckedChanged += (_, _) =>
        {
            if (_loadingUi) return;
            if (_rbTypeBasic.Checked)
            {
                var t = GetSelectedTemplate();
                if (t != null) t.Kind = TemplateKind.Basic;
                RebuildMatrixStructure();
            }
        };
        _rbTypeFilter = new RadioButton { Text = "筛选模板", AutoSize = true, Margin = new Padding(0, 1, 0, 0) };
        _rbTypeFilter.CheckedChanged += (_, _) =>
        {
            if (_loadingUi) return;
            if (_rbTypeFilter.Checked)
            {
                var t = GetSelectedTemplate();
                if (t != null) t.Kind = TemplateKind.Filter;
                RebuildMatrixStructure();
            }
        };
        _pnlTypeSelect.Controls.Add(lblType);
        _pnlTypeSelect.Controls.Add(_rbTypeBasic);
        _pnlTypeSelect.Controls.Add(_rbTypeFilter);

        // -- Row 1: Template name --
        var nameRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 4)
        };
        var lblName = new Label { Text = "模板名称：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 4, 0) };
        _txtTemplateName = new TextBox { Width = 260, Margin = new Padding(0, 0, 0, 0) };
        _txtTemplateName.TextChanged += (_, _) =>
        {
            if (_loadingUi) return;
            var t = GetSelectedTemplate();
            if (t != null)
            {
                t.Name = _txtTemplateName.Text;
                RefreshTemplateListPreserveSelection();
            }
        };
        nameRow.Controls.AddRange(new Control[] { lblName, _txtTemplateName });

        // -- Row 2: Matrix toolbar buttons --
        var matrixToolRow = ToolbarRow();
        _btnAddColumn = new Button { Text = "添加列" };
        _btnAddColumn.Click += BtnAddColumn_Click;
        _btnDeleteColumn = new Button { Text = "删除选中列" };
        _btnDeleteColumn.Click += BtnDeleteColumn_Click;
        _btnImportClipboard = new Button { Text = "从剪切板导入矩阵" };
        _btnImportClipboard.Click += BtnImportClipboard_Click;
        Pad(_btnAddColumn); matrixToolRow.Controls.Add(_btnAddColumn);
        Pad(_btnDeleteColumn); matrixToolRow.Controls.Add(_btnDeleteColumn);
        matrixToolRow.Controls.Add(Sep());
        Pad(_btnImportClipboard); matrixToolRow.Controls.Add(_btnImportClipboard);
        matrixToolRow.Controls.Add(Sep());

        var lblMatchMode = new Label { Text = "表头匹配：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 2, 3) };
        _cmbMatchMode = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 0, 2) };
        _cmbMatchMode.Items.Add("模糊匹配");
        _cmbMatchMode.Items.Add("精确匹配");
        _cmbMatchMode.SelectedIndex = 0;
        _cmbMatchMode.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingUi) return;
            var t = GetSelectedTemplate();
            if (t != null)
            {
                t.HeaderMatchMode = (HeaderMatchMode)_cmbMatchMode.SelectedIndex;
            }
        };
        matrixToolRow.Controls.Add(lblMatchMode);
        matrixToolRow.Controls.Add(_cmbMatchMode);

        // -- DataGridView (matrix) fills remaining space --
        _dgvMatrix = new DataGridView
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            RowHeadersVisible = false,
            ColumnHeadersVisible = false,
            ScrollBars = ScrollBars.Both,
            EditMode = DataGridViewEditMode.EditOnEnter
        };

        // Auto-save on cell edit
        _dgvMatrix.CellValueChanged += (_, _) => SaveMatrixToTemplate();

        // Right panel assembly
        rightPanel.Controls.Add(_dgvMatrix);
        rightPanel.Controls.Add(matrixToolRow);
        rightPanel.Controls.Add(nameRow);
        rightPanel.Controls.Add(_pnlTypeSelect);

        // ═══════════════════════════════════════════════════════════════
        //  SplitContainer: Left-Right
        // ═══════════════════════════════════════════════════════════════
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 4
        };
        mainSplit.Panel1.Controls.Add(leftPanel);
        mainSplit.Panel1.Padding = new Padding(0);
        mainSplit.Panel2.Controls.Add(rightPanel);

        // ═══════════════════════════════════════════════════════════════
        //  Bottom action buttons
        // ═══════════════════════════════════════════════════════════════
        var actionRow = ToolbarRow();
        actionRow.Dock = DockStyle.Bottom;
        actionRow.FlowDirection = FlowDirection.RightToLeft;

        _btnClose = new Button { Text = "关闭", AutoSize = true };
        _btnClose.Click += (_, _) => Close();
        Pad(_btnClose, 6, 6);
        actionRow.Controls.Add(_btnClose);

        _btnSave = new Button { Text = "保存当前模板", AutoSize = true };
        _btnSave.Click += BtnSave_Click;
        Pad(_btnSave, 6, 6);
        actionRow.Controls.Add(_btnSave);

        _btnApply = new Button { Text = "应用选中模板", AutoSize = true, BackColor = Color.LightGreen };
        _btnApply.Click += BtnApplyTemplate_Click;
        Pad(_btnApply, 6, 6);
        actionRow.Controls.Add(_btnApply);

        // ═══════════════════════════════════════════════════════════════
        //  Assemble
        // ═══════════════════════════════════════════════════════════════
        Controls.Add(mainSplit);
        Controls.Add(actionRow);

        // Set SplitterDistance after layout has real size (v2.6.2 crash fix)
        Load += (_, _) =>
        {
            ApplySafeSplit(mainSplit, 260, 400, 0.35, vertical: false);
        };

        // Initialize data
        _uiReady = true;
        RefreshTemplateList();
        LoadTemplateToUI();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Matrix structure
    // ═══════════════════════════════════════════════════════════════════
    private int MatrixRowCount => _rbTypeFilter.Checked ? 4 : 3;
    // Row indices:
    //   0 = 输出表头 / 首要匹配表头
    //   1 = 备用匹配表头 1
    //   2 = 备用匹配表头 2
    //   3 = 匹配值 (仅筛选模板)

    private static readonly string[] RowLabelsBasic = { "输出/首要表头", "备用匹配表头 1", "备用匹配表头 2" };
    private static readonly string[] RowLabelsFilter = { "输出/首要表头", "备用匹配表头 1", "备用匹配表头 2", "匹配值" };

    private string[] CurrentRowLabels => _rbTypeFilter.Checked ? RowLabelsFilter : RowLabelsBasic;

    private void RebuildMatrixStructure()
    {
        SaveMatrixToTemplate(); // preserve current edit

        // Rebuild DataGridView
        _refreshing = true;
        try
        {
            _dgvMatrix.Columns.Clear();
            _dgvMatrix.Rows.Clear();

            var t = GetSelectedTemplate();
            if (t == null) return;

            int rowCount = MatrixRowCount;
            string[] labels = CurrentRowLabels;

            // Column 0: row header (label)
            var labelCol = new DataGridViewTextBoxColumn
            {
                Name = "Label",
                HeaderText = "",
                Width = 120,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.LightGray,
                    Font = new Font(SystemFonts.DefaultFont, FontStyle.Regular)
                },
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _dgvMatrix.Columns.Add(labelCol);

            // Create label rows
            for (int i = 0; i < rowCount; i++)
            {
                _dgvMatrix.Rows.Add(labels[i]);
            }

            // Add data columns from template
            foreach (var col in t.TargetHeaders)
            {
                var dataCol = new DataGridViewTextBoxColumn
                {
                    Name = "Data",
                    HeaderText = "",
                    Width = 120,
                    MinimumWidth = 80,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                int colIdx = _dgvMatrix.Columns.Add(dataCol);

                // Fill values
                _dgvMatrix.Rows[0].Cells[colIdx].Value = col.Header;
                _dgvMatrix.Rows[1].Cells[colIdx].Value = string.IsNullOrWhiteSpace(col.BackupSource1) && !string.IsNullOrWhiteSpace(col.SourceHeader) && !string.Equals(col.SourceHeader, col.Header, StringComparison.OrdinalIgnoreCase)
                    ? col.SourceHeader
                    : col.BackupSource1;
                _dgvMatrix.Rows[2].Cells[colIdx].Value = col.BackupSource2;
                if (rowCount > 3)
                {
                    _dgvMatrix.Rows[3].Cells[colIdx].Value = col.MatchValue;
                }
            }

            // Auto-complete for rows 0 and 1 (header/source) using _allColumns
            if (_allColumns is { Count: > 0 })
            {
                _dgvMatrix.EditingControlShowing += (_, e) =>
                {
                    if (e.Control is DataGridViewTextBoxEditingControl tb)
                    {
                        var cell = _dgvMatrix.CurrentCell;
                        if (cell != null && cell.RowIndex is >= 0 and <= 2)
                        {
                            tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                            tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                            var source = new AutoCompleteStringCollection();
                            source.AddRange(_allColumns.ToArray());
                            tb.AutoCompleteCustomSource = source;
                        }
                        else
                        {
                            tb.AutoCompleteMode = AutoCompleteMode.None;
                            tb.AutoCompleteSource = AutoCompleteSource.None;
                        }
                    }
                };
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SaveMatrixToTemplate()
    {
        if (_refreshing) return;
        var t = GetSelectedTemplate();
        if (t == null) return;

        // Ensure matrix label column exists
        if (_dgvMatrix.Columns.Count < 2) return;

        int rowCount = MatrixRowCount;
        if (_dgvMatrix.Rows.Count < rowCount) return;

        t.TargetHeaders.Clear();
        for (int colIdx = 1; colIdx < _dgvMatrix.Columns.Count; colIdx++)
        {
            var header = _dgvMatrix.Rows[0].Cells[colIdx].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(header)) continue;

            var tc = new TemplateColumn
            {
                Header = header,
                SourceHeader = header,
                BackupSource1 = _dgvMatrix.Rows[1].Cells[colIdx].Value?.ToString() ?? "",
                BackupSource2 = _dgvMatrix.Rows[2].Cells[colIdx].Value?.ToString() ?? ""
            };

            if (rowCount > 3)
            {
                tc.MatchValue = _dgvMatrix.Rows[3].Cells[colIdx].Value?.ToString() ?? "";
            }

            t.TargetHeaders.Add(tc);
        }
    }

    private void LoadMatrixFromTemplate(CleanTemplate? t)
    {
        _refreshing = true;
        try
        {
            _dgvMatrix.Columns.Clear();
            _dgvMatrix.Rows.Clear();

            if (t == null) return;

            int rowCount = MatrixRowCount;
            string[] labels = CurrentRowLabels;

            // Column 0: labels
            var labelCol = new DataGridViewTextBoxColumn
            {
                Name = "Label",
                HeaderText = "",
                Width = 120,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.LightGray,
                    Font = new Font(SystemFonts.DefaultFont, FontStyle.Regular)
                },
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _dgvMatrix.Columns.Add(labelCol);

            for (int i = 0; i < rowCount; i++)
            {
                _dgvMatrix.Rows.Add(labels[i]);
            }

            foreach (var col in t.TargetHeaders)
            {
                var dataCol = new DataGridViewTextBoxColumn
                {
                    Name = "Data",
                    HeaderText = "",
                    Width = 120,
                    MinimumWidth = 80,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                int colIdx = _dgvMatrix.Columns.Add(dataCol);

                _dgvMatrix.Rows[0].Cells[colIdx].Value = col.Header;
                _dgvMatrix.Rows[1].Cells[colIdx].Value = string.IsNullOrWhiteSpace(col.BackupSource1) && !string.IsNullOrWhiteSpace(col.SourceHeader) && !string.Equals(col.SourceHeader, col.Header, StringComparison.OrdinalIgnoreCase)
                    ? col.SourceHeader
                    : col.BackupSource1;
                _dgvMatrix.Rows[2].Cells[colIdx].Value = col.BackupSource2;
                if (rowCount > 3)
                {
                    _dgvMatrix.Rows[3].Cells[colIdx].Value = col.MatchValue;
                }
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Template list
    // ═══════════════════════════════════════════════════════════════════
    private void RefreshTemplateList()
    {
        _lstTemplates.Items.Clear();
        foreach (var t in Templates)
        {
            var prefix = t.Kind == TemplateKind.Filter ? "🔍 " : "📋 ";
            _lstTemplates.Items.Add(prefix + t.Name);
        }
    }

    private void RefreshTemplateListPreserveSelection()
    {
        var oldIdx = _lstTemplates.SelectedIndex;
        string? oldId = oldIdx >= 0 && oldIdx < Templates.Count ? Templates[oldIdx].Id : null;

        _loadingUi = true;
        try
        {
            _lstTemplates.BeginUpdate();
            _lstTemplates.Items.Clear();
            foreach (var t in Templates)
            {
                var prefix = t.Kind == TemplateKind.Filter ? "🔍 " : "📋 ";
                _lstTemplates.Items.Add(prefix + t.Name);
            }

            if (oldId != null)
            {
                for (int i = 0; i < Templates.Count; i++)
                {
                    if (Templates[i].Id == oldId)
                    {
                        _lstTemplates.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        finally
        {
            _lstTemplates.EndUpdate();
            _loadingUi = false;
        }
    }

    private CleanTemplate? GetSelectedTemplate()
    {
        if (_lstTemplates.SelectedIndex < 0 || _lstTemplates.SelectedIndex >= Templates.Count)
            return null;
        return Templates[_lstTemplates.SelectedIndex];
    }

    private void LstTemplates_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingUi) return;
        LoadTemplateToUI();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Load / Save UI
    // ═══════════════════════════════════════════════════════════════════
    private void LoadTemplateToUI()
    {
        var t = GetSelectedTemplate();

        _loadingUi = true;
        try
        {
            // Name
            _txtTemplateName.Text = t?.Name ?? "";

            // Description
            _txtTemplateDesc.Text = t?.Description ?? "";

            // Type selector: update radio buttons without triggering CheckedChanged events
            _pnlTypeSelect.Visible = t != null;
            if (t != null)
            {
                bool isFilter = t.Kind == TemplateKind.Filter;
                if (isFilter)
                {
                    if (!_rbTypeFilter.Checked) _rbTypeFilter.Checked = true;
                }
                else
                {
                    if (!_rbTypeBasic.Checked) _rbTypeBasic.Checked = true;
                }
            }

            // Header match mode
            _cmbMatchMode.Visible = t != null;
            if (t != null)
            {
                _cmbMatchMode.SelectedIndex = (int)t.HeaderMatchMode;
            }

            // Load matrix
            LoadMatrixFromTemplate(t);

            // Enable/disable buttons
            bool hasSelection = t != null;
            _btnDeleteTemplate.Enabled = hasSelection;
            _btnApply.Enabled = hasSelection;
            _btnSave.Enabled = hasSelection;
            _btnAddColumn.Enabled = hasSelection;
            _btnDeleteColumn.Enabled = hasSelection;
            _btnImportClipboard.Enabled = hasSelection;
            _dgvMatrix.Enabled = hasSelection;
        }
        finally
        {
            _loadingUi = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Add / Delete Templates
    // ═══════════════════════════════════════════════════════════════════
    private void BtnAddBasicTemplate_Click(object? sender, EventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("请输入基础模板名称：", "添加基础模板", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var template = new CleanTemplate
        {
            Name = name.Trim(),
            Kind = TemplateKind.Basic
        };
        Templates.Add(template);
        RefreshTemplateList();
        _lstTemplates.SelectedIndex = Templates.Count - 1;
    }

    private void BtnAddFilterTemplate_Click(object? sender, EventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("请输入筛选模板名称：", "添加筛选模板", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var template = new CleanTemplate
        {
            Name = name.Trim(),
            Kind = TemplateKind.Filter
        };
        Templates.Add(template);

        // Keep CleanTemplateFilter for backward compat
        var filter = new CleanTemplateFilter
        {
            Name = name.Trim(),
            AppliedTemplateId = template.Id
        };
        Filters.Add(filter);

        RefreshTemplateList();
        _lstTemplates.SelectedIndex = Templates.Count - 1;
    }

    private void BtnDeleteTemplate_Click(object? sender, EventArgs e)
    {
        var idx = _lstTemplates.SelectedIndex;
        if (idx < 0 || idx >= Templates.Count) return;

        var t = Templates[idx];
        if (MessageBox.Show($"确定要删除模板「{t.Name}」吗？",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        Filters.RemoveAll(f => f.AppliedTemplateId == t.Id);
        Templates.RemoveAt(idx);

        RefreshTemplateList();

        if (Templates.Count > 0)
        {
            _lstTemplates.SelectedIndex = Math.Min(idx, Templates.Count - 1);
        }
        else
        {
            _lstTemplates.SelectedIndex = -1;
            LoadTemplateToUI();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Column management
    // ═══════════════════════════════════════════════════════════════════
    private void BtnAddColumn_Click(object? sender, EventArgs e)
    {
        SaveMatrixToTemplate();
        var t = GetSelectedTemplate();
        if (t == null) return;

        // Add an empty column
        var dataCol = new DataGridViewTextBoxColumn
        {
            Name = "Data",
            HeaderText = "",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        int colIdx = _dgvMatrix.Columns.Add(dataCol);

        // Fill as empty
        for (int r = 0; r < _dgvMatrix.Rows.Count; r++)
        {
            _dgvMatrix.Rows[r].Cells[colIdx].Value = "";
        }
    }

    private void BtnDeleteColumn_Click(object? sender, EventArgs e)
    {
        var t = GetSelectedTemplate();
        if (t == null) return;

        var cell = _dgvMatrix.CurrentCell;
        if (cell == null || cell.ColumnIndex < 1)
        {
            MessageBox.Show("请选中要删除的列中的任意单元格。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int colIdx = cell.ColumnIndex;
        SaveMatrixToTemplate();
        _dgvMatrix.Columns.RemoveAt(colIdx);
        // Also update template data
        SaveMatrixToTemplate();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Clipboard import
    // ═══════════════════════════════════════════════════════════════════
    private void BtnImportClipboard_Click(object? sender, EventArgs e)
    {
        var t = GetSelectedTemplate();
        if (t == null) return;

        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show("剪切板中没有文本数据。请先复制表格数据到剪切板。\n" +
                    "每列数据按行排列：第1行=输出/首要表头，第2行=备用1，第3行=备用2，第4行(筛选)=匹配值。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var data = ClipboardImportService.Import();
            if (data == null || data.RowCount == 0)
            {
                MessageBox.Show("无法解析剪切板数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int rowCount = MatrixRowCount;
            int addedCols = 0;

            // Parse: clipboard rows = matrix rows (output/primary header, backups, match value)
            // clipboard columns = template output column groups
            for (int ci = 0; ci < data.ColumnCount; ci++)
            {
                var header = data.Rows.Count > 0 && ci < data.Rows[0].Count ? data.Rows[0][ci] ?? "" : "";
                var backup1 = data.Rows.Count > 1 && ci < data.Rows[1].Count ? data.Rows[1][ci] ?? "" : "";
                var backup2 = data.Rows.Count > 2 && ci < data.Rows[2].Count ? data.Rows[2][ci] ?? "" : "";
                var matchVal = data.Rows.Count > 3 && ci < data.Rows[3].Count ? data.Rows[3][ci] ?? "" : "";

                if (string.IsNullOrWhiteSpace(header)) continue;

                var tc = new TemplateColumn
                {
                    Header = header,
                    SourceHeader = header,
                    BackupSource1 = backup1,
                    BackupSource2 = backup2,
                    MatchValue = rowCount > 3 ? matchVal : ""
                };
                t.TargetHeaders.Add(tc);
                addedCols++;
            }

            LoadMatrixFromTemplate(t);
            MessageBox.Show($"已从剪切板导入 {addedCols} 列到当前模板。", "导入成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Save button
    // ═══════════════════════════════════════════════════════════════════
    private void BtnSave_Click(object? sender, EventArgs e)
    {
        SaveMatrixToTemplate();
        var t = GetSelectedTemplate();
        if (t == null)
        {
            MessageBox.Show("请先选择或创建一个模板。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Update filter name if filter template
        if (t.Kind == TemplateKind.Filter)
        {
            var f = Filters.FirstOrDefault(ft => ft.AppliedTemplateId == t.Id);
            if (f != null) f.Name = t.Name;
        }

        ConfigService.SaveTemplates(Templates);
        ConfigService.SaveFilters(Filters);

        int cols = t.TargetHeaders.Count;
        MessageBox.Show($"模板「{t.Name}」已保存（{cols} 列）。", "保存成功",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Apply template
    // ═══════════════════════════════════════════════════════════════════
    private static void ApplySafeSplit(SplitContainer split, int desiredPanel1Min, int desiredPanel2Min, double ratio, bool vertical)
    {
        int length = vertical ? split.Height : split.Width;
        if (length <= split.SplitterWidth + 50) return;

        int available = Math.Max(50, length - split.SplitterWidth);
        int min1 = Math.Min(desiredPanel1Min, Math.Max(25, available / 3));
        int min2 = Math.Min(desiredPanel2Min, Math.Max(25, available - min1 - 25));
        int maxDistance = Math.Max(min1, length - split.SplitterWidth - min2);
        int target = Math.Max(min1, Math.Min((int)(length * ratio), maxDistance));

        split.SplitterDistance = target;
        split.Panel1MinSize = min1;
        split.Panel2MinSize = min2;
        split.SplitterDistance = Math.Max(split.Panel1MinSize,
            Math.Min(target, length - split.SplitterWidth - split.Panel2MinSize));
    }

    private void BtnApplyTemplate_Click(object? sender, EventArgs e)
    {
        SaveMatrixToTemplate();

        var t = GetSelectedTemplate();
        if (t == null)
        {
            MessageBox.Show("请先选择一个清洗模板。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (t.TargetHeaders.Count == 0)
        {
            MessageBox.Show("模板没有定义目标表头，请至少添加一列。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Save on apply too
        ConfigService.SaveTemplates(Templates);
        ConfigService.SaveFilters(Filters);

        AppliedTemplateId = t.Id;
        TemplateApplied?.Invoke(this, EventArgs.Empty);
    }
}
