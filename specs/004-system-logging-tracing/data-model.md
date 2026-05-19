# Data Model: System Logging and Tracing Infrastructure

**Feature**: 004-system-logging-tracing  
**Date**: 2026-05-19  
**Branch**: main

## Key Entities

### TraceContext

Represents the context for distributed tracing of a single request flow.

**Fields**:
- `TraceId` (string, required): Unique identifier for the entire request trace. Follows W3C Trace Context format (32-character hex string).
- `SpanId` (string, required): Unique identifier for the current operation within the trace. Follows W3C Trace Context format (16-character hex string).
- `ParentSpanId` (string, optional): Span ID of the parent operation. Empty for root spans.
- `CorrelationId` (string, required): User-facing identifier for tracking requests across systems. Should be human-readable if possible.
- `Timestamp` (DateTime, required): UTC timestamp when the trace context was created.
- `IsRoot` (bool, required): Indicates whether this is the root span (true) or a child span (false).

**Relationships**:
- One trace context can have many child spans (one-to-many)
- Child spans reference their parent via `ParentSpanId`

**Validation Rules**:
- `TraceId` must be 32-character lowercase hex string
- `SpanId` must be 16-character lowercase hex string
- `ParentSpanId` must be 16-character lowercase hex string if provided
- `Timestamp` must be UTC time
- `IsRoot` must be true if `ParentSpanId` is empty/null

**State Transitions**:
- Created when request enters system (root span)
- Child spans created when making downstream calls
- Spans completed when operation finishes

---

### LogEntry

Represents a structured log record.

**Fields**:
- `Timestamp` (DateTime, required): UTC timestamp when the log entry was created.
- `Level` (LogLevel, required): Log level (Trace, Debug, Information, Warning, Error, Critical).
- `Message` (string, required): Human-readable log message.
- `TraceId` (string, required): Reference to the trace context for correlation.
- `SpanId` (string, required): Reference to the span context for correlation.
- `CorrelationId` (string, required): User-facing identifier for tracking.
- `Exception` (ExceptionDetails, optional): Details about any exception that occurred.
- `Properties` (Dictionary<string, object>, optional): Additional structured properties.

**Relationships**:
- Belongs to a trace context (many log entries per trace)
- Belongs to a span context (many log entries per span)

**Validation Rules**:
- `Timestamp` must be UTC time
- `Level` must be one of the defined log levels
- `Message` must not be empty or whitespace
- `TraceId` and `SpanId` must be valid hex strings
- `Exception` properties are required if `Level` is Error or Critical

**State Transitions**:
- Created when logging occurs
- Written to console and/or file
- Exported to OpenTelemetry collector

---

### Span

Represents a unit of work in a distributed trace.

**Fields**:
- `SpanId` (string, required): Unique identifier for this span (16-character hex).
- `TraceId` (string, required): Reference to the parent trace.
- `OperationName` (string, required): Name of the operation (e.g., "HttpGet", "DatabaseQuery").
- `StartTime` (DateTime, required): UTC timestamp when span started.
- `EndTime` (DateTime, optional): UTC timestamp when span completed.
- `Status` (SpanStatus, required): Current status (Unset, Ok, Error).
- `Attributes` (Dictionary<string, object>, optional): Key-value pairs of span attributes.
- `Events` (List<SpanEvent>, optional): Events that occurred during span lifetime.
- `Links` (List<SpanLink>, optional): Links to other spans.
- `ParentSpanId` (string, optional): Reference to parent span.

**Relationships**:
- Child spans reference parent via `ParentSpanId`
- Many spans belong to one trace
- Spans can have multiple child spans

**Validation Rules**:
- `SpanId` must be 16-character lowercase hex string
- `TraceId` must be 32-character lowercase hex string
- `OperationName` must not be empty
- `StartTime` must be before or equal to `EndTime` (if set)
- `Status` must be one of: Unset, Ok, Error

**State Transitions**:
- Created with status Unset
- Started (StartTime set)
- Completed (EndTime set, status set to Ok or Error)

---

### ExceptionDetails

Represents detailed information about an exception.

**Fields**:
- `Type` (string, required): Full type name of the exception (e.g., "System.InvalidOperationException").
- `Message` (string, required): Exception message.
- `StackTrace` (string, required): Full stack trace.
- `InnerException` (ExceptionDetails, optional): Details of inner exception.
- `Source` (string, optional): Name of the application or object that caused the error.

**Relationships**:
- Can contain nested inner exceptions (recursive)

**Validation Rules**:
- `Type` must not be empty
- `Message` must not be empty
- `StackTrace` must not be empty

**State Transitions**:
- Created when exception is caught
- Contains full exception chain

---

### SpanEvent

Represents an event that occurred during span lifetime.

**Fields**:
- `Timestamp` (DateTime, required): UTC timestamp when event occurred.
- `Name` (string, required): Name of the event.
- `Attributes` (Dictionary<string, object>, optional): Event attributes.

**Validation Rules**:
- `Timestamp` must be within span's time range
- `Name` must not be empty

---

### SpanLink

Represents a link to another span.

**Fields**:
- `TraceId` (string, required): Trace ID of the linked span.
- `SpanId` (string, required): Span ID of the linked span.
- `Attributes` (Dictionary<string, object>, optional): Link attributes.

**Validation Rules**:
- `TraceId` must be 32-character hex string
- `SpanId` must be 16-character hex string

---

## Entity Relationships Diagram

```
┌─────────────────┐
│   TraceContext  │
├─────────────────┤
│ TraceId (PK)    │
│ SpanId (PK)     │
│ ParentSpanId    │
│ CorrelationId   │
│ Timestamp       │
│ IsRoot          │
└────────┬────────┘
         │
         │ 1:N
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│      Span       │     │    LogEntry     │
├─────────────────┤     ├─────────────────┤
│ SpanId (PK)     │     │ Timestamp       │
│ TraceId (FK)    │     │ Level           │
│ ParentSpanId    │     │ Message         │
│ OperationName   │     │ TraceId (FK)    │
│ StartTime       │     │ SpanId (FK)     │
│ EndTime         │     │ CorrelationId   │
│ Status          │     │ Exception       │
│ Attributes      │     │ Properties      │
│ Events          │     └─────────────────┘
│ Links           │
└─────────────────┘
```

## Data Flow

1. **Request Inbound**:
   - TraceContext created with new TraceId
   - Root Span created with same TraceId
   - LogEntry written for request start

2. **Request Processing**:
   - Child Spans created for downstream calls
   - LogEntries written for operations
   - TraceContext propagated via HTTP headers

3. **Request Outbound**:
   - Span completed
   - LogEntry written for request completion
   - Trace data exported to OpenTelemetry

4. **Exception Handling**:
   - ExceptionDetails created
   - LogEntry written with ERROR level
   - Span status set to Error
   - Response returned to client

## Persistence

- **Console**: No persistence (transient)
- **File**: JSON format, one file per day, 7-day retention
- **OpenTelemetry**: Exported via OTLP protocol
- **In-memory**: None (stateless logging)

## Performance Considerations

- Log writes must be asynchronous to avoid blocking
- Trace context stored in HttpContext.Items (in-memory)
- Span data buffered before export
- Log rotation handled by file sink (not application code)
