using System;

namespace ClamAVGui.Models
{
    public class HistoryEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
