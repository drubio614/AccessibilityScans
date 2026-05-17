# AccessibilityScans (.NET 8 + Axe + Selenium + Firefox)

## Quick setup (one-time per machine)
1. Ensure .NET 8 SDK is installed.
2. Install Firefox (stable release).

## Run tests
From the project folder:
```powershell
dotnet nuget locals global-packages --clear
rd /s /q bin
rd /s /q obj
dotnet restore
dotnet test
