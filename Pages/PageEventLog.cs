using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using he_dieu_hanh.Models;
using he_dieu_hanh.Services;

namespace he_dieu_hanh.Pages
{
    public partial class PageEventLog : UserControl
    {
        private FlowLayoutPanel topPanel;
        private Panel bottomPanel;
        private DataGridView dgvEvents;
        private Label lblStatus;
        private ProgressBar progressReplay;
        private Button btnStartRecord, btnStopRecord, btnSaveLog, btnLoadLog, btnClearLog, btnReplayAll;

        // Services
        private InputRecorder _recorder;
        private InputPlayer _player;
        
        // State
        private bool isRecording = false;
        private bool isReplaying = false;
        private bool isPaused = false;
        private CancellationTokenSource? replayCancellation;
        private List<RecordedEvent> recordedEvents = new List<RecordedEvent>();
        private DateTime recordingStartTime;

        // Pause Hook (Giữ lại ở đây để điều khiển UI loop khi Replay)
        private IntPtr _hookIDKeyboard = IntPtr.Zero;
        private Win32Api.LowLevelKeyboardProc _procKeyboard;

        public PageEventLog()
        {
            this.Dock = DockStyle.Fill;
            InitializeUI();
            ApplyTheme();

            _recorder = new InputRecorder();
            _player = new InputPlayer();
            _recorder.OnEventRecorded += OnEventRecorded;
        }

        private void InitializeUI()
        {
            // === KHỞI TẠO CÁC CONTROL UI ===
            topPanel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ThemeManager.PanelColor
            };

            btnStartRecord = CreateButton(" Start Record", Color.FromArgb(52, 152, 219));
            btnStopRecord = CreateButton(" Stop Record", Color.FromArgb(231, 76, 60));
            btnSaveLog = CreateButton(" Save Log", Color.FromArgb(46, 204, 113));
            btnLoadLog = CreateButton(" Load Log", Color.FromArgb(241, 196, 15));
            btnClearLog = CreateButton(" Clear Log", Color.FromArgb(127, 140, 141));
            btnReplayAll = CreateButton(" Replay All", Color.FromArgb(155, 89, 182));

            topPanel.Controls.AddRange(new Control[]
            {
                btnStartRecord, btnStopRecord, btnSaveLog, btnLoadLog, btnClearLog, btnReplayAll
            });

            dgvEvents = new DataGridView()
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = ThemeManager.BackgroundColor,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 10),
                ForeColor = ThemeManager.ForegroundColor,
                GridColor = Color.FromArgb(200, 200, 200)
            };

            dgvEvents.Columns.Add("colIndex", "#");
            dgvEvents.Columns.Add("colTime", "Time");
            dgvEvents.Columns.Add("colType", "Type");
            dgvEvents.Columns.Add("colDetails", "Details");

            var replayColumn = new DataGridViewButtonColumn()
            {
                HeaderText = "Replay",
                Name = "colReplay",
                Text = " Replay",
                UseColumnTextForButtonValue = true,
                Width = 100
            };
            dgvEvents.Columns.Add(replayColumn);

            dgvEvents.CellClick += DgvEvents_CellClick;
            dgvEvents.CellFormatting += DgvEvents_CellFormatting;
            dgvEvents.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvEvents.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220);
            dgvEvents.EnableHeadersVisualStyles = false;

            bottomPanel = new Panel()
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                Padding = new Padding(10),
                BackColor = ThemeManager.PanelColor
            };

            lblStatus = new Label()
            {
                Text = "Status: Ready",
                Dock = DockStyle.Left,
                Width = 350,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = ThemeManager.ForegroundColor
            };

            progressReplay = new ProgressBar()
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            bottomPanel.Controls.Add(progressReplay);
            bottomPanel.Controls.Add(lblStatus);

            this.Controls.Add(dgvEvents);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            AddButtonEffects();

            btnStopRecord.Enabled = false;
            UpdateReplayButtonState();
        }

        public void ApplyTheme()
        {
            this.BackColor = ThemeManager.BackgroundColor;
            if (topPanel != null) topPanel.BackColor = ThemeManager.PanelColor;
            if (bottomPanel != null) bottomPanel.BackColor = ThemeManager.PanelColor;
            if (lblStatus != null) lblStatus.ForeColor = ThemeManager.ForegroundColor;

            if (dgvEvents != null)
            {
                dgvEvents.BackgroundColor = ThemeManager.BackgroundColor;
                dgvEvents.ForeColor = ThemeManager.ForegroundColor;
                dgvEvents.GridColor = ThemeManager.IsDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
                
                dgvEvents.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(220, 220, 220);
                dgvEvents.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.IsDarkMode ? Color.White : Color.Black;
                
                dgvEvents.DefaultCellStyle.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(40, 44, 52) : Color.White;
                dgvEvents.DefaultCellStyle.ForeColor = ThemeManager.IsDarkMode ? Color.WhiteSmoke : Color.Black;
                dgvEvents.DefaultCellStyle.SelectionBackColor = ThemeManager.IsDarkMode ? Color.FromArgb(70, 70, 80) : SystemColors.Highlight;
                dgvEvents.DefaultCellStyle.SelectionForeColor = ThemeManager.IsDarkMode ? Color.White : SystemColors.HighlightText;
            }
        }

        private void UpdateReplayButtonState()
        {
            btnReplayAll.Enabled = !isRecording && !isReplaying && recordedEvents.Count > 0;
        }

        private void DgvEvents_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvEvents.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
            {
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.BackColor = Color.FromArgb(52, 152, 219);
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private Button CreateButton(string text, Color baseColor)
        {
            var btn = new Button()
            {
                Text = text,
                ForeColor = Color.White,
                BackColor = baseColor,
                Width = 130,
                Height = 40,
                Margin = new Padding(5),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.Region = Region.FromHrgn(Win32Api.CreateRoundRectRgn(0, 0, btn.Width, btn.Height, 15, 15));
            return btn;
        }

        private void AddButtonEffects()
        {
            foreach (Control control in topPanel.Controls)
            {
                if (control is Button btn)
                {
                    btn.MouseEnter += (s, e) => { btn.BackColor = ControlPaint.Light(btn.BackColor); btn.Cursor = Cursors.Hand; };
                    btn.MouseLeave += (s, e) => { btn.BackColor = ControlPaint.Dark(btn.BackColor, 0.1f); btn.Cursor = Cursors.Default; };
                    btn.Click += Btn_Click;
                }
            }
        }

        private async void Btn_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                switch (btn.Text)
                {
                    case " Start Record": StartRecording(); break;
                    case " Stop Record": StopRecording(); break;
                    case " Clear Log": ClearLog(); break;
                    case " Save Log": SaveLogToFile(); break;
                    case " Load Log": LoadLogFromFile(); break;
                    case " Replay All": await ReplayAllEvents(); break;
                }
            }
        }

        private void OnEventRecorded(RecordedEvent evt)
        {
            recordedEvents.Add(evt);
            EventLogger.TotalEventCount = recordedEvents.Count;
            if (evt.EventType.StartsWith("Mouse")) EventLogger.MouseEventsCount++;
            else if (evt.EventType == "KeyDown") EventLogger.KeyDownCount++;
            else if (evt.EventType == "KeyUp") EventLogger.KeyUpCount++;

            if (this.InvokeRequired) this.BeginInvoke(new Action(() => lblStatus.Text = $"Recording... Events: {recordedEvents.Count}"));
            else lblStatus.Text = $"Recording... Events: {recordedEvents.Count}";
        }

        private void StartRecording()
        {
            if (isRecording) return;
            isRecording = true;
            EventLogger.IsRecording = true;
            EventLogger.RecordingStartTime = DateTime.Now;
            recordingStartTime = EventLogger.RecordingStartTime;
            recordedEvents.Clear();
            dgvEvents.Rows.Clear();

            _recorder.Start();

            btnStartRecord.Enabled = false;
            btnStopRecord.Enabled = true;
            UpdateReplayButtonState();
            lblStatus.Text = "Status: Recording... (UI updates paused for performance)";
        }

        private void StopRecording()
        {
            if (!isRecording) return;
            isRecording = false;
            EventLogger.IsRecording = false;
            
            _recorder.Stop();

            btnStartRecord.Enabled = true;
            btnStopRecord.Enabled = false;
            UpdateReplayButtonState();
            lblStatus.Text = "Status: Processing data...";
            LoadEventsToGrid();
            lblStatus.Text = $"Status: Recording stopped. {recordedEvents.Count} events recorded.";
        }

        private void LoadEventsToGrid()
        {
            dgvEvents.Rows.Clear();
            dgvEvents.SuspendLayout();
            foreach (var evt in recordedEvents)
            {
                var details = string.Join(", ", evt.Data.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                var displayTime = recordingStartTime.AddMilliseconds(evt.Timestamp);
                dgvEvents.Rows.Add(dgvEvents.Rows.Count + 1, displayTime.ToString("HH:mm:ss.fff"), evt.EventType, details);
            }
            dgvEvents.ResumeLayout();
            if (dgvEvents.Rows.Count > 0) dgvEvents.FirstDisplayedScrollingRowIndex = dgvEvents.Rows.Count - 1;
        }

        private async void DgvEvents_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvEvents.Columns[e.ColumnIndex].Name == "colReplay")
            {
                if (isRecording || isReplaying) return;
                if (e.RowIndex < recordedEvents.Count)
                {
                    await _player.PlayEvent(recordedEvents[e.RowIndex]);
                }
            }
        }

        private async Task ReplayAllEvents()
        {
            if (isRecording || isReplaying || recordedEvents.Count == 0) return;

            isReplaying = true;
            isPaused = false;
            replayCancellation = new CancellationTokenSource();
            btnReplayAll.Enabled = false;
            btnStartRecord.Enabled = false;

            // Setup hook for Pause key (P) - Chỉ dùng cho UI logic này
            _procKeyboard = (nCode, wParam, lParam) =>
            {
                if (nCode >= 0 && (wParam == (IntPtr)Win32Api.WM_KEYDOWN))
                {
                    if ((Keys)Marshal.ReadInt32(lParam) == Keys.P)
                    {
                        isPaused = !isPaused;
                        string msg = isPaused ? "Status: Replay PAUSED (Press P to resume)" : "Status: Replaying...";
                        this.BeginInvoke(new Action(() => lblStatus.Text = msg));
                        return (IntPtr)1;
                    }
                }
                return Win32Api.CallNextHookEx(_hookIDKeyboard, nCode, wParam, lParam);
            };
            
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookIDKeyboard = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _procKeyboard, Win32Api.GetModuleHandle(curModule.ModuleName), 0);
            }

            try
            {
                progressReplay.Maximum = recordedEvents.Count;
                progressReplay.Value = 0;
                lblStatus.Text = "Status: Replaying events... (Press P to Pause)";

                for (int i = 0; i < recordedEvents.Count; i++)
                {
                    while (isPaused) { if (replayCancellation.Token.IsCancellationRequested) break; await Task.Delay(100); }
                    if (replayCancellation.Token.IsCancellationRequested) break;

                    var evt = recordedEvents[i];
                    if (evt.DelayMs > 0) await Task.Delay(evt.DelayMs, replayCancellation.Token);

                    await _player.PlayEvent(evt, false);
                    progressReplay.Value = i + 1;
                    if (!isPaused) lblStatus.Text = $"Status: Replaying {i + 1}/{recordedEvents.Count}...";
                }
                lblStatus.Text = "Status: Replay completed.";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally
            {
                Win32Api.UnhookWindowsHookEx(_hookIDKeyboard);
                _hookIDKeyboard = IntPtr.Zero;
                isReplaying = false;
                btnReplayAll.Enabled = true;
                btnStartRecord.Enabled = true;
                UpdateReplayButtonState();
            }
        }

        private void SaveLogToFile()
        {
            if (recordedEvents.Count == 0) return;
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "JSON Files (*.json)|*.json", FileName = $"event_log_{DateTime.Now:yyyyMMdd_HHmmss}.json" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(recordedEvents, new JsonSerializerOptions { WriteIndented = true }));
                    lblStatus.Text = $"Status: Saved to {Path.GetFileName(sfd.FileName)}";
                }
            }
        }

        private void LoadLogFromFile()
        {
            if (isRecording || isReplaying) return;
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "JSON Files (*.json)|*.json" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        recordedEvents = JsonSerializer.Deserialize<List<RecordedEvent>>(File.ReadAllText(ofd.FileName));
                        LoadEventsToGrid();
                        UpdateReplayButtonState();
                        lblStatus.Text = $"Status: Loaded {recordedEvents.Count} events.";
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void ClearLog()
        {
            if (isRecording) return;
            recordedEvents.Clear();
            dgvEvents.Rows.Clear();
            UpdateReplayButtonState();
            lblStatus.Text = "Status: Log cleared";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _recorder?.Dispose();
                replayCancellation?.Cancel();
                if (_hookIDKeyboard != IntPtr.Zero) Win32Api.UnhookWindowsHookEx(_hookIDKeyboard);
            }
            base.Dispose(disposing);
        }
    }
}
