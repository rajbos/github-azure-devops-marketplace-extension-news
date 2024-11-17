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
- Store all the results as json in Azure Blob storage
- On finding new items, post about it.

The posting to social media part has been disabled, as Twitter is no longer a safe place to post things and I will not promote it anymore. Haven't had time or inclination to set this up on Mastodon yet. 

Posting for the GitHub Actions news goes to https://devops-actions.github.io/github-actions-marketplace-news, which can be followed through an RSS feed. The source repository for that blog is here: https://github.com/devops-actions/github-actions-marketplace-news

## Building the solution
`dotnet build`

## Running the solution
`dotnet run --project GitHubActionsNews` or just hit F5 in Visual Studio / Code.
