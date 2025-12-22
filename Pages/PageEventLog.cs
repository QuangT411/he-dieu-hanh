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
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace he_dieu_hanh.Pages
{
    // Event data structure
    public class RecordedEvent
    {
        public string EventType { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public int DelayMs { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    public partial class PageEventLog : UserControl
    {
        private FlowLayoutPanel topPanel;
        private Panel bottomPanel;
        private DataGridView dgvEvents;
        private Label lblStatus;
        private ProgressBar progressReplay;

        private Button btnStartRecord, btnStopRecord, btnSaveLog, btnLoadLog, btnClearLog, btnReplayAll;

        // Windows Hook API (Native)
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private static IntPtr _hookIDKeyboard = IntPtr.Zero;
        private static IntPtr _hookIDMouse = IntPtr.Zero;
        private LowLevelKeyboardProc _procKeyboard;
        private LowLevelMouseProc _procMouse;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Hook Structs
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Mouse Constants
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;

        // Keyboard Constants
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private bool isRecording = false;
        private DateTime recordingStartTime;
        private List<RecordedEvent> recordedEvents = new List<RecordedEvent>();
        private RecordedEvent? lastEvent = null;

        // Replay
        private bool isReplaying = false;
        private bool isPaused = false;
        private CancellationTokenSource? replayCancellation;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse
        );

        // Windows API for replaying input
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mouse;
            [FieldOffset(0)]
            public KEYBDINPUT keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        public PageEventLog()
        {
            this.Dock = DockStyle.Fill;
            InitializeUI();
            ApplyTheme();
        }

        private void InitializeUI()
        {
            // === KHỞI TẠO CÁC CONTROL UI ===

            // ==== THANH CÔNG CỤ TRÊN ====
            topPanel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ThemeManager.PanelColor
            };

            btnStartRecord = CreateButton("🟢 Start Record", Color.FromArgb(52, 152, 219));
            btnStopRecord = CreateButton("⛔ Stop Record", Color.FromArgb(231, 76, 60));
            btnSaveLog = CreateButton("💾 Save Log", Color.FromArgb(46, 204, 113));
            btnLoadLog = CreateButton("📂 Load Log", Color.FromArgb(241, 196, 15));
            btnClearLog = CreateButton("🧹 Clear Log", Color.FromArgb(127, 140, 141));
            btnReplayAll = CreateButton("▶️ Replay All", Color.FromArgb(155, 89, 182));

            topPanel.Controls.AddRange(new Control[]
            {
                btnStartRecord, btnStopRecord, btnSaveLog, btnLoadLog, btnClearLog, btnReplayAll
            });

            // ==== BẢNG LOG (DataGridView) ====
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

            // Thiết lập Cột
            dgvEvents.Columns.Add("colIndex", "#");
            dgvEvents.Columns.Add("colTime", "Time");
            dgvEvents.Columns.Add("colType", "Type");
            dgvEvents.Columns.Add("colDetails", "Details");

            // Cột nút Replay
            var replayColumn = new DataGridViewButtonColumn()
            {
                HeaderText = "Replay",
                Name = "colReplay",
                Text = "▶️ Replay",
                UseColumnTextForButtonValue = true,
                Width = 100
            };
            dgvEvents.Columns.Add(replayColumn);

            // Gắn sự kiện để xử lý nhấp nút Replay
            dgvEvents.CellClick += DgvEvents_CellClick;

            // Định dạng Cell/Header (UI/UX)
            dgvEvents.CellFormatting += DgvEvents_CellFormatting;
            dgvEvents.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvEvents.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220);
            dgvEvents.EnableHeadersVisualStyles = false;

            // ==== THANH TRẠNG THÁI ====
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

            // === THÊM CONTROLS VÀO USER CONTROL ===
            this.Controls.Add(dgvEvents);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            // === HIỆU ỨNG NÚT ===
            AddButtonEffects();

            // Initialize button states
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

        // ============================================================
        // 🛠 HELPER: Xử lý chuyển đổi kiểu dữ liệu an toàn (JSON fix)
        // ============================================================
        private int GetIntFromData(object data)
        {
            if (data == null) return 0;

            // Trường hợp 1: Dữ liệu chưa qua JSON (vừa record xong)
            if (data is int i) return i;
            if (data is long l) return (int)l;

            // Trường hợp 2: Dữ liệu load từ file JSON (nó là JsonElement)
            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number)
                {
                    return element.GetInt32();
                }
            }

            // Cố gắng convert ép kiểu nếu các trường hợp trên không khớp
            try
            {
                return Convert.ToInt32(data);
            }
            catch
            {
                return 0;
            }
        }

        // Helper: Lấy chuỗi từ Data dictionary (hỗ trợ JSON và object)
        private string? GetStringFromData(Dictionary<string, object> data, string key)
        {
            if (!data.ContainsKey(key)) return null;

            var value = data[key];
            if (value == null) return null;

            if (value is string s) return s;

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString();
                }
            }

            return value.ToString();
        }
        // ============================================================

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

        // Replay individual event
        private async void DgvEvents_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Kiểm tra xem có bấm đúng vào cột nút "Replay" không
            if (e.RowIndex >= 0 && dgvEvents.Columns[e.ColumnIndex].Name == "colReplay")
            {
                // Chặn nếu đang record hoặc đang replay all
                if (isRecording || isReplaying)
                {
                    MessageBox.Show("Please stop recording/replaying first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (e.RowIndex < recordedEvents.Count)
                {
                    // --- LOGIC: CHỈ LẤY ĐÚNG 1 EVENT ĐỂ CHẠY ---
                    var eventToReplay = recordedEvents[e.RowIndex];

                    // Gọi hàm ReplayEvent (hàm này chỉ gửi lệnh Input, không có vòng lặp)
                    await ReplayEvent(eventToReplay);
                }
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
            btn.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btn.Width, btn.Height, 15, 15));
            return btn;
        }

        private void AddButtonEffects()
        {
            foreach (Control control in topPanel.Controls)
            {
                if (control is Button btn)
                {
                    // Hiệu ứng Hover (UX)
                    btn.MouseEnter += (s, e) =>
                    {
                        btn.BackColor = ControlPaint.Light(btn.BackColor);
                        btn.Cursor = Cursors.Hand;
                    };
                    btn.MouseLeave += (s, e) =>
                    {
                        btn.BackColor = ControlPaint.Dark(btn.BackColor, 0.1f);
                        btn.Cursor = Cursors.Default;
                    };
                    // Xử lý Click
                    btn.Click += Btn_Click;
                }
            }
        }

        // Handle button clicks
        private async void Btn_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                switch (btn.Text)
                {
                    case "🟢 Start Record":
                        StartRecording();
                        break;
                    case "⛔ Stop Record":
                        StopRecording();
                        break;
                    case "🧹 Clear Log":
                        ClearLog();
                        break;
                    case "💾 Save Log":
                        SaveLogToFile();
                        break;
                    case "📂 Load Log":
                        LoadLogFromFile();
                        break;
                    case "▶️ Replay All":
                        await ReplayAllEvents();
                        break;
                }
            }
        }

        // Add event to UI (Old Direct Method - Used for Replay/Load only)
        private void AddEventToUI(string type, string details, DateTime timestamp)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, string, DateTime>(AddEventToUI), type, details, timestamp);
                return;
            }

            dgvEvents.Rows.Add(
                dgvEvents.Rows.Count + 1,
                timestamp.ToString("HH:mm:ss.fff"),
                type,
                details
            );

            if (dgvEvents.Rows.Count > 0)
            {
                dgvEvents.FirstDisplayedScrollingRowIndex = dgvEvents.Rows.Count - 1;
            }
        }

        // 🚀 NEW: Load toàn bộ events vào Grid sau khi Stop (Tối ưu hiệu suất)
        private void LoadEventsToGrid()
        {
            dgvEvents.Rows.Clear();
            dgvEvents.SuspendLayout(); // Tạm dừng vẽ để tăng tốc

            foreach (var evt in recordedEvents)
            {
                var details = new StringBuilder();
                foreach (var kvp in evt.Data)
                {
                    if (details.Length > 0) details.Append(", ");
                    details.Append($"{kvp.Key}: {kvp.Value}");
                }

                // Tính toán thời gian hiển thị tương đối
                var displayTime = recordingStartTime.AddMilliseconds(evt.Timestamp);

                dgvEvents.Rows.Add(
                    dgvEvents.Rows.Count + 1,
                    displayTime.ToString("HH:mm:ss.fff"),
                    evt.EventType,
                    details.ToString()
                );
            }

            dgvEvents.ResumeLayout(); // Vẽ lại 1 lần

            if (dgvEvents.Rows.Count > 0)
            {
                dgvEvents.FirstDisplayedScrollingRowIndex = dgvEvents.Rows.Count - 1;
            }
        }

        // ========== RECORDING FUNCTIONS ==========
        private void StartRecording()
        {
            if (isRecording) return;

            isRecording = true;
            EventLogger.IsRecording = true;
            EventLogger.TotalEventCount = 0;
            EventLogger.MouseEventsCount = 0;
            EventLogger.KeyDownCount = 0;
            EventLogger.KeyUpCount = 0;
            EventLogger.RecordingStartTime = DateTime.Now;
            EventLogger.RecordingDuration = TimeSpan.Zero;
            recordingStartTime = EventLogger.RecordingStartTime;
            recordedEvents.Clear();
            lastEvent = null;
            dgvEvents.Rows.Clear();

            // Install Hooks
            _procKeyboard = HookCallbackKeyboard;
            _procMouse = HookCallbackMouse;
            _hookIDKeyboard = SetHook(_procKeyboard);
            _hookIDMouse = SetHook(_procMouse);

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
            EventLogger.RecordingDuration = DateTime.Now - EventLogger.RecordingStartTime;
            
            // Uninstall Hooks
            UnhookWindowsHookEx(_hookIDKeyboard);
            UnhookWindowsHookEx(_hookIDMouse);
            _hookIDKeyboard = IntPtr.Zero;
            _hookIDMouse = IntPtr.Zero;

            btnStartRecord.Enabled = true;
            btnStopRecord.Enabled = false;
            UpdateReplayButtonState();

            // 🚀 Hiển thị dữ liệu lên bảng sau khi đã dừng record
            lblStatus.Text = "Status: Processing data...";
            LoadEventsToGrid();

            lblStatus.Text = $"Status: Recording stopped. {recordedEvents.Count} events recorded.";
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private void RecordEvent(string eventType, Dictionary<string, object> data)
        {
            if (!isRecording) return;

            var now = DateTime.Now;
            long timestamp = (long)(now - recordingStartTime).TotalMilliseconds;
            int delayMs = 0;

            if (lastEvent != null)
            {
                delayMs = (int)(timestamp - lastEvent.Timestamp);
            }

            var evt = new RecordedEvent
            {
                EventType = eventType,
                Timestamp = timestamp,
                DelayMs = delayMs,
                Data = new Dictionary<string, object>(data)
            };

            recordedEvents.Add(evt);
            lastEvent = evt;
            EventLogger.TotalEventCount = recordedEvents.Count;

            // Cập nhật thống kê loại event
            if (eventType.StartsWith("Mouse"))
            {
                EventLogger.MouseEventsCount++;
            }
            else if (eventType == "KeyDown")
            {
                EventLogger.KeyDownCount++;
            }
            else if (eventType == "KeyUp")
            {
                EventLogger.KeyUpCount++;
            }

            // 🚀 PERFORMANCE FIX: Không gọi AddEventToUI ở đây nữa để tránh lag
            // Chỉ cập nhật số lượng event vào Label
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => lblStatus.Text = $"Recording... Events: {recordedEvents.Count}"));
            }
            else
            {
                lblStatus.Text = $"Recording... Events: {recordedEvents.Count}";
            }
        }

        // Hook Callbacks
        private IntPtr HookCallbackKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN || wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;
                bool isDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);

                // Handle Pause key during replay
                if (isReplaying && isDown && key == Keys.P)
                {
                     isPaused = !isPaused;
                     string statusMsg = isPaused ? "Status: Replay PAUSED (Press P to resume)" : "Status: Replaying...";
                     if (this.InvokeRequired) this.BeginInvoke(new Action(() => lblStatus.Text = statusMsg));
                     else lblStatus.Text = statusMsg;
                     return (IntPtr)1; // Handled
                }

                if (isRecording)
                {
                    // Filter Windows keys
                    if (key == Keys.LWin || key == Keys.RWin) return (IntPtr)1;

                    var data = new Dictionary<string, object>
                    {
                        { "Key", key.ToString() },
                        { "KeyValue", (int)key }
                    };
                    
                    var modifiers = new List<string>();
                    if ((Control.ModifierKeys & Keys.Control) != 0) modifiers.Add("Ctrl");
                    if ((Control.ModifierKeys & Keys.Shift) != 0) modifiers.Add("Shift");
                    if ((Control.ModifierKeys & Keys.Alt) != 0) modifiers.Add("Alt");
                    
                    if (modifiers.Count > 0) data["Modifiers"] = string.Join("+", modifiers);

                    RecordEvent(isDown ? "KeyDown" : "KeyUp", data);
                }
            }
            return CallNextHookEx(_hookIDKeyboard, nCode, wParam, lParam);
        }

        private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && isRecording)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;

                if (msg == WM_MOUSEMOVE)
                {
                    RecordEvent("MouseMove", new Dictionary<string, object> { { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    string btn = msg == WM_LBUTTONDOWN ? "Left" : (msg == WM_RBUTTONDOWN ? "Right" : "Middle");
                    RecordEvent("MouseDown", new Dictionary<string, object> { { "Button", btn }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == WM_LBUTTONUP || msg == WM_RBUTTONUP || msg == WM_MBUTTONUP)
                {
                    string btn = msg == WM_LBUTTONUP ? "Left" : (msg == WM_RBUTTONUP ? "Right" : "Middle");
                    RecordEvent("MouseUp", new Dictionary<string, object> { { "Button", btn }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == WM_MOUSEWHEEL)
                {
                    // High word of mouseData is delta
                    short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    RecordEvent("MouseWheel", new Dictionary<string, object> { { "Delta", delta }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
            }
            return CallNextHookEx(_hookIDMouse, nCode, wParam, lParam);
        }

        // ========== REPLAY FUNCTIONS ==========
        private async Task ReplayEvent(RecordedEvent evt, bool useEmbeddedModifiers = true)
        {
            try
            {
                switch (evt.EventType)
                {
                    case "MouseDown":
                        await ReplayMouseDown(evt);
                        break;
                    case "MouseUp":
                        await ReplayMouseUp(evt);
                        break;
                    case "MouseMove":
                        await ReplayMouseMove(evt);
                        break;
                    case "MouseWheel":
                        await ReplayMouseWheel(evt);
                        break;
                    case "KeyDown":
                        await ReplayKeyDown(evt, useEmbeddedModifiers);
                        break;
                    case "KeyUp":
                        await ReplayKeyUp(evt, useEmbeddedModifiers);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error replaying event: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReplayMouseDown(RecordedEvent evt)
        {
            // 🛠 FIX: Sử dụng GetIntFromData
            int x = GetIntFromData(evt.Data["X"]);
            int y = GetIntFromData(evt.Data["Y"]);
            string button = evt.Data["Button"].ToString() ?? "Left";

            uint flags = button switch
            {
                "Left" => MOUSEEVENTF_LEFTDOWN,
                "Right" => MOUSEEVENTF_RIGHTDOWN,
                "Middle" => MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF_LEFTDOWN
            };

            await SendMouseInput(x, y, flags);
        }

        private async Task ReplayMouseUp(RecordedEvent evt)
        {
            // 🛠 FIX: Sử dụng GetIntFromData
            int x = GetIntFromData(evt.Data["X"]);
            int y = GetIntFromData(evt.Data["Y"]);
            string button = evt.Data["Button"].ToString() ?? "Left";

            uint flags = button switch
            {
                "Left" => MOUSEEVENTF_LEFTUP,
                "Right" => MOUSEEVENTF_RIGHTUP,
                "Middle" => MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF_LEFTUP
            };

            await SendMouseInput(x, y, flags);
        }

        private async Task ReplayMouseMove(RecordedEvent evt)
        {
            // 🛠 FIX: Sử dụng GetIntFromData
            int x = GetIntFromData(evt.Data["X"]);
            int y = GetIntFromData(evt.Data["Y"]);
            await SendMouseInput(x, y, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE);
        }

        private async Task ReplayMouseWheel(RecordedEvent evt)
        {
            // 🛠 FIX: Sử dụng GetIntFromData
            int x = GetIntFromData(evt.Data["X"]);
            int y = GetIntFromData(evt.Data["Y"]);
            int delta = GetIntFromData(evt.Data["Delta"]);

            var input = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = x * 65536 / Screen.PrimaryScreen.Bounds.Width,
                            dy = y * 65536 / Screen.PrimaryScreen.Bounds.Height,
                            mouseData = (uint)delta,
                            dwFlags = MOUSEEVENTF_WHEEL | MOUSEEVENTF_ABSOLUTE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                }
            };

            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(10);
        }

        private async Task ReplayKeyDown(RecordedEvent evt, bool useEmbeddedModifiers)
        {
            int keyValue = GetIntFromData(evt.Data["KeyValue"]);
            
            // Chỉ xử lý modifiers nếu được yêu cầu (Single Replay)
            // Trong Replay All, các phím modifier đã được ghi lại thành event riêng
            if (useEmbeddedModifiers)
            {
                string? modifiers = evt.Data.ContainsKey("Modifiers") ? evt.Data["Modifiers"].ToString() : null;
                if (!string.IsNullOrEmpty(modifiers))
                {
                    if (modifiers.Contains("Ctrl")) await SendKeyInput(0x11, false); // VK_CONTROL
                    if (modifiers.Contains("Shift")) await SendKeyInput(0x10, false); // VK_SHIFT
                    if (modifiers.Contains("Alt")) await SendKeyInput(0x12, false); // VK_MENU
                }
            }

            // Gửi phím chính
            await SendKeyInput((ushort)keyValue, false);
        }

        private async Task ReplayKeyUp(RecordedEvent evt, bool useEmbeddedModifiers)
        {
            int keyValue = GetIntFromData(evt.Data["KeyValue"]);
            
            // Gửi phím chính
            await SendKeyInput((ushort)keyValue, true);

            // Release modifiers nếu đã nhấn ở KeyDown
            if (useEmbeddedModifiers)
            {
                string? modifiers = evt.Data.ContainsKey("Modifiers") ? evt.Data["Modifiers"].ToString() : null;
                if (!string.IsNullOrEmpty(modifiers))
                {
                    if (modifiers.Contains("Ctrl")) await SendKeyInput(0x11, true);
                    if (modifiers.Contains("Shift")) await SendKeyInput(0x10, true);
                    if (modifiers.Contains("Alt")) await SendKeyInput(0x12, true);
                }
            }
        }

        private async Task SendMouseInput(int x, int y, uint flags)
        {
            var input = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = x * 65536 / Screen.PrimaryScreen.Bounds.Width,
                            dy = y * 65536 / Screen.PrimaryScreen.Bounds.Height,
                            mouseData = 0,
                            dwFlags = flags | MOUSEEVENTF_ABSOLUTE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                }
            };

            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(10);
        }

        private async Task SendKeyInput(ushort vk, bool keyUp)
        {
            uint flags = keyUp ? KEYEVENTF_KEYUP : 0;

            // Check for extended keys (Arrows, Home, End, Ins, Del, PageUp, PageDown, Win keys)
            if ((vk >= 33 && vk <= 46) || vk == 91 || vk == 92)
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            var input = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    union = new INPUTUNION
                    {
                        keyboard = new KEYBDINPUT
                        {
                            wVk = vk,
                            wScan = 0,
                            dwFlags = flags,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                }
            };

            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(10);
        }

        private async Task ReplayAllEvents()
        {
            if (isRecording || isReplaying || recordedEvents.Count == 0)
                return;

            isReplaying = true;
            isPaused = false;
            replayCancellation = new CancellationTokenSource();
            btnReplayAll.Enabled = false;
            btnStartRecord.Enabled = false;

            // Setup hook for Pause key
            _procKeyboard = HookCallbackKeyboard;
            _hookIDKeyboard = SetHook(_procKeyboard);

            try
            {
                progressReplay.Maximum = recordedEvents.Count;
                progressReplay.Value = 0;
                lblStatus.Text = "Status: Replaying events... (Press P to Pause)";

                for (int i = 0; i < recordedEvents.Count; i++)
                {
                    // Check for pause
                    while (isPaused)
                    {
                        if (replayCancellation.Token.IsCancellationRequested) break;
                        await Task.Delay(100);
                    }

                    if (replayCancellation.Token.IsCancellationRequested)
                        break;

                    var evt = recordedEvents[i];

                    // Wait for delay
                    if (evt.DelayMs > 0)
                    {
                        await Task.Delay(evt.DelayMs, replayCancellation.Token);
                    }

                    // Replay All: Không dùng embedded modifiers vì đã có event riêng cho modifier
                    await ReplayEvent(evt, false);
                    progressReplay.Value = i + 1;
                    
                    if (!isPaused)
                        lblStatus.Text = $"Status: Replaying {i + 1}/{recordedEvents.Count}... (Press P to Pause)";
                }

                lblStatus.Text = $"Status: Replay completed. {recordedEvents.Count} events replayed.";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Status: Replay cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during replay: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Status: Replay error occurred.";
            }
            finally
            {
                UnhookWindowsHookEx(_hookIDKeyboard);
                _hookIDKeyboard = IntPtr.Zero;

                isReplaying = false;
                isPaused = false;
                progressReplay.Value = 0;
                btnReplayAll.Enabled = true;
                btnStartRecord.Enabled = true;
                UpdateReplayButtonState();
            }
        }

        // ========== FILE OPERATIONS ==========
        private void SaveLogToFile()
        {
            if (recordedEvents.Count == 0)
            {
                MessageBox.Show("No events to save.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog()
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = $"event_log_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string json = JsonSerializer.Serialize(recordedEvents, options);
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        lblStatus.Text = $"Status: Saved {recordedEvents.Count} events to {Path.GetFileName(sfd.FileName)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadLogFromFile()
        {
            if (isRecording || isReplaying)
            {
                MessageBox.Show("Please stop recording/replaying before loading a log file.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        var loadedEvents = JsonSerializer.Deserialize<List<RecordedEvent>>(json);

                        if (loadedEvents == null || loadedEvents.Count == 0)
                        {
                            MessageBox.Show("The file contains no events.", "Information",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        recordedEvents = loadedEvents;

                        // 🛠 Sử dụng LoadEventsToGrid để load dữ liệu lên bảng
                        LoadEventsToGrid();

                        UpdateReplayButtonState();
                        lblStatus.Text = $"Status: Loaded {recordedEvents.Count} events from {Path.GetFileName(ofd.FileName)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ClearLog()
        {
            if (isRecording)
            {
                MessageBox.Show("Please stop recording before clearing the log.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            recordedEvents.Clear();
            dgvEvents.Rows.Clear();
            UpdateReplayButtonState();
            lblStatus.Text = "Status: Log cleared";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRecording();
                replayCancellation?.Cancel();
                replayCancellation?.Dispose();
                if (_hookIDKeyboard != IntPtr.Zero) UnhookWindowsHookEx(_hookIDKeyboard);
                if (_hookIDMouse != IntPtr.Zero) UnhookWindowsHookEx(_hookIDMouse);
            }
            base.Dispose(disposing);
        }
    }
}