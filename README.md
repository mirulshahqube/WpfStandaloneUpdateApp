# WPF Standalone Update App (Velopack + GitHub Releases)

A version of the update-checker app that runs as a plain installed `.exe`
(no Microsoft Store, no MSIX) with its own auto-update mechanism, using
**Velopack** for the update engine and **GitHub Releases** as the centralized
"what's the latest version" source.

## How the pieces fit together

| Concern | Store edition | This edition |
|---|---|---|
| Package format | MSIX | Plain installed exe (Velopack installer) |
| "Latest version" source | Microsoft Store / Partner Center | GitHub Releases on your repo |
| Update engine | `Windows.Services.Store` | Velopack `UpdateManager` |
| Major update | Blocking overlay, forced restart | Same UI pattern, same blocking overlay |
| Minor update | Silent background install | Silent background download, applied on next natural app exit |

Your Major/Minor classification logic, and the overlay/background UX you
already built, carry over almost unchanged - only the service underneath
(`VelopackUpdateService` instead of `StoreUpdateService`) is different.

## One-time setup

1. **Create a public (or private, with a token) GitHub repo** for this app if
   you don't have one yet - this is what becomes your centralized version
   source. Velopack reads its Releases directly.

2. **Update the repo URL** in `MainWindow.xaml.cs`:
   ```csharp
   private readonly VelopackUpdateService _updateService =
       new("https://github.com/yourname/yourrepo");
   ```

3. **Install the Velopack CLI locally** (useful for testing before relying on CI):
   ```
   dotnet tool install -g vpk
   ```

4. **Publish and pack a first build locally** to sanity-check it works:
   ```
   dotnet publish WpfStandaloneUpdateApp.csproj -c Release -r win-x64 --self-contained true -o ./publish
   vpk pack -u WpfStandaloneUpdateApp -v 1.0.0 -p ./publish -e WpfStandaloneUpdateApp.exe
   ```
   This produces an installer (e.g. `WpfStandaloneUpdateApp-win-Setup.exe`) in
   a `Releases` output folder - run that to install the app locally like a
   real user would.

## Publishing new versions (this is your "centralize the version" step)

The included `.github/workflows/release.yml` automates this: **push a git tag
like `v1.1.0`**, and GitHub Actions will build, pack, and publish that version
to your repo's Releases page automatically. From then on, every installed
copy of the app checks that Releases page and picks up the new version.

```
git tag v1.1.0
git push origin v1.1.0
```

That's the entire "centralized version" mechanism — you don't host or
maintain any manifest file yourself; the git tag *is* the version of record,
and GitHub Releases is the centralized place every client checks against.

## ⚠️ Please verify before relying on this in production

Velopack's CLI flags and a couple of API member names (`UpdateManager`,
`GithubSource`, `WaitExitThenApplyUpdates`, etc.) have shifted across
versions as the project has matured quickly. Before shipping:

- Check the current API reference: https://docs.velopack.io/reference/cs/Velopack/UpdateManager
- Check the current CLI docs for `vpk pack` / `vpk upload github` flags:
  https://docs.velopack.io
- Run the local `vpk pack` step above and confirm it produces a working
  installer before wiring up the GitHub Actions workflow.

The overall architecture (GitHub Releases as version source → Velopack
checks/downloads/applies → your existing Major/Minor UI) is solid and won't
change; only exact method/flag names might need small tweaks depending on
which Velopack version you land on.

## Debugging locally in Visual Studio (before pushing anywhere)

**Plain F5 works fine for UI/logic** - `IsInstalled` is `false` when run from the
debugger, so `CheckForUpdatesAsync()` just reports "no update" harmlessly. Good
for iterating on layout, buttons, everything except the actual update mechanics.

**To debug the real check → download → apply → restart flow, the most reliable
approach is to install a real local copy and attach to it:**

1. Build and pack two versions locally, e.g.:
   ```
   dotnet publish WpfStandaloneUpdateApp.csproj -c Release -r win-x64 --self-contained true -o ./publish
   vpk pack -u WpfStandaloneUpdateApp -v 1.0.0 -p ./publish -e WpfStandaloneUpdateApp.exe -o ./local-releases
   ```
   Run the generated `Setup.exe` from `./local-releases` - this actually installs
   the app under `%LocalAppData%\WpfStandaloneUpdateApp\current\`, just like a
   real user would get it.
2. Bump the version in your code, re-publish, and re-pack into the **same**
   `./local-releases` folder with `-v 1.1.0` (or `-v 2.0.0` to test the Major
   path). Because both versions live in the same output folder, that folder
   now has a full "release history" Velopack can check against.
3. Point your `_updateService` at that folder instead of GitHub temporarily
   (a `file:///` path works with `SimpleWebSource`, or just use a local IIS/
   `dotnet serve` HTTP server pointed at the folder).
4. Launch the **installed** exe (from `%LocalAppData%\...\current\`, not from
   Visual Studio's `bin` folder), then in Visual Studio go to
   **Debug → Attach to Process** and attach to that running
   `WpfStandaloneUpdateApp.exe`. Your breakpoints will now hit normally, and
   `IsInstalled` will correctly be `true` - so "Check for Updates" exercises
   the real download/apply/restart cycle exactly like production.

This is slower to set up than pressing F5, but it's the only way to be certain
the Major/Minor apply-and-restart behavior actually works before you rely on
it with real users.

There's also a faster, more advanced route using Velopack's built-in test
locator to simulate "installed" state directly under F5 - see
https://docs.velopack.io/integrating/testing for the current API, since it's
moved around across Velopack versions and I'd rather point you at the live
docs than give you a snippet that might not compile against your installed
version.

## Testing the Major vs. Minor flows

1. Publish `v1.0.0`, install it locally.
2. Bump the version and publish `v1.0.1` (patch) - on next "Check for
   Updates," you should see it download and stage silently, applying only
   when you close and reopen the app.
3. Bump to `v2.0.0` (major) - on next check, you should see the blocking
   overlay, requiring you to click "Update Now" before continuing.
