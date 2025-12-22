namespace he_dieu_hanh
{
    // Interface này định nghĩa các hành động mà lớp ghi log phải có
    public interface IEventLogger
    {
        void StartHooking();
        void StopHooking();
        // Event để thông báo khi có log mới
        event Action<string, string> OnNewLogEntry; // (type, details)
    }
    
    // Static class để chia sẻ dữ liệu giữa các trang
    public static class EventLogger
    {
        // Tổng số sự kiện đã ghi
        public static int TotalEventCount { get; set; } = 0;
        
        // Trạng thái đang ghi log hay không
        public static bool IsRecording { get; set; } = false;

        // Thời gian bắt đầu ghi
        public static DateTime RecordingStartTime { get; set; } = DateTime.MinValue;

        // Thời gian ghi log cuối cùng (hoặc hiện tại nếu đang ghi)
        public static TimeSpan RecordingDuration { get; set; } = TimeSpan.Zero;

        // Thống kê chi tiết
        public static int MouseEventsCount { get; set; } = 0;
        public static int KeyDownCount { get; set; } = 0;
        public static int KeyUpCount { get; set; } = 0;
    }
}