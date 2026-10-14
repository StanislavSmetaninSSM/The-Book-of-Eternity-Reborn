param(
    [Parameter(Mandatory)][string]$DaemonPath,
    [Parameter(Mandatory)][string]$SessionPath,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$SeedDaemonPath = '',
    [ValidateSet('inspect','publish')][string]$Mode = 'inspect'
)
$ErrorActionPreference = 'Stop'
$report = [ordered]@{ completed = $false; runtimeExecuted = $false; failure = $null }
try {
    if ($SeedDaemonPath.Length -gt 0) {
        if ($Mode -ne 'inspect') { throw 'ast.seed_execution_forbidden' }
        $seedReportPath = $ReportPath + '.seed'
        # Same owned pwsh and absolute parent deadline; AST-only seed, no second process.
        & $PSCommandPath -DaemonPath $SeedDaemonPath -SessionPath $SessionPath -ReportPath $seedReportPath -Mode inspect
        $report.seed = ([IO.File]::ReadAllText($seedReportPath) | ConvertFrom-Json -ErrorAction Stop)
        if (!$report.seed.completed) { throw 'ast.seed_invalid' }
    }
    $raw = [IO.File]::ReadAllBytes($DaemonPath)
    $bom = if ($raw.Length -ge 3 -and $raw[0] -eq 239 -and $raw[1] -eq 187 -and $raw[2] -eq 191) { 3 } else { 0 }
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $text = $utf8.GetString($raw, $bom, $raw.Length - $bom)
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($text, $DaemonPath, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw 'ast.parse_errors' }
    function Is-DirectCommand($statement, [string]$name) {
        if ($statement -isnot [Management.Automation.Language.PipelineAst] -or $statement.PipelineElements.Count -ne 1 -or $statement.Background) { return $false }
        $command = $statement.PipelineElements[0]
        return $command -is [Management.Automation.Language.CommandAst] -and $command.GetCommandName() -ceq $name -and
            $command.Redirections.Count -eq 0 -and $command.InvocationOperator -eq [Management.Automation.Language.TokenKind]::Unknown
    }
    $statements = @($ast.EndBlock.Statements)
    $startupMatches = @()
    for ($i = 0; $i -lt $statements.Count; $i++) {
        if (!(Is-DirectCommand $statements[$i] 'Invoke-GmParticipatingConsumer')) { continue }
        $elements = $statements[$i].PipelineElements[0].CommandElements
        if ($elements.Count -ne 3 -or $elements[1] -isnot [Management.Automation.Language.VariableExpressionAst] -or
            $elements[1].VariablePath.UserPath -cne 'GameSessionPath' -or $elements[1].Splatted -or
            $elements[2] -isnot [Management.Automation.Language.ScriptBlockExpressionAst]) { continue }
        $block = $elements[2].ScriptBlock
        if ($null -ne $block.ParamBlock -or $null -ne $block.BeginBlock -or $null -ne $block.ProcessBlock -or
            $null -ne $block.DynamicParamBlock -or $block.EndBlock.Traps.Count -gt 0) { continue }
        $body = @($block.EndBlock.Statements)
        if ($body.Count -ne 2 -or !(Is-DirectCommand $body[0] 'Write-GmTurnHelperBootstrap') -or
            !(Is-DirectCommand $body[1] 'Write-GmContextPack')) { continue }
        if ($body[0].PipelineElements[0].CommandElements.Count -ne 1 -or $body[1].PipelineElements[0].CommandElements.Count -ne 1) { continue }
        $startupMatches += $i
    }
    if ($startupMatches.Count -ne 1) { throw 'ast.startup_boundary' }
    $index = $startupMatches[0]; $selected = $statements[$index]
    if ($index -lt 1 -or $index + 1 -ge $statements.Count -or
        $statements[$index - 1] -isnot [Management.Automation.Language.FunctionDefinitionAst] -or
        $statements[$index - 1].Name -cne 'Write-GmContextPack') { throw 'ast.predecessor' }
    $next = $statements[$index + 1]
    if ($next -isnot [Management.Automation.Language.AssignmentStatementAst] -or
        $next.Left.Extent.Text -cne '$script:GmCompactTemplateDirective' -or
        $next.Operator -ne [Management.Automation.Language.TokenKind]::PlusEquals -or
        $next.Right.Extent.Text -cne '$script:FactionMaterializationDirective') { throw 'ast.successor' }
    $end = $selected.Extent.EndOffset
    $length = $bom + $utf8.GetByteCount($text.Substring(0, $end))
    $prefix = [byte[]]::new($length); [Array]::Copy($raw, $prefix, $length)
    if ($utf8.GetString($prefix, $bom, $prefix.Length - $bom) -cne $text.Substring(0, $end)) { throw 'ast.byte_conversion' }
    $prefixTokens = $null; $prefixErrors = $null
    $prefixAst = [Management.Automation.Language.Parser]::ParseInput($text.Substring(0, $end), $DaemonPath, [ref]$prefixTokens, [ref]$prefixErrors)
    if ($prefixErrors.Count -ne 0 -or $prefixAst.EndBlock.Statements[-1].Extent.Text -cne $selected.Extent.Text) { throw 'ast.incomplete_prefix' }
    function Scope-Name($node) {
        $parent = $node.Parent
        while ($null -ne $parent) {
            if ($parent -is [Management.Automation.Language.FunctionDefinitionAst]) { return $parent.Name }
            $parent = $parent.Parent
        }
        return '<root>'
    }
    function Refs($node) {
        return ,@($node.FindAll({ param($value) $value -is [Management.Automation.Language.VariableExpressionAst] }, $true) |
            ForEach-Object { $_.VariablePath.UserPath } | Sort-Object -Unique)
    }
    $report.sourceHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw))
    $report.prefixHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($prefix))
    $report.bomLength = $bom; $report.endOffsetUtf16 = $end; $report.prefixLength = $length
    $report.selected = $selected.Extent.Text; $report.predecessor = $statements[$index - 1].Name; $report.successor = $next.Extent.Text
    $report.writes = @($ast.FindAll({ param($value) $value -is [Management.Automation.Language.AssignmentStatementAst] }, $true) |
        ForEach-Object { @{ scope = Scope-Name $_; left = $_.Left.Extent.Text; operator = $_.Operator.ToString(); right = $_.Right.Extent.Text; refs = Refs $_.Right; offset = $_.Extent.StartOffset } })
    $report.calls = @($ast.FindAll({ param($value) $value -is [Management.Automation.Language.CommandAst] -and $value.GetCommandName() -eq 'Dispatch-WithRetry' }, $true) |
        ForEach-Object { @{ scope = Scope-Name $_; text = $_.Extent.Text; refs = Refs $_; offset = $_.Extent.StartOffset } })
    $report.returns = @($ast.FindAll({ param($value) $value -is [Management.Automation.Language.ReturnStatementAst] }, $true) |
        ForEach-Object { @{ scope = Scope-Name $_; text = $_.Extent.Text; refs = Refs $_ } })
    $report.functions = @($ast.FindAll({ param($value) $value -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $value.Name -in @('Process-TurnCore','Process-RepairRequestCore','Process-TerminalProtocolFailureRequestCore','Process-QteEffectResolutionRequestCore','Build-QteEffectResolutionDispatchMessage','Build-FirstMortalBootstrapDispatchMessage') }, $true) |
        ForEach-Object { @{ name = $_.Name; text = $_.Body.Extent.Text } })
    if ($Mode -eq 'publish') {
        $prefixPath = Join-Path ([IO.Path]::GetDirectoryName($DaemonPath)) ('gm1557-prefix-' + [Guid]::NewGuid().ToString('N') + '.ps1')
        [IO.File]::WriteAllBytes($prefixPath, $prefix)
        $report.prefixPath = $prefixPath
        $report.runtimeExecuted = $true
        & $prefixPath -GameSessionPath $SessionPath
    }
    $report.completed = $true
} catch { $report.failure = $_.Exception.ToString() }
finally { [IO.File]::WriteAllText($ReportPath, ($report | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false)) }
if (!$report.completed) { exit 1 }
