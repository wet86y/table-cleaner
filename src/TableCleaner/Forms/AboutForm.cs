using DesktopUpdateKit;

namespace TableCleaner.Forms;

public sealed class AboutForm : Form
{
    private static readonly UpdateClientOptions UpdateOptions = new(
        ApplicationId: "table-cleaner",
        Repository: "wet86y/table-cleaner",
        ExeAssetName: "table-cleaner.exe",
        Sha256AssetName: "table-cleaner.exe.sha256",
        TempDirectoryName: "StupidTable-update",
        CurrentVersion: Version.Parse(AppVisuals.DisplayVersion));

    private static readonly UpdateClient SharedUpdateClient = new(UpdateOptions);
    private static readonly UpdateDownloadSession SharedUpdateSession = new(SharedUpdateClient);
    private readonly UpdateLauncher _updateLauncher = new();

    private UpdateRelease? _availableUpdate;
    private bool _checkingForUpdate;

    // Controls
    private readonly Label _lblTitle;
    private readonly Label _lblVersion;
    private readonly Label _lblDeveloper;
    private readonly LinkLabel _lnkGitHub;
    private readonly Button _btnCheckUpdate;
    private readonly Button _btnInstall;
    private readonly Button _btnPauseResume;
    private readonly Button _btnBackground;
    private readonly Button _btnCancel;
    private readonly Button _btnSwitchNode;
    private readonly CheckBox _chkAcceleration;
    private readonly Label _lblStatus;
    private readonly ProgressBar _prgDownload;
    private readonly GroupBox _grpReleaseNotes;
    private readonly TextBox _txtReleaseNotes;

    public AboutForm()
    {
        Icon = AppVisuals.WindowIcon;
        Text = "关于 笨蛋表格";
        ClientSize = new Size(504, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei", 9F);

        var y = 12;
        const int pad = 12;
        const int w = 480;

        // Title
        _lblTitle = new Label
        {
            Text = "笨蛋表格",
            Font = new Font("Microsoft YaHei", 20F, FontStyle.Bold),
            Location = new Point(pad, y),
            Size = new Size(w, 46),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseCompatibleTextRendering = true
        };
        y = _lblTitle.Bottom + 2;

        // Version
        _lblVersion = new Label
        {
            Text = $"版本 {GetCurrentVersion()}",
            Location = new Point(pad, y),
            Size = new Size(w, 22),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray
        };
        y = _lblVersion.Bottom + 2;

        // Developer
        _lblDeveloper = new Label
        {
            Text = "开发者：wet86y",
            Location = new Point(pad, y),
            Size = new Size(w, 22),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };
        y = _lblDeveloper.Bottom;

        // GitHub link
        _lnkGitHub = new LinkLabel
        {
            Text = "GitHub 项目仓库与更新记录",
            Location = new Point(pad, y),
            Size = new Size(w, 22),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        _lnkGitHub.LinkClicked += (_, _) =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo("https://github.com/wet86y/table-cleaner")
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        };
        y = _lnkGitHub.Bottom + pad;

        // --- Update section ---
        // Check update button
        _btnCheckUpdate = new Button
        {
            Text = "检查更新",
            Location = new Point(pad, y),
            Size = new Size(104, 32),
            UseVisualStyleBackColor = true
        };
        _btnCheckUpdate.Click += async (_, _) => await CheckUpdateAsync();
        y += _btnCheckUpdate.Height + 4;

        // Status
        _lblStatus = new Label
        {
            Text = "点击\"检查更新\"获取最新版本。",
            Location = new Point(pad, y),
            AutoSize = false,
            Size = new Size(w, 22),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(w, 0)
        };
        y += _lblStatus.Height + 8;

        // Progress bar
        _prgDownload = new ProgressBar
        {
            Location = new Point(pad, y),
            Size = new Size(w, 18),
            Visible = false
        };
        y += _prgDownload.Height + 8;

        // Action buttons row 1
        _btnInstall = CreateActionButton("下载更新", pad, ref y, visible: false);
        _btnPauseResume = CreateActionButton("暂停", _btnInstall.Right + 6, ref y, visible: false);
        _btnBackground = CreateActionButton("后台下载", pad, ref y, visible: false);
        _btnCancel = CreateActionButton("取消", _btnBackground.Right + 6, ref y, visible: false);

        // Action buttons row 2
        _chkAcceleration = new CheckBox
        {
            Text = "使用加速节点",
            Location = new Point(pad, y + 2),
            AutoSize = true,
            Checked = true,
            Visible = false
        };
        _chkAcceleration.CheckedChanged += AccelerationToggle_Changed;

        _btnSwitchNode = new Button
        {
            Text = "切换加速节点",
            Location = new Point(_chkAcceleration.Right + 12, y),
            Size = new Size(110, 28),
            Visible = false
        };
        _btnSwitchNode.Click += (_, _) =>
        {
            if (SharedUpdateSession.RequestNextAcceleratedNode())
            {
                _btnSwitchNode.Enabled = false;
                _lblStatus.Text = "正在切换到下一个加速节点...";
            }
        };
        y += 36;

        // Release notes
        _grpReleaseNotes = new GroupBox
        {
            Text = "更新说明",
            Location = new Point(pad, y),
            Size = new Size(w, 160),
            Padding = new Padding(8),
            Visible = false
        };
        _txtReleaseNotes = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            TabStop = false,
            HideSelection = true
        };
        _grpReleaseNotes.Controls.Add(_txtReleaseNotes);

        Controls.AddRange(new Control[]
        {
            _lblTitle, _lblVersion, _lblDeveloper, _lnkGitHub,
            _btnCheckUpdate, _btnInstall, _btnPauseResume, _btnBackground, _btnCancel,
            _chkAcceleration, _btnSwitchNode,
            _lblStatus, _prgDownload, _grpReleaseNotes
        });

        SharedUpdateSession.Changed += OnSessionChanged;
        Load += (_, _) => ApplySessionSnapshot(SharedUpdateSession.Snapshot);
        FormClosing += (_, _) =>
        {
            SharedUpdateSession.Changed -= OnSessionChanged;
            SharedUpdateSession.PauseWhenUiCloses();
        };
    }

    internal static bool VerifyUpdateLayouts()
    {
        using var form = new AboutForm
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Opacity = 0
        };
        form.Show();

        form._lblStatus.Text = "Downloading 17 MB/71.3 MB at 3.4 MB/s using four parallel connections through an accelerated node.";
        form._btnCheckUpdate.Visible = false;
        form._prgDownload.Visible = true;
        form.HideAllActionButtons();
        form._btnPauseResume.Visible = true;
        form._btnSwitchNode.Visible = true;
        form._btnBackground.Visible = true;
        form._btnCancel.Visible = true;
        form._grpReleaseNotes.Visible = true;
        form.LayoutUpdateControls();
        var downloadingIsValid = IsAbove(form._lblStatus, form._prgDownload)
            && IsAbove(form._prgDownload, form._btnPauseResume)
            && IsAbove(form._btnPauseResume, form._btnBackground)
            && IsAbove(form._btnBackground, form._grpReleaseNotes)
            && form.ClientRectangle.Contains(form._grpReleaseNotes.Bounds);

        form._lblStatus.Text = "The update was downloaded and verified. Click install to replace the current executable.";
        form._btnCheckUpdate.Visible = true;
        form._prgDownload.Visible = true;
        form.HideAllActionButtons();
        form._btnInstall.Visible = true;
        form._grpReleaseNotes.Visible = true;
        form.LayoutUpdateControls();
        var completedIsValid = IsAbove(form._btnCheckUpdate, form._prgDownload)
            && IsAbove(form._lblStatus, form._prgDownload)
            && IsAbove(form._prgDownload, form._btnInstall)
            && IsAbove(form._btnInstall, form._grpReleaseNotes)
            && form.ClientRectangle.Contains(form._grpReleaseNotes.Bounds);

        form.Close();
        return downloadingIsValid && completedIsValid;
    }

    internal static async Task<bool> ShutdownUpdateSessionAsync(TimeSpan timeout)
    {
        var stopped = await SharedUpdateSession.StopAsync(timeout).ConfigureAwait(false);
        SharedUpdateSession.Dispose();
        return stopped;
    }

    private static bool IsAbove(Control upper, Control lower) => upper.Bottom <= lower.Top;

    private static Button CreateActionButton(string text, int x, ref int y, bool visible = true)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(120, 32),
            Visible = visible
        };
        if (!visible) _ = y;
        return btn;
    }

    private async Task CheckUpdateAsync()
    {
        var session = SharedUpdateSession.Snapshot;
        if (_checkingForUpdate || session.State is UpdateDownloadSessionState.Downloading or UpdateDownloadSessionState.Paused)
        {
            return;
        }

        _checkingForUpdate = true;
        _btnCheckUpdate.Enabled = false;
        _btnCheckUpdate.Text = "正在检查...";
        _btnCheckUpdate.Visible = true;
        _prgDownload.Visible = false;
        _prgDownload.Value = 0;
        _prgDownload.Style = ProgressBarStyle.Blocks;
        _availableUpdate = null;
        _chkAcceleration.Visible = false;
        HideAllActionButtons();
        _grpReleaseNotes.Visible = false;
        _lblStatus.Text = "正在检查更新...";
        LayoutUpdateControls();

        try
        {
            var release = await SharedUpdateClient.CheckForUpdateAsync();
            if (release is null)
            {
                _lblStatus.Text = $"当前版本 {GetCurrentVersion()}，已是最新版本。";
                return;
            }

            ShowAvailableUpdate(release);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            _checkingForUpdate = false;
            _btnCheckUpdate.Enabled = true;
            _btnCheckUpdate.Text = "重新检查";
            LayoutUpdateControls();
        }
    }

    private void ShowAvailableUpdate(UpdateRelease release)
    {
        _availableUpdate = release;
        var notes = string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? "此版本没有附加更新说明。"
            : release.ReleaseNotes.Trim();
        if (notes.Length > 1800)
        {
            notes = notes[..1800] + "\n...";
        }

        _txtReleaseNotes.Text = notes;
        _grpReleaseNotes.Visible = true;
        _chkAcceleration.Visible = true;
        _btnInstall.Text = $"下载更新 {release.Version}";
        _btnInstall.Enabled = true;
        _btnInstall.Visible = true;
        _btnCheckUpdate.Visible = true;
        _btnCheckUpdate.Text = "重新检查";
        _lblStatus.Text = $"发现新版本 {release.Version}。确认说明后，点击\"下载更新\"。";
        LayoutUpdateControls();
    }

    private async void InstallUpdate_Click(object? sender, EventArgs e)
    {
        var session = SharedUpdateSession.Snapshot;
        if (session.State == UpdateDownloadSessionState.Completed
            && !string.IsNullOrWhiteSpace(session.DownloadedPath)
            && session.Release is not null)
        {
            _btnInstall.Enabled = false;
            _lblStatus.Text = "校验完成，正在启动更新助手...";
            try
            {
                await _updateLauncher.LaunchAsync(session.DownloadedPath, session.Release.ExpectedSha256);
                _lblStatus.Text = "更新助手已启动，程序即将退出。";
                Application.Exit();
            }
            catch (Exception ex)
            {
                _btnInstall.Enabled = true;
                _lblStatus.Text = $"启动更新失败：{ex.Message}";
            }

            return;
        }

        if (session.State == UpdateDownloadSessionState.Paused)
        {
            SharedUpdateSession.Resume();
            return;
        }

        if (session.State == UpdateDownloadSessionState.Downloading || _availableUpdate is null)
        {
            return;
        }

        if (!SharedUpdateSession.TryStart(_availableUpdate, _chkAcceleration.Checked))
        {
            _lblStatus.Text = "已有更新下载任务正在运行。";
        }
    }

    private void PauseResumeDownload_Click(object? sender, EventArgs e)
    {
        if (SharedUpdateSession.Snapshot.State == UpdateDownloadSessionState.Paused)
        {
            SharedUpdateSession.Resume();
            return;
        }

        SharedUpdateSession.Pause();
    }

    private void BackgroundDownload_Click(object? sender, EventArgs e)
    {
        if (SharedUpdateSession.ContinueInBackground())
        {
            Close();
        }
    }

    private void CancelDownload_Click(object? sender, EventArgs e)
    {
        if (SharedUpdateSession.Cancel())
        {
            _btnCancel.Enabled = false;
            _btnPauseResume.Enabled = false;
            _lblStatus.Text = "正在取消下载...";
        }
    }

    private void AccelerationToggle_Changed(object? sender, EventArgs e)
    {
        if (SharedUpdateSession.SetUseAccelerationNodes(_chkAcceleration.Checked))
        {
            _lblStatus.Text = _chkAcceleration.Checked
                ? "已启用加速节点，当前下载将切换到加速节点。"
                : "已关闭加速节点，当前下载将切换到 GitHub 官方直连。";
        }
    }

    private void OnSessionChanged(object? sender, UpdateDownloadSessionSnapshot snapshot)
    {
        if (!IsDisposed)
        {
            BeginInvoke(() => ApplySessionSnapshot(snapshot));
        }
    }

    private void ApplySessionSnapshot(UpdateDownloadSessionSnapshot snapshot)
    {
        if (snapshot.Release is not null)
        {
            _availableUpdate = snapshot.Release;
            ShowReleaseNotes(snapshot.Release);
        }

        _chkAcceleration.Checked = snapshot.UseAccelerationNodes;

        switch (snapshot.State)
        {
            case UpdateDownloadSessionState.Idle:
                return;

            case UpdateDownloadSessionState.Downloading:
                SetDownloadingUi(snapshot);
                break;

            case UpdateDownloadSessionState.Paused:
                SetPausedUi(snapshot);
                break;

            case UpdateDownloadSessionState.Completed:
                SetCompletedUi(snapshot);
                break;

            case UpdateDownloadSessionState.Cancelled:
                ResetDownloadUi();
                _lblStatus.Text = "下载已取消。可重新点击\"下载更新\"。";
                break;

            case UpdateDownloadSessionState.Failed:
                ResetDownloadUi();
                _lblStatus.Text = $"下载失败：{snapshot.ErrorMessage ?? "未知错误"}";
                break;
        }

        LayoutUpdateControls();
    }

    private void SetDownloadingUi(UpdateDownloadSessionSnapshot snapshot)
    {
        _btnCheckUpdate.Enabled = false;
        _btnCheckUpdate.Visible = false;
        _chkAcceleration.Visible = false;
        HideAllActionButtons();
        _btnPauseResume.Text = "暂停下载";
        _btnPauseResume.Visible = true;
        _btnBackground.Text = "后台下载";
        _btnBackground.Visible = true;
        _btnSwitchNode.Visible = snapshot.UseAccelerationNodes;
        _btnSwitchNode.Enabled = snapshot.UseAccelerationNodes;
        _btnCancel.Visible = true;
        _btnCancel.Enabled = true;
        _prgDownload.Visible = true;
        _prgDownload.Style = ProgressBarStyle.Blocks;

        if (snapshot.Progress is not null)
        {
            ReportDownloadProgress(snapshot.Progress, snapshot.ContinueInBackground);
        }
        else
        {
            _prgDownload.Style = ProgressBarStyle.Marquee;
            _lblStatus.Text = "正在下载并校验更新...";
            LayoutUpdateControls();
        }
    }

    private void SetPausedUi(UpdateDownloadSessionSnapshot snapshot)
    {
        _btnCheckUpdate.Enabled = false;
        _btnCheckUpdate.Visible = false;
        _chkAcceleration.Visible = false;
        HideAllActionButtons();
        _btnPauseResume.Text = "继续下载";
        _btnPauseResume.Visible = true;
        _btnBackground.Text = "后台继续";
        _btnBackground.Visible = true;
        _btnSwitchNode.Visible = snapshot.UseAccelerationNodes;
        _btnSwitchNode.Enabled = snapshot.UseAccelerationNodes;
        _btnCancel.Visible = true;
        _btnCancel.Enabled = true;
        _prgDownload.Visible = true;
        _prgDownload.Style = ProgressBarStyle.Blocks;

        if (snapshot.Progress is not null)
        {
            ReportDownloadProgress(snapshot.Progress, background: false);
        }

        _lblStatus.Text = "下载已暂停。关闭窗口不会取消；可后台继续。";
        LayoutUpdateControls();
    }

    private void SetCompletedUi(UpdateDownloadSessionSnapshot snapshot)
    {
        _btnCheckUpdate.Enabled = true;
        _btnCheckUpdate.Visible = true;
        _btnCheckUpdate.Text = "重新检查";
        _chkAcceleration.Visible = false;
        HideAllActionButtons();
        _prgDownload.Visible = true;
        _prgDownload.Style = ProgressBarStyle.Blocks;
        _prgDownload.Value = 100;
        _btnInstall.Text = $"立即安装 {snapshot.Release?.Version}";
        _btnInstall.Enabled = !string.IsNullOrWhiteSpace(snapshot.DownloadedPath);
        _btnInstall.Visible = true;
        _lblStatus.Text = "更新已下载并完成校验。点击\"立即安装\"后退出并替换程序。";

        LayoutUpdateControls();
    }

    private void ResetDownloadUi()
    {
        _btnCheckUpdate.Enabled = true;
        _btnCheckUpdate.Visible = true;
        _btnCheckUpdate.Text = "重新检查";
        _chkAcceleration.Visible = _availableUpdate is not null;
        _prgDownload.Visible = false;
        _prgDownload.Style = ProgressBarStyle.Blocks;
        HideAllActionButtons();
        _btnInstall.Text = _availableUpdate is null ? "下载更新" : $"下载更新 {_availableUpdate.Version}";
        _btnInstall.Enabled = _availableUpdate is not null;
        _btnInstall.Visible = _availableUpdate is not null;

        LayoutUpdateControls();
    }

    private void HideAllActionButtons()
    {
        _btnInstall.Visible = false;
        _btnPauseResume.Visible = false;
        _btnBackground.Visible = false;
        _btnCancel.Visible = false;
        _btnSwitchNode.Visible = false;
    }

    private void LayoutUpdateControls()
    {
        SuspendLayout();

        var pad = LogicalToDeviceUnits(12);
        var gap = LogicalToDeviceUnits(8);
        var rowGap = LogicalToDeviceUnits(6);
        var targetClientWidth = LogicalToDeviceUnits(504);
        var contentWidth = targetClientWidth - (pad * 2);
        var y = _lnkGitHub.Bottom + pad;
        var statusPlaced = false;

        if (_btnCheckUpdate.Visible)
        {
            _btnCheckUpdate.Location = new Point(pad, y);
            var statusGap = LogicalToDeviceUnits(12);
            var statusWidth = Math.Max(LogicalToDeviceUnits(80), contentWidth - _btnCheckUpdate.Width - statusGap);
            var statusHeight = MeasureStatusHeight(statusWidth);
            _lblStatus.Size = new Size(statusWidth, statusHeight);
            _lblStatus.Location = new Point(
                _btnCheckUpdate.Right + statusGap,
                y + Math.Max(0, (_btnCheckUpdate.Height - statusHeight) / 2));
            y = Math.Max(_btnCheckUpdate.Bottom, _lblStatus.Bottom) + gap;
            statusPlaced = true;
        }

        if (!statusPlaced)
        {
            _lblStatus.Location = new Point(pad, y);
            _lblStatus.Size = new Size(contentWidth, MeasureStatusHeight(contentWidth));
            y = _lblStatus.Bottom + gap;
        }

        if (_prgDownload.Visible)
        {
            _prgDownload.Location = new Point(pad, y);
            y = _prgDownload.Bottom + gap;
        }

        if (_btnInstall.Visible)
        {
            _btnInstall.Location = new Point(pad, y);
            y = _btnInstall.Bottom + gap;
        }

        if (_btnPauseResume.Visible)
        {
            _btnPauseResume.Location = new Point(pad, y);
            if (_btnSwitchNode.Visible)
            {
                _btnSwitchNode.Location = new Point(
                    _btnPauseResume.Right + rowGap,
                    y + LogicalToDeviceUnits(2));
            }
            y = Math.Max(_btnPauseResume.Bottom, _btnSwitchNode.Visible ? _btnSwitchNode.Bottom : 0) + rowGap;
        }

        if (_btnBackground.Visible || _btnCancel.Visible)
        {
            if (_btnBackground.Visible)
            {
                _btnBackground.Location = new Point(pad, y);
            }
            if (_btnCancel.Visible)
            {
                _btnCancel.Location = new Point(_btnBackground.Visible ? _btnBackground.Right + rowGap : pad, y);
            }
            y = Math.Max(_btnBackground.Visible ? _btnBackground.Bottom : 0, _btnCancel.Visible ? _btnCancel.Bottom : 0) + gap;
        }

        if (_chkAcceleration.Visible)
        {
            _chkAcceleration.Location = new Point(pad, y + 2);
            y = _chkAcceleration.Bottom + gap;
        }

        if (_grpReleaseNotes.Visible)
        {
            _grpReleaseNotes.Location = new Point(pad, y);
            _grpReleaseNotes.Size = new Size(contentWidth, LogicalToDeviceUnits(160));
            y = _grpReleaseNotes.Bottom + pad;
        }

        ClientSize = new Size(targetClientWidth, Math.Max(LogicalToDeviceUnits(250), y));
        ResumeLayout(performLayout: true);
    }

    private int MeasureStatusHeight(int width)
    {
        var measured = TextRenderer.MeasureText(
            _lblStatus.Text,
            _lblStatus.Font,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        return Math.Max(LogicalToDeviceUnits(20), measured.Height);
    }

    private void ShowReleaseNotes(UpdateRelease release)
    {
        _availableUpdate = release;
        var notes = string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? "此版本没有附加更新说明。"
            : release.ReleaseNotes.Trim();
        if (notes.Length > 1800)
        {
            notes = notes[..1800] + "\n...";
        }

        _txtReleaseNotes.Text = notes;
        _txtReleaseNotes.Select(0, 0);
        _grpReleaseNotes.Visible = true;
        LayoutUpdateControls();
    }

    private void ReportDownloadProgress(UpdateDownloadProgress progress, bool background)
    {
        if (progress.Fraction is double fraction)
        {
            _prgDownload.Style = ProgressBarStyle.Blocks;
            _prgDownload.Value = Math.Max(0, Math.Min(100, (int)(fraction * 100)));
        }

        var total = progress.TotalBytes is > 0 ? FormatSize(progress.TotalBytes.Value) : "未知";
        var node = string.IsNullOrWhiteSpace(progress.NodeId) ? "" : $" · {progress.NodeId}";
        var conn = progress.IsParallelFallback
            ? " · 4路失败已回退单路"
            : progress.ActiveConnectionCount > 1 ? $" · {progress.ActiveConnectionCount}路" : "";
        var prefix = background ? "后台下载中" : "下载中";
        _lblStatus.Text = $"{prefix}... {FormatSize(progress.BytesReceived)}/{total} · {FormatSize((long)progress.BytesPerSecond)}/秒{conn}{node}";
        LayoutUpdateControls();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F0} KB";
        return $"{bytes / 1024d / 1024d:F1} MB";
    }

    private static string GetCurrentVersion() => AppVisuals.DisplayVersion;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Wire up button events after form is loaded so visibility state is stable
        _btnInstall.Click += InstallUpdate_Click;
        _btnPauseResume.Click += PauseResumeDownload_Click;
        _btnBackground.Click += BackgroundDownload_Click;
        _btnCancel.Click += CancelDownload_Click;
        LayoutUpdateControls();
    }
}
