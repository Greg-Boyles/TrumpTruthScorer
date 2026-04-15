param(
    [ValidateSet('full', 'ingest', 'analyze', 'summary')]
    [string]$Mode = 'full',
    [string]$StartDate = '',
    [string]$EndDate = '',
    [int]$MaxPages = 20,
    [int]$MaxAnalysisPosts = 100,
    [string]$ResumeCursor = '',
    [string]$Handle = 'realDonaldTrump',
    [string]$FunctionName = 'TruthScorer-Backfill',
    [string]$Region = 'eu-west-1'
)

$payload = @{
    mode = $Mode
    handle = $Handle
    maxPages = $MaxPages
    maxAnalysisPosts = $MaxAnalysisPosts
}

if ($StartDate) { $payload.startDate = $StartDate }
if ($EndDate) { $payload.endDate = $EndDate }
if ($ResumeCursor) { $payload.resumeCursor = $ResumeCursor }

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$payloadPath = Join-Path $env:TEMP 'truthscorer-backfill-request.json'
$outputPath = Join-Path $env:TEMP 'truthscorer-backfill-response.json'
[System.IO.File]::WriteAllText($payloadPath, ($payload | ConvertTo-Json -Depth 10), $utf8NoBom)

$env:AWS_REGION = $Region
aws lambda invoke `
  --function-name $FunctionName `
  --cli-binary-format raw-in-base64-out `
  --payload "fileb://$payloadPath" `
  $outputPath | Out-Null

Get-Content $outputPath -Raw
