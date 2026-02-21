using System;
using System.Threading.Tasks;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceAttendance.AdminWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAttendanceRepository _repository;

        public AdminController(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<IActionResult> Students(string courseFilter, string yearFilter, string semesterFilter, string groupFilter)
        {
            var students = await _repository.GetAllStudentsAsync();
            
            if (!string.IsNullOrEmpty(courseFilter)) students = students.Where(s => s.Course == courseFilter);
            if (!string.IsNullOrEmpty(yearFilter)) students = students.Where(s => s.Year == yearFilter);
            if (!string.IsNullOrEmpty(semesterFilter)) students = students.Where(s => s.Semester == semesterFilter);
            if (!string.IsNullOrEmpty(groupFilter)) students = students.Where(s => s.StudentGroup == groupFilter);
            
            ViewBag.CourseFilter = courseFilter;
            ViewBag.YearFilter = yearFilter;
            ViewBag.SemesterFilter = semesterFilter;
            ViewBag.GroupFilter = groupFilter;

            return View(students);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                await _repository.DeleteStudentAsync(id);
                TempData["SuccessMessage"] = "Student deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting student: {ex.Message}";
            }
            
            return RedirectToAction(nameof(Students));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStudent(int id, string name, string studentId, string faceImagePath, string course, string year, string semester, string studentGroup)
        {
            try
            {
                var student = new Student
                {
                    Id = id,
                    Name = name,
                    StudentId = studentId,
                    FaceImagePath = faceImagePath,
                    Course = course,
                    Year = year,
                    Semester = semester,
                    StudentGroup = studentGroup
                };
                await _repository.UpdateStudentAsync(student);
                TempData["SuccessMessage"] = "Student details updated.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
            }

            return RedirectToAction(nameof(Students));
        }

        public async Task<IActionResult> Attendance(DateTime? dateFilter)
        {
            var records = await _repository.GetAttendanceRecordsAsync(dateFilter);
            ViewBag.SelectedDate = dateFilter?.ToString("yyyy-MM-dd");
            return View(records);
        }

        // --- FACULTY MANAGEMENT ---
        public async Task<IActionResult> Faculties()
        {
            var faculties = await _repository.GetAllFacultiesAsync();
            return View(faculties);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFaculty(string username, string name, string password)
        {
            try
            {
                var faculty = new Faculty { Username = username, Name = name, PasswordHash = password };
                await _repository.CreateFacultyAsync(faculty);
                TempData["SuccessMessage"] = "Faculty account created.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating faculty: " + ex.Message;
            }
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFaculty(int id)
        {
            try
            {
                await _repository.DeleteFacultyAsync(id);
                TempData["SuccessMessage"] = "Faculty account deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting faculty: " + ex.Message;
            }
            return RedirectToAction(nameof(Faculties));
        }
    }
}
