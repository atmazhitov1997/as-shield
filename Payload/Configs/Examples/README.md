# Example configuration files

- `AppLocker-DefaultRules-AuditOnly.xml` - the standard AppLocker default rules in **audit-only** mode. Nothing is blocked; events are written to the AppLocker event log so you can review the impact first.
- GPO backups are **not** shipped. To use an `lgpo` step, create a backup on a reference PC (`LGPO.exe /b <folder>`), put the backup folder here (for example `YourGpoBackup-Win11`), point the profile's `gpo` parameter at it and enable the step.
