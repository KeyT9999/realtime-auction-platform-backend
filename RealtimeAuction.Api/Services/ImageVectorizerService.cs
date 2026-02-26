using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RealtimeAuction.Api.Services
{
    public class ImageVectorizerService : IDisposable
    {
        private readonly InferenceSession _session;

        public ImageVectorizerService(IWebHostEnvironment env)
        {
            var modelPath = Path.Combine(env.ContentRootPath, "resnet18-v1-7.onnx");
            _session = new InferenceSession(modelPath);
        }

        public float[] GetVectorFromImageBytes(byte[] imageBytes)
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            
            // Resize image to 224x224
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(224, 224),
                Mode = ResizeMode.Crop
            }));

            // Preprocess to tensor directly (NCHW format for ResNet)
            var input = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var stddev = new[] { 0.229f, 0.224f, 0.225f };

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgb24> pixelSpan = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var pixel = pixelSpan[x];
                        input[0, 0, y, x] = ((pixel.R / 255f) - mean[0]) / stddev[0];
                        input[0, 1, y, x] = ((pixel.G / 255f) - mean[1]) / stddev[1];
                        input[0, 2, y, x] = ((pixel.B / 255f) - mean[2]) / stddev[2];
                    }
                }
            });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("data", input)
            };

            using var results = _session.Run(inputs);
            // The ResNet18 model outputs a 1000-dimensional vector
            var output = results.First().AsEnumerable<float>().ToArray();
            
            return output;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
