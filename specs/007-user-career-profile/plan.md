# Plan — User Career Profile (Story + Goals) on the CLI

## 1. Context

`REQ-006` already lists an *optional career narrative file* as a per-user CLI
option, but only the resume is currently wired up. The evaluation LLM
(`JobEvaluationAgent`) sees the resume in its prompt and nothing else about the
candidate.

This plan adds two new pieces of candidate context — **career story** and
**career goals** — and lets the user point at them either on the command line
or via files in their config directory. The two pieces give the LLM more signal
when scoring and reasoning about a job:

- *Career story* — narrative context: how the candidate got here, what they
  enjoyed, what kind of work they want to do next.
- *Career goals* — explicit short- to mid-term objectives (e.g. "move from
  hands-on coding into platform/architecture leadership").

## 2. Goals

- Add `--CareerStory` and `--CareerGoals` options to `Fidalgo.Agent`.
- Fall back to files in `%APPDATA%\.config\Fidalgo\` when the CLI flag is
  absent.
- Both pieces are **optional** — the agent still runs without them.
- Thread the loaded content into the `JobEvaluationAgent` prompt.
- Cover the new resolution logic with unit tests.
- Keep architecture memory in sync.

## 3. Design

### 3.1 File resolution

A small resolver service in `Fidalgo.Shared` so the same logic is reusable by
`Fidalgo.Ingest` later if we want the search agent to use it.

```
Fidalgo.Shared/UserProfile/UserProfileResolver.cs
Fidalgo.Shared/UserProfile/UserProfilePaths.cs
```

Responsibilities:

- `ResolveCareerStoryPath(string? cliPath)` → returns the CLI path if it
  exists; otherwise `%APPDATA%\.config\Fidalgo\career-story.md` if that
  exists; otherwise `null`.
- `ResolveCareerGoalsPath(string? cliPath)` → same shape, for
  `career-goals.md`.
- Logs a warning when the user passed `--CareerStory` / `--CareerGoals` but
  the file is missing.
- Path base dir is injected via constructor for testability (default =
  `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData),
  ".config", "Fidalgo")`).
- File contents: UTF-8 plain text / Markdown (matches the existing
  `Resume.md`).

### 3.2 CLI options

`Fidalgo.Agent/Configuration/CliOptions.cs` — add two new properties, both
nullable and without `[Required]`:

```csharp
public string? CareerStory { get; set; }   // file path or inline text
public string? CareerGoals { get; set; }   // file path or inline text
```

The `Program.cs` `--help` block gets two new lines and the validation block
no longer treats the resume as the sole required input for the evaluation
path (it already had logic for QueryJobs/DiscardJobId/ListDiscarded; that
stays the same).

### 3.3 Wiring

`Fidalgo.Agent/Program.cs` — after `IOptions<CliOptions>` is resolved:

1. Build `UserProfileResolver` (singleton already DI-registered or a local
   `new` with the default path).
2. Resolve both paths.
3. Read file contents (`File.ReadAllText`) when a path resolves.
4. Pass `(resume, careerStory, careerGoals)` into
   `JobEvaluationAgent`'s constructor.

### 3.4 JobEvaluationAgent

`Fidalgo.Agent/Agents/JobEvaluationAgent.cs`:

- New ctor parameters: `string? careerStory`, `string? careerGoals`.
- Prompt gains two new sections, appended after the Resume block and only
  when present:
  ```
  Career Story:
  {careerStory}

  Career Goals:
  {careerGoals}
  ```
- `TruncateResume` is **kept as is** for the resume (60 000-char cap).
  Career story and career goals are passed through verbatim — no cap, per
  the locked design decisions.

### 3.5 Tests

`Fidalgo.Shared.Tests/UserProfile/UserProfileResolverTests.cs` — NSubstitute
mocks are not needed; inject a temp directory for `baseDir`:

- CLI path provided and file exists → returns CLI path.
- CLI path provided but file missing → logs warning, returns default if
  present, else `null`.
- CLI path omitted, default file exists → returns default path.
- Neither exists → returns `null`.
- Resolution works when `baseDir` doesn't yet exist (caller's job to ensure
  the dir; resolver does not silently create it).

`Fidalgo.Agent.Tests/JobEvaluationAgentTests.cs` (extend if it exists, else
add) — verify the prompt contains career story / goals text when supplied
and omits the sections when they are `null`.

### 3.6 Architecture memory

`wiki/architecture/architecture-summary.md`:

- Add a one-liner to §4.3 *Match Engine*: "Loads the user's resume,
  optional career story, and optional career goals; LLM sees all three in
  the evaluation prompt."
- Add a short subsection in §8 *Security Considerations* or §6 *Key Design
  Decisions* noting the on-disk convention: career story/goals live at
  `%APPDATA%\.config\Fidalgo\*.md`, treated as user-controlled input.

## 4. File-by-file change list

| File | Change |
|---|---|
| `src/Fidalgo.Shared/UserProfile/UserProfilePaths.cs` | New — path constants + `GetDefaultBaseDir()` |
| `src/Fidalgo.Shared/UserProfile/UserProfileResolver.cs` | New — resolution + warning logging |
| `src/Fidalgo.Shared/Fidalgo.Shared.csproj` | No new package deps |
| `src/Fidalgo.Agent/Configuration/CliOptions.cs` | Add `CareerStory`, `CareerGoals` |
| `src/Fidalgo.Agent/Program.cs` | Resolve paths, read files, pass to agent; update `--help` |
| `src/Fidalgo.Agent/Agents/JobEvaluationAgent.cs` | New ctor params + prompt sections |
| `src/Fidalgo.Shared.Tests/UserProfile/UserProfileResolverTests.cs` | New — 4+ cases |
| `src/Fidalgo.Agent.Tests/JobEvaluationAgentTests.cs` | Extend — prompt section visibility |
| `wiki/architecture/architecture-summary.md` | One-line update to §4.3 |

## 5. Build & verify

```powershell
dotnet build src/Fidalgo.slnx
dotnet test  src/Fidalgo.slnx
```

- No new warnings (`TreatWarningsAsErrors` already on per `Directory.Build.props`).
- All existing tests still pass.

## 6. Locked design decisions

| Decision | Choice |
|---|---|
| Config folder | `%APPDATA%\.config\Fidalgo\` (capital F) |
| Scope | `Fidalgo.Agent` only — `Fidalgo.Ingest` is untouched |
| Default file names | `career-story.md`, `career-goals.md` (Markdown) |
| Truncation | No cap on career story or career goals. Resume keeps the existing 60 000-char cap. |

The `no cap` decision is a trust-the-user choice: oversized files are
caller's responsibility. A future change could add an opt-in
`--CareerStoryMaxChars` flag if context pressure becomes a problem.

## 7. Rollout

- Single PR.
- Commit per logical step (resolver + tests; options + Program.cs;
  agent + tests; architecture memory).
- README's CLI usage table gets the two new flags.
