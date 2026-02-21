using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Core.Models;

namespace FaceAttendance.Services
{
    public class AttendanceService
    {
        private readonly IAttendanceRepository _repository;
        private readonly IFaceRecognitionService _recognitionService;
        
        private List<Student> _cachedStudents = new();

        public AttendanceService(IAttendanceRepository repository, IFaceRecognitionService recognitionService)
        {
            _repository = repository;
            _recognitionService = recognitionService;
        }

        public async Task RefreshStudentCacheAsync()
        {
            var students = await _repository.GetAllStudentsAsync();
            _cachedStudents = students.ToList();
        }

        public async Task DeleteStudentAsync(int id)
        {
            await _repository.DeleteStudentAsync(id);
            _cachedStudents.RemoveAll(s => s.Id == id);
            
            // Note: In a production app, we would also delete the physical image file
            // but for simplicity we can leave it or add cleanup later.
        }

        public async Task<Student> RegisterStudentAsync(string name, string studentId, string course, string year, string semester, string group, byte[] faceImage)
        {
            // 1. Detect Face
            var detections = await _recognitionService.DetectFacesAsync(faceImage);
            if (detections.Count == 0) throw new Exception("No face detected.");
            if (detections.Count > 1) throw new Exception("Multiple faces detected. Please show only one face.");

            var face = detections.First();
            if (face.Score < 0.6) throw new Exception("Face quality too low.");

            // 2. Generate Embedding
            var embedding = await _recognitionService.GenerateEmbeddingAsync(faceImage, face);
            if (embedding.Length == 0) throw new Exception("Could not generate face embedding.");
            
            // 2b. Prevent Duplicates
            if (_cachedStudents.Count == 0) await RefreshStudentCacheAsync();
            const float Threshold = 0.25f;
            foreach (var existingStudent in _cachedStudents)
            {
                if (existingStudent.FaceEmbedding == null) continue;
                var score = _recognitionService.CalculateSimilarity(embedding, existingStudent.FaceEmbedding);
                if (score > Threshold)
                {
                    throw new Exception($"Registration failed: This person is already registered as '{existingStudent.Name}'.");
                }
            }

            // 3. Save Thumbnail
            string imgFolder = "img";
            if (!Directory.Exists(imgFolder)) Directory.CreateDirectory(imgFolder);
            string imgPath = Path.Combine(imgFolder, $"{Guid.NewGuid()}.jpg");
            
            try 
            {
                using var ms = new MemoryStream(faceImage);
                using var bmp = new Bitmap(ms);
                var rect = face.Box;
                rect.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
                if (rect.Width > 0 && rect.Height > 0)
                {
                    using var crop = bmp.Clone(rect, bmp.PixelFormat);
                    crop.Save(imgPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not save thumbnail: {ex.Message}");
                imgPath = ""; // fallback
            }

            var student = new Student
            {
                Name = name,
                StudentId = studentId,
                Course = course,
                Year = year,
                Semester = semester,
                StudentGroup = group,
                FaceEmbedding = embedding,
                FaceImagePath = imgPath,
                CreatedAt = DateTime.UtcNow
            };

            student.Id = await _repository.AddStudentAsync(student);
            _cachedStudents.Add(student);
            return student;
        }

        public async Task<List<(Student? Student, bool AlreadyMarked, string SessionStatus, float Confidence, FaceDetectionResult Face)>> ProcessFrameAsync(byte[] frameBytes)
        {
            if (_cachedStudents.Count == 0) await RefreshStudentCacheAsync();

            var results = new List<(Student?, bool, string, float, FaceDetectionResult)>();
            
            // 1. Detect Faces
            var detections = await _recognitionService.DetectFacesAsync(frameBytes);
            
            foreach (var face in detections)
            {
                if (face.Score < 0.5) continue;

                // 2. Generate Embedding
                var embedding = await _recognitionService.GenerateEmbeddingAsync(frameBytes, face);
                
                // 3. Match
                Student? bestMatch = null;
                float maxScore = 0;
                const float Threshold = 0.25f; // ArcFace cosine similarity often falls into 0.3-0.5 range for positive matches in wild environments

                foreach (var student in _cachedStudents)
                {
                    if (student.FaceEmbedding == null) continue;
                    
                    var score = _recognitionService.CalculateSimilarity(embedding, student.FaceEmbedding);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestMatch = student;
                    }
                }

                bool alreadyMarked = false;
                if (bestMatch != null && maxScore > Threshold)
                {
                    // Check active session for this student
                    var activeSessions = await _repository.GetActiveSessionsAsync();
                    var matchingSession = activeSessions.FirstOrDefault(s => 
                        (string.IsNullOrEmpty(s.Course) || s.Course == bestMatch.Course) &&
                        (string.IsNullOrEmpty(s.Year) || s.Year == bestMatch.Year) &&
                        (string.IsNullOrEmpty(s.Semester) || s.Semester == bestMatch.Semester) &&
                        (string.IsNullOrEmpty(s.StudentGroup) || s.StudentGroup == bestMatch.StudentGroup));

                    if (matchingSession == null)
                    {
                        // Found a face, but no active session allows them to mark attendance
                        results.Add((bestMatch, false, "No Active Session", maxScore, face)); // Will show scanning instead of false attendance
                        continue;
                    }

                    // Check if already marked
                    var today = DateTime.Today;
                    alreadyMarked = await _repository.HasAttendanceForDateAsync(bestMatch.Id, today);
                    
                    if (!alreadyMarked)
                    {
                        await _repository.MarkAttendanceAsync(new AttendanceRecord
                        {
                            UserId = bestMatch.Id,
                            SessionId = matchingSession.Id,
                            Date = today,
                            Time = DateTime.Now.TimeOfDay,
                            Status = "Present"
                        });
                    }
                }
                else
                {
                    bestMatch = null; // Ensure we don't return partial match
                }
                
                results.Add((bestMatch, alreadyMarked, alreadyMarked ? "Already Marked" : "Attendance Marked", maxScore, face));
            }

            return results;
        }
    }
}
