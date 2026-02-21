using System;
using System.Threading.Tasks;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FaceAttendance.AdminWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAttendanceRepository _repository;

        public AdminController(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<IActionResult> Students()
        {
            var students = await _repository.GetAllStudentsAsync();
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
        public async Task<IActionResult> UpdateStudent(int id, string name, string studentId, string? faceImagePath)
        {
            try
            {
                // Basic implementation for saving edited names/IDs
                var student = await _repository.GetStudentByStudentIdAsync(studentId) ?? new Student { Id = id };
                student.Id = id;
                student.Name = name;
                student.StudentId = studentId;
                student.FaceImagePath = faceImagePath;

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
    }
}
