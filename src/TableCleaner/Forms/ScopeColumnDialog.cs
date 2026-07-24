using TableCleaner.Models;

namespace TableCleaner.Forms;

/// <summary>作用列多选弹窗（按钮式入口，替代窄 CheckedListBox，支持实时筛选过滤并保留勾选状态）</summary>
public class ScopeColumnDialog : Form
{
    private readonly CheckedListBox _clbColumns;
    private readonly TextBox _txtFilter;
    private readonly Button _btnClearFilter;
    private readonly Button _btnSelectAll;
    private readonly Button _btnClearAll;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    private sealed record ColumnChoice(ColumnReference Reference, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly List<ColumnChoice> _allColumns;
    private readonly HashSet<ColumnReference> _checkedState = new();

    /// <summary>用户选择的列列表；null 或空 = 全表</summary>
    public List<ColumnReference>? SelectedColumns { get; private set; }

    public ScopeColumnDialog(TableData table, List<ColumnReference>? initialChecked = null)
    {
        Icon = AppVisuals.WindowIcon;
        _allColumns = table.Columns
            .Select((_, index) => new ColumnChoice(
                table.GetColumnReference(index),
                table.GetColumnDisplayName(index)))
            .ToList();

        Text = "选择作用列（留空=全表）";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 460);
        MinimumSize = new Size(320, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var filterRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _txtFilter = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "筛选列名...",
            Margin = new Padding(0, 0, 6, 0)
        };
        _txtFilter.TextChanged += (_, _) => ApplyFilter();

        _btnClearFilter = new Button
        {
            Text = "✕",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            TabStop = false
        };
        _btnClearFilter.Click += (_, _) =>
        {
            _txtFilter.Clear();
            _txtFilter.Focus();
        };
        filterRow.Controls.Add(_txtFilter, 0, 0);
        filterRow.Controls.Add(_btnClearFilter, 1, 0);

        _clbColumns = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false
        };

        foreach (var col in _allColumns)
        {
            _clbColumns.Items.Add(col);
            bool shouldCheck = initialChecked is { Count: > 0 } &&
                               initialChecked.Contains(col.Reference);
            if (shouldCheck)
                _checkedState.Add(col.Reference);
        }
        RestoreCheckedStateToVisible();

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnCancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 0, 0) };
        _btnOk = new Button { Text = "确定", AutoSize = true, DialogResult = DialogResult.OK, Margin = new Padding(6, 0, 0, 0) };
        _btnOk.Click += (_, _) =>
        {
            SyncCheckedStateFromVisible();
            SelectedColumns = _checkedState.Count > 0 ? new List<ColumnReference>(_checkedState) : null;
        };
        _btnClearAll = new Button { Text = "取消全选", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        _btnClearAll.Click += (_, _) =>
        {
            _checkedState.Clear();
            for (int i = 0; i < _clbColumns.Items.Count; i++)
                _clbColumns.SetItemChecked(i, false);
        };
        _btnSelectAll = new Button { Text = "全选", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        _btnSelectAll.Click += (_, _) =>
        {
            for (int i = 0; i < _clbColumns.Items.Count; i++)
            {
                var item = (ColumnChoice)_clbColumns.Items[i];
                _clbColumns.SetItemChecked(i, true);
                _checkedState.Add(item.Reference);
            }
        };

        actionRow.Controls.Add(_btnCancel);
        actionRow.Controls.Add(_btnOk);
        actionRow.Controls.Add(_btnClearAll);
        actionRow.Controls.Add(_btnSelectAll);

        root.Controls.Add(filterRow, 0, 0);
        root.Controls.Add(_clbColumns, 0, 1);
        root.Controls.Add(actionRow, 0, 2);
        Controls.Add(root);
    }

    private void SyncCheckedStateFromVisible()
    {
        for (int i = 0; i < _clbColumns.Items.Count; i++)
        {
            var item = (ColumnChoice)_clbColumns.Items[i];
            if (_clbColumns.GetItemChecked(i))
                _checkedState.Add(item.Reference);
            else
                _checkedState.Remove(item.Reference);
        }
    }

    private void RestoreCheckedStateToVisible()
    {
        for (int i = 0; i < _clbColumns.Items.Count; i++)
        {
            var item = (ColumnChoice)_clbColumns.Items[i];
            if (_checkedState.Contains(item.Reference))
                _clbColumns.SetItemChecked(i, true);
        }
    }

    private void ApplyFilter()
    {
        var keyword = _txtFilter.Text.Trim();
        SyncCheckedStateFromVisible();
        _clbColumns.Items.Clear();

        foreach (var col in _allColumns)
        {
            bool matches = string.IsNullOrEmpty(keyword) ||
                           col.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            if (matches)
                _clbColumns.Items.Add(col);
        }

        RestoreCheckedStateToVisible();
    }
}
