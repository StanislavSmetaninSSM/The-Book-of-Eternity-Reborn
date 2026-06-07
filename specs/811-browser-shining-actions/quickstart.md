# Quickstart: Browser Shining Abode Actions

**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## Developer Verification

1. Run focused RED tests after adding tests but before production implementation:

   ```powershell
   dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningActionsParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests" --logger "console;verbosity=minimal"
   ```

2. Implement the minimal C# browser command/prompt/write support.

3. Run focused GREEN tests:

   ```powershell
   dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningActionsParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests" --logger "console;verbosity=minimal"
   ```

4. Run the final Shining/browser parity sweep:

   ```powershell
   dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"
   ```

5. Build and scan:

   ```powershell
   dotnet build BookOfEternityClient.sln --no-restore
   git diff --check origin/main...HEAD
   git diff --unified=0 origin/main...HEAD -- . ":(exclude)specs/811-browser-shining-actions/**" | Select-String -Pattern "password|passwd|secret|token|apikey|api_key|authorization|bearer|client_secret|connectionstring|private_key|BEGIN RSA|BEGIN OPENSSH" -CaseSensitive:$false
   ```

## Manual Browser Smoke Scope

If a local runtime is available, verify these player flows in Shining Abode:

- open native faction discovery and confirm resource spend,
- open faction investment and select a visible faction,
- support a completed unsupported project,
- remove support from a supported project,
- retire a completed project,
- attempt one command outside Shining Abode and confirm a player-facing blocker.

No React-specific verification is required unless frontend files change.
