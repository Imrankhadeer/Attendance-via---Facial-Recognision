using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Core.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FaceAttendance.Data.Repositories
{
    public class AttendanceRepository : IAttendanceRepository
    {
        private readonly string _connectionString;

        public AttendanceRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
                
            // Auto-migrate schema changes
            using var connection = CreateConnection();
            try { connection.Execute("ALTER TABLE users ADD COLUMN IF NOT EXISTS face_image_path TEXT;"); } catch { }
        }

        private IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<int> AddStudentAsync(Student student)
        {
            const string sql = @"
                INSERT INTO users (name, student_id, embedding, face_image_path, course, year, semester, student_group, created_at)
                VALUES (@Name, @StudentId, @FaceEmbedding, @FaceImagePath, @Course, @Year, @Semester, @StudentGroup, @CreatedAt)
                RETURNING id;";

            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, student);
        }

        public async Task<IEnumerable<Student>> GetAllStudentsAsync()
        {
            const string sql = "SELECT id, name, student_id as StudentId, embedding as FaceEmbedding, face_image_path as FaceImagePath, course, year, semester, student_group as StudentGroup, created_at as CreatedAt FROM users;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Student>(sql);
        }
        
        public async Task<Student?> GetStudentByStudentIdAsync(string studentId)
        {
             const string sql = "SELECT id, name, student_id as StudentId, embedding as FaceEmbedding, face_image_path as FaceImagePath, course, year, semester, student_group as StudentGroup, created_at as CreatedAt FROM users WHERE student_id = @StudentId;";
             using var connection = CreateConnection();
             return await connection.QuerySingleOrDefaultAsync<Student>(sql, new { StudentId = studentId });
        }

        public async Task MarkAttendanceAsync(AttendanceRecord record)
        {
            const string sql = @"
                INSERT INTO attendance (user_id, session_id, date, time, status)
                VALUES (@UserId, @SessionId, @Date, @Time, @Status);";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new 
            {
                record.UserId,
                record.SessionId,
                Date = record.Date.Date, // Ensure only date part
                record.Time,
                record.Status
            });
        }

        public async Task<bool> HasAttendanceForDateAsync(int userId, DateTime date)
        {
            const string sql = "SELECT COUNT(1) FROM attendance WHERE user_id = @UserId AND date = @Date;";
            using var connection = CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, Date = date.Date });
            return count > 0;
        }

        public async Task UpdateStudentAsync(Student student)
        {
            const string sql = @"
                UPDATE users 
                SET name = @Name, student_id = @StudentId, face_image_path = @FaceImagePath, course = @Course, year = @Year, semester = @Semester, student_group = @StudentGroup
                WHERE id = @Id;";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, student);
        }

        public async Task DeleteStudentAsync(int id)
        {
            const string sql = @"
                DELETE FROM attendance WHERE user_id = @Id;
                DELETE FROM users WHERE id = @Id;";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsAsync(DateTime? date = null)
        {
            string sql = @"
                SELECT a.id, a.user_id as UserId, a.session_id as SessionId, u.name as StudentName, u.student_id as StudentId, a.date, a.time, a.status
                FROM attendance a
                JOIN users u ON a.user_id = u.id";
            
            if (date.HasValue)
            {
                sql += " WHERE a.date = @Date";
            }
            
            sql += " ORDER BY a.date DESC, a.time DESC;";

            using var connection = CreateConnection();
            return await connection.QueryAsync<AttendanceRecord>(sql, new { Date = date?.Date });
        }

        public async Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsByStudentIdAsync(string studentId)
        {
            string sql = @"
                SELECT a.id, a.user_id as UserId, a.session_id as SessionId, u.name as StudentName, u.student_id as StudentId, a.date, a.time, a.status
                FROM attendance a
                JOIN users u ON a.user_id = u.id
                WHERE u.student_id = @StudentId
                ORDER BY a.date DESC, a.time DESC;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<AttendanceRecord>(sql, new { StudentId = studentId });
        }

        // Faculties
        public async Task<int> CreateFacultyAsync(Faculty faculty)
        {
            const string sql = @"INSERT INTO faculties (username, password_hash, name) VALUES (@Username, @PasswordHash, @Name) RETURNING id;";
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, faculty);
        }

        public async Task<IEnumerable<Faculty>> GetAllFacultiesAsync()
        {
            const string sql = "SELECT id, username as Username, password_hash as PasswordHash, name as Name FROM faculties;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Faculty>(sql);
        }

        public async Task<Faculty?> GetFacultyByUsernameAsync(string username)
        {
            const string sql = "SELECT id, username as Username, password_hash as PasswordHash, name as Name FROM faculties WHERE username = @Username;";
            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Faculty>(sql, new { Username = username });
        }

        public async Task DeleteFacultyAsync(int id)
        {
            const string sql = "DELETE FROM faculties WHERE id = @Id;";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        // Sessions
        public async Task<int> CreateSessionAsync(ClassSession session)
        {
            const string sql = @"
                INSERT INTO sessions (faculty_id, course, year, semester, student_group, is_active, started_at)
                VALUES (@FacultyId, @Course, @Year, @Semester, @StudentGroup, @IsActive, @StartedAt)
                RETURNING id;";
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, session);
        }

        public async Task EndSessionAsync(int id)
        {
            const string sql = "UPDATE sessions SET is_active = false, ended_at = CURRENT_TIMESTAMP WHERE id = @Id;";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<IEnumerable<ClassSession>> GetActiveSessionsAsync()
        {
            const string sql = "SELECT id as Id, faculty_id as FacultyId, course as Course, year as Year, semester as Semester, student_group as StudentGroup, is_active as IsActive, started_at as StartedAt FROM sessions WHERE is_active = true;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<ClassSession>(sql);
        }

        public async Task<IEnumerable<ClassSession>> GetSessionsByFacultyIdAsync(int facultyId)
        {
            const string sql = "SELECT id as Id, faculty_id as FacultyId, course as Course, year as Year, semester as Semester, student_group as StudentGroup, is_active as IsActive, started_at as StartedAt, ended_at as EndedAt FROM sessions WHERE faculty_id = @FacultyId ORDER BY started_at DESC;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<ClassSession>(sql, new { FacultyId = facultyId });
        }
    }
}
