using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FaceAttendance.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceAttendance.AdminWeb.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IAttendanceRepository _repository;

        public StudentController(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<IActionResult> MyAttendance()
        {
            string studentId = User.FindFirstValue("StudentId");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Account");

            var records = await _repository.GetAttendanceRecordsByStudentIdAsync(studentId);
            return View(records);
        }
    }
}
