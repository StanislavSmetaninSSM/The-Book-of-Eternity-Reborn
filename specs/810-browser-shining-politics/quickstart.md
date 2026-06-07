# Quickstart: Browser Shining Abode Politics

## Local Verification

1. Confirm Spec Kit resolves the active feature:

   ```powershell
   .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
   ```

2. Run focused browser/Shining tests:

   ```powershell
   dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningPolitics|ShiningAbode|ExplorerWebCommandServiceTests|BrowserAfterlifeWriteServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"
   ```

3. Build touched C# projects:

   ```powershell
   dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
   dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
   ```

4. Run frontend verification only if frontend files or browser contract fixtures changed:

   ```powershell
   npm run verify --prefix BookOfEternityClient.WebFrontend
   ```

5. Run documentation verification if afterlife docs/examples/manifests changed:

   ```powershell
   dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
   ```

6. Run final static checks:

   ```powershell
   git diff --check origin/main...HEAD
   ```

## Manual Browser Smoke Path

1. Start a game state in ordinary active Shining Abode with visible factions, ascended residents, enough Ink Feathers, and enough Light Sparks.
2. Open `/help` or the browser action menu and confirm player-facing entries exist for founding, realignment, and leadership transition.
3. Open each guided form directly by command alias and confirm the form blocks outside Shining Abode.
4. Submit a valid form in Shining Abode and confirm the matching existing pending file receives one request.
5. Switch realm after opening a prompt and before submit; confirm submit blocks and writes nothing.
