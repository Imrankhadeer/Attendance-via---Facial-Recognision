using System;
using System.Windows;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Data.Repositories;
using FaceAttendance.Services;
using FaceAttendance.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FaceAttendance.UI
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                System.IO.File.WriteAllText("crash.log", e.Exception.ToString());
                MessageBox.Show(e.Exception.ToString(), "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
                Shutdown();
            };

            try
            {
                Services = ConfigureServices();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfiguration configuration = builder.Build();
            services.AddSingleton(configuration);

            // Services
            services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
            services.AddSingleton<ICameraService, CameraService>();
            services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();
            services.AddSingleton<AttendanceService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<AttendanceViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
