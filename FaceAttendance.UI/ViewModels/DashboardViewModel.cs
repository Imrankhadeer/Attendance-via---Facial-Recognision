using CommunityToolkit.Mvvm.ComponentModel;
using FaceAttendance.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FaceAttendance.Core.Models;

namespace FaceAttendance.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IAttendanceRepository _repository;

        [ObservableProperty]
        private int _totalStudents;

        [ObservableProperty]
        private int _presentToday;

        public ObservableCollection<AttendanceRecord> RecentActivity { get; } = new();

        public DashboardViewModel(IAttendanceRepository repository)
        {
            _repository = repository;
            LoadStatsAsync();
        }

        private async void LoadStatsAsync()
        {
            // Simple stats
            var students = await _repository.GetAllStudentsAsync();
            TotalStudents = System.Linq.Enumerable.Count(students);
            
            var todayRecords = await _repository.GetAttendanceRecordsAsync(System.DateTime.Today);
            PresentToday = System.Linq.Enumerable.Count(todayRecords);
            
            // Populate Recent Activity
            RecentActivity.Clear();
            foreach (var record in todayRecords)
            {
                RecentActivity.Add(record);
            }
        }
    }
}
