# Interstellar Menu

A free, open source menu for Gorilla Tag.

Current source version: **1.0.0**.

[Website](https://interstellar.rip)

## Features

- Basic Mods, movement, rig, world, room and gamemode tools.
- Name tags, voice indicators and tracers.
- Desktop camera controls, free camera and spectating.
- Search, favorites and mod presets.
- Saved menu sizing from 75% to 125% in 5% steps.

## Requirements

- Gorilla Tag for Windows.
- BepInEx 5 installed in the game directory.
- A current .NET SDK to build the source.

The game and BepInEx assemblies are read from your own installation. They are not included in this repository.

## Build

Clone the repository, open a terminal in its directory, and run:

```powershell
dotnet build InterstellarMenu.csproj -c Release "-p:GamePath=YOUR_GORILLA_TAG_DIRECTORY"
```

Replace `YOUR_GORILLA_TAG_DIRECTORY` with your game's installation directory.

The DLL is written to `bin/Release/netstandard2.1/Interstellar Menu.dll`. Building does not install it or launch the game.

If the assemblies are stored separately, supply their directories instead:

```powershell
dotnet build InterstellarMenu.csproj -c Release "-p:GameAssemblyPath=YOUR_MANAGED_ASSEMBLIES_DIRECTORY" "-p:BepInExAssemblyPath=YOUR_BEPINEX_CORE_DIRECTORY"
```

## Install and use

1. Close Gorilla Tag.
2. Copy `Interstellar Menu.dll` into the game's `BepInEx/plugins` directory.
3. Start the game.

In VR, hold the left controller's secondary button, usually **Y**, to open the menu. On desktop, hold **Q**.

Remove the DLL from `BepInEx/plugins` while the game is closed to uninstall it.

## License

The menu code is licensed under [GPL-3.0-or-later](LICENSE). This Interstellar source snapshot was prepared on 2026-10-14.

Required third-party notices and the separate font and icon licenses are in [Licenses](Licenses). Bundled assets retain their applicable licenses.
