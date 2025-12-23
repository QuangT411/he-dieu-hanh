using System.Collections.Generic;

namespace he_dieu_hanh.Models
{
    public class RecordedEvent
    {
        public string EventType { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public int DelayMs { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}
