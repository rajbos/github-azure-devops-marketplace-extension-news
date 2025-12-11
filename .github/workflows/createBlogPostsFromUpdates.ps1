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

function ConvertToSlug {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$text
    )
    
    # Convert to lowercase
    $slug = $text.ToLower()
    
    # Replace spaces and common separators with hyphens
    $slug = $slug -replace '[\s_]+', '-'
    
    # Remove special characters, keeping only alphanumeric and hyphens
    $slug = $slug -replace '[^a-z0-9\-]', ''
    
    # Remove multiple consecutive hyphens
    $slug = $slug -replace '\-{2,}', '-'
    
    # Remove leading and trailing hyphens
    $slug = $slug.Trim('-')
    
    return $slug
}

function GetContent {
    Param (
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$update,
        [Parameter(Mandatory = $true)]
        [string]$dependentsNumber,
        [Parameter(Mandatory = $false)]
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
    )
    if ($update.ActionType) {
        $content += "actionType: $(SanitizeContent $update.ActionType)"
    }
    if ($update.NodeVersion) {
        $content += "nodeVersion: $(SanitizeContent $update.NodeVersion)"
    }
    $content += "---"
    $content += ""
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
    if ($update.ActionType) {
        $content += ""
        $content += "## Action Type"
        $actionTypeInfo = "This is a **$($update.ActionType)** action"
        if ($update.ActionType -eq "Node" -and $update.NodeVersion) {
            $actionTypeInfo += " using Node version **$($update.NodeVersion)**"
        }
        $actionTypeInfo += "."
        $content += $actionTypeInfo
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

# use git porcelain to check if there are any changes
$changes = git status --porcelain
# if there are changes
if ($changes) {
    # configure git user
    git config --global user.email "bot@github-actions.com"
    git config --global user.name "github-actions"
    # add all changes
    git add .
    # commit the changes and capture the output
    $commitOutput = git commit -m "Update blog posts for $(Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")" 2>&1
    Write-Host $commitOutput
    
    # push the changes
    git push
    
    # Parse the commit output to extract created blog post files
    $blogPostLinks = @()
    $commitOutputLines = $commitOutput -split '\r?\n'
    foreach ($line in $commitOutputLines) {
        # Look for lines that start with "create mode" and contain blog post files
        if ($line -match "create mode \d+ (content/posts/.+\.md)$") {
            $filePath = $matches[1].Trim()
            # Pattern: content/posts/yyyy/MM/dd-HH-owner-repo.md
            # Hugo generates URLs based on the title field in front matter, not the filename
            if ($filePath -match "content/posts/(\d{4})/(\d{2})/(\d{2})-\d{2}-(.+)\.md$") {
                $year = $matches[1]
                $month = $matches[2]
                $day = $matches[3]
                $fileNameSlug = $matches[4].ToLower()
                $slug = $null
                
                # Read the file to extract the title from front matter
                if (Test-Path $filePath) {
                    try {
                        $fileContent = Get-Content -Path $filePath -Raw
                        # Extract title from front matter (format: "title: Title Text")
                        if ($fileContent -match '(?m)^title:\s*(.+)$') {
                            $title = $matches[1].Trim()
                            # Remove any surrounding quotes that might be in the title
                            $title = $title.Trim('"', "'")
                            # Convert title to slug (same way Hugo does it)
                            $slug = ConvertToSlug -text $title
                        }
                        else {
                            # Title not found, will use fallback
                            Write-Warning "Could not extract title from $filePath, using filename for URL"
                        }
                    }
                    catch {
                        # Error reading file, will use fallback
                        Write-Warning "Error reading file $filePath : $_"
                    }
                }
                else {
                    # File doesn't exist (shouldn't happen but handle it)
                    Write-Warning "File not found: $filePath, using filename for URL"
                }
                
                # Use filename-based slug as fallback if title extraction failed
                if (-not $slug) {
                    $slug = $fileNameSlug
                }
                
                # Generate the blog URL
                $blogUrl = "https://devops-actions.github.io/github-actions-marketplace-news/blog/$year/$month/$day/$slug/"
                $blogPostLinks += $blogUrl
            }
        }
    }
    
    # Write to the step summary file
    $summaryFile = $env:GITHUB_STEP_SUMMARY
    if ($summaryFile -and (Test-Path -Path $summaryFile)) {
        Add-Content -Path $summaryFile -Value "Created [$counter] blog posts"
        Add-Content -Path $summaryFile -Value ""
        if ($blogPostLinks.Count -gt 0) {
            Add-Content -Path $summaryFile -Value "## Blog Post Links"
            Add-Content -Path $summaryFile -Value ""
            foreach ($link in $blogPostLinks) {
                Add-Content -Path $summaryFile -Value "- $link"
            }
        }
    }
}
else {
    # Write to the step summary file even if no changes
    $summaryFile = $env:GITHUB_STEP_SUMMARY
    if ($summaryFile -and (Test-Path -Path $summaryFile)) {
        Add-Content -Path $summaryFile -Value "Created [$counter] blog posts"
    }
}
