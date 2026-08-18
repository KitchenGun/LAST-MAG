param(
    [string]$OutputName = "SFX_FacilityLockdown.wav"
)

$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$outputDirectory = Join-Path $projectRoot "Assets\Resources\Audio\SystemVoice"
$outputPath = Join-Path $outputDirectory $OutputName
$cuePath = Join-Path $projectRoot "Archive\UnusedResources\Assets\Audio\ThirdParty\DaydreamSound\InterfaceAndItemSounds\Error_Buzz_05.wav"
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("gulag-facility-lockdown-" + [guid]::NewGuid())
$filter = "highpass=f=120,lowpass=f=5200,acompressor=threshold=0.12:ratio=4:attack=5:release=80,acrusher=bits=12:mix=0.12,aecho=0.8:0.18:38:0.10,loudnorm=I=-16:TP=-1.5:LRA=5,silenceremove=start_periods=1:start_duration=0.03:start_threshold=-42dB,areverse,silenceremove=start_periods=1:start_duration=0.08:start_threshold=-42dB,areverse"

function Get-Duration([string]$Path)
{
    return [double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $Path)
}

function New-Voice([string]$Text, [string]$Name)
{
    $rawPath = Join-Path $tempDirectory "$Name-raw.wav"
    $processedPath = Join-Path $tempDirectory "$Name.wav"
    $synthesizer = New-Object System.Speech.Synthesis.SpeechSynthesizer
    $synthesizer.SelectVoice("Microsoft Zira Desktop")
    $synthesizer.Rate = -1
    $synthesizer.Volume = 100
    $synthesizer.SetOutputToWaveFile($rawPath)
    $synthesizer.Speak($Text)
    $synthesizer.Dispose()
    & ffmpeg -y -hide_banner -loglevel error -i $rawPath -af $filter -ar 44100 -ac 1 -c:a pcm_s16le $processedPath
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg voice processing failed with exit code $LASTEXITCODE" }
    return $processedPath
}

[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($tempDirectory) | Out-Null
Add-Type -AssemblyName System.Speech

try
{
    $processedCue = Join-Path $tempDirectory "cue.wav"
    & ffmpeg -y -hide_banner -loglevel error -i $cuePath -af "aformat=sample_rates=44100:channel_layouts=mono,areverse,silenceremove=start_periods=1:start_duration=0.08:start_threshold=-42dB,areverse" -ar 44100 -ac 1 -c:a pcm_s16le $processedCue
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg cue processing failed with exit code $LASTEXITCODE" }
    $line = New-Voice "Clear the area." "line"

    & ffmpeg -y -hide_banner -loglevel error -i $processedCue -i $line -filter_complex "[0:a]aformat=sample_rates=44100:channel_layouts=mono[cue];[1:a]aformat=sample_rates=44100:channel_layouts=mono[line];[cue][line]concat=n=2:v=0:a=1[out]" -map "[out]" -ar 44100 -ac 1 -c:a pcm_s16le $outputPath
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg concat failed with exit code $LASTEXITCODE" }

    Write-Output ("Cue: {0:N3}s" -f (Get-Duration $processedCue))
    Write-Output ("Line: {0:N3}s" -f (Get-Duration $line))
    Write-Output ("Final: {0:N3}s" -f (Get-Duration $outputPath))
    Write-Output $outputPath
}
finally
{
    if ([System.IO.Directory]::Exists($tempDirectory)) { Remove-Item -LiteralPath $tempDirectory -Recurse -Force }
}
