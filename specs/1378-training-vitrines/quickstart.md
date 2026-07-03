# Quickstart: Training Vitrines

## Console smoke test

1. Restore and build:

```powershell
dotnet restore BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore -p:UseAppHost=false
```

2. Open a Mortal World test save with an NPC teacher.
3. Run `/обучение`.
4. Verify:
   - teacher list appears;
   - offer details show price, requirements, current value, target value, teacher cap, lock reasons;
   - buying a legal skill deducts money and current-level XP progress;
   - stale offers request refresh instead of buying.

## Afterlife smoke test

1. Open Chaos Sea and Shining Abode test saves.
2. Run `/обучение` and `/духовные_искусства`.
3. Verify:
   - mentor offers show relationship discount;
   - self-training fallback is visible but expensive;
   - new special-art fallback unlock is blocked;
   - legal mentor upgrade writes a receipt.

## Browser smoke test

1. Start the local backend and Vite frontend.
2. Open the same Mortal and afterlife saves.
3. Run the training command from the browser.
4. Verify cards match the accepted data prototype: selectors for large collections, nested readable cards, localized labels, no raw JSON/semicolon strings.

## Required automated checks

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "Training|Skill|SpiritualArt|Validation"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
npm --prefix BookOfEternityClient.WebFrontend run verify
```
