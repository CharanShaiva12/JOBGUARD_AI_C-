using Microsoft.ML.Data;

namespace JobGuard.ML;

public class JobData
{
    [LoadColumn(0)]
    public float JobId { get; set; }

    [LoadColumn(1)]
    public string? Title { get; set; }

    [LoadColumn(2)]
    public string? Location { get; set; }

    [LoadColumn(3)]
    public string? Department { get; set; }

    [LoadColumn(4)]
    public string? SalaryRange { get; set; }

    [LoadColumn(5)]
    public string? CompanyProfile { get; set; }

    [LoadColumn(6)]
    public string? Description { get; set; }

    [LoadColumn(7)]
    public string? Requirements { get; set; }

    [LoadColumn(8)]
    public string? Benefits { get; set; }

    [LoadColumn(9)]
    public float Telecommuting { get; set; }

    [LoadColumn(10)]
    public float HasCompanyLogo { get; set; }

    [LoadColumn(11)]
    public float HasQuestions { get; set; }

    [LoadColumn(12)]
    public string? EmploymentType { get; set; }

    [LoadColumn(13)]
    public string? RequiredExperience { get; set; }

    [LoadColumn(14)]
    public string? RequiredEducation { get; set; }

    [LoadColumn(15)]
    public string? Industry { get; set; }

    [LoadColumn(16)]
    public string? Function { get; set; }

    [LoadColumn(17)]
    public bool Fraudulent { get; set; }
}

public class JobPredictionInput
{
    public string CombinedText { get; set; } = string.Empty;
}

public class JobPredictionOutput
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; } // true = Fraudulent, false = Legitimate

    [ColumnName("Probability")]
    public float Probability { get; set; } // Probability of Fraudulent

    [ColumnName("Score")]
    public float Score { get; set; }
}
