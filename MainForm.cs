using System;
using System.Drawing;
using System.Windows.Forms;

namespace he_dieu_hanh
{
    public partial class MainForm : Form
    {
        private Panel sideMenu;
        private Panel contentPanel;
        private Button btnDashboard, btnEventLog, btnSettings;

        // Cache các trang để giữ trạng thái (đặc biệt là EventLog đang recording)
        private Pages.PageDashboard? pageDashboard;
        private Pages.PageEventLog? pageEventLog;
        private Pages.PageSettings? pageSettings;

        public MainForm()
        {
            this.Text = "Hook Logger - Multi Page";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            InitializeUI();
            
            // Đăng ký sự kiện thay đổi theme
            ThemeManager.ThemeChanged += ApplyTheme;

            // Khởi tạo trang mặc định
            if (pageDashboard == null) pageDashboard = new Pages.PageDashboard();
            ShowPage(pageDashboard);

            // Áp dụng theme ban đầu
            ApplyTheme();
        }

        private void InitializeUI()
        {
            // Thanh menu bên trái
            sideMenu = new Panel()
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = Color.FromArgb(45, 52, 70)
            };

            // Nút menu
            btnDashboard = CreateMenuButton("🏠 Dashboard");
            btnEventLog = CreateMenuButton("🖱️ Event Log");
            btnSettings = CreateMenuButton("⚙️ Settings");

            sideMenu.Controls.AddRange(new Control[] { btnSettings, btnEventLog, btnDashboard });

            // Khu hiển thị trang
            contentPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 250)
            };

            this.Controls.Add(contentPanel);
            this.Controls.Add(sideMenu);

            // Gắn sự kiện chuyển trang (sử dụng singleton/cached instances)
            btnDashboard.Click += (s, e) => 
            {
                if (pageDashboard == null) pageDashboard = new Pages.PageDashboard();
                ShowPage(pageDashboard);
            };
            
            btnEventLog.Click += (s, e) => 
            {
                if (pageEventLog == null) pageEventLog = new Pages.PageEventLog();
                ShowPage(pageEventLog);
            };
            
            btnSettings.Click += (s, e) => 
            {
                if (pageSettings == null) pageSettings = new Pages.PageSettings();
                ShowPage(pageSettings);
            };
        }

        private Button CreateMenuButton(string text)
        {
            var btn = new Button()
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 60,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(52, 73, 94);

            btn.MouseEnter += (s, e) => btn.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(70, 70, 80) : Color.FromArgb(64, 90, 120);
            btn.MouseLeave += (s, e) => btn.BackColor = ThemeManager.IsDarkMode ? ThemeManager.PanelColor : Color.FromArgb(52, 73, 94);

            return btn;
        }

        public void ApplyTheme()
        {
            // Cập nhật màu nền chính
            this.BackColor = ThemeManager.BackgroundColor;
            contentPanel.BackColor = ThemeManager.BackgroundColor;
            
            // Cập nhật SideMenu
            sideMenu.BackColor = ThemeManager.IsDarkMode ? Color.FromArgb(30, 30, 30) : Color.FromArgb(45, 52, 70);

            // Cập nhật các nút trong SideMenu
            foreach (Control c in sideMenu.Controls)
            {
                if (c is Button btn)
                {
                    btn.BackColor = ThemeManager.IsDarkMode ? ThemeManager.PanelColor : Color.FromArgb(52, 73, 94);
                    btn.ForeColor = ThemeManager.IsDarkMode ? Color.WhiteSmoke : Color.White;
                }
            }

            // Cập nhật trang hiện tại và các trang đã cache
            if (pageDashboard != null) pageDashboard.ApplyTheme();
            if (pageSettings != null) pageSettings.ApplyTheme();
            
            if (pageEventLog != null) 
            {
                var method = pageEventLog.GetType().GetMethod("ApplyTheme");
                method?.Invoke(pageEventLog, null);
            }
        }

        private void ShowPage(UserControl page)
        {
            if (contentPanel.Controls.Count > 0)
            {
                contentPanel.Controls.RemoveAt(0);
            }
            
            page.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(page);
            
            var method = page.GetType().GetMethod("ApplyTheme");
            method?.Invoke(page, null);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Đảm bảo gỡ Hook khi tắt app
            if (pageEventLog != null)
            {
                pageEventLog.Dispose();
            }
            
            base.OnFormClosing(e);
        }
    }
}
