using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FaceAttendance.Core.Interfaces;
using FaceAttendance.Services;

namespace FaceAttendance.UI.ViewModels
{
    public partial class AttendanceViewModel : ObservableObject, IDisposable
    {
        private readonly ICameraService _cameraService;
        private readonly AttendanceService _attendanceService;
        private bool _isProcessing;

        [ObservableProperty]
        private BitmapImage? _cameraFeed;

        [ObservableProperty]
        private string _resultMessage = "Waiting for face...";

        [ObservableProperty]
        private string _lastRecognizedName = "-";

        [ObservableProperty]
        private string _confidenceScore = "-";

        public AttendanceViewModel(ICameraService cameraService, AttendanceService attendanceService)
        {
            _cameraService = cameraService;
            _attendanceService = attendanceService;
            
            _cameraService.FrameCaptured += OnFrameCaptured;
            _cameraService.StartService();
        }

        private void OnFrameCaptured(object? sender, byte[] bytes)
        {
            // First, update the camera feed for smooth video
            // But if we want to draw boxes, we need to process it first or overlay.
            // Overlaying is better for performance (don't draw on every frame if detection is slow).
            // But for simplicity, we draw on the frame we display.
            
            // Running detection every frame is heavy. 
            // Better: Run detection in background, update overlay. 
            // Even simpler for this requirement: Run detection on every frame if fast enough, 
            // or stick to the "Process periodically" model and just draw *last known* boxes?
            // Let's try processing periodically and drawing on that specific frame.
            
            if (_isProcessing) 
            {
                 // Show raw feed if processing
                 UpdateImage(bytes);
                 return;
            }

            _isProcessing = true;
            
            // Run processing in background to prevent UI jitter
            _ = Task.Run(async () =>
            {
                try 
                {
                    // Run detection/recognition
                    var results = await _attendanceService.ProcessFrameAsync(bytes);
                    
                    // Draw boxes if any
                    if (results.Any())
                    {
                        using var ms = new MemoryStream(bytes);
                        using var bitmap = new Bitmap(ms);
                        using var g = Graphics.FromImage(bitmap);
                        var pen = new Pen(Color.LimeGreen, 3);
                        var font = new Font("Arial", 16);
                        var brush = Brushes.LimeGreen;

                        foreach (var (student, alreadyMarked, sessionStatus, confidence, face) in results)
                        {
                            g.DrawRectangle(pen, face.Box);
                            
                            string name = student?.Name ?? "Unknown";
                            string info = $"{name} ({confidence:P0})";
                            if (alreadyMarked) info += " [Marked]";
                            
                            // Draw header background
                            var size = g.MeasureString(info, font);
                            g.FillRectangle(Brushes.Black, face.Box.X, face.Box.Y - size.Height, size.Width, size.Height);
                            g.DrawString(info, font, brush, face.Box.X, face.Box.Y - size.Height);

                            // Update ViewModel properties for side panel (just show first match)
                            if (student != null)
                            {
                               _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                               {
                                   LastRecognizedName = student.Name;
                                   ConfidenceScore = confidence.ToString("P1");
                                   ResultMessage = sessionStatus;
                               });
                            }
                        }

                        // Save drawn bitmap to bytes for display
                        using var outStream = new MemoryStream();
                        bitmap.Save(outStream, ImageFormat.Bmp);
                        UpdateImage(outStream.ToArray());
                    }
                    else
                    {
                        UpdateImage(bytes);
                         _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                         {
                             ResultMessage = "Scanning...";
                         });
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                     UpdateImage(bytes);
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }
        
        private void UpdateImage(byte[] bytes)
        {
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
        
        public void Dispose()
        {
            _cameraService.FrameCaptured -= OnFrameCaptured;
            _cameraService.StopService();
        }
    }
}
