using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Services;

namespace FaceAttendance.UI.ViewModels
{
    public partial class RegisterViewModel : ObservableObject, IDisposable
    {
        private readonly ICameraService _cameraService;
        private readonly AttendanceService _attendanceService;
        private readonly IFaceRecognitionService _recognitionService;

        private bool _isCapturing;

        [ObservableProperty]
        private string _studentName = string.Empty;

        [ObservableProperty]
        private string _studentId = string.Empty;

        [ObservableProperty]
        private BitmapImage? _cameraFeed;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        private byte[]? _currentFrame;

        public RegisterViewModel(ICameraService cameraService, AttendanceService attendanceService, IFaceRecognitionService recognitionService)
        {
            _cameraService = cameraService;
            _attendanceService = attendanceService;
            _recognitionService = recognitionService;
            
            _cameraService.FrameCaptured += OnFrameCaptured;
            _cameraService.StartService();
        }

        private void OnFrameCaptured(object? sender, byte[] bytes)
        {
            _currentFrame = bytes;
            
            // Update UI on UI thread non-blockingly
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                CameraFeed = image;
            });
        }

        [RelayCommand]
        public async Task CaptureAndRegister()
        {
            if (_currentFrame == null) 
            {
                StatusMessage = "No camera feed.";
                return;
            }

            if (string.IsNullOrWhiteSpace(StudentName) || string.IsNullOrWhiteSpace(StudentId))
            {
                StatusMessage = "Name and ID required.";
                return;
            }

            if (_isCapturing) return;
            _isCapturing = true;

            try 
            {
                StatusMessage = "Analyzing face stability... Please hold still.";
                
                // Stability Check Loop
                bool isStable = false;
                System.Drawing.Rectangle lastBox = System.Drawing.Rectangle.Empty;
                int stableFramesCount = 0;
                
                // Wait for up to 5 seconds to get 3 consecutive stable frames
                var timeoutTask = Task.Delay(5000);
                
                await Task.Run(async () =>
                {
                    while (!isStable && !timeoutTask.IsCompleted)
                    {
                        var frame = _currentFrame;
                        if (frame == null) { await Task.Delay(100); continue; }

                        var detections = await _recognitionService.DetectFacesAsync(frame);
                        if (detections.Count == 1 && detections[0].Score > 0.6)
                        {
                            var box = detections[0].Box;
                            if (lastBox != System.Drawing.Rectangle.Empty)
                            {
                                // Check if box center moved significantly (> 5% of width)
                                float dx = Math.Abs(box.X - lastBox.X);
                                float dy = Math.Abs(box.Y - lastBox.Y);
                                
                                if (dx < box.Width * 0.05f && dy < box.Height * 0.05f)
                                {
                                    stableFramesCount++;
                                    if (stableFramesCount >= 3)
                                    {
                                        isStable = true;
                                    }
                                }
                                else
                                {
                                    stableFramesCount = 0; // reset
                                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = "Movement detected. Please hold still...");
                                }
                            }
                            lastBox = box;
                        }
                        else
                        {
                            stableFramesCount = 0;
                            lastBox = System.Drawing.Rectangle.Empty;
                            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = "Looking for a single clear face...");
                        }
                        await Task.Delay(150); // Frame sampling delay
                    }
                    
                    if (isStable)
                    {
                        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = "Capturing...");
                        await _attendanceService.RegisterStudentAsync(StudentName, StudentId, _currentFrame!);
                    }
                });

                if (isStable)
                {
                    StatusMessage = $"Registered {StudentName} successfully!";
                    StudentName = "";
                    StudentId = "";
                }
                else
                {
                    StatusMessage = "Registration failed. Could not capture a stable face. Try again.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isCapturing = false;
            }
        }
        
        public void Dispose()
        {
             _cameraService.FrameCaptured -= OnFrameCaptured;
             _cameraService.StopService();
        }
    }
}
