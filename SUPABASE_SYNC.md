# Supabase Sync – Reference for AI / Developers

This document describes everything related to **Supabase syncing** in the Chronos screentime app so another AI or developer can work on it without searching the codebase.

---

## 1. Overview

- **Purpose:** Upload screentime data (apps + websites, per day) to Supabase for backup and cross-device access.
- **Flow:** User enables sync in Preferences and configures Supabase URL, anon key, and user ID. A timer runs at a configurable interval (default 30 minutes). Each tick saves in-memory data to disk, then the app POSTs a JSON payload to a Supabase Edge Function `upload-screentime`.
- **Direction:** One-way **upload only** (app → Supabase). No download/sync-from-cloud in this codebase.
- **Backend:** The app expects a **Supabase Edge Function** at `POST /functions/v1/upload-screentime` that accepts the payload below and returns a JSON response with `success`, `inserted`, and `total`.

---

## 2. Settings (AppSettings)

Defined in **`Models/AppSettings.cs`**:

```csharp
// Supabase Sync Settings
public bool EnableSupabaseSync { get; set; } = false;
public string SupabaseUrl { get; set; } = string.Empty;
public string SupabaseAnonKey { get; set; } = string.Empty;
public string SupabaseUserId { get; set; } = string.Empty;
public int SupabaseUploadIntervalMinutes { get; set; } = 30;
```

- **EnableSupabaseSync:** Master switch to enable/disable automatic uploads.
- **SupabaseUrl:** Base URL of the Supabase project (e.g. `https://your-project.supabase.co`). Trailing slash is stripped in code.
- **SupabaseAnonKey:** Supabase anonymous (public) key; sent as `apikey` and `Authorization: Bearer <key>`.
- **SupabaseUserId:** User identifier (UUID); must be a valid GUID. Referred to in UI as “LifeOS User ID”.
- **SupabaseUploadIntervalMinutes:** How often to run the upload timer (minutes). Minimum 1, default 30. If 0 or invalid, 30 is used.

---

## 3. Files and Locations

| What | Where |
|------|--------|
| Settings model | `Models/AppSettings.cs` (Supabase properties) |
| Upload service | `Services/SupabaseUploadService.cs` |
| MainWindow integration (init, timer, upload, handlers) | `MainWindow.xaml.cs` (#region Supabase Upload Service, #region Supabase Settings Handlers) |
| Preferences UI (Cloud Sync card) | `MainWindow.xaml` – search for “Cloud Sync (Supabase)” / `PageEnableSupabaseSyncCheckBox` |
| Screen time data model | `Models/TimeData.cs` (`ScreenTimeData`, `YearData`, `MonthData`, `WeekData`, `DayData`, `AppDailyData`, `WebsiteDailyData`) |
| Categories (used for app/website category in payload) | `Services/CategoryService.cs` – `GetCategoryForApp`, `GetCategoryForWebsite` |

---

## 4. SupabaseUploadService (`Services/SupabaseUploadService.cs`)

- **Constructor:** `SupabaseUploadService(string supabaseUrl, string supabaseAnonKey, string? userId = null, string? deviceId = null)`
  - `deviceId` defaults to `Environment.MachineName`.
  - Uses `CategoryService` to resolve app/website category for payload.
  - Cache directory: `%AppData%\ChronosScreenTime`. Cache file: `supabase_upload_cache.json`. AppLock file path: same directory, file named `AppLock` (deleted before each upload).
- **Main API:**  
  `Task<UploadResult> UploadScreentimeDataAsync(ScreenTimeData screenTimeData, string? userId = null, string? deviceId = null, int uploadIntervalMinutes = 30)`
  - Returns `UploadResult`: `Success`, `ErrorMessage`, `AppsInserted`, `WebsitesInserted`, `TotalApps`, `TotalWebsites`.
  - If `userId` is null/empty (and constructor userId is null), returns failure with “User ID is required”.
  - Loads cache; recalculates day totals; skips if no apps/websites in data; **time-based gate:** only uploads if `LastUploadTimeUtc + uploadIntervalMinutes` has passed (or never uploaded).
  - Builds payload via `ConvertToEdgeFunctionFormatFiltered`, POSTs to `{SupabaseUrl}/functions/v1/upload-screentime` with JSON body, then on success updates cache and `LastUploadTimeUtc`.
- **Payload shape:** See **Section 7** below.
- **Time format for durations:** `TotalTime` is sent as string `"hh:mm:ss.fffffff"` (e.g. `"01:30:45.0000000"`). Implemented in `FormatTimeSpan(TimeSpan)`; the comment says the Edge Function parses it and converts to total seconds.
- **Filtering:** App named `"AppLock"` (case-insensitive) is excluded from payload and from daily summary counts. All other apps and all websites are included; the service comment states the Edge Function should **upsert** so re-uploading the same day updates usage.
- **Daily summaries:** For each day that has any apps or websites, a daily summary object is added with `date`, `total_switches` (sum of app `SessionCount` for that day, excluding AppLock), and `total_apps` (count of apps for that day, excluding AppLock). The doc comment says the Edge Function must UPSERT into `screentime_daily_summary` with something like `ON CONFLICT (user_id, date, source, device_id, platform) DO UPDATE`.
- **Cache:** `UploadCache` has `UploadedApps`, `UploadedWebsites`, `UploadedDailySummaries` (HashSets of keys) and `LastUploadTimeUtc` (ISO 8601 string). Cache keys include `userId`, `date`, `source`, `deviceId`, `platform`, and app name or domain or `"summary"`. Cache is used for time-based gating and for tracking what was uploaded; after a successful upload the payload is merged into cache and `LastUploadTimeUtc` is set.
- **HTTP:** `HttpClient` with 5-minute timeout; headers `apikey` and `Authorization: Bearer <anon key>`; `Content-Type: application/json` on the request content.
- **Dispose:** Implements `IDisposable`; disposes `HttpClient`.

---

## 5. MainWindow Integration (`MainWindow.xaml.cs`)

- **Fields:** `_supabaseUploadService`, `_supabaseUploadTimer` (System.Timers.Timer).
- **On startup:** After other init, `InitializeSupabaseUploadService()` is called. If sync is enabled and URL, anon key, and user ID are set, it creates `SupabaseUploadService` and a timer with `SupabaseUploadIntervalMinutes`, then starts a one-shot 30-second timer to run the first upload.
- **On exit:** In cleanup, `_supabaseUploadTimer` is stopped and disposed; `_supabaseUploadService` is disposed.
- **When preferences are saved:** If the saved settings differ (e.g. Supabase settings changed), `InitializeSupabaseUploadService()` is called again (recreates service and timer).
- **Timer elapsed:** `OnSupabaseUploadTimerElapsed()` calls `PerformSupabaseUpload()`.
- **PerformSupabaseUpload():**
  - Requires `_supabaseUploadService` and `_screenTimeService` non-null.
  - If sync is disabled or `SupabaseUserId` is empty, returns without uploading.
  - Calls `_screenTimeService.PrepareDataForUpload()` so data is flushed to disk, then `_screenTimeService.GetScreenTimeData()` to get the same structure that was saved.
  - Calls `_supabaseUploadService.UploadScreentimeDataAsync(screenTimeData, settings.SupabaseUserId, Environment.MachineName, intervalMinutes)`.
  - Logs success/failure to debug; no UI toast on failure (only debug).
- **Preferences UI binding (save):** When saving preferences, Supabase fields are read from:
  - `PageEnableSupabaseSyncCheckBox` → `EnableSupabaseSync`
  - `PageSupabaseUrlTextBox` → `SupabaseUrl`
  - `PageSupabaseAnonKeyPasswordBox` or `PageSupabaseAnonKeyTextBox` (depending on visibility) → `SupabaseAnonKey`
  - `PageSupabaseUserIdTextBox` → `SupabaseUserId`
  - `PageSupabaseUploadIntervalMinutesTextBox` (value > 0) → `SupabaseUploadIntervalMinutes`
- **Preferences UI binding (load):** When loading preferences, the same controls are populated from `CurrentSettings`.
- **Handlers:**
  - **ToggleSupabaseKeyVisibility_Click:** Switches between PasswordBox and TextBox for the anon key; toggles button text “👁️ Show Key” / “🙈 Hide Key”.
  - **PageSupabaseAnonKeyPasswordBox_PasswordChanged:** Keeps the visible TextBox in sync with the PasswordBox when the user types in the password box.
  - **TestSupabaseConnection_Click:** Reads URL, anon key (from PasswordBox or TextBox depending on visibility), and User ID from the page. Validates non-empty and User ID as UUID. Creates a temporary `SupabaseUploadService`, builds minimal `ScreenTimeData` (empty day for today), calls `UploadScreentimeDataAsync` with that data, disposes the service. Shows MessageBox with success (and inserted counts) or failure message. So “Test Connection” actually performs a minimal upload to the Edge Function.

---

## 6. Preferences UI (XAML)

In **`MainWindow.xaml`**, under the Preferences area:

- **Card:** “Cloud Sync (Supabase)” with an InfoBar describing sync and configurable interval.
- **Controls:**
  - Toggle: “Enable Supabase sync” → `PageEnableSupabaseSyncCheckBox`
  - Supabase URL: `PageSupabaseUrlTextBox` (example: `https://your-project.supabase.co`)
  - Supabase Anon Key: `PageSupabaseAnonKeyPasswordBox` (default visible), `PageSupabaseAnonKeyTextBox` (hidden by default), and “👁️ Show Key” button `ToggleSupabaseKeyVisibilityButton`
  - User ID (UUID): `PageSupabaseUserIdTextBox` (hint: “LifeOS User ID”)
  - Upload interval: `PageSupabaseUploadIntervalMinutesTextBox` (NumberBox, min 1, max 10080), unit “minutes”
  - Button: “🔌 Test Connection” → `TestSupabaseConnection_Click`

---

## 7. Edge Function Contract (Payload and Response)

**Endpoint:** `POST {SupabaseUrl}/functions/v1/upload-screentime`  
**Headers:** `apikey`, `Authorization: Bearer <anon key>`, `Content-Type: application/json`

**Request body (summary):**

- Top-level: `user_id`, `device_id`, `platform`, `source`, `data`, `daily_summaries`.
- `platform`: `"windows"`, `source`: `"pc"`.
- `data`: `{ "Years": { "<year>": { "Months": { "<month>": { "Weeks": { "<week>": { "Days": { "<yyyy-MM-dd>": { "Date": "<yyyy-MM-dd>", "Apps": { "<key>": appObj }, "Websites": { "<key>": websiteObj } } } } } } } }`.
- **App object (per app per day):** `AppName`, `Category`, `ProcessPath`, `TotalTime` (string `hh:mm:ss.fffffff`), `SessionCount`, `FirstSeen`, `LastSeen`, `LastActiveTime` (ISO), `FirstSeenTime`, `LastSeenTime`, `LastActiveTimeOfDay` (HH:mm:ss).
- **Website object (per site per day):** `Domain`, `Category`, `TotalTime` (same string format), `SessionCount`, `FirstSeen`, `LastSeen`, `LastActiveTime`, `FirstSeenTime`, `LastSeenTime`, `LastActiveTimeOfDay`, `FaviconUrl`.
- **daily_summaries:** Array of `{ "date": "yyyy-MM-dd", "total_switches": number, "total_apps": number }` for each day that has apps or websites.

**Expected response (success):** JSON with at least:

- `success`: boolean
- `inserted`: `{ "apps": number, "websites": number }`
- `total`: `{ "apps": number, "websites": number }`

The client deserializes into `UploadResponse` / `InsertedData` / `TotalData` and logs or shows `result.Inserted.Apps`, `result.Inserted.Websites`. On non–2xx status, the body is put into `UploadResult.ErrorMessage`.

---

## 8. Data Models Used for Upload

- **ScreenTimeData** (`Models/TimeData.cs`): `Years` → YearData.
- **YearData:** `Months` → MonthData.
- **MonthData:** `Weeks` → WeekData.
- **WeekData:** `Days` (Dictionary<DateTime, DayData>).
- **DayData:** `Date`, `Apps` (Dictionary<string, AppDailyData>), `Websites` (Dictionary<string, WebsiteDailyData>), `TotalSwitches`, `TotalApps`.
- **AppDailyData:** `AppName`, `Category`, `ProcessPath`, `TotalTime`, `SessionCount`, `FirstSeen`, `LastSeen`, `LastActiveTime`. (Category in payload comes from `CategoryService.GetCategoryForApp(AppName)` if not set.)
- **WebsiteDailyData:** `Domain`, `TotalTime`, `SessionCount`, `FirstSeen`, `LastSeen`, `LastActiveTime`, `FaviconUrl`. (Category in payload from `CategoryService.GetCategoryForWebsite(Domain)`.)

`GetIso8601WeekOfYear(DateTime)` in MainWindow is used for the test payload’s week number; the upload service builds the same hierarchy from `ScreenTimeData`.

---

## 9. Cache and Time Gating

- **Path:** `%AppData%\ChronosScreenTime\supabase_upload_cache.json`.
- **Contents:** `UploadedApps`, `UploadedWebsites`, `UploadedDailySummaries` (lists of composite keys), and `LastUploadTimeUtc` (ISO 8601 string).
- **Usage:** After a successful upload, the payload is merged into cache and `LastUploadTimeUtc = DateTime.UtcNow.ToString("o")`. The next upload is allowed only when `DateTime.UtcNow >= lastUtc + uploadIntervalMinutes`. So the same device will not upload more often than the configured interval even if the timer fires earlier.

---

## 10. Quick Checklist for Another AI

- To **change upload interval or add a new setting:** Update `AppSettings.cs`, Preferences XAML, and the save/load and init logic in `MainWindow.xaml.cs`; pass the value into `UploadScreentimeDataAsync` or timer setup as needed.
- To **change payload or add a new field:** Edit `ConvertToEdgeFunctionFormatFiltered` in `SupabaseUploadService.cs` and ensure the Edge Function and DB schema accept it.
- To **add a new Supabase endpoint (e.g. health check):** Add a method in `SupabaseUploadService` (or a small helper) that calls the same base URL and headers; “Test Connection” could be switched to a lighter-weight call if desired.
- To **support download/sync-from-cloud:** No current implementation; would require new settings, service methods, and UI to fetch from Supabase and merge into `ScreenTimeData` / `ScreenTimeService`.

---

## 11. Supabase-only settings (JSON)

Use only these keys when passing or persisting settings for Supabase sync (e.g. for another AI or config export):

```json
{
  "EnableSupabaseSync": true,
  "SupabaseUrl": "https://your-project.supabase.co/functions/v1/upload-screentime-chronos",
  "SupabaseAnonKey": "your-supabase-anon-key",
  "SupabaseUserId": "your-supabase-user-uuid",
  "SupabaseUploadIntervalMinutes": 3
}
```

**Note:** If your Edge Function is not at `/functions/v1/upload-screentime`, the app currently appends that path to `SupabaseUrl`. So either set `SupabaseUrl` to the project base (e.g. `https://your-project.supabase.co`) and use an Edge Function named `upload-screentime`, or change the app to use `SupabaseUrl` as the full function URL when it already contains the path.

---

## 12. Edge Function (Deno) – full source

The Supabase Edge Function that receives Chronos (and other clients) uploads. It accepts the payload described in **Section 7**, supports nested `data.Years`, `daily_summaries`, flat `snapshots`, root-level `apps`/`websites`/`items`, and `activity_summary`. It upserts into `screentime_daily_app_stats`, `screentime_daily_website_stats`, and `screentime_daily_summary`. Uses `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY` env vars. Response includes `success`, `inserted` (apps, websites, summaries), `total`, and optionally `verify` when `debug: true`.

```typescript
/// <reference path="../deno.d.ts" />

import { createClient } from 'npm:@supabase/supabase-js@2';

const supabaseUrl = Deno.env.get('SUPABASE_URL')!;
const serviceRoleKey = Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!;

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
};

const supabase = createClient(supabaseUrl, serviceRoleKey);

interface AppData {
  AppName: string;
  Category: string;
  ProcessPath?: string;
  TotalTime: string;
  SessionCount: number;
  FirstSeen: string;
  LastSeen: string;
  LastActiveTime: string;
}

interface WebsiteData {
  Domain: string;
  TotalTime: string;
  SessionCount: number;
  FirstSeen: string;
  LastSeen: string;
  LastActiveTime: string;
  FaviconUrl?: string;
}

interface DayData {
  Date: string;
  Apps?: Record<string, AppData>;
  Websites?: Record<string, WebsiteData>;
  TotalSwitches?: number;
  TotalTime?: string;
  TotalApps?: number;
}

/** One entry per day for screentime_daily_summary (sent by tracker at root level). */
interface DailySummaryItem {
  date: string; // YYYY-MM-DD
  total_switches: number;
  total_apps: number;
}

interface FlatUsageItem {
  date?: string;
  name?: string;
  app_name?: string;
  app?: string;
  domain?: string;
  site?: string;
  url?: string;
  category?: string;
  process_path?: string;
  processPath?: string;
  favicon_url?: string;
  faviconUrl?: string;
  total_time_seconds?: number | string;
  duration_seconds?: number | string;
  seconds?: number | string;
  total_time?: number | string;
  totalTime?: number | string;
  duration?: number | string;
  time?: string;
  duration_minutes?: number | string;
  minutes?: number | string;
  total_minutes?: number | string;
  totalMinutes?: number | string;
  session_count?: number | string;
  sessions?: number | string;
  sessionCount?: number | string;
  first_seen_at?: string;
  firstSeenAt?: string;
  FirstSeen?: string;
  last_seen_at?: string;
  lastSeenAt?: string;
  LastSeen?: string;
  last_active_at?: string;
  lastActiveAt?: string;
  LastActiveTime?: string;
  kind?: string;
  type?: string;
}

interface FlatSnapshot {
  date?: string;
  apps?: FlatUsageItem[];
  websites?: FlatUsageItem[];
  items?: FlatUsageItem[];
  total_switches?: number | string;
  totalSwitches?: number | string;
  total_apps?: number | string;
  totalApps?: number | string;
}

interface ScreentimePayload {
  user_id: string;
  device_id?: string;
  platform: string;
  source: string;
  upload_date?: string;
  upload_time?: string;
  uploadDate?: string;
  uploadTime?: string;
  debug?: boolean;
  is_cumulative?: boolean;
  cumulative?: boolean;
  data?: {
    Years: Record<string, {
      Months: Record<string, {
        Weeks: Record<string, {
          Days: Record<string, DayData>;
        }>;
      }>;
    }>;
  };
  daily_summaries?: DailySummaryItem[];
  snapshots?: FlatSnapshot[];
  date?: string;
  apps?: FlatUsageItem[];
  websites?: FlatUsageItem[];
  items?: FlatUsageItem[];
  total_switches?: number | string;
  totalSwitches?: number | string;
  total_apps?: number | string;
  totalApps?: number | string;
  activity_summary?: string;
}

function parseTimeToSeconds(timeStr: string): number {
  const trimmed = String(timeStr || '').trim();
  if (!trimmed) return 0;
  const parts = trimmed.split(':');
  if (parts.length === 3) {
    const hours = parseInt(parts[0], 10) || 0;
    const minutes = parseInt(parts[1], 10) || 0;
    const secondsParts = parts[2].split('.');
    const seconds = parseInt(secondsParts[0], 10) || 0;
    return Math.max(0, hours * 3600 + minutes * 60 + seconds);
  }
  if (parts.length === 2) {
    const minutes = parseInt(parts[0], 10) || 0;
    const secondsParts = parts[1].split('.');
    const seconds = parseInt(secondsParts[0], 10) || 0;
    return Math.max(0, minutes * 60 + seconds);
  }
  return 0;
}

function parseDurationToSeconds(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) return Math.max(0, Math.round(value));
  if (typeof value !== 'string') return 0;
  const trimmed = value.trim();
  if (!trimmed) return 0;
  if (/^\d+(\.\d+)?$/.test(trimmed)) return Math.max(0, Math.round(parseFloat(trimmed)));
  if (trimmed.includes(':')) return parseTimeToSeconds(trimmed);
  const hoursMatch = trimmed.match(/(\d+(?:\.\d+)?)\s*h/i);
  const minutesMatch = trimmed.match(/(\d+(?:\.\d+)?)\s*m(?:in)?/i);
  const secondsMatch = trimmed.match(/(\d+(?:\.\d+)?)\s*s(?:ec)?/i);
  if (hoursMatch || minutesMatch || secondsMatch) {
    const hours = hoursMatch ? parseFloat(hoursMatch[1]) : 0;
    const minutes = minutesMatch ? parseFloat(minutesMatch[1]) : 0;
    const seconds = secondsMatch ? parseFloat(secondsMatch[1]) : 0;
    return Math.max(0, Math.round(hours * 3600 + minutes * 60 + seconds));
  }
  return 0;
}

function parseDateToDateString(dateStr: string): string {
  const raw = String(dateStr || '').trim();
  if (!raw) return raw;
  const slashMatch = raw.match(/^(\d{1,2})\/(\d{1,2})\/(\d{2}|\d{4})(?:\s+.*)?$/);
  if (slashMatch) {
    let a = parseInt(slashMatch[1], 10);
    let b = parseInt(slashMatch[2], 10);
    const yRaw = slashMatch[3];
    let year = parseInt(yRaw, 10);
    if (yRaw.length === 2) year = 2000 + year;
    let month = a, day = b;
    if (a > 12 && b <= 12) { month = b; day = a; }
    if (Number.isFinite(year) && Number.isFinite(month) && Number.isFinite(day) &&
        year >= 2000 && year <= 2100 && month >= 1 && month <= 12 && day >= 1 && day <= 31) {
      return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    }
  }
  try {
    const date = new Date(raw);
    if (isNaN(date.getTime())) return raw.split('T')[0];
    return date.toISOString().split('T')[0];
  } catch {
    return raw.split('T')[0];
  }
}

function parseTimestamp(tsStr: string): string | null {
  try {
    const date = new Date(tsStr);
    if (isNaN(date.getTime())) return null;
    return date.toISOString();
  } catch {
    return null;
  }
}

function buildUploadedAt(uploadDateRaw: string, uploadTimeRaw?: string | null): string | null {
  const datePart = parseDateToDateString(uploadDateRaw);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(datePart)) return null;
  const timeTrimmed = (uploadTimeRaw || '').trim();
  if (!timeTrimmed) return `${datePart}T00:00:00.000Z`;
  const m = timeTrimmed.match(/^(\d{1,2})(?::(\d{1,2}))?(?::(\d{1,2}))?$/);
  if (!m) return null;
  const hh = Math.max(0, Math.min(23, parseInt(m[1], 10) || 0));
  const mm = Math.max(0, Math.min(59, parseInt(m[2] ?? '0', 10) || 0));
  const ss = Math.max(0, Math.min(59, parseInt(m[3] ?? '0', 10) || 0));
  return `${datePart}T${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}.000Z`;
}

function toRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

function firstString(obj: Record<string, unknown>, keys: string[]): string | null {
  for (const key of keys) {
    const value = obj[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return null;
}

function firstNumber(obj: Record<string, unknown>, keys: string[]): number | null {
  for (const key of keys) {
    const value = obj[key];
    if (typeof value === 'number' && Number.isFinite(value)) return Math.round(value);
    if (typeof value === 'string' && value.trim() && !Number.isNaN(Number(value))) return Math.round(Number(value));
  }
  return null;
}

function getItemDurationSeconds(item: Record<string, unknown>): number {
  const secondsDirect = firstNumber(item, ['total_time_seconds', 'duration_seconds', 'seconds', 'totalSeconds']);
  if (secondsDirect !== null) return Math.max(0, secondsDirect);
  const minutesDirect = firstNumber(item, ['duration_minutes', 'minutes', 'total_minutes', 'totalMinutes']);
  if (minutesDirect !== null) return Math.max(0, minutesDirect * 60);
  const durationRaw = item.total_time ?? item.totalTime ?? item.duration ?? item.time;
  return parseDurationToSeconds(durationRaw);
}

function getItemSessionCount(item: Record<string, unknown>): number {
  const count = firstNumber(item, ['session_count', 'sessions', 'sessionCount', 'SessionCount']);
  return Math.max(0, count ?? 0);
}

function minIso(a: string | null, b: string | null): string | null {
  if (!a) return b; if (!b) return a; return a < b ? a : b;
}
function maxIso(a: string | null, b: string | null): string | null {
  if (!a) return b; if (!b) return a; return a > b ? a : b;
}

function extractDomain(raw: string): string {
  const trimmed = raw.trim();
  if (!trimmed) return '';
  const normalizeHost = (host: string) => host.replace(/^www\./i, '').toLowerCase();
  try {
    const asUrl = new URL(trimmed);
    return normalizeHost(asUrl.hostname);
  } catch {}
  try {
    const asUrl = new URL(`https://${trimmed}`);
    return normalizeHost(asUrl.hostname);
  } catch {
    return normalizeHost(trimmed.split('/')[0]);
  }
}

function isWebsiteItem(item: Record<string, unknown>): boolean {
  const explicitKind = firstString(item, ['kind', 'type'])?.toLowerCase();
  if (explicitKind) {
    if (['website', 'web', 'site', 'domain', 'url'].includes(explicitKind)) return true;
    if (['app', 'application'].includes(explicitKind)) return false;
  }
  return firstString(item, ['domain', 'site', 'url']) !== null;
}

function categorizeApp(appName: string): string {
  if (!appName) return 'Uncategorized';
  const normalized = appName.toLowerCase().trim();
  const appCategoryMap: Record<string, string> = {
    'code': 'Development', 'cursor': 'Development', 'windowsterminal': 'Development', 'notepad': 'Development',
    'jetbrains-toolbox': 'Development', 'githubdesktop': 'Development', 'powershell': 'Development',
    'visual studio code': 'Development', 'vscode': 'Development',
    'ticktick': 'Productivity', 'icloudpasswords': 'Productivity', 'excel': 'Productivity', 'powerpnt': 'Productivity',
    'powerpoint': 'Productivity', 'word': 'Productivity', 'onenote': 'Productivity', 'outlook': 'Productivity',
    'notes': 'Productivity', 'reminders': 'Productivity', 'calendar': 'Productivity', 'shortcuts': 'Productivity',
    'zoho desk': 'Productivity', 'gemini': 'Productivity', 'chatgpt': 'Productivity',
    'snippingtool': 'Utilities', 'winrar': 'Utilities', 'calculator': 'Utilities', 'cleanmgr': 'Utilities',
    'vlc': 'Entertainment', 'applemusic': 'Entertainment', 'itunes': 'Entertainment', 'music': 'Entertainment',
    'youtube': 'Entertainment', 'tiktok': 'Entertainment', 'instagram': 'Entertainment', 'netflix': 'Entertainment',
    'spotify': 'Entertainment',
    'whatsapp.root': 'Communication', 'whatsapp': 'Communication', 'wa business': 'Communication', 'messages': 'Communication',
    'mail': 'Communication', 'facetime': 'Communication', 'telegram': 'Communication', 'signal': 'Communication',
    'discord': 'Communication', 'slack': 'Communication', 'phone': 'Communication',
    'safari': 'Web Browsing', 'msedge': 'Web Browsing', 'chrome': 'Web Browsing', 'firefox': 'Web Browsing',
    'explorer': 'Web Browsing', 'shellexperiencehost': 'Web Browsing', 'msiexec': 'Web Browsing', 'web': 'Web Browsing',
    'facebook': 'Social', 'twitter': 'Social', 'x': 'Social', 'linkedin': 'Social', 'reddit': 'Social', 'snapchat': 'Social',
    'photoshop': 'Media', 'capcut': 'Media', 'snapseed': 'Media', 'picsart': 'Media', 'photos': 'Media', 'vn': 'Media',
    'settings': 'System', 'clock': 'System', 'app store': 'System', 'softwareupdate': 'System', 'applicationframehost': 'System',
    'shellhost': 'System', 'searchhost': 'System', 'credentialuibroker': 'System', 'lockapp': 'System',
    'chronos-screentime': 'System', 'lifeos': 'System',
    'icloudhome': 'Cloud', 'drive': 'Cloud', 'dropbox': 'Cloud', 'onedrive': 'Cloud', 'google drive': 'Cloud',
    'google maps': 'Navigation', 'maps': 'Navigation', 'waze': 'Navigation', 'apple maps': 'Navigation',
    'ld': 'Gaming', 'steam': 'Gaming', 'epic games': 'Gaming',
  };
  if (appCategoryMap[normalized]) return appCategoryMap[normalized];
  if (/code|editor|ide|studio|dev/i.test(normalized)) return 'Development';
  if (/terminal|cmd|powershell|bash|shell|console/i.test(normalized)) return 'Development';
  if (/git|github|gitlab|bitbucket|version control/i.test(normalized)) return 'Development';
  if (/browser|chrome|edge|firefox|safari|web|explorer/i.test(normalized)) return 'Browsing';
  if (/photo|image|picture|gallery|camera|snap/i.test(normalized)) return 'Media';
  if (/video|movie|film|player|vlc|media player/i.test(normalized)) return 'Media';
  if (/music|audio|sound|spotify|apple music|itunes|streaming/i.test(normalized)) return 'Entertainment';
  if (/message|chat|whatsapp|telegram|signal|messenger|sms/i.test(normalized)) return 'Communication';
  if (/mail|email|outlook|gmail|post/i.test(normalized)) return 'Communication';
  if (/social|facebook|twitter|instagram|linkedin|snapchat|tiktok/i.test(normalized)) return 'Social';
  if (/note|memo|notepad|text|document|write/i.test(normalized)) return 'Productivity';
  if (/calendar|schedule|reminder|todo|task|ticktick/i.test(normalized)) return 'Productivity';
  if (/bank|finance|payment|wallet|money|fawry|thndr|instapay/i.test(normalized)) return 'Finance';
  if (/health|fitness|workout|exercise|wellness/i.test(normalized)) return 'Health';
  if (/map|navigation|gps|location|directions/i.test(normalized)) return 'Navigation';
  if (/game|gaming|play|steam|epic/i.test(normalized)) return 'Gaming';
  if (/setting|config|preference|control panel|options/i.test(normalized)) return 'System';
  if (/system|windows|host|service|driver|process|exec|manager/i.test(normalized)) return 'System';
  if (/cloud|sync|backup|storage|icloud|drive|dropbox/i.test(normalized)) return 'Cloud';
  if (/utility|tool|helper|manager|clean|snipping|calculator/i.test(normalized)) return 'Utilities';
  if (/ai|assistant|chatgpt|gemini|claude/i.test(normalized)) return 'Productivity';
  return 'Uncategorized';
}

function parseActivitySummary(text: string): FlatUsageItem[] {
  if (!text) return [];
  return text.split(/\r?\n/).map(line => line.trim()).filter(line => line.length > 0).map((line) => {
    const match = line.match(/^(.+?)\s*\(([^)]+)\)$/);
    if (!match) return null;
    const name = match[1].trim();
    const durationLabel = match[2].trim();
    const durationSeconds = parseDurationToSeconds(durationLabel);
    if (durationSeconds <= 0) return null;
    const isWebsite = /\./.test(name);
    const entry: FlatUsageItem = { total_time_seconds: durationSeconds, duration: durationLabel, duration_minutes: Math.round(durationSeconds / 60) };
    if (isWebsite) entry.domain = name; else entry.app_name = name;
    return entry;
  }).filter((item): item is FlatUsageItem => item !== null);
}

Deno.serve(async (req: Request) => {
  if (req.method === 'OPTIONS') return new Response(null, { headers: corsHeaders });

  try {
    const FUNCTION_VERSION = 'upload-screentime@2026-02-17';
    const received_at = new Date().toISOString();
    const payload = (await req.json()) as ScreentimePayload;

    if (!payload.user_id) {
      return new Response(JSON.stringify({ error: 'user_id is required' }), { status: 400, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
    }

    const payloadRecord = toRecord(payload);
    const uploadDateRaw = firstString(payloadRecord, ['upload_date', 'uploadDate']);
    const uploadTimeRaw = firstString(payloadRecord, ['upload_time', 'uploadTime']);
    const upload_date = uploadDateRaw ? parseDateToDateString(uploadDateRaw) : null;
    const upload_time = uploadTimeRaw ? uploadTimeRaw.trim() : null;
    const uploaded_at = uploadDateRaw ? buildUploadedAt(uploadDateRaw, uploadTimeRaw) : null;
    const debugEnabled = payload.debug === true;

    const normalizedSnapshots = Array.isArray(payload.snapshots) ? [...payload.snapshots] : [];
    let activitySummaryDateUsed: string | null = null;
    let parsedActivityItemsCount = 0;
    if (payload.activity_summary) {
      const activityItems = parseActivitySummary(payload.activity_summary);
      parsedActivityItemsCount = activityItems.length;
      if (activityItems.length > 0) {
        const activityDate = parseDateToDateString(payload.date || new Date().toISOString());
        activitySummaryDateUsed = activityDate;
        normalizedSnapshots.push({ date: activityDate, items: activityItems });
      } else {
        const hasOtherPayload =
          (payload.data && typeof payload.data.Years === 'object' && Object.keys(payload.data.Years).length > 0) ||
          (Array.isArray(payload.daily_summaries) && payload.daily_summaries.length > 0) ||
          (Array.isArray(payload.snapshots) && payload.snapshots.length > 0) ||
          (Array.isArray(payload.apps) && payload.apps.length > 0) ||
          (Array.isArray(payload.websites) && payload.websites.length > 0) ||
          (Array.isArray(payload.items) && payload.items.length > 0);
        if (!hasOtherPayload) {
          return new Response(JSON.stringify({
            error: 'activity_summary was provided but no valid rows could be parsed.',
            hint: 'Each line must look like: "Instagram (42m)" or "YouTube (1h 12m)". Also send root-level "date" (YYYY-MM-DD).',
          }), { status: 400, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
        }
      }
    }

    const hasYears = payload.data && typeof payload.data.Years === 'object' && Object.keys(payload.data.Years).length > 0;
    const hasDailySummaries = Array.isArray(payload.daily_summaries) && payload.daily_summaries.length > 0;
    const hasSnapshots = normalizedSnapshots.length > 0;
    const hasRootItems = (Array.isArray(payload.apps) && payload.apps.length > 0) || (Array.isArray(payload.websites) && payload.websites.length > 0) || (Array.isArray(payload.items) && payload.items.length > 0);
    const hasRootSummary = firstNumber(payloadRecord, ['total_switches', 'totalSwitches']) !== null || firstNumber(payloadRecord, ['total_apps', 'totalApps']) !== null;

    if (!hasYears && !hasDailySummaries && !hasSnapshots && !hasRootItems && !hasRootSummary) {
      return new Response(JSON.stringify({ error: 'Provide one of: data.Years, daily_summaries, snapshots, or root-level apps/websites/items' }), { status: 400, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
    }

    const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!uuidRegex.test(payload.user_id)) {
      return new Response(JSON.stringify({ error: 'Invalid user_id format. Must be a valid UUID.' }), { status: 400, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
    }

    const source = payload.source || 'pc';
    const rawPlatform = (payload.platform || 'windows') as string;
    const platform = rawPlatform.toLowerCase() || 'windows';
    const deviceId = payload.device_id || '';
    const isCumulative = payload.is_cumulative === true || payload.cumulative === true;
    const todayDate = new Date().toISOString().split('T')[0];

    const appRows: any[] = [];
    const websiteRows: any[] = [];
    const summaryRows: any[] = [];

    const pushAppRow = (dateStr: string, raw: unknown) => {
      const item = toRecord(raw);
      const appName = firstString(item, ['app_name', 'app', 'name', 'AppName']);
      if (!appName) return;
      const firstSeen = firstString(item, ['first_seen_at', 'firstSeenAt', 'FirstSeen']);
      const lastSeen = firstString(item, ['last_seen_at', 'lastSeenAt', 'LastSeen']);
      const lastActive = firstString(item, ['last_active_at', 'lastActiveAt', 'LastActiveTime']);
      const providedCategory = firstString(item, ['category', 'Category']);
      const category = providedCategory && providedCategory !== 'Uncategorized' ? providedCategory : categorizeApp(appName);
      appRows.push({
        user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform,
        app_name: appName, category, process_path: firstString(item, ['process_path', 'processPath', 'ProcessPath']),
        total_time_seconds: getItemDurationSeconds(item), session_count: getItemSessionCount(item),
        first_seen_at: firstSeen ? parseTimestamp(firstSeen) : null, last_seen_at: lastSeen ? parseTimestamp(lastSeen) : null, last_active_at: lastActive ? parseTimestamp(lastActive) : null,
      });
    };

    const pushWebsiteRow = (dateStr: string, raw: unknown) => {
      const item = toRecord(raw);
      const rawDomain = firstString(item, ['domain', 'site', 'url', 'name', 'Domain']);
      if (!rawDomain) return;
      const domain = extractDomain(rawDomain);
      if (!domain) return;
      const firstSeen = firstString(item, ['first_seen_at', 'firstSeenAt', 'FirstSeen']);
      const lastSeen = firstString(item, ['last_seen_at', 'lastSeenAt', 'LastSeen']);
      const lastActive = firstString(item, ['last_active_at', 'lastActiveAt', 'LastActiveTime']);
      websiteRows.push({
        user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform, domain,
        favicon_url: firstString(item, ['favicon_url', 'faviconUrl', 'FaviconUrl']),
        total_time_seconds: getItemDurationSeconds(item), session_count: getItemSessionCount(item),
        first_seen_at: firstSeen ? parseTimestamp(firstSeen) : null, last_seen_at: lastSeen ? parseTimestamp(lastSeen) : null, last_active_at: lastActive ? parseTimestamp(lastActive) : null,
      });
    };

    if (hasDailySummaries && payload.daily_summaries) {
      const byKey = new Map<string, { user_id: string; date: string; source: string; device_id: string; platform: string; total_switches: number; total_apps: number }>();
      for (const item of payload.daily_summaries) {
        const dateStr = parseDateToDateString(item.date);
        const key = `${dateStr}|${source}|${deviceId}|${platform}`;
        const nextSwitches = typeof item.total_switches === 'number' ? Math.max(0, Math.round(item.total_switches)) : 0;
        const nextApps = typeof item.total_apps === 'number' ? Math.max(0, Math.round(item.total_apps)) : 0;
        const existing = byKey.get(key);
        if (existing) {
          existing.total_switches = isCumulative ? Math.max(existing.total_switches, nextSwitches) : nextSwitches;
          existing.total_apps = isCumulative ? Math.max(existing.total_apps, nextApps) : nextApps;
        } else {
          byKey.set(key, { user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform, total_switches: nextSwitches, total_apps: nextApps });
        }
      }
      summaryRows.push(...byKey.values());
    }

    if (hasYears && payload.data) {
      for (const yearKey in payload.data.Years) {
        const year = payload.data.Years[yearKey];
        if (!year.Months) continue;
        for (const monthKey in year.Months) {
          const month = year.Months[monthKey];
          if (!month.Weeks) continue;
          for (const weekKey in month.Weeks) {
            const week = month.Weeks[weekKey];
            if (!week.Days) continue;
            for (const dayKey in week.Days) {
              const day = week.Days[dayKey];
              const dateStr = day.Date ? parseDateToDateString(day.Date) : parseDateToDateString(dayKey);
              if (day.Apps) {
                for (const appKey in day.Apps) {
                  const app = day.Apps[appKey];
                  const appName = app.AppName || appKey;
                  const providedCategory = app.Category;
                  const category = providedCategory && providedCategory !== 'Uncategorized' ? providedCategory : categorizeApp(appName);
                  appRows.push({
                    user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform,
                    app_name: appName, category, process_path: app.ProcessPath || null,
                    total_time_seconds: parseTimeToSeconds(app.TotalTime), session_count: app.SessionCount || 0,
                    first_seen_at: app.FirstSeen ? parseTimestamp(app.FirstSeen) : null, last_seen_at: app.LastSeen ? parseTimestamp(app.LastSeen) : null, last_active_at: app.LastActiveTime ? parseTimestamp(app.LastActiveTime) : null,
                  });
                }
              }
              if (day.Websites) {
                for (const domainKey in day.Websites) {
                  const website = day.Websites[domainKey];
                  websiteRows.push({
                    user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform,
                    domain: website.Domain || domainKey, favicon_url: website.FaviconUrl || null,
                    total_time_seconds: parseTimeToSeconds(website.TotalTime), session_count: website.SessionCount || 0,
                    first_seen_at: website.FirstSeen ? parseTimestamp(website.FirstSeen) : null, last_seen_at: website.LastSeen ? parseTimestamp(website.LastSeen) : null, last_active_at: website.LastActiveTime ? parseTimestamp(website.LastActiveTime) : null,
                  });
                }
              }
              if (!hasDailySummaries) {
                summaryRows.push({
                  user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform,
                  total_switches: day.TotalSwitches ?? 0, total_apps: day.TotalApps ?? 0,
                });
              }
            }
          }
        }
      }
    }

    if (hasSnapshots) {
      for (const snapshotRaw of normalizedSnapshots) {
        const snapshot = toRecord(snapshotRaw);
        const snapshotDateInput = firstString(snapshot, ['date']) || payload.date || todayDate;
        const dateStr = parseDateToDateString(snapshotDateInput);
        const snapshotApps = Array.isArray(snapshot.apps) ? snapshot.apps : [];
        const snapshotWebsites = Array.isArray(snapshot.websites) ? snapshot.websites : [];
        const snapshotItems = Array.isArray(snapshot.items) ? snapshot.items : [];
        for (const appItem of snapshotApps) {
          pushAppRow(parseDateToDateString(firstString(toRecord(appItem), ['date']) || dateStr), appItem);
        }
        for (const websiteItem of snapshotWebsites) {
          pushWebsiteRow(parseDateToDateString(firstString(toRecord(websiteItem), ['date']) || dateStr), websiteItem);
        }
        for (const genericItem of snapshotItems) {
          const itemRecord = toRecord(genericItem);
          const itemDate = parseDateToDateString(firstString(itemRecord, ['date']) || dateStr);
          if (isWebsiteItem(itemRecord)) pushWebsiteRow(itemDate, itemRecord); else pushAppRow(itemDate, itemRecord);
        }
        const summarySwitches = firstNumber(snapshot, ['total_switches', 'totalSwitches']);
        const summaryApps = firstNumber(snapshot, ['total_apps', 'totalApps']);
        if (summarySwitches !== null || summaryApps !== null) {
          summaryRows.push({ user_id: payload.user_id, date: dateStr, source, device_id: deviceId, platform, total_switches: Math.max(0, summarySwitches ?? 0), total_apps: Math.max(0, summaryApps ?? 0) });
        }
      }
    }

    if (hasRootItems || hasRootSummary) {
      const baseDate = parseDateToDateString(payload.date || todayDate);
      const rootApps = Array.isArray(payload.apps) ? payload.apps : [];
      const rootWebsites = Array.isArray(payload.websites) ? payload.websites : [];
      const rootItems = Array.isArray(payload.items) ? payload.items : [];
      for (const appItem of rootApps) pushAppRow(parseDateToDateString(firstString(toRecord(appItem), ['date']) || baseDate), appItem);
      for (const websiteItem of rootWebsites) pushWebsiteRow(parseDateToDateString(firstString(toRecord(websiteItem), ['date']) || baseDate), websiteItem);
      for (const genericItem of rootItems) {
        const itemRecord = toRecord(genericItem);
        const itemDate = parseDateToDateString(firstString(itemRecord, ['date']) || baseDate);
        if (isWebsiteItem(itemRecord)) pushWebsiteRow(itemDate, itemRecord); else pushAppRow(itemDate, itemRecord);
      }
      const summarySwitches = firstNumber(payloadRecord, ['total_switches', 'totalSwitches']);
      const summaryApps = firstNumber(payloadRecord, ['total_apps', 'totalApps']);
      if (summarySwitches !== null || summaryApps !== null) {
        summaryRows.push({ user_id: payload.user_id, date: baseDate, source, device_id: deviceId, platform, total_switches: Math.max(0, summarySwitches ?? 0), total_apps: Math.max(0, summaryApps ?? 0) });
      }
    }

    const appRowsByKey = new Map<string, typeof appRows[0]>();
    for (const row of appRows) {
      const key = `${row.date}|${row.source}|${row.device_id}|${row.platform}|${row.app_name}`;
      const existing = appRowsByKey.get(key);
      if (existing) {
        if (isCumulative) {
          existing.total_time_seconds = Math.max(existing.total_time_seconds, row.total_time_seconds);
          existing.session_count = Math.max(existing.session_count, row.session_count);
        } else {
          existing.total_time_seconds += row.total_time_seconds;
          existing.session_count += row.session_count;
        }
        existing.last_active_at = maxIso(existing.last_active_at, row.last_active_at);
        existing.first_seen_at = minIso(existing.first_seen_at, row.first_seen_at);
        existing.last_seen_at = maxIso(existing.last_seen_at, row.last_seen_at);
        if ((!existing.category || existing.category === 'Uncategorized') && row.category) existing.category = row.category;
        if (!existing.process_path && row.process_path) existing.process_path = row.process_path;
      } else {
        appRowsByKey.set(key, { ...row });
      }
    }
    const mergedAppRows = Array.from(appRowsByKey.values());

    const websiteRowsByKey = new Map<string, typeof websiteRows[0]>();
    for (const row of websiteRows) {
      const key = `${row.date}|${row.source}|${row.device_id}|${row.platform}|${row.domain}`;
      const existing = websiteRowsByKey.get(key);
      if (existing) {
        if (isCumulative) {
          existing.total_time_seconds = Math.max(existing.total_time_seconds, row.total_time_seconds);
          existing.session_count = Math.max(existing.session_count, row.session_count);
        } else {
          existing.total_time_seconds += row.total_time_seconds;
          existing.session_count += row.session_count;
        }
        existing.last_active_at = maxIso(existing.last_active_at, row.last_active_at);
        existing.first_seen_at = minIso(existing.first_seen_at, row.first_seen_at);
        existing.last_seen_at = maxIso(existing.last_seen_at, row.last_seen_at);
        if (!existing.favicon_url && row.favicon_url) existing.favicon_url = row.favicon_url;
      } else {
        websiteRowsByKey.set(key, { ...row });
      }
    }
    const mergedWebsiteRows = Array.from(websiteRowsByKey.values());

    const summaryRowsByKey = new Map<string, typeof summaryRows[0]>();
    for (const row of summaryRows) {
      const key = `${row.date}|${row.source}|${row.device_id}|${row.platform}`;
      const existing = summaryRowsByKey.get(key);
      if (existing) {
        existing.total_switches = isCumulative ? Math.max(existing.total_switches, row.total_switches) : row.total_switches;
        existing.total_apps = isCumulative ? Math.max(existing.total_apps, row.total_apps) : row.total_apps;
      } else {
        summaryRowsByKey.set(key, { ...row });
      }
    }
    const mergedSummaryRows = Array.from(summaryRowsByKey.values());

    if (isCumulative && mergedAppRows.length > 0) {
      const appDates = mergedAppRows.map((r) => r.date).sort();
      const minDate = appDates[0], maxDate = appDates[appDates.length - 1];
      const appQuery = supabase.from('screentime_daily_app_stats').select('date, app_name, total_time_seconds, session_count, first_seen_at, last_seen_at, last_active_at').eq('user_id', payload.user_id).eq('source', source).eq('device_id', deviceId).eq('platform', platform).gte('date', minDate);
      const { data: existingApps, error: existingAppsError } = await (appQuery as any).lte('date', maxDate);
      if (existingAppsError) {
        return new Response(JSON.stringify({ error: `Failed reading existing app rows for cumulative merge: ${existingAppsError.message}` }), { status: 500, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
      }
      const existingAppsByKey = new Map<string, any>();
      for (const existing of existingApps || []) existingAppsByKey.set(`${existing.date}|${existing.app_name}`, existing);
      for (const row of mergedAppRows) {
        const existing = existingAppsByKey.get(`${row.date}|${row.app_name}`);
        if (!existing) continue;
        row.total_time_seconds = Math.max(row.total_time_seconds, existing.total_time_seconds || 0);
        row.session_count = Math.max(row.session_count, existing.session_count || 0);
        row.first_seen_at = minIso(row.first_seen_at, existing.first_seen_at || null);
        row.last_seen_at = maxIso(row.last_seen_at, existing.last_seen_at || null);
        row.last_active_at = maxIso(row.last_active_at, existing.last_active_at || null);
      }
    }

    if (isCumulative && mergedWebsiteRows.length > 0) {
      const websiteDates = mergedWebsiteRows.map((r) => r.date).sort();
      const minDate = websiteDates[0], maxDate = websiteDates[websiteDates.length - 1];
      const websiteQuery = supabase.from('screentime_daily_website_stats').select('date, domain, total_time_seconds, session_count, first_seen_at, last_seen_at, last_active_at').eq('user_id', payload.user_id).eq('source', source).eq('device_id', deviceId).eq('platform', platform).gte('date', minDate);
      const { data: existingWebsites, error: existingWebsitesError } = await (websiteQuery as any).lte('date', maxDate);
      if (existingWebsitesError) {
        return new Response(JSON.stringify({ error: `Failed reading existing website rows for cumulative merge: ${existingWebsitesError.message}` }), { status: 500, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
      }
      const existingWebsitesByKey = new Map<string, any>();
      for (const existing of existingWebsites || []) existingWebsitesByKey.set(`${existing.date}|${existing.domain}`, existing);
      for (const row of mergedWebsiteRows) {
        const existing = existingWebsitesByKey.get(`${row.date}|${row.domain}`);
        if (!existing) continue;
        row.total_time_seconds = Math.max(row.total_time_seconds, existing.total_time_seconds || 0);
        row.session_count = Math.max(row.session_count, existing.session_count || 0);
        row.first_seen_at = minIso(row.first_seen_at, existing.first_seen_at || null);
        row.last_seen_at = maxIso(row.last_seen_at, existing.last_seen_at || null);
        row.last_active_at = maxIso(row.last_active_at, existing.last_active_at || null);
      }
    }

    if (isCumulative && mergedSummaryRows.length > 0) {
      const summaryDates = mergedSummaryRows.map((r) => r.date).sort();
      const minDate = summaryDates[0], maxDate = summaryDates[summaryDates.length - 1];
      const summaryQuery = supabase.from('screentime_daily_summary').select('date, total_switches, total_apps').eq('user_id', payload.user_id).eq('source', source).eq('device_id', deviceId).eq('platform', platform).gte('date', minDate);
      const { data: existingSummaries, error: existingSummariesError } = await (summaryQuery as any).lte('date', maxDate);
      if (existingSummariesError) {
        return new Response(JSON.stringify({ error: `Failed reading existing summary rows for cumulative merge: ${existingSummariesError.message}` }), { status: 500, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
      }
      const existingSummaryByDate = new Map<string, any>();
      for (const existing of existingSummaries || []) existingSummaryByDate.set(existing.date, existing);
      for (const row of mergedSummaryRows) {
        const existing = existingSummaryByDate.get(row.date);
        if (!existing) continue;
        row.total_switches = Math.max(row.total_switches, existing.total_switches || 0);
        row.total_apps = Math.max(row.total_apps, existing.total_apps || 0);
      }
    }

    let appInserted = 0, websiteInserted = 0, summaryInserted = 0;
    const appErrors: string[] = [], websiteErrors: string[] = [], summaryErrors: string[] = [];

    if (mergedAppRows.length > 0) {
      const batchSize = 500;
      for (let i = 0; i < mergedAppRows.length; i += batchSize) {
        const batch = mergedAppRows.slice(i, i + batchSize);
        const { data, error } = await supabase.from('screentime_daily_app_stats').upsert(batch, { onConflict: 'user_id,date,source,device_id,platform,app_name' }).select() as { data: any[] | null; error: { message: string } | null };
        if (error) { appErrors.push(`Batch ${Math.floor(i / batchSize) + 1}: ${error.message}`); } else { appInserted += Array.isArray(data) ? data.length : 0; }
      }
    }
    if (mergedWebsiteRows.length > 0) {
      const batchSize = 500;
      for (let i = 0; i < mergedWebsiteRows.length; i += batchSize) {
        const batch = mergedWebsiteRows.slice(i, i + batchSize);
        const { data, error } = await supabase.from('screentime_daily_website_stats').upsert(batch, { onConflict: 'user_id,date,source,device_id,platform,domain' }).select() as { data: any[] | null; error: { message: string } | null };
        if (error) { websiteErrors.push(`Batch ${Math.floor(i / batchSize) + 1}: ${error.message}`); } else { websiteInserted += Array.isArray(data) ? data.length : 0; }
      }
    }
    if (mergedSummaryRows.length > 0) {
      const batchSize = 500;
      for (let i = 0; i < mergedSummaryRows.length; i += batchSize) {
        const batch = mergedSummaryRows.slice(i, i + batchSize);
        const { data, error } = await supabase.from('screentime_daily_summary').upsert(batch, { onConflict: 'user_id,date,source,device_id,platform' }).select() as { data: any[] | null; error: { message: string } | null };
        if (error) { summaryErrors.push(`Batch ${Math.floor(i / batchSize) + 1}: ${error.message}`); } else { summaryInserted += Array.isArray(data) ? data.length : 0; }
      }
    }

    const shouldVerify = debugEnabled || (payload.activity_summary && appInserted + websiteInserted + summaryInserted === 0);
    let verify: null | { app_rows_found: number; website_rows_found: number; summary_rows_found: number; activity_date_used?: string; parsed_activity_items?: number; keys: { user_id: string; date: string | null; source: string; device_id: string; platform: string }; function_version: string } = null;

    if (shouldVerify) {
      const checkDate = activitySummaryDateUsed || (payload.date ? parseDateToDateString(payload.date) : null);
      const baseKeys = { user_id: payload.user_id, date: checkDate, source, device_id: deviceId, platform };
      if (checkDate) {
        const [{ count: appCount }, { count: webCount }, { count: sumCount }] = await Promise.all([
          (supabase.from('screentime_daily_app_stats') as any).select('id', { count: 'exact', head: true } as any).eq('user_id', payload.user_id).eq('date', checkDate).eq('source', source).eq('device_id', deviceId).eq('platform', platform),
          (supabase.from('screentime_daily_website_stats') as any).select('id', { count: 'exact', head: true } as any).eq('user_id', payload.user_id).eq('date', checkDate).eq('source', source).eq('device_id', deviceId).eq('platform', platform),
          (supabase.from('screentime_daily_summary') as any).select('id', { count: 'exact', head: true } as any).eq('user_id', payload.user_id).eq('date', checkDate).eq('source', source).eq('device_id', deviceId).eq('platform', platform),
        ]);
        verify = { app_rows_found: appCount ?? 0, website_rows_found: webCount ?? 0, summary_rows_found: sumCount ?? 0, activity_date_used: activitySummaryDateUsed ?? undefined, parsed_activity_items: payload.activity_summary ? parsedActivityItemsCount : undefined, keys: baseKeys, function_version: FUNCTION_VERSION };
      } else {
        verify = { app_rows_found: 0, website_rows_found: 0, summary_rows_found: 0, activity_date_used: activitySummaryDateUsed ?? undefined, parsed_activity_items: payload.activity_summary ? parsedActivityItemsCount : undefined, keys: baseKeys, function_version: FUNCTION_VERSION };
      }
    }

    if (appErrors.length > 0 || websiteErrors.length > 0 || summaryErrors.length > 0) {
      return new Response(JSON.stringify({
        success: true, warning: 'Some batches failed',
        inserted: { apps: appInserted, websites: websiteInserted, summaries: summaryInserted },
        total: { apps: mergedAppRows.length, websites: mergedWebsiteRows.length, summaries: mergedSummaryRows.length },
        upload_date: upload_date ?? undefined, upload_time: upload_time ?? undefined, uploaded_at: uploaded_at ?? undefined, received_at, verify: verify ?? undefined,
        errors: { apps: appErrors, websites: websiteErrors, summaries: summaryErrors },
      }), { status: 207, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
    }

    return new Response(JSON.stringify({
      success: true,
      inserted: { apps: appInserted, websites: websiteInserted, summaries: summaryInserted },
      total: { apps: mergedAppRows.length, websites: mergedWebsiteRows.length, summaries: mergedSummaryRows.length },
      upload_date: upload_date ?? undefined, upload_time: upload_time ?? undefined, uploaded_at: uploaded_at ?? undefined, received_at, verify: verify ?? undefined,
    }), { headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
  } catch (err) {
    console.error('Error:', err);
    return new Response(JSON.stringify({ error: String(err), stack: err instanceof Error ? err.stack : undefined }), { status: 500, headers: { ...corsHeaders, 'Content-Type': 'application/json' } });
  }
});
```

**Tables:** `screentime_daily_app_stats` (upsert key: `user_id, date, source, device_id, platform, app_name`), `screentime_daily_website_stats` (upsert key: `user_id, date, source, device_id, platform, domain`), `screentime_daily_summary` (upsert key: `user_id, date, source, device_id, platform`). Chronos sends `data.Years` + `daily_summaries`; the function also supports flat `snapshots`, root-level `apps`/`websites`/`items`, and `activity_summary` for other clients (e.g. iOS Shortcuts).
