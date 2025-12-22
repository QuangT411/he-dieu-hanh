using System.Drawing;

namespace he_dieu_hanh
{
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; private set; } = false;

        public static event Action? ThemeChanged;

        public static Color BackgroundColor => IsDarkMode ? Color.FromArgb(40, 44, 52) : Color.FromArgb(245, 247, 250);
        public static Color ForegroundColor => IsDarkMode ? Color.WhiteSmoke : Color.Black;
        public static Color PanelColor => IsDarkMode ? Color.FromArgb(50, 54, 62) : Color.White;
        public static Color AccentColor => Color.FromArgb(52, 152, 219);

        public static void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            ThemeChanged?.Invoke();
        }
    }
}
