export interface ProfileDefaultsDto {
  defaultEmail: string | null;
  hasResume: boolean;
  hasCareerStory: boolean;
  careerStoryLength: number;
  hasCareerGoals: boolean;
  careerGoalsLength: number;
  baseResumeMarkdown: string | null;
  careerStoryMarkdown: string | null;
  careerGoalsMarkdown: string | null;
  outputDirectory: string;
}

export interface PacketFilesDto {
  resumePdfUrl: string;
  coverLetterPdfUrl: string;
  jobPostingUrl: string;
}

export interface ApplicationPacketStatusDto {
  exists: boolean;
  folderName: string;
  directoryPath: string;
  generatedAtUtc: string | null;
  files: PacketFilesDto;
  resumeMarkdown: string | null;
  coverLetterMarkdown: string | null;
}

export interface GeneratePacketRequest {
  baseResumeOverride?: string | null;
  careerStoryOverride?: string | null;
  careerGoalsOverride?: string | null;
  force?: boolean;
}

export interface ApplicationPacketResultDto {
  outcome: string;
  jobId: string;
  folderName: string;
  directoryPath: string;
  generatedAtUtc: string;
  files: PacketFilesDto;
  resumeMarkdown: string;
  coverLetterMarkdown: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export type PacketFileName =
  | 'resume.pdf'
  | 'cover-letter.pdf'
  | 'job-posting.md'
  | 'resume.md'
  | 'cover-letter.md';

/**
 * Error raised when a Fidalgo API call returns a non-2xx response,
 * preserving parsed ASP.NET Core ProblemDetails when present.
 */
export class ApiProblemError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(message: string, status: number, problem: ProblemDetails | null = null) {
    super(message);
    this.name = 'ApiProblemError';
    this.status = status;
    this.problem = problem;
  }
}

async function parseErrorResponse(response: Response): Promise<ApiProblemError> {
  const status = response.status;
  let problem: ProblemDetails | null = null;

  try {
    const contentType = response.headers?.get?.('content-type') ?? '';
    if (contentType.includes('json') || typeof response.json === 'function') {
      const parsed = (await response.json()) as ProblemDetails;
      if (parsed && typeof parsed === 'object') {
        problem = parsed;
      }
    }
  } catch {
    problem = null;
  }

  const message =
    problem?.detail ||
    problem?.title ||
    `Request failed with status ${status}`;

  return new ApiProblemError(message, status, problem);
}

/**
 * Fetches server-side user profile defaults and readiness flags.
 */
export async function getProfileDefaults(signal?: AbortSignal): Promise<ProfileDefaultsDto> {
  const response = await fetch('/api/profile/defaults', { signal });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }
  return (await response.json()) as ProfileDefaultsDto;
}

/**
 * Fetches the current on-disk application packet status for a job.
 */
export async function getJobPacketStatus(
  jobId: string,
  signal?: AbortSignal,
): Promise<ApplicationPacketStatusDto> {
  const response = await fetch(`/api/jobs/${encodeURIComponent(jobId)}/packet`, { signal });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }
  return (await response.json()) as ApplicationPacketStatusDto;
}

/**
 * Generates (or returns existing) tailored resume & cover letter packet for a job.
 */
export async function generateJobPacket(
  jobId: string,
  request: GeneratePacketRequest,
  signal?: AbortSignal,
): Promise<ApplicationPacketResultDto> {
  const response = await fetch(`/api/jobs/${encodeURIComponent(jobId)}/packet`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }
  return (await response.json()) as ApplicationPacketResultDto;
}

/**
 * Fetches the raw text contents of a Markdown file inside a job's packet directory.
 */
export async function fetchPacketTextFile(
  fileUrl: string,
  signal?: AbortSignal,
): Promise<string> {
  const response = await fetch(fileUrl, { signal });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }
  return await response.text();
}

/**
 * Appends a query parameter (`?key=value` or `&key=value`) to a URL.
 */
function appendQueryParam(fileUrl: string, key: string, value: string): string {
  if (!fileUrl) return fileUrl;
  const separator = fileUrl.includes('?') ? '&' : '?';
  return `${fileUrl}${separator}${encodeURIComponent(key)}=${encodeURIComponent(value)}`;
}

/**
 * Builds the canonical URL for a file within a job's application packet.
 */
export function getPacketFileUrl(
  jobId: string,
  fileName: PacketFileName,
  download = false,
  version?: number,
): string {
  let url = `/api/jobs/${encodeURIComponent(jobId)}/packet/files/${encodeURIComponent(fileName)}`;
  if (download) {
    url = appendQueryParam(url, 'download', 'true');
  }
  if (version !== undefined) {
    url = appendQueryParam(url, 'v', String(version));
  }
  return url;
}

/**
 * Appends `?download=true` (or `&download=true`) to a packet file URL for attachment downloads.
 */
export function withDownloadParam(fileUrl: string): string {
  return appendQueryParam(fileUrl, 'download', 'true');
}

/**
 * Appends `?v=<version>` (or `&v=<version>`) to a packet file URL for preview cache-busting.
 */
export function withPreviewVersion(fileUrl: string, version: number): string {
  return appendQueryParam(fileUrl, 'v', String(version));
}
