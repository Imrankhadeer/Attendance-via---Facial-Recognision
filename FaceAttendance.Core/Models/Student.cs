using System;

namespace FaceAttendance.Core.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public byte[]? FaceEmbedding { get; set; }
        public string? FaceImagePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
