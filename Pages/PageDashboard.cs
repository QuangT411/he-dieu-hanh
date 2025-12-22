using System;
using System.Drawing;
using System.Windows.Forms;

namespace he_dieu_hanh.Pages
{
    public partial class PageDashboard : UserControl
    {
        private static readonly Color AccentColor1 = Color.FromArgb(46, 204, 113);
        private static readonly Color AccentColor2 = Color.FromArgb(231, 76, 60);
        private static readonly Color PrimaryColor_Static = Color.FromArgb(0, 120, 215);

        private TableLayoutPanel tlpMain;
        private Panel pnlHeader;
        private Label lblTitle;
        private TableLayoutPanel tlpContent;

        // Label để hiển thị tổng số event (có thể cập nhật)
        private Label? lblTotalEventsValue;
        private Label? lblRecordingStatusValue;
        private Label? lblDurationValue;
        private Label? lblDistributionValue;
        
        // Timer để cập nhật số liệu
        private System.Windows.Forms.Timer? updateTimer;

        public PageDashboard()
        {
            this.Dock = DockStyle.Fill;
            // Ban đầu chỉ gọi InitializeUI, màu sẽ được ApplyTheme() thiết lập
            InitializeSimpleUI();

            // Áp dụng theme ban đầu
            ApplyTheme();
        }

        private void InitializeSimpleUI()
        {
            // Sử dụng TableLayoutPanel để chia bố cục thành Header và Body
            tlpMain = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
            };

            tlpMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // === 1. HEADER (Hàng 0) ===
            pnlHeader = new Panel()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 0, 20, 0),
            };

            lblTitle = new Label()
            {
                Text = "📊 Dashboard - Tổng quan Hệ thống",
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = PrimaryColor_Static, // Màu tiêu đề cố định để giữ sự nổi bật
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);
            tlpMain.Controls.Add(pnlHeader, 0, 0);

            // === 2. BODY / MAIN CONTENT (Hàng 1) ===
            var pnlBody = new Panel()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
            };

            // TLP bên trong Body để tạo lưới 2x2 cho các chỉ số
            tlpContent = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };

            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // --- THÊM CÁC CHỈ SỐ (PLACEHOLDERS) ---

            // Ô 1: Trạng thái Ghi log
            var statusCard = CreateSimpleMetricCard("Trạng thái Ghi Log", "⏸️ PAUSED", "Trạng thái: Đang chờ", AccentColor2, out lblRecordingStatusValue);
            tlpContent.Controls.Add(statusCard, 0, 0);

            // Ô 2: Tổng số Sự kiện
            var eventsCard = CreateSimpleMetricCard("Tổng số Sự kiện", "0", "Tổng sự kiện Mouse và Keyboard đã ghi", PrimaryColor_Static, out lblTotalEventsValue);
            tlpContent.Controls.Add(eventsCard, 1, 0);

            // Ô 3: Thời gian hoạt động
            tlpContent.Controls.Add(
                CreateSimpleMetricCard("Thời gian Hook", "00:00:00", "Thời gian ghi log hoạt động gần nhất", PrimaryColor_Static, out lblDurationValue), 0, 1);

            // Ô 4: Phân phối Sự kiện
            tlpContent.Controls.Add(
                CreateSimpleMetricCard("Phân phối Log", "M: 0 | K: 0", "Mouse (M) | Keyboard (K)", AccentColor1, out lblDistributionValue), 1, 1);


            pnlBody.Controls.Add(tlpContent);

            tlpMain.Controls.Add(pnlBody, 0, 1);

            this.Controls.Add(tlpMain);
            
            // Khởi tạo timer để cập nhật số liệu
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 500; // Cập nhật mỗi 500ms
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        // Cập nhật số liệu từ EventLogger
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Cập nhật tổng số events
            int totalEvents = EventLogger.TotalEventCount;
            if (lblTotalEventsValue != null)
            {
                lblTotalEventsValue.Text = totalEvents.ToString("N0");
            }
            
            // Cập nhật trạng thái recording
            if (lblRecordingStatusValue != null)
            {
                if (EventLogger.IsRecording)
                {
                    lblRecordingStatusValue.Text = "🔴 REC";
                    lblRecordingStatusValue.ForeColor = AccentColor2;
                }
                else
                {
                    lblRecordingStatusValue.Text = "⏸️ PAUSED";
                    lblRecordingStatusValue.ForeColor = Color.Gray;
                }
            }

            // Cập nhật thời gian record
            if (lblDurationValue != null)
            {
                TimeSpan duration;
                if (EventLogger.IsRecording)
                {
                    duration = DateTime.Now - EventLogger.RecordingStartTime;
                }
                else
                {
                    duration = EventLogger.RecordingDuration;
                }
                lblDurationValue.Text = duration.ToString(@"hh\:mm\:ss");
            }

            // Cập nhật phân phối log
            if (lblDistributionValue != null)
            {
                int totalKeyboard = EventLogger.KeyDownCount + EventLogger.KeyUpCount;
                lblDistributionValue.Text = $"M: {EventLogger.MouseEventsCount} | K: {totalKeyboard}";
                // Giảm font size nếu text quá dài
                if (lblDistributionValue.Text.Length > 15) 
                    lblDistributionValue.Font = new Font("Segoe UI", 24, FontStyle.Bold);
                else
                    lblDistributionValue.Font = new Font("Segoe UI", 36, FontStyle.Bold);
            }
        }

        // =================================================================
        // HÀM TẠO CARD (CÓ THỂ XỬ LÝ THEME)
        // =================================================================

        private Panel CreateSimpleMetricCard(string title, string value, string description, Color color, out Label? lblValueRef)
        {
            // Panel bên ngoài (chỉ để giữ padding và màu nền chung)
            var pnl = new Panel()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle, // Giữ lại viền
                Tag = "CardContainer" // Đánh dấu để dễ dàng áp dụng Theme
            };

            var tlpCard = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(15),
                Tag = "CardContent" // Đánh dấu để dễ dàng áp dụng Theme
            };
            tlpCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tlpCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlpCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            // 1. Tiêu đề
            var lblTitle = new Label()
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = ThemeManager.ForegroundColor, // Sử dụng ForegroundColor
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = "Title"
            };
            tlpCard.Controls.Add(lblTitle, 0, 0);

            // 2. Giá trị/Placeholder chính
            var lblValue = new Label()
            {
                Text = value,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 36, FontStyle.Bold),
                ForeColor = color, // Màu Accent cố định không thay đổi theo theme
                TextAlign = ContentAlignment.MiddleCenter,
            };
            tlpCard.Controls.Add(lblValue, 0, 1);
            
            // Trả về reference của label value
            lblValueRef = lblValue;

            // 3. Mô tả
            var lblDescription = new Label()
            {
                Text = description,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Gray, // Giữ màu xám
                TextAlign = ContentAlignment.TopCenter,
            };
            tlpCard.Controls.Add(lblDescription, 0, 2);

            pnl.Controls.Add(tlpCard);
            return pnl;
        }

        // =================================================================
        // HÀM CẬP NHẬT THEME
        // =================================================================

        /// <summary>
        /// Áp dụng màu sắc Theme cho toàn bộ UserControl.
        /// </summary>
        public void ApplyTheme()
        {
            // 1. Nền tổng thể
            this.BackColor = ThemeManager.BackgroundColor;
            tlpMain.BackColor = ThemeManager.BackgroundColor;

            // 2. Header
            pnlHeader.BackColor = ThemeManager.PanelColor;
            lblTitle.ForeColor = ThemeManager.ForegroundColor; // Tiêu đề chính theo theme

            // 3. Nội dung chính (TLP và các Card)

            // Đệ quy cập nhật màu cho tất cả controls
            UpdateControlColors(this.Controls);
        }

        /// <summary>
        /// Hàm đệ quy duyệt qua và cập nhật màu sắc các controls dựa trên Tag.
        /// </summary>
        private void UpdateControlColors(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                if (control.Tag != null)
                {
                    // Card Container: Màu nền Panel
                    if (control.Tag.ToString() == "CardContainer")
                    {
                        control.BackColor = ThemeManager.PanelColor;
                        control.ForeColor = ThemeManager.ForegroundColor;
                    }
                    // Card Content: Màu nền Panel
                    else if (control.Tag.ToString() == "CardContent")
                    {
                        control.BackColor = ThemeManager.PanelColor;
                        control.ForeColor = ThemeManager.ForegroundColor;
                    }
                    // Tiêu đề trong Card
                    else if (control.Tag.ToString() == "Title")
                    {
                        control.ForeColor = ThemeManager.ForegroundColor;
                        control.BackColor = ThemeManager.PanelColor;
                    }
                }
                else
                {
                    // Đối với các control không có Tag, cố gắng đặt ForeColor/BackColor
                    if (control is Panel || control is TableLayoutPanel)
                    {
                        // Giữ nguyên màu nền nếu nó là BackgroundColor
                        if (control.BackColor == Color.FromArgb(240, 240, 240) || control.BackColor == ThemeManager.BackgroundColor)
                        {
                            control.BackColor = ThemeManager.BackgroundColor;
                        }
                    }
                    else if (control is Label)
                    {
                        // Nếu Label có màu TextColor cũ (hoặc màu mặc định), cập nhật
                        if (control.ForeColor == Color.Black || control.ForeColor == Color.FromArgb(40, 40, 40))
                        {
                            control.ForeColor = ThemeManager.ForegroundColor;
                        }
                    }
                }

                // Đệ quy tiếp
                if (control.HasChildren)
                {
                    UpdateControlColors(control.Controls);
                }
            }
        }
    }
}