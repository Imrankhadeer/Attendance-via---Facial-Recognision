using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceAttendance.AdminWeb.Controllers
{
    [Authorize(Roles = "Faculty")]
    public class FacultyController : Controller
    {
        private readonly IAttendanceRepository _repository;

        public FacultyController(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<IActionResult> Dashboard()
        {
            int facultyId = int.Parse(User.FindFirstValue("FacultyId") ?? "0");
            var sessions = await _repository.GetSessionsByFacultyIdAsync(facultyId);
            return View(sessions);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession(string course, string year, string semester, string group)
        {
            try
            {
                int facultyId = int.Parse(User.FindFirstValue("FacultyId") ?? "0");
                var session = new ClassSession
                {
                    FacultyId = facultyId,
                    Course = course,
                    Year = year,
                    Semester = semester,
                    StudentGroup = group,
                    IsActive = true,
                    StartedAt = DateTime.UtcNow
                };
                
                await _repository.CreateSessionAsync(session);
                TempData["SuccessMessage"] = "Session started. The desktop app will now capture attendance for these students.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error starting session: " + ex.Message;
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> EndSession(int id)
        {
            try
            {
                await _repository.EndSessionAsync(id);
                TempData["SuccessMessage"] = "Session ended successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error ending session: " + ex.Message;
            }
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
