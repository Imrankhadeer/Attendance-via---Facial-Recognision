using System;

namespace FaceAttendance.Core.Models
{
    public class ClassSession
    {
        public int Id { get; set; }
        public int FacultyId { get; set; }
        public string? Course { get; set; }
        public string? Year { get; set; }
        public string? Semester { get; set; }
        public string? StudentGroup { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
