using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FaceAttendance.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private object? _currentViewModel;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            // Default to Dashboard
            NavigateToDashboard();
        }

        private void ChangeViewModel<TViewModel>() where TViewModel : class
        {
            // Do not switch if already on the requested view
            if (CurrentViewModel is TViewModel) return;

            // Dispose the current view model first, so it releases resources (like CameraService)
            if (CurrentViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Now resolve and set the new view model, which can safely acquire resources
            CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
        }

        [RelayCommand]
        public void NavigateToDashboard() => ChangeViewModel<DashboardViewModel>();

        [RelayCommand]
        public void NavigateToRegister() => ChangeViewModel<RegisterViewModel>();

        [RelayCommand]
        public void NavigateToAttendance() => ChangeViewModel<AttendanceViewModel>();
    }
}
