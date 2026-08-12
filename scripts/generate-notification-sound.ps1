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
