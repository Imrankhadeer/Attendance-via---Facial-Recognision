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
                INSERT INTO users (name, student_id, embedding, face_image_path, created_at)
                VALUES (@Name, @StudentId, @FaceEmbedding, @FaceImagePath, @CreatedAt)
                RETURNING id;";

            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, student);
        }

        public async Task<IEnumerable<Student>> GetAllStudentsAsync()
        {
            const string sql = "SELECT id, name, student_id as StudentId, embedding as FaceEmbedding, face_image_path as FaceImagePath, created_at as CreatedAt FROM users;";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Student>(sql);
        }
        
        public async Task<Student?> GetStudentByStudentIdAsync(string studentId)
        {
             const string sql = "SELECT id, name, student_id as StudentId, embedding as FaceEmbedding, face_image_path as FaceImagePath, created_at as CreatedAt FROM users WHERE student_id = @StudentId;";
             using var connection = CreateConnection();
             return await connection.QuerySingleOrDefaultAsync<Student>(sql, new { StudentId = studentId });
        }

        public async Task MarkAttendanceAsync(AttendanceRecord record)
        {
            const string sql = @"
                INSERT INTO attendance (user_id, date, time, status)
                VALUES (@UserId, @Date, @Time, @Status);";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new 
            {
                record.UserId,
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
                SET name = @Name, student_id = @StudentId, face_image_path = @FaceImagePath
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
                SELECT a.id, a.user_id as UserId, u.name as StudentName, u.student_id as StudentId, a.date, a.time, a.status
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
    }
}
