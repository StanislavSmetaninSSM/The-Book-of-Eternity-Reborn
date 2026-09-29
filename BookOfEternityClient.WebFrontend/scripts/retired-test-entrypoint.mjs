console.error('Aggregate frontend verification is retired. From the repository root, use PowerShell 7:');
console.error('./scripts/test-csharp.ps1 -ListCategories');
console.error('./scripts/test-csharp.ps1 -Category <selected-domain-category>');
console.error('Typecheck/build remain available separately. See docs/testing.md.');
process.exitCode = 2;
