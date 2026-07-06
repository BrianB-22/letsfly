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

## 1b. Update the minimum version gate (if old clients should be blocked)

Edit `supabase/functions/verify-client/index.ts` and bump `MIN_VERSION`:

```ts
const MIN_VERSION = [0, 2]; // major.minor — patch is ignored
```

Set this to the new release's major.minor. Any client below this version will be
denied at login with a "please download the latest version" message.

Deploy the updated edge function:

```powershell
supabase functions deploy verify-client
```

Or push to GitHub — the function deploys automatically if CI is wired up.

## 2. Publish the single-file exe

```powershell
cd CheckRide\CheckRide
dotnet publish -p:PublishProfile=SingleFileRelease
```

Output: `bin\Publish\CheckRide.exe` — self-contained win-x64, no .NET install required.

## 3. Generate the SHA-256 hash

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

## 4. Smoke test

- Run `CheckRide.exe` on a machine (or clean folder) **without** the repo present
- Verify: login form shows the banner image, sign-in works, sounds play,
  flight list loads, and `%LOCALAPPDATA%\SimLetsFly\CheckRide\assets\{version}\`
  was created with `sounds\`, `images\`, `refdata\`
- SmartScreen will warn on an unsigned exe — "More info → Run anyway" is expected

## 5. Release

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

## Verifier note (what testers see)

SmartScreen flags unsigned downloads until reputation builds. The hash check is the
interim integrity story; Azure Trusted Signing (~$10/mo) is the plan before any
public/paid launch — it drops into step 2 as a post-publish signing command.
