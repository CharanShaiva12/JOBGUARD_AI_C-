# JobGuard - AI-Based Fake Job Detection Web Application

JobGuard is an AI-assisted job advertisement fraud and scam detection platform built with **ASP.NET Core MVC (.NET 10.0)**, **Microsoft SQL Server (ADO.NET)**, **ML.NET** (TF-IDF Text Featurization + Logistic Regression), and **Tesseract OCR / PDF Pig**.

It empowers job seekers to evaluate employment postings, detects deceptive recruitment language, identifies dangerous URLs, and computes clear risk scores from 0 to 100 based on statistical models trained on genuine recruitment fraud data.

---

## 1. System Architecture & Features

- **Direct Job Analysis on Homepage**: Visitors immediately see the job analysis interface upon opening the site. Unauthenticated visitors can compose or test descriptions, and are safely redirected to sign in/register before executing and persisting the analysis.
- **Machine Learning Pipeline (ML.NET)**:
  - Algorithm: N-Gram TF-IDF Text Featurization + Binary Logistic Regression (`LbfgsLogisticRegression`).
  - Dataset: Benchmark EMSCAD / Balanced Real & Fake Job Postings dataset (`1,732` verified records: 866 legitimate, 866 fraudulent).
  - Test Performance on Held-Out 20% Evaluation Partition: **86.01% Accuracy**, **95.20% ROC AUC**, **86.13% F1-score**.
- **Pure ADO.NET Persistence**: Direct SQL queries using `SqlConnection`, `SqlCommand`, `SqlDataReader`, and parameterized queries. No Entity Framework Core overhead.
- **Document OCR & PDF Parser**: Direct vector text extraction from PDF files via `UglyToad.PdfPig`, supplemented with native Tesseract OCR for scanned ad screenshots and images (`.png`, `.jpg`).
- **URL & SSRF Protection**: Heuristic inspection of link schemes (HTTPS vs HTTP), IP address host detection, deep subdomain spoofing indicators, and loopback/private IP blocking.
- **Security & Authentication**:
  - Secure ASP.NET Core cookie authentication (`JobGuard.AuthToken`) with `HttpOnly`, `SameSite=Lax`, and sliding expiration.
  - PBKDF2-SHA256 password hashing with 100,000 iterations and distinct cryptographic salts (`v1:100000:{salt}:{hash}`).
  - Role-based authorization (`[Authorize(Roles = "Admin")]`).
  - Anti-forgery CSRF tokens on all state-altering requests.
- **Cookie Consent**: Interactive banner and persistent preferences modal respecting essential security cookies and optional tracking preferences.
- **Dedicated Dashboards**:
  - **User Dashboard**: Real-time personal analysis history, prediction breakdowns, risk categories, and links to detailed reports.
  - **Admin Dashboard**: Aggregated platform metrics, legitimate vs. fraudulent counts, system-wide average risk score, and user accounts.
- **Design System**: Tailored security platform UI built in `jobguard.css` (dark cybersecurity theme: slate, navy, cyan accents). Strictly free from purple gradients, fake testimonials, or placeholder statistics.

---

## 2. Prerequisites & Development Environment

- **Operating System**: Windows 10/11
- **.NET SDK**: .NET 10.0 (or installed .NET Core SDK supporting net10.0)
- **Database**: Microsoft SQL Server (LocalDB `(localdb)\MSSQLLocalDB`, SQL Server Express, or Full SQL Server)
- **Tooling**: Windows PowerShell or Command Prompt

---

## 3. Quick Start & Setup Instructions

### Step 1: Clone and Navigate to Directory
```powershell
cd c:\Users\chara\Documents\antigravity\lively-hopper
```

### Step 2: Configure SQL Server Connection
The default configuration in `JobGuard/appsettings.json` connects to LocalDB:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=JobGuardDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```
*If using a named SQL Server Express instance, update the connection string to:*
`Server=localhost\\SQLEXPRESS;Database=JobGuardDb;Trusted_Connection=True;TrustServerCertificate=True`

### Step 3: Initialize Database Schema
The application automatically creates required tables on startup if the database exists. Alternatively, run the SQL script manually using `sqlcmd`:
```powershell
# Create database if not present
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'JobGuardDb') CREATE DATABASE JobGuardDb;"

# Execute schema script
sqlcmd -S "(localdb)\MSSQLLocalDB" -d JobGuardDb -i "JobGuard\Data\Sql\CreateTables.sql"
```

### Step 4: Machine Learning Model Training (Optional - Pre-trained model included)
A pre-trained model artifact is already serialized in `JobGuard/ML/Models/FakeJobModel.zip`. To re-train the model anytime:
```powershell
dotnet run --project JobGuard.ML.Trainer/JobGuard.ML.Trainer.csproj
```

### Step 5: Run Automated Tests
Execute the 14-test unit and integration test suite:
```powershell
dotnet test
```

### Step 6: Launch the Web Application
```powershell
dotnet run --project JobGuard/JobGuard.csproj
```
Open your web browser and navigate to:
```
https://localhost:5001  or  http://localhost:5000
```
*(The exact port assigned by launch settings will appear in the console output).*

---

## 4. Provisioning an Administrator Account

To promote any registered user to an administrator:
1. Register a new user via `/Account/Register` (e.g. `admin@jobguard.local`).
2. Run this SQL command in SQL Server:
```sql
USE JobGuardDb;
UPDATE Users SET Role = 'Admin' WHERE Email = 'admin@jobguard.local';
```
3. Sign in to access the **Admin Console** at `/Admin`.

---

## 5. Production Readiness Checklist

Before publishing JobGuard to a public server:
- [ ] Configure a public production domain with TLS/HTTPS certificate.
- [ ] Point `DefaultConnection` to a secured, authenticated SQL Server instance.
- [ ] Store database credentials in Azure Key Vault or Environment Variables.
- [ ] Set `CookieSecurePolicy.Always` in `Program.cs`.
- [ ] Disable Developer Exception Page and enable production logging.
- [ ] Ensure firewall restricts database port `1433` to the web application host.
