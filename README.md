# AS Shield

AS Shield is a Windows hardening tool. It applies security profiles to a workstation through a WinForms front end and a PowerShell engine. A profile is data, not code: a `profile.json` lists ordered steps (registry values, services, AppLocker, local group policy, event log sizes and so on), and the engine runs each step with a typed handler.

> **Status:** 2.0, a public rewrite of an internal 1.x tool. Test every profile in a VM or on a spare machine before using it on real workstations.

## Features

- **Data-driven profiles.** `Payload/Profiles/<id>/profile.json`, with inheritance (`basedOn`, `removeSteps`), per-OS values (`{"10": ..., "11": ...}`), conditions (`when`), confirmations (`confirm`) and an `onError` policy.
- **15 step types:** `restorePoint`, `registry`, `services`, `appLocker`, `lgpo`, `lgpoText`, `eventLogs`, `folder`, `userProfilesPath`, `removeApps`, `removeWinXShell`, `shotPolicies`, `userRoles`, `selfExclusion`, `script`.
- **Plan mode.** Shows what a profile would do without changing anything.
- **Clear results.** Each step returns `OK`, `DONE`, `SKIP` or `FAIL`. The process exit code is `0` (no failures), `2` (finished, but some steps failed), `1` (stopped by a step with `onError: stop`) or `3` (profile not run: invalid profile or unsupported OS).
- **Local profiles.** Administrators can drop their own profiles into `%ProgramData%\ASShield\Profiles`. They are loaded only if that folder is writable by Administrators/SYSTEM alone.
- **Payload integrity.** The embedded payload is checked against a SHA-256 manifest before every run, and the extraction folder gets a High integrity label.
- **Tools:** reset policies to Windows defaults, USB history cleanup, software license report, sensitive-phrase document scan, PC information card, policy compliance audit, system integrity check, and evidence screenshots with `.docx` reports.

## Repository layout

```
App/            C# WinForms front end (.NET Framework 4.7.2)
Payload/        Everything packed into the exe as payload.zip
  RunAction.ps1   Entry point invoked by the GUI
  Engine/         Profile engine: Lib (loader, resolver) + Steps (handlers)
  Profiles/       Built-in example profiles
  Configs/        Shared configuration files (defaults, examples)
  Catalog/        menu.json (GUI menu) and shots.json (evidence screenshots)
  Schema/         JSON Schema for profile.json
build/          Build, validation, hashing and signing scripts
tests/          Engine mock test (runs on Linux/macOS with PowerShell 7)
```

## Building

Requirements: Windows, PowerShell 5.1 or 7, and the .NET SDK (to build a `net472` project).

```powershell
.\build\build.ps1                          # get-lgpo -> validate -> pack -> hash -> dotnet build
.\build\build.ps1 -CertThumbprint <thumb>  # also Authenticode-sign the scripts and the exe
```

`build\get-lgpo.ps1` downloads **LGPO.exe** from the Microsoft Download Center (Security Compliance Toolkit) and checks its SHA-256. LGPO is Microsoft software. It is not included in this repository.

## Writing a profile

```json
{
  "schemaVersion": 2,
  "id": "my-profile",
  "title": "My profile",
  "os": { "supported": ["10", "11"] },
  "basedOn": "workstation-baseline",
  "steps": [
    { "id": "no-autorun", "type": "registry",
      "params": { "values": [
        { "path": "HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer",
          "name": "NoDriveTypeAutoRun", "type": "DWord", "value": 255 } ] } }
  ]
}
```

Put it in `Payload/Profiles/my-profile/` (built in) or `%ProgramData%\ASShield\Profiles\my-profile\` (local). Then run `build\validate.ps1`. The full format is in `Payload/Schema/profile.schema.json`. The two example profiles are `workstation-baseline` and `kiosk-display`, which inherits from it.

**GPO backups.** The `lgpo` step imports a GPO backup folder. None are shipped, because a real backup reflects one organization's policy. Export your own with `LGPO.exe /b`, put it under `Payload/Configs/`, point the step's `gpo` parameter at it and set `"enabled": true`.

## Testing

```powershell
pwsh ./tests/run-mock.ps1   # runs the engine with mocked Windows APIs
pwsh ./build/validate.ps1   # validates every profile and the catalog files
```

## Security notes

- The app requires elevation and uses a normal UAC prompt. It does not bypass UAC.
- Scripts run with `-ExecutionPolicy Bypass` from the extracted payload. The payload's integrity check and folder ACLs exist for this reason. For production use, sign the build and use `AllSigned`.
- See [SECURITY.md](SECURITY.md) to report a vulnerability.

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in [NOTICE](NOTICE).
