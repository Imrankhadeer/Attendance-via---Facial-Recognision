using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FaceAttendance.Core.Models;

namespace FaceAttendance.Core.Interfaces
{
    public interface IAttendanceRepository
    {
        Task<int> AddStudentAsync(Student student);
        Task<IEnumerable<Student>> GetAllStudentsAsync(); // Load all for in-memory matching
        Task<Student?> GetStudentByStudentIdAsync(string studentId);
        Task UpdateStudentAsync(Student student);
        Task DeleteStudentAsync(int id);
        
        Task MarkAttendanceAsync(AttendanceRecord record);
        Task<bool> HasAttendanceForDateAsync(int studentId, DateTime date);
        Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsAsync(DateTime? date = null);
    }
}
