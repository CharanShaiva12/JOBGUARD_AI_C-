using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using JobGuard.ML;

namespace JobGuard.ML.Trainer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine(" JobGuard ML.NET Model Training Pipeline");
        Console.WriteLine(" Algorithm: TF-IDF Text Feature Extraction + Logistic Regression");
        Console.WriteLine(" Dataset: EMSCAD / Balanced Real & Fake Job Postings");
        Console.WriteLine("==========================================================");

        var basePath = AppContext.BaseDirectory;
        // Search for dataset file in standard locations
        string datasetPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "JobGuard", "ML", "Datasets", "balanced.csv"));
        if (!File.Exists(datasetPath))
        {
            datasetPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "JobGuard", "ML", "Datasets", "balanced.csv"));
        }
        if (!File.Exists(datasetPath))
        {
            datasetPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "JobGuard", "ML", "Datasets", "balanced.csv"));
        }

        if (!File.Exists(datasetPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Dataset not found at: {datasetPath}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"[1/5] Loading and parsing dataset from: {datasetPath}");

        var mlContext = new MLContext(seed: 42);

        // Preprocess dataset in memory into JobTrainingRecord items to clean quotes and combine text fields
        var records = LoadAndPreprocessData(datasetPath);
        Console.WriteLine($"[2/5] Cleaned and processed {records.Count} valid job posting records.");
        int fakeCount = records.Count(r => r.Label);
        int realCount = records.Count(r => !r.Label);
        Console.WriteLine($"      Distribution: {realCount} Legitimate, {fakeCount} Fraudulent.");

        var dataView = mlContext.Data.LoadFromEnumerable(records);

        // Train / Test Split: 80% Train, 20% Held-Out Test
        var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);
        Console.WriteLine($"[3/5] Split dataset: 80% Training ({split.TrainSet.GetRowCount()} rows), 20% Test ({split.TestSet.GetRowCount()} rows).");

        // Define Pipeline: FeaturizeText (TF-IDF N-grams) -> LbfgsLogisticRegression
        var pipeline = mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                inputColumnName: nameof(JobTrainingRecord.CombinedText))
            .Append(mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(
                labelColumnName: nameof(JobTrainingRecord.Label),
                featureColumnName: "Features",
                l1Regularization: 0.1f,
                l2Regularization: 0.1f));

        Console.WriteLine("[4/5] Training Logistic Regression model with TF-IDF features...");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var model = pipeline.Fit(split.TrainSet);
        watch.Stop();
        Console.WriteLine($"      Training completed in {watch.ElapsedMilliseconds} ms.");

        // Evaluate on Held-Out Test Set
        Console.WriteLine("[5/5] Evaluating model performance on held-out test data...");
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.BinaryClassification.Evaluate(
            data: predictions,
            labelColumnName: nameof(JobTrainingRecord.Label),
            scoreColumnName: "Score",
            predictedLabelColumnName: "PredictedLabel");

        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine(" EVALUATION METRICS ON HELD-OUT TEST PARTITION:");
        Console.WriteLine($" Accuracy:             {metrics.Accuracy:P2}");
        Console.WriteLine($" Area Under ROC (AUC): {metrics.AreaUnderRocCurve:P2}");
        Console.WriteLine($" F1-Score:             {metrics.F1Score:P2}");
        Console.WriteLine($" Positive Precision:   {metrics.PositivePrecision:P2}");
        Console.WriteLine($" Positive Recall:      {metrics.PositiveRecall:P2}");
        Console.WriteLine($" Negative Precision:   {metrics.NegativePrecision:P2}");
        Console.WriteLine($" Negative Recall:      {metrics.NegativeRecall:P2}");
        Console.WriteLine($" Log-Loss:             {metrics.LogLoss:F4}");
        Console.WriteLine(" Confusion Matrix:");
        Console.WriteLine(metrics.ConfusionMatrix.GetFormattedConfusionTable());
        Console.WriteLine("----------------------------------------------------------");

        // Save trained model to JobGuard/ML/Models/FakeJobModel.zip
        var modelSaveDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(datasetPath)!, "..", "Models"));
        Directory.CreateDirectory(modelSaveDir);
        var modelSavePath = Path.Combine(modelSaveDir, "FakeJobModel.zip");

        mlContext.Model.Save(model, split.TrainSet.Schema, modelSavePath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] Model successfully serialized and saved to:\n{modelSavePath}");
        Console.ResetColor();

        // Also test a quick sample prediction
        var engine = mlContext.Model.CreatePredictionEngine<JobPredictionInput, JobPredictionOutput>(model);
        var sampleTest = engine.Predict(new JobPredictionInput
        {
            CombinedText = "Urgent data entry assistant needed. Work from home 2 hours daily. Earn $500 daily. Send your bank credentials to wire payment."
        });
        Console.WriteLine($"Sample test prediction: Fraudulent={sampleTest.PredictedLabel}, Probability={sampleTest.Probability:P2}");
    }

    public class JobTrainingRecord
    {
        public bool Label { get; set; }
        public string CombinedText { get; set; } = string.Empty;
    }

    private static List<JobTrainingRecord> LoadAndPreprocessData(string csvPath)
    {
        var list = new List<JobTrainingRecord>();
        using var reader = new StreamReader(csvPath);

        // Header line
        string? header = reader.ReadLine();
        if (header == null) return list;

        // Custom RFC4180 CSV parser
        while (!reader.EndOfStream)
        {
            var row = ReadCsvRow(reader);
            if (row == null || row.Count < 18) continue;

            // Columns:
            // 0: job_id, 1: title, 2: location, 3: department, 4: salary_range, 5: company_profile,
            // 6: description, 7: requirements, 8: benefits, 9: telecommuting, 10: has_company_logo,
            // 11: has_questions, 12: employment_type, 13: required_experience, 14: required_education,
            // 15: industry, 16: function, 17: fraudulent

            string title = row[1];
            string companyProfile = row[5];
            string description = row[6];
            string requirements = row[7];
            string benefits = row[8];
            string fraudStr = row[17].Trim();

            if (!bool.TryParse(fraudStr, out bool isFraud) && fraudStr != "0" && fraudStr != "1")
            {
                continue;
            }

            bool fraudBool = fraudStr == "1" || fraudStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            string combined = $"{title} {companyProfile} {description} {requirements} {benefits}".Trim();
            if (string.IsNullOrWhiteSpace(combined) || combined.Length < 10)
            {
                continue;
            }

            // Normalize whitespace
            combined = Regex.Replace(combined, @"\s+", " ");

            list.Add(new JobTrainingRecord
            {
                Label = fraudBool,
                CombinedText = combined
            });
        }

        return list;
    }

    private static List<string>? ReadCsvRow(StreamReader reader)
    {
        var row = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;

        while (true)
        {
            int nextChar = reader.Read();
            if (nextChar == -1)
            {
                if (row.Count == 0 && currentField.Length == 0) return null;
                row.Add(currentField.ToString());
                return row;
            }

            char c = (char)nextChar;

            if (inQuotes)
            {
                if (c == '"')
                {
                    int peek = reader.Peek();
                    if (peek == '"')
                    {
                        currentField.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (reader.Peek() == '\n') reader.Read();
                    row.Add(currentField.ToString());
                    return row;
                }
                else if (c == '\n')
                {
                    row.Add(currentField.ToString());
                    return row;
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }
    }
}
