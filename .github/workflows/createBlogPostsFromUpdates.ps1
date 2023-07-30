Param(
    [Parameter(Mandatory=$true)]
    [string]$token
)
$repositoryUrl = "https://x:$token@github.com/devops-actions/azure-devops-extension-news.git"
# load the json file from disk
$updates = Get-Content -Path ./ArtifactFolder/Actions-Updated-Overview.json | ConvertFrom-Json

Wrie-Host "Got a token with length $($token.Length)"

# clone the git repository
git clone $repositoryUrl
# get the name of the new folder
$folderName = $repositoryUrl.Split("/")[-1].Split(".")[0]
# change into the repository
Set-Location $folderName

function CreateBlogPost{
    Param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$update
    )
    # create the file name based on the repoUrl
    $splitted = $update.RepoUrl.Split("/")
    $fileName = "$((Get-Date).ToString("dd"))-$($splitted[0])-$($splitted[1])"
    # get current date and split ISO representation into yyyy/MM/dd
    $date = (Get-Date).ToString("yyyy/MM")
    $fileName = "$($date)-$($fileName).md"
    # create the file in the following folder 'content/posts/yyyy/MM'
    $filePath = "content/posts/$($date)/$($fileName)"
    # create the file
    New-Item -Path $filePath -ItemType File -Force
    # get the content to write into the file
    $content = GetContent -update $update
    # write the content into the file
    Set-Content -Path $filePath -Value $content
}

function GetContent {
    Params (
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$update
    )
    
    # write the content as a multiline array
    $content = @(
        "---"
        "title: $($update.Title)"
        "date: $(Get-Date -Format yyyy-MM-ddTHH:mm:ssZ)"
        "tags:"
        "  - $($update.Publisher)"
        "  - GitHub Actions"
        "draft: false"
        "---"
        ""
    )
    # add an empty line
    $content += ""
    # add the content of the update
    if ($update.Version) {
        $content += "Version updated for $($update.RepoUrl) to version $($update.Version)"
    }
    $content += ""
    $content += "Go to the [GitHub Marketplace]($($update.Url)) to find the latest changes."
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
foreach ($update in $updates) {
    # create the file name
    CreateBlogPost -update $update
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
    git commit -m "Update blog posts for $(Get-Date -Format yyyy-MM-ddTHH:mm:ssZ)"
    # push the changes
    git push
}