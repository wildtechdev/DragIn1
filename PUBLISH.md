# Publishing checklist

Internal notes. Delete this file before making the repo public, or keep it, it
is harmless either way.

## First push

```
cd DragIn1-repo
git init
git add .
git commit -m "DragIn1 1.0.0"
git branch -M main
git remote add origin https://github.com/<your-user>/DragIn1.git
git push -u origin main
```

Then in the repo settings on GitHub:

- **Description:** `Drag attachments out of New Outlook again. Free, open source, runs entirely on your PC.`
- **Website:** `https://wildtechdev.com`
- **Topics:** `windows`, `outlook`, `drag-and-drop`, `chromium`, `csharp`, `win32`, `productivity`
- Turn **off** Wikis and Projects. Leave Issues on.

## Cutting a release

```
git tag v1.0.0
git push origin v1.0.0
```

The GitHub Action builds `DragIn1-Setup.exe` on a Windows runner using the C#
compiler that ships with Windows, computes the SHA256, sanity-checks that the
payload actually got embedded, and attaches both to the release.

Paste the SHA256 into the release notes so people can verify their download.

## Do not publish

Keep these out of the repo. They are research notes about a commercial product
and they do not belong in public:

- `Recon-MagicDragin.ps1` and any `Recon-*.txt`
- `FINDINGS.md`
- `DragProbe*.ps1` and the probe logs
- `captured-*/` folders (they contain real attachments)

`docs/how-it-works.md` is the public version of that material. It keeps the
technical substance and drops the competitor teardown.

## Before you tell anyone about it

- [ ] Copy `DragIn1-Setup.exe` alone into an empty folder, run it, confirm it
      installs and opens. This proves no hidden dependency on the build folder.
- [ ] Test on a machine that has never had it installed.
- [ ] Confirm uninstall from Settings → Apps leaves nothing behind.
- [ ] Check the README renders correctly on GitHub, especially the logo at the top.

## SignPath Foundation (free code signing)

SignPath Foundation gives OSI-licensed open source projects a free code signing
certificate. That would remove the SmartScreen problem properly, because
reputation attaches to the certificate and carries across releases, instead of
resetting on every new file hash the way it does for unsigned builds.

**Apply only after v1.0.0 is published.** Their terms say "the project must
already be released in the form that should be signed," so a pre-release
application gets rejected on sequencing alone.

Checklist before applying:

- [ ] v1.0.0 tagged and released with `DragIn1-Setup.exe` attached
- [ ] MFA enabled on every GitHub account with access to this repo
- [ ] `CODE_SIGNING_POLICY.md` linked from the README and from the product page
      on wildtechdev.com (they require the policy be published on the project
      homepage)
- [ ] Some visible maintenance history: a couple of commits, a closed issue, a
      second release. A repo pushed yesterday reads as abandoned-on-arrival
- [ ] Privacy disclosure and uninstall instructions visible on the download page
      (both already in the README)

Apply at https://signpath.org/apply

If accepted, add their signing step to `.github/workflows/release.yml` between
the compile and release steps, and update `CODE_SIGNING_POLICY.md` plus the
README to state that releases are signed.
