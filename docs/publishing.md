# Publishing to NuGet

This document describes how to publish **ElBruno.Connectors.SqliteVec** to NuGet using GitHub Actions with Trusted Publishing.

## Package

| Package | NuGet |
|---------|-------|
| `ElBruno.Connectors.SqliteVec` | [![NuGet](https://img.shields.io/nuget/v/ElBruno.Connectors.SqliteVec.svg?style=flat-square)](https://www.nuget.org/packages/ElBruno.Connectors.SqliteVec) |

## NuGet Trusted Publishing Setup

1. Go to [nuget.org](https://www.nuget.org) → Sign in → Account Settings → **API keys**
2. Create a new API key scoped to the `ElBruno.Connectors.SqliteVec` package (or use a Trusted Publisher configuration)
3. Note the key for use as a GitHub secret

## GitHub Repository Setup

### Create the `release` Environment

1. Go to **Settings** → **Environments** → **New environment**
2. Name it `release`
3. (Optional) Add required reviewers for release approval
4. (Optional) Restrict to the `main` branch

### Add the `NUGET_USER` Secret

1. Go to **Settings** → **Environments** → `release` → **Environment secrets**
2. Add secret `NUGET_USER` with your NuGet API key value

## Publishing via GitHub Release (Recommended)

1. Push your changes to `main`
2. Go to **Releases** → **Draft a new release**
3. Create a new tag following semver (e.g., `v0.2.0`)
4. Set the release title (e.g., `v0.2.0`)
5. Add release notes
6. Click **Publish release**

The `publish.yml` workflow will automatically:

- Strip the `v` prefix from the tag to get the version
- Build and test the solution
- Pack the NuGet package with that version
- Push to nuget.org

## Manual Dispatch

You can also trigger the workflow manually:

1. Go to **Actions** → **Publish to NuGet** → **Run workflow**
2. Optionally enter a version (leave empty to use the version in the `.csproj`)
3. Click **Run workflow**

## Version Resolution Priority

The workflow determines the package version in this order:

| Priority | Source | Example |
|----------|--------|---------|
| 1 | GitHub Release tag (with `v` prefix stripped) | Tag `v1.2.3` → version `1.2.3` |
| 2 | Manual dispatch `version` input | Input `1.2.3-preview` → version `1.2.3-preview` |
| 3 | `<Version>` in `src/ElBruno.Connectors.SqliteVec/ElBruno.Connectors.SqliteVec.csproj` | `<Version>0.1.0</Version>` → version `0.1.0` |

---

**Author**: Bruno Capuano ([@elbruno](https://github.com/elbruno)) | [Blog](https://elbruno.com) | [YouTube](https://www.youtube.com/elbruno)

---

**Author**: Bruno Capuano ([@elbruno](https://github.com/elbruno)) | [elbruno.com](https://elbruno.com)
