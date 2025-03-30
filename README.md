# KiteConnectJugaad

[![GitHub release](https://img.shields.io/github/v/release/pishangujeniya/KiteConnectJugaad)](https://github.com/pishangujeniya/KiteConnectJugaad/releases)
[![Issues](https://img.shields.io/github/issues/pishangujeniya/KiteConnectJugaad)](https://github.com/pishangujeniya/KiteConnectJugaad/issues)
[![Last Commit](https://img.shields.io/github/last-commit/pishangujeniya/KiteConnectJugaad)](https://github.com/pishangujeniya/KiteConnectJugaad/commits/main)

`KiteConnectJugaad` is a wrapper around the official [dotnetkiteconnect](https://github.com/zerodha/dotnetkiteconnect) .NET SDK. This project overrides certain methods to allow usage via username and password instead of the API key.

---

## Disclaimer

- **This is not an official SDK** and we are **not affiliated with Kite Connect or Zerodha** in any way.
- Use this package at your own risk. We are not responsible for any issues, losses, or damages that may arise from using this package.

---

## Features

- Overrides methods in the official KiteConnect SDK to enable login via username and password.
- Uses the official SDK as a submodule to ensure compatibility with the original implementation.

---

## Installation

### Using NuGet Package Manager

```sh
dotnet add package KiteConnectJugaad --source "https://nuget.pkg.github.com/pishangujeniya/index.json"
```

### Adding the GitHub Package Source

To use this package, you need to configure your NuGet client to use the GitHub package registry. Add the following source to your `NuGet.Config` file:

```xml
<configuration>
  <packageSources>
    <add key="KiteConnectJugaadGitHub" value="https://nuget.pkg.github.com/pishangujeniya/index.json" />
  </packageSources>
</configuration>
```