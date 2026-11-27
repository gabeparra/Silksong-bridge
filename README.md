# Silksong-bridge

> A minimal BepInEx plugin that opens a TCP server to accept JSON commands (single object or batch) and executes them on the Unity main thread.

**Status:** `Experimental` - minimal BepInEx TCP JSON bridge prototype for Streamer.bot or external clients controlling Hollow Knight: Silksong.

## Quickstart

### Prerequisites
- .NET SDK with `net472` targeting support
- Local BepInEx and Unity managed assemblies (`BepInEx.dll`, `UnityEngine*.dll`) placed in `lib/` (or updated in `HkBridge.csproj`)

### Setup & Run
1. **Build:** Place BepInEx and Unity managed assemblies in `lib/` (or update `HkBridge.csproj` `HintPath` entries) and run `dotnet build`.
2. **Install:** Copy the compiled `HkBridge.dll` to `BepInEx/plugins/Silksong-bridge/`.
3. **Run:** Launch Silksong with BepInEx enabled and check `BepInEx/LogOutput.log` for `"Silksong Bridge starting on TCP port 9000"`.
4. **Test:** Connect with any TCP client (`nc`, `telnet`, Python, or Streamer.bot) and send newline-delimited JSON:
   - Single command: `{"cmd":"log","msg":"hello from Streamer.bot"}`
   - Batch: `[{"cmd":"log","msg":"one"},{"cmd":"log","msg":"two"}]`
5. **Extend:** Replace the `spawn_enemy` pseudo-code with Silksong API calls (inspect `Assembly-CSharp` via dnSpy) and add authentication before exposing beyond localhost.

## Stack & Architecture

- **Stack:** C# (.NET Framework 4.7.2), BepInEx plugin framework, Unity main-thread command queue, TCP JSON line protocol.
- **Layout:** `HkBridge.csproj` (project file referencing local BepInEx/Unity assemblies) and `src/HkBridge.cs` (TCP listener, JSON command dispatcher, Unity main-thread queue).

## Network & Security

- The plugin binds to `0.0.0.0:9000` for cross-machine LAN access; allow TCP port 9000 in Windows Firewall if connecting from another machine.
- It accepts unauthenticated connections from any host on the network; add token auth or IP allowlisting before production use.

## Example Commands

- `{"cmd":"log","msg":"Test message"}` - Log a message
- `{"cmd":"spawn_enemy","enemy":"GrubMother","dx":2.0,"dy":0.0}` - Spawn an enemy
- `{"cmd":"set_flag","flag":"myflag","value":true}` - Set a game flag

## License

MIT - see [LICENSE](./LICENSE).
