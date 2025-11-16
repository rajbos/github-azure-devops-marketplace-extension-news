# GitHub Copilot Custom Instructions

## Project Overview

This is a .NET Core tool that checks the Azure DevOps API and scrapes the GitHub Marketplace for new and updated extensions/actions. The project monitors marketplace updates and publishes news about new and updated extensions.

## Architecture

### Solution Structure
The solution (`AzDoExtensionNews.sln`) contains five projects:

1. **AzDoExtensionNews** - Main console application for Azure DevOps extension news
2. **GitHubActionsNews** - Console application for GitHub Actions marketplace news
3. **News.Library** - Shared library containing core functionality
4. **AzDoExtensionNews.UnitTests** - Unit tests for AzDoExtensionNews
5. **GitHubActionsNews.Tests** - Unit tests for GitHubActionsNews

### Key Dependencies
- Uses central package management via `Directory.Packages.props`
- Locked mode dependency restoration for security
- Major dependencies include:
  - Microsoft.Azure.Storage.Blob for Azure blob storage (note: this is a legacy package; Azure.Storage.Blobs is recommended for new development)
  - Microsoft.Extensions.Configuration for configuration management
  - Newtonsoft.Json for JSON serialization
  - Microsoft.Playwright for web scraping
  - NUnit/MSTest for testing

## Development Guidelines

### Building the Solution

```bash
# Restore dependencies in locked mode (recommended)
dotnet restore AzDoExtensionNews/AzDoExtensionNews.sln --locked-mode

# If dependencies have changed, update lock files
dotnet restore AzDoExtensionNews/AzDoExtensionNews.sln --use-lock-file --force-evaluate

# Build the solution
dotnet build AzDoExtensionNews/AzDoExtensionNews.sln

# Install Playwright browsers (required for running tests)
pwsh AzDoExtensionNews/GitHubActionsNews.Tests/bin/Debug/net8.0/playwright.ps1 install

# Run tests
dotnet test AzDoExtensionNews/AzDoExtensionNews.sln
```

### Running the Applications

```bash
# Run GitHub Actions News
dotnet run --project AzDoExtensionNews/GitHubActionsNews/GitHubActionsNews.csproj

# Run Azure DevOps Extension News
dotnet run --project AzDoExtensionNews/AzDoExtensionNews/AzDoExtensionNews.csproj
```

## Important Notes for Code Changes

### Dependency Management
- **Always use locked mode for restores** to maintain security
- Dependencies are centrally managed in `Directory.Packages.props`
- Package lock files (`packages.lock.json`) must be updated when dependencies change
- Dependabot PRs automatically handle lock file updates via CI workflow

### Testing
- Unit tests use NUnit and MSTest frameworks
- Tests for GitHub Actions news use Playwright for browser automation
- **Important**: Playwright browsers must be installed before running tests (see build instructions)
- Run tests after any code changes to ensure nothing breaks
- Test projects follow the naming convention `{ProjectName}.Tests` or `{ProjectName}.UnitTests`

### CI/CD
- The CI build runs on all pushes and pull requests
- Build process: restore → build → test → publish
- Two artifacts are published: `AzDoNews` and `GitHubNews`
- Security scanning with CodeQL is enabled
- Dependency review workflow checks for vulnerabilities

### Code Quality
- Project targets .NET 8.0, so use C# 12.0+ features where appropriate
- Follow existing code patterns and conventions in the repository
- Ensure proper error handling and logging
- Keep shared functionality in `News.Library` project

### Configuration
- Configuration is managed via JSON files and environment variables
- Sensitive data should use secrets management (Azure Storage connection strings, etc.)
- Files matching `*.secrets.json` are gitignored

## Common Tasks

### Adding a New Dependency
1. Update the version in `Directory.Packages.props`
2. Add `PackageReference` (without version) to the relevant `.csproj` file
3. Run `dotnet restore --use-lock-file --force-evaluate` to update lock files
4. Commit both `Directory.Packages.props` and the updated `packages.lock.json` files

### Adding New Features
1. Identify if the feature belongs in the shared library or a specific project
2. Add corresponding unit tests in the appropriate test project
3. Update README.md if the feature changes usage or behavior
4. Ensure CI build passes before submitting PR

### Updating GitHub Actions
- GitHub Actions versions are managed by Dependabot
- Changes are grouped in monthly updates
- Pin action versions using full commit SHA for security

## Project-Specific Context

- **Social media posting** has been disabled for Twitter; RSS feeds are used instead
- **GitHub Actions news** is published to https://devops-actions.github.io/github-actions-marketplace-news
- Results are stored as JSON in Azure Blob storage
- Web scraping uses Microsoft.Playwright with Chromium browser
- The project uses DevContainers for consistent development environments
