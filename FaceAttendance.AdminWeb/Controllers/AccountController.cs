using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FaceAttendance.AdminWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;

        public AccountController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Port=5432;Database=face_attendance;Username=postgres;Password=postgres";
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Student")) return RedirectToAction("MyAttendance", "Student");
                if (User.IsInRole("Faculty")) return RedirectToAction("Dashboard", "Faculty");
                if (User.IsInRole("Admin")) return RedirectToAction("Students", "Admin");
                
                // If they have a legacy cookie without a role, log them out
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string role)
        {
            bool isValid = false;
            string actualRole = role;
            string displayName = username;
            string facultyIdStr = "";
            string studentIdStr = "";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                if (role == "Admin")
                {
                    var query = "SELECT password_hash FROM admins WHERE username = @u";
                    using var cmd = new NpgsqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("u", username);
                    var dbPassword = await cmd.ExecuteScalarAsync() as string;
                    if (dbPassword != null && dbPassword == password) isValid = true;
                }
                else if (role == "Faculty")
                {
                    var query = "SELECT id, name, password_hash FROM faculties WHERE username = @u";
                    using var cmd = new NpgsqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("u", username);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var dbPassword = reader.GetString(2);
                        if (dbPassword == password)
                        {
                            isValid = true;
                            facultyIdStr = reader.GetInt32(0).ToString();
                            displayName = reader.GetString(1);
                        }
                    }
                }
                else if (role == "Student")
                {
                    // For student, username = Student ID, password = Name (for simple verification)
                    var query = "SELECT id, name FROM users WHERE student_id = @u";
                    using var cmd = new NpgsqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("u", username);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var dbName = reader.GetString(1);
                        if (dbName.ToLower() == password.ToLower())
                        {
                            isValid = true;
                            studentIdStr = username; // StudentId in system
                            displayName = dbName;
                        }
                    }
                }
            }

            if (isValid)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, displayName),
                    new Claim(ClaimTypes.Role, actualRole)
                };

                if (actualRole == "Faculty") claims.Add(new Claim("FacultyId", facultyIdStr));
                if (actualRole == "Student") claims.Add(new Claim("StudentId", studentIdStr));

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity));

                if (actualRole == "Student") return RedirectToAction("MyAttendance", "Student");
                if (actualRole == "Faculty") return RedirectToAction("Dashboard", "Faculty");
                return RedirectToAction("Students", "Admin");
            }

            ViewBag.ErrorMessage = "Invalid credentials.";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
