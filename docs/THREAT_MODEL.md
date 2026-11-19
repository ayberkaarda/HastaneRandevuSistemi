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
- The demo accounts created in a new database are fictional and use synthetic national ids that only pass the
  checksum; they must be removed or have their passwords changed before real use.

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
| National ids shown on screen | The session keeps only a masked form (`100******50`); the full number is not kept after sign-in | Mitigated |
| Personal data written to log files | Error entries contain only exception types, HResult codes and method names, without messages or file paths; refused operations log only the exception type; audit entries are fixed codes with a role (`Audit auth.sign_in.failed patient`), with no id, name, national id, password or appointment detail. A test runs a full session and checks the file | Mitigated |
| Lock state disclosed through the log file | A sign-in refused because the account is locked writes the same `auth.sign_in.failed` entry as an unknown account or a wrong password; there is no separate lock code. A test compares the entries of the three cases | Mitigated |
| Personal data in error dialogs | Users see fixed Turkish messages; raw exception text is never shown | Mitigated |
| Log files read by other local users | Logs are under the user's own local application data folder | Mitigated by OS permissions |
| Log files growing without bound | Daily rolling files, the last 30 are kept | Mitigated |
| SQL injection | All access goes through EF Core with parameters; the only raw statements are constant schema and lock statements of the legacy upgrade | Mitigated |
| Connection string leaked through the repository | `appsettings.json` is git-ignored; user secrets and the `HASTANE_CONNECTION` environment variable are supported; only `appsettings.example.json` is committed | Mitigated |
| Interception of the database connection | `TrustServerCertificate=True` in the example is for a local server only; for a remote server use `Encrypt=True` with a trusted certificate | Documented |
| Direct table access with the application's SQL login | Grant that login only the table permissions it needs, prefer Windows authentication, and do not give the connection string to patients | Documented |
| Database backups exposing personal data and legacy plain text passwords | `*.bak`, `*.mdf` and `*.ldf` are git-ignored and the old tracked backup was removed. Backups taken before the upgrade, or while legacy accounts remain, contain plain text passwords: encrypt them (SQL Server backup encryption or an encrypted volume), restrict access and delete old ones | Documented |
| Legacy upgrade altering or exposing existing rows | The upgrade runs in one transaction under a checked `sp_getapplock` lock and refuses before any change when rows break the new rules, reporting counts only | Mitigated |
| Fake or malformed national ids | Registration checks the 11 digit format and the checksum; sign-in checks the format only | Mitigated |
| Data kept longer than needed | No retention or deletion feature; cancelled appointments stay as history. The operator deletes data in the database when required | Accepted (out of scope for this version) |

## Residual risks

- Accounts migrated from the original application keep their plain text password until their next successful
  sign-in. An operator can force the change by resetting those passwords; the format column shows which
  accounts are affected (`SifreBicimi = 1`).
- Doctors sign in by full name, which is easy to guess; the lockout and the password are the only protection.
- There is no password change or reset screen yet.

## Out of scope

- Network services, web access, multi-factor authentication, encryption of data at rest inside the application,
  automated backups and retention policies, and a per-user audit trail of who read which record.
