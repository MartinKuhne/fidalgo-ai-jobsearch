# Contract: Fetch Tool

**Purpose**: Fetch web page content from a URL and return sanitized text, stripping HTML rendering elements.

## Signature

```
fetch(url: string) -> string
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| url | string | Required | The URL to fetch (e.g., Indeed.com search URL or individual job page) |

## Indeed Search URL Construction

When searching for jobs on Indeed.com, construct URLs using the following format:

```
https://www.indeed.com/jobs?q={keywords}&l={location}&start={offset}
```

**Query Parameters:**
- `q` - Job keywords (URL-encoded, spaces replaced with `+`)
- `l` - Location (optional, URL-encoded)
- `start` - Pagination offset (0, 10, 20, etc. - Indeed uses 10 results per page)

**Examples:**
- Search for "software engineer" in Seattle: `https://www.indeed.com/jobs?q=software+engineer&l=Seattle`
- Search for "data+analyst" with pagination: `https://www.indeed.com/jobs?q=data+analyst&start=10`

## Returns

| Field | Type | Description |
|-------|------|-------------|
| Content | string | The page content in plaintext/markdown with HTML rendering elements removed |
| Success | bool | Whether the fetch succeeded |
| ErrorMessage | string | Error details if Success is false |

## Behavior

- Makes an HTTP GET request to the specified URL
- Strips HTML rendering-only elements: `<font>`, `<style>`, `<link>`, `<script>`, inline `style` attributes, SVG elements, font-family/font-style CSS properties
- Returns the cleaned content in markdown format
- Handles HTTP errors gracefully (returns Success=false with error message)
- Respects basic rate limiting (e.g., 1 request per 2 seconds)

## Usage Pattern for Job Search

1. Construct search URL with keywords using the Indeed search format
2. Fetch the search results page
3. Extract job listing URLs from the HTML
4. Fetch individual job pages for detailed analysis
5. Iterate through pagination by incrementing `start` parameter

## Sanitization Rules

| Element / Attribute | Reason |
|--------------------|--------|
| `<font>` tags | Rendering only |
| `<style>` tags | Rendering only |
| `<link>` tags | Rendering only |
| `<script>` tags | Not content |
| `style` attribute on any element | Rendering only |
| `font-family` CSS properties | Rendering only |
| `font-size` CSS properties | Rendering only |
| SVG elements | Icons, not content |

## Error Handling

| HTTP Status | Behavior |
|-------------|----------|
| 200 | Return sanitized content |
| 403 / 429 | Return Success=false with "Rate limited" message |
| 404 | Return Success=false with "not found" message |
| Network error | Return Success=false with connection error message |