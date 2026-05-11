# Methodology: Website Tracking and Supabase Upload

- Product: Chronos Screen Time Tracker
- Scope: How website activity is captured, persisted, and uploaded
- Platform: Windows Desktop (WPF, .NET 8)
- Date: 2026-03-15

## 1. Objective
Define a clear, repeatable method for:
1. Detecting active website domains from supported browsers.
2. Converting browser activity into daily website metrics.
3. Persisting data locally as source of truth.
4. Uploading app and website data to Supabase on a safe schedule.

## 2. Supported Inputs and Preconditions

### Browser support
1. Microsoft Edge (`msedge`)
2. Google Chrome (`chrome`)
3. Mozilla Firefox (`firefox`)

### Sync preconditions
1. Supabase sync enabled in settings.
2. Supabase URL is provided.
3. Supabase anon key is provided.
4. Supabase user ID is provided and valid.

If preconditions are not met, upload is disabled and local tracking continues.

## 3. Website Tracking Method

### Step A: Active window polling
1. A tracking timer runs every second.
2. On each tick, the app reads the active foreground process.
3. Idle and lock screen states are treated as non-trackable activity.

### Step B: Browser URL extraction
1. If active process is a supported browser, browser tracking starts.
2. UI Automation searches for address bar controls.
3. URL is read using available automation patterns.
4. URL validation is applied before accepting the value.

### Step C: Domain normalization
1. Extract host from URL.
2. Lowercase the domain.
3. Remove `www.` prefix.
4. Remove trailing slash when present.

Result: one canonical domain key per site (for example, `example.com`).

### Step D: Session and duration accumulation
1. If domain changes, close previous website session.
2. Start a new session for the new domain.
3. Increment website session count on switch.
4. Accumulate duration in second-level updates.
5. Ignore sub-second noise where short-session threshold applies.

### Step E: Website record lifecycle
For each new domain:
1. Create a website entity if missing.
2. Set first seen, last seen, and last active timestamps.
3. Assign category via category service.
4. Attach favicon URL for display support.

## 4. Local Persistence Method

### Data model
Data is written in hierarchical daily buckets:
1. Year
2. Month
3. Week
4. Day
5. Day.Websites[domain]

Website daily fields include:
1. Domain
2. TotalTime
3. SessionCount
4. FirstSeen
5. LastSeen
6. LastActiveTime
7. FaviconUrl

### Storage behavior
1. Data is saved periodically during tracking.
2. Data is saved when tracking stops.
3. Data is reloaded on startup.
4. If file is missing or invalid, app falls back safely without crash.

## 5. Supabase Upload Method

### Triggering strategy
1. Upload service is initialized only when sync settings are valid.
2. A recurring timer runs at configured interval (default 30 minutes).
3. A first upload attempt occurs shortly after app startup.
4. A Test Connection action performs a minimal upload check.

### Pre-upload preparation
1. Reload local screentime JSON to avoid memory-disk drift.
2. Recalculate day totals to keep summaries consistent.
3. Skip upload when there is no app or website data.

### Rate gating and cache
1. Read cache from AppData.
2. If last successful upload time plus interval has not passed, skip upload.
3. On success, update cache and last upload timestamp.

## 6. Payload Construction Method

### Endpoint
`POST {supabase_url}/functions/v1/upload-screentime`

### Authentication
1. `apikey: <anon-key>`
2. `authorization: Bearer <anon-key>`

### Payload shape
Top-level fields:
1. `user_id`
2. `device_id`
3. `platform` (windows)
4. `source` (pc)
5. `data` (nested years/months/weeks/days)
6. `daily_summaries`

Per website per day object includes:
1. Domain
2. Category
3. TotalTime (formatted duration string)
4. SessionCount
5. FirstSeen
6. LastSeen
7. LastActiveTime
8. FirstSeenTime
9. LastSeenTime
10. LastActiveTimeOfDay
11. FaviconUrl

## 7. Upload Result Handling

### Success path
1. Parse inserted and total counts from response.
2. Update upload cache.
3. Keep tracking uninterrupted.

### Failure path
1. Capture HTTP or exception error details.
2. Keep local tracking uninterrupted.
3. Retry on next timer cycle.
4. Surface connection test result in UI when triggered manually.

## 8. Data Integrity and Reliability Rules
1. Local JSON remains the source of truth.
2. Upload is additive/upsert-oriented at backend contract level.
3. App lock process data is excluded from meaningful tracking metrics.
4. Upload errors never stop tracking timers.
5. Time-based gate prevents excessive repeated uploads.

## 9. Security and Privacy Posture
1. Local-first by default.
2. Cloud sync is opt-in only.
3. User controls URL, key, user ID, and interval.
4. Only configured Supabase endpoint is used for remote upload.

## 10. Verification Checklist
1. Open three different domains in supported browser and confirm three domain records.
2. Switch domains and verify session count increments.
3. Confirm domain time increases only while domain is active.
4. Restart app and confirm website totals are restored.
5. Enable sync with valid credentials and confirm periodic upload attempts.
6. Force invalid key and confirm upload fails without stopping local tracking.
7. Run Test Connection and verify pass or fail feedback appears.

## 11. Operational Notes
1. If the function path differs from `upload-screentime`, adjust endpoint construction in code.
2. Keep payload contract versioned with Edge Function changes.
3. Validate UUID format for user ID before enabling production sync.
