<div align="center">
  <img src="Assets/gh-dc-wdget-logo.png" alt="Discord GitHub Widget logo" width="300">

  <h1>Discord GitHub Widget</h1>

  <p>Unofficial GitHub activity widget for Discord</p>

  <p>
    <a href="https://github.com/Kiruyuto/discord-github-widget/actions/workflows/ci.yml">
      <img src="https://github.com/Kiruyuto/discord-github-widget/actions/workflows/ci.yml/badge.svg?event=push" alt="CI status">
    </a>
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
    <img src="https://img.shields.io/badge/Discord%20Lib-NetCord-5865F2?logo=discord&logoColor=white" alt="NetCord">
  </p>
</div>

## Preview

<table>
  <tr>
    <td width="68%">
      <img src="Docs/Images/FullProfile.png" alt="Full Discord profile showing the GitHub widget">
    </td>
    <td width="32%">
      <img src="Docs/Images/MiniProfile.png" alt="Compact Discord profile showing the GitHub widget">
    </td>
  </tr>
</table>

## What It Does

Fetches public GitHub profile stats and displays them in a Discord profile widget.

This particular widget shows:

- GitHub handle, avatar, display name, and bio
- Total contributions
- Total stars across owned repositories
- Public repository count
- Top repository language
- Followers and following counts

## Add It to Your Profile

Discord changed how widgets work recently, so you must either own the application or be part of the team that owns it before you can add the widget to your profile.

That means you cannot simply authorize someone else's hosted application and use it directly.  
**The good news is you can follow [Setup.md](./Docs/Setup.md) to create your own Discord application.**

For more context, see the Discord Previews message below:
[Discord Previews announcement](https://discord.com/channels/603970300668805120/983619277531779082/1511885826408189952)

![WidgetsUpdate](./Docs/Images/WidgetsUpdate.png)

## Development

Restore, build, and test from the repository root:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-restore --no-build
```

Run the same formatting check used by CI:

```bash
# Check formatting:
dotnet format --no-restore --verbosity diagnostic --severity info
# Verify formatting:
dotnet format --no-restore --verbosity diagnostic --severity info --verify-no-changes
```
