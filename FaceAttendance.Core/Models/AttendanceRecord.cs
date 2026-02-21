using System;

namespace FaceAttendance.Core.Models
{
    public class AttendanceRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string StudentName { get; set; } = string.Empty; // For display convenience
        public string StudentId { get; set; } = string.Empty; // For display convenience
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public string Status { get; set; } = "Present";
    }
}
