Set-StrictMode -Off

$script:BoeGameSessionPath = $null

function Initialize-BoeGmTurnHelper {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GameSessionPath
    )

    if (!(Test-Path -LiteralPath $GameSessionPath)) {
        throw "Game session path does not exist: $GameSessionPath"
    }

    $script:BoeGameSessionPath = (Resolve-Path -LiteralPath $GameSessionPath).Path
}

function Assert-BoeGmTurnHelperInitialized {
    if ([string]::IsNullOrWhiteSpace($script:BoeGameSessionPath)) {
        throw "GM turn helper is not initialized. Run Initialize-BoeGmTurnHelper -GameSessionPath <path> first."
    }
}

function Resolve-BoeSessionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    Assert-BoeGmTurnHelperInitialized

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        $fullPath = [System.IO.Path]::GetFullPath($RelativePath)
    }
    else {
        $normalized = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $fullPath = [System.IO.Path]::GetFullPath((Join-Path $script:BoeGameSessionPath $normalized))
    }

    $sessionRoot = [System.IO.Path]::GetFullPath($script:BoeGameSessionPath)
    if (!$fullPath.StartsWith($sessionRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside game_session: $RelativePath"
    }

    return $fullPath
}

function Read-BoeJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Resolve-BoeSessionPath -RelativePath $RelativePath
    if (!(Test-Path -LiteralPath $path)) {
        throw "JSON file does not exist: $RelativePath"
    }

    $json = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    return ConvertFrom-BoeJsonMutable -Json $json
}

function ConvertFrom-BoeJsonMutable {
    param(
        [AllowNull()]
        [string]$Json
    )

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return $null
    }

    $convertFromJson = Get-Command ConvertFrom-Json
    if ($convertFromJson.Parameters.ContainsKey("AsHashtable") -and
        $convertFromJson.Parameters.ContainsKey("NoEnumerate")) {
        $parsed = $Json | ConvertFrom-Json -AsHashtable -NoEnumerate
        return ConvertTo-BoeMutableJsonValue -Value $parsed
    }

    try {
        Add-Type -AssemblyName System.Web.Extensions -ErrorAction Stop
        $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
        $serializer.MaxJsonLength = [int]::MaxValue
        return ConvertTo-BoeMutableJsonValue -Value $serializer.DeserializeObject($Json)
    }
    catch {
        $parsed = $Json | ConvertFrom-Json
        return ConvertTo-BoeMutableJsonValue -Value $parsed
    }
}

function ConvertTo-BoeMutableJsonValue {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in $Value.Keys) {
            $result[[string]$key] = ConvertTo-BoeMutableJsonValue -Value $Value[$key]
        }

        return $result
    }

    if ($Value -is [System.Array]) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($item in $Value) {
            $items.Add((ConvertTo-BoeMutableJsonValue -Value $item))
        }

        return ,([object[]]$items.ToArray())
    }

    if ($Value.GetType().FullName -eq "System.Management.Automation.PSCustomObject") {
        $result = [ordered]@{}
        foreach ($property in $Value.PSObject.Properties) {
            if ($property.MemberType -eq [System.Management.Automation.PSMemberTypes]::NoteProperty) {
                $result[$property.Name] = ConvertTo-BoeMutableJsonValue -Value $property.Value
            }
        }

        return $result
    }

    return $Value
}

function Get-BoeJsonValue {
    param(
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string[]]$Names,

        [object]$Default = $null
    )

    if ($null -eq $Object) {
        return $Default
    }

    foreach ($name in @($Names)) {
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        if ($Object -is [System.Collections.IDictionary]) {
            if ($Object.Contains($name)) {
                return $Object[$name]
            }

            foreach ($key in $Object.Keys) {
                if ([string]::Equals([string]$key, $name, [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $Object[$key]
                }
            }
        }

        $property = $Object.PSObject.Properties |
            Where-Object { [string]::Equals($_.Name, $name, [System.StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1

        if ($null -ne $property) {
            return $property.Value
        }
    }

    return $Default
}

function Get-BoeJsonArrayItems {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Array]) {
        foreach ($item in $Value) {
            $item
        }
        return
    }

    $Value
}

function Set-BoeJsonProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [object]$Value
    )

    if ($Object -is [System.Collections.IDictionary]) {
        $Object[$Name] = $Value
        return
    }

    $property = $Object.PSObject.Properties |
        Where-Object { [string]::Equals($_.Name, $Name, [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1

    if ($null -ne $property) {
        try {
            $property.Value = $Value
            return
        }
        catch {
            # Fall through and replace unusual member shapes with a JSON-like NoteProperty.
        }
    }

    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
}

function Add-BoeJsonArrayItem {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [object]$Item,

        [string]$UniqueBy = ""
    )

    $existingValue = Get-BoeJsonValue -Object $Object -Names @($PropertyName)
    $existingItems = @(Get-BoeJsonArrayItems -Value $existingValue)
    $nextItems = New-Object System.Collections.Generic.List[object]

    $replacementKey = $null
    if (![string]::IsNullOrWhiteSpace($UniqueBy)) {
        $replacementKey = Get-BoeJsonValue -Object $Item -Names @($UniqueBy)
    }

    foreach ($existingItem in $existingItems) {
        if (![string]::IsNullOrWhiteSpace($UniqueBy) -and $null -ne $replacementKey) {
            $existingKey = Get-BoeJsonValue -Object $existingItem -Names @($UniqueBy)
            if ($null -ne $existingKey -and [string]::Equals([string]$existingKey, [string]$replacementKey, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
        }

        $nextItems.Add($existingItem)
    }

    $nextItems.Add($Item)
    $arrayValue = [object[]]$nextItems.ToArray()
    Set-BoeJsonProperty -Object $Object -Name $PropertyName -Value $arrayValue

    return ,$arrayValue
}

function Test-BoeJsonTextEquals {
    param(
        [AllowNull()]
        [object]$Left,

        [AllowNull()]
        [object]$Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $false
    }

    return [string]::Equals([string]$Left, [string]$Right, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-BoeFirstNonEmptyJsonString {
    param(
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in @($Names)) {
        $value = Get-BoeJsonValue -Object $Object -Names @($name)
        if ($null -ne $value -and ![string]::IsNullOrWhiteSpace([string]$value)) {
            return [string]$value
        }
    }

    return ""
}

function Get-BoeJsonInt {
    param(
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string[]]$Names,

        [int]$Default = 0
    )

    $value = Get-BoeJsonValue -Object $Object -Names $Names
    if ($null -eq $value) {
        return $Default
    }

    try {
        return [int]$value
    }
    catch {
        $parsed = 0
        if ([int]::TryParse([string]$value, [ref]$parsed)) {
            return $parsed
        }
    }

    return $Default
}

function Test-BoeNpcTradeItemClassCode {
    param(
        [AllowNull()]
        [string]$TradeItemClass
    )

    return $TradeItemClass -in @("Functional", "Material", "FlavorOrUtility")
}

function Get-BoeClampedDouble {
    param(
        [double]$Value,
        [double]$Min,
        [double]$Max
    )

    if ($Value -lt $Min) {
        return $Min
    }

    if ($Value -gt $Max) {
        return $Max
    }

    return $Value
}

function Get-BoePlayerTradeValue {
    foreach ($relativePath in @("game_state/misc/characteristics.json", "game_state/player/player_status.json", "game_state/core/player_status.json")) {
        try {
            $path = Resolve-BoeSessionPath -RelativePath $relativePath
            if (!(Test-Path -LiteralPath $path)) {
                continue
            }

            $root = Read-BoeJson -RelativePath $relativePath
            $modified = Get-BoeJsonInt -Object $root -Names @("modifiedTrade") -Default ([int]::MinValue)
            if ($modified -ne [int]::MinValue) {
                return $modified
            }

            $trade = Get-BoeJsonInt -Object $root -Names @("trade") -Default ([int]::MinValue)
            if ($trade -ne [int]::MinValue) {
                return $trade
            }
        }
        catch {
            # Try the next canonical source.
        }
    }

    return 10
}

function Get-BoeNpcTradeValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Npc
    )

    $characteristics = Get-BoeJsonValue -Object $Npc -Names @("characteristics")
    if ($null -ne $characteristics) {
        $modified = Get-BoeJsonInt -Object $characteristics -Names @("modifiedTrade") -Default ([int]::MinValue)
        if ($modified -ne [int]::MinValue) {
            return $modified
        }

        $standard = Get-BoeJsonInt -Object $characteristics -Names @("standardTrade") -Default ([int]::MinValue)
        if ($standard -ne [int]::MinValue) {
            return $standard
        }

        $trade = Get-BoeJsonInt -Object $characteristics -Names @("trade") -Default ([int]::MinValue)
        if ($trade -ne [int]::MinValue) {
            return $trade
        }
    }

    return 10
}

function Get-BoeNpcPricingReputationModifier {
    param(
        [AllowNull()]
        [string]$PricingTradeTier
    )

    switch ($PricingTradeTier) {
        "Hostile" { return 1.20 }
        "Wary" { return 1.10 }
        "Warm" { return 0.92 }
        "Trusted" { return 0.85 }
        default { return 1.00 }
    }
}

function Get-BoeNpcTradeBuyPrice {
    param(
        [int]$BasePrice,
        [int]$PlayerTrade,
        [int]$NpcTrade,
        [string]$PricingTradeTier
    )

    $tradeDelta = $PlayerTrade - $NpcTrade
    $tradeAdjustment = Get-BoeClampedDouble -Value ([double]$tradeDelta * 0.01) -Min -0.20 -Max 0.20
    $tradeModifier = 1.20 - $tradeAdjustment
    $reputationModifier = Get-BoeNpcPricingReputationModifier -PricingTradeTier $PricingTradeTier
    return [int][Math]::Ceiling([double]$BasePrice * [double]$tradeModifier * [double]$reputationModifier)
}

function Find-BoeNpcTradeRequest {
    param(
        [Parameter(Mandatory = $true)]
        [object]$PendingRoot,

        [Parameter(Mandatory = $true)]
        [string]$RequestId
    )

    foreach ($request in @(Get-BoeJsonArrayItems -Value (Get-BoeJsonValue -Object $PendingRoot -Names @("requests")))) {
        $candidateRequestId = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("requestId")
        if (Test-BoeJsonTextEquals -Left $candidateRequestId -Right $RequestId) {
            return $request
        }
    }

    $singleRequestId = Get-BoeFirstNonEmptyJsonString -Object $PendingRoot -Names @("requestId")
    if (Test-BoeJsonTextEquals -Left $singleRequestId -Right $RequestId) {
        return $PendingRoot
    }

    return $null
}

function Find-BoeCanonicalNpcObject {
    param(
        [Parameter(Mandatory = $true)]
        [object]$NpcRoot,

        [Parameter(Mandatory = $true)]
        [string]$NpcId
    )

    foreach ($arrayName in @("NPCsInScene", "npcs", "UpdateNPCs", "NPCs", "characters", "actors")) {
        foreach ($npc in @(Get-BoeJsonArrayItems -Value (Get-BoeJsonValue -Object $NpcRoot -Names @($arrayName)))) {
            $candidateId = Get-BoeFirstNonEmptyJsonString -Object $npc -Names @("NPCId", "npcId", "id", "initialId")
            if (Test-BoeJsonTextEquals -Left $candidateId -Right $NpcId) {
                return $npc
            }
        }
    }

    return $null
}

function Normalize-BoeNpcTradeItem {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Item,

        [Parameter(Mandatory = $true)]
        [object]$Request,

        [Parameter(Mandatory = $true)]
        [object]$Npc,

        [Parameter(Mandatory = $true)]
        [string]$PricingTradeTier,

        [Parameter(Mandatory = $true)]
        [int]$Index
    )

    $requestProfile = Get-BoeFirstNonEmptyJsonString -Object $Request -Names @("merchantProfile")
    if ([string]::IsNullOrWhiteSpace($requestProfile)) {
        throw "NPC trade request is missing merchantProfile."
    }

    $itemData = Get-BoeJsonValue -Object $Item -Names @("itemData")
    if ($null -eq $itemData) {
        throw "NPC trade item #$Index is missing itemData."
    }

    $itemName = Get-BoeFirstNonEmptyJsonString -Object $itemData -Names @("name")
    if ([string]::IsNullOrWhiteSpace($itemName)) {
        throw "NPC trade item #$Index itemData is missing name."
    }

    $itemId = Get-BoeFirstNonEmptyJsonString -Object $Item -Names @("itemId")
    if ([string]::IsNullOrWhiteSpace($itemId)) {
        $itemId = Get-BoeFirstNonEmptyJsonString -Object $itemData -Names @("itemId", "id")
        if (![string]::IsNullOrWhiteSpace($itemId)) {
            Set-BoeJsonProperty -Object $Item -Name "itemId" -Value $itemId
        }
    }

    if ([string]::IsNullOrWhiteSpace($itemId)) {
        throw "NPC trade item #$Index must define itemId either on the slot or itemData."
    }

    if ([string]::IsNullOrWhiteSpace((Get-BoeFirstNonEmptyJsonString -Object $itemData -Names @("itemId", "id")))) {
        Set-BoeJsonProperty -Object $itemData -Name "itemId" -Value $itemId
    }

    $slotId = Get-BoeFirstNonEmptyJsonString -Object $Item -Names @("slotId")
    if ([string]::IsNullOrWhiteSpace($slotId)) {
        $requestId = Get-BoeFirstNonEmptyJsonString -Object $Request -Names @("requestId")
        $slotId = "$requestId-slot-$Index"
        Set-BoeJsonProperty -Object $Item -Name "slotId" -Value $slotId
    }

    $itemProfile = Get-BoeFirstNonEmptyJsonString -Object $Item -Names @("merchantProfile")
    if ([string]::IsNullOrWhiteSpace($itemProfile)) {
        Set-BoeJsonProperty -Object $Item -Name "merchantProfile" -Value $requestProfile
    }
    elseif (!(Test-BoeJsonTextEquals -Left $itemProfile -Right $requestProfile)) {
        throw "NPC trade item #$Index merchantProfile '$itemProfile' does not match request merchantProfile '$requestProfile'."
    }

    if ($null -eq (Get-BoeJsonValue -Object $Item -Names @("soldOut"))) {
        Set-BoeJsonProperty -Object $Item -Name "soldOut" -Value $false
    }

    $tradeItemClass = Get-BoeFirstNonEmptyJsonString -Object $itemData -Names @("tradeItemClass")
    if ([string]::IsNullOrWhiteSpace($tradeItemClass)) {
        $tradeItemClass = Get-BoeFirstNonEmptyJsonString -Object $Item -Names @("tradeItemClass")
    }

    if ([string]::IsNullOrWhiteSpace($tradeItemClass)) {
        throw "NPC trade item #$Index itemData is missing tradeItemClass. Use Functional, Material, or FlavorOrUtility."
    }

    if (!(Test-BoeNpcTradeItemClassCode -TradeItemClass $tradeItemClass)) {
        throw "NPC trade item #$Index has unsupported tradeItemClass '$tradeItemClass'. Use Functional, Material, or FlavorOrUtility."
    }

    Set-BoeJsonProperty -Object $itemData -Name "tradeItemClass" -Value $tradeItemClass

    $rarity = Get-BoeFirstNonEmptyJsonString -Object $itemData -Names @("quality", "rarity")
    if ([string]::IsNullOrWhiteSpace($rarity)) {
        throw "NPC trade item #$Index itemData is missing quality or rarity."
    }

    $basePrice = Get-BoeJsonInt -Object $itemData -Names @("price") -Default 0
    if ($basePrice -le 0) {
        throw "NPC trade item #$Index itemData.price must be a positive number."
    }

    $baseSellPrice = Get-BoeJsonInt -Object $itemData -Names @("baseSellPrice") -Default -1
    if ($baseSellPrice -lt 0) {
        throw "NPC trade item #$Index itemData.baseSellPrice must be a non-negative number."
    }

    $canonicalPrice = Get-BoeNpcTradeBuyPrice `
        -BasePrice $basePrice `
        -PlayerTrade (Get-BoePlayerTradeValue) `
        -NpcTrade (Get-BoeNpcTradeValue -Npc $Npc) `
        -PricingTradeTier $PricingTradeTier
    Set-BoeJsonProperty -Object $Item -Name "price" -Value $canonicalPrice

    return $Item
}

function Complete-BoeNpcTradeInventoryRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RequestId,

        [Parameter(Mandatory = $true)]
        [object[]]$Items,

        [string]$GenerationTradeTier = "Good",

        [string]$PricingTradeTier = "Neutral"
    )

    if ([string]::IsNullOrWhiteSpace($RequestId)) {
        throw "RequestId is required for Complete-BoeNpcTradeInventoryRequest."
    }

    $pendingRoot = Read-BoeJson -RelativePath "game_state/control/pending_npc_trade_inventory_requests.json"
    $request = Find-BoeNpcTradeRequest -PendingRoot $pendingRoot -RequestId $RequestId
    if ($null -eq $request) {
        throw "NPC trade request not found: $RequestId"
    }

    $npcId = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("npcId")
    if ([string]::IsNullOrWhiteSpace($npcId)) {
        throw "NPC trade request '$RequestId' is missing npcId."
    }

    $npcRoot = Read-BoeJson -RelativePath "game_state/npcs/npc_core.json"
    $npc = Find-BoeCanonicalNpcObject -NpcRoot $npcRoot -NpcId $npcId
    if ($null -eq $npc) {
        throw "NPC trade request '$RequestId' references unknown NPC '$npcId'. Same-turn NPCs may expose this identity as initialId; ensure npc_core.json contains NPCId, npcId, id, or initialId."
    }

    $expectedCountRaw = Get-BoeJsonValue -Object $request -Names @("derivedTradeSlotCount") -Default 0
    $expectedCount = [int]$expectedCountRaw
    $itemList = New-Object System.Collections.Generic.List[object]
    $index = 1
    foreach ($item in @($Items)) {
        $itemList.Add((Normalize-BoeNpcTradeItem -Item $item -Request $request -Npc $npc -PricingTradeTier $PricingTradeTier -Index $index))
        $index++
    }

    if ($expectedCount -gt 0 -and $itemList.Count -ne $expectedCount) {
        throw "NPC trade request '$RequestId' expects $expectedCount trade items, but helper received $($itemList.Count)."
    }

    $createdAtWorldDateRaw = Get-BoeJsonValue -Object $request -Names @("createdAtWorldDate") -Default 0
    $refreshAfterWorldDateRaw = Get-BoeJsonValue -Object $request -Names @("refreshAfterWorldDate") -Default 0
    $createdAtTurnRaw = Get-BoeJsonValue -Object $request -Names @("createdAtTurn") -Default 0
    $tradeInventory = [ordered]@{
        tradeCycleId = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("tradeCycleId")
        generatedAtWorldDate = [int]$createdAtWorldDateRaw
        refreshAfterWorldDate = [int]$refreshAfterWorldDateRaw
        generationTradeTier = $GenerationTradeTier
        pricingTradeTier = $PricingTradeTier
        items = [object[]]$itemList.ToArray()
    }

    Set-BoeJsonProperty -Object $npc -Name "tradeInventory" -Value $tradeInventory

    $receipt = [ordered]@{
        requestId = $RequestId
        npcId = $npcId
        npcName = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("npcName")
        tradeCycleId = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("tradeCycleId")
        merchantProfile = Get-BoeFirstNonEmptyJsonString -Object $request -Names @("merchantProfile")
        status = "ready"
        itemCount = $itemList.Count
        resolvedAtTurn = [int]$createdAtTurnRaw
        resolvedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }

    $null = Add-BoeJsonArrayItem -Object $npcRoot -PropertyName "UpdateNpcTradeInventoryReceipts" -Item $receipt -UniqueBy "requestId"
    Write-BoeJson -RelativePath "game_state/npcs/npc_core.json" -Data $npcRoot -Depth 100
}

function Normalize-BoeRelativePath {
    param(
        [AllowNull()]
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return ""
    }

    return $RelativePath.Replace('\', '/').TrimStart('/').Trim()
}

function Convert-BoeFullPathToSessionRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    Assert-BoeGmTurnHelperInitialized

    $full = [System.IO.Path]::GetFullPath($FullPath)
    $sessionRoot = [System.IO.Path]::GetFullPath($script:BoeGameSessionPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $prefix = $sessionRoot + [System.IO.Path]::DirectorySeparatorChar

    if ([string]::Equals($full, $sessionRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    if (!$full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside game_session: $FullPath"
    }

    return Normalize-BoeRelativePath -RelativePath $full.Substring($prefix.Length)
}

function Get-BoePolicyRelativePath {
    param(
        [AllowNull()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        $fullPath = Resolve-BoeSessionPath -RelativePath $Path
        return Convert-BoeFullPathToSessionRelativePath -FullPath $fullPath
    }

    return Normalize-BoeRelativePath -RelativePath $Path
}

function Test-BoeClientOwnedRuntimePath {
    param(
        [AllowNull()]
        [string]$RelativePath
    )

    $path = Normalize-BoeRelativePath -RelativePath $RelativePath
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $false
    }

    if ([string]::Equals($path, "input/turn_request.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ([string]::Equals($path, "game_state/history/chat_log.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ([string]::Equals($path, "game_state/control/pending_turn_snapshot.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "game_state/control/pending_turn_snapshot.authority.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "game_state/control/pending_turn_snapshot_manifest.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        $path.StartsWith("game_state/control/pending_turn_snapshot/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ([string]::Equals($path, "game_state/control/validation_repair_request.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "game_state/control/validation_diagnostic_failure_report.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "game_state/control/terminal_protocol_failure_request.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "game_state/control/gm_bridge_status.json", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ($path.StartsWith("stories/", [System.StringComparison]::OrdinalIgnoreCase) -and
        $path.EndsWith(".jsonl", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $false
}

function Get-BoeCurrentRealm {
    Assert-BoeGmTurnHelperInitialized

    $path = Resolve-BoeSessionPath -RelativePath "game_state/meta/soul_state.json"
    if (!(Test-Path -LiteralPath $path)) {
        return ""
    }

    try {
        $state = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        $realm = Get-BoeJsonValue -Object $state -Names @("currentRealm")
        if ($null -eq $realm) {
            return ""
        }

        return [string]$realm
    }
    catch {
        return ""
    }
}

function Test-BoeAfterlifeRealm {
    $realm = Get-BoeCurrentRealm
    return [string]::Equals($realm, "Chaos Sea", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($realm, "Shining Abode", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-BoeMortalWorldProfilePath {
    param(
        [AllowNull()]
        [string]$RelativePath
    )

    $path = Normalize-BoeRelativePath -RelativePath $RelativePath
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $false
    }

    foreach ($prefix in @(
        "game_state/world/",
        "game_state/npcs/",
        "game_state/factions/",
        "game_state/player/",
        "game_state/inventory/",
        "game_state/combat/",
        "game_state/quests/"
    )) {
        if ($path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-BoeRollbackArtifactPath {
    param(
        [AllowNull()]
        [string]$RelativePath
    )

    $path = Normalize-BoeRelativePath -RelativePath $RelativePath
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $false
    }

    return $path.IndexOf(".rollback.", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Assert-BoeRealmWritableRuntimePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $policyPath = Get-BoePolicyRelativePath -Path $RelativePath
    if ((Test-BoeMortalWorldProfilePath -RelativePath $policyPath) -and (Test-BoeAfterlifeRealm)) {
        $realm = Get-BoeCurrentRealm
        throw "Path is wrong realm Mortal World profile state while currentRealm is '$realm' and must not be written by the GM helper: $RelativePath"
    }
}

function Assert-BoeGmWritableRuntimePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $policyPath = Get-BoePolicyRelativePath -Path $RelativePath
    if (Test-BoeClientOwnedRuntimePath -RelativePath $policyPath) {
        throw "Path is client-owned runtime state and must not be written by the GM helper: $RelativePath"
    }

    Assert-BoeRealmWritableRuntimePath -RelativePath $policyPath
}

function Assert-BoeGmFilesModifiedEntries {
    param(
        [AllowNull()]
        [string[]]$FilesModified
    )

    foreach ($entry in @($FilesModified)) {
        $policyPath = Get-BoePolicyRelativePath -Path $entry
        if (Test-BoeClientOwnedRuntimePath -RelativePath $policyPath) {
            throw "filesModified contains client-owned runtime state. Remove this entry and let the client maintain it: $entry"
        }

        Assert-BoeRealmWritableRuntimePath -RelativePath $policyPath
    }
}

function Test-BoeTerminalSignalPath {
    param(
        [AllowNull()]
        [string]$RelativePath
    )

    $path = Normalize-BoeRelativePath -RelativePath $RelativePath
    return [string]::Equals($path, "ready/turn_complete.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($path, "ready/turn_error.json", [System.StringComparison]::OrdinalIgnoreCase)
}

function Read-BoeTerminalSignalOrNull {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Resolve-BoeSessionPath -RelativePath $RelativePath
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Existing terminal signal is unreadable: $RelativePath. Let the client clean stale ready state before writing more runtime data."
    }
}

function Test-BoeTerminalSignalMatchesTurn {
    param(
        [AllowNull()]
        [object]$Signal,

        [Parameter(Mandatory = $true)]
        [object]$TurnRequest
    )

    if ($null -eq $Signal) {
        return $false
    }

    return [string]$Signal.sessionId -eq [string]$TurnRequest.sessionId -and
        [string]$Signal.requestId -eq [string]$TurnRequest.requestId -and
        [int]$Signal.turnNumber -eq [int]$TurnRequest.turnNumber
}

function Assert-BoeNoExistingTerminalSignalForTurn {
    param(
        [Parameter(Mandatory = $true)]
        [object]$TurnRequest,

        [Parameter(Mandatory = $true)]
        [string]$Operation
    )

    foreach ($terminalPath in @("ready/turn_complete.json", "ready/turn_error.json")) {
        $signal = Read-BoeTerminalSignalOrNull -RelativePath $terminalPath
        if ($null -eq $signal) {
            continue
        }

        if (Test-BoeTerminalSignalMatchesTurn -Signal $signal -TurnRequest $TurnRequest) {
            throw "$Operation blocked: current request already has terminal signal $terminalPath. Do not write stale runtime data after success/error; wait for the client rollback/cleanup cycle and resend a fresh turn if needed."
        }

        throw "$Operation blocked: existing terminal signal $terminalPath belongs to another request. Let the client clean stale ready state before writing more runtime data."
    }
}

function Get-BoeCurrentTurnRequestOrNull {
    $path = Resolve-BoeSessionPath -RelativePath "input/turn_request.json"
    if (!(Test-Path -LiteralPath $path)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Current input/turn_request.json is unreadable; do not write runtime state until the client repairs or clears the pending turn."
    }
}

function Test-BoeActiveValidationRepairRequestForTurn {
    param(
        [Parameter(Mandatory = $true)]
        [object]$TurnRequest
    )

    $path = Resolve-BoeSessionPath -RelativePath "game_state/control/validation_repair_request.json"
    if (!(Test-Path -LiteralPath $path)) {
        return $false
    }

    try {
        $repair = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($repair.metadataDiagnosticOnly) {
            return $false
        }

        return [string]$repair.sessionId -eq [string]$TurnRequest.sessionId -and
            [string]$repair.requestId -eq [string]$TurnRequest.requestId -and
            [int]$repair.turnNumber -eq [int]$TurnRequest.turnNumber
    }
    catch {
        return $false
    }
}

function Assert-BoeNoTerminalSignalBeforeRuntimeWrite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    if (Test-BoeTerminalSignalPath -RelativePath $RelativePath) {
        return
    }

    $turn = Get-BoeCurrentTurnRequestOrNull
    if ($null -eq $turn) {
        return
    }

    if (Test-BoeActiveValidationRepairRequestForTurn -TurnRequest $turn) {
        return
    }

    Assert-BoeNoExistingTerminalSignalForTurn -TurnRequest $turn -Operation "Write-BoeJson"
}

function Get-BoeFileSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (!(Test-Path -LiteralPath $Path)) {
        return ""
    }

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $sha.ComputeHash($stream)
            return [System.BitConverter]::ToString($bytes).Replace("-", "")
        }
        finally {
            $sha.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function ConvertTo-BoeCanonicalJsonString {
    param(
        [AllowNull()]
        $Value
    )

    if ($null -eq $Value) {
        return "null"
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $parts = @()
        foreach ($property in @($Value.PSObject.Properties | Sort-Object Name)) {
            $name = ConvertTo-Json -InputObject $property.Name -Compress
            $propertyValue = ConvertTo-BoeCanonicalJsonString -Value $property.Value
            $parts += "${name}:$propertyValue"
        }

        return "{" + ($parts -join ",") + "}"
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $parts = @()
        foreach ($key in @($Value.Keys | Sort-Object)) {
            $name = ConvertTo-Json -InputObject ([string]$key) -Compress
            $propertyValue = ConvertTo-BoeCanonicalJsonString -Value $Value[$key]
            $parts += "${name}:$propertyValue"
        }

        return "{" + ($parts -join ",") + "}"
    }

    if (($Value -is [System.Collections.IEnumerable]) -and !($Value -is [string])) {
        $items = @()
        foreach ($item in $Value) {
            $items += ConvertTo-BoeCanonicalJsonString -Value $item
        }

        return "[" + ($items -join ",") + "]"
    }

    return ConvertTo-Json -InputObject $Value -Compress
}

function Get-BoeComparableFileSignature {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (!(Test-Path -LiteralPath $Path)) {
        return ""
    }

    if ([string]::Equals([System.IO.Path]::GetExtension($Path), ".json", [System.StringComparison]::OrdinalIgnoreCase)) {
        try {
            $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
            $parsed = ConvertFrom-Json -InputObject $raw
            return "json:" + (ConvertTo-BoeCanonicalJsonString -Value $parsed)
        }
        catch {
            # Invalid JSON still needs deterministic comparison; fall back to byte identity.
        }
    }

    return "sha256:" + (Get-BoeFileSha256 -Path $Path)
}

function Get-BoeRawMortalWorldProfileMutations {
    if (!(Test-BoeAfterlifeRealm)) {
        return @()
    }

    $snapshotRoot = Resolve-BoeSessionPath -RelativePath "game_state/control/pending_turn_snapshot"
    if (!(Test-Path -LiteralPath $snapshotRoot)) {
        return @()
    }

    $violations = @()
    $seenCurrentPaths = @{}
    foreach ($prefix in @(
        "game_state/world",
        "game_state/npcs",
        "game_state/factions",
        "game_state/player",
        "game_state/inventory",
        "game_state/combat",
        "game_state/quests"
    )) {
        $root = Resolve-BoeSessionPath -RelativePath $prefix
        if (!(Test-Path -LiteralPath $root)) {
            continue
        }

        foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction SilentlyContinue) {
            $relativePath = Convert-BoeFullPathToSessionRelativePath -FullPath $file.FullName
            if (!(Test-BoeMortalWorldProfilePath -RelativePath $relativePath)) {
                continue
            }

            if (Test-BoeRollbackArtifactPath -RelativePath $relativePath) {
                continue
            }

            $seenCurrentPaths[$relativePath.ToLowerInvariant()] = $true
            $snapshotPath = Join-Path $snapshotRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
            if (!(Test-Path -LiteralPath $snapshotPath)) {
                $violations += [pscustomobject]@{
                    path = $relativePath
                    reason = "new forbidden Mortal World profile file"
                }
                continue
            }

            $currentSignature = Get-BoeComparableFileSignature -Path $file.FullName
            $snapshotSignature = Get-BoeComparableFileSignature -Path $snapshotPath
            if (![string]::Equals($currentSignature, $snapshotSignature, [System.StringComparison]::Ordinal)) {
                $violations += [pscustomobject]@{
                    path = $relativePath
                    reason = "changed from pending-turn snapshot"
                }
            }
        }
    }

    $snapshotRootFull = [System.IO.Path]::GetFullPath($snapshotRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $snapshotPrefix = $snapshotRootFull + [System.IO.Path]::DirectorySeparatorChar

    foreach ($prefix in @(
        "game_state/world",
        "game_state/npcs",
        "game_state/factions",
        "game_state/player",
        "game_state/inventory",
        "game_state/combat",
        "game_state/quests"
    )) {
        $snapshotPrefixRoot = Join-Path $snapshotRoot ($prefix.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (!(Test-Path -LiteralPath $snapshotPrefixRoot)) {
            continue
        }

        foreach ($snapshotFile in Get-ChildItem -LiteralPath $snapshotPrefixRoot -File -Recurse -ErrorAction SilentlyContinue) {
            $snapshotFull = [System.IO.Path]::GetFullPath($snapshotFile.FullName)
            if (!$snapshotFull.StartsWith($snapshotPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $relativePath = Normalize-BoeRelativePath -RelativePath $snapshotFull.Substring($snapshotPrefix.Length)
            if (!(Test-BoeMortalWorldProfilePath -RelativePath $relativePath)) {
                continue
            }

            if (Test-BoeRollbackArtifactPath -RelativePath $relativePath) {
                continue
            }

            if ($seenCurrentPaths.ContainsKey($relativePath.ToLowerInvariant())) {
                continue
            }

            $currentPath = Resolve-BoeSessionPath -RelativePath $relativePath
            if (!(Test-Path -LiteralPath $currentPath)) {
                $violations += [pscustomobject]@{
                    path = $relativePath
                    reason = "deleted from pending-turn snapshot"
                }
            }
        }
    }

    return $violations
}

function Assert-BoeNoRawMortalWorldProfileMutations {
    param(
        [string]$Operation = "GM turn helper completion"
    )

    $violations = @(Get-BoeRawMortalWorldProfileMutations)
    if ($violations.Count -eq 0) {
        return
    }

    $details = ($violations |
        Select-Object -First 12 |
        ForEach-Object { "$($_.path) ($($_.reason))" }) -join "; "
    if ($violations.Count -gt 12) {
        $details += "; ..."
    }

    $realm = Get-BoeCurrentRealm
    throw "$Operation blocked: raw wrong-realm Mortal World profile mutations detected while currentRealm is '$realm'. Restore or remove these changes before completing the turn: $details"
}

function Test-BoeFilesModifiedContainsPath {
    param(
        [AllowNull()]
        [string[]]$FilesModified,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $target = Normalize-BoeRelativePath -RelativePath $RelativePath
    foreach ($entry in @($FilesModified)) {
        $policyPath = Get-BoePolicyRelativePath -Path $entry
        if ([string]::Equals($policyPath, $target, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-BoeGuardianLooksLikeSystemPreset {
    param(
        [AllowNull()]
        [object]$Guardian
    )

    if ($null -eq $Guardian) {
        return $false
    }

    $originType = Get-BoeJsonValue -Object $Guardian -Names @("originType")
    if ($null -ne $originType -and [string]::Equals([string]$originType, "system_preset", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $sourcePreset = Get-BoeJsonValue -Object $Guardian -Names @("sourcePreset")
    if ($null -eq $sourcePreset) {
        return $false
    }

    $presetId = Get-BoeJsonValue -Object $sourcePreset -Names @("presetId")
    return ![string]::IsNullOrWhiteSpace([string]$presetId)
}

function Test-BoeGuardianRootLooksLikeClientOwnedSystemSeed {
    param(
        [AllowNull()]
        [object]$Root
    )

    if ($null -eq $Root) {
        return $false
    }

    $pending = Get-BoeJsonValue -Object $Root -Names @("pendingGuardianCreation")
    if ($null -ne $pending) {
        return $false
    }

    $activeGuardian = Get-BoeJsonValue -Object $Root -Names @("activeGuardian")
    if (Test-BoeGuardianLooksLikeSystemPreset -Guardian $activeGuardian) {
        return $true
    }

    foreach ($guardian in @(Get-BoeJsonArrayItems -Value (Get-BoeJsonValue -Object $Root -Names @("guardians")))) {
        if (Test-BoeGuardianLooksLikeSystemPreset -Guardian $guardian) {
            return $true
        }
    }

    return $false
}

function Test-BoeTurnHasAuthorizedGuardianMutation {
    param(
        [Parameter(Mandatory = $true)]
        [object]$TurnRequest
    )

    $playerAction = [string](Get-BoeJsonValue -Object $TurnRequest -Names @("playerAction") -Default "")
    foreach ($marker in @(
        "[SYSTEM_GUARDIAN_ATTRACTION]",
        "[PLAYER_GUARDIAN_FOUNDATION]",
        "[GUARDIAN_TRADE_REQUEST]",
        "[CHAOS_SEA_TRAVEL]",
        "[GUARDIAN_PROVOCATION]",
        "UpdateGuardians",
        "processGacha",
        "startGuardianProjects",
        "completeGuardianProjects",
        "guardianThoughtJournalUpdates",
        "guardianSocialJournalUpdates",
        "guardianPowerEvents"
    )) {
        if ($playerAction.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    foreach ($controlPath in @(
        "game_state/control/system_guardian_attraction.json",
        "game_state/control/pending_player_guardian_foundation.json",
        "game_state/control/pending_guardian_trade_request.json"
    )) {
        $fullPath = Resolve-BoeSessionPath -RelativePath $controlPath
        if (Test-Path -LiteralPath $fullPath) {
            return $true
        }
    }

    return $false
}

function Assert-BoeNoUnauthorizedSystemGuardianBootstrapMutation {
    param(
        [Parameter(Mandatory = $true)]
        [object]$TurnRequest,

        [AllowNull()]
        [string[]]$FilesModified
    )

    $turnNumber = [int](Get-BoeJsonValue -Object $TurnRequest -Names @("turnNumber") -Default 0)
    if ($turnNumber -ne 1 -or !(Test-BoeAfterlifeRealm)) {
        return
    }

    $snapshotPath = Resolve-BoeSessionPath -RelativePath "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json"
    if (!(Test-Path -LiteralPath $snapshotPath)) {
        return
    }

    try {
        $snapshotRoot = Get-Content -LiteralPath $snapshotPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (!(Test-BoeGuardianRootLooksLikeClientOwnedSystemSeed -Root $snapshotRoot)) {
            return
        }
    }
    catch {
        return
    }

    if (Test-BoeTurnHasAuthorizedGuardianMutation -TurnRequest $TurnRequest) {
        return
    }

    if (Test-BoeFilesModifiedContainsPath -FilesModified $FilesModified -RelativePath "game_state/meta/guardians.json") {
        throw "Complete-BoeTurn blocked: first Chaos Sea system Guardian bootstrap treats game_state/meta/guardians.json as a client-owned Guardian mirror. Do not list guardians.json in FilesModified unless the turn has an authorized Guardian mutation contract."
    }

    $currentPath = Resolve-BoeSessionPath -RelativePath "game_state/meta/guardians.json"
    if (!(Test-Path -LiteralPath $currentPath)) {
        throw "Complete-BoeTurn blocked: first Chaos Sea system Guardian bootstrap deleted game_state/meta/guardians.json. Restore the client-owned system Guardian mirror from pending_turn_snapshot before completing the turn."
    }

    $currentSignature = Get-BoeComparableFileSignature -Path $currentPath
    $snapshotSignature = Get-BoeComparableFileSignature -Path $snapshotPath
    if (![string]::Equals($currentSignature, $snapshotSignature, [System.StringComparison]::Ordinal)) {
        throw "Complete-BoeTurn blocked: first Chaos Sea system Guardian bootstrap changed game_state/meta/guardians.json without an authorized Guardian mutation contract. Restore the client-owned system Guardian mirror from pending_turn_snapshot and persist first-meeting memory through afterlife_chronicles instead."
    }
}

function Write-BoeJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [object]$Data,

        [int]$Depth = 100,

        [switch]$AllowClientOwnedRuntimeWrite
    )

    if (!$AllowClientOwnedRuntimeWrite) {
        Assert-BoeGmWritableRuntimePath -RelativePath $RelativePath
    }
    Assert-BoeNoTerminalSignalBeforeRuntimeWrite -RelativePath $RelativePath

    $path = Resolve-BoeSessionPath -RelativePath $RelativePath
    $parent = Split-Path -Parent $path
    if (!(Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $effectiveDepth = [Math]::Max(1, [Math]::Min($Depth, 100))
    $json = $Data | ConvertTo-Json -Depth $effectiveDepth
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($path, $json + [Environment]::NewLine, $utf8NoBom)
}

function Get-BoeCurrentTurnRequest {
    $path = Resolve-BoeSessionPath -RelativePath "input/turn_request.json"
    if (!(Test-Path -LiteralPath $path)) {
        throw "Current input/turn_request.json is missing. The daemon may have already closed this wait cycle; do not write a stale terminal signal."
    }

    return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Assert-BoeCurrentPendingSnapshotContext {
    param(
        [Parameter(Mandatory = $true)]
        [object]$TurnRequest
    )

    $manifestPath = Resolve-BoeSessionPath -RelativePath "game_state/control/pending_turn_snapshot.json"
    if (!(Test-Path -LiteralPath $manifestPath)) {
        throw "Current game_state/control/pending_turn_snapshot.json is missing. The client no longer has active pending-turn authority; do not write a stale terminal signal."
    }

    $authorityPath = Resolve-BoeSessionPath -RelativePath "game_state/control/pending_turn_snapshot.authority.json"
    if (!(Test-Path -LiteralPath $authorityPath)) {
        throw "Current game_state/control/pending_turn_snapshot.authority.json is missing. The client no longer has active pending-turn authority; do not write a stale terminal signal."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifestSessionId = [string]$manifest.sessionId
    $manifestRequestId = [string]$manifest.requestId
    $manifestTurnNumber = [int]$manifest.turnNumber

    if ($manifestSessionId -ne [string]$TurnRequest.sessionId -or
        $manifestRequestId -ne [string]$TurnRequest.requestId -or
        $manifestTurnNumber -ne [int]$TurnRequest.turnNumber) {
        throw "Current pending_turn_snapshot context does not match input/turn_request.json; do not write a stale terminal signal."
    }
}

function Complete-BoeTurn {
    param(
        [string[]]$FilesModified = @()
    )

    Assert-BoeGmFilesModifiedEntries -FilesModified $FilesModified
    Assert-BoeNoRawMortalWorldProfileMutations -Operation "Complete-BoeTurn"
    $turn = Get-BoeCurrentTurnRequest
    Assert-BoeCurrentPendingSnapshotContext -TurnRequest $turn
    Assert-BoeNoExistingTerminalSignalForTurn -TurnRequest $turn -Operation "Complete-BoeTurn"
    Assert-BoeNoUnauthorizedSystemGuardianBootstrapMutation -TurnRequest $turn -FilesModified $FilesModified
    $signal = [ordered]@{
        sessionId = [string]$turn.sessionId
        requestId = [string]$turn.requestId
        turnNumber = [int]$turn.turnNumber
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
        status = "success"
        filesModified = @($FilesModified)
    }

    Write-BoeJson -RelativePath "ready/turn_complete.json" -Data $signal -Depth 20 -AllowClientOwnedRuntimeWrite
}

function Fail-BoeTurn {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ErrorMessage
    )

    $turn = Get-BoeCurrentTurnRequest
    Assert-BoeCurrentPendingSnapshotContext -TurnRequest $turn
    Assert-BoeNoExistingTerminalSignalForTurn -TurnRequest $turn -Operation "Fail-BoeTurn"
    $signal = [ordered]@{
        sessionId = [string]$turn.sessionId
        requestId = [string]$turn.requestId
        turnNumber = [int]$turn.turnNumber
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
        status = "error"
        error = $ErrorMessage
    }

    Write-BoeJson -RelativePath "ready/turn_error.json" -Data $signal -Depth 20 -AllowClientOwnedRuntimeWrite
    throw "GM turn failed: $ErrorMessage"
}

function Complete-BoeValidationRepair {
    $repair = Read-BoeJson -RelativePath "game_state/control/validation_repair_request.json"
    if ($repair.metadataDiagnosticOnly) {
        throw "validation_repair_request.json uses diagnostic-only metadata. Do not write validation_repair_ready.json from this request."
    }

    Assert-BoeNoRawMortalWorldProfileMutations -Operation "Complete-BoeValidationRepair"

    $signal = [ordered]@{
        sessionId = [string]$repair.sessionId
        requestId = [string]$repair.requestId
        turnNumber = [int]$repair.turnNumber
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
        status = "success"
    }

    Write-BoeJson -RelativePath "game_state/control/validation_repair_ready.json" -Data $signal -Depth 20
}
