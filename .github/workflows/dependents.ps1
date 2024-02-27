function GetDependentsForRepo {
    Param (
        [Parameter(Mandatory=$true)]
        [string]$repo,
        [Parameter(Mandatory=$true)]
        [string]$owner
    )

    try {
        # make the request
        $url = "https://github.com/$owner/$repo/network/dependents"
        $content = Invoke-WebRequest -Uri $url -UseBasicParsing

        # find the text where it says "10 repositories"
        $regex = [regex]"\d*(,\d{1,3})*\s*\n\s*Repositories"
        $myMatches = $regex.Matches($content.Content)
        foreach ($match in $myMatches) {
            if ($match.Value -ne "") {
                $found = $match.Value.Replace(" ", "").Replace("`n", "").Replace("Repositories", "")
                if ($found -ne "") {
                    return $found
                }
            }
        }
        # check for regex matches
        if ($myMatches.Count -eq 1) {
            # replace all spaces with nothing
            $found = $myMatches[0].Value.Replace(" ", "").Replace("`n", "").Replace("Repositories", "")
            Write-Debug "Found match: $found"

            return $found
        }
        else {
            Write-Debug "Found $($myMatches.Count) matches for owner [$owner] and repo [$repo]: https://github.com/$owner/$repo/network/dependents"
            return "?"
        }
    }
    catch {
        Write-Host "Error loading dependents for owner [$owner] and repo [$repo]:"
        Write-Host "$_"
        return "?"
    }
}

function main {
    $repo = "satis-to-artifact-action"
    $owner = "mattgrul"
    $dependents = GetDependentsForRepo -repo $repo -owner $owner
    Write-Host "Dependents: $dependents repositories"
}
