using System.Data;
using Microsoft.Data.SqlClient;
using JobGuard.Models;

namespace JobGuard.Data;

public interface IAnalysisRepository
{
    Task<int> CreateJobAnalysisAsync(JobAnalysis analysis);
    Task<JobAnalysis?> GetAnalysisByIdAsync(int analysisId);
    Task<List<JobAnalysis>> GetAnalysesByUserIdAsync(int userId, int limit = 50);
    Task<int> GetUserAnalysisCountAsync(int userId);
    Task<(int TotalCount, int LegitimateCount, int FraudulentCount)> GetUserStatsAsync(int userId);
    Task<(int TotalAnalyses, int TotalUsers, int LegitimateCount, int FraudulentCount, double AvgRiskScore)> GetSystemStatsAsync();
    Task<List<JobAnalysis>> GetRecentAnalysesAsync(int limit = 20);
    Task<int> SaveFeedbackAsync(Feedback feedback);
}

public class AnalysisRepository : IAnalysisRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public AnalysisRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateJobAnalysisAsync(JobAnalysis analysis)
    {
        const string sql = @"
            INSERT INTO JobAnalyses 
            (UserId, CompanyName, JobUrl, JobDescription, ExtractedText, Prediction, RiskScore, RiskCategory, Confidence, SuspiciousFlags, ModelVersion, AnalysisSummary, CreatedAt)
            OUTPUT INSERTED.AnalysisId
            VALUES 
            (@UserId, @CompanyName, @JobUrl, @JobDescription, @ExtractedText, @Prediction, @RiskScore, @RiskCategory, @Confidence, @SuspiciousFlags, @ModelVersion, @AnalysisSummary, @CreatedAt);";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = analysis.UserId;
        cmd.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 200).Value = (object?)analysis.CompanyName ?? DBNull.Value;
        cmd.Parameters.Add("@JobUrl", SqlDbType.NVarChar, 1000).Value = (object?)analysis.JobUrl ?? DBNull.Value;
        cmd.Parameters.Add("@JobDescription", SqlDbType.NVarChar, -1).Value = analysis.JobDescription;
        cmd.Parameters.Add("@ExtractedText", SqlDbType.NVarChar, -1).Value = (object?)analysis.ExtractedText ?? DBNull.Value;
        cmd.Parameters.Add("@Prediction", SqlDbType.NVarChar, 50).Value = analysis.Prediction;
        cmd.Parameters.Add("@RiskScore", SqlDbType.Float).Value = analysis.RiskScore;
        cmd.Parameters.Add("@RiskCategory", SqlDbType.NVarChar, 50).Value = analysis.RiskCategory;
        cmd.Parameters.Add("@Confidence", SqlDbType.Float).Value = analysis.Confidence;
        cmd.Parameters.Add("@SuspiciousFlags", SqlDbType.NVarChar, -1).Value = (object?)analysis.SuspiciousFlags ?? DBNull.Value;
        cmd.Parameters.Add("@ModelVersion", SqlDbType.NVarChar, 50).Value = analysis.ModelVersion;
        cmd.Parameters.Add("@AnalysisSummary", SqlDbType.NVarChar, -1).Value = (object?)analysis.AnalysisSummary ?? DBNull.Value;
        cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = analysis.CreatedAt == default ? DateTime.UtcNow : analysis.CreatedAt;

        await conn.OpenAsync();
        var id = (int?)await cmd.ExecuteScalarAsync();
        return id ?? 0;
    }

    public async Task<JobAnalysis?> GetAnalysisByIdAsync(int analysisId)
    {
        const string sql = @"
            SELECT ja.AnalysisId, ja.UserId, ja.CompanyName, ja.JobUrl, ja.JobDescription, 
                   ja.ExtractedText, ja.Prediction, ja.RiskScore, ja.RiskCategory, ja.Confidence, 
                   ja.SuspiciousFlags, ja.ModelVersion, ja.AnalysisSummary, ja.CreatedAt,
                   u.Email AS UserEmail
            FROM JobAnalyses ja
            LEFT JOIN Users u ON ja.UserId = u.UserId
            WHERE ja.AnalysisId = @AnalysisId;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@AnalysisId", SqlDbType.Int).Value = analysisId;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return MapAnalysis(reader);
        }

        return null;
    }

    public async Task<List<JobAnalysis>> GetAnalysesByUserIdAsync(int userId, int limit = 50)
    {
        var list = new List<JobAnalysis>();
        const string sql = @"
            SELECT TOP (@Limit) ja.AnalysisId, ja.UserId, ja.CompanyName, ja.JobUrl, ja.JobDescription, 
                   ja.ExtractedText, ja.Prediction, ja.RiskScore, ja.RiskCategory, ja.Confidence, 
                   ja.SuspiciousFlags, ja.ModelVersion, ja.AnalysisSummary, ja.CreatedAt,
                   u.Email AS UserEmail
            FROM JobAnalyses ja
            LEFT JOIN Users u ON ja.UserId = u.UserId
            WHERE ja.UserId = @UserId
            ORDER BY ja.CreatedAt DESC;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        cmd.Parameters.Add("@Limit", SqlDbType.Int).Value = limit;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapAnalysis(reader));
        }

        return list;
    }

    public async Task<int> GetUserAnalysisCountAsync(int userId)
    {
        const string sql = "SELECT COUNT(1) FROM JobAnalyses WHERE UserId = @UserId;";
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        await conn.OpenAsync();
        var res = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(res);
    }

    public async Task<(int TotalCount, int LegitimateCount, int FraudulentCount)> GetUserStatsAsync(int userId)
    {
        const string sql = @"
            SELECT 
                COUNT(1) AS TotalCount,
                ISNULL(SUM(CASE WHEN Prediction = 'Legitimate' THEN 1 ELSE 0 END), 0) AS LegitimateCount,
                ISNULL(SUM(CASE WHEN Prediction = 'Fraudulent' THEN 1 ELSE 0 END), 0) AS FraudulentCount
            FROM JobAnalyses
            WHERE UserId = @UserId;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return (
                reader.GetInt32(reader.GetOrdinal("TotalCount")),
                reader.GetInt32(reader.GetOrdinal("LegitimateCount")),
                reader.GetInt32(reader.GetOrdinal("FraudulentCount"))
            );
        }

        return (0, 0, 0);
    }

    public async Task<(int TotalAnalyses, int TotalUsers, int LegitimateCount, int FraudulentCount, double AvgRiskScore)> GetSystemStatsAsync()
    {
        const string sql = @"
            SELECT 
                (SELECT COUNT(1) FROM JobAnalyses) AS TotalAnalyses,
                (SELECT COUNT(1) FROM Users) AS TotalUsers,
                ISNULL(SUM(CASE WHEN Prediction = 'Legitimate' THEN 1 ELSE 0 END), 0) AS LegitimateCount,
                ISNULL(SUM(CASE WHEN Prediction = 'Fraudulent' THEN 1 ELSE 0 END), 0) AS FraudulentCount,
                ISNULL(AVG(RiskScore), 0.0) AS AvgRiskScore
            FROM JobAnalyses;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return (
                reader.GetInt32(reader.GetOrdinal("TotalAnalyses")),
                reader.GetInt32(reader.GetOrdinal("TotalUsers")),
                reader.GetInt32(reader.GetOrdinal("LegitimateCount")),
                reader.GetInt32(reader.GetOrdinal("FraudulentCount")),
                Convert.ToDouble(reader["AvgRiskScore"])
            );
        }

        return (0, 0, 0, 0, 0.0);
    }

    public async Task<List<JobAnalysis>> GetRecentAnalysesAsync(int limit = 20)
    {
        var list = new List<JobAnalysis>();
        const string sql = @"
            SELECT TOP (@Limit) ja.AnalysisId, ja.UserId, ja.CompanyName, ja.JobUrl, ja.JobDescription, 
                   ja.ExtractedText, ja.Prediction, ja.RiskScore, ja.RiskCategory, ja.Confidence, 
                   ja.SuspiciousFlags, ja.ModelVersion, ja.AnalysisSummary, ja.CreatedAt,
                   u.Email AS UserEmail
            FROM JobAnalyses ja
            LEFT JOIN Users u ON ja.UserId = u.UserId
            ORDER BY ja.CreatedAt DESC;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Limit", SqlDbType.Int).Value = limit;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapAnalysis(reader));
        }

        return list;
    }

    public async Task<int> SaveFeedbackAsync(Feedback feedback)
    {
        const string sql = @"
            INSERT INTO Feedback (UserId, AnalysisId, FeedbackType, FeedbackText, CreatedAt)
            OUTPUT INSERTED.FeedbackId
            VALUES (@UserId, @AnalysisId, @FeedbackType, @FeedbackText, @CreatedAt);";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = feedback.UserId;
        cmd.Parameters.Add("@AnalysisId", SqlDbType.Int).Value = feedback.AnalysisId;
        cmd.Parameters.Add("@FeedbackType", SqlDbType.NVarChar, 50).Value = feedback.FeedbackType;
        cmd.Parameters.Add("@FeedbackText", SqlDbType.NVarChar, 1000).Value = (object?)feedback.FeedbackText ?? DBNull.Value;
        cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = feedback.CreatedAt == default ? DateTime.UtcNow : feedback.CreatedAt;

        await conn.OpenAsync();
        var id = (int?)await cmd.ExecuteScalarAsync();
        return id ?? 0;
    }

    private static JobAnalysis MapAnalysis(SqlDataReader reader)
    {
        return new JobAnalysis
        {
            AnalysisId = reader.GetInt32(reader.GetOrdinal("AnalysisId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
            JobUrl = reader.IsDBNull(reader.GetOrdinal("JobUrl")) ? null : reader.GetString(reader.GetOrdinal("JobUrl")),
            JobDescription = reader.GetString(reader.GetOrdinal("JobDescription")),
            ExtractedText = reader.IsDBNull(reader.GetOrdinal("ExtractedText")) ? null : reader.GetString(reader.GetOrdinal("ExtractedText")),
            Prediction = reader.GetString(reader.GetOrdinal("Prediction")),
            RiskScore = Convert.ToDouble(reader["RiskScore"]),
            RiskCategory = reader.GetString(reader.GetOrdinal("RiskCategory")),
            Confidence = Convert.ToDouble(reader["Confidence"]),
            SuspiciousFlags = reader.IsDBNull(reader.GetOrdinal("SuspiciousFlags")) ? null : reader.GetString(reader.GetOrdinal("SuspiciousFlags")),
            ModelVersion = reader.GetString(reader.GetOrdinal("ModelVersion")),
            AnalysisSummary = reader.IsDBNull(reader.GetOrdinal("AnalysisSummary")) ? null : reader.GetString(reader.GetOrdinal("AnalysisSummary")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UserEmail = reader.IsDBNull(reader.GetOrdinal("UserEmail")) ? null : reader.GetString(reader.GetOrdinal("UserEmail"))
        };
    }
}
