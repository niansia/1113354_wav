using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace _1113354_陳冠瑋
{
    public partial class Form1 : Form
    {
        // 使用 Windows 內建 MCI 播放 WAV，不用安裝 NuGet 套件
        [DllImport("winmm.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);

        private const string Alias = "wavxplayer";

        private readonly List<string> _playlist = new List<string>();
        private readonly Random _random = new Random();

        private bool _mciOpen = false;
        private bool _playRequested = false;
        private bool _isSeeking = false;

        private int _currentIndex = -1;
        private long _durationMs = 0;
        private long _lastPositionMs = 0;

        private TextBox mTxtPath;
        private ListBox mLstPlaylist;

        private Button mBtnAddFiles;
        private Button mBtnAddFolder;
        private Button mBtnPlay;
        private Button mBtnPause;
        private Button mBtnStop;
        private Button mBtnPrev;
        private Button mBtnNext;
        private Button mBtnRemove;
        private Button mBtnClear;
        private Button mBtnSaveList;
        private Button mBtnLoadList;
        private Button mBtnExit;

        private TrackBar mTrkProgress;
        private TrackBar mTrkVolume;
        private TrackBar mTrkSpeed;

        private Label mLblNow;
        private Label mLblTime;
        private Label mLblInfo;
        private Label mLblStatus;
        private Label mLblVolume;
        private Label mLblSpeed;

        private CheckBox mChkShuffle;
        private ComboBox mCboLoop;

        private Timer mTimer;

        public Form1()
        {
            InitializeComponent();

            BuildModernUI();

            this.FormClosing -= Form1_FormClosing;
            this.FormClosing += Form1_FormClosing;
        }

        private void BuildModernUI()
        {
            SuspendLayout();
            Controls.Clear();

            Text = "WAV 音效播放器 Pro Max";
            Size = new Size(1000, 700);
            MinimumSize = new Size(900, 580);
            Font = new Font("Microsoft JhengHei UI", 10F);
            KeyPreview = true;
            AllowDrop = true;

            DragEnter += Form1_DragEnter;
            DragDrop += Form1_DragDrop;
            KeyDown += Form1_KeyDown;

            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.Padding = new Padding(14);
            main.RowCount = 5;
            main.ColumnCount = 1;
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Controls.Add(main);

            GroupBox fileBox = new GroupBox();
            fileBox.Text = "音效位置 / 匯入 WAV";
            fileBox.Dock = DockStyle.Fill;

            TableLayoutPanel fileLayout = new TableLayoutPanel();
            fileLayout.Dock = DockStyle.Fill;
            fileLayout.Padding = new Padding(10);
            fileLayout.ColumnCount = 4;
            fileLayout.RowCount = 1;
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            mTxtPath = new TextBox();
            mTxtPath.Dock = DockStyle.Fill;
            mTxtPath.ReadOnly = true;
            mTxtPath.Margin = new Padding(4, 16, 8, 4);

            mBtnAddFiles = MakeButton("加入檔案", 100);
            mBtnAddFolder = MakeButton("加入資料夾", 100);
            mBtnExit = MakeButton("結束", 75);

            mBtnAddFiles.Click += delegate { AddFilesFromDialog(); };
            mBtnAddFolder.Click += delegate { AddFolderFromDialog(); };
            mBtnExit.Click += delegate { Close(); };

            fileLayout.Controls.Add(mTxtPath, 0, 0);
            fileLayout.Controls.Add(mBtnAddFiles, 1, 0);
            fileLayout.Controls.Add(mBtnAddFolder, 2, 0);
            fileLayout.Controls.Add(mBtnExit, 3, 0);

            fileBox.Controls.Add(fileLayout);
            main.Controls.Add(fileBox, 0, 0);

            GroupBox controlBox = new GroupBox();
            controlBox.Text = "播放控制";
            controlBox.Dock = DockStyle.Fill;

            FlowLayoutPanel controlPanel = new FlowLayoutPanel();
            controlPanel.Dock = DockStyle.Fill;
            controlPanel.Padding = new Padding(12);
            controlPanel.WrapContents = true;

            mBtnPrev = MakeButton("⏮ 上一首", 100);
            mBtnPlay = MakeButton("▶ 播放", 100);
            mBtnPause = MakeButton("⏸ 暫停", 100);
            mBtnStop = MakeButton("⏹ 停止", 100);
            mBtnNext = MakeButton("⏭ 下一首", 100);
            mBtnRemove = MakeButton("移除選取", 105);
            mBtnClear = MakeButton("清空清單", 105);
            mBtnSaveList = MakeButton("儲存清單", 105);
            mBtnLoadList = MakeButton("載入清單", 105);

            mChkShuffle = new CheckBox();
            mChkShuffle.Text = "隨機播放";
            mChkShuffle.AutoSize = true;
            mChkShuffle.Margin = new Padding(12, 12, 6, 6);

            Label loopLabel = new Label();
            loopLabel.Text = "循環模式";
            loopLabel.AutoSize = true;
            loopLabel.Margin = new Padding(12, 14, 4, 4);

            mCboLoop = new ComboBox();
            mCboLoop.DropDownStyle = ComboBoxStyle.DropDownList;
            mCboLoop.Width = 120;
            mCboLoop.Margin = new Padding(4, 9, 6, 6);
            mCboLoop.Items.Add("不循環");
            mCboLoop.Items.Add("單曲循環");
            mCboLoop.Items.Add("清單循環");
            mCboLoop.SelectedIndex = 0;

            mBtnPrev.Click += delegate { PlayPrevious(); };
            mBtnPlay.Click += delegate { PlaySelectedOrCurrent(); };
            mBtnPause.Click += delegate { TogglePause(); };
            mBtnStop.Click += delegate { StopPlayback(); };
            mBtnNext.Click += delegate { PlayNext(); };
            mBtnRemove.Click += delegate { RemoveSelected(); };
            mBtnClear.Click += delegate { ClearPlaylist(); };
            mBtnSaveList.Click += delegate { SavePlaylist(); };
            mBtnLoadList.Click += delegate { LoadPlaylist(); };

            controlPanel.Controls.Add(mBtnPrev);
            controlPanel.Controls.Add(mBtnPlay);
            controlPanel.Controls.Add(mBtnPause);
            controlPanel.Controls.Add(mBtnStop);
            controlPanel.Controls.Add(mBtnNext);
            controlPanel.Controls.Add(mBtnRemove);
            controlPanel.Controls.Add(mBtnClear);
            controlPanel.Controls.Add(mBtnSaveList);
            controlPanel.Controls.Add(mBtnLoadList);
            controlPanel.Controls.Add(mChkShuffle);
            controlPanel.Controls.Add(loopLabel);
            controlPanel.Controls.Add(mCboLoop);

            controlBox.Controls.Add(controlPanel);
            main.Controls.Add(controlBox, 0, 1);

            GroupBox listBox = new GroupBox();
            listBox.Text = "播放清單：可直接拖曳 WAV 檔或資料夾進來";
            listBox.Dock = DockStyle.Fill;

            mLstPlaylist = new ListBox();
            mLstPlaylist.Dock = DockStyle.Fill;
            mLstPlaylist.HorizontalScrollbar = true;
            mLstPlaylist.IntegralHeight = false;
            mLstPlaylist.DoubleClick += delegate { PlaySelectedOrCurrent(); };
            mLstPlaylist.SelectedIndexChanged += delegate { UpdateSelectedInfo(); };

            listBox.Controls.Add(mLstPlaylist);
            main.Controls.Add(listBox, 0, 2);

            GroupBox nowBox = new GroupBox();
            nowBox.Text = "播放狀態 / 進度 / 音量 / 倍速";
            nowBox.Dock = DockStyle.Fill;

            TableLayoutPanel nowLayout = new TableLayoutPanel();
            nowLayout.Dock = DockStyle.Fill;
            nowLayout.Padding = new Padding(10);
            nowLayout.RowCount = 4;
            nowLayout.ColumnCount = 1;
            nowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            nowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            nowLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            nowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            mLblNow = new Label();
            mLblNow.Text = "尚未播放";
            mLblNow.Dock = DockStyle.Fill;
            mLblNow.TextAlign = ContentAlignment.MiddleLeft;

            mTrkProgress = new TrackBar();
            mTrkProgress.Dock = DockStyle.Fill;
            mTrkProgress.Minimum = 0;
            mTrkProgress.Maximum = 10000;
            mTrkProgress.TickFrequency = 1000;
            mTrkProgress.MouseDown += delegate { _isSeeking = true; };
            mTrkProgress.MouseUp += delegate
            {
                SeekFromProgressBar();
                _isSeeking = false;
            };
            mTrkProgress.KeyUp += delegate { SeekFromProgressBar(); };
            mTrkProgress.Scroll += delegate
            {
                if (_durationMs > 0)
                {
                    long preview = ProgressValueToMs(mTrkProgress.Value);
                    UpdateTimeDisplay(preview, _durationMs);
                }
            };

            mLblTime = new Label();
            mLblTime.Text = "00:00 / 00:00";
            mLblTime.Dock = DockStyle.Fill;
            mLblTime.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel optionPanel = new FlowLayoutPanel();
            optionPanel.Dock = DockStyle.Fill;
            optionPanel.WrapContents = false;

            mLblVolume = new Label();
            mLblVolume.Text = "音量 80%";
            mLblVolume.Width = 85;
            mLblVolume.TextAlign = ContentAlignment.MiddleLeft;
            mLblVolume.Margin = new Padding(4, 12, 4, 4);

            mTrkVolume = new TrackBar();
            mTrkVolume.Minimum = 0;
            mTrkVolume.Maximum = 1000;
            mTrkVolume.Value = 800;
            mTrkVolume.Width = 180;
            mTrkVolume.TickFrequency = 100;
            mTrkVolume.ValueChanged += delegate
            {
                mLblVolume.Text = "音量 " + (mTrkVolume.Value / 10) + "%";
                ApplyVolume();
            };

            mLblSpeed = new Label();
            mLblSpeed.Text = "倍速 100%";
            mLblSpeed.Width = 95;
            mLblSpeed.TextAlign = ContentAlignment.MiddleLeft;
            mLblSpeed.Margin = new Padding(20, 12, 4, 4);

            mTrkSpeed = new TrackBar();
            mTrkSpeed.Minimum = 50;
            mTrkSpeed.Maximum = 200;
            mTrkSpeed.Value = 100;
            mTrkSpeed.Width = 200;
            mTrkSpeed.TickFrequency = 25;
            mTrkSpeed.ValueChanged += delegate
            {
                mLblSpeed.Text = "倍速 " + mTrkSpeed.Value + "%";
                ApplySpeed();
            };

            mLblInfo = new Label();
            mLblInfo.Text = "WAV 資訊：尚未選取檔案";
            mLblInfo.AutoSize = true;
            mLblInfo.Margin = new Padding(20, 13, 4, 4);

            optionPanel.Controls.Add(mLblVolume);
            optionPanel.Controls.Add(mTrkVolume);
            optionPanel.Controls.Add(mLblSpeed);
            optionPanel.Controls.Add(mTrkSpeed);
            optionPanel.Controls.Add(mLblInfo);

            nowLayout.Controls.Add(mLblNow, 0, 0);
            nowLayout.Controls.Add(mTrkProgress, 0, 1);
            nowLayout.Controls.Add(mLblTime, 0, 2);
            nowLayout.Controls.Add(optionPanel, 0, 3);

            nowBox.Controls.Add(nowLayout);
            main.Controls.Add(nowBox, 0, 3);

            mLblStatus = new Label();
            mLblStatus.Dock = DockStyle.Fill;
            mLblStatus.TextAlign = ContentAlignment.MiddleLeft;
            mLblStatus.Text = "提示：Space 暫停/繼續，←/→ 倒退/快轉 5 秒，N 下一首，P 上一首，Delete 移除選取。";
            main.Controls.Add(mLblStatus, 0, 4);

            mTimer = new Timer();
            mTimer.Interval = 200;
            mTimer.Tick += UiTimer_Tick;
            mTimer.Start();

            ResumeLayout();
        }

        private Button MakeButton(string text, int width)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Width = width;
            btn.Height = 36;
            btn.Margin = new Padding(6);
            btn.FlatStyle = FlatStyle.System;
            return btn;
        }

        private void AddFilesFromDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "選擇 WAV 檔案";
                ofd.Filter = "WAV 音效檔 (*.wav)|*.wav";
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    AddFilesToPlaylist(ofd.FileNames);
                }
            }
        }

        private void AddFolderFromDialog()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "選擇包含 WAV 的資料夾，會自動掃描子資料夾";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    AddFilesToPlaylist(new string[] { fbd.SelectedPath });
                }
            }
        }

        private int AddFilesToPlaylist(IEnumerable<string> paths)
        {
            int added = 0;

            foreach (string rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                if (Directory.Exists(rawPath))
                {
                    added += AddFilesToPlaylist(SafeEnumerateWavFiles(rawPath));
                    continue;
                }

                if (!File.Exists(rawPath))
                    continue;

                if (!string.Equals(Path.GetExtension(rawPath), ".wav", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fullPath = Path.GetFullPath(rawPath);

                if (PlaylistContains(fullPath))
                    continue;

                _playlist.Add(fullPath);
                mLstPlaylist.Items.Add(GetPlaylistCaption(fullPath));
                added++;
            }

            if (_currentIndex < 0 && _playlist.Count > 0)
            {
                _currentIndex = 0;
                mLstPlaylist.SelectedIndex = 0;
            }

            SetStatus("已加入 " + added + " 個 WAV 檔案。");
            return added;
        }

        private IEnumerable<string> SafeEnumerateWavFiles(string folder)
        {
            string[] files = new string[0];
            string[] folders = new string[0];

            try
            {
                files = Directory.GetFiles(folder, "*.wav");
            }
            catch
            {
            }

            foreach (string file in files)
                yield return file;

            try
            {
                folders = Directory.GetDirectories(folder);
            }
            catch
            {
            }

            foreach (string subFolder in folders)
            {
                foreach (string file in SafeEnumerateWavFiles(subFolder))
                    yield return file;
            }
        }

        private bool PlaylistContains(string path)
        {
            string full = Path.GetFullPath(path);

            foreach (string item in _playlist)
            {
                if (string.Equals(Path.GetFullPath(item), full, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private string GetPlaylistCaption(string path)
        {
            try
            {
                WaveInfo info = WaveInfo.Read(path);
                return Path.GetFileName(path) + "    [" + FormatTime(info.DurationMs) + "]    " + info.ShortDescription;
            }
            catch
            {
                return Path.GetFileName(path);
            }
        }

        private void PlaySelectedOrCurrent()
        {
            if (_playlist.Count == 0)
            {
                SetStatus("請先加入 WAV 檔案。");
                return;
            }

            int index = mLstPlaylist.SelectedIndex;

            if (index < 0)
                index = _currentIndex >= 0 ? _currentIndex : 0;

            PlayIndex(index);
        }

        private void PlayIndex(int index)
        {
            if (index < 0 || index >= _playlist.Count)
                return;

            string path = _playlist[index];

            if (!File.Exists(path))
            {
                MessageBox.Show("找不到檔案：\n" + path, "檔案不存在", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CloseMci();

                bool opened = RunMci("open " + Quote(path) + " type waveaudio alias " + Alias, false);

                if (!opened)
                    RunMci("open " + Quote(path) + " alias " + Alias, true);

                _mciOpen = true;

                RunMci("set " + Alias + " time format milliseconds", false);

                _durationMs = GetLengthSafe();

                if (_durationMs <= 0)
                {
                    try
                    {
                        _durationMs = WaveInfo.Read(path).DurationMs;
                    }
                    catch
                    {
                        _durationMs = 0;
                    }
                }

                ApplyVolume();
                ApplySpeed();

                RunMci("play " + Alias, true);

                _currentIndex = index;
                _playRequested = true;
                _lastPositionMs = 0;

                if (mLstPlaylist.SelectedIndex != index)
                    mLstPlaylist.SelectedIndex = index;

                mTxtPath.Text = path;
                mLblNow.Text = "正在播放：" + Path.GetFileName(path);
                mBtnPause.Text = "⏸ 暫停";

                UpdateTimeDisplay(0, _durationMs);
                SetStatus("播放中：" + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                _playRequested = false;
                MessageBox.Show("播放失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TogglePause()
        {
            if (!_mciOpen)
            {
                PlaySelectedOrCurrent();
                return;
            }

            string mode = GetModeSafe();

            if (mode == "playing")
            {
                RunMci("pause " + Alias, false);
                _playRequested = false;
                mBtnPause.Text = "▶ 繼續";
                SetStatus("已暫停。");
            }
            else if (mode == "paused")
            {
                bool ok = RunMci("resume " + Alias, false);

                if (!ok)
                    RunMci("play " + Alias, false);

                _playRequested = true;
                mBtnPause.Text = "⏸ 暫停";
                SetStatus("繼續播放。");
            }
            else
            {
                RunMci("play " + Alias, false);
                _playRequested = true;
                mBtnPause.Text = "⏸ 暫停";
                SetStatus("繼續播放。");
            }
        }

        private void StopPlayback()
        {
            if (_mciOpen)
            {
                RunMci("stop " + Alias, false);
                RunMci("seek " + Alias + " to start", false);
            }

            _playRequested = false;
            _lastPositionMs = 0;
            mBtnPause.Text = "⏸ 暫停";

            if (mTrkProgress != null)
                mTrkProgress.Value = 0;

            UpdateTimeDisplay(0, _durationMs);
            SetStatus("已停止。");
        }

        private void PlayNext()
        {
            int next;

            if (TryGetNextIndex(out next))
                PlayIndex(next);
            else
                SetStatus("已經是最後一首。");
        }

        private void PlayPrevious()
        {
            if (_playlist.Count == 0)
                return;

            long pos = GetPositionSafe();

            if (pos > 3000)
            {
                SeekTo(0);
                return;
            }

            int prev = _currentIndex - 1;

            if (prev < 0)
            {
                if (GetLoopMode() == 2)
                    prev = _playlist.Count - 1;
                else
                {
                    SetStatus("已經是第一首。");
                    return;
                }
            }

            PlayIndex(prev);
        }

        private bool TryGetNextIndex(out int next)
        {
            next = -1;

            if (_playlist.Count == 0)
                return false;

            if (mChkShuffle.Checked && _playlist.Count > 1)
            {
                do
                {
                    next = _random.Next(_playlist.Count);
                }
                while (next == _currentIndex);

                return true;
            }

            int candidate = _currentIndex + 1;

            if (candidate < _playlist.Count)
            {
                next = candidate;
                return true;
            }

            if (GetLoopMode() == 2)
            {
                next = 0;
                return true;
            }

            return false;
        }

        private int GetLoopMode()
        {
            if (mCboLoop == null || mCboLoop.SelectedIndex < 0)
                return 0;

            return mCboLoop.SelectedIndex;
        }

        private void HandleTrackEnded()
        {
            _playRequested = false;

            if (GetLoopMode() == 1)
            {
                PlayIndex(_currentIndex);
                return;
            }

            int next;

            if (TryGetNextIndex(out next))
            {
                PlayIndex(next);
            }
            else
            {
                StopPlayback();
                SetStatus("清單播放完畢。");
            }
        }

        private void RemoveSelected()
        {
            int index = mLstPlaylist.SelectedIndex;

            if (index < 0 || index >= _playlist.Count)
                return;

            bool removingCurrent = index == _currentIndex;

            if (removingCurrent)
                CloseMci();

            _playlist.RemoveAt(index);
            mLstPlaylist.Items.RemoveAt(index);

            if (_playlist.Count == 0)
            {
                _currentIndex = -1;
                mTxtPath.Clear();
                mLblNow.Text = "尚未播放";
                mLblInfo.Text = "WAV 資訊：尚未選取檔案";
                UpdateTimeDisplay(0, 0);
            }
            else
            {
                if (_currentIndex > index)
                    _currentIndex--;

                int newIndex = Math.Min(index, _playlist.Count - 1);
                mLstPlaylist.SelectedIndex = newIndex;
            }

            SetStatus("已移除選取項目。");
        }

        private void ClearPlaylist()
        {
            CloseMci();

            _playlist.Clear();
            mLstPlaylist.Items.Clear();

            _currentIndex = -1;
            _durationMs = 0;
            _lastPositionMs = 0;

            mTxtPath.Clear();
            mLblNow.Text = "尚未播放";
            mLblInfo.Text = "WAV 資訊：尚未選取檔案";
            mTrkProgress.Value = 0;
            UpdateTimeDisplay(0, 0);

            SetStatus("播放清單已清空。");
        }

        private void SavePlaylist()
        {
            if (_playlist.Count == 0)
            {
                SetStatus("播放清單是空的，無法儲存。");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "儲存播放清單";
                sfd.Filter = "M3U 播放清單 (*.m3u)|*.m3u|文字檔 (*.txt)|*.txt";
                sfd.FileName = "WAV_Playlist.m3u";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    List<string> lines = new List<string>();
                    lines.Add("#EXTM3U");

                    foreach (string path in _playlist)
                        lines.Add(path);

                    File.WriteAllLines(sfd.FileName, lines.ToArray(), Encoding.UTF8);
                    SetStatus("播放清單已儲存。");
                }
            }
        }

        private void LoadPlaylist()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "載入播放清單";
                ofd.Filter = "M3U 或文字播放清單 (*.m3u;*.txt)|*.m3u;*.txt|所有檔案 (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string baseFolder = Path.GetDirectoryName(ofd.FileName);
                    List<string> files = new List<string>();

                    foreach (string line in File.ReadAllLines(ofd.FileName, Encoding.UTF8))
                    {
                        string item = line.Trim();

                        if (item.Length == 0 || item.StartsWith("#"))
                            continue;

                        if (!Path.IsPathRooted(item))
                            item = Path.Combine(baseFolder, item);

                        files.Add(item);
                    }

                    AddFilesToPlaylist(files);
                    SetStatus("播放清單已載入。");
                }
            }
        }

        private void UpdateSelectedInfo()
        {
            int index = mLstPlaylist.SelectedIndex;

            if (index < 0 || index >= _playlist.Count)
                return;

            string path = _playlist[index];
            mTxtPath.Text = path;

            if (!_mciOpen || index != _currentIndex)
                mLblNow.Text = "選取：" + Path.GetFileName(path);

            try
            {
                WaveInfo info = WaveInfo.Read(path);
                mLblInfo.Text = info.FullDescription;

                if (!_mciOpen || index != _currentIndex)
                    UpdateTimeDisplay(0, info.DurationMs);
            }
            catch (Exception ex)
            {
                mLblInfo.Text = "WAV 資訊讀取失敗：" + ex.Message;
            }
        }

        private void SeekFromProgressBar()
        {
            if (!_mciOpen || _durationMs <= 0)
                return;

            long target = ProgressValueToMs(mTrkProgress.Value);
            SeekTo(target);
        }

        private void SeekTo(long targetMs)
        {
            if (!_mciOpen)
                return;

            if (targetMs < 0)
                targetMs = 0;

            if (_durationMs > 0 && targetMs > _durationMs)
                targetMs = _durationMs;

            RunMci("seek " + Alias + " to " + targetMs, false);

            if (_playRequested || GetModeSafe() == "playing")
                RunMci("play " + Alias, false);

            _lastPositionMs = targetMs;
            UpdateTimeDisplay(targetMs, _durationMs);
        }

        private void SeekRelative(long offsetMs)
        {
            if (!_mciOpen)
                return;

            long pos = GetPositionSafe();
            SeekTo(pos + offsetMs);
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (!_mciOpen)
                return;

            long pos = GetPositionSafe();

            if (_durationMs <= 0)
                _durationMs = GetLengthSafe();

            if (!_isSeeking && _durationMs > 0)
            {
                int value = MsToProgressValue(pos);
                value = Math.Max(mTrkProgress.Minimum, Math.Min(mTrkProgress.Maximum, value));
                mTrkProgress.Value = value;
            }

            UpdateTimeDisplay(pos, _durationMs);

            string mode = GetModeSafe();

            if (mode == "playing")
                mBtnPause.Text = "⏸ 暫停";
            else if (mode == "paused")
                mBtnPause.Text = "▶ 繼續";

            bool reachedEnd = _durationMs > 0 &&
                              (pos >= _durationMs - 450 || _lastPositionMs >= _durationMs - 450);

            if (_playRequested && mode == "stopped" && reachedEnd)
            {
                _lastPositionMs = 0;
                HandleTrackEnded();
                return;
            }

            _lastPositionMs = pos;
        }

        private void ApplyVolume()
        {
            if (!_mciOpen || mTrkVolume == null)
                return;

            RunMci("setaudio " + Alias + " volume to " + mTrkVolume.Value, false);
        }

        private void ApplySpeed()
        {
            if (!_mciOpen || mTrkSpeed == null)
                return;

            // MCI 的標準速度值：1000 = 原速，所以 50% = 500，200% = 2000
            int speed = mTrkSpeed.Value * 10;
            RunMci("set " + Alias + " speed " + speed, false);
        }

        private long GetLengthSafe()
        {
            long value;

            if (long.TryParse(QueryMci("status " + Alias + " length"), out value))
                return value;

            return 0;
        }

        private long GetPositionSafe()
        {
            long value;

            if (long.TryParse(QueryMci("status " + Alias + " position"), out value))
                return value;

            return 0;
        }

        private string GetModeSafe()
        {
            string mode = QueryMci("status " + Alias + " mode");

            if (string.IsNullOrWhiteSpace(mode))
                return "";

            return mode.Trim().ToLower();
        }

        private void CloseMci()
        {
            if (_mciOpen)
            {
                RunMci("stop " + Alias, false);
                RunMci("close " + Alias, false);
            }

            _mciOpen = false;
            _playRequested = false;
            _durationMs = 0;
            _lastPositionMs = 0;
        }

        private static bool RunMci(string command, bool throwOnError)
        {
            int error = mciSendString(command, null, 0, IntPtr.Zero);

            if (error != 0 && throwOnError)
                throw new InvalidOperationException(GetMciError(error) + "\n指令：" + command);

            return error == 0;
        }

        private static string QueryMci(string command)
        {
            StringBuilder buffer = new StringBuilder(256);
            int error = mciSendString(command, buffer, buffer.Capacity, IntPtr.Zero);

            if (error != 0)
                return "";

            return buffer.ToString();
        }

        private static string GetMciError(int errorCode)
        {
            StringBuilder sb = new StringBuilder(256);

            if (mciGetErrorString(errorCode, sb, sb.Capacity))
                return sb.ToString();

            return "MCI 錯誤代碼：" + errorCode;
        }

        private static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "") + "\"";
        }

        private int MsToProgressValue(long ms)
        {
            if (_durationMs <= 0)
                return 0;

            return (int)(ms * mTrkProgress.Maximum / _durationMs);
        }

        private long ProgressValueToMs(int value)
        {
            if (_durationMs <= 0)
                return 0;

            return (long)(_durationMs * value / (double)mTrkProgress.Maximum);
        }

        private void UpdateTimeDisplay(long posMs, long totalMs)
        {
            if (mLblTime != null)
                mLblTime.Text = FormatTime(posMs) + " / " + FormatTime(totalMs);
        }

        private static string FormatTime(long ms)
        {
            if (ms < 0)
                ms = 0;

            TimeSpan t = TimeSpan.FromMilliseconds(ms);

            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);

            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        private void SetStatus(string text)
        {
            if (mLblStatus != null)
                mLblStatus.Text = DateTime.Now.ToString("HH:mm:ss") + "　" + text;
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];

            if (paths != null)
                AddFilesToPlaylist(paths);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                TogglePause();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SeekRelative(5000);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                SeekRelative(-5000);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.N)
            {
                PlayNext();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.P)
            {
                PlayPrevious();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && mLstPlaylist.Focused)
            {
                RemoveSelected();
                e.Handled = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "確定要關閉應用程式嗎？",
                "關閉確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            CloseMci();
        }

        // 以下保留舊按鈕事件名稱，避免 Designer 還有綁定時編譯失敗
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            AddFilesFromDialog();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            PlaySelectedOrCurrent();
        }

        private void btnLoop_Click(object sender, EventArgs e)
        {
            if (mCboLoop != null)
                mCboLoop.SelectedIndex = 1;

            PlaySelectedOrCurrent();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopPlayback();
        }

        private void btnEnd_Click(object sender, EventArgs e)
        {
            Close();
        }

        private class WaveInfo
        {
            public short AudioFormat;
            public short Channels;
            public int SampleRate;
            public int ByteRate;
            public short BlockAlign;
            public short BitsPerSample;
            public long DataBytes;
            public long DurationMs;

            public string FormatName
            {
                get
                {
                    if (AudioFormat == 1)
                        return "PCM";
                    if (AudioFormat == 3)
                        return "IEEE Float";

                    return "格式代碼 " + AudioFormat;
                }
            }

            public string ShortDescription
            {
                get
                {
                    return Channels + " 聲道 / " +
                           SampleRate + " Hz / " +
                           BitsPerSample + " bit";
                }
            }

            public string FullDescription
            {
                get
                {
                    return "WAV 資訊：" +
                           FormatName + "｜" +
                           Channels + " 聲道｜" +
                           SampleRate + " Hz｜" +
                           BitsPerSample + " bit｜" +
                           "長度 " + FormatTime(DurationMs);
                }
            }

            public static WaveInfo Read(string path)
            {
                using (FileStream fs = File.OpenRead(path))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    string riff = ReadFourCC(br);

                    if (riff != "RIFF")
                        throw new InvalidDataException("不是 RIFF 檔案。");

                    br.ReadUInt32();

                    string wave = ReadFourCC(br);

                    if (wave != "WAVE")
                        throw new InvalidDataException("不是 WAVE 檔案。");

                    WaveInfo info = new WaveInfo();

                    while (fs.Position + 8 <= fs.Length)
                    {
                        string chunkId = ReadFourCC(br);
                        uint chunkSize = br.ReadUInt32();
                        long nextChunk = fs.Position + chunkSize;

                        if (chunkId == "fmt " && chunkSize >= 16)
                        {
                            info.AudioFormat = br.ReadInt16();
                            info.Channels = br.ReadInt16();
                            info.SampleRate = br.ReadInt32();
                            info.ByteRate = br.ReadInt32();
                            info.BlockAlign = br.ReadInt16();
                            info.BitsPerSample = br.ReadInt16();
                        }
                        else if (chunkId == "data")
                        {
                            info.DataBytes = chunkSize;
                        }

                        fs.Position = nextChunk;

                        if ((chunkSize & 1) == 1 && fs.Position < fs.Length)
                            fs.Position++;
                    }

                    if (info.ByteRate > 0 && info.DataBytes > 0)
                        info.DurationMs = info.DataBytes * 1000 / info.ByteRate;

                    return info;
                }
            }

            private static string ReadFourCC(BinaryReader br)
            {
                byte[] bytes = br.ReadBytes(4);

                if (bytes.Length < 4)
                    return "";

                return Encoding.ASCII.GetString(bytes);
            }
        }
    }
}