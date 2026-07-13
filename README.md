# Fidalgo Job Search AI Agent

## Summary

_This is primarily a research project, not a finished product._

There are two problems this wants to solve
- Going though job listings and matching the listings against the candidate's resume, resulting in a detailed analysis and a single match probability
- Making recommendations to adjust the candidate's resume to highlight how the candidate meets the job requirements. This is more than mere curiosity, since we want this agent to run frequently and have it scan a large amount of jobs. Running AI in a loop against a paid model is not a bright idea.

Furthermore, I wanted to understand how well this problem can be solved by using a local LLM.

## Architecture

There are three ways code can run in an agentic application

- The harness (i.e. scrape the web for jobs, then invoke the AI for each job)
- The model (i.e. 'search for jobs' as a prompt). There the model becomes the code
- Tool calls

This presents some interesting choices, where the same task might be performed in different ways with different tradeoffs. To 'let the model figure it out' can yield initially amazing returns, but the results are rarely quick or consistent.

The chosen approach is for the model to do the initial scraping, then delegates distilling information from individual job postings to a subagent, protecting the context as much as possible. So far this doesn't run very consistently and the model gets sidetracked frequently. See the next paragraph for further discussion.

## Key learnings

### AI broke the internet's business model

The implied contract of today's internet has been that the user gets a service that has a value for free, in exchange for interacting with the vendor's 'experience'. In exchange for getting something we want for free, we volunteer our time and mindshare on content that we don't want, and we volunteer our usage data to be shared for advertising there or elsewhere. _This is not a moral judgement, this is just to acknowledge what the business model looks like_.

This is relevant to the project as we are not interested in any 'experience', we are just interested in the smallest possible snippet of information to determine if we want to apply for a job. LLMs are great at that, but we are now violating the business model. **The hardest problem to solve here is not how to use AI, it's how to get past all the counter measures that services have installed to make sure the information they serve gets in front of a human eye.**

Major job sites have APIs, but they reportedly are picky about whom they allow access. Web search APIs are $5/1000 invocations. There are job listing APIs provided by third parties (who presumably have the scraping problem solved). which likely is the best option for future iterations of this tool.

### Use subagents to protect yur context

That's a little more obvious. The first iteration was sifting through full html pages iteratively, and it got lost quickly. When you run a subagent, that subagent builds it's own context and then returns a targeted result, disarding the context it had to build to get it.

Subagent's aren't magic. The harness already knows how to invoke an LLM, so you just create a 'delegate' tool and that will in turn re-invoke the LLM.

### Doable with larger models

This seems very doable with what's probably the largest size models available for local use, the 27-35 billion parameter models. I haven't spent much time with 9-12 billion parameter models, as they are usually not as good with tool calling, but they should work as well.

## Overview

Fidalgo is an AI-powered job search assistant designed to autonomously find, fetch, and analyze job postings based on your resume. It utilizes a local SQLite database to track processed jobs and delegates detailed resume matching to sub-agents to keep the main agent's context clean and efficient. The system leverages Playwright for robust web scraping, SearxNG for meta-searching, and OpenAI language models for intelligent decision-making.

## Features
- **Autonomous Searching**: Automatically searches Indeed and other job boards for matching positions.
- **Smart Triage**: Uses LLM sub-agents to analyze job descriptions against your resume to determine fit.
- **Database Persistence**: Keeps track of seen jobs in a local SQLite database (`%APPDATA%\fidalgo\jobs.db`) to prevent duplicate analysis.
- **Scoring & Recommendations**: Automatically assigns a match score (0-100) and provides pros, cons, and resume tailoring hints.
- **W3C Distributed Tracing**: Full OpenTelemetry tracing context propagation throughout the agent's tasks.

## Command Line Arguments

To run the ingestion tool, use the `Fidalgo.Ingest` CLI. It searches for jobs matching your criteria and pulls them into the database. You must provide your email, search keywords, and zip code.

```text
Usage: Fidalgo.Ingest [options]

Options:
  --Email <email>              [Required] The user's email address
  --Keywords <keywords>        [Required] Search keywords (e.g. 'software engineer')
  --ZipCode <zip>              [Required] Target zip code
  --QueryJobs true             Query mode: List saved jobs
  --EmployerFilter <name>      Query mode: Filter jobs by employer
  --DateFrom <date>            Query mode: Filter jobs from this date
  --DateTo <date>              Query mode: Filter jobs to this date
  --SourceWebsiteFilter <site> Query mode: Filter by job board source
  --Api                        Mode: Read from Adzuna API without LLM (default)
  --Scrape                     Mode: Use LLM and playwright to scrape jobs
  --help, -h                   Show this help message
```

To run the autonomous agent, use the `Fidalgo.Agent` CLI. It analyzes the jobs that were ingested. You must provide your email and a path to your text-based resume.

```text
Usage: Fidalgo.Agent [options]

Options:
  --Email <email>              [Required] The user's email address
  --Resume <path>              [Required] Path to resume text file
  --QueryJobs true             Query mode: List saved jobs
  --EmployerFilter <name>      Query mode: Filter jobs by employer
  --DateFrom <date>            Query mode: Filter jobs from this date
  --DateTo <date>              Query mode: Filter jobs to this date
  --SourceWebsiteFilter <site> Query mode: Filter by job board source
  --DiscardJobId <guid>        Discard mode: Mark a job as discarded
  --ListDiscarded true         List mode: Show all discarded jobs
  --help, -h                   Show this help message
```

## Getting Started

1. Set up your Adzuna API credentials using `dotnet user-secrets`. Run the following commands from inside `src/Fidalgo.Agent` and `src/Fidalgo.Ingest`:
```bash
dotnet user-secrets init
dotnet user-secrets set Adzuna:AppId <your-app-id>
dotnet user-secrets set Adzuna:AppKey <your-app-key>
```

2. Ensure you have a text or markdown version of your resume available (e.g., `Resume.md`).
3. Run the ingest tool to search for and download job postings:
```bash
dotnet run --project src/Fidalgo.Ingest --Email "you@example.com" --Keywords "software architect" --ZipCode "98033"
```

4. Run the agent to analyze the saved jobs against your resume:
```bash
dotnet run --project src/Fidalgo.Agent --Email "you@example.com" --Resume "Resume.md"
```

5. To view jobs the agent has saved or processed:
```bash
dotnet run --project src/Fidalgo.Agent --Email "you@example.com" --QueryJobs true
```

## AI Agent Tools

The LLM is equipped with the following tools to perform its job search and analysis autonomously:

- `playwright_fetch`: Fetches a web page using a real headless browser (via Playwright) to execute JavaScript and overcome simple anti-bot measures, returning sanitized HTML content.
- `markdown_fetch`: Fetches a web page quickly without JavaScript rendering and converts it to clean, readable markdown using a url-to-markdown service.
- `web_search`: Searches the web for job boards, companies, or relevant queries using the SearxNG meta-search engine.
- `job_in_db`: Checks the local SQLite database to see if a job ID from a specific source has already been processed and scanned.
- `save_job`: Saves a fully analyzed job to the local SQLite database along with its match score, pros, cons, and recommendations.
- `get_jobs`: Queries the local database for saved jobs matching specific filters (like date, employer, or source website).
- `delegate`: Spawns an isolated sub-agent to perform complex processing (like comparing a job description against the user's resume) in order to keep the main agent's context clean and focused.

### Notes

- playwright_fetch is a local implementation that attempts to deal with cloudflare and logins
- markdown_fetch relies on a containerized instance of https://github.com/nanodeck/url-to-markdown
- web_search relies on a containerized instance of SearxNG (which itself does not know how to overcome bot protection)
