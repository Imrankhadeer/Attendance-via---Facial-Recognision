using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using FaceAttendance.Core.Interfaces;

namespace FaceAttendance.Services
{
    public class CameraService : ICameraService
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private bool _isRunning;

        public event EventHandler<byte[]>? FrameCaptured;

        public bool IsRunning => _isRunning;

        public void StartService()
        {
            if (_isRunning) return;

            try 
            {
                System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: Starting camera initialization...\n");
                
                _capture = new VideoCapture(0, VideoCapture.API.DShow);
                System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: DShow IsOpened: {_capture.IsOpened}\n");

                if (!_capture.IsOpened)
                {
                    _capture.Dispose();
                    _capture = new VideoCapture(0, VideoCapture.API.Msmf);
                    System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: MSMF IsOpened: {_capture.IsOpened}\n");
                }
                if (!_capture.IsOpened)
                {
                    _capture.Dispose();
                    _capture = new VideoCapture(0);
                    System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: Default IsOpened: {_capture.IsOpened}\n");
                }

                if (!_capture.IsOpened)
                {
                    throw new Exception("Camera could not be opened using DShow, MSMF, or Any API.");
                }

                System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: Camera successfully opened. Starting capture loop.\n");
                _isRunning = true;
                _cts = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                // Handle or log error
                System.IO.File.AppendAllText("camera_debug.log", $"{DateTime.Now}: Exception: {ex.ToString()}\n");
                System.IO.File.WriteAllText("camera_error.log", $"Error starting camera: {ex.ToString()}");
                Console.WriteLine($"Error starting camera: {ex.Message}");
                _isRunning = false;
            }
        }

        public void StopService()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            // Let the thread die organically to avoid cross-thread deadlocks
            _captureTask = null;
            
            _capture?.Dispose();
            _capture = null;
            _isRunning = false;
        }

        private void CaptureLoop(CancellationToken token)
        {
            using var mat = new Mat();
            
            while (!token.IsCancellationRequested && _capture != null)
            {
                if (_capture.Read(mat) && !mat.IsEmpty)
                {
                    // Convert Mat to byte[] (Bitmap/JPEG) for UI
                    // We use Bitmap to get bytes easily
                    using (var bitmap = mat.ToBitmap())
                    {
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Bmp); // Bmp is fast, Jpeg is smaller
                            var bytes = stream.ToArray();
                            FrameCaptured?.Invoke(this, bytes);
                        }
                    }
                }
                
                // Cap frame rate slightly to avoid 100% CPU loop
                Thread.Sleep(33); // ~30 FPS
            }
        }
    }
}
