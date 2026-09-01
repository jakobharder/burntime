# Steamworks native redistributables

These are the unmodified Steam API runtime libraries from Steamworks SDK 1.65:

- `win-x64/steam_api64.dll`
- `linux-x64/libsteam_api.so`
- `osx/libsteam_api.dylib` (universal x86_64/arm64)

The managed bridge is Steamworks.NET 2025.164.0, compiled from the official
`Standalone2.0/Steamworks.NET.Standard.csproj` for Windows x64, Unix x64, and Unix
arm64. Separate managed builds are required because Steamworks.NET selects platform
interop and pointer layout at compile time. The official 2025.164.0 source zip used
for these builds had SHA-256
`19c37aa7b08ea09d415a3debdb8ee870f2a3a7e036395c6bc61afe31fd83a1b6`.

The matching managed and native libraries are copied beside the game executable so
Steam integration can initialize. The game falls back to text prompts when Steam is
unavailable.

Distribution is governed by the Steamworks SDK Access Agreement. Attribution for
Steamworks.NET is included in `resources/licenses/THIRD-PARTY-NOTICES.txt`.
