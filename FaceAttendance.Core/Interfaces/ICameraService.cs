using System;
using System.Threading.Tasks;

namespace FaceAttendance.Core.Interfaces
{
    public interface ICameraService
    {
        void StartService();
        void StopService();
        bool IsRunning { get; }
        
        event EventHandler<byte[]> FrameCaptured; // Returns JPEG or Bitmap data
    }
}
