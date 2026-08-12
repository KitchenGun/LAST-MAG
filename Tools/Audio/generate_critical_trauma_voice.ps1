$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$outputDirectory = Join-Path $projectRoot "Assets\Resources\Audio\SystemVoice"
$outputPath = Join-Path $outputDirectory "SFX_CriticalTraumaDetected.wav"
$rawPath = Join-Path ([System.IO.Path]::GetTempPath()) "gulag-critical-trauma-raw.wav"

[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
Add-Type -AssemblyName System.Speech

$synthesizer = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synthesizer.SelectVoice("Microsoft Zira Desktop")
$synthesizer.Rate = -1
$synthesizer.Volume = 100
$synthesizer.SetOutputToWaveFile($rawPath)
$synthesizer.Speak("Critical trauma detected.")
$synthesizer.Dispose()

& ffmpeg -y -hide_banner -loglevel error -i $rawPath -af "highpass=f=120,lowpass=f=5200,acompressor=threshold=0.12:ratio=4:attack=5:release=80,acrusher=bits=12:mix=0.12,aecho=0.8:0.18:38:0.10,loudnorm=I=-16:TP=-1.5:LRA=5" -ar 44100 -ac 1 -c:a pcm_s16le $outputPath
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE" }

if ([System.IO.File]::Exists($rawPath))
{
    [System.IO.File]::Delete($rawPath)
}

Write-Output $outputPath
