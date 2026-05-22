# Quickstart: Job Browse Web Site

**Feature**: 006-job-browse-web
**Branch**: 006-job-browse-web
**Date**: 2026-05-22

## Prerequisites

- .NET 10.0 SDK or later
- SQLite (bundled with .NET via `Microsoft.Data.Sqlite`)
- A populated jobs database (run `Fidalgo.Agent` to discover jobs first)

## Building the Project

```powershell
# From the repository root
dotnet build src/Fidalgo.slnx
```

## Running the Web Site

```powershell
# Run the Blazor WebAssembly project
dotnet run --project src/Fidalgo.Web/Fidalgo.Web.csproj
```

The site will be available at `https://localhost:5001` (or the port shown in the console output).

### Database

The web site stores jobs in `%APPDATA%\fidalgo\jobs.db` by default. To use a database populated by `Fidalgo.Agent`, copy the agent's database to this location:

```powershell
# On Windows (PowerShell)
Copy-Item "$HOME\.config\fidalgo-jobs.db" "$env:APPDATA\fidalgo\jobs.db" -Force
```

## Using the Web Site

### 1. Select Your Email

When the site loads, the top drop-down shows all email addresses that have jobs in the database. Select your email to see your job list.

### 2. Browse Jobs

The jobs table displays:
- **Score**: Suitability rating (0-100)
- **Employer**: Company name
- **Date**: Job posted date
- **Title**: Job title (click to view details)
- **Recommendation**: Apply / Maybe / Do not apply

Jobs are ordered by score (highest first) by default.

### 3. Filter by Date

Use the date filter control above the jobs table to show only jobs posted on or after a specific date.

### 4. Navigate Pages

Use the pagination controls at the bottom of the table to browse through results (20 jobs per page).

### 5. View Job Details

Click on any job title to open a modal dialog showing all job fields:
- Full description
- Pros and cons
- Resume hints
- Salary range
- Source website
- Posted date

### 6. Delete Jobs

- **From the list**: Click the trashcan icon on any job row to soft-delete it
- **From the modal**: Click the delete button in the job details modal

Deleted jobs are immediately removed from the list and will not appear in future queries.

## Project Structure

```
src/Fidalgo.Web/
├── Components/
│   ├── Layout/MainLayout.razor        # Top navbar + page content area
│   ├── Pages/Jobs.razor               # Main job list page
│   └── Components/JobDetailsModal.razor # Modal dialog for job details
├── Services/
│   ├── JobsService.cs                 # Job CRUD operations
│   └── TenantService.cs               # Email drop-down population
└── wwwroot/index.html                 # SPA entry point
```

## Testing

```powershell
# Run web project tests
dotnet test src/Fidalgo.Web.Tests/Fidalgo.Web.Tests.csproj
```
