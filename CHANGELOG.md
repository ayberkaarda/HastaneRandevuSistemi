# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Rescheduling: a patient moves their own appointment to a free slot of the same doctor in one conditional update
  (no cancel and rebook), until `Appointments:RescheduleCutoffHours` before the start (default 2, validated at
  startup). Races for the new slot between moves and bookings are tested on SQL Server.
- Doctor working schedule: each doctor sets their own working days and hours and leave days (tables
  `DoktorCalismaSaatleri` and `DoktorIzinGunleri`, migration `AddDoctorSchedules`). Free slots and bookings follow it; a
  doctor without a schedule keeps the weekday 09:00-17:00 rule. Only the doctor can change their own schedule; existing
  appointments are kept and those on a leave day or outside the hours are listed as warnings.
- Daily rolling file logs with Serilog under `%LOCALAPPDATA%\HastaneRandevu\logs` (files older than
  30 days are deleted, at most 30 files are kept; tests check this with dated files).
  National ids, names, passwords, appointment details and exception messages are never logged.
- Anonymous audit entries for sign-in, password upgrade, registration, booking, cancellation and refused
  access: a fixed code and a role only, for example `Audit auth.sign_in.failed patient`.
- Central FluentValidation validators for registration, patient and doctor sign-in, booking and cancellation.
- Validator boundary tests, Moq tests for the lockout end, the hash upgrade, the 90 day window, the cancellation
  cut-off and ownership, and SQLite tests for the same rules with the real repositories.
- A test that runs a full session and checks the log file for personal data.
- A single dialog helper that the UI tests replace, so no test opens a window; the UI tests run serially.
- Release job in CI: on `v*` tags a win-x64 build is published and uploaded as a zip artifact.
- English and Turkish README with architecture diagram, threat model (`docs/THREAT_MODEL.md`) and this changelog.

### Changed

- A new database no longer receives accounts with published passwords: the migrations insert no rows, and the
  demo doctors and patients are created only in demo mode (`HASTANE_DEMO=1` or `Seed:Demo=true`, off by default).
  Doctor accounts are added by the operator, as there is no screen for that.
- The checks written by hand in the services moved to the validators; messages, trimming and the order of
  the checks are unchanged (registration password 8-64 characters, sign-in password up to 100).
- Errors that were written to the trace output are now written to the log file.

### Fixed

- After a refused sign-in the password field, and after a registration the name field, receive the focus once
  the form is enabled again.
- A sign-in refused because the account is locked is logged as `auth.sign_in.failed`, like every other failed
  sign-in, instead of with a separate lock code, so a local user who can read the log file cannot tell which
  account is locked.

### Removed

- The old screenshots, which showed government logos and stock photos.

## Layered .NET 10 foundation - 2026-11-19 (#1)

### Added

- Layered solution: `Domain`, `Business`, `Data` and `UI` projects under `src/`.
- Patient and doctor sign-in with PBKDF2 password hashes, a lockout after 5 failed attempts and national id checks.
- Appointment services that check ownership against the signed-in session: 30 minute weekday slots from 09:00 to
  17:00, at most 90 days ahead, cancellation only before the start.
- EF Core data access with entity configurations, migrations and fictional sample data; migrations are applied
  on startup.
- In-place upgrade of databases created by the original application, keeping their data and marking their
  plain text passwords, which are hashed on the next sign-in.
- Configuration from `appsettings.json`, user secrets and the `HASTANE_CONNECTION` environment variable;
  `appsettings.example.json` is committed instead of real settings.
- Original logo, background and application icon with their SVG sources.
- Test projects for the business rules, the services on SQLite, race conditions on SQL Server, the legacy
  upgrade and the UI guards.
- CI workflow that builds and tests on every push and pull request.

### Changed

- Converted to SDK-style projects targeting .NET 10 with nullable reference types.
- Forms call the Business services through dependency injection instead of running SQL directly.

### Fixed

- Repeated clicks on write buttons no longer start duplicate operations.
- Only the latest slot and appointment lists are shown when answers arrive out of order.
- A locked account gets the same answer as wrong credentials.

### Removed

- Tracked build output, IDE state, the database backup, government logos and licensed stock images.
