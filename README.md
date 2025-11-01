# Silksong-bridge

A minimal BepInEx plugin that opens a TCP server to accept JSON commands (single object or an array/batch) and executes them on the Unity main thread. Designed as a bridge for Streamer.bot or any external client to control Silksong/Hollow Knight via a mod.

Usage
1. Build:
   - Place BepInEx and Unity managed assemblies (BepInEx.dll, UnityEngine*.dll) in a `lib/` folder or update the csproj HintPath to your local copies.
   - Build with `dotnet build` (ensure you have .NET targeting net472 support).

2. Install:
   - Copy the compiled HkBridge.dll to `BepInEx/plugins/Silksong-bridge/`.

3. Run:
   - Launch Silksong with BepInEx enabled. Check `BepInEx/LogOutput.log` for "Silksong Bridge starting on TCP port 9000".

4. Test:
   - Connect with any TCP client (e.g., telnet, nc, Python, Streamer.bot).
   - Send JSON line by line:
     - Single command: `{"cmd":"log","msg":"hello from Streamer.bot"}`
     - Batch: `[{"cmd":"log","msg":"one"},{"cmd":"log","msg":"two"}]`

5. Extend:
   - Replace the `spawn_enemy` pseudo-code with the correct Silksong API calls (use dnSpy to find correct prefab paths/methods in Assembly-CSharp).
   - Add optional token/auth or config entries for production use.

Network Setup
- The plugin binds to all interfaces (0.0.0.0:9000) for cross-machine LAN access.
- If connecting from another machine on the same network, you may need to allow TCP port 9000 in your Windows Firewall.

Security
- The plugin accepts connections from any machine on your network.
- Consider adding authentication or IP whitelisting for production deployments.

Example Commands
- `{"cmd":"log","msg":"Test message"}` - Log a message
- `{"cmd":"spawn_enemy","enemy":"GrubMother","dx":2.0,"dy":0.0}` - Spawn an enemy
- `{"cmd":"set_flag","flag":"myflag","value":true}` - Set a game flag

License
- Add your preferred license.
