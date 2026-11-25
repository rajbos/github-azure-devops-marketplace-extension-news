$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '..\createBlogPostsFromUpdates.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) {
    throw "Publishing script has syntax errors: $parseErrors"
}

function Assert($condition, $message) {
    if (-not $condition) { throw $message }
}

$pushFunction = $ast.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'PushNews'
}, $true)
. ([scriptblock]::Create($pushFunction.Extent.Text))

# Only synthetic credentials are used, and no network requests are made.
$settingsNames = @(
    'NEWS_PUBLISH_TOKEN', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0',
    'GIT_CONFIG_KEY_1', 'GIT_CONFIG_VALUE_1', 'GIT_CONFIG_KEY_2', 'GIT_CONFIG_VALUE_2',
    'GIT_TERMINAL_PROMPT', 'GIT_TRACE_REDACT'
)
$original = @{}
foreach ($name in $settingsNames) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$tempDirectory = Join-Path ([IO.Path]::GetTempPath()) "news-publishing-test-$([guid]::NewGuid())"
$originalLocation = Get-Location
$gitExecutable = (Get-Command git -CommandType Application | Select-Object -First 1).Source
$repositoryRoot = & $gitExecutable rev-parse --show-toplevel
try {
    $env:NEWS_PUBLISH_TOKEN = 'synthetic-publishing-credential'
    $env:GIT_CONFIG_COUNT = '1'
    $env:GIT_CONFIG_KEY_0 = 'test.previous'
    $env:GIT_CONFIG_VALUE_0 = 'preserve-me'
    $beforePush = @{}
    foreach ($name in $settingsNames) {
        $beforePush[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    function git {
        $expectedArguments = if ($script:validateAccess) { 'push --dry-run' } else { 'push' }
        Assert (($args -join ' ') -eq $expectedArguments) 'Push must not pass authentication in command arguments.'
        Assert ($env:GIT_CONFIG_COUNT -eq '3') 'Push must use process-only Git configuration.'
        Assert ($env:GIT_CONFIG_KEY_0 -eq 'credential.helper' -and $env:GIT_CONFIG_VALUE_0 -eq '') 'Credential helpers must be disabled.'
        Assert ($env:GIT_CONFIG_VALUE_1 -eq '') 'Existing authorization headers must be reset.'
        Assert ($env:GIT_CONFIG_KEY_2 -eq 'http.https://github.com/devops-actions/github-actions-marketplace-news.git.extraheader') 'Authorization must be scoped to the news repository.'
        $expected = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("x:$env:NEWS_PUBLISH_TOKEN"))
        Assert ($env:GIT_CONFIG_VALUE_2 -eq "AUTHORIZATION: basic $expected") 'Push authorization is incorrect.'
        Assert ($env:GIT_TERMINAL_PROMPT -eq '0') 'Push must not prompt for credentials.'
        Assert ($env:GIT_TRACE_REDACT -eq '1') 'Git trace must redact authorization headers.'
        if ($script:throwFromGit) { throw "Synthetic command exception: $env:NEWS_PUBLISH_TOKEN" }
        $global:LASTEXITCODE = $script:gitExitCode
        "Simulated remote output containing $env:NEWS_PUBLISH_TOKEN and $expected"
    }
    $script:validateAccess = $true
    $script:throwFromGit = $true
    $failure = $null
    try { PushNews -ValidateAccess } catch { $failure = $_ }
    Assert ($null -ne $failure -and $failure.ToString() -match 'Could not execute') 'Command exceptions must be surfaced.'
    Assert ($failure.ToString() -notmatch 'synthetic-publishing-credential') 'Command exceptions must be sanitized.'
    foreach ($name in $settingsNames) {
        Assert ([Environment]::GetEnvironmentVariable($name, 'Process') -ceq $beforePush[$name]) "Command exception did not restore $name."
    }
    $script:throwFromGit = $false

    foreach ($testCase in @(
        @{ ExitCode = 0; ValidateAccess = $false },
        @{ ExitCode = 128; ValidateAccess = $false },
        @{ ExitCode = 0; ValidateAccess = $true },
        @{ ExitCode = 128; ValidateAccess = $true }
    )) {
        $exitCode = $testCase.ExitCode
        $script:validateAccess = $testCase.ValidateAccess
        $script:gitExitCode = $exitCode
        $failure = $null
        $output = try { PushNews -ValidateAccess:$script:validateAccess 6>&1 } catch { $failure = $_ }
        Assert (($output | Out-String) -notmatch 'synthetic-publishing-credential') 'Push output leaked the credential.'
        if ($exitCode -eq 0) {
            Assert ($null -eq $failure) 'Successful push must succeed.'
        }
        else {
            Assert ($null -ne $failure) 'Failed push must terminate.'
            Assert ($failure.ToString() -match 'Git exit code: 128') 'Failure must retain the exit code.'
            Assert ($failure.ToString() -notmatch 'synthetic-publishing-credential|Simulated remote') 'Failure must not echo raw Git output.'
            if ($script:validateAccess) {
                Assert ($failure.ToString() -match 'validate news publishing access') 'Preflight failure must explain that publishing access could not be validated.'
            }
        }
        foreach ($name in $settingsNames) {
            Assert ([Environment]::GetEnvironmentVariable($name, 'Process') -ceq $beforePush[$name]) "Push did not restore $name."
        }
    }
    $env:NEWS_PUBLISH_TOKEN = ''
    $failure = $null
    try { & $scriptPath -filePath 'unused-for-missing-credential.json' | Out-Null } catch { $failure = $_ }
    Assert ($null -ne $failure -and $failure.ToString() -match 'Set NEWS_PUBLISH_TOKEN') 'Missing credential must fail before cloning or loading updates.'
    $env:NEWS_PUBLISH_TOKEN = $beforePush.NEWS_PUBLISH_TOKEN
    Remove-Item Function:\git

    # Exercise real Git configuration parsing and push to a disposable local remote.
    New-Item -Path $tempDirectory -ItemType Directory | Out-Null
    Set-Location $tempDirectory
    & $gitExecutable init --bare --quiet remote.git
    Assert ($LASTEXITCODE -eq 0) 'Could not initialize test remote.'
    & $gitExecutable init --quiet working
    Set-Location working
    & $gitExecutable fetch --quiet $repositoryRoot HEAD
    Assert ($LASTEXITCODE -eq 0) 'Could not fetch existing commit for the local push test.'
    & $gitExecutable checkout --quiet -b publishing-regression FETCH_HEAD
    & $gitExecutable remote add origin (Join-Path $tempDirectory 'remote.git')
    & $gitExecutable config push.default current
    PushNews -ValidateAccess
    & $gitExecutable --git-dir (Join-Path $tempDirectory 'remote.git') show-ref --quiet
    Assert ($LASTEXITCODE -eq 1) 'Preflight must not modify the remote.'
    PushNews
    $persistedConfig = Get-Content .git\config -Raw
    Assert ($persistedConfig -notmatch 'extraheader|synthetic-publishing-credential|AUTHORIZATION') 'Authentication was persisted in Git configuration.'
    & $gitExecutable --git-dir (Join-Path $tempDirectory 'remote.git') rev-parse --verify refs/heads/publishing-regression | Out-Null
    Assert ($LASTEXITCODE -eq 0) 'Real Git push did not update the local remote.'

    $workflow = Get-Content (Join-Path $PSScriptRoot '..\deploy.yml') -Raw
    Assert ($workflow -match 'NEWS_PUBLISH_TOKEN: \$\{\{ secrets.PAT \}\}') 'Workflow must provide PAT through environment.'
    Assert ($workflow -notmatch '-token \$\{\{ secrets.PAT \}\}') 'Workflow must not interpolate PAT into the script.'
    $source = $ast.Extent.Text
    Assert ($source -notmatch 'https://x:|git config --local http\.|-access_token \$token') 'Publishing script retains unsafe credential handling.'
    Assert ($source.IndexOf('PushNews -ValidateAccess') -lt $source.IndexOf('foreach ($update in $updates)')) 'Access validation must precede post generation.'
    Write-Host 'Publishing regression checks passed (mocked success/failure and real local Git push).'
}
finally {
    Set-Location $originalLocation
    if (Test-Path Function:\git) { Remove-Item Function:\git }
    foreach ($name in $original.Keys) {
        if ($null -eq $original[$name]) {
            if (Test-Path "Env:\$name") { Remove-Item "Env:\$name" }
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process')
        }
    }
    if (Test-Path $tempDirectory) { Remove-Item -LiteralPath $tempDirectory -Recurse -Force }
}
