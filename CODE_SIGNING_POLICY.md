# Code signing policy

This document describes how DragIn1 releases are built, signed and verified.
It exists so that anyone downloading a signed DragIn1 binary can confirm what
that signature does and does not attest to.

## What is signed

Only `DragIn1-Setup.exe`, the single-file installer published on the
[Releases](../../releases) page of this repository. The application
(`DragIn1.exe`) is embedded inside it and is written to disk at install time.

No other channel is official. If you obtained DragIn1 anywhere other than this
repository's Releases page or wildtechdev.com, it is not a release we published.

## How releases are built

Every released binary is built by GitHub Actions from the public source in this
repository, using the workflow in
[`.github/workflows/release.yml`](.github/workflows/release.yml).

- The build runs on a GitHub-hosted `windows-latest` runner.
- It compiles with the C# compiler included in Windows (.NET Framework 4.x).
  There are no third-party dependencies, no package restore, and no network
  access during compilation.
- The build is triggered by pushing a version tag, and the workflow run is
  publicly visible.
- The published SHA256 is computed by that same workflow.

No binary is ever built on a developer workstation and uploaded by hand.

## Roles

DragIn1 is maintained by WildTech Development.

- **Author** — commits source changes and opens pull requests.
- **Reviewer** — reviews changes before they reach `main`.
- **Approver** — approves a signing request for a tagged release.

All maintainers with repository or signing access have multi-factor
authentication enabled on their GitHub and signing provider accounts.

## What a signature attests to

A valid signature means the binary was produced by the DragIn1 release workflow
from the source in this repository, and was approved for release by a
maintainer.

It is not a security audit, and it is not a warranty. The software is provided
under the MIT licence, as-is.

## Verifying a download

Each release publishes a SHA256 in `SHA256.txt`. To check a download:

```powershell
Get-FileHash DragIn1-Setup.exe -Algorithm SHA256
```

Compare the result with the value in the release. If a release is signed, you
can also right-click the file, choose **Properties → Digital Signatures**, and
confirm the publisher.

If a binary claiming to be DragIn1 fails either check, please do not run it, and
open an issue so we can look into it.

## What DragIn1 does to your system

Disclosed in full, because a signature is worth less without it.

**Data.** DragIn1 makes no network connections of any kind. It has no accounts,
no licence checks, no update checks and no analytics. Files you drop on it are
copied to `%LOCALAPPDATA%\DragIn1\` on your own machine and deleted after 7
days. Nothing is transmitted anywhere.

**System changes made at install.** All are per-user, none require
administrator rights:

| Change | When | Opt out |
|---|---|---|
| Files written to the chosen install folder | Always | Choose the folder during setup |
| `HKCU\...\Uninstall\DragIn1` registry key | Always | Removed on uninstall |
| Start Menu shortcut | If selected | Checkbox during setup |
| Desktop shortcut | If selected | Checkbox during setup |
| `HKCU\...\Run\DragIn1` startup entry | If selected | Checkbox during setup, or the right-click menu in the app |

DragIn1 installs no services, no drivers, no shell extensions, no scheduled
tasks, and no browser components. It does not load code into any other process.

**Uninstalling.** Settings → Apps → Installed apps → DragIn1 → Uninstall. You
are asked whether to keep or delete the files DragIn1 captured. Nothing is left
behind either way.

## Reporting a problem

Open an issue in this repository. For anything security sensitive, contact
WildTech Development at <https://wildtechdev.com> rather than filing publicly.

## Acknowledgement

If DragIn1 releases are signed by a certificate provided by the SignPath
Foundation, that will be noted here and on the releases page. Free code signing
for open source projects is provided by [SignPath.io](https://signpath.io),
certificate by [SignPath Foundation](https://signpath.org).
