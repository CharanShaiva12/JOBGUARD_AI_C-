using Microsoft.ML;
using JobGuard.ML;

namespace JobGuard.Services;

public interface IMachineLearningService
{
    bool IsModelLoaded { get; }
    string ModelVersion { get; }
    JobPredictionOutput Predict(string text);
}

public class MachineLearningService : IMachineLearningService
{
    private readonly ILogger<MachineLearningService> _logger;
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<JobPredictionInput, JobPredictionOutput>? _predictionEngine;
    private readonly object _syncLock = new();

    public bool IsModelLoaded => _predictionEngine != null;
    public string ModelVersion => "1.0.0-LR-TFIDF";

    public MachineLearningService(IWebHostEnvironment env, ILogger<MachineLearningService> logger)
    {
        _logger = logger;
        _mlContext = new MLContext(seed: 42);

        LoadModel(env.ContentRootPath);
    }

    private void LoadModel(string contentRootPath)
    {
        try
        {
            var modelPath = Path.Combine(contentRootPath, "ML", "Models", "FakeJobModel.zip");
            if (!File.Exists(modelPath))
            {
                // Fallback for bin directory or development workspace
                modelPath = Path.Combine(AppContext.BaseDirectory, "ML", "Models", "FakeJobModel.zip");
            }

            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("Machine Learning model artifact not found at {Path}. ML inference will be offline.", modelPath);
                return;
            }

            using var stream = File.OpenRead(modelPath);
            _model = _mlContext.Model.Load(stream, out var schema);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<JobPredictionInput, JobPredictionOutput>(_model);
            _logger.LogInformation("JobGuard ML.NET model loaded successfully from {Path}.", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ML.NET model file.");
        }
    }

    public JobPredictionOutput Predict(string text)
    {
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Machine learning model is not loaded or unavailable.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new JobPredictionOutput
            {
                PredictedLabel = false,
                Probability = 0f,
                Score = 0f
            };
        }

        lock (_syncLock)
        {
            return _predictionEngine.Predict(new JobPredictionInput
            {
                CombinedText = text
            });
        }
    }
}
