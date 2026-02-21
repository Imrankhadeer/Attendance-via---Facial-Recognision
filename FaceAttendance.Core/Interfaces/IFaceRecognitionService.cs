using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace FaceAttendance.Core.Interfaces
{
    public record FaceDetectionResult(Rectangle Box, PointF[] Landmarks, float Score);

    public interface IFaceRecognitionService
    {
        Task<List<FaceDetectionResult>> DetectFacesAsync(byte[] imageBytes);
        Task<byte[]> GenerateEmbeddingAsync(byte[] imageBytes, FaceDetectionResult face);
        float CalculateSimilarity(byte[] embedding1, byte[] embedding2);
    }
}
