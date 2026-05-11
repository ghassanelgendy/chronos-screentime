# Product Requirements Document (PRD): Core Functions

- Product: Chronos Screen Time Tracker
- Platform: Windows Desktop (WPF, .NET 8)
- Version Scope: Core Functions v1
- Date: 2026-03-15
- Status: Draft

## 1. Purpose
Chronos helps users understand and improve digital habits by automatically tracking active app and web usage, presenting clear summaries, and preserving data reliably over time.

This PRD defines the minimum high-value feature set that must remain stable and usable as the foundation for future roadmap work.

## 2. Goals
1. Accurately track active foreground usage in near real time.
2. Make usage data understandable at a glance and explorable in detail.
3. Ensure data is durable locally and optionally backed up to Supabase.
4. Give users direct control over tracking state and data lifecycle.
5. Keep the app lightweight, responsive, and safe to run continuously.

## 3. Non-Goals (For Core Scope)
1. Cross-device data merge conflict resolution.
2. Team/shared analytics dashboards.
3. Full productivity coaching, scoring, and blocking automation.
4. Third-party integrations outside optional Supabase upload.

## 4. Primary Users
1. Productivity-focused individual users on Windows.
2. Developers and knowledge workers who want app/site time breakdowns.
3. Users who want local-first storage with optional cloud backup.

## 5. Core User Jobs To Be Done
1. "Track my app usage automatically while I work."
2. "See where my time went today and this week."
3. "Pause tracking when needed and resume quickly."
4. "Delete/reset data when I want a clean slate."
5. "Optionally sync my data to Supabase without friction."

## 6. Core Functional Requirements

### F1. Real-Time Foreground Tracking
- The system shall detect and record the active foreground application.
- The system shall accumulate per-app time with second-level granularity.
- The system shall count app switches (session transitions).
- The system shall support start/stop tracking from main UI controls.
- The system shall ignore very short sessions when the setting is enabled.

Acceptance Criteria:
1. With tracking enabled for 10 minutes across at least 3 apps, total recorded time is within +/- 5% of wall-clock time.
2. App switch count increases when foreground app changes.
3. Stopping tracking halts time increments within 1 second.

### F2. Website Usage Tracking
- The system shall capture website domain usage through browser tracking.
- The system shall aggregate per-domain time and sessions.
- The system shall display website metrics in a dedicated web browsing view.

Acceptance Criteria:
1. Visiting at least 3 domains in a supported browser creates 3 corresponding domain records.
2. Domain time increases only while the domain is active.
3. Web stats view refreshes and reflects latest persisted values.

### F3. Data Modeling and Persistence
- The system shall store app and website usage in structured daily time buckets.
- The system shall persist data locally to JSON under user AppData.
- The system shall restore data on startup without manual action.
- The system shall preserve data integrity across normal app restarts.

Acceptance Criteria:
1. After restart, previously tracked totals are restored with no required re-import.
2. Local data file is updated during active use and exists in configured AppData location.
3. Corrupt/missing file handling does not crash startup; app starts with safe defaults.

### F4. Analytics and Summaries
- The system shall show key summary metrics: total apps, total screen time, total switches.
- The system shall provide period views (at minimum: Today, Yesterday, This Week, Last Week, This Month).
- The system shall display ranked app lists with total time and session counts.
- The system shall support category-based visualization and filtering where configured.

Acceptance Criteria:
1. Summary numbers update within the UI refresh interval while tracking is active.
2. Switching period updates app list and totals to the selected period.
3. Category filters apply consistently across list and chart views.

### F5. Tracking and Window Controls
- The system shall support Always On Top and Hide Title Bar preferences.
- The system shall support system tray behavior, including minimize-to-tray.
- The system shall expose explicit tracking state (running or stopped).

Acceptance Criteria:
1. Enabling Always On Top keeps main window above normal windows.
2. When minimize-to-tray is enabled, close/minimize action moves app to tray and tracking continues.
3. Tracking state text and control label always match actual tracker state.

### F6. Data Management Actions
- The system shall allow reset of all tracked data.
- The system shall allow reset/clear for a specific app entry.
- The system shall require confirmation before destructive actions.

Acceptance Criteria:
1. Reset All clears totals and list data immediately after confirmation.
2. Per-app reset removes or zeroes targeted app data only.
3. Destructive actions are cancelled when user declines confirmation.

### F7. Export (Core Utility)
- The system shall support exporting tracked data via Export Service.
- Export output shall be user-accessible in a chosen location.

Acceptance Criteria:
1. Export action produces a file in selected destination.
2. Exported output opens and contains non-empty tracked records when data exists.
3. Export action reports clear error when destination is invalid or unavailable.

### F8. Optional Supabase Upload Backup
- The system shall provide opt-in Supabase sync settings (URL, anon key, user ID, interval).
- The system shall upload data to Edge Function endpoint on configured interval.
- The system shall not block local tracking if upload fails.
- The system shall provide a test connection workflow with result feedback.

Acceptance Criteria:
1. With valid settings and sync enabled, periodic upload attempts occur at configured interval.
2. Failed upload surfaces an error state/message but local tracking continues.
3. Test Connection validates required fields and shows pass/fail result.

## 7. Non-Functional Requirements
1. Performance: UI remains responsive while tracking and refreshing once per second.
2. Reliability: No data loss during normal shutdown and restart sequences.
3. Resource Usage: Background monitoring should remain suitable for all-day execution on typical Windows devices.
4. Privacy/Security: Local-first storage by default; Supabase upload only when explicitly enabled.
5. Maintainability: Core services remain modular (tracking, settings, export, charts, sync).

## 8. UX Requirements
1. Time-to-value: User sees live tracking and summary metrics immediately on first launch.
2. Clarity: Start/Stop and Reset actions are easy to discover.
3. Feedback: Long operations or external operations (export/upload) provide clear result messages.
4. Preferences: Core settings can be changed without app restart unless technically required.

## 9. Telemetry and Success Metrics
1. Core Activation: % of users who keep tracking enabled after first session.
2. Retention Proxy: Number of active days with at least one tracked session per week.
3. Reliability: Upload success ratio for users with Supabase enabled.
4. Data Confidence: Ratio of days where tracked total is within expected awake-hours bounds.
5. Stability: Crash-free sessions rate.

## 10. Risks and Mitigations
1. Browser tracking variability across browsers.
- Mitigation: Graceful fallback to app-only tracking and clear support documentation.

2. Clock drift and edge-case session boundaries.
- Mitigation: Centralized time normalization and periodic data sanity checks.

3. Upload endpoint contract drift.
- Mitigation: Versioned payload contract and connection test before enabling sync.

4. User trust/privacy concerns.
- Mitigation: Local-first default, explicit opt-in for cloud upload, and transparent settings.

## 11. Release Definition of Done (Core)
1. All F1-F8 acceptance criteria pass on Windows 10 and Windows 11.
2. Manual QA covers startup, tracking, period switching, reset actions, export, and Supabase test/upload.
3. No blocker or critical defects in core flows.
4. Core documentation is present and linked from project docs/readme.

## 12. Future Extensions (Out of Core PRD)
1. Idle detection and smarter focus attribution.
2. Goals, scoring, and distraction interventions.
3. Deeper charting and report generation.
4. Cloud download/merge and multi-device reconciliation.
5. Start with Windows and advanced startup behaviors.
