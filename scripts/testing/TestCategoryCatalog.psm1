Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-CatalogText {
    param($Value, [string]$Field)
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value)) {
        throw "Catalog field '$Field' requires non-empty text."
    }
}

function Read-TestCategoryCatalog {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $catalog = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    if ($catalog.schemaVersion -ne 1 -or @($catalog.categories).Count -eq 0) {
        throw 'Category catalog requires schemaVersion 1 and at least one category.'
    }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($category in $catalog.categories) {
        foreach ($field in @('id', 'responsibility', 'excludes')) {
            Assert-CatalogText $category[$field] $field
        }
        if ($category.id -cnotmatch '^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$' -or -not $ids.Add($category.id)) {
            throw "Invalid or duplicate category ID '$($category.id)'."
        }
        if ($category.timeoutMinutes -isnot [long] -and $category.timeoutMinutes -isnot [int]) {
            throw "Category '$($category.id)' requires an integer time budget."
        }
        if ($category.timeoutMinutes -lt 1 -or $category.timeoutMinutes -gt 15) {
            throw "Category '$($category.id)' time budget must be within 1..15 minutes."
        }
        if (-not $category.Contains('expectedSeconds') -or
            ($null -ne $category.expectedSeconds -and
                ($category.expectedSeconds -isnot [ValueType] -or $category.expectedSeconds -lt 0))) {
            throw "Category '$($category.id)' requires measured expectedSeconds or null."
        }
        foreach ($requirement in @('frontendBuild', 'exclusive')) {
            if ($category.requirements[$requirement] -isnot [bool]) {
                throw "Category '$($category.id)' requires boolean '$requirement'."
            }
        }
        if (@($category.changeHints).Count -eq 0 -or -not $category.Contains('related')) {
            throw "Category '$($category.id)' requires change hints and explicit related links."
        }
        foreach ($hint in $category.changeHints) { Assert-CatalogText $hint 'changeHints' }
        if (@($category.selectors).Count -eq 0) { throw "Category '$($category.id)' has no selectors." }
        foreach ($selector in $category.selectors) {
            if ($selector.project -cnotin @('unit', 'integration', 'frontend', 'powershell') -or
                @($selector.tests).Count -eq 0) {
                throw "Category '$($category.id)' has an unknown project or empty selector."
            }
            foreach ($test in $selector.tests) {
                Assert-CatalogText $test 'selector.tests'
                if ($selector.project -in @('unit', 'integration')) {
                    if ($test -cnotmatch '^BookOfEternityClient\.Tests\.(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*(?:\+[A-Za-z_]\w*)?\.[A-Za-z_\d*?]+$') {
                        throw "C# selector must identify a literal class and method pattern: '$test'."
                    }
                }
                else {
                    $expected = if ($selector.project -eq 'frontend') {
                        '^BookOfEternityClient\.WebFrontend/test/[A-Za-z0-9_.-]+\.test\.tsx?$'
                    } else { '^scripts/tests/[A-Za-z0-9_.-]+\.tests\.ps1$' }
                    if ($test -cnotmatch $expected) { throw "Invalid test file selector '$test'." }
                }
            }
            if ($selector.project -eq 'frontend' -and $selector.runner -cnotin @('node', 'vitest', 'typecheck')) {
                throw "Frontend selector in '$($category.id)' requires an explicit supported runner."
            }
        }
    }
    foreach ($category in $catalog.categories) {
        foreach ($related in $category.related) {
            if (-not $ids.Contains($related.id)) { throw "Unknown related category '$($related.id)'." }
            Assert-CatalogText $related.when 'related.when'
        }
    }
    return $catalog
}

function New-CategoryInventoryIndex {
    param([object[]]$Inventory)
    $methods = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $classes = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($test in $Inventory) {
        $key = "$($test.Project):$($test.Name)"
        if (-not $methods.ContainsKey($key)) {
            $method = [pscustomobject]@{
                Project = $test.Project; Name = $test.Name
                Cases = [Collections.Generic.List[string]]::new()
            }
            $methods.Add($key, $method)
            $classKey = if ($test.Project -in @('unit', 'integration')) {
                "$($test.Project):$($test.Name.Substring(0, $test.Name.LastIndexOf('.')))"
            } else { $key }
            if (-not $classes.ContainsKey($classKey)) {
                $classes.Add($classKey, [Collections.Generic.List[object]]::new())
            }
            $classes[$classKey].Add($method)
        }
        $methods[$key].Cases.Add($test.CaseName)
    }
    return @{ Methods = $methods; Classes = $classes }
}

function Find-CategorySelectorMatches {
    param($Index, [string]$Project, [string]$Pattern)
    $classKey = if ($Project -in @('unit', 'integration')) {
        "${Project}:$($Pattern.Substring(0, $Pattern.LastIndexOf('.')))"
    } else { "${Project}:$Pattern" }
    if (-not $Index.Classes.ContainsKey($classKey)) { return }
    foreach ($method in $Index.Classes[$classKey]) {
        if ($method.Name -clike $Pattern) { $method }
    }
}

function Resolve-TestCategorySelection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Catalog,
        [AllowEmptyCollection()][string[]]$Category,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Inventory
    )
    if (@($Category).Count -eq 0) { throw 'Select at least one explicit category; there is no default suite.' }
    $known = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Catalog.categories) { $known.Add($entry.id, $entry) }
    foreach ($id in $Category) {
        if ([string]::IsNullOrWhiteSpace($id) -or -not $known.ContainsKey($id)) {
            throw "Unknown category '$id'. Use -ListCategories; no tests were selected."
        }
    }
    $index = New-CategoryInventoryIndex $Inventory
    $selected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($id in ($Category | Select-Object -Unique)) {
        $entry = $known[$id]
        foreach ($selector in $entry.selectors) {
            foreach ($pattern in $selector.tests) {
                $matches = @(Find-CategorySelectorMatches $index $selector.project $pattern)
                if ($matches.Count -eq 0) { throw "Stale or empty selector in '$id': $($selector.project):$pattern" }
                foreach ($method in $matches) {
                    $key = "$($method.Project):$($method.Name)"
                    $runner = if ($selector.project -eq 'frontend') { $selector.runner } else { $selector.project }
                    if (-not $selected.ContainsKey($key)) {
                        $selected.Add($key, [pscustomobject]@{
                            Project = $method.Project; Name = $method.Name; Cases = @($method.Cases)
                            Categories = [Collections.Generic.List[string]]::new()
                            Runner = $runner; Exclusive = $false; FrontendBuild = $false
                            TimeoutMinutes = [int]$entry.timeoutMinutes
                        })
                    }
                    $item = $selected[$key]
                    if ($item.Runner -cne $runner) { throw "Conflicting runners for '$key'." }
                    if (-not $item.Categories.Contains($id)) { $item.Categories.Add($id) }
                    $item.Exclusive = $item.Exclusive -or $entry.requirements.exclusive
                    $item.FrontendBuild = $item.FrontendBuild -or $entry.requirements.frontendBuild
                    $item.TimeoutMinutes = [Math]::Min($item.TimeoutMinutes, [int]$entry.timeoutMinutes)
                }
            }
        }
    }
    return @($selected.Values | Sort-Object Project, Name)
}

function Test-TestCategoryInventory {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Catalog, [Parameter(Mandatory)][object[]]$Inventory)
    $index = New-CategoryInventoryIndex $Inventory
    $owned = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $stale = [Collections.Generic.List[string]]::new()
    foreach ($entry in $Catalog.categories) {
        foreach ($selector in $entry.selectors) {
            foreach ($pattern in $selector.tests) {
                $matches = @(Find-CategorySelectorMatches $index $selector.project $pattern)
                if ($matches.Count -eq 0) { $stale.Add("$($entry.id):$($selector.project):$pattern") }
                foreach ($method in $matches) { [void]$owned.Add("$($method.Project):$($method.Name)") }
            }
        }
    }
    $unmapped = @($index.Methods.Keys | Where-Object { -not $owned.Contains($_) } | Sort-Object)
    return [pscustomobject]@{
        Valid = $unmapped.Count -eq 0 -and $stale.Count -eq 0
        Methods = $index.Methods.Count; Categories = @($Catalog.categories).Count
        Unmapped = $unmapped; StaleSelectors = @($stale)
    }
}

function Assert-TestCategoryFrontendIsolation {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Path)
    # Test modules are entrypoints. Shared code belongs in helpers, never another test.
    # Reject literal static/dynamic imports, re-exports and CommonJS requires.
    $pattern = '(?m)\b(?:import|export)\s+(?:[^;\r\n]*?\s+from\s+)?["''][^"'']*\.test\.[cm]?[jt]sx?["'']|\b(?:import|require)\s*\(\s*["''][^"'']*\.test\.[cm]?[jt]sx?["'']'
    if ($Source -match $pattern) { throw "Frontend test '$Path' imports another test entrypoint. Extract shared helpers instead of hiding an aggregate run." }
}

Export-ModuleMember -Function Read-TestCategoryCatalog, Resolve-TestCategorySelection, Test-TestCategoryInventory, Assert-TestCategoryFrontendIsolation
