using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _1113354_陳冠瑋
{
    public partial class Form1 : Form
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);

        private const string MciAlias = "wav_player";
        private static readonly string[] SupportedAudioExtensions = new string[] { ".wav", ".mp3", ".mp4" };
        private const string SupportedAudioFilter = "支援音訊/影片檔 (*.wav;*.mp3;*.mp4)|*.wav;*.mp3;*.mp4|WAV 檔案 (*.wav)|*.wav|MP3 檔案 (*.mp3)|*.mp3|MP4 影片/音訊 (*.mp4)|*.mp4|所有檔案 (*.*)|*.*";

        private readonly List<TrackItem> _tracks = new List<TrackItem>();
        private readonly HashSet<string> _favoritePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random _random = new Random();

        private TrackItem _currentTrack;
        private FileSystemWatcher _watcher;

        private bool _mciOpen = false;
        private bool _useWmp = false;
        private bool _wmpOpen = false;
        private object _wmpPlayer = null;
        private bool _playRequested = false;
        private bool _isSeeking = false;
        private bool _muted = false;

        private long _durationMs = 0;
        private long _lastPositionMs = 0;
        private long _loopA = -1;
        private long _loopB = -1;

        private int _volumeBeforeMute = 800;
        private int _waveformLoadStamp = 0;

        private TextBox mTxtSearch;
        private TextBox mTxtPath;

        private ListView mList;

        private Label mLblTitle;
        private Label mLblSubtitle;
        private Label mLblStats;
        private Label mLblSelectedInfo;
        private Label mLblNow;
        private Label mLblTime;
        private Label mLblStatus;
        private Label mLblWatch;
        private Label mLblVolume;
        private Label mLblSpeed;
        private Label mLblAB;

        private ModernButton mBtnAddFiles;
        private ModernButton mBtnAddFolder;
        private ModernButton mBtnWatchFolder;
        private ModernButton mBtnStopWatch;
        private ModernButton mBtnCleanMissing;

        private ModernButton mBtnPrev;
        private ModernButton mBtnPlay;
        private ModernButton mBtnPause;
        private ModernButton mBtnStop;
        private ModernButton mBtnNext;
        private ModernButton mBtnMute;

        private ModernButton mBtnRemove;
        private ModernButton mBtnClear;
        private ModernButton mBtnSaveList;
        private ModernButton mBtnLoadList;

        private ModernButton mBtnSetA;
        private ModernButton mBtnSetB;
        private ModernButton mBtnClearAB;

        private CheckBox mChkShuffle;
        private CheckBox mChkOnlyFavorite;
        private ComboBox mCboLoop;

        private TrackBar mTrkProgress;
        private TrackBar mTrkVolume;
        private TrackBar mTrkSpeed;

        private WaveformView mWaveform;
        private VisualizerView mVisualizer;

        private Timer mTimer;

        private ContextMenuStrip mMenu;

        public Form1()
        {
            InitializeComponent();

            this.FormClosing -= Form1_FormClosing;

            BuildProductUI();
            LoadSession();

            this.FormClosing += Form1_FormClosing;
        }

        private void BuildProductUI()
        {
            SuspendLayout();

            Controls.Clear();

            Text = "音訊播放器";
            Size = new Size(1180, 760);
            MinimumSize = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft JhengHei UI", 10F);
            BackColor = AppColor.Bg;
            ForeColor = AppColor.Text;
            KeyPreview = true;
            AllowDrop = true;

            DragEnter += Form1_DragEnter;
            DragDrop += Form1_DragDrop;
            KeyDown += Form1_KeyDown;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.BackColor = AppColor.Bg;
            root.ColumnCount = 2;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(root);

            GradientPanel header = new GradientPanel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0, 0, 0, 14);
            header.Radius = 22;
            header.Color1 = Color.FromArgb(70, 95, 255);
            header.Color2 = Color.FromArgb(120, 55, 210);
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            TableLayoutPanel headerLayout = new TableLayoutPanel();
            headerLayout.Dock = DockStyle.Fill;
            headerLayout.Padding = new Padding(26, 16, 26, 16);
            headerLayout.ColumnCount = 2;
            headerLayout.RowCount = 2;
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            header.Controls.Add(headerLayout);

            mLblTitle = MakeLabel("音訊播放器", 24, FontStyle.Bold, Color.White);
            mLblTitle.Dock = DockStyle.Fill;

            mLblSubtitle = MakeLabel("播放清單｜MP3/MP4/WAV｜波形圖｜A/B 循環｜音訊工具箱｜統計儀表板", 10, FontStyle.Regular, Color.FromArgb(230, 235, 255));
            mLblSubtitle.Dock = DockStyle.Fill;

            mLblStats = MakeLabel("0 首｜00:00｜收藏 0", 11, FontStyle.Bold, Color.White);
            mLblStats.Dock = DockStyle.Fill;
            mLblStats.TextAlign = ContentAlignment.MiddleRight;

            Label hotkey = MakeLabel("Space 暫停/繼續　←/→ 快轉倒退　N 下一首　P 上一首", 9, FontStyle.Regular, Color.FromArgb(232, 236, 255));
            hotkey.Dock = DockStyle.Fill;
            hotkey.TextAlign = ContentAlignment.MiddleRight;

            headerLayout.Controls.Add(mLblTitle, 0, 0);
            headerLayout.Controls.Add(mLblSubtitle, 0, 1);
            headerLayout.Controls.Add(mLblStats, 1, 0);
            headerLayout.Controls.Add(hotkey, 1, 1);

            CardPanel sideCard = new CardPanel();
            sideCard.Dock = DockStyle.Fill;
            sideCard.Margin = new Padding(0, 0, 14, 14);
            root.Controls.Add(sideCard, 0, 1);

            TableLayoutPanel side = new TableLayoutPanel();
            side.Dock = DockStyle.Fill;
            side.Padding = new Padding(16);
            side.RowCount = 15;
            side.ColumnCount = 1;
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 15));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            sideCard.Controls.Add(side);

            Label sideTitle = MakeLabel("音樂庫", 14, FontStyle.Bold, AppColor.Text);
            side.Controls.Add(sideTitle, 0, 0);

            mBtnAddFiles = MakeButton("＋ 加入音訊/影片", true);
            mBtnAddFolder = MakeButton("＋ 加入整個資料夾", false);
            mBtnWatchFolder = MakeButton("◎ 監看資料夾", false);
            mBtnStopWatch = MakeButton("停止監看", false);
            mBtnCleanMissing = MakeButton("清除失效檔案", false);

            mBtnAddFiles.Click += delegate { AddFilesFromDialog(); };
            mBtnAddFolder.Click += delegate { AddFolderFromDialog(); };
            mBtnWatchFolder.Click += delegate { ChooseWatchFolder(); };
            mBtnStopWatch.Click += delegate { StopWatchingFolder(); };
            mBtnCleanMissing.Click += delegate { CleanMissingFiles(); };

            side.Controls.Add(mBtnAddFiles, 0, 1);
            side.Controls.Add(mBtnAddFolder, 0, 2);
            side.Controls.Add(mBtnWatchFolder, 0, 3);
            side.Controls.Add(mBtnStopWatch, 0, 4);
            side.Controls.Add(mBtnCleanMissing, 0, 5);

            Label searchTitle = MakeLabel("搜尋 / 篩選", 12, FontStyle.Bold, AppColor.Text);
            side.Controls.Add(searchTitle, 0, 7);

            mTxtSearch = new TextBox();
            mTxtSearch.Dock = DockStyle.Fill;
            mTxtSearch.BackColor = AppColor.Input;
            mTxtSearch.ForeColor = AppColor.Text;
            mTxtSearch.BorderStyle = BorderStyle.FixedSingle;
            mTxtSearch.Margin = new Padding(0, 5, 0, 5);
            mTxtSearch.TextChanged += delegate { RefreshList(); };
            side.Controls.Add(mTxtSearch, 0, 8);

            mChkOnlyFavorite = new CheckBox();
            mChkOnlyFavorite.Text = "只顯示收藏";
            mChkOnlyFavorite.ForeColor = AppColor.Text;
            mChkOnlyFavorite.BackColor = Color.Transparent;
            mChkOnlyFavorite.Dock = DockStyle.Fill;
            mChkOnlyFavorite.CheckedChanged += delegate { RefreshList(); };
            side.Controls.Add(mChkOnlyFavorite, 0, 9);

            mCboLoop = new ComboBox();
            mCboLoop.DropDownStyle = ComboBoxStyle.DropDownList;
            mCboLoop.BackColor = AppColor.Input;
            mCboLoop.ForeColor = AppColor.Text;
            mCboLoop.Items.Add("不循環");
            mCboLoop.Items.Add("單曲循環");
            mCboLoop.Items.Add("清單循環");
            mCboLoop.SelectedIndex = 0;
            mCboLoop.Dock = DockStyle.Fill;
            mCboLoop.Margin = new Padding(0, 5, 0, 5);
            side.Controls.Add(mCboLoop, 0, 10);

            mChkShuffle = new CheckBox();
            mChkShuffle.Text = "隨機播放";
            mChkShuffle.ForeColor = AppColor.Text;
            mChkShuffle.BackColor = Color.Transparent;
            mChkShuffle.Dock = DockStyle.Fill;
            side.Controls.Add(mChkShuffle, 0, 12);

            mLblWatch = MakeLabel("監看：未啟用", 9, FontStyle.Regular, AppColor.SubText);
            mLblWatch.Dock = DockStyle.Fill;
            mLblWatch.TextAlign = ContentAlignment.BottomLeft;
            side.Controls.Add(mLblWatch, 0, 13);

            mBtnLoadList = MakeButton("載入 M3U / TXT 清單", false);
            mBtnLoadList.Click += delegate { LoadPlaylistFromDialog(); };
            side.Controls.Add(mBtnLoadList, 0, 14);

            CardPanel listCard = new CardPanel();
            listCard.Dock = DockStyle.Fill;
            listCard.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(listCard, 1, 1);

            TableLayoutPanel listLayout = new TableLayoutPanel();
            listLayout.Dock = DockStyle.Fill;
            listLayout.Padding = new Padding(16);
            listLayout.RowCount = 3;
            listLayout.ColumnCount = 1;
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            listCard.Controls.Add(listLayout);

            Label listTitle = MakeLabel("播放清單", 14, FontStyle.Bold, AppColor.Text);
            listLayout.Controls.Add(listTitle, 0, 0);

            mList = new ListView();
            mList.Dock = DockStyle.Fill;
            mList.View = View.Details;
            mList.FullRowSelect = true;
            mList.HideSelection = false;
            mList.MultiSelect = false;
            mList.BorderStyle = BorderStyle.None;
            mList.BackColor = AppColor.Card2;
            mList.ForeColor = AppColor.Text;
            mList.OwnerDraw = true;
            mList.Columns.Add("", 42);
            mList.Columns.Add("檔名", 250);
            mList.Columns.Add("長度", 80);
            mList.Columns.Add("格式", 110);
            mList.Columns.Add("取樣率", 90);
            mList.Columns.Add("聲道", 70);
            mList.Columns.Add("位元", 70);
            mList.Columns.Add("大小", 85);
            mList.Columns.Add("路徑", 420);
            mList.DoubleClick += delegate { PlaySelectedOrCurrent(); };
            mList.SelectedIndexChanged += delegate { UpdateSelectedInfo(); };
            mList.DrawColumnHeader += List_DrawColumnHeader;
            mList.DrawSubItem += List_DrawSubItem;
            listLayout.Controls.Add(mList, 0, 1);

            BuildContextMenu();

            FlowLayoutPanel listButtons = new FlowLayoutPanel();
            listButtons.Dock = DockStyle.Fill;
            listButtons.Padding = new Padding(0, 10, 0, 0);
            listButtons.WrapContents = false;

            mBtnRemove = MakeButton("移除選取", false);
            mBtnClear = MakeButton("清空清單", false);
            mBtnSaveList = MakeButton("儲存 M3U", false);

            ModernButton btnToolbox = MakeButton("音訊工具箱", false);
            ModernButton btnDashboard = MakeButton("統計頁", false);

            mBtnRemove.Width = 95;
            mBtnClear.Width = 95;
            mBtnSaveList.Width = 95;
            btnToolbox.Width = 115;
            btnDashboard.Width = 85;

            mBtnRemove.Click += delegate { RemoveSelected(); };
            mBtnClear.Click += delegate { ClearPlaylist(); };
            mBtnSaveList.Click += delegate { SavePlaylist(); };
            btnToolbox.Click += delegate { ShowAudioToolboxPage(); };
            btnDashboard.Click += delegate { ShowDashboardPage(); };

            listButtons.Controls.Add(mBtnRemove);
            listButtons.Controls.Add(mBtnClear);
            listButtons.Controls.Add(mBtnSaveList);
            listButtons.Controls.Add(btnToolbox);
            listButtons.Controls.Add(btnDashboard);
            listLayout.Controls.Add(listButtons, 0, 2);

            CardPanel playerCard = new CardPanel();
            playerCard.Dock = DockStyle.Fill;
            playerCard.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(playerCard, 0, 2);
            root.SetColumnSpan(playerCard, 2);

            TableLayoutPanel player = new TableLayoutPanel();
            player.Dock = DockStyle.Fill;
            player.Padding = new Padding(18);
            player.ColumnCount = 3;
            player.RowCount = 5;
            player.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            player.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            player.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            player.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            player.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            player.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            player.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            player.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            playerCard.Controls.Add(player);

            mLblNow = MakeLabel("尚未播放", 13, FontStyle.Bold, AppColor.Text);
            mLblNow.Dock = DockStyle.Fill;
            player.Controls.Add(mLblNow, 0, 0);
            player.SetColumnSpan(mLblNow, 3);

            mWaveform = new WaveformView();
            mWaveform.Dock = DockStyle.Fill;
            mWaveform.Margin = new Padding(0, 4, 16, 0);
            mWaveform.SeekRequested += delegate (object sender, SeekEventArgs e)
            {
                SeekTo(e.PositionMs);
            };
            player.Controls.Add(mWaveform, 0, 1);

            mVisualizer = new VisualizerView();
            mVisualizer.Dock = DockStyle.Fill;
            mVisualizer.Margin = new Padding(0, 4, 16, 0);
            player.Controls.Add(mVisualizer, 1, 1);

            mLblSelectedInfo = MakeLabel("檔案資訊：尚未選取檔案", 9, FontStyle.Regular, AppColor.SubText);
            mLblSelectedInfo.Dock = DockStyle.Fill;
            mLblSelectedInfo.TextAlign = ContentAlignment.MiddleLeft;
            player.Controls.Add(mLblSelectedInfo, 2, 1);

            mTrkProgress = new TrackBar();
            mTrkProgress.Dock = DockStyle.Fill;
            mTrkProgress.Minimum = 0;
            mTrkProgress.Maximum = 10000;
            mTrkProgress.TickFrequency = 1000;
            mTrkProgress.BackColor = AppColor.Card;
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
            player.Controls.Add(mTrkProgress, 0, 2);
            player.SetColumnSpan(mTrkProgress, 2);

            mLblTime = MakeLabel("00:00 / 00:00", 10, FontStyle.Bold, AppColor.Text);
            mLblTime.Dock = DockStyle.Fill;
            mLblTime.TextAlign = ContentAlignment.MiddleRight;
            player.Controls.Add(mLblTime, 2, 2);

            FlowLayoutPanel controlBar = new FlowLayoutPanel();
            controlBar.Dock = DockStyle.Fill;
            controlBar.WrapContents = false;
            controlBar.Padding = new Padding(0, 2, 0, 0);

            mBtnPrev = MakeButton("⏮", false);
            mBtnPlay = MakeButton("▶ 播放", true);
            mBtnPause = MakeButton("⏸", false);
            mBtnStop = MakeButton("⏹", false);
            mBtnNext = MakeButton("⏭", false);
            mBtnMute = MakeButton("🔊", false);

            mBtnPrev.Width = 58;
            mBtnPlay.Width = 105;
            mBtnPause.Width = 58;
            mBtnStop.Width = 58;
            mBtnNext.Width = 58;
            mBtnMute.Width = 58;

            mBtnPrev.Click += delegate { PlayPrevious(); };
            mBtnPlay.Click += delegate { PlaySelectedOrCurrent(); };
            mBtnPause.Click += delegate { TogglePause(); };
            mBtnStop.Click += delegate { StopPlayback(); };
            mBtnNext.Click += delegate { PlayNext(); };
            mBtnMute.Click += delegate { ToggleMute(); };

            controlBar.Controls.Add(mBtnPrev);
            controlBar.Controls.Add(mBtnPlay);
            controlBar.Controls.Add(mBtnPause);
            controlBar.Controls.Add(mBtnStop);
            controlBar.Controls.Add(mBtnNext);
            controlBar.Controls.Add(mBtnMute);

            player.Controls.Add(controlBar, 0, 3);

            FlowLayoutPanel abBar = new FlowLayoutPanel();
            abBar.Dock = DockStyle.Fill;
            abBar.WrapContents = false;
            abBar.Padding = new Padding(0, 2, 0, 0);

            mBtnSetA = MakeButton("設 A 點", false);
            mBtnSetB = MakeButton("設 B 點", false);
            mBtnClearAB = MakeButton("清除 AB", false);
            mBtnSetA.Width = 82;
            mBtnSetB.Width = 82;
            mBtnClearAB.Width = 90;

            mBtnSetA.Click += delegate { SetLoopA(); };
            mBtnSetB.Click += delegate { SetLoopB(); };
            mBtnClearAB.Click += delegate { ClearABLoop(); };

            mLblAB = MakeLabel("AB：未設定", 9, FontStyle.Regular, AppColor.SubText);
            mLblAB.Width = 250;
            mLblAB.TextAlign = ContentAlignment.MiddleLeft;

            abBar.Controls.Add(mBtnSetA);
            abBar.Controls.Add(mBtnSetB);
            abBar.Controls.Add(mBtnClearAB);
            abBar.Controls.Add(mLblAB);
            player.Controls.Add(abBar, 1, 3);
            player.SetColumnSpan(abBar, 2);

            FlowLayoutPanel sliders = new FlowLayoutPanel();
            sliders.Dock = DockStyle.Fill;
            sliders.WrapContents = false;
            sliders.Padding = new Padding(0, 4, 0, 0);

            mLblVolume = MakeLabel("音量 80%", 9, FontStyle.Regular, AppColor.SubText);
            mLblVolume.Width = 78;
            mLblVolume.TextAlign = ContentAlignment.MiddleLeft;

            mTrkVolume = new TrackBar();
            mTrkVolume.Minimum = 0;
            mTrkVolume.Maximum = 1000;
            mTrkVolume.Value = 800;
            mTrkVolume.Width = 170;
            mTrkVolume.TickFrequency = 100;
            mTrkVolume.BackColor = AppColor.Card;
            mTrkVolume.ValueChanged += delegate
            {
                mLblVolume.Text = "音量 " + (mTrkVolume.Value / 10) + "%";
                if (!_muted)
                    ApplyVolume();
            };

            mLblSpeed = MakeLabel("倍速 100%", 9, FontStyle.Regular, AppColor.SubText);
            mLblSpeed.Width = 88;
            mLblSpeed.TextAlign = ContentAlignment.MiddleLeft;
            mLblSpeed.Margin = new Padding(20, 0, 0, 0);

            mTrkSpeed = new TrackBar();
            mTrkSpeed.Minimum = 50;
            mTrkSpeed.Maximum = 200;
            mTrkSpeed.Value = 100;
            mTrkSpeed.Width = 190;
            mTrkSpeed.TickFrequency = 25;
            mTrkSpeed.BackColor = AppColor.Card;
            mTrkSpeed.ValueChanged += delegate
            {
                mLblSpeed.Text = "倍速 " + mTrkSpeed.Value + "%";
                ApplySpeed();
            };

            mTxtPath = new TextBox();
            mTxtPath.ReadOnly = true;
            mTxtPath.BorderStyle = BorderStyle.FixedSingle;
            mTxtPath.BackColor = AppColor.Input;
            mTxtPath.ForeColor = AppColor.SubText;
            mTxtPath.Width = 455;
            mTxtPath.Margin = new Padding(20, 5, 0, 0);

            sliders.Controls.Add(mLblVolume);
            sliders.Controls.Add(mTrkVolume);
            sliders.Controls.Add(mLblSpeed);
            sliders.Controls.Add(mTrkSpeed);
            sliders.Controls.Add(mTxtPath);

            player.Controls.Add(sliders, 0, 4);
            player.SetColumnSpan(sliders, 3);

            mLblStatus = MakeLabel("就緒。可拖曳 WAV / MP3 / MP4 檔或資料夾進來。", 9, FontStyle.Regular, AppColor.SubText);
            mLblStatus.Dock = DockStyle.Fill;
            mLblStatus.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(mLblStatus, 0, 3);
            root.SetColumnSpan(mLblStatus, 2);

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(mBtnPrev, "上一首");
            toolTip.SetToolTip(mBtnPlay, "播放");
            toolTip.SetToolTip(mBtnPause, "暫停 / 繼續");
            toolTip.SetToolTip(mBtnStop, "停止");
            toolTip.SetToolTip(mBtnNext, "下一首");
            toolTip.SetToolTip(mBtnMute, "切換靜音");

            mTimer = new Timer();
            mTimer.Interval = 160;
            mTimer.Tick += UiTimer_Tick;
            mTimer.Start();

            ResumeLayout();
        }

        private ModernButton MakeButton(string text, bool primary)
        {
            ModernButton btn = new ModernButton();
            btn.Text = text;
            btn.Width = 145;
            btn.Height = 34;
            btn.Margin = new Padding(0, 4, 8, 4);
            btn.Primary = primary;
            btn.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);
            return btn;
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Font = new Font("Microsoft JhengHei UI", size, style);
            label.AutoEllipsis = true;
            return label;
        }

        private void BuildContextMenu()
        {
            mMenu = new ContextMenuStrip();
            mMenu.BackColor = AppColor.Card2;
            mMenu.ForeColor = AppColor.Text;
            mMenu.RenderMode = ToolStripRenderMode.System;

            ToolStripMenuItem play = new ToolStripMenuItem("播放");
            ToolStripMenuItem fav = new ToolStripMenuItem("切換收藏");
            ToolStripMenuItem openFolder = new ToolStripMenuItem("在檔案總管中顯示");
            ToolStripMenuItem copyPath = new ToolStripMenuItem("複製路徑");
            ToolStripMenuItem remove = new ToolStripMenuItem("移除");

            play.Click += delegate { PlaySelectedOrCurrent(); };
            fav.Click += delegate { ToggleSelectedFavorite(); };
            openFolder.Click += delegate { OpenSelectedInExplorer(); };
            copyPath.Click += delegate { CopySelectedPath(); };
            remove.Click += delegate { RemoveSelected(); };

            mMenu.Items.Add(play);
            mMenu.Items.Add(fav);
            mMenu.Items.Add(new ToolStripSeparator());
            mMenu.Items.Add(openFolder);
            mMenu.Items.Add(copyPath);
            mMenu.Items.Add(new ToolStripSeparator());
            mMenu.Items.Add(remove);

            mList.ContextMenuStrip = mMenu;
        }

        private void AddFilesFromDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "選擇 WAV / MP3 / MP4 檔案";
                ofd.Filter = SupportedAudioFilter;
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                    AddFilesToPlaylist(ofd.FileNames, true);
            }
        }

        private void AddFolderFromDialog()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "選擇包含 WAV / MP3 / MP4 的資料夾，會自動掃描子資料夾";

                if (fbd.ShowDialog() == DialogResult.OK)
                    AddFilesToPlaylist(new string[] { fbd.SelectedPath }, true);
            }
        }

        private void AddFilesToPlaylist(IEnumerable<string> paths, bool showMessage)
        {
            int added = 0;
            int skipped = 0;

            List<string> expanded = new List<string>();

            foreach (string raw in paths)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                if (Directory.Exists(raw))
                    expanded.AddRange(SafeEnumerateAudioFiles(raw));
                else
                    expanded.Add(raw);
            }

            foreach (string rawPath in expanded)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                if (!File.Exists(rawPath))
                {
                    skipped++;
                    continue;
                }

                if (!IsSupportedAudioFile(rawPath))
                {
                    skipped++;
                    continue;
                }

                string full = Path.GetFullPath(rawPath);

                if (_tracks.Any(t => string.Equals(t.Path, full, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    TrackItem item = TrackItem.FromFile(full);
                    item.IsFavorite = _favoritePaths.Contains(full);
                    _tracks.Add(item);
                    added++;
                }
                catch
                {
                    skipped++;
                }
            }

            RefreshList();
            UpdateStats();

            if (_tracks.Count > 0 && mList.SelectedItems.Count == 0)
                SelectTrack(_tracks[0]);

            if (showMessage)
                SetStatus("已加入 " + added + " 個音訊/影片檔案，略過 " + skipped + " 個項目。");
        }

        private IEnumerable<string> SafeEnumerateAudioFiles(string folder)
        {
            string[] files = new string[0];
            string[] dirs = new string[0];

            try
            {
                files = Directory.GetFiles(folder, "*.*");
            }
            catch
            {
            }

            foreach (string file in files)
            {
                if (IsSupportedAudioFile(file))
                    yield return file;
            }

            try
            {
                dirs = Directory.GetDirectories(folder);
            }
            catch
            {
            }

            foreach (string dir in dirs)
            {
                foreach (string file in SafeEnumerateAudioFiles(dir))
                    yield return file;
            }
        }

        private void RefreshList()
        {
            if (mList == null)
                return;

            TrackItem selected = GetSelectedTrack();

            mList.BeginUpdate();
            mList.Items.Clear();

            string keyword = mTxtSearch == null ? "" : mTxtSearch.Text.Trim();

            foreach (TrackItem track in _tracks)
            {
                if (mChkOnlyFavorite != null && mChkOnlyFavorite.Checked && !track.IsFavorite)
                    continue;

                if (keyword.Length > 0)
                {
                    string hay = track.FileName + " " + track.Path + " " + track.FormatName;
                    if (hay.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                string flag = "";

                if (_currentTrack != null && string.Equals(_currentTrack.Path, track.Path, StringComparison.OrdinalIgnoreCase))
                    flag = "▶";
                else if (track.IsFavorite)
                    flag = "★";

                ListViewItem item = new ListViewItem(flag);
                item.Tag = track;
                item.SubItems.Add(track.FileName);
                item.SubItems.Add(FormatTime(track.DurationMs));
                item.SubItems.Add(track.FormatName);
                item.SubItems.Add(track.SampleRateText);
                item.SubItems.Add(track.ChannelsText);
                item.SubItems.Add(track.BitsText);
                item.SubItems.Add(FormatBytes(track.FileSize));
                item.SubItems.Add(track.Path);

                mList.Items.Add(item);

                if (selected != null && string.Equals(selected.Path, track.Path, StringComparison.OrdinalIgnoreCase))
                    item.Selected = true;
            }

            mList.EndUpdate();
        }

        private void SelectTrack(TrackItem track)
        {
            if (track == null)
                return;

            foreach (ListViewItem item in mList.Items)
            {
                TrackItem t = item.Tag as TrackItem;

                if (t != null && string.Equals(t.Path, track.Path, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private TrackItem GetSelectedTrack()
        {
            if (mList == null || mList.SelectedItems.Count == 0)
                return null;

            return mList.SelectedItems[0].Tag as TrackItem;
        }

        private void PlaySelectedOrCurrent()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected != null)
            {
                PlayTrack(selected);
                return;
            }

            if (_currentTrack != null)
            {
                PlayTrack(_currentTrack);
                return;
            }

            if (_tracks.Count > 0)
            {
                PlayTrack(_tracks[0]);
                return;
            }

            SetStatus("請先加入 WAV / MP3 / MP4 檔案。");
        }

        private void PlayTrack(TrackItem track)
        {
            if (track == null)
                return;

            if (!File.Exists(track.Path))
            {
                MessageBox.Show("找不到檔案：\n" + track.Path, "檔案不存在", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CloseMci();

                if (ShouldUseWmpPlayback(track.Path))
                    OpenWmpFile(track.Path);
                else
                    OpenMciFile(track.Path);

                long playerLength = GetLengthSafe();
                _durationMs = playerLength > 0 ? playerLength : track.DurationMs;

                if (track.DurationMs <= 0 && _durationMs > 0)
                    track.DurationMs = _durationMs;

                ApplyVolume();
                ApplySpeed();

                if (_useWmp)
                    WmpPlay();
                else
                    RunMci("play " + MciAlias, true);

                _currentTrack = track;
                _playRequested = true;
                _lastPositionMs = 0;
                _loopA = -1;
                _loopB = -1;

                mTxtPath.Text = track.Path;
                mLblNow.Text = "正在播放：" + track.FileName;
                mBtnPlay.Text = "▶ 播放";
                mBtnPause.Text = "⏸";
                mLblAB.Text = "AB：未設定";

                UpdateTimeDisplay(0, _durationMs);
                SelectTrack(track);
                RefreshList();
                LoadWaveformAsync(track);
                SetStatus("播放中：" + track.FileName);
            }
            catch (Exception ex)
            {
                _playRequested = false;
                CloseMci();
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
                if (_useWmp)
                    WmpPause();
                else
                    RunMci("pause " + MciAlias, false);

                _playRequested = false;
                mBtnPause.Text = "▶";
                SetStatus("已暫停。");
            }
            else
            {
                if (_useWmp)
                {
                    WmpPlay();
                }
                else
                {
                    bool ok = RunMci("resume " + MciAlias, false);

                    if (!ok)
                        RunMci("play " + MciAlias, false);
                }

                _playRequested = true;
                mBtnPause.Text = "⏸";
                SetStatus("繼續播放。");
            }
        }

        private void StopPlayback()
        {
            if (_mciOpen)
            {
                if (_useWmp)
                {
                    WmpStop();
                    WmpSeekTo(0);
                }
                else
                {
                    RunMci("stop " + MciAlias, false);
                    RunMci("seek " + MciAlias + " to start", false);
                }
            }

            _playRequested = false;
            _lastPositionMs = 0;

            if (mTrkProgress != null)
                mTrkProgress.Value = 0;

            if (mWaveform != null)
                mWaveform.PositionMs = 0;

            if (mVisualizer != null)
                mVisualizer.Level = 0;

            UpdateTimeDisplay(0, _durationMs);
            SetStatus("已停止。");
        }

        private void PlayNext()
        {
            TrackItem next = GetNextTrack();

            if (next != null)
                PlayTrack(next);
            else
                SetStatus("已經是最後一首。");
        }

        private void PlayPrevious()
        {
            if (_tracks.Count == 0)
                return;

            long pos = GetPositionSafe();

            if (pos > 3000)
            {
                SeekTo(0);
                return;
            }

            int index = _currentTrack == null ? -1 : _tracks.IndexOf(_currentTrack);
            int prevIndex = index - 1;

            if (prevIndex < 0)
            {
                if (GetLoopMode() == 2)
                    prevIndex = _tracks.Count - 1;
                else
                {
                    SetStatus("已經是第一首。");
                    return;
                }
            }

            PlayTrack(_tracks[prevIndex]);
        }

        private TrackItem GetNextTrack()
        {
            if (_tracks.Count == 0)
                return null;

            if (mChkShuffle != null && mChkShuffle.Checked && _tracks.Count > 1)
            {
                TrackItem picked;

                do
                {
                    picked = _tracks[_random.Next(_tracks.Count)];
                }
                while (_currentTrack != null && string.Equals(picked.Path, _currentTrack.Path, StringComparison.OrdinalIgnoreCase));

                return picked;
            }

            int index = _currentTrack == null ? -1 : _tracks.IndexOf(_currentTrack);
            int nextIndex = index + 1;

            if (nextIndex >= 0 && nextIndex < _tracks.Count)
                return _tracks[nextIndex];

            if (GetLoopMode() == 2)
                return _tracks[0];

            return null;
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

            if (GetLoopMode() == 1 && _currentTrack != null)
            {
                PlayTrack(_currentTrack);
                return;
            }

            TrackItem next = GetNextTrack();

            if (next != null)
            {
                PlayTrack(next);
            }
            else
            {
                StopPlayback();
                SetStatus("清單播放完畢。");
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

            if (_useWmp)
            {
                WmpSeekTo(targetMs);

                if (_playRequested || GetModeSafe() == "playing")
                    WmpPlay();
            }
            else
            {
                RunMci("seek " + MciAlias + " to " + targetMs, false);

                if (_playRequested || GetModeSafe() == "playing")
                    RunMci("play " + MciAlias, false);
            }

            _lastPositionMs = targetMs;
            UpdateTimeDisplay(targetMs, _durationMs);

            if (mWaveform != null)
                mWaveform.PositionMs = targetMs;
        }

        private void SeekRelative(long offsetMs)
        {
            if (!_mciOpen)
                return;

            long pos = GetPositionSafe();
            SeekTo(pos + offsetMs);
        }

        private void SetLoopA()
        {
            if (!_mciOpen)
            {
                SetStatus("請先播放檔案再設定 A 點。");
                return;
            }

            _loopA = GetPositionSafe();

            if (_loopB >= 0 && _loopB <= _loopA)
                _loopB = -1;

            UpdateABLabel();
            SetStatus("已設定 A 點：" + FormatTime(_loopA));
        }

        private void SetLoopB()
        {
            if (!_mciOpen)
            {
                SetStatus("請先播放檔案再設定 B 點。");
                return;
            }

            long now = GetPositionSafe();

            if (_loopA < 0)
            {
                SetStatus("請先設定 A 點。");
                return;
            }

            if (now <= _loopA + 300)
            {
                SetStatus("B 點必須晚於 A 點。");
                return;
            }

            _loopB = now;
            UpdateABLabel();
            SetStatus("已設定 B 點：" + FormatTime(_loopB));
        }

        private void ClearABLoop()
        {
            _loopA = -1;
            _loopB = -1;
            UpdateABLabel();
            SetStatus("已清除 AB 循環。");
        }

        private void UpdateABLabel()
        {
            if (_loopA >= 0 && _loopB >= 0)
                mLblAB.Text = "AB：" + FormatTime(_loopA) + " → " + FormatTime(_loopB);
            else if (_loopA >= 0)
                mLblAB.Text = "AB：A=" + FormatTime(_loopA) + "，B 未設定";
            else
                mLblAB.Text = "AB：未設定";
        }

        private void ToggleMute()
        {
            if (_muted)
            {
                _muted = false;
                mBtnMute.Text = "🔊";
                mTrkVolume.Value = Math.Max(0, Math.Min(1000, _volumeBeforeMute));
                ApplyVolume();
                SetStatus("已取消靜音。");
            }
            else
            {
                _muted = true;
                _volumeBeforeMute = mTrkVolume.Value;
                mBtnMute.Text = "🔇";
                ApplyVolume();
                SetStatus("已靜音。");
            }
        }

        private void ApplyVolume()
        {
            if (!_mciOpen)
                return;

            int volume = _muted ? 0 : mTrkVolume.Value;

            if (_useWmp)
            {
                WmpSetVolume(volume);
                return;
            }

            RunMci("setaudio " + MciAlias + " volume to " + volume, false);
        }

        private void ApplySpeed()
        {
            if (!_mciOpen || mTrkSpeed == null)
                return;

            if (_useWmp)
            {
                WmpSetRate(mTrkSpeed.Value / 100.0);
                return;
            }

            int speed = mTrkSpeed.Value * 10;
            RunMci("set " + MciAlias + " speed " + speed, false);
        }

        private void RemoveSelected()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected == null)
                return;

            bool wasCurrent = _currentTrack != null &&
                              string.Equals(_currentTrack.Path, selected.Path, StringComparison.OrdinalIgnoreCase);

            if (wasCurrent)
            {
                CloseMci();
                _currentTrack = null;
            }

            _tracks.Remove(selected);

            RefreshList();
            UpdateStats();

            if (_tracks.Count == 0)
            {
                mLblNow.Text = "尚未播放";
                mLblSelectedInfo.Text = "檔案資訊：尚未選取檔案";
                mTxtPath.Clear();
                UpdateTimeDisplay(0, 0);
                mWaveform.SetPeaks(null, 0);
            }

            SetStatus("已移除選取項目。");
        }

        private void ClearPlaylist()
        {
            DialogResult result = MessageBox.Show(
                "確定要清空整個播放清單嗎？",
                "清空確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.No)
                return;

            CloseMci();

            _tracks.Clear();
            _currentTrack = null;
            _durationMs = 0;
            _lastPositionMs = 0;

            mTxtPath.Clear();
            mLblNow.Text = "尚未播放";
            mLblSelectedInfo.Text = "檔案資訊：尚未選取檔案";
            mWaveform.SetPeaks(null, 0);
            mVisualizer.Level = 0;

            RefreshList();
            UpdateStats();
            UpdateTimeDisplay(0, 0);
            SetStatus("播放清單已清空。");
        }

        private void CleanMissingFiles()
        {
            int before = _tracks.Count;

            if (_currentTrack != null && !File.Exists(_currentTrack.Path))
            {
                CloseMci();
                _currentTrack = null;
            }

            _tracks.RemoveAll(t => !File.Exists(t.Path));

            int removed = before - _tracks.Count;

            RefreshList();
            UpdateStats();
            SetStatus("已清除 " + removed + " 個失效檔案。");
        }

        private void ToggleSelectedFavorite()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected == null)
                return;

            selected.IsFavorite = !selected.IsFavorite;

            if (selected.IsFavorite)
                _favoritePaths.Add(selected.Path);
            else
                _favoritePaths.Remove(selected.Path);

            RefreshList();
            UpdateStats();

            SetStatus(selected.IsFavorite ? "已加入收藏。" : "已取消收藏。");
        }

        private void OpenSelectedInExplorer()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected == null)
                return;

            if (!File.Exists(selected.Path))
            {
                SetStatus("檔案不存在。");
                return;
            }

            Process.Start("explorer.exe", "/select,\"" + selected.Path + "\"");
        }

        private void CopySelectedPath()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected == null)
                return;

            Clipboard.SetText(selected.Path);
            SetStatus("已複製路徑。");
        }

        private void SavePlaylist()
        {
            if (_tracks.Count == 0)
            {
                SetStatus("播放清單是空的，無法儲存。");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "儲存播放清單";
                sfd.Filter = "M3U 播放清單 (*.m3u)|*.m3u|文字檔 (*.txt)|*.txt";
                sfd.FileName = "音訊播放器_Playlist.m3u";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    List<string> lines = new List<string>();
                    lines.Add("#EXTM3U");

                    foreach (TrackItem track in _tracks)
                        lines.Add(track.Path);

                    File.WriteAllLines(sfd.FileName, lines.ToArray(), Encoding.UTF8);
                    SetStatus("播放清單已儲存。");
                }
            }
        }

        private void LoadPlaylistFromDialog()
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

                    AddFilesToPlaylist(files, true);
                    SetStatus("播放清單已載入。");
                }
            }
        }

        private void ChooseWatchFolder()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "選擇要監看的資料夾，新出現的 WAV / MP3 / MP4 會自動加入播放清單";

                if (fbd.ShowDialog() == DialogResult.OK)
                    StartWatchingFolder(fbd.SelectedPath);
            }
        }

        private void StartWatchingFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            StopWatchingFolder(false);

            _watcher = new FileSystemWatcher(folder, "*.*");
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
            _watcher.Created += Watcher_FileAppeared;
            _watcher.Renamed += Watcher_Renamed;
            _watcher.EnableRaisingEvents = true;

            mLblWatch.Text = "監看：" + folder;
            SetStatus("已開始監看資料夾：" + folder);
        }

        private void StopWatchingFolder()
        {
            StopWatchingFolder(true);
        }

        private void StopWatchingFolder(bool showStatus)
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
                catch
                {
                }

                _watcher = null;
            }

            if (mLblWatch != null)
                mLblWatch.Text = "監看：未啟用";

            if (showStatus)
                SetStatus("已停止監看資料夾。");
        }

        private void Watcher_FileAppeared(object sender, FileSystemEventArgs e)
        {
            AddWatchedFileLater(e.FullPath);
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            AddWatchedFileLater(e.FullPath);
        }

        private void AddWatchedFileLater(string path)
        {
            if (!IsSupportedAudioFile(path))
                return;

            Task.Factory.StartNew(delegate
            {
                System.Threading.Thread.Sleep(800);

                if (IsDisposed)
                    return;

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        AddFilesToPlaylist(new string[] { path }, false);
                        SetStatus("監看資料夾偵測到新檔案：" + Path.GetFileName(path));
                    }));
                }
                catch
                {
                }
            });
        }

        private void UpdateSelectedInfo()
        {
            TrackItem selected = GetSelectedTrack();

            if (selected == null)
                return;

            mTxtPath.Text = selected.Path;
            mLblSelectedInfo.Text = selected.FullDescription;

            if (_currentTrack == null || !string.Equals(_currentTrack.Path, selected.Path, StringComparison.OrdinalIgnoreCase))
                mLblNow.Text = "選取：" + selected.FileName;
        }

        private void UpdateStats()
        {
            if (mLblStats == null)
                return;

            long totalMs = 0;
            int favoriteCount = 0;

            foreach (TrackItem track in _tracks)
            {
                totalMs += track.DurationMs;

                if (track.IsFavorite)
                    favoriteCount++;
            }

            mLblStats.Text = _tracks.Count + " 首｜" + FormatTime(totalMs) + "｜收藏 " + favoriteCount;
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (!_mciOpen)
            {
                if (mVisualizer != null)
                    mVisualizer.Level = 0;

                return;
            }

            long pos = GetPositionSafe();

            if (_durationMs <= 0)
                _durationMs = GetLengthSafe();

            if (_currentTrack != null && _currentTrack.DurationMs <= 0 && _durationMs > 0)
            {
                _currentTrack.DurationMs = _durationMs;
                RefreshList();
                UpdateStats();
            }

            if (_loopA >= 0 && _loopB > _loopA && pos >= _loopB - 80)
            {
                SeekTo(_loopA);
                return;
            }

            if (!_isSeeking && _durationMs > 0)
            {
                int value = MsToProgressValue(pos);
                value = Math.Max(mTrkProgress.Minimum, Math.Min(mTrkProgress.Maximum, value));
                mTrkProgress.Value = value;
            }

            if (mWaveform != null)
                mWaveform.PositionMs = pos;

            UpdateTimeDisplay(pos, _durationMs);

            string mode = GetModeSafe();

            if (mode == "playing")
            {
                if (mBtnPause.Text != "⏸")
                    mBtnPause.Text = "⏸";
            }
            else if (mode == "paused")
            {
                if (mBtnPause.Text != "▶")
                    mBtnPause.Text = "▶";
            }

            if (mVisualizer != null)
            {
                float level = 0;

                if (mode == "playing")
                    level = mWaveform == null ? 0.4f : mWaveform.GetPeakAt(pos);

                mVisualizer.Level = level;
            }

            bool reachedEnd = _durationMs > 0 &&
                              (pos >= _durationMs - 420 || _lastPositionMs >= _durationMs - 420);

            if (_useWmp && _playRequested && GetWmpPlayState() == 8)
            {
                _lastPositionMs = 0;
                HandleTrackEnded();
                return;
            }

            if (_playRequested && mode == "stopped" && reachedEnd)
            {
                _lastPositionMs = 0;
                HandleTrackEnded();
                return;
            }

            _lastPositionMs = pos;
        }

        private void LoadWaveformAsync(TrackItem track)
        {
            if (track == null || mWaveform == null)
                return;

            int stamp = ++_waveformLoadStamp;
            mWaveform.SetPeaks(null, track.DurationMs);

            Task.Factory.StartNew(delegate
            {
                float[] peaks = WaveformAnalyzer.BuildPeaks(track.Path, 900);

                if (IsDisposed)
                    return;

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (stamp == _waveformLoadStamp)
                            mWaveform.SetPeaks(peaks, track.DurationMs);
                    }));
                }
                catch
                {
                }
            });
        }

        private long GetLengthSafe()
        {
            if (_useWmp)
                return GetWmpDurationMs();

            long value;

            if (long.TryParse(QueryMci("status " + MciAlias + " length"), out value))
                return value;

            return 0;
        }

        private long GetPositionSafe()
        {
            if (_useWmp)
            {
                if (GetWmpPlayState() == 8 && _durationMs > 0)
                    return _durationMs;

                return GetWmpPositionMs();
            }

            long value;

            if (long.TryParse(QueryMci("status " + MciAlias + " position"), out value))
                return value;

            return 0;
        }

        private string GetModeSafe()
        {
            if (_useWmp)
            {
                int state = GetWmpPlayState();

                if (state == 3 || state == 6 || state == 9)
                    return "playing";

                if (state == 2)
                    return "paused";

                if (state == 1 || state == 8 || state == 10)
                    return "stopped";

                return "";
            }

            string mode = QueryMci("status " + MciAlias + " mode");

            if (string.IsNullOrWhiteSpace(mode))
                return "";

            return mode.Trim().ToLower();
        }

        private void CloseMci()
        {
            if (_mciOpen)
            {
                if (_useWmp || _wmpOpen)
                {
                    WmpStop();
                    WmpSetUrl("");
                }
                else
                {
                    RunMci("stop " + MciAlias, false);
                    RunMci("close " + MciAlias, false);
                }
            }

            _mciOpen = false;
            _useWmp = false;
            _wmpOpen = false;
            _playRequested = false;
            _durationMs = 0;
            _lastPositionMs = 0;
        }

        private static bool ShouldUseWmpPlayback(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp3" || ext == ".mp4";
        }

        private void OpenMciFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            List<string> errors = new List<string>();
            bool opened = false;

            if (ext == ".wav")
            {
                opened = TryRunMci("open " + Quote(path) + " type waveaudio alias " + MciAlias, errors);

                if (!opened)
                    opened = TryRunMci("open " + Quote(path) + " alias " + MciAlias, errors);
            }
            else
            {
                opened = TryRunMci("open " + Quote(path) + " type mpegvideo alias " + MciAlias, errors);

                if (!opened)
                    opened = TryRunMci("open " + Quote(path) + " alias " + MciAlias, errors);
            }

            if (!opened)
            {
                string detail = errors.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, errors.ToArray()) : "未知 MCI 錯誤。";
                throw new InvalidOperationException("MCI 無法開啟這個檔案。" + Environment.NewLine + detail);
            }

            _useWmp = false;
            _wmpOpen = false;
            _mciOpen = true;
            RunMci("set " + MciAlias + " time format milliseconds", false);
        }

        private void OpenWmpFile(string path)
        {
            EnsureWmpPlayer();
            WmpSetUrl(path);
            _useWmp = true;
            _wmpOpen = true;
            _mciOpen = true;
        }

        private object EnsureWmpPlayer()
        {
            if (_wmpPlayer != null)
                return _wmpPlayer;

            Type type = Type.GetTypeFromProgID("WMPlayer.OCX");

            if (type == null)
                throw new InvalidOperationException("找不到 Windows Media Player 元件。請確認 Windows 已啟用 Windows Media Player / Media Features。");

            _wmpPlayer = Activator.CreateInstance(type);
            return _wmpPlayer;
        }

        private void WmpSetUrl(string path)
        {
            if (_wmpPlayer == null)
                return;

            try
            {
                ComSet(_wmpPlayer, "URL", path);
            }
            catch
            {
            }
        }

        private object WmpControls()
        {
            return _wmpPlayer == null ? null : ComGet(_wmpPlayer, "controls");
        }

        private object WmpSettings()
        {
            return _wmpPlayer == null ? null : ComGet(_wmpPlayer, "settings");
        }

        private void WmpPlay()
        {
            try
            {
                object controls = WmpControls();

                if (controls != null)
                    ComCall(controls, "play");
            }
            catch
            {
            }
        }

        private void WmpPause()
        {
            try
            {
                object controls = WmpControls();

                if (controls != null)
                    ComCall(controls, "pause");
            }
            catch
            {
            }
        }

        private void WmpStop()
        {
            try
            {
                object controls = WmpControls();

                if (controls != null)
                    ComCall(controls, "stop");
            }
            catch
            {
            }
        }

        private void WmpSeekTo(long positionMs)
        {
            try
            {
                object controls = WmpControls();

                if (controls != null)
                    ComSet(controls, "currentPosition", positionMs / 1000.0);
            }
            catch
            {
            }
        }

        private long GetWmpPositionMs()
        {
            try
            {
                object controls = WmpControls();

                if (controls == null)
                    return 0;

                object value = ComGet(controls, "currentPosition");
                return (long)(Convert.ToDouble(value) * 1000.0);
            }
            catch
            {
                return 0;
            }
        }

        private long GetWmpDurationMs()
        {
            try
            {
                if (_wmpPlayer == null)
                    return 0;

                object media = ComGet(_wmpPlayer, "currentMedia");

                if (media == null)
                    return 0;

                object value = ComGet(media, "duration");
                return (long)(Convert.ToDouble(value) * 1000.0);
            }
            catch
            {
                return 0;
            }
        }

        private int GetWmpPlayState()
        {
            try
            {
                if (_wmpPlayer == null)
                    return 0;

                object value = ComGet(_wmpPlayer, "playState");
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private void WmpSetVolume(int mciVolume)
        {
            try
            {
                object settings = WmpSettings();

                if (settings == null)
                    return;

                int volume = Math.Max(0, Math.Min(100, mciVolume / 10));
                ComSet(settings, "volume", volume);
                ComSet(settings, "mute", _muted);
            }
            catch
            {
            }
        }

        private void WmpSetRate(double rate)
        {
            try
            {
                object settings = WmpSettings();

                if (settings != null)
                    ComSet(settings, "rate", rate);
            }
            catch
            {
            }
        }

        private static object ComGet(object target, string name)
        {
            return target.GetType().InvokeMember(name, System.Reflection.BindingFlags.GetProperty, null, target, null);
        }

        private static void ComSet(object target, string name, object value)
        {
            target.GetType().InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, target, new object[] { value });
        }

        private static object ComCall(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(name, System.Reflection.BindingFlags.InvokeMethod, null, target, args);
        }

        private static bool TryRunMci(string command, List<string> errors)
        {
            int error = mciSendString(command, null, 0, IntPtr.Zero);

            if (error != 0 && errors != null)
                errors.Add(GetMciError(error) + Environment.NewLine + "指令：" + command);

            return error == 0;
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
            StringBuilder buffer = new StringBuilder(512);
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

        private static bool IsSupportedAudioFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string ext = Path.GetExtension(path);

            if (string.IsNullOrWhiteSpace(ext))
                return false;

            foreach (string supported in SupportedAudioExtensions)
            {
                if (string.Equals(ext, supported, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
            {
                string newText = FormatTime(posMs) + " / " + FormatTime(totalMs);
                if (mLblTime.Text != newText)
                    mLblTime.Text = newText;
            }
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

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";

            double kb = bytes / 1024.0;

            if (kb < 1024)
                return kb.ToString("0.0") + " KB";

            double mb = kb / 1024.0;

            if (mb < 1024)
                return mb.ToString("0.0") + " MB";

            return (mb / 1024.0).ToString("0.0") + " GB";
        }

        private void SetStatus(string text)
        {
            if (mLblStatus != null)
                mLblStatus.Text = DateTime.Now.ToString("HH:mm:ss") + "　" + text;
        }

        private void SaveSession()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("VOLUME|" + mTrkVolume.Value);
                lines.Add("SPEED|" + mTrkSpeed.Value);
                lines.Add("SHUFFLE|" + (mChkShuffle.Checked ? "1" : "0"));
                lines.Add("LOOP|" + mCboLoop.SelectedIndex);

                if (_watcher != null)
                    lines.Add("WATCH|" + _watcher.Path);

                foreach (TrackItem track in _tracks)
                {
                    if (track.IsFavorite)
                        lines.Add("FAV|" + track.Path);
                }

                foreach (TrackItem track in _tracks)
                    lines.Add("TRACK|" + track.Path);

                File.WriteAllLines(GetSessionPath(), lines.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private void LoadSession()
        {
            try
            {
                string file = GetSessionPath();

                if (!File.Exists(file))
                    return;

                List<string> loadTracks = new List<string>();
                string watchFolder = "";

                foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
                {
                    if (line.StartsWith("VOLUME|"))
                    {
                        int value;
                        if (int.TryParse(line.Substring(7), out value))
                            mTrkVolume.Value = Math.Max(mTrkVolume.Minimum, Math.Min(mTrkVolume.Maximum, value));
                    }
                    else if (line.StartsWith("SPEED|"))
                    {
                        int value;
                        if (int.TryParse(line.Substring(6), out value))
                            mTrkSpeed.Value = Math.Max(mTrkSpeed.Minimum, Math.Min(mTrkSpeed.Maximum, value));
                    }
                    else if (line.StartsWith("SHUFFLE|"))
                    {
                        mChkShuffle.Checked = line.Substring(8) == "1";
                    }
                    else if (line.StartsWith("LOOP|"))
                    {
                        int value;
                        if (int.TryParse(line.Substring(5), out value))
                        {
                            if (value >= 0 && value < mCboLoop.Items.Count)
                                mCboLoop.SelectedIndex = value;
                        }
                    }
                    else if (line.StartsWith("FAV|"))
                    {
                        _favoritePaths.Add(line.Substring(4));
                    }
                    else if (line.StartsWith("TRACK|"))
                    {
                        loadTracks.Add(line.Substring(6));
                    }
                    else if (line.StartsWith("WATCH|"))
                    {
                        watchFolder = line.Substring(6);
                    }
                }

                AddFilesToPlaylist(loadTracks, false);

                foreach (TrackItem track in _tracks)
                    track.IsFavorite = _favoritePaths.Contains(track.Path);

                RefreshList();
                UpdateStats();

                if (Directory.Exists(watchFolder))
                    StartWatchingFolder(watchFolder);

                if (_tracks.Count > 0)
                    SetStatus("已還原上次播放清單。");
            }
            catch
            {
            }
        }

        private static string GetSessionPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WAVPlayer"
            );

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session.txt");
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
                AddFilesToPlaylist(paths, true);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                AddFilesFromDialog();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                mTxtSearch.Focus();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
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
            else if (e.KeyCode == Keys.Delete && mList.Focused)
            {
                RemoveSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && mList.Focused)
            {
                PlaySelectedOrCurrent();
                e.Handled = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "確定要關閉 音訊播放器 嗎？",
                "關閉確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            SaveSession();
            StopWatchingFolder(false);
            CloseMci();
        }

        private void List_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(AppColor.Card3))
                e.Graphics.FillRectangle(b, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                Font,
                e.Bounds,
                AppColor.SubText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis
            );
        }

        private void List_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            Color bg = selected ? AppColor.Selected : AppColor.Card2;
            Color fg = selected ? Color.White : AppColor.Text;

            if (e.ColumnIndex == 0)
            {
                TrackItem t = e.Item.Tag as TrackItem;

                if (t != null && t.IsFavorite && e.SubItem.Text == "★")
                    fg = AppColor.Warning;

                if (e.SubItem.Text == "▶")
                    fg = AppColor.Accent2;
            }

            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, e.Bounds);

            Rectangle textRect = e.Bounds;
            textRect.Inflate(-6, 0);

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                Font,
                textRect,
                fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis
            );
        }

        private void ShowAudioToolboxPage()
        {
            AudioToolboxForm page = new AudioToolboxForm(this);
            page.Show(this);
        }

        private void ShowDashboardPage()
        {
            DashboardForm page = new DashboardForm(this);
            page.Show(this);
        }

        // 保留舊 Designer 事件名稱，避免原本設計器還有綁定時編譯失敗
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

        private class AudioToolboxForm : Form
        {
            private readonly Form1 _host;
            private TextBox _log;

            public AudioToolboxForm(Form1 host)
            {
                _host = host;
                BuildUI();
            }

            private void BuildUI()
            {
                Text = "音訊播放器 - 音訊工具箱";
                Size = new Size(920, 620);
                MinimumSize = new Size(820, 520);
                StartPosition = FormStartPosition.CenterParent;
                BackColor = AppColor.Bg;
                ForeColor = AppColor.Text;
                Font = new Font("Microsoft JhengHei UI", 10F);

                TableLayoutPanel root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(18);
                root.RowCount = 3;
                root.ColumnCount = 1;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                Controls.Add(root);

                Label title = new Label();
                title.Text = "音訊工具箱";
                title.Dock = DockStyle.Fill;
                title.ForeColor = Color.White;
                title.Font = new Font("Microsoft JhengHei UI", 24F, FontStyle.Bold);
                title.TextAlign = ContentAlignment.MiddleLeft;
                root.Controls.Add(title, 0, 0);

                FlowLayoutPanel tools = new FlowLayoutPanel();
                tools.Dock = DockStyle.Fill;
                tools.WrapContents = true;
                tools.Padding = new Padding(0, 8, 0, 8);
                tools.BackColor = AppColor.Bg;
                root.Controls.Add(tools, 0, 1);

                tools.Controls.Add(MakeToolButton("檢查完整性", RunIntegrityCheck));
                tools.Controls.Add(MakeToolButton("匯出 CSV 報表", ExportCsv));
                tools.Controls.Add(MakeToolButton("匯出 HTML 報表", ExportHtml));
                tools.Controls.Add(MakeToolButton("偵測目前檔案靜音段", DetectSilenceCurrent));
                tools.Controls.Add(MakeToolButton("反轉目前 WAV", ReverseCurrentWav));
                tools.Controls.Add(MakeToolButton("淡入淡出目前 WAV", FadeCurrentWav));
                tools.Controls.Add(MakeToolButton("音量正規化目前 WAV", NormalizeCurrentWav));
                tools.Controls.Add(MakeToolButton("備份目前檔案", BackupCurrentFile));

                _log = new TextBox();
                _log.Dock = DockStyle.Fill;
                _log.Multiline = true;
                _log.ScrollBars = ScrollBars.Both;
                _log.ReadOnly = true;
                _log.WordWrap = false;
                _log.BackColor = AppColor.Input;
                _log.ForeColor = AppColor.Text;
                _log.BorderStyle = BorderStyle.FixedSingle;
                _log.Font = new Font("Consolas", 10F);
                root.Controls.Add(_log, 0, 2);

                WriteLog("音訊工具箱已啟動。");
                WriteLog("目前播放清單：" + _host._tracks.Count + " 個音訊/影片檔案。");
            }

            private Button MakeToolButton(string text, Action action)
            {
                Button btn = new Button();
                btn.Text = text;
                btn.Width = 190;
                btn.Height = 38;
                btn.Margin = new Padding(0, 0, 10, 10);
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = AppColor.Accent;
                btn.ForeColor = Color.White;
                btn.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);
                btn.Cursor = Cursors.Hand;
                btn.Click += delegate
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        WriteLog("錯誤：" + ex.Message);
                        MessageBox.Show(ex.Message, "操作失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                return btn;
            }

            private void WriteLog(string text)
            {
                if (_log == null)
                    return;

                _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            }

            private TrackItem GetTargetTrack()
            {
                TrackItem selected = _host.GetSelectedTrack();

                if (selected != null)
                    return selected;

                if (_host._currentTrack != null)
                    return _host._currentTrack;

                return null;
            }

            private void RunIntegrityCheck()
            {
                WriteLog("開始檢查檔案完整性...");

                int ok = 0;
                int fail = 0;

                foreach (TrackItem track in _host._tracks)
                {
                    if (!File.Exists(track.Path))
                    {
                        fail++;
                        WriteLog("遺失：" + track.Path);
                        continue;
                    }

                    try
                    {
                        AudioInfo info = AudioInfo.Read(track.Path);

                        if (info.DurationMs <= 0)
                        {
                            fail++;
                            WriteLog("異常長度：" + track.FileName);
                        }
                        else
                        {
                            ok++;
                            WriteLog("正常：" + track.FileName + "｜" + info.FormatName + "｜" + Form1.FormatTime(info.DurationMs));
                        }
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        WriteLog("錯誤：" + track.FileName + "｜" + ex.Message);
                    }
                }

                WriteLog("檢查完成。正常 " + ok + " 個，異常 " + fail + " 個。");
            }

            private void ExportCsv()
            {
                if (_host._tracks.Count == 0)
                {
                    WriteLog("播放清單是空的，無法匯出。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "匯出 CSV 報表";
                    sfd.Filter = "CSV 檔案 (*.csv)|*.csv";
                    sfd.FileName = "音訊播放器_音訊報表.csv";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("檔名,長度,格式,取樣率,聲道,位元,大小,收藏,路徑");

                    foreach (TrackItem t in _host._tracks)
                    {
                        sb.AppendLine(
                            Csv(t.FileName) + "," +
                            Csv(Form1.FormatTime(t.DurationMs)) + "," +
                            Csv(t.FormatName) + "," +
                            Csv(t.SampleRateText) + "," +
                            Csv(t.ChannelsText) + "," +
                            Csv(t.BitsText) + "," +
                            Csv(Form1.FormatBytes(t.FileSize)) + "," +
                            Csv(t.IsFavorite ? "是" : "否") + "," +
                            Csv(t.Path)
                        );
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    WriteLog("已匯出 CSV：" + sfd.FileName);
                }
            }

            private void ExportHtml()
            {
                if (_host._tracks.Count == 0)
                {
                    WriteLog("播放清單是空的，無法匯出。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "匯出 HTML 報表";
                    sfd.Filter = "HTML 檔案 (*.html)|*.html";
                    sfd.FileName = "音訊播放器_音訊報表.html";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    long totalMs = _host._tracks.Sum(t => t.DurationMs);
                    long totalBytes = _host._tracks.Sum(t => t.FileSize);

                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("<!doctype html>");
                    sb.AppendLine("<html lang=\"zh-Hant\"><head><meta charset=\"utf-8\">");
                    sb.AppendLine("<title>音訊播放器 音訊報表</title>");
                    sb.AppendLine("<style>");
                    sb.AppendLine("body{font-family:'Microsoft JhengHei',sans-serif;background:#0f121c;color:#ebeef8;padding:32px;}");
                    sb.AppendLine("h1{font-size:32px;margin-bottom:8px;}");
                    sb.AppendLine(".card{background:#191e2d;border:1px solid #373f55;border-radius:18px;padding:20px;margin:16px 0;}");
                    sb.AppendLine("table{border-collapse:collapse;width:100%;font-size:14px;}");
                    sb.AppendLine("th,td{border-bottom:1px solid #373f55;padding:10px;text-align:left;}");
                    sb.AppendLine("th{color:#54d2ff;}");
                    sb.AppendLine(".sub{color:#a0aac3;}");
                    sb.AppendLine("</style></head><body>");
                    sb.AppendLine("<h1>音訊播放器 音訊報表</h1>");
                    sb.AppendLine("<div class=\"sub\">產生時間：" + Html(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "</div>");
                    sb.AppendLine("<div class=\"card\">");
                    sb.AppendLine("總檔案數：" + _host._tracks.Count + "<br>");
                    sb.AppendLine("總長度：" + Html(Form1.FormatTime(totalMs)) + "<br>");
                    sb.AppendLine("總大小：" + Html(Form1.FormatBytes(totalBytes)));
                    sb.AppendLine("</div>");
                    sb.AppendLine("<div class=\"card\"><table>");
                    sb.AppendLine("<tr><th>檔名</th><th>長度</th><th>格式</th><th>取樣率</th><th>聲道</th><th>位元</th><th>大小</th><th>收藏</th><th>路徑</th></tr>");

                    foreach (TrackItem t in _host._tracks)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine("<td>" + Html(t.FileName) + "</td>");
                        sb.AppendLine("<td>" + Html(Form1.FormatTime(t.DurationMs)) + "</td>");
                        sb.AppendLine("<td>" + Html(t.FormatName) + "</td>");
                        sb.AppendLine("<td>" + Html(t.SampleRateText) + "</td>");
                        sb.AppendLine("<td>" + Html(t.ChannelsText) + "</td>");
                        sb.AppendLine("<td>" + Html(t.BitsText) + "</td>");
                        sb.AppendLine("<td>" + Html(Form1.FormatBytes(t.FileSize)) + "</td>");
                        sb.AppendLine("<td>" + Html(t.IsFavorite ? "★" : "") + "</td>");
                        sb.AppendLine("<td>" + Html(t.Path) + "</td>");
                        sb.AppendLine("</tr>");
                    }

                    sb.AppendLine("</table></div>");
                    sb.AppendLine("</body></html>");

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    WriteLog("已匯出 HTML：" + sfd.FileName);
                }
            }

            private void DetectSilenceCurrent()
            {
                TrackItem t = GetTargetTrack();

                if (t == null)
                {
                    WriteLog("請先選取或播放一個音訊/影片檔案。");
                    return;
                }

                WriteLog("開始偵測靜音段：" + t.FileName);

                float[] peaks = WaveformAnalyzer.BuildPeaks(t.Path, 2400);

                if (peaks == null || peaks.Length == 0 || t.DurationMs <= 0)
                {
                    WriteLog("無法分析此檔案。");
                    return;
                }

                double msPerPeak = t.DurationMs / (double)peaks.Length;
                int minSilentPeaks = Math.Max(1, (int)Math.Ceiling(800 / msPerPeak));
                float threshold = 0.015f;
                int count = 0;

                int i = 0;

                while (i < peaks.Length)
                {
                    if (peaks[i] > threshold)
                    {
                        i++;
                        continue;
                    }

                    int start = i;

                    while (i < peaks.Length && peaks[i] <= threshold)
                        i++;

                    int end = i - 1;
                    int len = end - start + 1;

                    if (len >= minSilentPeaks)
                    {
                        long startMs = (long)(start * msPerPeak);
                        long endMs = (long)(end * msPerPeak);
                        count++;

                        WriteLog("靜音段 " + count + "：" + Form1.FormatTime(startMs) + " ~ " + Form1.FormatTime(endMs));
                    }
                }

                if (count == 0)
                    WriteLog("沒有偵測到超過 0.8 秒的明顯靜音段。");
                else
                    WriteLog("靜音段偵測完成，共 " + count + " 段。");
            }

            private void ReverseCurrentWav()
            {
                TrackItem t = GetTargetTrack();

                if (t == null)
                {
                    WriteLog("請先選取或播放一個音訊/影片檔案。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "輸出反轉 WAV";
                    sfd.Filter = "WAV 檔案 (*.wav)|*.wav";
                    sfd.FileName = Path.GetFileNameWithoutExtension(t.Path) + "_反轉.wav";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    WavEditor.ReverseFrames(t.Path, sfd.FileName);
                    WriteLog("已輸出反轉 WAV：" + sfd.FileName);
                }
            }

            private void FadeCurrentWav()
            {
                TrackItem t = GetTargetTrack();

                if (t == null)
                {
                    WriteLog("請先選取或播放一個音訊/影片檔案。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "輸出淡入淡出 WAV";
                    sfd.Filter = "WAV 檔案 (*.wav)|*.wav";
                    sfd.FileName = Path.GetFileNameWithoutExtension(t.Path) + "_淡入淡出.wav";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    WavEditor.ApplyFade(t.Path, sfd.FileName, 1000);
                    WriteLog("已輸出淡入淡出 WAV：" + sfd.FileName);
                }
            }

            private void NormalizeCurrentWav()
            {
                TrackItem t = GetTargetTrack();

                if (t == null)
                {
                    WriteLog("請先選取或播放一個音訊/影片檔案。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "輸出音量正規化 WAV";
                    sfd.Filter = "WAV 檔案 (*.wav)|*.wav";
                    sfd.FileName = Path.GetFileNameWithoutExtension(t.Path) + "_正規化.wav";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    WavEditor.Normalize(t.Path, sfd.FileName, 0.95);
                    WriteLog("已輸出正規化 WAV：" + sfd.FileName);
                }
            }

            private void BackupCurrentFile()
            {
                TrackItem t = GetTargetTrack();

                if (t == null)
                {
                    WriteLog("請先選取或播放一個音訊/影片檔案。");
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "備份目前 WAV";
                    sfd.Filter = "WAV 檔案 (*.wav)|*.wav";
                    sfd.FileName = Path.GetFileNameWithoutExtension(t.Path) + "_備份.wav";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    File.Copy(t.Path, sfd.FileName, true);
                    WriteLog("已備份：" + sfd.FileName);
                }
            }

            private static string Csv(string value)
            {
                if (value == null)
                    value = "";

                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            private static string Html(string value)
            {
                if (value == null)
                    return "";

                return value
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
            }
        }

        private class DashboardForm : Form
        {
            private readonly Form1 _host;
            private Label _summary;
            private TextBox _detail;
            private MiniBarChart _chart;

            public DashboardForm(Form1 host)
            {
                _host = host;
                BuildUI();
                RefreshDashboard();
            }

            private void BuildUI()
            {
                Text = "音訊播放器 - 統計頁";
                Size = new Size(900, 620);
                MinimumSize = new Size(780, 520);
                StartPosition = FormStartPosition.CenterParent;
                BackColor = AppColor.Bg;
                ForeColor = AppColor.Text;
                Font = new Font("Microsoft JhengHei UI", 10F);

                TableLayoutPanel root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(18);
                root.RowCount = 4;
                root.ColumnCount = 1;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
                Controls.Add(root);

                Label title = new Label();
                title.Text = "統計儀表板";
                title.Dock = DockStyle.Fill;
                title.ForeColor = Color.White;
                title.Font = new Font("Microsoft JhengHei UI", 24F, FontStyle.Bold);
                title.TextAlign = ContentAlignment.MiddleLeft;
                root.Controls.Add(title, 0, 0);

                _summary = new Label();
                _summary.Dock = DockStyle.Fill;
                _summary.ForeColor = AppColor.Text;
                _summary.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
                _summary.TextAlign = ContentAlignment.MiddleLeft;
                root.Controls.Add(_summary, 0, 1);

                _chart = new MiniBarChart();
                _chart.Dock = DockStyle.Fill;
                _chart.Margin = new Padding(0, 0, 0, 14);
                root.Controls.Add(_chart, 0, 2);

                _detail = new TextBox();
                _detail.Dock = DockStyle.Fill;
                _detail.Multiline = true;
                _detail.ScrollBars = ScrollBars.Both;
                _detail.ReadOnly = true;
                _detail.WordWrap = false;
                _detail.BackColor = AppColor.Input;
                _detail.ForeColor = AppColor.Text;
                _detail.BorderStyle = BorderStyle.FixedSingle;
                _detail.Font = new Font("Consolas", 10F);
                root.Controls.Add(_detail, 0, 3);
            }

            private void RefreshDashboard()
            {
                int count = _host._tracks.Count;
                long totalMs = _host._tracks.Sum(t => t.DurationMs);
                long totalBytes = _host._tracks.Sum(t => t.FileSize);
                int favCount = _host._tracks.Count(t => t.IsFavorite);

                TrackItem longest = _host._tracks.OrderByDescending(t => t.DurationMs).FirstOrDefault();
                TrackItem biggest = _host._tracks.OrderByDescending(t => t.FileSize).FirstOrDefault();

                double avgRate = _host._tracks.Where(t => t.SampleRate > 0).Any() ? _host._tracks.Where(t => t.SampleRate > 0).Average(t => t.SampleRate) : 0;
                double avgBits = _host._tracks.Where(t => t.BitsPerSample > 0).Any() ? _host._tracks.Where(t => t.BitsPerSample > 0).Average(t => t.BitsPerSample) : 0;

                _summary.Text =
                    "總曲數 " + count +
                    "　｜　總長度 " + Form1.FormatTime(totalMs) +
                    "　｜　總大小 " + Form1.FormatBytes(totalBytes) +
                    "　｜　收藏 " + favCount;

                Dictionary<string, int> formatData = _host._tracks
                    .GroupBy(t => t.FormatName)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                _chart.SetData(formatData, "格式分布");

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("音訊播放器 統計摘要");
                sb.AppendLine("產生時間：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("總曲數：" + count);
                sb.AppendLine("總長度：" + Form1.FormatTime(totalMs));
                sb.AppendLine("總大小：" + Form1.FormatBytes(totalBytes));
                sb.AppendLine("收藏數：" + favCount);
                sb.AppendLine("平均取樣率：" + avgRate.ToString("0") + " Hz");
                sb.AppendLine("平均位元深度：" + avgBits.ToString("0.0") + " bit");
                sb.AppendLine();

                if (longest != null)
                {
                    sb.AppendLine("最長檔案：" + longest.FileName);
                    sb.AppendLine("長度：" + Form1.FormatTime(longest.DurationMs));
                    sb.AppendLine("路徑：" + longest.Path);
                    sb.AppendLine();
                }

                if (biggest != null)
                {
                    sb.AppendLine("最大檔案：" + biggest.FileName);
                    sb.AppendLine("大小：" + Form1.FormatBytes(biggest.FileSize));
                    sb.AppendLine("路徑：" + biggest.Path);
                    sb.AppendLine();
                }

                sb.AppendLine("格式分布：");

                foreach (var pair in formatData)
                    sb.AppendLine("- " + pair.Key + "：" + pair.Value + " 個");

                sb.AppendLine();
                sb.AppendLine("取樣率分布：");

                foreach (var group in _host._tracks.GroupBy(t => t.SampleRate > 0 ? t.SampleRate + " Hz" : "未知").OrderByDescending(g => g.Count()))
                    sb.AppendLine("- " + group.Key + "：" + group.Count() + " 個");

                _detail.Text = sb.ToString();
            }
        }

        private class TrackItem
        {
            public string Path;
            public string FileName;
            public string FormatName;
            public short Channels;
            public int SampleRate;
            public short BitsPerSample;
            public long DurationMs;
            public long FileSize;
            public bool IsFavorite;

            public string ChannelsText
            {
                get { return Channels > 0 ? Channels.ToString() : "-"; }
            }

            public string SampleRateText
            {
                get { return SampleRate > 0 ? SampleRate + " Hz" : "-"; }
            }

            public string BitsText
            {
                get { return BitsPerSample > 0 ? BitsPerSample + " bit" : "-"; }
            }

            public string FullDescription
            {
                get
                {
                    return FormatName + "｜" +
                           ChannelsText + " 聲道｜" +
                           SampleRateText + "｜" +
                           BitsText + "｜" +
                           FormatBytes(FileSize) + "｜" +
                           FormatTime(DurationMs);
                }
            }

            public static TrackItem FromFile(string path)
            {
                AudioInfo info = AudioInfo.Read(path);
                FileInfo file = new FileInfo(path);

                TrackItem item = new TrackItem();
                item.Path = path;
                item.FileName = System.IO.Path.GetFileName(path);
                item.FormatName = info.FormatName;
                item.Channels = info.Channels;
                item.SampleRate = info.SampleRate;
                item.BitsPerSample = info.BitsPerSample;
                item.DurationMs = info.DurationMs;
                item.FileSize = file.Length;

                return item;
            }
        }

        private class AudioInfo
        {
            public short Channels;
            public int SampleRate;
            public short BitsPerSample;
            public long DurationMs;
            public string FormatName;

            public static AudioInfo Read(string path)
            {
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".wav")
                {
                    try
                    {
                        WaveInfo wav = WaveInfo.Read(path);

                        AudioInfo result = new AudioInfo();
                        result.Channels = wav.Channels;
                        result.SampleRate = wav.SampleRate;
                        result.BitsPerSample = wav.BitsPerSample;
                        result.DurationMs = wav.DurationMs;
                        result.FormatName = wav.FormatName;
                        return result;
                    }
                    catch
                    {
                        // 非標準 WAV 或壓縮 WAV，改用 Windows Media Foundation 嘗試讀取。
                    }
                }

                AudioInfo info = null;

                try
                {
                    info = MediaFoundationAudio.ReadInfo(path);
                }
                catch
                {
                    info = new AudioInfo();
                }

                if (string.IsNullOrWhiteSpace(info.FormatName))
                    info.FormatName = GuessFormatName(path);

                if (info.DurationMs <= 0)
                    info.DurationMs = SimpleAudioDuration.GetDurationMs(path);

                if (info.DurationMs <= 0)
                    info.DurationMs = MciAudioInfo.GetDurationMs(path);

                return info;
            }

            public static string GuessFormatName(string path)
            {
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".wav")
                    return "WAV";
                if (ext == ".mp3")
                    return "MP3";
                if (ext == ".mp4")
                    return "MP4 音訊/影片";

                if (ext.Length > 1)
                    return ext.Substring(1).ToUpperInvariant();

                return "未知格式";
            }
        }

        private static class SimpleAudioDuration
        {
            public static long GetDurationMs(string path)
            {
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".mp3")
                    return GetMp3DurationMs(path);

                if (ext == ".mp4" || ext == ".m4a")
                    return GetMp4DurationMs(path);

                return 0;
            }

            private static long GetMp3DurationMs(string path)
            {
                try
                {
                    using (FileStream fs = File.OpenRead(path))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        long start = SkipId3v2(br, fs.Length);
                        fs.Position = start;

                        while (fs.Position + 4 < fs.Length)
                        {
                            long framePos = fs.Position;
                            uint header = ReadUInt32BE(br);

                            if (IsValidMp3Header(header))
                            {
                                int bitRate = GetMp3BitRate(header);

                                if (bitRate > 0)
                                {
                                    long audioBytes = Math.Max(0, fs.Length - framePos);
                                    return audioBytes * 8L * 1000L / bitRate;
                                }
                            }

                            fs.Position = framePos + 1;
                        }
                    }
                }
                catch
                {
                }

                return 0;
            }

            private static long SkipId3v2(BinaryReader br, long length)
            {
                if (length < 10)
                    return 0;

                byte[] tag = br.ReadBytes(3);

                if (tag.Length == 3 && tag[0] == 'I' && tag[1] == 'D' && tag[2] == '3')
                {
                    br.ReadByte();
                    br.ReadByte();
                    byte flags = br.ReadByte();
                    byte[] sizeBytes = br.ReadBytes(4);

                    if (sizeBytes.Length == 4)
                    {
                        int size = ((sizeBytes[0] & 0x7F) << 21) |
                                   ((sizeBytes[1] & 0x7F) << 14) |
                                   ((sizeBytes[2] & 0x7F) << 7) |
                                   (sizeBytes[3] & 0x7F);

                        long total = 10L + size;

                        if ((flags & 0x10) != 0)
                            total += 10;

                        if (total < length)
                            return total;
                    }
                }

                return 0;
            }

            private static bool IsValidMp3Header(uint header)
            {
                if ((header & 0xFFE00000) != 0xFFE00000)
                    return false;

                int version = (int)((header >> 19) & 0x3);
                int layer = (int)((header >> 17) & 0x3);
                int bitrateIndex = (int)((header >> 12) & 0xF);
                int sampleRateIndex = (int)((header >> 10) & 0x3);

                return version != 1 && layer != 0 && bitrateIndex != 0 && bitrateIndex != 15 && sampleRateIndex != 3;
            }

            private static int GetMp3BitRate(uint header)
            {
                int version = (int)((header >> 19) & 0x3);
                int layer = (int)((header >> 17) & 0x3);
                int index = (int)((header >> 12) & 0xF);

                int[] mpeg1Layer1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 };
                int[] mpeg1Layer2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 };
                int[] mpeg1Layer3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
                int[] mpeg2Layer1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 };
                int[] mpeg2Layer23 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

                int kbps;

                if (version == 3)
                {
                    if (layer == 3)
                        kbps = mpeg1Layer1[index];
                    else if (layer == 2)
                        kbps = mpeg1Layer2[index];
                    else
                        kbps = mpeg1Layer3[index];
                }
                else
                {
                    if (layer == 3)
                        kbps = mpeg2Layer1[index];
                    else
                        kbps = mpeg2Layer23[index];
                }

                return kbps * 1000;
            }

            private static long GetMp4DurationMs(string path)
            {
                try
                {
                    using (FileStream fs = File.OpenRead(path))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        return FindMp4Duration(br, fs.Length, 0);
                    }
                }
                catch
                {
                    return 0;
                }
            }

            private static long FindMp4Duration(BinaryReader br, long end, int depth)
            {
                Stream s = br.BaseStream;

                while (s.Position + 8 <= end)
                {
                    long atomStart = s.Position;
                    long size = ReadUInt32BE(br);
                    string type = Encoding.ASCII.GetString(br.ReadBytes(4));

                    if (size == 1 && s.Position + 8 <= end)
                        size = ReadInt64BE(br);
                    else if (size == 0)
                        size = end - atomStart;

                    if (size < 8)
                        break;

                    long atomEnd = Math.Min(end, atomStart + size);

                    if (type == "mvhd")
                    {
                        byte version = br.ReadByte();
                        br.ReadBytes(3);

                        long timescale;
                        long duration;

                        if (version == 1)
                        {
                            br.ReadBytes(16);
                            timescale = ReadUInt32BE(br);
                            duration = ReadInt64BE(br);
                        }
                        else
                        {
                            br.ReadBytes(8);
                            timescale = ReadUInt32BE(br);
                            duration = ReadUInt32BE(br);
                        }

                        if (timescale > 0 && duration > 0)
                            return duration * 1000L / timescale;
                    }
                    else if ((type == "moov" || type == "trak" || type == "mdia") && depth < 5)
                    {
                        long found = FindMp4Duration(br, atomEnd, depth + 1);

                        if (found > 0)
                            return found;
                    }

                    s.Position = atomEnd;
                }

                return 0;
            }

            private static uint ReadUInt32BE(BinaryReader br)
            {
                byte[] b = br.ReadBytes(4);

                if (b.Length < 4)
                    return 0;

                return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            }

            private static long ReadInt64BE(BinaryReader br)
            {
                byte[] b = br.ReadBytes(8);

                if (b.Length < 8)
                    return 0;

                return ((long)b[0] << 56) |
                       ((long)b[1] << 48) |
                       ((long)b[2] << 40) |
                       ((long)b[3] << 32) |
                       ((long)b[4] << 24) |
                       ((long)b[5] << 16) |
                       ((long)b[6] << 8) |
                       b[7];
            }
        }

        private static class MciAudioInfo
        {
            public static long GetDurationMs(string path)
            {
                string alias = "mciinfo" + Guid.NewGuid().ToString("N").Substring(0, 8);
                bool opened = false;

                try
                {
                    opened = Send("open " + Quote(path) + " alias " + alias);

                    if (!opened)
                        opened = Send("open " + Quote(path) + " type mpegvideo alias " + alias);

                    if (!opened)
                        opened = Send("open " + Quote(path) + " type waveaudio alias " + alias);

                    if (!opened)
                        return 0;

                    Send("set " + alias + " time format milliseconds");

                    string lengthText = Query("status " + alias + " length");
                    long length;

                    if (long.TryParse(lengthText, out length))
                        return length;
                }
                catch
                {
                }
                finally
                {
                    if (opened)
                        Send("close " + alias);
                }

                return 0;
            }

            private static bool Send(string command)
            {
                int error = mciSendString(command, null, 0, IntPtr.Zero);
                return error == 0;
            }

            private static string Query(string command)
            {
                StringBuilder buffer = new StringBuilder(512);
                int error = mciSendString(command, buffer, buffer.Capacity, IntPtr.Zero);

                if (error != 0)
                    return "";

                return buffer.ToString().Trim();
            }
        }

        private class WaveInfo
        {
            public short AudioFormat;
            public short Channels;
            public int SampleRate;
            public int ByteRate;
            public short BlockAlign;
            public short BitsPerSample;
            public long DataOffset;
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

                    return "WAV 格式 " + AudioFormat;
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
                        long chunkStart = fs.Position;
                        long nextChunk = chunkStart + chunkSize;

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
                            info.DataOffset = chunkStart;
                            info.DataBytes = chunkSize;
                        }

                        fs.Position = Math.Min(nextChunk, fs.Length);

                        if ((chunkSize & 1) == 1 && fs.Position < fs.Length)
                            fs.Position++;
                    }

                    if (info.ByteRate > 0 && info.DataBytes > 0)
                        info.DurationMs = info.DataBytes * 1000 / info.ByteRate;

                    if (info.Channels <= 0 || info.SampleRate <= 0 || info.BitsPerSample <= 0)
                        throw new InvalidDataException("WAV 格式資訊不完整。");

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

        private static class WaveformAnalyzer
        {
            public static float[] BuildPeaks(string path, int desiredPeaks)
            {
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                try
                {
                    if (ext == ".wav")
                    {
                        float[] wavPeaks = BuildWavPeaks(path, desiredPeaks);

                        if (HasUsefulPeaks(wavPeaks))
                            return wavPeaks;
                    }

                    // 第一優先：使用 Windows 內建 Media Foundation 嘗試解碼成 PCM，這是最接近真實聲音波形的方式。
                    // 不需要安裝 NuGet 或第三方 DLL，但會受 Windows 系統解碼能力影響。
                    float[] mfPeaks = MediaFoundationAudio.BuildPeaks(path, desiredPeaks);

                    if (HasUsefulPeaks(mfPeaks))
                        return mfPeaks;

                    // 第二優先：有些電腦可以播放 MP3，但 Media Foundation SourceReader 不一定能把它轉成 PCM。
                    // 這裡改用 MP3 frame energy 產生「預覽波形」，至少不會空白。
                    if (ext == ".mp3")
                        return BuildMp3FramePreviewPeaks(path, desiredPeaks);

                    // MP4 / M4A 若系統無法解碼，就用壓縮資料產生視覺化預覽。
                    if (ext == ".mp4" || ext == ".m4a")
                        return BuildCompressedBytePreviewPeaks(path, desiredPeaks);

                    return mfPeaks == null ? new float[0] : mfPeaks;
                }
                catch
                {
                    try
                    {
                        if (ext == ".mp3")
                            return BuildMp3FramePreviewPeaks(path, desiredPeaks);

                        if (ext == ".mp4" || ext == ".m4a")
                            return BuildCompressedBytePreviewPeaks(path, desiredPeaks);
                    }
                    catch
                    {
                    }

                    return new float[0];
                }
            }

            private static bool HasUsefulPeaks(float[] peaks)
            {
                if (peaks == null || peaks.Length == 0)
                    return false;

                for (int i = 0; i < peaks.Length; i++)
                {
                    if (peaks[i] > 0.0001f)
                        return true;
                }

                return false;
            }

            private struct Mp3FrameInfo
            {
                public int VersionId;
                public int Layer;
                public int BitRate;
                public int SampleRate;
                public int SamplesPerFrame;
                public int FrameLength;
                public int ChannelMode;
            }

            private static float[] BuildMp3FramePreviewPeaks(string path, int desiredPeaks)
            {
                try
                {
                    List<float> frameEnergy = new List<float>();

                    using (FileStream fs = File.OpenRead(path))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        fs.Position = SkipId3v2ForPreview(br, fs.Length);

                        while (fs.Position + 4 < fs.Length && frameEnergy.Count < 300000)
                        {
                            long frameStart = fs.Position;
                            uint header = ReadUInt32BEForPreview(br);
                            Mp3FrameInfo info;

                            if (TryParseMp3Frame(header, out info) &&
                                info.FrameLength >= 8 &&
                                frameStart + info.FrameLength <= fs.Length)
                            {
                                int payloadLength = info.FrameLength - 4;
                                byte[] payload = br.ReadBytes(payloadLength);

                                if (payload.Length > 0)
                                {
                                    int skip = GetMp3SideInfoSkip(info);

                                    if (skip >= payload.Length)
                                        skip = 0;

                                    frameEnergy.Add(CompressedEnergy(payload, skip));
                                }

                                fs.Position = frameStart + info.FrameLength;
                            }
                            else
                            {
                                fs.Position = frameStart + 1;
                            }
                        }
                    }

                    if (frameEnergy.Count == 0)
                        return new float[0];

                    return ResamplePreviewPeaks(frameEnergy, desiredPeaks);
                }
                catch
                {
                    return new float[0];
                }
            }

            private static long SkipId3v2ForPreview(BinaryReader br, long length)
            {
                try
                {
                    if (length < 10)
                        return 0;

                    br.BaseStream.Position = 0;
                    byte[] tag = br.ReadBytes(3);

                    if (tag.Length == 3 && tag[0] == 'I' && tag[1] == 'D' && tag[2] == '3')
                    {
                        br.ReadByte();
                        br.ReadByte();
                        byte flags = br.ReadByte();
                        byte[] sizeBytes = br.ReadBytes(4);

                        if (sizeBytes.Length == 4)
                        {
                            int size = ((sizeBytes[0] & 0x7F) << 21) |
                                       ((sizeBytes[1] & 0x7F) << 14) |
                                       ((sizeBytes[2] & 0x7F) << 7) |
                                       (sizeBytes[3] & 0x7F);

                            long total = 10L + size;

                            if ((flags & 0x10) != 0)
                                total += 10;

                            if (total > 0 && total < length)
                                return total;
                        }
                    }
                }
                catch
                {
                }

                return 0;
            }

            private static uint ReadUInt32BEForPreview(BinaryReader br)
            {
                byte[] b = br.ReadBytes(4);

                if (b.Length < 4)
                    return 0;

                return ((uint)b[0] << 24) |
                       ((uint)b[1] << 16) |
                       ((uint)b[2] << 8) |
                       b[3];
            }

            private static bool TryParseMp3Frame(uint header, out Mp3FrameInfo info)
            {
                info = new Mp3FrameInfo();

                if ((header & 0xFFE00000) != 0xFFE00000)
                    return false;

                int versionId = (int)((header >> 19) & 0x3);
                int layer = (int)((header >> 17) & 0x3);
                int bitRateIndex = (int)((header >> 12) & 0xF);
                int sampleRateIndex = (int)((header >> 10) & 0x3);
                int padding = (int)((header >> 9) & 0x1);
                int channelMode = (int)((header >> 6) & 0x3);

                if (versionId == 1 || layer == 0 || bitRateIndex == 0 || bitRateIndex == 15 || sampleRateIndex == 3)
                    return false;

                int bitRate = GetMp3BitRateForPreview(versionId, layer, bitRateIndex);
                int sampleRate = GetMp3SampleRateForPreview(versionId, sampleRateIndex);

                if (bitRate <= 0 || sampleRate <= 0)
                    return false;

                int samplesPerFrame;
                int frameLength;

                if (layer == 3) // Layer I
                {
                    samplesPerFrame = 384;
                    frameLength = ((12 * bitRate / sampleRate) + padding) * 4;
                }
                else if (layer == 2) // Layer II
                {
                    samplesPerFrame = 1152;
                    frameLength = 144 * bitRate / sampleRate + padding;
                }
                else // Layer III
                {
                    samplesPerFrame = versionId == 3 ? 1152 : 576;

                    if (versionId == 3)
                        frameLength = 144 * bitRate / sampleRate + padding;
                    else
                        frameLength = 72 * bitRate / sampleRate + padding;
                }

                if (frameLength < 8)
                    return false;

                info.VersionId = versionId;
                info.Layer = layer;
                info.BitRate = bitRate;
                info.SampleRate = sampleRate;
                info.SamplesPerFrame = samplesPerFrame;
                info.FrameLength = frameLength;
                info.ChannelMode = channelMode;
                return true;
            }

            private static int GetMp3BitRateForPreview(int versionId, int layer, int index)
            {
                int[] mpeg1Layer1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 };
                int[] mpeg1Layer2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 };
                int[] mpeg1Layer3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
                int[] mpeg2Layer1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 };
                int[] mpeg2Layer23 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

                int kbps;

                if (versionId == 3)
                {
                    if (layer == 3)
                        kbps = mpeg1Layer1[index];
                    else if (layer == 2)
                        kbps = mpeg1Layer2[index];
                    else
                        kbps = mpeg1Layer3[index];
                }
                else
                {
                    if (layer == 3)
                        kbps = mpeg2Layer1[index];
                    else
                        kbps = mpeg2Layer23[index];
                }

                return kbps * 1000;
            }

            private static int GetMp3SampleRateForPreview(int versionId, int index)
            {
                int[,] table = new int[,]
                {
                    { 11025, 12000, 8000 },
                    { 0, 0, 0 },
                    { 22050, 24000, 16000 },
                    { 44100, 48000, 32000 }
                };

                return table[versionId, index];
            }

            private static int GetMp3SideInfoSkip(Mp3FrameInfo info)
            {
                // payload 不包含 4 bytes header，因此這裡只跳過 side-info，不再加 header 長度。
                // Layer III 的 side-info 長度：MPEG1 mono 17 / stereo 32；MPEG2/2.5 mono 9 / stereo 17。
                // 加上一點保守值，讓預覽比較不受 frame header/side-info 影響。
                if (info.Layer != 1)
                    return 0;

                bool mono = info.ChannelMode == 3;

                if (info.VersionId == 3)
                    return mono ? 17 : 32;

                return mono ? 9 : 17;
            }

            private static float CompressedEnergy(byte[] data, int start)
            {
                if (data == null || data.Length == 0)
                    return 0;

                start = Math.Max(0, Math.Min(start, data.Length - 1));

                int length = data.Length - start;
                int step = Math.Max(1, length / 220);
                int count = 0;
                int previous = 128;
                double sum = 0;
                double diff = 0;

                for (int i = start; i < data.Length; i += step)
                {
                    int v = data[i];
                    double centered = (v - 128) / 128.0;
                    sum += centered * centered;
                    diff += Math.Abs(v - previous) / 255.0;
                    previous = v;
                    count++;
                }

                if (count == 0)
                    return 0;

                double rms = Math.Sqrt(sum / count);
                double movement = diff / count;
                double value = rms * 0.72 + movement * 0.28;

                if (value < 0.02)
                    value = 0.02;
                if (value > 1.0)
                    value = 1.0;

                return (float)value;
            }

            private static float[] BuildCompressedBytePreviewPeaks(string path, int desiredPeaks)
            {
                try
                {
                    using (FileStream fs = File.OpenRead(path))
                    {
                        if (fs.Length <= 0)
                            return new float[0];

                        int peakCount = Math.Max(1, desiredPeaks);
                        peakCount = (int)Math.Min(peakCount, Math.Max(1, fs.Length / 512));
                        float[] peaks = new float[peakCount];
                        byte[] buffer = new byte[4096];

                        for (int i = 0; i < peakCount; i++)
                        {
                            long start = i * fs.Length / peakCount;
                            long end = (i + 1) * fs.Length / peakCount;
                            long length = Math.Max(1, end - start);
                            long step = Math.Max(1, length / buffer.Length);
                            int count = 0;
                            double sum = 0;
                            int previous = 128;
                            double diff = 0;

                            for (long pos = start; pos < end; pos += step)
                            {
                                fs.Position = pos;
                                int b = fs.ReadByte();

                                if (b < 0)
                                    break;

                                double centered = (b - 128) / 128.0;
                                sum += centered * centered;
                                diff += Math.Abs(b - previous) / 255.0;
                                previous = b;
                                count++;
                            }

                            if (count > 0)
                            {
                                double rms = Math.Sqrt(sum / count);
                                double movement = diff / count;
                                peaks[i] = (float)Math.Max(0.02, Math.Min(1.0, rms * 0.72 + movement * 0.28));
                            }
                        }

                        return NormalizePreviewPeaks(peaks);
                    }
                }
                catch
                {
                    return new float[0];
                }
            }

            private static float[] ResamplePreviewPeaks(List<float> source, int desiredPeaks)
            {
                if (source == null || source.Count == 0)
                    return new float[0];

                int peakCount = Math.Max(1, Math.Min(desiredPeaks, source.Count));
                float[] peaks = new float[peakCount];

                for (int i = 0; i < peakCount; i++)
                {
                    int start = i * source.Count / peakCount;
                    int end = (i + 1) * source.Count / peakCount;

                    if (end <= start)
                        end = start + 1;

                    float max = 0;

                    for (int j = start; j < end && j < source.Count; j++)
                    {
                        if (source[j] > max)
                            max = source[j];
                    }

                    peaks[i] = max;
                }

                SmoothPreviewPeaks(peaks);
                return NormalizePreviewPeaks(peaks);
            }

            private static void SmoothPreviewPeaks(float[] peaks)
            {
                if (peaks == null || peaks.Length < 3)
                    return;

                float previous = peaks[0];

                for (int i = 1; i < peaks.Length - 1; i++)
                {
                    float current = peaks[i];
                    peaks[i] = previous * 0.25f + current * 0.50f + peaks[i + 1] * 0.25f;
                    previous = current;
                }
            }

            private static float[] NormalizePreviewPeaks(float[] peaks)
            {
                if (peaks == null || peaks.Length == 0)
                    return new float[0];

                float min = float.MaxValue;
                float max = float.MinValue;

                for (int i = 0; i < peaks.Length; i++)
                {
                    if (peaks[i] < min)
                        min = peaks[i];
                    if (peaks[i] > max)
                        max = peaks[i];
                }

                if (max <= 0)
                    return peaks;

                float range = max - min;

                for (int i = 0; i < peaks.Length; i++)
                {
                    float value;

                    if (range > 0.015f)
                        value = 0.08f + ((peaks[i] - min) / range) * 0.86f;
                    else
                        value = 0.18f + (peaks[i] / max) * 0.62f;

                    if (value < 0.03f)
                        value = 0.03f;
                    if (value > 0.98f)
                        value = 0.98f;

                    peaks[i] = value;
                }

                return peaks;
            }

            private static float[] BuildWavPeaks(string path, int desiredPeaks)
            {
                try
                {
                    WaveInfo info = WaveInfo.Read(path);

                    if (info.DataOffset <= 0 || info.DataBytes <= 0 || info.BlockAlign <= 0)
                        return new float[0];

                    if (info.AudioFormat != 1 && info.AudioFormat != 3)
                        return new float[0];

                    int bytesPerSample = info.BitsPerSample / 8;

                    if (bytesPerSample <= 0)
                        return new float[0];

                    long totalFrames = info.DataBytes / info.BlockAlign;

                    if (totalFrames <= 0)
                        return new float[0];

                    int peakCount = (int)Math.Min(desiredPeaks, totalFrames);
                    peakCount = Math.Max(1, peakCount);

                    float[] peaks = new float[peakCount];

                    using (FileStream fs = File.OpenRead(path))
                    {
                        byte[] frame = new byte[info.BlockAlign];

                        for (int i = 0; i < peakCount; i++)
                        {
                            long startFrame = i * totalFrames / peakCount;
                            long endFrame = (i + 1) * totalFrames / peakCount;
                            long framesInBucket = Math.Max(1, endFrame - startFrame);
                            long step = Math.Max(1, framesInBucket / 180);

                            float max = 0;

                            for (long f = startFrame; f < endFrame; f += step)
                            {
                                long pos = info.DataOffset + f * info.BlockAlign;

                                if (pos < 0 || pos + info.BlockAlign > fs.Length)
                                    break;

                                fs.Position = pos;

                                int read = fs.Read(frame, 0, frame.Length);

                                if (read < frame.Length)
                                    break;

                                for (int ch = 0; ch < info.Channels; ch++)
                                {
                                    int offset = ch * bytesPerSample;

                                    if (offset + bytesPerSample > frame.Length)
                                        continue;

                                    float sample = Math.Abs(ReadSample(frame, offset, info.AudioFormat, info.BitsPerSample));

                                    if (sample > max)
                                        max = sample;
                                }
                            }

                            if (max > 1)
                                max = 1;

                            peaks[i] = max;
                        }
                    }

                    return peaks;
                }
                catch
                {
                    return new float[0];
                }
            }

            internal static float ReadSample(byte[] data, int offset, short audioFormat, short bitsPerSample)
            {
                if (audioFormat == 3 && bitsPerSample == 32)
                    return BitConverter.ToSingle(data, offset);

                if (bitsPerSample == 8)
                    return (data[offset] - 128) / 128f;

                if (bitsPerSample == 16)
                    return BitConverter.ToInt16(data, offset) / 32768f;

                if (bitsPerSample == 24)
                {
                    int value = data[offset] |
                                (data[offset + 1] << 8) |
                                (data[offset + 2] << 16);

                    if ((value & 0x800000) != 0)
                        value |= unchecked((int)0xFF000000);

                    return value / 8388608f;
                }

                if (bitsPerSample == 32)
                    return BitConverter.ToInt32(data, offset) / 2147483648f;

                return 0;
            }
        }

        private static class MediaFoundationAudio
        {
            private const int MF_VERSION = 0x00020070;
            private const int MF_SOURCE_READERF_ENDOFSTREAM = 0x00000002;
            private const int MF_SOURCE_READER_CONTROLF_NONE = 0x00000000;
            private static readonly int MF_SOURCE_READER_FIRST_AUDIO_STREAM = unchecked((int)0xFFFFFFFD);
            private static readonly int MF_SOURCE_READER_ALL_STREAMS = unchecked((int)0xFFFFFFFE);

            private static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00AA00389B71");
            private static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71");
            private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
            private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
            private static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
            private static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534d09d4acb");
            private static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

            [DllImport("mfplat.dll", ExactSpelling = true)]
            private static extern int MFStartup(int version, int dwFlags);

            [DllImport("mfplat.dll", ExactSpelling = true)]
            private static extern int MFShutdown();

            [DllImport("mfplat.dll", ExactSpelling = true)]
            private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

            [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
            private static extern int MFCreateSourceReaderFromURL(string pwszURL, IMFAttributes pAttributes, out IMFSourceReader ppSourceReader);

            public static AudioInfo ReadInfo(string path)
            {
                IMFSourceReader reader = null;
                IMFMediaType currentType = null;
                bool started = false;

                try
                {
                    Check(MFStartup(MF_VERSION, 0), "MFStartup");
                    started = true;

                    Check(MFCreateSourceReaderFromURL(path, null, out reader), "MFCreateSourceReaderFromURL");
                    ConfigurePcm(reader);

                    Check(reader.GetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, out currentType), "GetCurrentMediaType");

                    AudioInfo info = new AudioInfo();
                    info.Channels = (short)GetUInt32(currentType, MF_MT_AUDIO_NUM_CHANNELS, 0);
                    info.SampleRate = GetUInt32(currentType, MF_MT_AUDIO_SAMPLES_PER_SECOND, 0);
                    info.BitsPerSample = (short)GetUInt32(currentType, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
                    info.DurationMs = MciAudioInfo.GetDurationMs(path);
                    info.FormatName = AudioInfo.GuessFormatName(path);
                    return info;
                }
                finally
                {
                    Release(currentType);
                    Release(reader);

                    if (started)
                        MFShutdown();
                }
            }

            public static float[] BuildPeaks(string path, int desiredPeaks)
            {
                IMFSourceReader reader = null;
                IMFMediaType currentType = null;
                IMFSample sample = null;
                IMFMediaBuffer mediaBuffer = null;
                bool started = false;

                try
                {
                    Check(MFStartup(MF_VERSION, 0), "MFStartup");
                    started = true;

                    Check(MFCreateSourceReaderFromURL(path, null, out reader), "MFCreateSourceReaderFromURL");
                    ConfigurePcm(reader);

                    Check(reader.GetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, out currentType), "GetCurrentMediaType");

                    int channels = Math.Max(1, GetUInt32(currentType, MF_MT_AUDIO_NUM_CHANNELS, 1));
                    int sampleRate = Math.Max(1, GetUInt32(currentType, MF_MT_AUDIO_SAMPLES_PER_SECOND, 44100));
                    int bitsPerSample = GetUInt32(currentType, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);

                    if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                        bitsPerSample = 16;

                    int bytesPerSample = Math.Max(1, bitsPerSample / 8);
                    int blockAlign = Math.Max(1, channels * bytesPerSample);
                    long durationMs = MciAudioInfo.GetDurationMs(path);
                    long totalFrames = durationMs > 0 ? durationMs * sampleRate / 1000 : sampleRate * 60L;

                    if (totalFrames <= 0)
                        totalFrames = sampleRate * 60L;

                    int peakCount = (int)Math.Min(desiredPeaks, totalFrames);
                    peakCount = Math.Max(1, peakCount);

                    float[] peaks = new float[peakCount];
                    long frameIndex = 0;

                    while (true)
                    {
                        Release(mediaBuffer);
                        mediaBuffer = null;
                        Release(sample);
                        sample = null;

                        int actualStreamIndex;
                        int streamFlags;
                        long timestamp;

                        Check(reader.ReadSample(
                            MF_SOURCE_READER_FIRST_AUDIO_STREAM,
                            MF_SOURCE_READER_CONTROLF_NONE,
                            out actualStreamIndex,
                            out streamFlags,
                            out timestamp,
                            out sample), "ReadSample");

                        if ((streamFlags & MF_SOURCE_READERF_ENDOFSTREAM) != 0)
                            break;

                        if (sample == null)
                            continue;

                        Check(sample.ConvertToContiguousBuffer(out mediaBuffer), "ConvertToContiguousBuffer");

                        IntPtr audioData;
                        int maxLength;
                        int currentLength;

                        Check(mediaBuffer.Lock(out audioData, out maxLength, out currentLength), "MediaBuffer.Lock");

                        try
                        {
                            if (currentLength <= 0)
                                continue;

                            byte[] bytes = new byte[currentLength];
                            Marshal.Copy(audioData, bytes, 0, currentLength);

                            int frames = currentLength / blockAlign;

                            for (int frame = 0; frame < frames; frame++)
                            {
                                int peakIndex = (int)(frameIndex * peakCount / totalFrames);

                                if (peakIndex < 0)
                                    peakIndex = 0;
                                if (peakIndex >= peakCount)
                                    peakIndex = peakCount - 1;

                                float max = 0;
                                int frameOffset = frame * blockAlign;

                                for (int ch = 0; ch < channels; ch++)
                                {
                                    int offset = frameOffset + ch * bytesPerSample;

                                    if (offset + bytesPerSample > bytes.Length)
                                        continue;

                                    float value = Math.Abs(WaveformAnalyzer.ReadSample(bytes, offset, 1, (short)bitsPerSample));

                                    if (value > max)
                                        max = value;
                                }

                                if (max > 1)
                                    max = 1;

                                if (max > peaks[peakIndex])
                                    peaks[peakIndex] = max;

                                frameIndex++;
                            }
                        }
                        finally
                        {
                            mediaBuffer.Unlock();
                        }
                    }

                    return peaks;
                }
                catch
                {
                    return new float[0];
                }
                finally
                {
                    Release(mediaBuffer);
                    Release(sample);
                    Release(currentType);
                    Release(reader);

                    if (started)
                        MFShutdown();
                }
            }

            private static void ConfigurePcm(IMFSourceReader reader)
            {
                IMFMediaType mediaType = null;

                try
                {
                    reader.SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, false);
                    reader.SetStreamSelection(MF_SOURCE_READER_FIRST_AUDIO_STREAM, true);

                    Check(MFCreateMediaType(out mediaType), "MFCreateMediaType");
                    SetGuid(mediaType, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
                    SetGuid(mediaType, MF_MT_SUBTYPE, MFAudioFormat_PCM);

                    Check(reader.SetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, IntPtr.Zero, mediaType), "SetCurrentMediaType");
                }
                finally
                {
                    Release(mediaType);
                }
            }

            private static void SetGuid(IMFAttributes attributes, Guid key, Guid value)
            {
                Guid localKey = key;
                Guid localValue = value;
                Check(attributes.SetGUID(ref localKey, ref localValue), "SetGUID");
            }

            private static int GetUInt32(IMFAttributes attributes, Guid key, int defaultValue)
            {
                try
                {
                    Guid localKey = key;
                    int value;
                    int hr = attributes.GetUINT32(ref localKey, out value);

                    if (hr < 0)
                        return defaultValue;

                    return value;
                }
                catch
                {
                    return defaultValue;
                }
            }

            private static void Check(int hr, string action)
            {
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);
            }

            private static void Release(object obj)
            {
                if (obj != null && Marshal.IsComObject(obj))
                {
                    try
                    {
                        Marshal.ReleaseComObject(obj);
                    }
                    catch
                    {
                    }
                }
            }

            [ComImport]
            [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IMFAttributes
            {
                [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
                [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
                [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
                [PreserveSig] int Compare(IMFAttributes pTheirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
                [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
                [PreserveSig] int GetUINT64(ref Guid guidKey, out long punValue);
                [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
                [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
                [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
                [PreserveSig] int GetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszValue, int cchBufSize, out int pcchLength);
                [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
                [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
                [PreserveSig] int GetBlob(ref Guid guidKey, byte[] pBuf, int cbBufSize, out int pcbBlobSize);
                [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ip, out int pcbSize);
                [PreserveSig] int InitFromBlob(byte[] pBuf, int cbBufSize);
                [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
                [PreserveSig] int DeleteItem(ref Guid guidKey);
                [PreserveSig] int DeleteAllItems();
                [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
                [PreserveSig] int SetUINT64(ref Guid guidKey, long unValue);
                [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
                [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
                [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
                [PreserveSig] int SetBlob(ref Guid guidKey, byte[] pBuf, int cbBufSize);
                [PreserveSig] int LockStore();
                [PreserveSig] int UnlockStore();
                [PreserveSig] int GetCount(out int pcItems);
                [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
                [PreserveSig] int CopyAllItems(IMFAttributes pDest);
            }

            [ComImport]
            [Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IMFMediaType : IMFAttributes
            {
                [PreserveSig] int GetMajorType(out Guid pguidMajorType);
                [PreserveSig] int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool pfCompressed);
                [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out int pdwFlags);
                [PreserveSig] int GetRepresentation(ref Guid guidRepresentation, out IntPtr ppvRepresentation);
                [PreserveSig] int FreeRepresentation(ref Guid guidRepresentation, IntPtr pvRepresentation);
            }

            [ComImport]
            [Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IMFSourceReader
            {
                [PreserveSig] int GetStreamSelection(int dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] out bool pfSelected);
                [PreserveSig] int SetStreamSelection(int dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] bool fSelected);
                [PreserveSig] int GetNativeMediaType(int dwStreamIndex, int dwMediaTypeIndex, out IMFMediaType ppMediaType);
                [PreserveSig] int GetCurrentMediaType(int dwStreamIndex, out IMFMediaType ppMediaType);
                [PreserveSig] int SetCurrentMediaType(int dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);
                [PreserveSig] int SetCurrentPosition(ref Guid guidTimeFormat, IntPtr varPosition);
                [PreserveSig] int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex, out int pdwStreamFlags, out long pllTimestamp, out IMFSample ppSample);
                [PreserveSig] int Flush(int dwStreamIndex);
                [PreserveSig] int GetServiceForStream(int dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
                [PreserveSig] int GetPresentationAttribute(int dwStreamIndex, ref Guid guidAttribute, IntPtr pvarAttribute);
            }

            [ComImport]
            [Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IMFSample : IMFAttributes
            {
                [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
                [PreserveSig] int SetSampleFlags(int dwSampleFlags);
                [PreserveSig] int GetSampleTime(out long phnsSampleTime);
                [PreserveSig] int SetSampleTime(long hnsSampleTime);
                [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
                [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
                [PreserveSig] int GetBufferCount(out int pdwBufferCount);
                [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
                [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
                [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
                [PreserveSig] int RemoveBufferByIndex(int dwIndex);
                [PreserveSig] int RemoveAllBuffers();
                [PreserveSig] int GetTotalLength(out int pcbTotalLength);
                [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
            }

            [ComImport]
            [Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IMFMediaBuffer
            {
                [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
                [PreserveSig] int Unlock();
                [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
                [PreserveSig] int SetCurrentLength(int cbCurrentLength);
                [PreserveSig] int GetMaxLength(out int pcbMaxLength);
            }
        }

        private static class WavEditor
        {
            public static void ReverseFrames(string input, string output)
            {
                WaveInfo info = WaveInfo.Read(input);
                EnsureEditable(info);
                EnsureSmallEnough(info);

                byte[] file = File.ReadAllBytes(input);
                byte[] data = new byte[(int)info.DataBytes];

                Buffer.BlockCopy(file, (int)info.DataOffset, data, 0, data.Length);

                int block = info.BlockAlign;
                int frames = data.Length / block;

                byte[] reversed = new byte[data.Length];

                for (int i = 0; i < frames; i++)
                {
                    Buffer.BlockCopy(data, i * block, reversed, (frames - 1 - i) * block, block);
                }

                int remainStart = frames * block;

                if (remainStart < data.Length)
                    Buffer.BlockCopy(data, remainStart, reversed, remainStart, data.Length - remainStart);

                Buffer.BlockCopy(reversed, 0, file, (int)info.DataOffset, reversed.Length);
                File.WriteAllBytes(output, file);
            }

            public static void ApplyFade(string input, string output, int fadeMs)
            {
                WaveInfo info = WaveInfo.Read(input);
                EnsureEditable(info);
                EnsureSmallEnough(info);

                byte[] file = File.ReadAllBytes(input);
                byte[] data = new byte[(int)info.DataBytes];

                Buffer.BlockCopy(file, (int)info.DataOffset, data, 0, data.Length);

                int frames = data.Length / info.BlockAlign;
                int fadeFrames = Math.Max(1, (int)(info.SampleRate * (fadeMs / 1000.0)));

                for (int f = 0; f < frames; f++)
                {
                    double gain = 1.0;

                    if (f < fadeFrames)
                        gain = Math.Min(gain, f / (double)fadeFrames);

                    int remain = frames - 1 - f;

                    if (remain < fadeFrames)
                        gain = Math.Min(gain, remain / (double)fadeFrames);

                    ApplyGainToFrame(data, f * info.BlockAlign, info, gain);
                }

                Buffer.BlockCopy(data, 0, file, (int)info.DataOffset, data.Length);
                File.WriteAllBytes(output, file);
            }

            public static void Normalize(string input, string output, double targetPeak)
            {
                WaveInfo info = WaveInfo.Read(input);
                EnsureEditable(info);
                EnsureSmallEnough(info);

                byte[] file = File.ReadAllBytes(input);
                byte[] data = new byte[(int)info.DataBytes];

                Buffer.BlockCopy(file, (int)info.DataOffset, data, 0, data.Length);

                double peak = FindPeak(data, info);

                if (peak < 0.000001)
                    throw new InvalidOperationException("此檔案幾乎是靜音，無法正規化。");

                double gain = targetPeak / peak;

                int frames = data.Length / info.BlockAlign;

                for (int f = 0; f < frames; f++)
                    ApplyGainToFrame(data, f * info.BlockAlign, info, gain);

                Buffer.BlockCopy(data, 0, file, (int)info.DataOffset, data.Length);
                File.WriteAllBytes(output, file);
            }

            private static void EnsureEditable(WaveInfo info)
            {
                if (info.AudioFormat != 1 && info.AudioFormat != 3)
                    throw new InvalidOperationException("目前只支援 PCM 或 32-bit IEEE Float WAV 編輯。");

                if (info.AudioFormat == 3 && info.BitsPerSample != 32)
                    throw new InvalidOperationException("Float WAV 目前只支援 32-bit。");

                if (info.BitsPerSample != 8 &&
                    info.BitsPerSample != 16 &&
                    info.BitsPerSample != 24 &&
                    info.BitsPerSample != 32)
                {
                    throw new InvalidOperationException("不支援的位元深度：" + info.BitsPerSample);
                }

                if (info.BlockAlign <= 0 || info.Channels <= 0 || info.DataBytes <= 0)
                    throw new InvalidOperationException("WAV 資料不完整，無法編輯。");
            }

            private static void EnsureSmallEnough(WaveInfo info)
            {
                if (info.DataOffset > int.MaxValue || info.DataBytes > int.MaxValue)
                    throw new InvalidOperationException("檔案太大，這個內建編輯器無法一次載入。");
            }

            private static double FindPeak(byte[] data, WaveInfo info)
            {
                double peak = 0;
                int frames = data.Length / info.BlockAlign;
                int bytesPerSample = info.BitsPerSample / 8;

                for (int f = 0; f < frames; f++)
                {
                    int frameOffset = f * info.BlockAlign;

                    for (int ch = 0; ch < info.Channels; ch++)
                    {
                        int offset = frameOffset + ch * bytesPerSample;

                        if (offset + bytesPerSample > data.Length)
                            continue;

                        double value = Math.Abs(ReadSample(data, offset, info));

                        if (value > peak)
                            peak = value;
                    }
                }

                return peak;
            }

            private static void ApplyGainToFrame(byte[] data, int frameOffset, WaveInfo info, double gain)
            {
                int bytesPerSample = info.BitsPerSample / 8;

                for (int ch = 0; ch < info.Channels; ch++)
                {
                    int offset = frameOffset + ch * bytesPerSample;

                    if (offset + bytesPerSample > data.Length)
                        continue;

                    double sample = ReadSample(data, offset, info);
                    WriteSample(data, offset, info, sample * gain);
                }
            }

            private static double ReadSample(byte[] data, int offset, WaveInfo info)
            {
                if (info.AudioFormat == 3 && info.BitsPerSample == 32)
                    return BitConverter.ToSingle(data, offset);

                if (info.BitsPerSample == 8)
                    return (data[offset] - 128) / 128.0;

                if (info.BitsPerSample == 16)
                    return BitConverter.ToInt16(data, offset) / 32768.0;

                if (info.BitsPerSample == 24)
                {
                    int value =
                        data[offset] |
                        (data[offset + 1] << 8) |
                        (data[offset + 2] << 16);

                    if ((value & 0x800000) != 0)
                        value |= unchecked((int)0xFF000000);

                    return value / 8388608.0;
                }

                if (info.BitsPerSample == 32)
                    return BitConverter.ToInt32(data, offset) / 2147483648.0;

                return 0;
            }

            private static void WriteSample(byte[] data, int offset, WaveInfo info, double sample)
            {
                if (sample > 1)
                    sample = 1;

                if (sample < -1)
                    sample = -1;

                if (info.AudioFormat == 3 && info.BitsPerSample == 32)
                {
                    byte[] bytes = BitConverter.GetBytes((float)sample);
                    Buffer.BlockCopy(bytes, 0, data, offset, 4);
                    return;
                }

                if (info.BitsPerSample == 8)
                {
                    int value = (int)Math.Round(sample * 127.0 + 128.0);
                    value = Math.Max(0, Math.Min(255, value));
                    data[offset] = (byte)value;
                    return;
                }

                if (info.BitsPerSample == 16)
                {
                    short value = sample <= -1
                        ? short.MinValue
                        : (short)Math.Round(sample * short.MaxValue);

                    byte[] bytes = BitConverter.GetBytes(value);
                    data[offset] = bytes[0];
                    data[offset + 1] = bytes[1];
                    return;
                }

                if (info.BitsPerSample == 24)
                {
                    int value = sample <= -1
                        ? -8388608
                        : (int)Math.Round(sample * 8388607.0);

                    data[offset] = (byte)(value & 0xFF);
                    data[offset + 1] = (byte)((value >> 8) & 0xFF);
                    data[offset + 2] = (byte)((value >> 16) & 0xFF);
                    return;
                }

                if (info.BitsPerSample == 32)
                {
                    int value = sample <= -1
                        ? int.MinValue
                        : (int)Math.Round(sample * int.MaxValue);

                    byte[] bytes = BitConverter.GetBytes(value);
                    Buffer.BlockCopy(bytes, 0, data, offset, 4);
                }
            }
        }

        private class SeekEventArgs : EventArgs
        {
            public long PositionMs;

            public SeekEventArgs(long positionMs)
            {
                PositionMs = positionMs;
            }
        }

        private class WaveformView : Control
        {
            private float[] _peaks;
            private long _durationMs;
            private long _positionMs;

            public event EventHandler<SeekEventArgs> SeekRequested;

            public long PositionMs
            {
                get { return _positionMs; }
                set
                {
                    _positionMs = value;
                    Invalidate();
                }
            }

            public WaveformView()
            {
                DoubleBuffered = true;
                BackColor = AppColor.Card3;
                Cursor = Cursors.Hand;
            }

            public void SetPeaks(float[] peaks, long durationMs)
            {
                _peaks = peaks;
                _durationMs = durationMs;
                _positionMs = 0;
                Invalidate();
            }

            public float GetPeakAt(long positionMs)
            {
                if (_peaks == null || _peaks.Length == 0 || _durationMs <= 0)
                    return 0.25f;

                int index = (int)(positionMs * _peaks.Length / _durationMs);
                index = Math.Max(0, Math.Min(_peaks.Length - 1, index));

                return Math.Max(0.08f, _peaks[index]);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                if (_durationMs <= 0 || Width <= 0)
                    return;

                long pos = (long)(_durationMs * (e.X / (double)Width));

                if (SeekRequested != null)
                    SeekRequested(this, new SeekEventArgs(pos));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle area = ClientRectangle;
                area.Inflate(-1, -1);

                using (GraphicsPath path = Ui.RoundRect(area, 12))
                using (SolidBrush bg = new SolidBrush(AppColor.Card3))
                {
                    e.Graphics.FillPath(bg, path);
                }

                if (_peaks == null)
                {
                    DrawCenterText(e.Graphics, "波形載入中 / 尚未播放");
                    return;
                }

                if (_peaks.Length == 0)
                {
                    DrawCenterText(e.Graphics, "此檔案無法產生波形預覽");
                    return;
                }

                int mid = Height / 2;
                int usableHeight = Math.Max(8, Height - 18);
                float xStep = Width / (float)_peaks.Length;

                using (Pen p = new Pen(AppColor.Accent2, Math.Max(1f, xStep)))
                {
                    for (int i = 0; i < _peaks.Length; i++)
                    {
                        float peak = Math.Max(0.02f, _peaks[i]);
                        float x = i * xStep;
                        int h = (int)(peak * usableHeight / 2);
                        e.Graphics.DrawLine(p, x, mid - h, x, mid + h);
                    }
                }

                if (_durationMs > 0)
                {
                    float progressX = (float)(Width * (_positionMs / (double)_durationMs));

                    using (SolidBrush overlay = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                        e.Graphics.FillRectangle(overlay, 0, 0, progressX, Height);

                    using (Pen p = new Pen(Color.White, 2))
                        e.Graphics.DrawLine(p, progressX, 6, progressX, Height - 6);
                }
            }

            private void DrawCenterText(Graphics g, string text)
            {
                TextRenderer.DrawText(
                    g,
                    text,
                    Font,
                    ClientRectangle,
                    AppColor.SubText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                );
            }
        }

        private class VisualizerView : Control
        {
            private readonly Random _rng = new Random();
            private float _level;

            public float Level
            {
                get { return _level; }
                set
                {
                    _level = Math.Max(0, Math.Min(1, value));
                    Invalidate();
                }
            }

            public VisualizerView()
            {
                DoubleBuffered = true;
                BackColor = AppColor.Card3;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle area = ClientRectangle;
                area.Inflate(-1, -1);

                using (GraphicsPath path = Ui.RoundRect(area, 12))
                using (SolidBrush bg = new SolidBrush(AppColor.Card3))
                    e.Graphics.FillPath(bg, path);

                int bars = 22;
                int gap = 4;
                int barWidth = Math.Max(3, (Width - gap * (bars + 1)) / bars);
                int maxH = Math.Max(8, Height - 20);

                for (int i = 0; i < bars; i++)
                {
                    float randomPart = (float)_rng.NextDouble();
                    float v = _level <= 0.01f ? 0.05f : (_level * 0.45f + randomPart * _level * 0.75f);
                    int h = Math.Max(4, (int)(v * maxH));
                    int x = gap + i * (barWidth + gap);
                    int y = Height - 10 - h;

                    Color color = Color.FromArgb(
                        210,
                        80 + Math.Min(120, i * 5),
                        130,
                        255
                    );

                    using (SolidBrush b = new SolidBrush(color))
                        e.Graphics.FillRectangle(b, x, y, barWidth, h);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    "LIVE",
                    new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold),
                    new Rectangle(8, 6, Width - 16, 18),
                    AppColor.SubText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                );
            }
        }

        private class MiniBarChart : Control
        {
            private Dictionary<string, int> _data = new Dictionary<string, int>();
            private string _title = "";

            public MiniBarChart()
            {
                DoubleBuffered = true;
                BackColor = AppColor.Card;
            }

            public void SetData(Dictionary<string, int> data, string title)
            {
                _data = data ?? new Dictionary<string, int>();
                _title = title ?? "";
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = ClientRectangle;
                rect.Inflate(-1, -1);

                using (GraphicsPath path = Ui.RoundRect(rect, 18))
                using (SolidBrush bg = new SolidBrush(AppColor.Card))
                using (Pen border = new Pen(AppColor.Border))
                {
                    e.Graphics.FillPath(bg, path);
                    e.Graphics.DrawPath(border, path);
                }

                Rectangle titleRect = new Rectangle(22, 14, Width - 44, 28);

                TextRenderer.DrawText(
                    e.Graphics,
                    _title,
                    new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold),
                    titleRect,
                    AppColor.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                );

                if (_data == null || _data.Count == 0)
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        "目前沒有資料",
                        Font,
                        ClientRectangle,
                        AppColor.SubText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    );
                    return;
                }

                int max = Math.Max(1, _data.Values.Max());
                int top = 60;
                int left = 24;
                int right = Width - 24;
                int bottom = Height - 24;
                int barAreaWidth = right - left;
                int rowHeight = 34;

                int i = 0;

                foreach (var pair in _data.Take(8))
                {
                    int y = top + i * rowHeight;

                    if (y + rowHeight > bottom)
                        break;

                    int barWidth = (int)(barAreaWidth * (pair.Value / (double)max));

                    Rectangle barRect = new Rectangle(left, y + 7, Math.Max(4, barWidth), 18);

                    using (LinearGradientBrush brush = new LinearGradientBrush(barRect, AppColor.Accent, AppColor.Accent2, LinearGradientMode.Horizontal))
                        e.Graphics.FillRectangle(brush, barRect);

                    TextRenderer.DrawText(
                        e.Graphics,
                        pair.Key + "　" + pair.Value,
                        Font,
                        new Rectangle(left + 8, y, barAreaWidth - 16, rowHeight),
                        Color.White,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    );

                    i++;
                }
            }
        }

        private class ModernButton : Button
        {
            public bool Primary = false;
            private bool _hover = false;
            private bool _down = false;

            public ModernButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                ForeColor = Color.White;
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hover = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = false;
                _down = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs mevent)
            {
                _down = true;
                Invalidate();
                base.OnMouseDown(mevent);
            }

            protected override void OnMouseUp(MouseEventArgs mevent)
            {
                _down = false;
                Invalidate();
                base.OnMouseUp(mevent);
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = ClientRectangle;
                rect.Inflate(-1, -1);

                Color c1 = Primary ? AppColor.Accent : AppColor.Button;
                Color c2 = Primary ? AppColor.Accent2 : AppColor.Button2;

                if (_hover)
                {
                    c1 = Ui.Lighten(c1, 20);
                    c2 = Ui.Lighten(c2, 20);
                }

                if (_down)
                {
                    c1 = Ui.Darken(c1, 20);
                    c2 = Ui.Darken(c2, 20);
                }

                using (GraphicsPath path = Ui.RoundRect(rect, 13))
                using (LinearGradientBrush b = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Horizontal))
                {
                    pevent.Graphics.FillPath(b, path);
                }

                TextRenderer.DrawText(
                    pevent.Graphics,
                    Text,
                    Font,
                    rect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                );
            }
        }

        private class CardPanel : Panel
        {
            public int Radius = 22;

            public CardPanel()
            {
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Padding = new Padding(1);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = ClientRectangle;
                rect.Inflate(-1, -1);

                using (GraphicsPath path = Ui.RoundRect(rect, Radius))
                using (SolidBrush b = new SolidBrush(AppColor.Card))
                using (Pen p = new Pen(AppColor.Border))
                {
                    e.Graphics.FillPath(b, path);
                    e.Graphics.DrawPath(p, path);
                }

                base.OnPaint(e);
            }
        }

        private class GradientPanel : Panel
        {
            public Color Color1 = AppColor.Accent;
            public Color Color2 = AppColor.Accent2;
            public int Radius = 22;

            public GradientPanel()
            {
                DoubleBuffered = true;
                BackColor = Color.Transparent;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = ClientRectangle;
                rect.Inflate(-1, -1);

                using (GraphicsPath path = Ui.RoundRect(rect, Radius))
                using (LinearGradientBrush b = new LinearGradientBrush(rect, Color1, Color2, LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillPath(b, path);
                }

                base.OnPaint(e);
            }
        }

        private static class Ui
        {
            public static GraphicsPath RoundRect(Rectangle rect, int radius)
            {
                GraphicsPath path = new GraphicsPath();

                int d = radius * 2;

                if (d > rect.Width)
                    d = rect.Width;

                if (d > rect.Height)
                    d = rect.Height;

                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                return path;
            }

            public static Color Lighten(Color c, int amount)
            {
                return Color.FromArgb(
                    c.A,
                    Math.Min(255, c.R + amount),
                    Math.Min(255, c.G + amount),
                    Math.Min(255, c.B + amount)
                );
            }

            public static Color Darken(Color c, int amount)
            {
                return Color.FromArgb(
                    c.A,
                    Math.Max(0, c.R - amount),
                    Math.Max(0, c.G - amount),
                    Math.Max(0, c.B - amount)
                );
            }
        }

        private static class AppColor
        {
            public static readonly Color Bg = Color.FromArgb(15, 18, 28);
            public static readonly Color Card = Color.FromArgb(25, 30, 45);
            public static readonly Color Card2 = Color.FromArgb(31, 37, 55);
            public static readonly Color Card3 = Color.FromArgb(38, 45, 65);
            public static readonly Color Input = Color.FromArgb(20, 24, 36);
            public static readonly Color Border = Color.FromArgb(55, 63, 85);

            public static readonly Color Text = Color.FromArgb(235, 238, 248);
            public static readonly Color SubText = Color.FromArgb(160, 170, 195);

            public static readonly Color Accent = Color.FromArgb(91, 118, 255);
            public static readonly Color Accent2 = Color.FromArgb(84, 210, 255);
            public static readonly Color Button = Color.FromArgb(52, 61, 86);
            public static readonly Color Button2 = Color.FromArgb(42, 49, 70);
            public static readonly Color Selected = Color.FromArgb(68, 86, 145);
            public static readonly Color Warning = Color.FromArgb(255, 205, 85);
        }
    }
}