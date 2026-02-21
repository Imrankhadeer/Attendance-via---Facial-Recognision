using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using FaceAttendance.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceAttendance.Services
{
    public class FaceRecognitionService : IFaceRecognitionService, IDisposable
    {
        private InferenceSession? _detSession;
        private InferenceSession? _recSession;
        
        // Config
        private const float DetThreshold = 0.5f;
        private const float NmsThreshold = 0.4f;
        private readonly int[] _detInputSize = new[] { 640, 640 }; // 640x640 is standard for SCRFD

        // Standard 5 landmarks for 112x112 alignment
        private readonly PointF[] _refLandmarks = new[]
        {
            new PointF(38.2946f, 51.6963f),
            new PointF(73.5318f, 51.5014f),
            new PointF(56.0252f, 71.7366f),
            new PointF(41.5493f, 92.3655f),
            new PointF(70.7299f, 92.2041f)
        };

        public FaceRecognitionService()
        {
            try 
            {
                var options = new SessionOptions();
                // options.AppendExecutionProvider_CPU(); 
                
                // Assuming buffalo_l paths relative to executable or explicit
                // Adjust strict paths based on deployment
                string detPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "buffalo_l", "det_10g.onnx");
                string recPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "buffalo_l", "w600k_r50.onnx");

                if (File.Exists(detPath)) _detSession = new InferenceSession(detPath, options);
                if (File.Exists(recPath)) _recSession = new InferenceSession(recPath, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading models: {ex.Message}");
            }
        }

        public async Task<List<FaceDetectionResult>> DetectFacesAsync(byte[] imageBytes)
        {
            if (_detSession == null) return new List<FaceDetectionResult>();

            return await Task.Run(() =>
            {
                try 
                {
                    using var ms = new MemoryStream(imageBytes);
                    using var originalImage = new Bitmap(ms);
                    
                    // Preprocess
                    var (tensor, ratio, padX, padY) = PreprocessDetection(originalImage);
                    
                    // Run Inference
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(_detSession.InputMetadata.Keys.First(), tensor)
                    };

                    using var results = _detSession.Run(inputs);
                    
                    // SCRFD Post-process (Decoding outputs)
                    // Outputs: typically multiple generic "output" names or specific names like "score_8", "bbox_8" etc.
                    // We need to handle dynamic names or indices.
                    // Usually outputs are sorted by stride: 8, 16, 32
                    // 3 outputs per stride: score (1x1xH/sxW/s), bbox (1x4x...), kps (1x10x...) or similar
                    // Let's implement robust decoding based on output shapes.
                    
                    var detections = DecodeScrfdOutputs(results, ratio);
                    
                    // NMS
                    return Nms(detections);
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("onnx_debug.log", $"Detection Error: {ex.ToString()}\n");
                    Console.WriteLine($"Detection Error: {ex.Message}");
                    return new List<FaceDetectionResult>();
                }
            });
        }

        public async Task<byte[]> GenerateEmbeddingAsync(byte[] imageBytes, FaceDetectionResult face)
        {
            if (_recSession == null) return Array.Empty<byte>();

            return await Task.Run(() =>
            {
                try 
                {
                    using var ms = new MemoryStream(imageBytes);
                    using var originalImage = new Bitmap(ms);
                    
                    // Align and Crop
                    using var alignedFace = AlignFace(originalImage, face.Landmarks);
                    try { alignedFace.Save("aligned_debug.jpg", ImageFormat.Jpeg); } catch {}
                    
                    // Recognition Preprocess
                    var tensor = PreprocessRecognition(alignedFace);
                    
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(_recSession.InputMetadata.Keys.First(), tensor)
                    };

                    using var results = _recSession.Run(inputs);
                    var output = results.First().AsTensor<float>();
                    var embedding = Normalize(output.ToArray());
                    
                    // Initializing byte array
                    var byteEmbedding = new byte[embedding.Length * 4];
                    Buffer.BlockCopy(embedding, 0, byteEmbedding, 0, byteEmbedding.Length);
                    
                    return byteEmbedding;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Recognition Error: {ex.Message}");
                    return Array.Empty<byte>();
                }
            });
        }

        public float CalculateSimilarity(byte[] embedding1, byte[] embedding2)
        {
             if (embedding1.Length == 0 || embedding2.Length == 0) return 0;
            
            var float1 = new float[embedding1.Length / 4];
            var float2 = new float[embedding2.Length / 4];
            
            Buffer.BlockCopy(embedding1, 0, float1, 0, embedding1.Length);
            Buffer.BlockCopy(embedding2, 0, float2, 0, embedding2.Length);
            
            var dot = float1.Zip(float2, (a, b) => a * b).Sum();
            var mag1 = Math.Sqrt(float1.Sum(x => x * x));
            var mag2 = Math.Sqrt(float2.Sum(x => x * x));
            
            return (float)(dot / (mag1 * mag2));
        }

        #region Private Helpers

        private (DenseTensor<float>, float ratio, float padX, float padY) PreprocessDetection(Bitmap image)
        {
            int targetW = _detInputSize[0];
            int targetH = _detInputSize[1];
            
            float ratio = Math.Min((float)targetW / image.Width, (float)targetH / image.Height);
            int newW = (int)(image.Width * ratio);
            int newH = (int)(image.Height * ratio);
            
            using var resized = new Bitmap(targetW, targetH);
            using (var g = Graphics.FromImage(resized))
            {
                g.Clear(Color.Black);
                g.DrawImage(image, 0, 0, newW, newH);
            }

            var tensor = new DenseTensor<float>(new[] { 1, 3, targetH, targetW });
            
            // Bitmap lock bits for speed
            var data = resized.LockBits(new Rectangle(0, 0, targetW, targetH), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;
                
                for (int y = 0; y < targetH; y++)
                {
                    for (int x = 0; x < targetW; x++)
                    {
                        // Normalize 0-255 to usually - mean / std? 
                        // SCRFD usually requires 0-255 input directly or simple mean subtraction?
                        // InsightFace models often take RGB mean 127.5 std 128.0 (normalized -1 to 1) 
                        // OR simple sub mean. Checking config... 
                        // Default InsightFace `det_10g` uses mean 127.5, std 128.0.
                        
                        // BGR to RGB? Bitmap is BGR in memory usually (Format24bppRgb is distinct but verify).
                        // Windows Bitmap is BGR.
                        
                        byte b = ptr[y * stride + x * 3];
                        byte g = ptr[y * stride + x * 3 + 1];
                        byte r = ptr[y * stride + x * 3 + 2];
                        
                        tensor[0, 0, y, x] = (r - 127.5f) / 128.0f;
                        tensor[0, 1, y, x] = (g - 127.5f) / 128.0f;
                        tensor[0, 2, y, x] = (b - 127.5f) / 128.0f;
                    }
                }
            }
            resized.UnlockBits(data);
            
            return (tensor, ratio, 0, 0);
        }

        private List<FaceDetectionResult> DecodeScrfdOutputs(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, float ratio)
        {
            var detections = new List<FaceDetectionResult>();
            
            // Simplified decoding knowing structure of det_10g (strides 8, 16, 32)
            // Need to map outputs correctly. 
            // In C# OnnxRuntime, we iterate.
            
            // This part is tricky without exact output names.
            // But usually they come in order: score_8, bbox_8, kps_8, score_16...
            // Let's assume order or scan by shape.
            
            var resultList = results.ToList();
            var debugInfo = string.Join("\n", resultList.Select(r => $"{r.Name}: {string.Join(",", r.AsTensor<float>().Dimensions.ToArray())}"));
            System.IO.File.WriteAllText("onnx_debug.log", $"ONNX Outputs:\n{debugInfo}\n");

            int[] strides = { 8, 16, 32 };
            
            for (int i = 0; i < strides.Length; i++)
            {
                int stride = strides[i];
                // Assuming result order: score, bbox, kps for stride 8, then 16, then 32
                // Or checking shapes.
                // Score: 1x1x(H/s)x(W/s) ? NO, SCRFD usually: 1x(H*W*nA)x1
                // Let's implement simplified anchoring using loops if shape assumption fails.
                
                // For robustness, I will just return a placeholder logic if I can't guarantee output format.
                // But to make it work, I must interpret them.
                // Looking at standard buffalo_l export: 9 outputs total.
                // indices: 0=score8, 1=bbox8, 2=kps8, 3=score16... 
                
                var scoreTensor = resultList[i].AsTensor<float>(); // 0, 1, 2 = scores
                var bboxTensor = resultList[i + 3].AsTensor<float>(); // 3, 4, 5 = bboxes
                var kpsTensor = resultList[i + 6].AsTensor<float>(); // 6, 7, 8 = kps
                
                // Process each stride
                int featW = _detInputSize[0] / stride;
                int featH = _detInputSize[1] / stride;
                int numAnchorsPerPixel = scoreTensor.Dimensions[0] / (featW * featH); 
                
                int index = 0;
                for (int y = 0; y < featH; y++)
                {
                    for (int x = 0; x < featW; x++)
                    {
                        for (int a = 0; a < numAnchorsPerPixel; a++)
                        {
                            float score = scoreTensor[index, 0];
                            if (score >= DetThreshold)
                            {
                                float anchorX = x * stride; 
                                float anchorY = y * stride;
                                
                                float l = bboxTensor[index, 0] * stride;
                                float t = bboxTensor[index, 1] * stride;
                                float r = bboxTensor[index, 2] * stride;
                                float b = bboxTensor[index, 3] * stride;
                                
                                float x1 = anchorX - l;
                                float y1 = anchorY - t;
                                float x2 = anchorX + r;
                                float y2 = anchorY + b;
                                
                                var kps = new PointF[5];
                                for (int k = 0; k < 5; k++)
                                {
                                    float kx = kpsTensor[index, k * 2] * stride;
                                    float ky = kpsTensor[index, k * 2 + 1] * stride;
                                    kps[k] = new PointF((anchorX + kx) / ratio, (anchorY + ky) / ratio);
                                }
                                
                                var rect = new Rectangle((int)(x1 / ratio), (int)(y1 / ratio), (int)((x2 - x1) / ratio), (int)((y2 - y1) / ratio));
                                detections.Add(new FaceDetectionResult(rect, kps, score));
                            }
                            index++;
                        }
                    }
                }
            }

            return detections;
        }
        
        private List<FaceDetectionResult> Nms(List<FaceDetectionResult> dets)
        {
            var sorted = dets.OrderByDescending(d => d.Score).ToList();
            var result = new List<FaceDetectionResult>();
            
            while (sorted.Count > 0)
            {
                var current = sorted[0];
                result.Add(current);
                sorted.RemoveAt(0);
                
                sorted.RemoveAll(d => Iou(current.Box, d.Box) > NmsThreshold);
            }
            
            return result;
        }

        private float Iou(Rectangle r1, Rectangle r2)
        {
            Rectangle intersect = Rectangle.Intersect(r1, r2);
            float interArea = intersect.Width * intersect.Height;
            if (intersect.Width < 0 || intersect.Height < 0) interArea = 0;
            
            float unionArea = (r1.Width * r1.Height) + (r2.Width * r2.Height) - interArea;
            return interArea / unionArea;
        }

        private Bitmap AlignFace(Bitmap src, PointF[] landmarks)
        {
            // Use Emgu.CV for warp affine
            // Convert Bitmap to Image<Bgr, byte>
            using var img = src.ToImage<Bgr, byte>();
            
            var srcPts = landmarks;
            var dstPts = _refLandmarks;
            
            // Start Affine Transformation
            // Need 3 points for affine. Use Left Eye, Right Eye, and Nose for better stability.
            PointF[] src3 = { srcPts[0], srcPts[1], srcPts[2] }; 
            PointF[] dst3 = { dstPts[0], dstPts[1], dstPts[2] };
            
            using var transMat = CvInvoke.GetAffineTransform(src3, dst3);
            var aligned = new Image<Bgr, byte>(112, 112);
            CvInvoke.WarpAffine(img, aligned, transMat, new Size(112, 112));
            
            return aligned.ToBitmap();
        }

        private DenseTensor<float> PreprocessRecognition(Bitmap bitmap)
        {
            // ArcFace: 112x112, RGB, (x - 127.5)/128
            var tensor = new DenseTensor<float>(new[] { 1, 3, 112, 112 });
            
            var data = bitmap.LockBits(new Rectangle(0, 0, 112, 112), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;
                
                for (int y = 0; y < 112; y++)
                {
                    for (int x = 0; x < 112; x++)
                    {
                        byte b = ptr[y * stride + x * 3];
                        byte g = ptr[y * stride + x * 3 + 1];
                        byte r = ptr[y * stride + x * 3 + 2];
                        
                        tensor[0, 0, y, x] = (r - 127.5f) / 128.0f;
                        tensor[0, 1, y, x] = (g - 127.5f) / 128.0f;
                        tensor[0, 2, y, x] = (b - 127.5f) / 128.0f;
                    }
                }
            }
            bitmap.UnlockBits(data);
            return tensor;
        }
        
        private float[] Normalize(float[] v)
        {
            var sum = v.Sum(x => x * x);
            var norm = (float)Math.Sqrt(sum);
            return v.Select(x => x / norm).ToArray();
        }
        
        public void Dispose()
        {
            _detSession?.Dispose();
            _recSession?.Dispose();
        }

        #endregion
    }
}
