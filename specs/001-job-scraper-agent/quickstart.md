# Quickstart: Job Scraper Agent

## Prerequisites

- .NET 10.0 SDK or later
- SQLite (bundled with Microsoft.Data.Sqlite, no separate install needed)
- Internet access to job search websites

## Build

```powershell
dotnet restore src/Fidalgo.Agent/Fidalgo.Agent.csproj
dotnet build src/Fidalgo.Agent/Fidalgo.Agent.csproj
```

## Configuration

Create a JSON configuration file (e.g., `appsettings.json`) with your search settings:

```json
{
  "SearchConfig": {
    "UserEmail": "user@example.com",
    "Websites": [
      "governmentjobs.com",
      "google",
      "glassdoor.com",
      "monster.com",
      "indeed.com",
      "linkedin.com"
    ],
    "Keywords": [
      "software engineer",
      "software engineering manager"
    ]
  }
}
```

## Running the Agent

```powershell
dotnet run --project src/Fidalgo.Agent/Fidalgo.Agent.csproj -- --config appsettings.json
```

The agent will:
1. Load the configuration from the specified JSON file
2. Connect to the local SQLite database (created automatically at `jobs.db`)
3. Search each configured website for each keyword
4. Extract job postings and store them in the database
5. Log all activity to the console

## Expected Output

```
[INFO] Loading configuration from appsettings.json
[INFO] Configuration loaded: 6 websites, 2 keywords
[INFO] Database initialized at jobs.db
[INFO] Starting search cycle...
[INFO] Searching governmentjobs.com for "software engineer"
[INFO] Found 12 jobs, stored 8 new, skipped 4 duplicates
[INFO] Searching governmentjobs.com for "software engineering manager"
[INFO] Found 5 jobs, stored 5 new, skipped 0 duplicates
[INFO] Searching google for "software engineer"
[INFO] Found 20 jobs, stored 15 new, skipped 5 duplicates
...
[INFO] Search cycle complete. Total: 45 new jobs, 12 duplicates skipped.
```

## Database Location

The SQLite database file `jobs.db` is created in the working directory. To change the location, set the `DatabasePath` setting in the configuration file.

## Running as a Scheduled Task

The agent is designed to run periodically (every 4 hours per project requirements). On Windows, schedule it using Task Scheduler:

```powershell
# Create a scheduled task that runs every 4 hours
$action = New-ScheduledTaskAction -Execute "dotnet" -Argument "run --project src/Fidalgo.Agent/Fidalgo.Agent.csproj -- --config appsettings.json"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 4)
Register-ScheduledTask -TaskName "FidalgoJobAgent" -Action $action -Trigger $trigger
```

## Running in a Docker Container

```powershell
docker build -t fidalgo-agent .
docker run -v $(pwd)/appsettings.json:/app/appsettings.json -v $(pwd)/jobs.db:/app/jobs.db fidalgo-agent
```

## Testing

```powershell
dotnet test tests/Fidalgo.Agent.Tests/Fidalgo.Agent.Tests.csproj
```
