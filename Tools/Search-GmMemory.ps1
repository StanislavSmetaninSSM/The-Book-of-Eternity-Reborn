param(
    [string]$SessionRoot = ".\game_session",
    [string]$EntityType = "any",
    [string]$EntityId = "",
    [string]$EntityName = "",
    [string]$Source = "",
    [string]$Query = "",
    [int]$FromTurn = 0,
    [int]$ToTurn = 0,
    [int]$Limit = 20,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$script:SearchSourceFilter = $Source
$script:HasActorFilter = $EntityType -ne "any" -or -not [string]::IsNullOrWhiteSpace($EntityId) -or -not [string]::IsNullOrWhiteSpace($EntityName)

function Test-MatchText {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Query)) { return $true }
    if ($null -eq $Text) { return $false }
    return $Text.IndexOf($Query, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Test-MatchActor {
    param(
        [string]$ActorId,
        [string]$ActorName
    )

    if (-not [string]::IsNullOrWhiteSpace($EntityId) -and $ActorId -ne $EntityId) { return $false }
    if (-not [string]::IsNullOrWhiteSpace($EntityName)) {
        if ([string]::IsNullOrWhiteSpace($ActorName)) { return $false }
        if ($ActorName.IndexOf($EntityName, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { return $false }
    }
    return $true
}

function Test-MatchSource {
    param([string]$Candidate)

    if ([string]::IsNullOrWhiteSpace($script:SearchSourceFilter)) { return $true }
    $candidateLower = $Candidate.ToLowerInvariant()
    foreach ($token in ($script:SearchSourceFilter -split "," | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        switch ($token) {
            "stories" {
                if ($candidateLower.StartsWith("stories/")) { return $true }
                continue
            }
            "journals" {
                if ($candidateLower.Contains("journal") -or $candidateLower.EndsWith("_log") -or $candidateLower -eq "npc_journals") { return $true }
                continue
            }
            "continuity" {
                if ($candidateLower -in @("guardian_project_journal", "abode_power_journal", "faction_chronicles", "world_events", "character_chronicle")) { return $true }
                continue
            }
            default {
                if ($candidateLower -eq $token) { return $true }
                continue
            }
        }
    }

    return $false
}

function Add-Hit {
    param(
        [System.Collections.Generic.List[object]]$Hits,
        [string]$Source,
        [string]$ActorType,
        [string]$ActorId,
        [string]$ActorName,
        [int]$Turn,
        [string]$Timestamp,
        [string]$Title,
        [string]$Excerpt
    )

    if (-not (Test-MatchSource -Candidate $Source)) { return }
    if ([string]::IsNullOrWhiteSpace($ActorType) -and $script:HasActorFilter) { return }
    if ($ActorType -eq "any" -and $script:HasActorFilter) { return }
    if ($EntityType -ne "any" -and $ActorType -ne "any" -and $ActorType -ne $EntityType) { return }
    if ($FromTurn -gt 0 -and $Turn -gt 0 -and $Turn -lt $FromTurn) { return }
    if ($ToTurn -gt 0 -and $Turn -gt 0 -and $Turn -gt $ToTurn) { return }
    if (-not (Test-MatchActor -ActorId $ActorId -ActorName $ActorName)) { return }

    $haystack = "$Title`n$Excerpt`n$ActorId`n$ActorName"
    if (-not (Test-MatchText -Text $haystack)) { return }

    $Hits.Add([pscustomobject]@{
        Source    = $Source
        ActorType = $ActorType
        ActorId   = $ActorId
        ActorName = $ActorName
        Turn      = $Turn
        Timestamp = $Timestamp
        Title     = $Title
        Excerpt   = $Excerpt
    })
}

function Read-Json {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    return Get-Content -Path $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-ValueOrDefault {
    param($Value, $DefaultValue = "")
    if ($null -eq $Value) { return $DefaultValue }
    return $Value
}

function Get-FirstString {
    param(
        $Object,
        [string[]]$Names
    )

    if ($null -eq $Object) { return "" }
    foreach ($name in $Names) {
        if ($Object.PSObject.Properties.Name -contains $name) {
            $value = [string](Get-ValueOrDefault $Object.$name "")
            if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
        }
    }

    return ""
}

function Get-FirstInt {
    param(
        $Object,
        [string[]]$Names
    )

    if ($null -eq $Object) { return 0 }
    foreach ($name in $Names) {
        if ($Object.PSObject.Properties.Name -contains $name) {
            $value = Get-ValueOrDefault $Object.$name 0
            if ($value -is [int]) { return $value }
            if ($value -is [long]) { return [int]$value }
            if ($value -is [double]) { return [int]$value }
            $parsed = 0
            if ([int]::TryParse([string]$value, [ref]$parsed)) { return $parsed }
        }
    }

    return 0
}

function Add-ActorMapEntry {
    param(
        [hashtable]$Map,
        [string]$Id,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Id) -or [string]::IsNullOrWhiteSpace($Name)) { return }
    if (-not $Map.ContainsKey($Id)) { $Map[$Id] = $Name }
}

function Add-FlexibleEntryHits {
    param(
        [System.Collections.Generic.List[object]]$Hits,
        [string]$Source,
        [string]$ActorType,
        [string]$ActorIdProperty,
        [hashtable]$ActorNames,
        $Entries
    )

    if ($null -eq $Entries) { return }
    foreach ($entry in @($Entries)) {
        if ($null -eq $entry) { continue }
        $actorId = if ([string]::IsNullOrWhiteSpace($ActorIdProperty)) { "" } else { [string](Get-ValueOrDefault $entry.$ActorIdProperty "") }
        $actorName = if (-not [string]::IsNullOrWhiteSpace($actorId) -and $ActorNames.ContainsKey($actorId)) { [string]$ActorNames[$actorId] } else { Get-FirstString $entry @("guardianName", "residentName", "npcName", "name", "displayName") }
        $turn = Get-FirstInt $entry @("turn", "revealedAtTurn", "resolvedAtTurn", "completionTurn", "turnNumber")
        $timestamp = Get-FirstString $entry @("timestamp", "revealedAtUtc", "resolvedAtUtc", "appliedAt", "completionTimestamp")
        $title = Get-FirstString $entry @("title", "name", "projectName", "displayName", "entryId", "eventId")
        $excerpt = Get-FirstString $entry @("summary", "description", "content", "entry", "chronicle", "text", "eventSummary", "reason", "narrative")
        Add-Hit -Hits $Hits -Source $Source -ActorType $ActorType -ActorId $actorId -ActorName $actorName -Turn $turn -Timestamp $timestamp -Title $title -Excerpt $excerpt
    }
}

function Resolve-ActorName {
    param(
        [string]$ActorType,
        [string]$ActorId,
        [string]$FallbackName
    )

    if (-not [string]::IsNullOrWhiteSpace($FallbackName)) { return $FallbackName }
    if ([string]::IsNullOrWhiteSpace($ActorId)) { return "" }

    switch ($ActorType) {
        "guardian" {
            if ($guardianNames.ContainsKey($ActorId)) { return [string]$guardianNames[$ActorId] }
        }
        "resident" {
            if ($residentNames.ContainsKey($ActorId)) { return [string]$residentNames[$ActorId] }
        }
        "npc" {
            if ($npcNames.ContainsKey($ActorId)) { return [string]$npcNames[$ActorId] }
        }
        "faction" {
            if ($factionNames.ContainsKey($ActorId)) { return [string]$factionNames[$ActorId] }
        }
    }

    return ""
}

function Get-RootEntries {
    param($Root)

    if ($null -eq $Root) { return @() }
    if ($Root -is [System.Array]) { return @($Root) }
    if ($Root.PSObject.Properties.Name -contains "entries") { return @($Root.entries) }
    if ($Root.PSObject.Properties.Name -contains "events") { return @($Root.events) }

    $results = New-Object 'System.Collections.Generic.List[object]'
    foreach ($prop in $Root.PSObject.Properties) {
        if ($prop.Name.StartsWith("_")) { continue }
        if ($null -eq $prop.Value) { continue }
        if ($prop.Value -is [System.Array]) {
            foreach ($item in @($prop.Value)) {
                if ($null -ne $item) { $results.Add($item) }
            }
        }
    }

    return $results.ToArray()
}

$sessionRootResolved = Resolve-Path $SessionRoot -ErrorAction Stop
$hits = New-Object 'System.Collections.Generic.List[object]'

$guardianNames = @{}
$residentNames = @{}
$npcNames = @{}
$factionNames = @{}

$guardians = Read-Json (Join-Path $sessionRootResolved "game_state\meta\guardians.json")
if ($guardians) {
    foreach ($guardian in @($guardians.guardians)) {
        Add-ActorMapEntry -Map $guardianNames -Id ([string](Get-FirstString $guardian @("guardianId", "id"))) -Name ([string](Get-FirstString $guardian @("canonicalName", "name", "displayName")))
    }
    if ($guardians.activeGuardian) {
        Add-ActorMapEntry -Map $guardianNames -Id ([string](Get-FirstString $guardians.activeGuardian @("guardianId", "id"))) -Name ([string](Get-FirstString $guardians.activeGuardian @("canonicalName", "name", "displayName")))
    }
}

$residentsState = Read-Json (Join-Path $sessionRootResolved "game_state\meta\guardian_abode_residents.json")
if ($residentsState) {
    foreach ($resident in @($residentsState.entries)) {
        Add-ActorMapEntry -Map $residentNames -Id ([string](Get-FirstString $resident @("residentId"))) -Name ([string](Get-FirstString $resident @("displayName", "name")))
    }
}

$npcCore = Read-Json (Join-Path $sessionRootResolved "game_state\npcs\npc_core.json")
if ($npcCore) {
    foreach ($propertyName in @("NPCsInScene", "UpdateNPCs", "NPCs", "entries")) {
        if ($npcCore.PSObject.Properties.Name -contains $propertyName) {
            foreach ($npc in @($npcCore.$propertyName)) {
                Add-ActorMapEntry -Map $npcNames -Id ([string](Get-FirstString $npc @("NPCId", "npcId", "id"))) -Name ([string](Get-FirstString $npc @("NPCName", "npcName", "name", "displayName")))
            }
        }
    }
}

$factionCore = Read-Json (Join-Path $sessionRootResolved "game_state\factions\faction_core.json")
if ($factionCore) {
    foreach ($faction in @(Get-RootEntries $factionCore)) {
        Add-ActorMapEntry -Map $factionNames -Id ([string](Get-FirstString $faction @("factionId", "id"))) -Name ([string](Get-FirstString $faction @("factionName", "name", "displayName")))
    }
}

# stories/*.jsonl
$storiesDir = Join-Path $sessionRootResolved "stories"
if (Test-Path $storiesDir) {
    Get-ChildItem $storiesDir -Filter *.jsonl -Recurse | Sort-Object FullName | ForEach-Object {
        $storyName = $_.FullName.Substring($storiesDir.Length).TrimStart('\', '/').Replace('\', '/')
        foreach ($line in Get-Content $_.FullName -Encoding UTF8) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $entry = $line | ConvertFrom-Json
                $title = [string](Get-FirstString $entry @("player", "title"))
                $excerpt = [string](Get-FirstString $entry @("narrative", "response", "content"))
                $turn = Get-FirstInt $entry @("turn", "turnNumber")
                $timestamp = [string](Get-FirstString $entry @("timestamp"))
                $entityRefs = @()
                if ($entry.PSObject.Properties.Name -contains "entityRefs" -and $null -ne $entry.entityRefs) {
                    $entityRefs = @($entry.entityRefs | Where-Object { $null -ne $_ })
                }
                if ($entityRefs.Count -gt 0 -and $script:HasActorFilter) {
                    foreach ($entityRef in $entityRefs) {
                        if ($null -eq $entityRef) { continue }

                        $actorType = [string](Get-FirstString $entityRef @("entityType", "actorType", "type"))
                        $actorId = [string](Get-FirstString $entityRef @("entityId", "actorId", "id"))
                        $actorName = Resolve-ActorName -ActorType $actorType -ActorId $actorId -FallbackName ([string](Get-FirstString $entityRef @("displayName", "name")))
                        Add-Hit -Hits $hits -Source "stories/$storyName" -ActorType $actorType -ActorId $actorId -ActorName $actorName -Turn $turn -Timestamp $timestamp -Title $title -Excerpt $excerpt
                    }
                }
                elseif ($entityRefs.Count -gt 0) {
                    $primaryRef = $entityRefs | Select-Object -First 1
                    $actorType = [string](Get-FirstString $primaryRef @("entityType", "actorType", "type"))
                    $actorId = [string](Get-FirstString $primaryRef @("entityId", "actorId", "id"))
                    $actorName = Resolve-ActorName -ActorType $actorType -ActorId $actorId -FallbackName ([string](Get-FirstString $primaryRef @("displayName", "name")))
                    Add-Hit -Hits $hits -Source "stories/$storyName" -ActorType $actorType -ActorId $actorId -ActorName $actorName -Turn $turn -Timestamp $timestamp -Title $title -Excerpt $excerpt
                }
                else {
                    Add-Hit -Hits $hits -Source "stories/$storyName" -ActorType "any" -ActorId "" -ActorName "" -Turn $turn -Timestamp $timestamp -Title $title -Excerpt $excerpt
                }
            } catch { }
        }
    }
}

# NPC thought journal
$npcJournals = Read-Json (Join-Path $sessionRootResolved "game_state\npcs\npc_journals.json")
if ($npcJournals) {
    $entries = @()
    if ($npcJournals.NPCJournals) { $entries = @($npcJournals.NPCJournals) }
    elseif ($npcJournals.npcJournals) { $entries = @($npcJournals.npcJournals) }

    foreach ($entry in $entries) {
        $npcId = [string](Get-FirstString $entry @("NPCId", "npcId"))
        $npcName = [string](Get-FirstString $entry @("NPCName", "npcName", "name", "displayName"))
        if ([string]::IsNullOrWhiteSpace($npcName) -and $npcNames.ContainsKey($npcId)) { $npcName = [string]$npcNames[$npcId] }
        $summary = [string](Get-FirstString $entry @("lastJournalNote", "summary"))
        if ([string]::IsNullOrWhiteSpace($summary) -and $entry.journalEntries) {
            $last = @($entry.journalEntries)[-1]
            if ($last) { $summary = [string](Get-FirstString $last @("description", "text", "summary")) }
        }

        Add-Hit -Hits $hits -Source "npc_journals" -ActorType "npc" -ActorId $npcId -ActorName $npcName -Turn 0 -Timestamp "" -Title $npcName -Excerpt $summary
    }
}

# NPC interaction journal
$npcInteraction = Read-Json (Join-Path $sessionRootResolved "game_state\npcs\npc_interaction_journal.json")
if ($npcInteraction) {
    Add-FlexibleEntryHits -Hits $hits -Source "npc_interaction_journal" -ActorType "npc" -ActorIdProperty "npcId" -ActorNames $npcNames -Entries (Get-RootEntries $npcInteraction)
}

# Guardian thought/event journals
$guardianThought = Read-Json (Join-Path $sessionRootResolved "game_state\meta\guardian_thought_journal.json")
if ($guardianThought) {
    Add-FlexibleEntryHits -Hits $hits -Source "guardian_thought_journal" -ActorType "guardian" -ActorIdProperty "guardianId" -ActorNames $guardianNames -Entries (Get-RootEntries $guardianThought)
}

$guardianSocial = Read-Json (Join-Path $sessionRootResolved "game_state\meta\guardian_social_journal.json")
if ($guardianSocial) {
    Add-FlexibleEntryHits -Hits $hits -Source "guardian_social_journal" -ActorType "guardian" -ActorIdProperty "guardianId" -ActorNames $guardianNames -Entries (Get-RootEntries $guardianSocial)
}

# Resident journals
if ($residentsState) {
    Add-FlexibleEntryHits -Hits $hits -Source "resident_thought_journal" -ActorType "resident" -ActorIdProperty "residentId" -ActorNames $residentNames -Entries @($residentsState.thoughtJournal)
    Add-FlexibleEntryHits -Hits $hits -Source "resident_interaction_log" -ActorType "resident" -ActorIdProperty "residentId" -ActorNames $residentNames -Entries @($residentsState.interactionLog)

    foreach ($entry in @($residentsState.historyLog)) {
        $residentId = [string](Get-FirstString $entry @("residentId"))
        $residentName = if ($residentNames.ContainsKey($residentId)) { [string]$residentNames[$residentId] } else { "" }
        Add-Hit -Hits $hits -Source "resident_history_log" -ActorType "resident" -ActorId $residentId -ActorName $residentName `
            -Turn (Get-FirstInt $entry @("revealedAtTurn")) -Timestamp ([string](Get-FirstString $entry @("revealedAtUtc"))) `
            -Title ([string](Get-FirstString $entry @("title", "entryId"))) -Excerpt ([string](Get-FirstString $entry @("summary")))
    }
}

# Guardian project journal
$guardianProjectJournal = Read-Json (Join-Path $sessionRootResolved "game_state\meta\guardian_project_journal.json")
if ($guardianProjectJournal) {
    Add-FlexibleEntryHits -Hits $hits -Source "guardian_project_journal" -ActorType "guardian" -ActorIdProperty "guardianId" -ActorNames $guardianNames -Entries (Get-RootEntries $guardianProjectJournal)
}

# Abode power journal
$abodePowerJournal = Read-Json (Join-Path $sessionRootResolved "game_state\meta\abode_power_journal.json")
if ($abodePowerJournal) {
    Add-FlexibleEntryHits -Hits $hits -Source "abode_power_journal" -ActorType "guardian" -ActorIdProperty "guardianId" -ActorNames $guardianNames -Entries (Get-RootEntries $abodePowerJournal)
}

# Faction chronicles
$factionChronicles = Read-Json (Join-Path $sessionRootResolved "game_state\factions\faction_chronicles.json")
if ($factionChronicles) {
    Add-FlexibleEntryHits -Hits $hits -Source "faction_chronicles" -ActorType "faction" -ActorIdProperty "factionId" -ActorNames $factionNames -Entries (Get-RootEntries $factionChronicles)
}

# World events
$worldEvents = Read-Json (Join-Path $sessionRootResolved "game_state\world\world_events.json")
if ($worldEvents) {
    Add-FlexibleEntryHits -Hits $hits -Source "world_events" -ActorType "any" -ActorIdProperty "" -ActorNames @{} -Entries (Get-RootEntries $worldEvents)
}

# Character chronicle
$characterChronicle = Read-Json (Join-Path $sessionRootResolved "game_state\meta\character_chronicle.json")
if ($characterChronicle) {
    Add-FlexibleEntryHits -Hits $hits -Source "character_chronicle" -ActorType "any" -ActorIdProperty "" -ActorNames @{} -Entries (Get-RootEntries $characterChronicle)
}

$sorted = $hits | Sort-Object @{Expression="Turn";Descending=$true}, @{Expression="Timestamp";Descending=$true} | Select-Object -First $Limit
if (-not $sorted) {
    if ($Json) {
        ConvertTo-Json -InputObject @() -Depth 5
    }
    else {
        Write-Host "No matches."
    }
    exit 0
}

if ($Json) {
    ConvertTo-Json -InputObject @($sorted) -Depth 5
    exit 0
}

foreach ($hit in $sorted) {
    $actorLabel = if ([string]::IsNullOrWhiteSpace($hit.ActorId)) { $hit.ActorType } else { "$($hit.ActorType):$($hit.ActorId)" }
    if (-not [string]::IsNullOrWhiteSpace($hit.ActorName)) {
        $actorLabel += " [$($hit.ActorName)]"
    }

    Write-Host "[$($hit.Source)] $actorLabel turn=$($hit.Turn) ts=$($hit.Timestamp)"
    if (-not [string]::IsNullOrWhiteSpace($hit.Title)) { Write-Host "  $($hit.Title)" }
    if (-not [string]::IsNullOrWhiteSpace($hit.Excerpt)) { Write-Host "  $($hit.Excerpt)" }
    Write-Host ""
}
