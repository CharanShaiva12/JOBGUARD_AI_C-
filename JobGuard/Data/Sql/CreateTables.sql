-- =========================================================================
-- JobGuard Database Schema
-- Microsoft SQL Server / LocalDB Schema
-- =========================================================================

-- Ensure database exists (optional if run against specific database)
-- CREATE DATABASE JobGuardDb;
-- USE JobGuardDb;

-- 1. Users Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(256) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        Role NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'User',
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
    CREATE INDEX IX_Users_Email ON Users(Email);
END
GO

-- 2. JobAnalyses Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobAnalyses')
BEGIN
    CREATE TABLE JobAnalyses (
        AnalysisId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CompanyName NVARCHAR(200) NULL,
        JobUrl NVARCHAR(1000) NULL,
        JobDescription NVARCHAR(MAX) NOT NULL,
        ExtractedText NVARCHAR(MAX) NULL,
        Prediction NVARCHAR(50) NOT NULL, -- 'Legitimate' or 'Fraudulent'
        RiskScore FLOAT NOT NULL,          -- 0.0 to 100.0
        RiskCategory NVARCHAR(50) NOT NULL, -- 'Lower Risk', 'Moderate Risk', 'High Risk', 'Very High Risk'
        Confidence FLOAT NOT NULL,        -- Estimated probability
        SuspiciousFlags NVARCHAR(MAX) NULL, -- JSON or delimited list of suspicious markers
        ModelVersion NVARCHAR(50) NOT NULL,
        AnalysisSummary NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_JobAnalyses_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobAnalyses_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
    );
    CREATE INDEX IX_JobAnalyses_UserId ON JobAnalyses(UserId);
    CREATE INDEX IX_JobAnalyses_CreatedAt ON JobAnalyses(CreatedAt DESC);
END
GO

-- 3. Feedback Table (Optional feedback loop)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Feedback')
BEGIN
    CREATE TABLE Feedback (
        FeedbackId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        AnalysisId INT NOT NULL,
        FeedbackType NVARCHAR(50) NOT NULL, -- 'Accurate', 'FalsePositive', 'FalseNegative', 'Other'
        FeedbackText NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Feedback_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Feedback_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
        CONSTRAINT FK_Feedback_JobAnalyses FOREIGN KEY (AnalysisId) REFERENCES JobAnalyses(AnalysisId) ON DELETE CASCADE
    );
    CREATE INDEX IX_Feedback_AnalysisId ON Feedback(AnalysisId);
END
GO

-- 4. CookieConsent Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CookieConsent')
BEGIN
    CREATE TABLE CookieConsent (
        ConsentId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NULL, -- NULL for anonymous visitor
        ConsentIdentifier NVARCHAR(100) NOT NULL, -- Cookie ID token
        AcceptedEssential BIT NOT NULL CONSTRAINT DF_CookieConsent_Essential DEFAULT 1,
        AcceptedAnalytics BIT NOT NULL CONSTRAINT DF_CookieConsent_Analytics DEFAULT 0,
        AcceptedPreferences BIT NOT NULL CONSTRAINT DF_CookieConsent_Prefs DEFAULT 0,
        PolicyVersion NVARCHAR(20) NOT NULL,
        ConsentDate DATETIME2 NOT NULL CONSTRAINT DF_CookieConsent_Date DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CookieConsent_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE SET NULL
    );
    CREATE INDEX IX_CookieConsent_Identifier ON CookieConsent(ConsentIdentifier);
END
GO
