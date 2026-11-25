# Fidalgo Job Search AI Agent

## Summary

_This is primarily a research project, not a finished product._

There are two problems this wants to solve:
- Going through job listings and matching the listings against the candidate's resume, career story, and career goals, resulting in a detailed analysis and match score.
- Generating job-targeted application packets (`resume.pdf`, `cover-letter.pdf`, and a snapshot `job-posting.md`) tailored to each opportunity without inventing unverified experience, both in unattended CLI batch runs and interactively in the web UI.

Furthermore, I wanted to understand how well this problem can be solved by using a local LLM.

## Architecture

There are three ways code can run in an agentic application:

- The harness (i.e. scrape the web for jobs, then invoke the AI for each job)
- The model (i.e. 'search for jobs' as a prompt). There the model becomes the code
- Tool calls

This presents some interesting choices, where the same task might be performed in different ways with different tradeoffs. To 'let the model figure it out' can yield initially amazing returns, but the results are rarely quick or consistent.

The chosen approach is for the model to do the initial scraping, then delegate distilling information from individual job postings to a subagent, protecting the context as much as possible.

## Key learnings

### AI broke the internet's business model

The implied contract of today's internet has been that the user gets a service that has a value for free, in exchange for interacting with the vendor's 'experience'. In exchange for getting something we want for free, we volunteer our time and mindshare on content that we don't want, and we volunteer our usage data to be shared for advertising there or elsewhere. _This is not a moral judgement, this is just to acknowledge what the business model looks like_.

This is relevant to the project as we are not interested in any 'experience', we are just interested in the smallest possible snippet of information to determine if we want to apply for a job. LLMs are great at that, but we are now violating the business model. **The hardest problem to solve here is not how to use AI, it's how to get past all the counter measures that services have installed to make sure the information they serve gets in front of a human eye.**

Major job sites have APIs, but they reportedly are picky about whom they allow access. Web search APIs are $5/1000 invocations. There are job listing APIs provided by third parties (who presumably have the scraping problem solved), which likely is the best option for future iterations of this tool.

### Use subagents to protect your context

That's a little more obvious. The first iteration was sifting through full html pages iteratively, and it got lost quickly. When you run a subagent, that subagent builds its own context and then returns a targeted result, discarding the context it had to build to get it.

Subagents aren't magic. The harness already knows how to invoke an LLM, so you just create a 'delegate' tool and that will in turn re-invoke the LLM.

### Doable with larger models

This seems very doable with what's probably the largest size models available for local use, the 27-35 billion parameter models. I haven't spent much time with 9-12 billion parameter models, as they are usually not as good with tool calling, but they should work as well.

## Overview

Fidalgo is an AI-powered job search and application-packet assistant. It discovers job postings (`Fidalgo.Ingest`), evaluates them against your resume, career story, and career goals (`Fidalgo.Agent`), generates job-targeted resume and cover letter PDFs (`Fidalgo.Shared` + `spawnia/md-to-pdf`), and provides a React + ASP.NET Core web interface (`Fidalgo.Host` + `Fidalgo.Frontend`) for reviewing jobs and previewing/downloading packets.

## Features
- **Autonomous Searching**: Searches Adzuna (API mode) or Indeed/job boards via Playwright (`Fidalgo.Ingest`).
- **Smart Triage**: Uses a local OpenAI-compatible LLM (`Fidalgo.Agent`) to score fit (0–100), summarize pros/cons, and produce resume tailoring hints.
- **Application Packet Generation**: Generates `resume.pdf`, `cover-letter.pdf`, and `job-posting.md` inside `Documents\Fidalgo\applications\<employer>_<title>_<short-id>\` via `spawnia/md-to-pdf` (`weasyprint`).
- **Web UI & REST API**: Browse, search, sort, and discard jobs, inspect server profile defaults, generate packets, and preview/download both PDFs inline (`Fidalgo.Host`).
- **Database & Log Persistence**: Stores jobs in `%APPDATA%\fidalgo\jobs.db` and writes 7-day rolling structured JSON logs to `%APPDATA%\fidalgo\logs\log-.json`.
- **W3C Distributed Tracing**: Propagates trace context across HTTP dependencies (`TracingHttpMessageHandler`).

## Default Paths

Fidalgo separates configuration, application state, and user-facing documents:

| Purpose | Default Location | Notes |
|---|---|---|
| User configuration | `%APPDATA%\.config\Fidalgo\config.json` | Optional defaults for `Email`, `ZipCode`, `JobSearchKeywords`, and `ApplicationPacket` |
| Profile documents | `Documents\Fidalgo\` (`resume.md`, `career-story.md`, `career-goals.md`) | Falls back to `%APPDATA%\.config\Fidalgo\` if not yet migrated |
| Generated packets | `Documents\Fidalgo\applications\<employer>_<title>_<short-id>\` | Contains `resume.pdf`, `cover-letter.pdf`, `job-posting.md` (+ `.md` sources & `packet.json`) |
| SQLite DB & logs | `%APPDATA%\fidalgo\jobs.db`, `%APPDATA%\fidalgo\logs\log-.json` | 7-day rolling JSON logs |

## Command Line Arguments

Both `Fidalgo.Ingest` and `Fidalgo.Agent` can be run with **zero CLI flags** once `%APPDATA%\.config\Fidalgo\config.json` and `Documents\Fidalgo\resume.md` are configured. CLI flags always override `config.json`.

### `Fidalgo.Ingest`

```text
Usage: Fidalgo.Ingest [options]

Configuration Defaults:
  When omitted from CLI arguments, Email, JobSearchKeywords (-> Keywords), and ZipCode
  are loaded automatically from %APPDATA%\.config\Fidalgo\config.json.

Options:
  --Email <email>              [Required if not in config.json] Tenant email address
  --Keywords <keywords>        [Required if not in config.json] Search keywords (e.g. 'software engineer')
  --ZipCode <zip>              [Required if not in config.json] Target zip code
  --QueryJobs                  Query mode: List saved jobs
  --EmployerFilter <name>      Query mode: Filter jobs by employer
  --DateFrom <date>            Query mode: Filter jobs from this date
  --DateTo <date>              Query mode: Filter jobs to this date
  --SourceWebsiteFilter <site> Query mode: Filter by job board source
  --Api                        Mode: Read from Adzuna API without LLM (default)
  --Scrape                     Mode: Use LLM and Playwright to scrape jobs
  --help, -h                   Show this help message
```

### `Fidalgo.Agent`

```text
Usage: Fidalgo.Agent [options]

Configuration & Profile Defaults:
  Config file:      %APPDATA%\.config\Fidalgo\config.json (Email, ApplicationPacket, LLM)
  Profile files:    Documents\Fidalgo\resume.md, career-story.md, career-goals.md
                    (falls back to %APPDATA%\.config\Fidalgo\ if not yet migrated)
  Packet output:    Documents\Fidalgo\applications\<employer>_<title>_<short-id>\

Options:
  --Email <email>                 [Required if not in config.json] Tenant email address
  --Resume <path>                 Optional path to base resume (default: Documents\Fidalgo\resume.md)
  --CareerStory <path>            Optional path to career story (default: Documents\Fidalgo\career-story.md)
  --CareerGoals <path>            Optional path to career goals (default: Documents\Fidalgo\career-goals.md)
  --GeneratePackets               Generate application packets for matching evaluated jobs
  --TargetJobId <guid>            Generate application packet for a single job ID
  --ForceRegenerate               Overwrite existing complete packets when generating
  --SkipExisting <bool>           Skip complete existing packets (default: true)
  --OutputDir <path>              Override output base directory for generated packets
  --RecommendationFilter <tiers>  Comma-separated recommendation tiers (default: Apply)
  --QueryJobs                     Query mode: List saved jobs
  --EmployerFilter <name>         Query mode: Filter jobs by employer
  --DateFrom <date>               Query mode: Filter jobs from this date
  --DateTo <date>                 Query mode: Filter jobs to this date
  --SourceWebsiteFilter <site>    Query mode: Filter by job board source
  --DiscardJobId <guid>           Discard mode: Mark a job as discarded
  --ListDiscarded                 List mode: Show all discarded jobs
  --help, -h                      Show this help message
```

## Getting Started

1. Set up your Adzuna API credentials using `dotnet user-secrets` from inside `src/Fidalgo.Agent` and `src/Fidalgo.Ingest`:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set Adzuna:AppId <your-app-id>
   dotnet user-secrets set Adzuna:AppKey <your-app-key>
   ```

2. Start the supporting Docker services via Docker Compose (starts `spawnia/md-to-pdf` on port `8000` by default; pass `--profile scrape` to also start `url-to-markdown` on `3333` and `searxng` on `8090` for `Fidalgo.Ingest --Scrape`):
   ```bash
   # PDF rendering service (required for resume.pdf and cover-letter.pdf generation)
   docker compose up -d

   # Optional: also start url-to-markdown + searxng for LLM web-scraping mode
   docker compose --profile scrape up -d
   ```

3. Create `%APPDATA%\.config\Fidalgo\config.json` so `Fidalgo.Ingest`, `Fidalgo.Agent`, and `Fidalgo.Host` can run cleanly without repeated CLI flags:
   ```json
   {
     "Email": "you@example.com",
     "ZipCode": "98033",
     "JobSearchKeywords": ["software architect", "principal engineer"],
     "ApplicationPacket": {
       "OutputDirectory": null,
       "AutoGenerateForApply": false,
       "RecommendationFilter": "Apply",
       "ResumePageSize": "Letter",
       "LlmTimeoutSeconds": 120,
       "MdToPdfEndpoint": "http://localhost:8000",
       "MdToPdfEngine": "weasyprint",
       "PdfTimeoutSeconds": 60
     }
   }
   ```

4. Place your `resume.md` (and optional `career-story.md` and `career-goals.md`) in `Documents\Fidalgo\`, or pass `--Resume`, `--CareerStory`, and `--CareerGoals` explicitly on the `Fidalgo.Agent` command line.

5. Run job ingestion, evaluation, packet generation, and the web UI:
   ```bash
   # Ingest jobs (uses config.json defaults, or pass --Email / --Keywords / --ZipCode)
   dotnet run --project src/Fidalgo.Ingest

   # Evaluate untriaged jobs (uses config.json and Documents\Fidalgo\ defaults)
   dotnet run --project src/Fidalgo.Agent

   # Generate application packets for all "Apply" jobs (idempotent; add --ForceRegenerate to overwrite)
   dotnet run --project src/Fidalgo.Agent -- --GeneratePackets

   # Generate a packet for a single job
   dotnet run --project src/Fidalgo.Agent -- --TargetJobId <job-guid>

   # Start the Web UI & API (loads defaults from config.json and Documents\Fidalgo\)
   dotnet run --project src/Fidalgo.Host
   ```

## AI Agent Tools

The LLM is equipped with the following tools when scraping/evaluating:

- `playwright_fetch`: Fetches a web page using a real headless browser (via Playwright) to execute JavaScript and overcome simple anti-bot measures, returning sanitized HTML content.
- `markdown_fetch`: Fetches a web page quickly without JavaScript rendering and converts it to clean, readable markdown using a url-to-markdown service.
- `web_search`: Searches the web for job boards, companies, or relevant queries using the SearxNG meta-search engine.
- `job_in_db`: Checks the local SQLite database to see if a job ID from a specific source has already been processed and scanned.
- `save_job`: Saves a fully analyzed job to the local SQLite database along with its match score, pros, cons, and recommendations.
- `get_jobs`: Queries the local database for saved jobs matching specific filters (like date, employer, or source website).
- `delegate`: Spawns an isolated sub-agent to perform complex processing (like comparing a job description against the user's resume) in order to keep the main agent's context clean and focused.

### Notes

- `playwright_fetch` is a local implementation that attempts to deal with Cloudflare and logins.
- `markdown_fetch` relies on a containerized instance of https://github.com/nanodeck/url-to-markdown.
- `web_search` relies on a containerized instance of SearxNG (which itself does not know how to overcome bot protection).
- PDF packet rendering relies on a containerized instance of `spawnia/md-to-pdf` (`http://localhost:8000`, `weasyprint` engine).
