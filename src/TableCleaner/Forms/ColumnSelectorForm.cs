using TableCleaner.Models;
using TableCleaner.Services;

namespace TableCleaner.Forms;

/// <summary>列选择 + 合并规则配置（非模态窗口，应用后保持打开）</summary>
public class ColumnSelectorForm : Form
{
    private readonly CheckedListBox _clbKeep;
    private readonly CheckedListBox _clbGroup;
    private readonly CheckedListBox _clbSum;

    private readonly ComboBox _cmbProfile;
    private readonly TextBox _txtProfileName;
    private readonly Button _btnSave;
    private readonly Button _btnDelete;

    private readonly Button _btnApplyKeep;
    private readonly Button _btnApplyMerge;
    private readonly Button _btnClose;

    private List<CleanProfile> _profiles;
    private readonly List<string> _allColumns;

    public CleanProfile? SelectedProfile { get; private set; }

    public List<string> KeptColumns =>
        _clbKeep.CheckedItems.Cast<string>().ToList();

    public List<string> GroupColumns =>
        _clbGroup.CheckedItems.Cast<string>().ToList();

    public List<string> SumColumns =>
        _clbSum.CheckedItems.Cast<string>().ToList();

    /// <summary>列清洗已应用事件</summary>
    public event EventHandler? KeepApplied;

    /// <summary>合并规则已应用事件</summary>
    public event EventHandler? MergeApplied;

    public ColumnSelectorForm(List<string> allColumns, List<CleanProfile> existingProfiles, CleanProfile? current = null)
    {
        Icon = AppVisuals.WindowIcon;
        _allColumns = allColumns;
        _profiles = existingProfiles;

        Text = "列选择与合并规则";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1120, 760);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        // ---- Profile section (DPI-safe: FlowLayoutPanel) ----
        var profilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 8, 12, 8)
        };
        var lblProfile = new Label { Text = "方案：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) };
        _cmbProfile = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0) };
        _cmbProfile.SelectedIndexChanged += (_, _) => LoadProfileFromCombo();
        _txtProfileName = new TextBox { Width = 120, PlaceholderText = "新方案名", Margin = new Padding(0, 0, 6, 0) };
        _btnSave = new Button { Text = "保存方案", AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
        _btnSave.Click += BtnSaveProfile_Click;
        _btnDelete = new Button { Text = "删除方案", AutoSize = true };
        _btnDelete.Click += (_, _) =>
        {
            if (_cmbProfile.SelectedIndex < 0) return;
            _profiles.RemoveAt(_cmbProfile.SelectedIndex);
            ConfigService.SaveProfiles(_profiles);
            RefreshProfiles();
        };
        profilePanel.Controls.AddRange(new Control[] { lblProfile, _cmbProfile, _txtProfileName, _btnSave, _btnDelete });

        // ---- Three-column selection panels (DPI/font safe: TableLayoutPanel, no absolute coordinates) ----
        var columnsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 12, 0),
            ColumnCount = 3,
            RowCount = 1
        };
        columnsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        columnsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        columnsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        columnsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        bool keepAllChecked = true;
        bool groupAllChecked = false;
        bool sumAllChecked = false;

        static Panel CreateColumnPanel(string title, CheckedListBox list, EventHandler toggleHandler)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 8, 0) };
            var lbl = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };
            var btn = new Button
            {
                Text = "全选 / 取消全选",
                Dock = DockStyle.Top,
                Height = 30,
                Margin = new Padding(0, 0, 0, 4)
            };
            btn.Click += toggleHandler;
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.IntegralHeight = false;

            panel.Controls.Add(list);
            panel.Controls.Add(btn);
            panel.Controls.Add(lbl);
            return panel;
        }

        _clbKeep = new CheckedListBox();
        _clbGroup = new CheckedListBox();
        _clbSum = new CheckedListBox();

        columnsPanel.Controls.Add(CreateColumnPanel("保留列", _clbKeep, (_, _) => { keepAllChecked = !keepAllChecked; ToggleAll(_clbKeep, keepAllChecked); }), 0, 0);
        columnsPanel.Controls.Add(CreateColumnPanel("分组列（去重）", _clbGroup, (_, _) => { groupAllChecked = !groupAllChecked; ToggleAll(_clbGroup, groupAllChecked); }), 1, 0);
        columnsPanel.Controls.Add(CreateColumnPanel("求和列", _clbSum, (_, _) => { sumAllChecked = !sumAllChecked; ToggleAll(_clbSum, sumAllChecked); }), 2, 0);

        // Populate column lists
        foreach (var col in allColumns)
        {
            _clbKeep.Items.Add(col, true);
            _clbGroup.Items.Add(col);
            _clbSum.Items.Add(col);
        }

        // ---- Action buttons (DPI-safe: FlowLayoutPanel) ----
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(12, 8, 12, 8)
        };

        _btnClose = new Button { Text = "关闭", AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        _btnClose.Click += (_, _) => Close();
        _btnApplyMerge = new Button { Text = "应用合并规则", AutoSize = true, BackColor = Color.LightGreen, Margin = new Padding(6, 0, 6, 0) };
        _btnApplyMerge.Click += (_, _) =>
        {
            if (_clbGroup.CheckedItems.Count == 0)
            {
                MessageBox.Show("请至少选择一个分组列。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MergeApplied?.Invoke(this, EventArgs.Empty);
        };
        _btnApplyKeep = new Button { Text = "应用列清洗（保留勾选列）", AutoSize = true, BackColor = Color.LightSteelBlue, Margin = new Padding(6, 0, 6, 0) };
        _btnApplyKeep.Click += (_, _) => { KeepApplied?.Invoke(this, EventArgs.Empty); };
        var btnUsage = new Button { Text = "📖 使用说明", AutoSize = true, Margin = new Padding(6, 0, 6, 0) };
        btnUsage.Click += (_, _) => { 
            MessageBox.Show("列选择：勾选要保留的列并点击\"应用列清洗\"。\n合并规则：勾选分组列和求和列后点击\"应用合并规则\"。\n注意：列清洗和合并规则不能同时应用。", "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information); 
        };
        actionPanel.Controls.Add(btnUsage);
        actionPanel.Controls.Add(_btnClose);
        actionPanel.Controls.Add(_btnApplyMerge);
        actionPanel.Controls.Add(_btnApplyKeep);

        // Info label (DPI-safe)
        var infoPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 4, 12, 6)
        };
        var lblInfo = new Label
        {
            Text = "提示：保留列勾选后仅保留这些列；分组列将按相同值合并为一行；求和列做数值加总；其他列不同值用 + 连接忽略空值。",
            AutoSize = true,
            ForeColor = Color.Gray
        };
        infoPanel.Controls.Add(lblInfo);

        Controls.Add(columnsPanel);
        Controls.Add(infoPanel);
        Controls.Add(actionPanel);
        Controls.Add(profilePanel);

        RefreshProfiles();
        if (current != null) LoadProfile(current);
    }

    private void RefreshProfiles()
    {
        _cmbProfile.Items.Clear();
        foreach (var p in _profiles)
            _cmbProfile.Items.Add(p.Name);
        if (_profiles.Count > 0) _cmbProfile.SelectedIndex = 0;
    }

    private void LoadProfileFromCombo()
    {
        if (_cmbProfile.SelectedIndex >= 0 && _cmbProfile.SelectedIndex < _profiles.Count)
            LoadProfile(_profiles[_cmbProfile.SelectedIndex]);
    }

    private void LoadProfile(CleanProfile profile)
    {
        // Keep columns
        for (int i = 0; i < _clbKeep.Items.Count; i++)
        {
            var name = _clbKeep.Items[i].ToString();
            _clbKeep.SetItemChecked(i, profile.KeptColumns.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        // Group columns
        for (int i = 0; i < _clbGroup.Items.Count; i++)
        {
            var name = _clbGroup.Items[i].ToString();
            _clbGroup.SetItemChecked(i, profile.GroupColumns.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        // Sum columns
        for (int i = 0; i < _clbSum.Items.Count; i++)
        {
            var name = _clbSum.Items[i].ToString();
            _clbSum.SetItemChecked(i, profile.SumColumns.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
    }

    private void BtnSaveProfile_Click(object? sender, EventArgs e)
    {
        var name = _txtProfileName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        { MessageBox.Show("请输入方案名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var profile = new CleanProfile
        {
            Name = name,
            KeptColumns = KeptColumns,
            GroupColumns = GroupColumns,
            SumColumns = SumColumns
        };

        _profiles.RemoveAll(p => p.Name == name);
        _profiles.Insert(0, profile);
        ConfigService.SaveProfiles(_profiles);
        RefreshProfiles();

        // Select the newly saved profile
        for (int i = 0; i < _cmbProfile.Items.Count; i++)
            if (_cmbProfile.Items[i]?.ToString() == name) { _cmbProfile.SelectedIndex = i; break; }

        MessageBox.Show($"方案「{name}」已保存。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void ToggleAll(CheckedListBox clb, bool check)
    {
        for (int i = 0; i < clb.Items.Count; i++) clb.SetItemChecked(i, check);
    }
}
