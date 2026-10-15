using System.Data;
using Microsoft.Data.SqlClient;
using JobGuard.Models;

namespace JobGuard.Data;

public interface ICookieConsentRepository
{
    Task<int> SaveConsentAsync(CookieConsent consent);
    Task<CookieConsent?> GetConsentByIdentifierAsync(string identifier);
}

public class CookieConsentRepository : ICookieConsentRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CookieConsentRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> SaveConsentAsync(CookieConsent consent)
    {
        const string sql = @"
            INSERT INTO CookieConsent (UserId, ConsentIdentifier, AcceptedEssential, AcceptedAnalytics, AcceptedPreferences, PolicyVersion, ConsentDate)
            OUTPUT INSERTED.ConsentId
            VALUES (@UserId, @ConsentIdentifier, @AcceptedEssential, @AcceptedAnalytics, @AcceptedPreferences, @PolicyVersion, @ConsentDate);";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = (object?)consent.UserId ?? DBNull.Value;
        cmd.Parameters.Add("@ConsentIdentifier", SqlDbType.NVarChar, 100).Value = consent.ConsentIdentifier;
        cmd.Parameters.Add("@AcceptedEssential", SqlDbType.Bit).Value = consent.AcceptedEssential;
        cmd.Parameters.Add("@AcceptedAnalytics", SqlDbType.Bit).Value = consent.AcceptedAnalytics;
        cmd.Parameters.Add("@AcceptedPreferences", SqlDbType.Bit).Value = consent.AcceptedPreferences;
        cmd.Parameters.Add("@PolicyVersion", SqlDbType.NVarChar, 20).Value = consent.PolicyVersion;
        cmd.Parameters.Add("@ConsentDate", SqlDbType.DateTime2).Value = consent.ConsentDate == default ? DateTime.UtcNow : consent.ConsentDate;

        await conn.OpenAsync();
        var id = (int?)await cmd.ExecuteScalarAsync();
        return id ?? 0;
    }

    public async Task<CookieConsent?> GetConsentByIdentifierAsync(string identifier)
    {
        const string sql = @"
            SELECT TOP 1 ConsentId, UserId, ConsentIdentifier, AcceptedEssential, AcceptedAnalytics, AcceptedPreferences, PolicyVersion, ConsentDate
            FROM CookieConsent
            WHERE ConsentIdentifier = @ConsentIdentifier
            ORDER BY ConsentDate DESC;";

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@ConsentIdentifier", SqlDbType.NVarChar, 100).Value = identifier;

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (await reader.ReadAsync())
        {
            return new CookieConsent
            {
                ConsentId = reader.GetInt32(reader.GetOrdinal("ConsentId")),
                UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : reader.GetInt32(reader.GetOrdinal("UserId")),
                ConsentIdentifier = reader.GetString(reader.GetOrdinal("ConsentIdentifier")),
                AcceptedEssential = reader.GetBoolean(reader.GetOrdinal("AcceptedEssential")),
                AcceptedAnalytics = reader.GetBoolean(reader.GetOrdinal("AcceptedAnalytics")),
                AcceptedPreferences = reader.GetBoolean(reader.GetOrdinal("AcceptedPreferences")),
                PolicyVersion = reader.GetString(reader.GetOrdinal("PolicyVersion")),
                ConsentDate = reader.GetDateTime(reader.GetOrdinal("ConsentDate"))
            };
        }

        return null;
    }
}
