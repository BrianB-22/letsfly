# CheckRide — Release Publish Workflow (Windows)

Run these steps on the Windows machine from the repo root (`letsfly\CheckRide`).

## 1. Bump the version

Edit `CheckRide\CheckRide.csproj` — update both:

```xml
<AssemblyVersion>0.2.0.0</AssemblyVersion>
<FileVersion>0.2.0.0</FileVersion>
```

The version drives two things: the `client_version` sent to `verify-client` at login,
and the extraction folder for embedded assets
(`%LOCALAPPDATA%\SimLetsFly\CheckRide\assets\{version}` — a new version re-extracts).

If `Models/CheckRideReport.cs` → `ScoringVersionConst` changed since the last release,
bump that too — it's what lets you tell which scoring logic actually produced a given
`checkride_results` row.

## 2. Commit and tag before publishing

**Do this before running `dotnet publish`, not after.** A published exe that doesn't
correspond to an exact commit can't be reproduced or audited later — this has already
happened once (v0.2.5.0 shipped from an uncommitted version bump and has no matching
commit in this repo).

```powershell
git add CheckRide\CheckRide.csproj
git commit -m "Bump version to 0.2.x.0"
git tag v0.2.x
git push origin main --tags
```

Only `dotnet publish` from this exact tagged commit — no further edits, staged or not,
between tagging and publishing.

## 3. Publish the single-file exe

```powershell
cd CheckRide\CheckRide
dotnet publish -p:PublishProfile=SingleFileRelease
```

Output: `bin\Publish\CheckRide.exe` — self-contained win-x64, no .NET install required.

## 4. Generate the SHA-256 hash

Until the exe is code-signed (see security-review.md L-6), publish a hash with every
release so testers can verify their download:

```powershell
Get-FileHash bin\Publish\CheckRide.exe -Algorithm SHA256 | Format-List
```

Or to write it to a file that ships alongside the exe:

```powershell
(Get-FileHash bin\Publish\CheckRide.exe -Algorithm SHA256).Hash.ToLower() |
  Out-File -Encoding ascii bin\Publish\CheckRide.exe.sha256
```

## 5. Smoke test

- Run `CheckRide.exe` on a machine (or clean folder) **without** the repo present
- Verify: login form shows the banner image, sign-in works, sounds play,
  flight list loads, and `%LOCALAPPDATA%\SimLetsFly\CheckRide\assets\{version}\`
  was created with `sounds\`, `images\`, `refdata\`
- SmartScreen will warn on an unsigned exe — "More info → Run anyway" is expected

## 6. Release

- Attach `CheckRide.exe` and `CheckRide.exe.sha256` to the GitHub release
- Put the hash in the release notes too (testers rarely download the .sha256 file):

  ```
  SHA-256: <paste hash here>
  ```

- Tell beta testers to verify with:

  ```powershell
  Get-FileHash CheckRide.exe -Algorithm SHA256
  ```

  and compare against the hash in the release notes.

- **Update the version gate** in the Supabase SQL Editor to block older clients:

  ```sql
  UPDATE app_config SET min_client_version = '0.2.7' WHERE id = 1;
  ```

  Replace `0.2.7` with this release's full major.minor.patch — the
  `verify-client` edge function compares all three segments (fixed 2026-08-10;
  it used to silently ignore the patch number, so e.g. `0.2` or `0.2.6` would
  both let a `0.2.6.0` client through even after bumping to `0.2.7`). Do this
  after the new exe is published so users aren't blocked before they can
  download it, and redeploy the edge function first if you've changed it:

  ```powershell
  supabase functions deploy verify-client
  ```

## Verifier note (what testers see)

SmartScreen flags unsigned downloads until reputation builds. The hash check is the
interim integrity story; Azure Trusted Signing (~$10/mo) is the plan before any
public/paid launch — it drops into step 3 as a post-publish signing command.
