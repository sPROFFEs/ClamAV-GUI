using System;

namespace ClamAVGui.Models
{
    public class QuarantineItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string QuarantinePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string ThreatName { get; set; } = string.Empty;
        public DateTime QuarantinedAt { get; set; } = DateTime.Now;
        public string Notes { get; set; } = string.Empty;
    }
}
