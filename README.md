# Azure DevOps and GitHub Marketplace Extension/Actions News

.NET Core tool to check the Azure DevOps API and scrape the GitHub Marketplace for new and updated extensions.

Build and runs with GitHub Actions:

## Reason for this project
The reason I started with this project is that I always wondered if there could be a way to stay up to date on Azure DevOps extensions or GitHub Actions on the Marketplace: there are many extensions available in it and if you don’t check the marketplace regularly, you can easily miss some gems. Of course, when you encounter a specific problem, you will probably find the extensions when you need them, but I thought this was a fun thing to do.

Searching around did seemed to prove there isn’t a good way to stay up to date on the extensions: there is no RSS feed, bot, or any other option than to regularly check the “Most recent” feed. Since I still am a developer, I thought I could probably make something myself and that this should be a fun thing to do that should not cost to much time to make!

More information can be found in this [blogpost](https://rajbos.github.io/blog/2019/08/16/AzDoMarketplaceNews).

The basic setup is as follows:
- Run through the Azure DevOps API
- Scrape through the pages on the GitHub Marketplace
- Scan organization repositories for actions not published to the marketplace
- Store all the results as json in Azure Blob storage
- On finding new items, post about it.

The posting to social media part has been disabled, as Twitter is no longer a safe place to post things and I will not promote it anymore. Haven't had time or inclination to set this up on Mastodon yet. 

Posting for the GitHub Actions news goes to https://devops-actions.github.io/github-actions-marketplace-news, which can be followed through an RSS feed. The source repository for that blog is here: https://github.com/devops-actions/github-actions-marketplace-news

### News publishing credentials
The `PostNews` job reads the repository Actions secret [`PAT`](https://github.com/rajbos/github-azure-devops-marketplace-extension-news/settings/secrets/actions/PAT) through the `NEWS_PUBLISH_TOKEN` environment variable. It must have Contents read/write access to `devops-actions/github-actions-marketplace-news`; a fine-grained token must select that repository and receive any required organization approval. Classic tokens need the appropriate repository scope and any required SSO authorization. The source workflow's `GITHUB_TOKEN` cannot grant write access to this separate repository; it is used only for release, commit, and README API reads.

The public news repository is cloned without credentials. A dry-run push checks the Git write endpoint before generating posts, and missing credentials fail before cloning. Push authentication is supplied through process-only Git configuration, not command arguments, remote URLs, or `.git/config`. Do not interpolate secrets into PowerShell `run` blocks: error source-line rendering can expose them. If authentication fails, the repository owner must replace the `PAT` secret with a valid authorized credential. Secret update age alone does not prove token expiration. If a token has appeared in logs, treat it as compromised and rotate it; changing the scripts does not revoke it or remove historical logs.

To eliminate the long-lived PAT, an owner can install a GitHub App on the news repository with Contents read/write permission and configure an App ID and private key for this workflow to mint short-lived installation tokens. That requires owner-managed App setup; the current workflow does not have that infrastructure.

Run focused publishing regression checks with `pwsh -NoProfile -File .github/workflows/tests/publishing.Tests.ps1`. They use only synthetic credentials and a temporary local Git remote, without scraping, publishing news, or contacting GitHub.

To test the actual stored classic `PAT`, manually dispatch **Check repository PAT** on `main`. It checks authentication, classic repository scope, repository push access, and unchanged default-branch dry-run pushes to both repositories without scraping or modifying repository data. Pull-request creation permission is inferred from scope/access, not exercised; changed commits may still be rejected by branch rules.

### Organization Actions Scanning
In addition to marketplace actions, the tool also scans specific GitHub organizations for actions that may not be published to the marketplace. This includes:
- github
- actions
- mona-actions
- dependabot

The workflow uses [devops-actions/load-available-actions](https://github.com/devops-actions/load-available-actions) to discover actions in these organizations, fetches the latest semver tags for version information, deduplicates against marketplace actions, and stores the results in blob storage for tracking updates.

## Building the solution
``` shell
dotnet restore AzDoExtensionNews/AzDoExtensionNews.sln --locked-mode
dotnet build AzDoExtensionNews/AzDoExtensionNews.sln
```

If the restore fails with changed dependencies, consider to update the lock files after carefully reviewing the changes:
``` shell
dotnet restore AzDoExtensionNews/AzDoExtensionNews.sln --use-lock-file --force-evaluate
```

**Note for Dependabot PRs:** The CI build is configured to automatically handle lock file updates for Dependabot PRs. When Dependabot updates dependencies in `Directory.Packages.props` or project files (`.csproj`), the build process will automatically update the corresponding `packages.lock.json` files to maintain security while preventing build failures.

### Automated NU1004 Error Resolution
This repository includes automated workflows to detect and resolve NU1004 errors that occur when project reference dependencies change (e.g., when News.Library adds new package dependencies):

1. **Proactive Prevention** (`update-lock-files-dependabot.yml`):
   - Automatically updates all package lock files when Dependabot modifies `.csproj` files or `Directory.Packages.props`
   - Commits lock file updates directly to Dependabot PRs
   - Prevents NU1004 errors from occurring in the first place

2. **Reactive Fix** (`fix-nu1004-errors.yml`):
   - Monitors CI Build workflow failures
   - Detects NU1004 errors in build logs
   - Automatically updates lock files and creates a PR with the fix
   - Can also be manually triggered via workflow dispatch

These workflows ensure that transitive dependencies from project references (especially News.Library) are properly reflected in dependent projects' lock files.

## Running the solution
`dotnet run --project GitHubActionsNews` or just hit F5 in Visual Studio / Code.

