# Threat Model

A short threat model for the Hastane Randevu desktop application. The application stores Turkish national
identity numbers (T.C. Kimlik No), names, passwords and appointments with doctors of a named branch. Under the
Turkish Personal Data Protection Law (KVKK, Law No. 6698) the national id and the names are personal data, and
an appointment with a cardiology or orthopaedics doctor reveals something about a person's health, which is a
special category of personal data. This document lists technical measures only; it is not a legal assessment.

## Scope and assets

- **Application:** a Windows Forms client with patient and doctor sign-in that talks directly to SQL Server.
- **Assets:**
  - National ids, full names and appointments (doctor, branch, date and time) of patients.
  - Passwords of patients and doctors, and their stored hashes.
  - Sign-in counters and lockout times.
  - The connection string (server name, database, and possibly SQL credentials).
  - Log files under `%LOCALAPPDATA%\HastaneRandevu\logs`.
  - Database backups made by the operator (`.bak` files).
- **Trust boundaries:** the Windows user session that runs the application; the network link to SQL Server;
  the local file system (settings, logs, backups); the boundary between a signed-in account and the data of
  other accounts.

## Assumptions

- The application runs on trusted workstations; patients and doctors sign in on them with their own accounts.
- Every instance connects with the same SQL Server login, so the database itself does not tell patients apart:
  ownership is enforced by the application, and anyone holding the connection string can read every table.
- The operator is the data controller and is responsible for consent, retention, deletion requests and the
  security of the server, its logins and its backups.
- A new database contains no account by default. Demo accounts (fictional, synthetic national ids that only
  pass the checksum, passwords published in the README) are created only when demo mode is switched on
  explicitly with `HASTANE_DEMO=1` or `Seed:Demo=true`; demo mode is never used with real data.

## Threats and mitigations

| Threat | Mitigation | Status |
|---|---|---|
| Passwords read from the database or a backup | PBKDF2-HMAC-SHA256 with 600,000 iterations and a 16 byte random salt per password; the hash is compared in constant time | Mitigated |
| Plain text passwords of the original application | The legacy upgrade marks them `LegacyPlainText` in their own format column; the first successful sign-in replaces each with a PBKDF2 hash in the same conditional update. Accounts that never sign in again keep plain text until then | Partly mitigated (see below) |
| A stored value read in the wrong format (a hash accepted as plain text or the reverse) | The format comes from the account's format column, never from the shape of the value; tests cover a hash-shaped legacy password and plain text in the hash format | Mitigated |
| Weaker hashes staying in place | A hash with fewer iterations than configured is replaced on the next successful sign-in | Mitigated |
| Online password guessing | 5 failed attempts lock the account for 15 minutes; the counter is increased in one atomic `UPDATE`, attempts during a lock are neither counted nor extend it, and a success is written only while the account is still unlocked, so concurrent attempts cannot clear a fresh lock | Mitigated |
| Account enumeration through sign-in answers or timing | Unknown accounts, wrong passwords and locked accounts get the same message, and each costs one key derivation | Mitigated |
| Lockout used to block a known patient or doctor (denial of service) | The lock ends by itself after 15 minutes; no permanent lock exists | Accepted |
| IDOR: a signed-in account reading or cancelling other accounts' appointments by id | Every call takes the session issued by `AuthService` (constructor is internal); lists are queried by the session's own id; cancellation checks the owner per role and repeats the owner, status and time conditions in the `UPDATE`; missing and foreign appointments get the same answer | Mitigated |
| A doctor booking appointments, or a patient using a doctor's id | Booking and free slot queries require the patient role; ownership compares the column of the session's role, so equal numeric ids of a patient and a doctor do not match | Mitigated |
| Double booking of a slot | Unique filtered index on doctor and start time for active appointments; a lost race is reported as "slot taken" | Mitigated |
| A doctor changing another doctor's schedule (IDOR) | The schedule service has no doctor id parameter: every read and write uses the session's own account and requires the doctor role; tests on SQLite check that other doctors' rows stay unchanged | Mitigated |
| Rescheduling someone else's appointment, or losing an appointment in a race | One conditional `UPDATE` repeats owner, status, start and cut-off; the unique filtered index decides races for the new slot; there is no cancel-and-rebook step, so a lost race leaves the appointment where it was (SQL Server race tests) | Mitigated |
| Formula injection through exported CSV | Every cell starting with `=`, `+`, `-`, `@`, tab or carriage return gets a leading apostrophe; separators, quotes and line breaks are quoted | Mitigated |
| Script injection through the exported agenda | The page has no scripts, a `default-src 'none'` content security policy and every value HTML encoded | Mitigated |
| National ids in exported files | Exports contain only the masked form; their content and paths are not logged. The files are saved where the user chooses and are then the user's responsibility | Mitigated (files outside the application) |
| National ids shown on screen | The session keeps only a masked form (`100******50`); the full number is not kept after sign-in | Mitigated |
| Personal data written to log files | Error entries contain only exception types, HResult codes and method names, without messages or file paths; refused operations log only the exception type; audit entries are fixed codes with a role (`Audit auth.sign_in.failed patient`), with no id, name, national id, password or appointment detail. A test runs a full session and checks the file | Mitigated |
| Lock state disclosed through the log file | A sign-in refused because the account is locked writes the same `auth.sign_in.failed` entry as an unknown account or a wrong password; there is no separate lock code. A test compares the entries of the three cases | Mitigated |
| Personal data in error dialogs | Users see fixed Turkish messages; raw exception text is never shown | Mitigated |
| Log files read by other local users | Logs are under the user's own local application data folder | Mitigated by OS permissions |
| Log files read by another application account on the same Windows account | Windows file permissions separate Windows accounts, not the patient and doctor accounts of the application: everyone who signs in on the same Windows account (for example a shared front-desk workstation) can read the same log files and knows when they made their own attempts. The files therefore hold only anonymous codes, roles and exception types, and every failed sign-in (unknown account, wrong password, locked account) writes the same code, so the file reveals neither who signed in nor which account exists or is locked | Mitigated (counts and times of events remain visible) |
| Log files growing without bound | Daily rolling files; when a file is opened, files dated more than 30 days ago and any beyond the newest 30 are deleted. Tests create dated files and check which remain | Mitigated |
| SQL injection | All access goes through EF Core with parameters; the only raw statements are constant schema and lock statements of the legacy upgrade | Mitigated |
| Connection string leaked through the repository | `appsettings.json` is git-ignored; user secrets and the `HASTANE_CONNECTION` environment variable are supported; only `appsettings.example.json` is committed | Mitigated |
| Interception of the database connection | `TrustServerCertificate=True` in the example is for a local server only; for a remote server use `Encrypt=True` with a trusted certificate | Documented |
| Direct table access with the application's SQL login | Grant that login only the table permissions it needs, prefer Windows authentication, and do not give the connection string to patients | Documented |
| Database backups exposing personal data and legacy plain text passwords | `*.bak`, `*.mdf` and `*.ldf` are git-ignored and the old tracked backup was removed. Backups taken before the upgrade, or while legacy accounts remain, contain plain text passwords: encrypt them (SQL Server backup encryption or an encrypted volume), restrict access and delete old ones | Documented |
| Legacy upgrade altering or exposing existing rows | The upgrade runs in one transaction under a checked `sp_getapplock` lock and refuses before any change when rows break the new rules, reporting counts only | Mitigated |
| Accounts with published passwords in a default installation | The migrations insert no rows; the demo accounts are added by a separate seeder only in demo mode, which is off unless `HASTANE_DEMO=1` or `Seed:Demo=true` is set. Tests check that a default start on SQLite and on SQL Server creates no account and that the demo passwords are refused | Mitigated (see residual risks for older databases) |
| Fake or malformed national ids | Registration checks the 11 digit format and the checksum; sign-in checks the format only | Mitigated |
| Data kept longer than needed | No retention or deletion feature; cancelled appointments stay as history. The operator deletes data in the database when required | Accepted (out of scope for this version) |

## Residual risks

- Accounts migrated from the original application keep their plain text password until their next successful
  sign-in. An operator can force the change by resetting those passwords; the format column shows which
  accounts are affected (`SifreBicimi = 1`).
- Databases created by an earlier version of this application received the demo accounts through the first
  migration. They are not removed automatically, because real appointments may refer to them; the operator
  deletes them or changes their passwords.
- There is no screen for adding doctors; the operator inserts doctor accounts into the database.
- Doctors sign in by full name, which is easy to guess; the lockout and the password are the only protection.
- There is no password change or reset screen yet.
- On a Windows account shared by several application users, each of them can read when sign-ins, bookings and
  cancellations happened from the log file, though not by whom. Give each person their own Windows account if
  even that is too much.

## Out of scope

- Network services, web access, multi-factor authentication, encryption of data at rest inside the application,
  automated backups and retention policies, and a per-user audit trail of who read which record.
