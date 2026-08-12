# Public Repository Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish `StanislavSmetaninSSM/The-Book-of-Eternity-Reborn` as an unreleased, non-commercial public project with an explicit mixed-license boundary, reproducible asset provenance, owner-reviewed pull requests for every non-owner change to `main`, and collaborator-only issue creation.

**Architecture:** Prepare and merge all repository-owned documentation and provenance changes while the repository remains private. Treat code, original project content, and excluded/third-party assets as three separate licensing domains. Run a pinned, redacted audit over the tracked tree, all reachable Git refs, GitHub text surfaces, Actions logs, and every downloadable artifact before visibility changes. Configure issue policy and presentation first; then perform the public-visibility and branch-protection rollout as one operator-controlled sequence, with the owner retaining administrator bypass.

**Tech Stack:** Markdown, C# 12/.NET 8/xUnit 2.9.2 documentation guards, PowerShell 7, Git, GitHub CLI and REST/GraphQL APIs, Gitleaks 8.30.1, deterministic PCM WAV generation, existing bounded `scripts/test-csharp.ps1` lanes.

## Global Constraints

- Source task: [GitHub Issue #1525](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1525). Keep the issue open until public settings are verified; the preparation PR uses `Refs #1525`, not an auto-closing keyword.
- Work on branch `1525-public-repository-readiness` in `E:\Games\worktrees\boe-1525-public-readiness` and preserve unrelated `BookOfEternityClient.TestSupport/bin/` and `obj/` artifacts.
- The game has not been released. Do not promise stability or save compatibility, and do not describe the visibility change as a game release.
- The project is non-commercial. The software license is `AGPL-3.0-or-later`; original project-owned story/world/lore/rules prose is `CC BY-NC-SA 4.0`; music and third-party assets remain outside both grants unless an asset-specific notice says otherwise.
- Preserve all music files. State that they were generated on Suno Basic/free for this non-commercial project, require Suno attribution, remain subject to Suno terms and third-party rights, and are not sublicensed by this repository.
- Use `Copyright © 2026 Stanislav Smetanin (Lottarend)` exactly.
- The owner `@StanislavSmetaninSSM` retains administrator bypass and can push or merge directly. Every other writer must use a pull request and receive the owner's current approval.
- Issues remain public and readable but new issue creation is `COLLABORATORS_ONLY`; pull-request creation remains `ALL`. Wiki and Discussions stay disabled.
- Do not require the currently unreliable CI workflow as a status check. Create a separate tracked follow-up after publication.
- Never print a detected secret. Redact reports, keep them outside the repository, and report only rule/path/commit/count metadata.
- The previously detected generation-service credential is confirmed revoked by the owner. Remove its literal from the current tree; retain history without rewriting it; accept only the two known historical occurrences plus the known prose false positive. Any additional or active credential blocks publication.
- Use official license/service sources: [GNU AGPL v3](https://www.gnu.org/licenses/agpl-3.0.html), [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), [Suno Terms](https://suno.com/terms), and [Suno Basic ownership guidance](https://help.suno.com/en/articles/2416769).
- Use PowerShell 7 and `./scripts/test-csharp.ps1`. During implementation run the smallest relevant `Focused` filter, one meaningful `Fast` checkpoint, and exactly one final `PreMerge` control without a duplicate final Fast run.
- This work changes repository governance and documentation only. It does not change a GM-authored game contract, afterlife contract, runtime capability, or player UI, so no Rules, TaskGuides, GM examples, manifests, or afterlife matrix updates are required.

## File Responsibility Map

| File | Responsibility |
| --- | --- |
| `README.md` | Short English summary, full Russian project description, truthful status, setup, architecture, contribution, and license map. |
| `LICENSE` | Unmodified GNU Affero General Public License version 3 text. |
| `CONTENT_LICENSE.md` | CC BY-NC-SA 4.0 grant for project-owned narrative/content, attribution, scope, and exclusions. |
| `THIRD_PARTY_NOTICES.md` | Music, sound, visual provenance, dependencies, and no-sublicense boundary. |
| `CONTRIBUTING.md` | Tracked-task requirement, branch/PR workflow, verification, docs synchronization, and owner review. |
| `.github/CODEOWNERS` | Repository-wide owner assignment. |
| `.github/pull_request_template.md` | Issue, scope, verification, docs, license, and asset-impact checklist. |
| `.github/ISSUE_TRACKING.md` | Public-readable/collaborator-created issue policy and contribution intake. |
| `BookOfEternityClient/Music/README.md` | Suno Basic attribution, non-commercial boundary, inspirations, and takedown contact. |
| `BookOfEternityClient/Sounds/README.md` | Exact Freesound CC BY 4.0 credits and original notification-chime provenance. |
| `BookOfEternityClient/Sounds/sound-notification.wav` | Deterministic project-original replacement for the asset with insufficient redistribution evidence. |
| `scripts/generate-notification-sound.ps1` | Reproducible source for `sound-notification.wav`. |
| `BookOfEternityClient.WebFrontend/public/generated-art/README.md` | Per-file provenance for the three generated launcher/shell images. |
| `docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md` | Safe environment-variable example with no live credential literal. |
| `BookOfEternityClient.Tests/RepositoryPublicationDocumentationTests.cs` | Durable guards for licensing, status, governance, asset provenance, and current-tree secret removal. |

---

### Task 1: Lock the Publication Contract with Failing Tests

**Files:**
- Create: `BookOfEternityClient.Tests/RepositoryPublicationDocumentationTests.cs`
- Reference: `docs/superpowers/specs/2026-08-12-public-repository-readiness-design.md`
- Reference: `docs/testing.md`

- [ ] **Step 1: Create repository-root and text helpers**

Use a repository-root lookup that is independent of the current shell directory:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

public sealed class RepositoryPublicationDocumentationTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", ".."));

    private static string Read(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Required publication file is missing: {relativePath}");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static string Sha256(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
```

- [ ] **Step 2: Add mixed-license and truthful-status tests**

Add `RootPublicationDocuments_DefineApprovedMixedLicenseBoundary` asserting:

```csharp
var readme = Read("README.md");
var content = Read("CONTENT_LICENSE.md");
var notices = Read("THIRD_PARTY_NOTICES.md");
var license = Read("LICENSE");

Assert.StartsWith("GNU AFFERO GENERAL PUBLIC LICENSE", license);
Assert.Equal(
    "0D96A4FF68AD6D4B6F1F30F713B18D5184912BA8DD389F86AA7710DB079ABCB0",
    Sha256("LICENSE"));
Assert.Contains("AGPL-3.0-or-later", readme, StringComparison.Ordinal);
Assert.Contains("CC BY-NC-SA 4.0", content, StringComparison.Ordinal);
Assert.Contains("Copyright © 2026 Stanislav Smetanin (Lottarend)", content, StringComparison.Ordinal);
Assert.Contains("unreleased", readme, StringComparison.OrdinalIgnoreCase);
Assert.Contains("non-commercial", readme, StringComparison.OrdinalIgnoreCase);
Assert.Contains("Игра ещё не вышла", readme, StringComparison.Ordinal);
Assert.Contains("Совместимость сохранений не является требованием", readme, StringComparison.Ordinal);
Assert.Contains("Suno Basic", notices, StringComparison.Ordinal);
Assert.Contains("not licensed under", notices, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 3: Add governance tests**

Add `RepositoryGovernanceFiles_RequireOwnerReviewedPullRequests` asserting:

```csharp
Assert.Equal("* @StanislavSmetaninSSM\n", Read(".github/CODEOWNERS").Replace("\r\n", "\n"));

var contributing = Read("CONTRIBUTING.md");
Assert.Contains("tracked GitHub Issue", contributing, StringComparison.OrdinalIgnoreCase);
Assert.Contains("pull request", contributing, StringComparison.OrdinalIgnoreCase);
Assert.Contains("@StanislavSmetaninSSM", contributing, StringComparison.Ordinal);

var template = Read(".github/pull_request_template.md");
Assert.Contains("Tracked issue", template, StringComparison.OrdinalIgnoreCase);
Assert.Contains("Verification", template, StringComparison.OrdinalIgnoreCase);
Assert.Contains("License / asset impact", template, StringComparison.OrdinalIgnoreCase);
Assert.Contains("GM / documentation impact", template, StringComparison.OrdinalIgnoreCase);

var issuePolicy = Read(".github/ISSUE_TRACKING.md");
Assert.Contains("Collaborators only", issuePolicy, StringComparison.OrdinalIgnoreCase);
Assert.Contains("publicly readable", issuePolicy, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 4: Add asset and revoked-secret tests**

Add `AssetNotices_PreserveMusicAndDocumentProvenance` and `NotificationSound_IsDeterministicOriginalAsset`:

```csharp
var music = Read("BookOfEternityClient/Music/README.md");
Assert.Contains("Suno Basic", music, StringComparison.Ordinal);
Assert.Contains("non-commercial", music, StringComparison.OrdinalIgnoreCase);
Assert.Contains("not licensed under", music, StringComparison.OrdinalIgnoreCase);

var sounds = Read("BookOfEternityClient/Sounds/README.md");
Assert.Contains("https://freesound.org/s/833055/", sounds, StringComparison.Ordinal);
Assert.Contains("https://freesound.org/s/810754/", sounds, StringComparison.Ordinal);
Assert.Contains("https://freesound.org/s/810739/", sounds, StringComparison.Ordinal);
Assert.Contains("https://freesound.org/s/810748/", sounds, StringComparison.Ordinal);
Assert.Contains("scripts/generate-notification-sound.ps1", sounds, StringComparison.Ordinal);

var generatedArt = Read("BookOfEternityClient.WebFrontend/public/generated-art/README.md");
Assert.Contains("game-shell-bg.png", generatedArt, StringComparison.Ordinal);
Assert.Contains("launcher-side-left.png", generatedArt, StringComparison.Ordinal);
Assert.Contains("launcher-side-right.png", generatedArt, StringComparison.Ordinal);

Assert.Equal(
    "72DF5E7044E57595EB57A5F38DE756A64959C579C15B3E11065F2598348E21AF",
    Sha256("BookOfEternityClient/Sounds/sound-notification.wav"));
```

Add `RevokedGenerationCredential_IsAbsentFromCurrentDocumentation` without embedding the revoked value:

```csharp
var historicalPlan = Read("docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md");
Assert.DoesNotMatch(new Regex(@"plln_[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant), historicalPlan);
Assert.Contains("POLLINATIONS_API_KEY", historicalPlan, StringComparison.Ordinal);
```

- [ ] **Step 5: Run the new class and observe RED**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests"
```

Expected: bounded failures identify missing root licensing/governance documents and the old notification-sound hash. Compilation must succeed; an unrelated build failure is diagnosed before continuing.

---

### Task 2: Add the Mixed-License Root Documentation

**Files:**
- Create: `README.md`
- Create: `LICENSE`
- Create: `CONTENT_LICENSE.md`
- Create: `THIRD_PARTY_NOTICES.md`
- Modify: `BookOfEternityClient.Tests/RepositoryPublicationDocumentationTests.cs`

- [ ] **Step 1: Install the exact AGPL v3 license text**

Download to a temporary path, verify it, and then add the verified text as `LICENSE`:

```powershell
$licenseTemp = Join-Path ([IO.Path]::GetTempPath()) 'boe-agpl-3.0.txt'
Invoke-WebRequest -Uri 'https://www.gnu.org/licenses/agpl-3.0.txt' -OutFile $licenseTemp
$licenseHash = (Get-FileHash -LiteralPath $licenseTemp -Algorithm SHA256).Hash
if ($licenseHash -ne '0D96A4FF68AD6D4B6F1F30F713B18D5184912BA8DD389F86AA7710DB079ABCB0') {
    throw "Unexpected AGPL text hash: $licenseHash"
}
```

Use `apply_patch` to add the exact verified text. Do not add project-specific prose inside `LICENSE`; the `-or-later` choice belongs in `README.md` and `CONTENT_LICENSE.md`.

- [ ] **Step 2: Write the root README**

The top of `README.md` must contain a short English summary before the full Russian section:

```markdown
# The Book of Eternity: Reborn

The Book of Eternity: Reborn is an unreleased, non-commercial dark-fantasy RPG
whose evolving world is driven by an external AI Game Master. The repository
contains the .NET 8 game runtime, console client, React Browser Client, game
contracts, examples, and development tooling. Public source availability is
not a release, stability promise, or save-compatibility promise.

## О проекте

«The Book of Eternity: Reborn» — ещё не вышедшая некоммерческая ролевая игра
в жанре тёмного фэнтези. Внешний ИИ-ведущий формирует повествование, а клиент
проверяет, материализует и безопасно сохраняет состояние живого мира.

> Игра ещё не вышла. Совместимость сохранений не является требованием.
```

Continue with complete Russian sections for:

1. current concept and the split between GM-authored semantics and client validation/state authority;
2. console and Browser clients;
3. repository layout;
4. prerequisites: .NET 8 SDK, Node.js/npm for frontend development, PowerShell 7, and a separately configured compatible external GM command;
5. quick start:

```powershell
dotnet run --project BookOfEternityClient
npm ci --prefix BookOfEternityClient.WebFrontend
npm run dev:local --prefix BookOfEternityClient.WebFrontend
```

6. bounded verification:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

7. contribution link to `CONTRIBUTING.md` and the tracked-task requirement;
8. the exact license map and links to all three notices;
9. the copyright line.

Do not claim that a specific external provider is built in, free, supported, or ready. Describe only the existing external-GM command boundary.

- [ ] **Step 3: Write the content license**

`CONTENT_LICENSE.md` must state these boundaries explicitly:

```markdown
# Original Game Content License

Copyright © 2026 Stanislav Smetanin (Lottarend)

Except where a file or directory carries a more specific notice, original
project-authored setting, story, lore, characters, rules prose, dialogue,
examples, and other non-code game text in this repository are licensed under
the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International
License (CC BY-NC-SA 4.0):
https://creativecommons.org/licenses/by-nc-sa/4.0/

This grant applies only to material for which the project owner holds the
necessary rights. It does not grant rights in third-party works, generated
music, attributed sound effects, dependencies, trademarks, or assets excluded
or governed by `THIRD_PARTY_NOTICES.md` and per-directory provenance files.
```

Add concise attribution instructions: credit the title and `Stanislav Smetanin (Lottarend)`, link the license, identify modifications, keep derivatives non-commercial, and use the same license. Clarify that software code and scripts are governed by `LICENSE` instead.

- [ ] **Step 4: Write the third-party and excluded-asset notice**

`THIRD_PARTY_NOTICES.md` must not purport to sublicense the music. Include:

```markdown
## Music generated with Suno Basic

The tracks in `BookOfEternityClient/Music/` were generated using the Suno
Basic/free tier for this non-commercial project. They are not licensed under
the repository's AGPL-3.0-or-later software license or CC BY-NC-SA 4.0 content
license. Suno attribution applies. Use, copying, redistribution, and any rights
in those tracks remain subject to the applicable Suno terms and any third-party
rights; this repository grants no separate license to them.
```

Preserve the existing named composition/inspiration credits and takedown contact. Add separate sections for:

- the four Freesound files with author, URL, and `CC BY 4.0` link;
- `sound-notification.wav` as a project-original deterministic synthesized chime whose source is the checked-in generator;
- `main-menu-bg.webp`, `browser-ui-assets/`, and `generated-art/`, each linked to its provenance record and excluded from the AGPL software grant unless its own notice says otherwise;
- NuGet/npm dependencies remaining under their upstream licenses.

Add a non-legal-advice sentence and avoid any claim that publication eliminates third-party risk.

- [ ] **Step 5: Run only the root-license test**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests.RootPublicationDocuments_DefineApprovedMixedLicenseBoundary"
```

Expected: PASS.

- [ ] **Step 6: Commit the root license package**

```powershell
git add README.md LICENSE CONTENT_LICENSE.md THIRD_PARTY_NOTICES.md BookOfEternityClient.Tests/RepositoryPublicationDocumentationTests.cs
git diff --cached --check
git commit -m "docs: define public mixed-license boundary (#1525)"
```

---

### Task 3: Make Asset Provenance Public-Safe and Reproducible

**Files:**
- Create: `scripts/generate-notification-sound.ps1`
- Replace: `BookOfEternityClient/Sounds/sound-notification.wav`
- Modify: `BookOfEternityClient/Sounds/README.md`
- Modify: `BookOfEternityClient/Music/README.md`
- Create: `BookOfEternityClient.WebFrontend/public/generated-art/README.md`
- Modify: `docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md`
- Modify: `THIRD_PARTY_NOTICES.md`

- [ ] **Step 1: Add the deterministic notification-sound generator**

Implement `scripts/generate-notification-sound.ps1` exactly as follows:

```powershell
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\BookOfEternityClient\Sounds\sound-notification.wav')
)

$sampleRate = 44100
$durationSeconds = 1.25
$sampleCount = [int]($sampleRate * $durationSeconds)
$samples = [short[]]::new($sampleCount)
$tones = @(
    @{ Frequency = 659.2551138; Start = 0.00; Duration = 0.62; Gain = 0.34 },
    @{ Frequency = 987.7666025; Start = 0.28; Duration = 0.82; Gain = 0.28 }
)

for ($index = 0; $index -lt $sampleCount; $index++) {
    $time = $index / [double]$sampleRate
    $value = 0.0

    foreach ($tone in $tones) {
        $localTime = $time - [double]$tone.Start
        if ($localTime -lt 0.0 -or $localTime -ge [double]$tone.Duration) {
            continue
        }

        $attack = [Math]::Min(1.0, $localTime / 0.018)
        $release = [Math]::Min(1.0, ([double]$tone.Duration - $localTime) / 0.24)
        $envelope = $attack * $release * [Math]::Exp(-1.45 * $localTime)
        $value += [double]$tone.Gain * $envelope * [Math]::Sin(
            2.0 * [Math]::PI * [double]$tone.Frequency * $localTime)
    }

    $value = [Math]::Max(-1.0, [Math]::Min(1.0, $value))
    $samples[$index] = [short][Math]::Round(
        $value * [short]::MaxValue,
        [MidpointRounding]::AwayFromZero)
}

$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($parent)) | Out-Null
}

$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::ASCII, $true)
$dataLength = $sampleCount * 2
$writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
$writer.Write([int](36 + $dataLength))
$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVE'))
$writer.Write([Text.Encoding]::ASCII.GetBytes('fmt '))
$writer.Write([int]16)
$writer.Write([short]1)
$writer.Write([short]1)
$writer.Write([int]$sampleRate)
$writer.Write([int]($sampleRate * 2))
$writer.Write([short]2)
$writer.Write([short]16)
$writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
$writer.Write([int]$dataLength)
foreach ($sample in $samples) {
    $writer.Write($sample)
}
$writer.Dispose()
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputPath), $stream.ToArray())
$stream.Dispose()
```

- [ ] **Step 2: Regenerate and verify the replacement**

```powershell
pwsh -NoProfile -File .\scripts\generate-notification-sound.ps1
$sound = Get-Item .\BookOfEternityClient\Sounds\sound-notification.wav
$hash = (Get-FileHash -LiteralPath $sound.FullName -Algorithm SHA256).Hash
if ($sound.Length -ne 110294) { throw "Unexpected WAV length: $($sound.Length)" }
if ($hash -ne '72DF5E7044E57595EB57A5F38DE756A64959C579C15B3E11065F2598348E21AF') {
    throw "Unexpected WAV hash: $hash"
}
```

Keep the filename so existing console/browser fallbacks require no runtime code change.

- [ ] **Step 3: Replace the sound and music notices**

In `BookOfEternityClient/Sounds/README.md`:

- retain all four exact Freesound author/URL/CC BY 4.0 credits;
- replace the generic composition disclaimer with sound-specific wording;
- identify `sound-notification.wav` as a deterministic synthesized two-tone chime generated by `scripts/generate-notification-sound.ps1`;
- retain QTE filename and fallback documentation.

In `BookOfEternityClient/Music/README.md`:

- state Suno Basic/free generation and required Suno attribution;
- state non-commercial use;
- state that the files are not licensed under AGPL or CC BY-NC-SA by this repository;
- retain the existing named composition/inspiration credits and contact address;
- say that a substantiated rights request will be handled by changing credit or removing affected files.

- [ ] **Step 4: Add generated-art provenance**

Create `BookOfEternityClient.WebFrontend/public/generated-art/README.md` with one table row for each file:

| File | Intended surface | Provenance | Source image input | Runtime behavior |
| --- | --- | --- | --- | --- |
| `game-shell-bg.png` | Game shell backdrop | Generated for tracked UI work using Codex image generation | None | Checked-in local asset |
| `launcher-side-left.png` | Launcher left decoration | Generated for tracked UI work using Codex image generation | None | Checked-in local asset |
| `launcher-side-right.png` | Launcher right decoration | Generated for tracked UI work using Codex image generation | None | Checked-in local asset |

State that the images are not remotely generated at runtime, do not knowingly contain third-party logos/text/reference images, and remain outside the AGPL software grant unless explicitly licensed elsewhere.

- [ ] **Step 5: Remove the revoked literal from the current tree**

In `docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md`, replace both literal credential examples with an environment-variable example such as:

```powershell
$apiKey = $env:POLLINATIONS_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw 'POLLINATIONS_API_KEY is required for this optional generation step.'
}
```

Keep the historical design intent, but do not add another credential, token-shaped dummy, or query-string literal. Add one sentence saying credentials must never be committed.

- [ ] **Step 6: Run the asset tests**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests.AssetNotices_PreserveMusicAndDocumentProvenance|FullyQualifiedName~RepositoryPublicationDocumentationTests.NotificationSound_IsDeterministicOriginalAsset|FullyQualifiedName~RepositoryPublicationDocumentationTests.RevokedGenerationCredential_IsAbsentFromCurrentDocumentation"
```

Expected: PASS.

- [ ] **Step 7: Commit the provenance package**

```powershell
git add scripts/generate-notification-sound.ps1 BookOfEternityClient/Sounds/sound-notification.wav BookOfEternityClient/Sounds/README.md BookOfEternityClient/Music/README.md BookOfEternityClient.WebFrontend/public/generated-art/README.md docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md THIRD_PARTY_NOTICES.md
git diff --cached --check
git commit -m "docs: make bundled asset provenance explicit (#1525)"
```

---

### Task 4: Add Contribution and Owner-Review Governance Files

**Files:**
- Create: `CONTRIBUTING.md`
- Create: `.github/CODEOWNERS`
- Create: `.github/pull_request_template.md`
- Modify: `.github/ISSUE_TRACKING.md`

- [ ] **Step 1: Write CONTRIBUTING.md**

Include these exact rules:

```markdown
# Contributing

Thank you for helping with The Book of Eternity: Reborn. The project is
unreleased and changes rapidly.

## Before implementation

Every implementation change must have a tracked GitHub Issue created or
accepted by a collaborator. Public issue creation is intentionally restricted
to collaborators; an outside contributor should discuss a proposed change in
an existing relevant pull request or contact a maintainer before investing in
large work.

## Branch and pull-request workflow

All contributors and collaborators other than the repository owner work on a
feature branch and submit a pull request to `main`. `@StanislavSmetaninSSM` is
the repository-wide code owner and final approver. Do not force-push or delete
`main`.
```

Then document branch naming, `Refs #<issue>`/`Fixes #<issue>` semantics, focused/Fast/PreMerge commands, preserving user-owned worktree changes, licensing of contributions, asset provenance requirements, and GM/afterlife documentation synchronization by linking `AGENTS.md` and `docs/testing.md`.

- [ ] **Step 2: Add CODEOWNERS**

Create `.github/CODEOWNERS` with exactly:

```text
* @StanislavSmetaninSSM
```

This also protects future changes to CODEOWNERS through the same owner-review requirement.

- [ ] **Step 3: Add the pull-request template**

Create `.github/pull_request_template.md`:

```markdown
## Tracked issue

Refs #

## Summary

<!-- Describe the user-visible and technical scope. -->

## Verification

- [ ] Smallest relevant Focused lane
- [ ] Fast checkpoint when appropriate
- [ ] PreMerge immediately before merge
- Evidence/result path:

## License / asset impact

- [ ] No new asset or license impact
- [ ] Provenance and notices updated for every new or changed asset

## GM / documentation impact

- [ ] GM prompts, docs, examples, manifests, and guards updated
- [ ] No GM-authored/runtime contract impact; rationale below

Rationale:

## Checklist

- [ ] No credential, private key, token, environment file, or private data added
- [ ] User-owned/unrelated worktree changes preserved
- [ ] Player-facing and browser/console parity checked where applicable
```

- [ ] **Step 4: Update the issue workflow**

Amend `.github/ISSUE_TRACKING.md` to state:

- Issues are publicly readable project planning records;
- only the owner and invited collaborators can create them;
- outside pull requests remain possible, but implementation must be tied to a maintainer-created or accepted issue;
- preparation PRs may use `Refs`, while completed work normally uses `Fixes` only when automatic closure is intended.

Do not claim that `.github/ISSUE_TEMPLATE/config.yml` enforces the policy; the repository setting does.

- [ ] **Step 5: Run the governance test**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests.RepositoryGovernanceFiles_RequireOwnerReviewedPullRequests"
```

Expected: PASS.

- [ ] **Step 6: Commit governance files**

```powershell
git add CONTRIBUTING.md .github/CODEOWNERS .github/pull_request_template.md .github/ISSUE_TRACKING.md
git diff --cached --check
git commit -m "docs: require owner-reviewed contribution workflow (#1525)"
```

---

### Task 5: Verify Repository-Owned Publication Files

**Files:**
- Modify if needed: files from Tasks 1–4
- Do not add: generated test output under `TestResults/`

- [ ] **Step 1: Run the complete publication guard class**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests"
```

Expected: all publication documentation tests PASS.

- [ ] **Step 2: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: PASS. Record the result directory in Issue #1525 or the preparation PR.

- [ ] **Step 3: Inspect the complete diff and links**

```powershell
git status --short
git diff origin/main...HEAD --check
git diff --stat origin/main...HEAD
rg -n -i "TBD|TODO|FIXME|XXX|placeholder|fill in|implement later" README.md CONTENT_LICENSE.md THIRD_PARTY_NOTICES.md CONTRIBUTING.md .github BookOfEternityClient/Music/README.md BookOfEternityClient/Sounds/README.md BookOfEternityClient.WebFrontend/public/generated-art/README.md
```

Expected: only intentional prose, no placeholder content, and only task-owned paths plus the known untracked build artifacts.

- [ ] **Step 4: Record the GM/afterlife no-update rationale**

Use this exact PR summary sentence:

> No GM or afterlife prompt/example/manifest update is required: this change only documents repository publication, licensing, contribution governance, and existing asset provenance; it does not change a GM-authored output, runtime contract, command, validation rule, normalizer side effect, pending/control surface, or player behavior.

---

### Task 6: Run the Private Pre-Public Security and Exposure Audit

**Files:**
- Do not modify repository files unless the audit finds a new problem.
- Keep reports under a uniquely named temporary directory.
- Add only a sanitized summary comment to Issue #1525.

- [ ] **Step 1: Refresh every reachable Git reference, including PR heads**

```powershell
git fetch --all --tags --prune
git fetch origin '+refs/pull/*/head:refs/remotes/origin/pr/*'
git for-each-ref --format='%(refname) %(objectname)' refs/heads refs/remotes refs/tags | Sort-Object
```

Review unexpected branches/tags before continuing. Do not delete a ref merely to make the audit smaller.

- [ ] **Step 2: Install pinned Gitleaks in a temporary directory**

Use release `8.30.1`; download the Windows x64 archive and checksum manifest from the official release, verify the archive SHA256 is:

```text
D29144DEFF3A68AA93CED33DDDF84B7FDC26070ADD4AA0F4513094C8332AFC4E
```

Extract only after checksum verification. Set `$gitleaks` to the resulting executable. Do not add it to the repository.

- [ ] **Step 3: Scan the current tracked tree and full reachable history**

```powershell
$auditRoot = Join-Path ([IO.Path]::GetTempPath()) ("boe-public-audit-" + [Guid]::NewGuid().ToString('N'))
$treeRoot = Join-Path $auditRoot 'tracked-tree'
New-Item -ItemType Directory -Path $treeRoot -Force | Out-Null
git archive --format=tar HEAD -o (Join-Path $auditRoot 'tracked-tree.tar')
tar -xf (Join-Path $auditRoot 'tracked-tree.tar') -C $treeRoot

& $gitleaks dir $treeRoot --redact --report-format json --report-path (Join-Path $auditRoot 'current.json')
& $gitleaks git . --log-opts='--all --full-history' --redact --report-format json --report-path (Join-Path $auditRoot 'history.json')
```

Expected after Task 3:

- current tree: only the already classified prose false positive, or fewer;
- reachable history: exactly two occurrences of the confirmed-revoked credential in the historical documentation revision plus the same prose false positive;
- zero private keys, environment files, active credentials, or new findings.

Compare by rule, path, line, and commit metadata without printing the redacted secret fields. Any delta stops publication.

- [ ] **Step 4: Export and scan GitHub text surfaces**

Export to `$auditRoot/github-text/` without emitting the response bodies to the terminal:

```powershell
$repo = 'StanislavSmetaninSSM/The-Book-of-Eternity-Reborn'
$textRoot = Join-Path $auditRoot 'github-text'
[IO.Directory]::CreateDirectory($textRoot) | Out-Null

function Save-NativeText([string]$Path, [scriptblock]$Command) {
    $content = & $Command | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Export failed: $Path" }
    [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false))
}

Save-NativeText (Join-Path $textRoot 'issues.json') { gh api "repos/$repo/issues?state=all&per_page=100" --paginate --slurp }
Save-NativeText (Join-Path $textRoot 'issue-comments.json') { gh api "repos/$repo/issues/comments?per_page=100" --paginate --slurp }
Save-NativeText (Join-Path $textRoot 'review-comments.json') { gh api "repos/$repo/pulls/comments?per_page=100" --paginate --slurp }
Save-NativeText (Join-Path $textRoot 'commit-comments.json') { gh api "repos/$repo/comments?per_page=100" --paginate --slurp }
Save-NativeText (Join-Path $textRoot 'pull-requests.json') { gh pr list --repo $repo --state all --limit 2000 --json number,title,body,reviews,comments }
```

Run `gitleaks dir` over that directory with `--redact`. Manually review public contact information and links even when they are not secret-shaped.

- [ ] **Step 5: Download and scan every Actions log and artifact**

Enumerate runs and artifacts through the API. The discovery count, successful download count, successful extraction count, and scanned count must agree:

```powershell
$runs = gh run list --repo $repo --limit 1000 --json databaseId,status,conclusion,createdAt | ConvertFrom-Json
$artifactPages = gh api "repos/$repo/actions/artifacts?per_page=100" --paginate --slurp | ConvertFrom-Json
$artifacts = @($artifactPages | ForEach-Object { $_.artifacts })
```

For each run, write `gh run view <id> --repo $repo --log` to a temporary text file. For each non-expired artifact, obtain `gh auth token` only in memory, download `archive_download_url` with `Invoke-WebRequest`, extract to a per-artifact directory, and scan the combined log/artifact root with Gitleaks. Do not echo the authorization header or token. A download, extraction, or scan failure blocks publication; do not silently skip an artifact.

- [ ] **Step 6: Record the accepted revoked-secret disposition**

The Issue #1525 audit comment must state, without the value:

- owner confirmed the credential was revoked before publication;
- its literal is absent from `HEAD` and scanned artifacts;
- two known occurrences remain only in reachable Git history;
- history was intentionally not rewritten because revocation removed operational value and rewriting would disrupt refs/clones/PRs;
- any matching GitHub secret-scanning alert after publication must be resolved as `revoked`, while any different alert blocks completion.

- [ ] **Step 7: Remove temporary reports only after recording counts**

Resolve and verify `$auditRoot` is below `[IO.Path]::GetTempPath()` before recursive deletion. Use PowerShell end-to-end:

```powershell
$resolvedAuditRoot = [IO.Path]::GetFullPath($auditRoot)
$resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $resolvedAuditRoot.StartsWith($resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove non-temporary audit path: $resolvedAuditRoot"
}
Remove-Item -LiteralPath $resolvedAuditRoot -Recurse -Force
```

---

### Task 7: Final Review, PreMerge, Pull Request, and Private Merge

**Files:**
- Modify only if review finds an issue.

- [ ] **Step 1: Rebase on the latest private main**

```powershell
git fetch origin main
git rebase origin/main
git status --short
```

Expected: only known untracked `bin/obj` artifacts remain outside the clean feature diff.

- [ ] **Step 2: Perform the final content review**

Check:

- README commands and links resolve;
- all mixed-license statements agree;
- music is present and excluded from both blanket licenses;
- no new asset lacks provenance;
- no public text promises release/save compatibility;
- owner bypass and non-owner PR requirement are described consistently;
- Issues policy is collaborator-only creation, not globally closed;
- no deferred capability is described as implemented.

- [ ] **Step 3: Run the only final PreMerge control**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: PASS. Record its unique `TestResults/test-lanes/...-premerge` directory. Do not run another Fast immediately before or after it merely as a ritual.

- [ ] **Step 4: Push and open the preparation PR while private**

```powershell
git push -u origin 1525-public-repository-readiness
gh pr create --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --base main --head 1525-public-repository-readiness --title "Prepare repository for public collaboration" --body-file $prBodyPath
```

The PR body must contain `Refs #1525`, the PreMerge evidence path, sanitized security-audit counts, the mixed-license summary, owner-review settings to be applied after merge, and the exact GM/afterlife no-update rationale from Task 5. It must not close #1525 yet.

- [ ] **Step 5: Review and merge while visibility is still private**

Verify the PR diff and repository visibility, then merge as the owner:

```powershell
gh repo view StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json visibility,defaultBranchRef
gh pr diff --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
gh pr merge --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --merge
```

Expected: `main` contains the publication package and Issue #1525 remains open.

---

### Task 8: Preconfigure Repository Presentation and Creation Policies

**Files:**
- No repository file changes.
- GitHub repository settings only.

- [ ] **Step 1: Set description, topics, and feature toggles while private**

```powershell
$repo = 'StanislavSmetaninSSM/The-Book-of-Eternity-Reborn'
gh repo edit $repo `
  --description 'Unreleased non-commercial dark-fantasy RPG driven by an external AI Game Master. Built with .NET 8 and React.' `
  --enable-issues=true `
  --enable-wiki=false `
  --enable-discussions=false `
  --add-topic rpg `
  --add-topic dark-fantasy `
  --add-topic ai-game-master `
  --add-topic dotnet `
  --add-topic react `
  --add-topic non-commercial
```

- [ ] **Step 2: Permanently restrict issue creation while preserving public PR creation**

Use the GraphQL repository policy fields, not temporary interaction limits:

```powershell
$repositoryId = gh repo view $repo --json id --jq '.id'
$mutation = @'
mutation UpdateCreationPolicies($input: UpdateRepositoryInput!) {
  updateRepository(input: $input) {
    repository {
      issueCreationPolicy
      pullRequestCreationPolicy
    }
  }
}
'@
$payload = @{
  query = $mutation
  variables = @{
    input = @{
      repositoryId = $repositoryId
      issueCreationPolicy = 'COLLABORATORS_ONLY'
      pullRequestCreationPolicy = 'ALL'
    }
  }
} | ConvertTo-Json -Depth 8
$payload | gh api graphql --input -
```

- [ ] **Step 3: Verify private preconfiguration**

```powershell
$query = @'
query RepositoryPolicy($owner: String!, $name: String!) {
  repository(owner: $owner, name: $name) {
    visibility
    issueCreationPolicy
    pullRequestCreationPolicy
    hasIssuesEnabled
    hasWikiEnabled
    hasDiscussionsEnabled
  }
}
'@
```

Submit with owner/name variables and assert: `PRIVATE`, `COLLABORATORS_ONLY`, `ALL`, Issues true, Wiki false, Discussions false.

---

### Task 9: Make the Repository Public and Protect main

**Files:**
- No repository file changes.
- GitHub visibility, branch protection, and security settings only.

- [ ] **Step 1: Confirm the pre-public gate one last time**

Before changing visibility, assert:

- preparation PR merged into `main`;
- Issue #1525 still open;
- security audit complete with no active finding;
- no collaborator other than the owner currently has write access, or every writer has been notified not to push during rollout;
- owner is authenticated with repository administration permission.

- [ ] **Step 2: Change visibility**

```powershell
gh repo edit $repo --visibility public --accept-visibility-change-consequences
```

Proceed immediately to branch protection. If the command fails, keep the repository private and diagnose; do not partially claim publication.

- [ ] **Step 3: Apply exact classic branch protection**

```powershell
$protection = @{
  required_status_checks = $null
  enforce_admins = $false
  required_pull_request_reviews = @{
    dismiss_stale_reviews = $true
    require_code_owner_reviews = $true
    required_approving_review_count = 1
    require_last_push_approval = $false
  }
  restrictions = $null
  required_conversation_resolution = $true
  required_linear_history = $false
  allow_force_pushes = $false
  allow_deletions = $false
  block_creations = $false
  lock_branch = $false
} | ConvertTo-Json -Depth 8
$protection | gh api --method PUT "repos/$repo/branches/main/protection" --input -
```

`enforce_admins=false` is intentional: the owner retains administrator bypass. Do not add a required CI context in this task.

- [ ] **Step 4: Enable public-repository security features**

```powershell
gh repo edit $repo --enable-secret-scanning=true --enable-secret-scanning-push-protection=true
gh api --method PUT "repos/$repo/vulnerability-alerts"
```

If GitHub rejects a feature for plan/account reasons, record the actual response and do not claim it is enabled. Do not weaken branch protection to compensate.

- [ ] **Step 5: Resolve only the known historical secret alert**

List alerts after scanning completes, projecting away the secret value:

```powershell
$alertJson = gh api "repos/$repo/secret-scanning/alerts?state=open&per_page=100" `
  --paginate --slurp `
  --jq '[.[][] | {number, secret_type, state, resolution, locations_url}]' | Out-String
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate secret-scanning alerts.' }
$alerts = $alertJson | ConvertFrom-Json
```

Resolve an alert as `revoked` only when its path/commit/rule matches the accepted historical audit finding. Use a resolution comment that says the credential was revoked before publication and removed from the current tree. Any other alert stops completion and is not auto-resolved.

---

### Task 10: Verify the Public Boundary and Close the Task

**Files:**
- No repository file changes.
- GitHub issue/settings evidence only.

- [ ] **Step 1: Verify through authenticated APIs**

```powershell
gh repo view $repo --json visibility,description,defaultBranchRef,hasIssuesEnabled,hasWikiEnabled,hasDiscussionsEnabled,licenseInfo,repositoryTopics
gh api "repos/$repo/branches/main/protection"
gh api "repos/$repo" --jq '.security_and_analysis'
```

Assert:

- visibility `PUBLIC`;
- default branch `main`;
- description/topics exact;
- license detected as AGPL v3;
- Issues on, Wiki/Discussions off;
- one approval and code-owner review required;
- stale reviews dismissed and conversations resolved;
- force push/delete disabled;
- `enforce_admins.enabled == false` so owner bypass remains;
- no required status checks;
- configured security features report enabled or have a recorded limitation.

- [ ] **Step 2: Verify issue and PR creation policies**

Run the GraphQL query from Task 8 and assert `issueCreationPolicy=COLLABORATORS_ONLY` and `pullRequestCreationPolicy=ALL` after visibility changes.

- [ ] **Step 3: Verify anonymous public access**

Use an unauthenticated request with no GitHub token/header:

```powershell
$public = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo" -Headers @{ 'User-Agent' = 'boe-public-verification' }
if ($public.private -ne $false) { throw 'Anonymous API does not report a public repository.' }
if ($public.default_branch -ne 'main') { throw "Unexpected default branch: $($public.default_branch)" }
```

Then invoke `$browser-act`, follow its browser-selection gates, open the public repository in a signed-out/clean session, and verify the rendered README, license badge/detection, files, Issues readability, Pull requests tab, and absence of Wiki/Discussions. Do not rely solely on the owner's authenticated page.

- [ ] **Step 4: Create the CI follow-up issue**

Create a collaborator-owned issue titled:

```text
[Task] Restore GitHub Actions CI before making it a required main check
```

The body records that recent runs were failing before publication, CI was intentionally not added as a required check, and the completion criterion is a stable green workflow followed by a separate owner decision to require it.

- [ ] **Step 5: Record final evidence and close #1525**

Comment on Issue #1525 with:

- merged preparation PR and commit;
- PreMerge result directory and test count;
- sanitized current/history/GitHub-log/artifact audit counts;
- revoked historical-secret disposition;
- exact visibility, branch protection, issue/PR policy, and security-feature results;
- link to the CI follow-up issue;
- statement that no music was removed;
- GM/afterlife no-update rationale.

Only then close Issue #1525.

- [ ] **Step 6: Final local hygiene**

```powershell
git fetch origin main
git log -1 --oneline origin/main
git status --short
```

Expected: `origin/main` contains the publication merge; no task-owned uncommitted files remain; unrelated `bin/obj` artifacts are untouched.

## Rollback and Stop Conditions

- A new or active secret: keep/private the repository, revoke first, remove current/artifact copies, re-audit; never print the value.
- An artifact/log that cannot be downloaded or scanned: stop before visibility change.
- Visibility becomes public but branch protection fails: do nothing else until protection succeeds; do not grant collaborator write access meanwhile.
- `COLLABORATORS_ONLY` cannot be preserved: temporarily disable Issues instead of allowing unrestricted creation, then restore the approved policy before completion.
- A security setting is unavailable: record the exact available state and do not make a false claim.
- A public page exposes unexpected private information: stop, remove/rotate the exposure, and remember that changing back to private does not retract existing clones/downloads.

## Completion Evidence

The task is complete only when all of the following are true:

- repository-owned publication tests and final PreMerge pass;
- current tree, all reachable refs, GitHub text, Actions logs, and every artifact were scanned with no active secret;
- root README/license/content/third-party notices agree;
- music remains present and explicitly excluded from blanket licenses;
- public anonymous access works;
- non-owner `main` changes require a PR and current owner approval;
- owner administrator bypass remains enabled;
- issue creation is collaborators-only while PR creation remains public;
- force push/delete are disabled;
- security settings are verified or limitations recorded;
- the CI repair follow-up exists;
- Issue #1525 contains evidence and is closed only after every setting is verified.
