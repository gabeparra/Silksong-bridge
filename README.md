# Silksong-bridge

A minimal BepInEx plugin that opens a local WebSocket server to accept JSON commands (single object or an array/batch) and executes them on the Unity main thread. Designed as a bridge for Streamer.bot or any external client to control Silksong/Hollow Knight via a mod.

Usage
1. Build:
   - Place BepInEx and Unity managed assemblies (BepInEx.dll, UnityEngine*.dll) in a `lib/` folder or update the csproj HintPath to your local copies.
   - Build with Visual Studio or `dotnet build` (ensure you have .NET targeting/net472 support).

2. Install:
   - Copy the compiled HkBridge.dll to `BepInEx/plugins/Silksong-bridge/`.

3. Run:
   - Launch Silksong with BepInEx enabled. Check `BepInEx/LogOutput.log` for "Silksong Bridge starting on ws://127.0.0.1:9000".

4. Test:
   - Connect with a WebSocket client (e.g., websocat, Python websockets, Streamer.bot).
   - Send:
     - Single command: `{\"cmd\":\"log\",\"msg\":\"hello from Streamer.bot\"}`
     - Batch: `[{\"cmd\":\"log\",\"msg\":\"one\"},{\"cmd\":\"log\",\"msg\":\"two\"}]`

5. Extend:
   - Replace the `spawn_enemy` pseudo-code with the correct Silksong API calls (use dnSpy to find correct prefab paths/methods in Assembly-CSharp).
   - Add optional token/auth, config entries, or use a production-ready WebSocket library.

Security
- The plugin binds to localhost only (127.0.0.1). Do not bind to a public interface.
- Consider adding a simple shared secret token in JSON payloads if you want to restrict access from other local processes.

License
- Add your preferred license.
