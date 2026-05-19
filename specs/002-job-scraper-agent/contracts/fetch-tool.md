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