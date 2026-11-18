# Security policy

AS Shield changes security settings on Windows machines, so a bug can weaken a machine instead of hardening it.

## Reporting a vulnerability

Do not open a public issue. Use GitHub's **Report a vulnerability** button (private vulnerability reporting) on this repository. Include the version, the profile you used and the steps to reproduce.

## Scope

These are in scope:
- ways to make the elevated engine run code that is not in the signed payload: payload tampering, local profile injection, path hijacking;
- steps that leave a machine less secure than they claim to;
- bypasses of the payload integrity check (PayloadGuard) or of the local profile folder ACL check.

Profiles you write yourself, and GPO backups you import, are your responsibility.
