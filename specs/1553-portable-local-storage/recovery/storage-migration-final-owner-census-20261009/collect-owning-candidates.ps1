param([string]$Repo, [string]$Out, [string]$Source = '')
$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath($Repo)
$sourceSha = (& git -C $repoPath rev-parse $(if ($Source) { $Source } else { "HEAD" })).Trim()
$rows = [Collections.Generic.List[object]]::new()
$blobs = [Collections.Generic.List[object]]::new()
$parser = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]
$paths = @(& git -C $repoPath ls-tree -r --name-only $sourceSha -- 'BookOfEternityClient' | Where-Object { $_.EndsWith('.cs') })
foreach ($path in $paths) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $repoPath; $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.ArgumentList.Add('show'); $start.ArgumentList.Add("${sourceSha}:$path")
    $process = [Diagnostics.Process]::Start($start)
    $buffer = [IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($buffer)
    $errorText = $process.StandardError.ReadToEnd(); $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Cannot read pinned source $path : $errorText" }
    $bytes = $buffer.ToArray(); $buffer.Dispose(); $process.Dispose()
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $tree = $parser::ParseText($text)
    $sourceHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $blobs.Add([ordered]@{Path = $path; Sha256 = $sourceHash; Bytes = $bytes.Length})
    foreach ($node in $tree.GetRoot().DescendantNodes()) {
        if ($node.GetType().Name -ne 'InvocationExpressionSyntax' -or
            $node.Expression.ToString() -notmatch '(^|\.)AcquireCanonicalWriteLeaseAsync$') { continue }
        $method = $null; $lambda = $null; $using = $null; $classes = [Collections.Generic.List[string]]::new()
        foreach ($ancestor in $node.Ancestors()) {
            $kind = $ancestor.GetType().Name
            if ($kind -in @('SimpleLambdaExpressionSyntax','ParenthesizedLambdaExpressionSyntax','AnonymousMethodExpressionSyntax') -and $null -eq $lambda) { $lambda = $ancestor }
            if ($kind -in @('MethodDeclarationSyntax','LocalFunctionStatementSyntax','ConstructorDeclarationSyntax') -and $null -eq $method) { $method = $ancestor }
            if ($kind -eq 'UsingStatementSyntax' -and $null -eq $using) { $using = $ancestor }
            if ($kind -eq 'LocalDeclarationStatementSyntax' -and $ancestor.UsingKeyword.Text -eq 'using' -and $null -eq $using) { $using = $ancestor }
            if ($kind -in @('ClassDeclarationSyntax','StructDeclarationSyntax','RecordDeclarationSyntax')) { $classes.Add($ancestor.Identifier.Text) }
        }
        $body = if ($method) { $method.ToString() } else { '' }
        $rows.Add([ordered]@{
            Path = $path; Line = $tree.GetLineSpan($node.Span).StartLinePosition.Line + 1
            Method = if ($method) { $method.Identifier.Text } else { $null }
            InvocationSpanStart = $node.Span.Start; InvocationSpanLength = $node.Span.Length
            MethodSpanLength = if ($method) { $method.Span.Length } else { $null }
            MethodBodySha256 = if ($method) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.UTF8Encoding]::new($false).GetBytes($method.ToFullString()))).ToLowerInvariant() } else { $null }
            MethodSpanStart = if ($method) { $method.Span.Start } else { $null }
            MethodStartLine = if ($method) { $tree.GetLineSpan($method.Span).StartLinePosition.Line + 1 } else { $null }
            MethodEndLine = if ($method) { $tree.GetLineSpan($method.Span).EndLinePosition.Line + 1 } else { $null }
            MethodSignature = if ($method) { ($method.Modifiers.ToString() + ' ' + $method.ReturnType.ToString() + ' ' + $method.Identifier.Text + $(if ($method.TypeParameterList) { $method.TypeParameterList.ToString() } else { '' }) + $method.ParameterList.ToString()).Trim() } else { $null }
            Classes = $classes.ToArray(); Invocation = $node.ToString()
            UsingSyntax = if ($using) { $using.ToString().Substring(0, [Math]::Min(500, $using.ToString().Length)) } else { $null }
            NestedLambda = $null -ne $lambda
            MethodMentionsCsp = $body.Contains('CoordinatedStatePublicationUncertainException')
            MethodMentionsReleaseHelper = $body.Contains('ReleaseOwnedLeaseAsync')
            SourceSha256 = $sourceHash
        })
    }
}
$result = [ordered]@{Source = $sourceSha; Parser = $parser.Assembly.FullName; ParserPath = $parser.Assembly.Location;
    Scope = 'Product BookOfEternityClient tracked C# syntax only. Acquisition candidates and nearest method/using/lambda, not a semantic publisher/defect/acceptance classification.';
    FilesRead = $paths.Count; InvocationCount = $rows.Count; SourceBlobs = $blobs.ToArray(); Invocations = $rows.ToArray()}
$result | ConvertTo-Json -Depth 12 | Set-Content -Path $Out -Encoding utf8NoBOM
Write-Output "$sourceSha $($paths.Count) files $($rows.Count) acquisition candidates; syntax only, zero test execution"
