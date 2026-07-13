# Fidalgo JobSearch AI — Architecture Summary

## 1. System Overview

Fidalgo JobSearch AI is a long-running, containerized C# application that periodically scrapes job boards, uses a local OpenAI-compatible LLM to analyze job postings against user profiles, and delivers ranked job recommendations via email with customized resume downloads. A lightweight web interface lets users browse, filter, and manage their job pipeline.

## 2. Technology Choices

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS (latest) | REQ-003, long-lived AOT-friendly runtime |
| Agent Framework | Microsoft Agent Framework (AGENTS) | REQ-002 |
| Database | SQLite (EF Core) | REQ-007, zero-dependency, single-file |
| LLM | Local OpenAI-compatible at `http://192.168.1.21:8080/v1` | REQ-100 |
| Web scraping | Playwright (headless Chromium) | Handles JS-rendered job boards; resilient to anti-bot measures |
| PDF generation | QuestPDF | Modern, fluent API, no external dependencies |
| Web UI | Minimal API + Razor Pages (built into .NET) | Single binary, no separate server needed |
| Scheduling | `System.Threading.Timer` + Cron-style background service | Hosted service pattern |
| Email | `MailKit` (SMTP) | RFC-compliant, TLS support |
| Container | Docker (multi-stage, ASP.NET runtime image) | REQ-001 |

## 3. Architectural Layers

```
┌─────────────────────────────────────────────────┐
│                  Presentation                    │
│  ┌──────────────┐    ┌───────────────────────┐  │
│  │   Web UI     │    │   Email Dispatcher    │  │
│  │  (Razor)     │    │   (SMTP / QuestPDF)   │  │
│  └──────────────┘    └───────────────────────┘  │
├─────────────────────────────────────────────────┤
│                    Application                   │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────┐ │
│  │ Job Scrape   │ │ Skill Extract│ │ Match   │ │
│  │ Orchestrator │ │ (LLM agent)  │ │ Engine  │ │
│  └──────────────┘ └──────────────┘ └─────────┘ │
│  ┌──────────────┐ ┌──────────────┐             │
│  │ Resume       │ │ Email        │             │
│  │ Customizer   │ │ Service      │             │
│  └──────────────┘ └──────────────┘             │
├─────────────────────────────────────────────────┤
│                     Domain                       │
│  User │ Job │ JobSkill │ UserPreference │ Log  │
├─────────────────────────────────────────────────┤
│                    Infrastructure                │
│  ┌──────────┐  ┌───────────┐  ┌────────────┐  │
│  │  SQLite  │  │  Playwright│  │  MailKit   │  │
│  │  (EF)    │  │  Browser  │  │  SMTP      │  │
│  └──────────┘  └───────────┘  └────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │    OpenAI Client (HTTP → local LLM)     │  │
│  └──────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

## 4. Module Responsibilities

### 4.1 Scrape Agent (`JobScrapeAgent`)
- Runs every 4 hours (REQ-004).
- Launches a headless Playwright session per job-board source.
- Searches for `'software engineer'` and `'software engineering manager'`.
- Extracts job listing URLs; reads up to 3 detail pages per invocation.
- Delegates skill extraction and suitability scoring to the LLM agent.

### 4.2 Skill Extraction Agent (`SkillExtractAgent`)
- Receives a job description (plain text).
- Uses the local OpenAI-compatible LLM to produce:
  - Top 10 technical skills (REQ-008)
  - Top 10 interpersonal skills (REQ-009)
- Returns structured JSON consumed by the match engine.

### 4.3 Match Engine (`MatchEngine`)
- Loads user's resume + optional career narrative.
- Loads extracted employer skills for a given job.
- Computes overlap percentage → suitability rating (REQ-010, REQ-011).
- Persists the full job record via EF Core (REQ-011).

### 4.4 Email Service (`EmailService`)
- Triggered daily at 05:00 by a hosted cron timer (REQ-012).
- Also triggered on-demand via a web API endpoint.
- For each user: queries non-discarded jobs, orders by suitability descending.
- Generates a personalized PDF resume per job (REQ-013).
- Sends an HTML email with ranked job list and clickable resume download links.

### 4.5 Resume Customizer (`ResumeCustomizer`)
- Reads the user's base resume (PDF/DOCX).
- Injects job-specific keywords and highlights derived from the skill overlap analysis.
- Outputs a tailored PDF via QuestPDF.

### 4.6 Web UI (`FidalgoWeb`)
- Razor Pages application hosted in the same process (REQ-001).
- Exposes endpoints for browsing, filtering, and marking jobs (WEB-001–WEB-006).
- Filters by date range and suitability rating (WEB-002, WEB-003).
- Allows marking applied (with date) and discarded (WEB-004, WEB-005).
- Hides discarded jobs from all views by default (WEB-006).

## 5. Data Flow

```
[Job Boards] ──scrape──▶ [Playwright] ──URLs──▶ [SkillExtractAgent]
                                                        │
                                                  [LLM Local]
                                                        │
                                           tech + interpersonal skills
                                                        │
                                              [MatchEngine]
                                                        │
                                          (resume + narrative)
                                                        │
                                              suitability %
                                                        │
                                              [SQLite / EF Core]
                                                        │
                              ┌─────────────────────────┼──────────────────┐
                              ▼                         ▼                  ▼
                        [Web UI browse]     [EmailService 05:00]   [ResumeCustomizer]
                              │                         │                  │
                              ▼                         ▼                  ▼
                        [Filtered jobs]     [Rank + PDF + send]   [Tailored PDF]
```

## 6. Key Design Decisions

### 6.1 SQLite over a managed RDBMS
- **Trade-off**: No concurrent writers; single-file simplicity.
- **Justification**: Single-instance deployment; no external database server needed.
- **Reversibility**: Medium — EF Core abstraction makes migration to PostgreSQL feasible.

### 6.2 Playwright over HTTP-only scrapers
- **Trade-off**: Heavier resource usage (Chromium binary ~300 MB).
- **Justification**: Job boards render content client-side and employ anti-bot measures.
- **Reversibility**: Low — Playwright handles JS rendering; switching to HTTP clients would break on dynamic pages.

### 6.3 Local LLM over cloud API
- **Trade-off**: Requires a local GPU/CPU server at `192.168.1.21:8080`.
- **Justification**: No API costs, full data locality, no PII leaves the network.
- **Reversibility**: Medium — the OpenAI client abstraction allows fallback to a cloud provider.

### 6.4 Single-process hosting (Web UI + background agents)
- **Trade-off**: Web requests compete with scraping for CPU/memory.
- **Justification**: Simplifies deployment (one container image, REQ-001).
- **Reversibility**: Medium — can split into separate services later.

## 7. Scheduling Summary

| Schedule | Action |
|---|---|
| Every 4 hours | Scrape job boards (REQ-004) |
| Every day at 05:00 | Send email reports (REQ-012) |
| On-demand (API call) | Send email report (REQ-012) |

## 8. Security Considerations

- User resume files and career narratives are stored on the container filesystem, not in the database.
- SMTP credentials are injected via environment variables or Docker secrets (no hardcoded passwords).
- The web UI has no authentication layer by requirement; in production, consider adding a simple API key or cookie-based auth.
- The local LLM endpoint is on a private network (`192.168.1.21`), not exposed externally.

## 9. Reversibility Assessment

| Decision | Reversibility | Notes |
|---|---|---|
| SQLite | Medium | EF Core data access layer isolates changes |
| Playwright | Low | Core scraping logic depends on browser automation |
| Local LLM endpoint | Medium | OpenAI-compatible client abstraction |
| Single-process hosting | Medium | Can split Web UI and background service |
| .NET 10 LTS | Low | Tied to the container runtime image |
