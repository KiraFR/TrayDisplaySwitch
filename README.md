# TrayDisplaySwitch

A lightweight **Windows 11 tray application** to quickly switch display modes (PC only, Duplicate, Extend, Second screen only) using the built‑in `DisplaySwitch.exe` tool.

## Features
- Runs in the **system tray** (notification area).
- Context menu with options:
  - **PC screen only** (`/internal`)
  - **Duplicate** (`/clone`)
  - **Extend** (`/extend`)
  - **Second screen only** (`/external`)
  - **Open Display Settings** (shortcut to Windows settings)
  - **Exit**
- **Embedded icon** (no external file needed).
- **Secure**: uses absolute path to `DisplaySwitch.exe` and argument whitelist.
- Prevents multiple instances (global mutex).

## Requirements
- Windows 10/11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)

## Build
```powershell
# Clone the repo
cd TrayDisplaySwitch

# Build in Release mode
dotnet build -c Release

# Run
dotnet run -c Release

# Or launch the .exe directly
./bin/Release/net8.0-windows/TrayDisplaySwitch.exe
```

## Packaging
Create a single self‑contained executable:
```powershell
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true
```
Resulting binary will be under:
```
bin/Release/net8.0-windows/win-x64/publish/TrayDisplaySwitch.exe
```

## Usage
Once launched:
- The app appears in the tray near the clock.
- Right‑click → choose display mode.

## Security Notes
- Uses absolute path (`%SystemRoot%\\System32\\DisplaySwitch.exe`).
- Hardcoded whitelist of arguments.
- Runs with `asInvoker` (no admin rights needed).
- Icon embedded in the executable to avoid DLL hijacking via external files.

## License
MIT License. See [LICENSE](LICENSE) file for details.
