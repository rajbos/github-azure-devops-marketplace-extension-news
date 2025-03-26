Param(
    [Parameter(Mandatory = $true)]
    [string]$token,
    [Parameter(Mandatory = $true)]
    [string]$filePath
)

# load the dependents functions
. $PSScriptRoot/dependents.ps1
Write-Host "Got a token with length $($token.Length)"
Write-Host "Got this file path $($filePath)"
Write-Host "We are running from this location: $PSScriptRoot"
Get-Location
$repositoryUrl = "https://x:$token@github.com/devops-actions/github-actions-marketplace-news.git"

# load the json file from disk
$updates = Get-Content -Path $filePath | ConvertFrom-Json
if ($null -eq $updates) {
    Write-Host "No updates found"
    exit 0
}

# get the name of the new folder
$folderName = $repositoryUrl.Split("/")[-1].Split(".")[0]
# remove the folder if it already exists
if (Test-Path -Path $folderName) {
    Remove-Item -Path $folderName -Recurse -Force
}

# clone the git repository
git clone $repositoryUrl
# change into the repository
Set-Location $folderName

function GetBasicAuthenticationHeader() {
    Param (
        $access_token = $env:GITHUB_TOKEN
    )

    $CredPair = "x:$access_token"
    $EncodedCredentials = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($CredPair))

    return "Basic $EncodedCredentials";
}

function ApiCall {
    Param (
        $url,
        $access_token = $env:GITHUB_TOKEN
    )
    $headers = @{
        Authorization = GetBasicAuthenticationHeader -access_token $access_token
    }
    if ($null -ne $body) {
        $headers.Add('Content-Type', 'application/json')
        $headers.Add('User-Agent', 'rajbos')
    }
    $method = "GET"
    $result = Invoke-WebRequest -Uri $url -Headers $headers -Method $method -ErrorVariable $errvar -ErrorAction Continue
    $response = $result.Content | ConvertFrom-Json

    return $response
}

function GetReleaseBody {
    Param (
        [Parameter(Mandatory = $true)]
        [string] $repo,
        [Parameter(Mandatory = $true)]
        [string] $owner,
        [Parameter(Mandatory = $true)]
        [string] $tag
    )

    $url = "https://api.github.com/repos/$owner/$repo/releases/tags/$tag"
    try {
        $release = ApiCall -url $url -access_token $token
        return $release.body
    }
    catch {
        Write-Warning "Error getting release body for repo [$repo] and tag [$tag]"
        Write-Warning "$_"
        return ""
    }

}

function CreateBlogPost {
    Param (
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$update
    )

    if (!$update.RepoUrl || $update.RepoUrl -eq "") {
        Write-Warning "RepoUrl is empty for update [$update], skipping blogpost creation"
        return
    }

    $splitted = $update.RepoUrl.Replace(";", "").Replace("https://github.com/", "").Split("/")
    $owner = $splitted[0]
    $repo = $splitted[1]
    Write-Host "Found repo [$repo] and owner [$owner] for update [$update]"
    $dependentsNumber = GetDependentsForRepo -repo $repo -owner $owner

    $releaseBody = GetReleaseBody -repo $repo -owner $owner -tag $update.Version

    # create the file name based on the repo
    $fileName = "$((Get-Date).ToString("dd-HH"))-$owner-$repo"
    # get current date and split ISO representation into yyyy/MM
    $date = (Get-Date).ToString("yyyy/MM")
    Write-Host("This is the value of date [$date]")
    $fileName = "$($fileName).md"
    Write-Host("This is the value of fileName [$fileName]")
    # create the file in the following folder 'content/posts/yyyy/MM'
    $filePath = "content/posts/$($date)/$($fileName)"
    Write-Host("This is the value of filePath [$filePath]")
    # create the file
    New-Item -Path $filePath -ItemType File -Force | Out-Null
    # get the content to write into the file
    $content = GetContent -update $update -dependentsNumber "$dependentsNumber" -releaseBody $releaseBody
    # write the content into the file
    Set-Content -Path $filePath -Value $content
}

function SanitizeContent {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$content
    )

    return $content.Replace(":", "").Replace("@", "").Replace("""", "").Replace("'", "")
}

function GetContent {
    Param (
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$update,
        [Parameter(Mandatory = $true)]
        [string]$dependentsNumber,
        [Parameter(Mandatory = $true)]
        [string]$releaseBody
    )

    # write the content as a multiline array
    if ($dependentsNumber -eq "?") {
        $dependentsNumberString = "?"
    }
    else {
        $dependentsNumberString = $dependentsNumber
    }
    $content = @(
        "---"
        "title: $(SanitizeContent $update.Title)"
        "date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss +00:00")"
        "tags:"
        "  - $($update.Publisher)"
        "  - GitHub Actions"
        "draft: false"
        "repo: $($update.RepoUrl)"
        "marketplace: $($update.Url)"
        "version: $(SanitizeContent $update.Version)"
        "dependentsNumber: ""$dependentsNumberString"""
        "---"
        ""
    )
    # add an empty line
    $content += ""
    # add the content of the update
    if ($update.Version) {
        $content += "Version updated for **$($update.RepoUrl)** to version **$($update.Version)**."
    }

    if ($update.Verified) {
        $content += "- This publisher is shown as 'verified' by GitHub."
    }
    if ("" -ne $dependentsNumber) {
        $content += "- This action is used across all versions by **$dependentsNumber** repositories."
    }
    $content += ""
    $content += "Go to the [GitHub Marketplace]($($update.Url)) to find the latest changes."
    $content += ""
    if ($releaseBody -ne "") {
        $content += "## Release notes"
        $content += ""
        # convert the \r\n in the text to lines in the array
        foreach ($line in $releaseBody.Split("\r\n")) {
            $content += $line
        }
    }

    # return the content of the update
    return $content
}

# loop through the updates
# each entry will be in the following format:
# {
#     "Url": "https://github.com/marketplace/actions/webhook-trigger",
#     "Title": "Webhook Trigger",
#     "Publisher": "zzzze",
#     "Version": "v1.0.0",
#     "Updated": "2020-12-09T17:25:39.0724078Z",
#     "RepoUrl": "zzzze/webhook-trigger"
#   }
$counter = 0
foreach ($update in $updates) {
    # create the file name
    try {
        CreateBlogPost -update $update
        # sleep 2 seconds
        Start-Sleep -Seconds 2
        $counter++
    }
    catch {
        Write-Host "Error creating blog post for update [$update]"
        Write-Host "$_"
    }
}

# show what we did
Write-Host "Created [$counter] blog posts"
# also write to the step summary file
$summaryFile = "$GITHUB_STEP_SUMMARY"
if (Test-Path -Path $summaryFile) {
    Add-Content -Path $summaryFile -Value "Created [$counter] blog posts"
}

# use git porcelain to check if there are any changes
$changes = git status --porcelain
# if there are changes
if ($changes) {
    # configure git user
    git config --global user.email "bot@github-actions.com"
    git config --global user.name "github-actions"
    # add all changes
    git add .
    # commit the changes
    git commit -m "Update blog posts for $(Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")"
    # push the changes
    git push
}
