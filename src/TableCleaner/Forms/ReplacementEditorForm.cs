using System.Data;
using System.Text;
using TableCleaner.Models;
using TableCleaner.Services;

namespace TableCleaner.Forms;

/// <summary>替换库管理窗体（非模态，支持分组、作用列、匹配模式、替换类型、拓展写入模式）</summary>
public class ReplacementEditorForm : Form
{
    private readonly DataGridView _dgv;
    private readonly Button _btnApply;
    private readonly Button _btnSave;
    private readonly Button _btnAdd;
    private readonly Button _btnDelete;
    private readonly Button _btnImportClipboard;
    private readonly Button _btnAddExtraColumn;
    private readonly Button _btnRemoveExtraColumn;
    private readonly Button _btnRenameExtColumns;
    private readonly Button _btnClose;

    // Group controls
    private readonly ComboBox _cmbGroup;
    private readonly Button _btnAddGroup;
    private readonly Button _btnDeleteGroup;
    private readonly TextBox _txtNewGroupName;

    // Scope columns — button + label
    private readonly Label _lblScopeSummary;
    private readonly Button _btnSelectScopeColumns;
    private List<string>? _savedScopeColumns;

    // Match mode radio buttons
    private readonly RadioButton _rbMatchFuzzy;
    private readonly RadioButton _rbMatchExact;

    // Replacement type radio buttons (group-level, new in v2.5.2)
    private readonly RadioButton _rbTypeNormal;
    private readonly RadioButton _rbTypeExtended;

    // ExtendWriteMode label + radio buttons — only visible when Type=Extended
    private readonly Label _lblWriteMode;
    private readonly Panel _pnlWriteMode;
    private readonly RadioButton _rbWriteOverwrite;
    private readonly RadioButton _rbWriteInsert;

    private List<ReplacementGroup> _groups;
    private readonly List<string>? _allColumns;
    private int _currentGroupIndex = -1;

    /// <summary>用户点击"应用替换"时触发；MainForm 订阅此事件以执行替换。</summary>
    public event EventHandler? ReplacementsApplied;

    public ReplacementEditorForm(List<string>? allColumns = null)
    {
        _allColumns = allColumns;
        _groups = ConfigService.LoadReplacementGroups();

        Text = "替换库管理";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1060, 720);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterParent;

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
        static void Pad(Control c, int left = 5, int right = 5) { c.Margin = new Padding(left, 0, right, 0); c.AutoSize = true; }
        static Label Sep() => new() { Text = "│", AutoSize = true, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(8, 0, 8, 0) };

        // ═══ Row 1: rule buttons ═══
        _btnAdd = new Button { Text = "添加行" }; _btnAdd.Click += (_, _) => { CurrentRules.Add(new ReplacementRule()); RefreshGrid(); };
        _btnDelete = new Button { Text = "删除选中" }; _btnDelete.Click += BtnDelete_Click;
        _btnImportClipboard = new Button { Text = "从剪切板导入替换表" }; _btnImportClipboard.Click += BtnImportClipboard_Click;
        var row1 = ToolbarRow();
        Pad(_btnAdd); row1.Controls.Add(_btnAdd);
        Pad(_btnDelete); row1.Controls.Add(_btnDelete);
        Pad(_btnImportClipboard); row1.Controls.Add(_btnImportClipboard);

        // ═══ Row 2: group controls ═══
        var lblGroup = new Label { Text = "分组：", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        _cmbGroup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        _cmbGroup.SelectedIndexChanged += (_, _) =>
        {
            SaveCurrentEditorStateToGroup(_currentGroupIndex);
            _currentGroupIndex = _cmbGroup.SelectedIndex;
            LoadCurrentGroupToUI();
            RefreshGrid();
        };
        _txtNewGroupName = new TextBox { Width = 115, PlaceholderText = "新分组名" };
        _btnAddGroup = new Button { Text = "新增分组" }; _btnAddGroup.Click += BtnAddGroup_Click;
        _btnDeleteGroup = new Button { Text = "删除分组", BackColor = Color.LightCoral }; _btnDeleteGroup.Click += BtnDeleteGroup_Click;
        var row2 = ToolbarRow();
        Pad(lblGroup, 0, 4); row2.Controls.Add(lblGroup);
        Pad(_cmbGroup, 0); row2.Controls.Add(_cmbGroup);
        Pad(_txtNewGroupName); row2.Controls.Add(_txtNewGroupName);
        Pad(_btnAddGroup); row2.Controls.Add(_btnAddGroup);
        Pad(_btnDeleteGroup); row2.Controls.Add(_btnDeleteGroup);

        // ═══ Row 3: scope + match + type ═══
        var lblScopeLabel = new Label { Text = "作用列：", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        _lblScopeSummary = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.HotTrack, Text = "[全表]" };
        _btnSelectScopeColumns = new Button { Text = "选择作用列...", Enabled = _allColumns != null };
        _btnSelectScopeColumns.Click += BtnSelectScopeColumns_Click;

        var lblMatch = new Label { Text = "匹配：", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        var pnlMatchMode = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        _rbMatchFuzzy = new RadioButton { Text = "包含匹配", AutoSize = true, Margin = new Padding(0, 0, 12, 0), Checked = true };
        _rbMatchFuzzy.CheckedChanged += (_, _) => { if (_rbMatchFuzzy.Checked) UpdateMatchModeFromUI(); };
        _rbMatchExact = new RadioButton { Text = "精确匹配", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
        _rbMatchExact.CheckedChanged += (_, _) => { if (_rbMatchExact.Checked) UpdateMatchModeFromUI(); };
        pnlMatchMode.Controls.Add(_rbMatchFuzzy); pnlMatchMode.Controls.Add(_rbMatchExact);

        var lblType = new Label { Text = "类型：", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true };
        var pnlReplacementType = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        _rbTypeNormal = new RadioButton { Text = "普通替换", AutoSize = true, Margin = new Padding(0, 0, 12, 0), Checked = true };
        _rbTypeNormal.CheckedChanged += (_, _) => { if (_rbTypeNormal.Checked) { UpdateTypeFromUI(); RefreshGrid(); } };
        _rbTypeExtended = new RadioButton { Text = "拓展替换", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
        _rbTypeExtended.CheckedChanged += (_, _) => { if (_rbTypeExtended.Checked) { UpdateTypeFromUI(); RefreshGrid(); } };
        pnlReplacementType.Controls.Add(_rbTypeNormal); pnlReplacementType.Controls.Add(_rbTypeExtended);

        var row3 = ToolbarRow();
        Pad(lblScopeLabel, 0, 4); row3.Controls.Add(lblScopeLabel);
        Pad(_lblScopeSummary, 0); row3.Controls.Add(_lblScopeSummary);
        Pad(_btnSelectScopeColumns); row3.Controls.Add(_btnSelectScopeColumns);
        row3.Controls.Add(Sep());
        Pad(lblMatch, 0, 4); row3.Controls.Add(lblMatch);
        Pad(pnlMatchMode, 0); row3.Controls.Add(pnlMatchMode);
        row3.Controls.Add(Sep());
        Pad(lblType, 0, 4); row3.Controls.Add(lblType);
        Pad(pnlReplacementType, 0); row3.Controls.Add(pnlReplacementType);

        // ═══ Row 4: write mode + extra column buttons ═══
        _lblWriteMode = new Label { Text = "写入模式：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };
        _pnlWriteMode = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        _rbWriteOverwrite = new RadioButton { Text = "覆盖", AutoSize = true, Margin = new Padding(0, 0, 12, 0), Checked = true };
        _rbWriteOverwrite.CheckedChanged += (_, _) => { if (_rbWriteOverwrite.Checked) UpdateWriteModeFromUI(); };
        _rbWriteInsert = new RadioButton { Text = "插值", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
        _rbWriteInsert.CheckedChanged += (_, _) => { if (_rbWriteInsert.Checked) UpdateWriteModeFromUI(); };
        _pnlWriteMode.Controls.Add(_rbWriteOverwrite); _pnlWriteMode.Controls.Add(_rbWriteInsert);
        _btnAddExtraColumn = new Button { Text = "添加扩展列" }; _btnAddExtraColumn.Click += BtnAddExtraColumn_Click;
        _btnRemoveExtraColumn = new Button { Text = "删除扩展列" }; _btnRemoveExtraColumn.Click += BtnRemoveExtraColumn_Click;
        _btnRenameExtColumns = new Button { Text = "命名扩展列..." }; _btnRenameExtColumns.Click += BtnRenameExtColumns_Click;
        var row4 = ToolbarRow();
        Pad(_lblWriteMode, 0, 4); row4.Controls.Add(_lblWriteMode);
        Pad(_pnlWriteMode, 0); row4.Controls.Add(_pnlWriteMode);
        Pad(_btnAddExtraColumn, 10); row4.Controls.Add(_btnAddExtraColumn);
        Pad(_btnRemoveExtraColumn); row4.Controls.Add(_btnRemoveExtraColumn);
        Pad(_btnRenameExtColumns, 10); row4.Controls.Add(_btnRenameExtColumns);

        // ═══ DataGridView — fills remaining space ═══
        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 0, 6, 0),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false
        };

        // ═══ Bottom buttons ═══
        _btnSave = new Button { Text = "保存替换库" };
        _btnSave.Click += (_, _) =>
        {
            CaptureFromGrid();
            SaveGroupUIToCurrentGroup();
            ConfigService.SaveReplacementGroups(_groups);
            var totalRules = _groups.Sum(g => g.Rules.Count);
            MessageBox.Show($"已保存 {totalRules} 条替换规则（{_groups.Count} 个分组）。", "保存成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _btnApply = new Button { Text = "应用替换", BackColor = Color.LightGreen };
        _btnApply.Click += (_, _) =>
        {
            CaptureFromGrid();
            SaveGroupUIToCurrentGroup();
            ConfigService.SaveReplacementGroups(_groups);
            AppliedGroupName = _cmbGroup.SelectedItem?.ToString();
            ReplacementsApplied?.Invoke(this, EventArgs.Empty);
        };
        _btnClose = new Button { Text = "关闭" };
        _btnClose.Click += (_, _) => Close();
        var bottomRow = ToolbarRow();
        bottomRow.Dock = DockStyle.Bottom;
        bottomRow.FlowDirection = FlowDirection.RightToLeft;
        var btnUsage = new Button { Text = "📖 使用说明", AutoSize = true, Padding = new Padding(6, 0, 6, 0) };
        btnUsage.Click += (_, _) => {
            MessageBox.Show("替换库：单击选中行进行编辑，右键可删除行。\n支持纯文本/正则替换、整行/列范围操作。\n分组可管理不同替换方案。", "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Pad(btnUsage, 6, 6); bottomRow.Controls.Add(btnUsage);
        Pad(_btnClose, 0, 6); bottomRow.Controls.Add(_btnClose);
        Pad(_btnApply, 6, 6); bottomRow.Controls.Add(_btnApply);
        Pad(_btnSave, 6, 6); bottomRow.Controls.Add(_btnSave);

        // ═══ Add to form ═══
        Controls.Add(_dgv);
        Controls.Add(row4);
        Controls.Add(row3);
        Controls.Add(row2);
        Controls.Add(row1);
        Controls.Add(bottomRow);

        RefreshGroups();
        LoadCurrentGroupToUI();
        RefreshGrid();
    }

    /// <summary>应用替换时选中的分组名称</summary>
    public string? AppliedGroupName { get; private set; }

    /// <summary>作用列，从当前分组返回 group.ScopeColumns</summary>
    public List<string>? SelectedScopeColumns
    {
        get
        {
            var group = GetCurrentGroup();
            return group?.ScopeColumns;
        }
    }

    private List<ReplacementRule> CurrentRules
    {
        get
        {
            if (_cmbGroup.SelectedIndex < 0 || _cmbGroup.SelectedIndex >= _groups.Count)
                return _groups.Count > 0 ? _groups[0].Rules : new List<ReplacementRule>();
            return _groups[_cmbGroup.SelectedIndex].Rules;
        }
    }

    private ReplacementGroup? GetCurrentGroup()
    {
        if (_groups.Count == 0) return null;
        int idx = _cmbGroup.SelectedIndex;
        if (idx < 0 || idx >= _groups.Count) return _groups[0];
        return _groups[idx];
    }

    private bool _refreshing;

    private void RefreshGroups()
    {
        var prevIndex = _cmbGroup.SelectedIndex;
        _cmbGroup.Items.Clear();
        foreach (var g in _groups)
            _cmbGroup.Items.Add(g.Name);
        if (_groups.Count > 0)
            _cmbGroup.SelectedIndex = prevIndex >= 0 && prevIndex < _groups.Count ? prevIndex : 0;
    }

    private void LoadCurrentGroupToUI()
    {
        var group = GetCurrentGroup();
        if (group == null) return;

        _savedScopeColumns = group.ScopeColumns;
        UpdateScopeSummaryLabel();

        _rbMatchFuzzy.Checked = group.MatchMode == ReplacementMatchMode.Fuzzy;
        _rbMatchExact.Checked = group.MatchMode == ReplacementMatchMode.Exact;

        _rbTypeNormal.Checked = group.Type == ReplacementType.Normal;
        _rbTypeExtended.Checked = group.Type == ReplacementType.Extended;

        _rbWriteOverwrite.Checked = group.ExtendWriteMode == ExtendWriteMode.Overwrite;
        _rbWriteInsert.Checked = group.ExtendWriteMode == ExtendWriteMode.Insert;

        UpdateTypeDependentControls();
    }

    private void SaveGroupUIToCurrentGroup()
    {
        SaveGroupUIToGroup(GetCurrentGroup());
    }

    private void SaveCurrentEditorStateToGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= _groups.Count) return;
        var group = _groups[groupIndex];
        CaptureFromGridToGroup(group);
        SaveGroupUIToGroup(group);
    }

    private void SaveGroupUIToGroup(ReplacementGroup? group)
    {
        if (group == null) return;
        group.ScopeColumns = _savedScopeColumns is { Count: > 0 } ? _savedScopeColumns : null;
        group.MatchMode = _rbMatchExact.Checked ? ReplacementMatchMode.Exact : ReplacementMatchMode.Fuzzy;
        group.Type = _rbTypeExtended.Checked ? ReplacementType.Extended : ReplacementType.Normal;
        group.ExtendWriteMode = _rbWriteInsert.Checked ? ExtendWriteMode.Insert : ExtendWriteMode.Overwrite;
    }

    private void UpdateMatchModeFromUI()
    {
        var group = GetCurrentGroup();
        if (group == null) return;
        group.MatchMode = _rbMatchExact.Checked ? ReplacementMatchMode.Exact : ReplacementMatchMode.Fuzzy;
    }

    /// <summary>替换类型变更时：写回 group.Type 并刷新 grid 列结构 / 写入模式可见性</summary>
    private void UpdateTypeFromUI()
    {
        var group = GetCurrentGroup();
        if (group == null) return;
        group.Type = _rbTypeExtended.Checked ? ReplacementType.Extended : ReplacementType.Normal;
        UpdateTypeDependentControls();
    }

    private void UpdateWriteModeFromUI()
    {
        var group = GetCurrentGroup();
        if (group == null) return;
        group.ExtendWriteMode = _rbWriteInsert.Checked ? ExtendWriteMode.Insert : ExtendWriteMode.Overwrite;
    }

    private void UpdateTypeDependentControls()
    {
        var group = GetCurrentGroup();
        bool isExtended = group?.Type == ReplacementType.Extended;
        _lblWriteMode.Visible = isExtended;
        _pnlWriteMode.Visible = isExtended;
        _btnAddExtraColumn.Visible = isExtended;
        _btnRemoveExtraColumn.Visible = isExtended;
        _btnRenameExtColumns.Visible = isExtended;
    }

    private void UpdateScopeSummaryLabel()
    {
        if (_allColumns == null)
        {
            _lblScopeSummary.Text = "[全表]";
            return;
        }

        if (_savedScopeColumns is { Count: > 0 })
        {
            int maxShow = 3;
            var display = string.Join("、", _savedScopeColumns.Take(maxShow));
            if (_savedScopeColumns.Count > maxShow)
                display = $"{display} 等";
            _lblScopeSummary.Text = $"[{_savedScopeColumns.Count}列：{display}]";
        }
        else
        {
            _lblScopeSummary.Text = "[全表]";
        }
    }

    private void BtnSelectScopeColumns_Click(object? sender, EventArgs e)
    {
        if (_allColumns == null) return;

        using var dialog = new ScopeColumnDialog(_allColumns, _savedScopeColumns);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _savedScopeColumns = dialog.SelectedColumns;
            UpdateScopeSummaryLabel();
        }
    }

    private void BtnAddGroup_Click(object? sender, EventArgs e)
    {
        var name = _txtNewGroupName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请输入分组名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_groups.Any(g => g.Name == name))
        {
            MessageBox.Show("分组名已存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _groups.Add(new ReplacementGroup { Name = name });
        RefreshGroups();
        _cmbGroup.SelectedIndex = _groups.Count - 1;
        LoadCurrentGroupToUI();
        RefreshGrid();
        _txtNewGroupName.Clear();
    }

    private void BtnDeleteGroup_Click(object? sender, EventArgs e)
    {
        if (_groups.Count <= 1)
        {
            MessageBox.Show("至少保留一个分组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var idx = _cmbGroup.SelectedIndex;
        if (idx < 0 || idx >= _groups.Count) return;

        var name = _groups[idx].Name;
        var result = MessageBox.Show($"确定删除分组「{name}」及其所有替换规则吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        _groups.RemoveAt(idx);
        RefreshGroups();
        LoadCurrentGroupToUI();
        RefreshGrid();
    }

    /// <summary>
    /// 刷新 DataGridView。列结构取决于分组替换类型：
    /// 普通替换 → 启用、替换前、替换后
    /// 拓展替换 → 启用、替换前、替换后、扩展1、扩展2...
    /// </summary>
    private void RefreshGrid()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var rules = CurrentRules;
            var group = GetCurrentGroup();
            bool isExtended = group?.Type == ReplacementType.Extended;

            int maxExtra = 0;
            if (isExtended)
            {
                foreach (var r in rules)
                {
                    if (r.ExtraValues.Count > maxExtra)
                        maxExtra = r.ExtraValues.Count;
                }
                // 拓展替换至少显示 1 个扩展列，否则看起来会和普通替换表单完全一样。
                maxExtra = Math.Max(1, maxExtra);
            }

            var dt = new DataTable();
            dt.Columns.Add("启用", typeof(bool));
            dt.Columns.Add("替换前", typeof(string));
            dt.Columns.Add("替换后", typeof(string));
            if (isExtended)
            {
                for (int i = 0; i < maxExtra; i++)
                {
                    var name = group?.ExtraColumnNames is { Count: > 0 } && i < group.ExtraColumnNames.Count
                        ? group.ExtraColumnNames[i]
                        : $"扩展{i + 1}";
                    dt.Columns.Add(name, typeof(string));
                }
            }

            foreach (var r in rules)
            {
                var rowValues = new List<object?> { r.Enabled, r.Before, r.After };
                if (isExtended)
                {
                    for (int i = 0; i < maxExtra; i++)
                        rowValues.Add(i < r.ExtraValues.Count ? r.ExtraValues[i] : "");
                }
                dt.Rows.Add(rowValues.ToArray());
            }

            _dgv.DataSource = null;
            _dgv.Columns.Clear();
            _dgv.DataSource = dt;
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>从 DataGridView 捕获数据并写回当前分组。</summary>
    private void CaptureFromGrid()
    {
        CaptureFromGridToGroup(GetCurrentGroup());
    }

    private void CaptureFromGridToGroup(ReplacementGroup? group)
    {
        if (group == null) return;
        if (_dgv.DataSource is not DataTable dt) return;

        if (_dgv.IsCurrentCellInEditMode)
            _dgv.EndEdit();

        var rules = group.Rules;
        rules.Clear();

        bool isExtended = group.Type == ReplacementType.Extended;

        int colEnabled = -1, colBefore = -1, colAfter = -1;
        var colExtras = new List<int>();
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            var name = dt.Columns[i].ColumnName;
            if (name == "启用") colEnabled = i;
            else if (name == "替换前") colBefore = i;
            else if (name == "替换后") colAfter = i;
            else if (isExtended && colAfter >= 0 && i > colAfter) colExtras.Add(i);
        }

        foreach (DataRow r in dt.Rows)
        {
            var extraValues = new List<string>();
            if (isExtended)
            {
                foreach (int ei in colExtras)
                    extraValues.Add(r[ei]?.ToString() ?? "");
            }

            rules.Add(new ReplacementRule
            {
                Enabled = colEnabled >= 0 && r[colEnabled] is bool b && b,
                Before = colBefore >= 0 ? r[colBefore]?.ToString() ?? "" : "",
                After = colAfter >= 0 ? r[colAfter]?.ToString() ?? "" : "",
                ReplacementType = isExtended ? ReplacementType.Extended : ReplacementType.Normal,
                ExtraValues = extraValues
            });
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;
        CaptureFromGrid();
        var rules = CurrentRules;
        var indicesToRemove = _dgv.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.Index)
            .OrderByDescending(i => i)
            .ToList();
        foreach (int idx in indicesToRemove)
        {
            if (idx >= 0 && idx < rules.Count)
                rules.RemoveAt(idx);
        }
        RefreshGrid();
    }

    private void BtnAddExtraColumn_Click(object? sender, EventArgs e)
    {
        var group = GetCurrentGroup();
        if (group?.Type != ReplacementType.Extended) return;
        CaptureFromGrid();
        foreach (var rule in CurrentRules)
            rule.ExtraValues.Add("");
        RefreshGrid();
    }

    private void BtnRemoveExtraColumn_Click(object? sender, EventArgs e)
    {
        var group = GetCurrentGroup();
        if (group?.Type != ReplacementType.Extended) return;
        CaptureFromGrid();
        foreach (var rule in CurrentRules)
        {
            if (rule.ExtraValues.Count > 0)
                rule.ExtraValues.RemoveAt(rule.ExtraValues.Count - 1);
        }
        RefreshGrid();
    }

    private void BtnRenameExtColumns_Click(object? sender, EventArgs e)
    {
        var group = GetCurrentGroup();
        if (group?.Type != ReplacementType.Extended) return;

        int colCount = 0;
        foreach (var r in CurrentRules)
            colCount = Math.Max(colCount, r.ExtraValues.Count);

        var names = group.ExtraColumnNames ?? new List<string>();
        var current = string.Join(", ", names);
        if (colCount > names.Count)
        {
            for (int i = names.Count; i < colCount; i++)
                current += string.IsNullOrEmpty(current) ? $"扩展{i + 1}" : $", 扩展{i + 1}";
        }

        using var dialog = new Form
        {
            Text = "命名扩展列",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(480, 150),
            MinimumSize = new Size(420, 150),
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
        var lbl = new Label
        {
            Text = "输入扩展列名称，用逗号分隔（如：规格, 图号, 材质）：",
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8)
        };
        var txtNames = new TextBox
        {
            Text = current,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12)
        };
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
        layout.Controls.Add(txtNames, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = btnOk;
        dialog.CancelButton = btnCancel;

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var newNames = txtNames.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        group.ExtraColumnNames = newNames.Count > 0 ? newNames : null;
        RefreshGrid();
    }

    private void BtnImportClipboard_Click(object? sender, EventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show("剪切板中没有文本数据。请先复制替换表到剪切板（列头建议包含：替换前, 替换后）。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var data = ClipboardImportService.Import();
            if (data == null || data.RowCount == 0)
            {
                MessageBox.Show("无法解析剪切板数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int beforeCol = -1, afterCol = -1;
            for (int i = 0; i < data.ColumnCount; i++)
            {
                var name = data.Headers[i].ToLowerInvariant();
                if (name.Contains("前") || name.Contains("旧") || name == "old" || name == "before") beforeCol = i;
                if (name.Contains("后") || name.Contains("新") || name == "new" || name == "after") afterCol = i;
            }
            if (beforeCol < 0) beforeCol = 0;
            if (afterCol < 0) afterCol = Math.Min(1, data.ColumnCount - 1);

            var group = GetCurrentGroup();
            bool isExtended = group?.Type == ReplacementType.Extended;

            int added = 0;
            foreach (var row in data.Rows)
            {
                var before = (beforeCol < row.Count ? row[beforeCol] : "")?.Trim();
                var after = (afterCol < row.Count ? row[afterCol] : "")?.Trim();
                if (!string.IsNullOrEmpty(before))
                {
                    var extraValues = new List<string>();
                    if (isExtended)
                    {
                        for (int i = afterCol + 1; i < row.Count; i++)
                            extraValues.Add(row[i]?.Trim() ?? "");
                    }

                    CurrentRules.Add(new ReplacementRule
                    {
                        Before = before,
                        After = after ?? "",
                        Enabled = true,
                        ReplacementType = isExtended ? ReplacementType.Extended : ReplacementType.Normal,
                        ExtraValues = extraValues
                    });
                    added++;
                }
            }

            RefreshGrid();
            MessageBox.Show($"已从剪切板导入 {added} 条替换规则到当前分组。", "导入成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
