using BepInEx;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using Newtonsoft.Json.Linq;

[BepInPlugin("com.gabeparra.silksong.bridge","Silksong Bridge","0.1.0")]
public class HkBridge : BaseUnityPlugin
{
    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly List<WebSocket> clients = new List<WebSocket>();
    private readonly object clientsLock = new object();
    private CancellationTokenSource cts;
    private readonly int port = 9000;

    void Awake()
    {
        Logger.LogInfo($"Silksong Bridge starting on ws://127.0.0.1:{port}");
        cts = new CancellationTokenSource();
        Task.Run(() => StartListener(port, cts.Token));
    }

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var a))
        {
            try { a(); } catch (Exception e) { Logger.LogError(e.ToString()); }
        }
    }

    void OnDestroy()
    {
        cts?.Cancel();
        lock (clientsLock)
        {
            foreach (var ws in clients.ToArray()) { try { ws.Abort(); ws.Dispose(); } catch {} }
            clients.Clear();
        }
    }

    private void EnqueueMain(Action action) => mainThreadQueue.Enqueue(action);

    // Game command dispatcher — runs on Unity main thread
    // Replace/extend handlers below with real Silksong API calls found in Assembly-CSharp
    private void ExecuteCommand(JObject obj)
    {
        var cmd = (string)obj["cmd"];
        switch (cmd)
        {
            case "log":
                Logger.LogInfo((string)obj["msg"] ?? "<no msg>");
                break;

            case "spawn_enemy":
                {
                    // NOTE: Replace this pseudo-implementation with real Silksong spawn code.
                    // Use dnSpy to find the correct prefab paths or spawn methods in Assembly-CSharp.
                    string enemyName = (string)obj["enemy"] ?? "DefaultEnemy";
                    float dx = obj["dx"] != null ? (float)obj["dx"] : 0f;
                    float dy = obj["dy"] != null ? (float)obj["dy"] : 0f;
                    Logger.LogInfo($"Request spawn_enemy {enemyName} offset ({dx},{dy})");

                    // PSEUDO:
                    // var playerGO = GameObject.Find("Player");
                    // if (playerGO != null)
                    // {
                    //     var playerPos = playerGO.transform.position;
                    //     var prefab = Resources.Load<GameObject>("Prefabs/Enemies/{enemyName}");
                    //     if (prefab != null)
                    //     {
                    //         GameObject.Instantiate(prefab, playerPos + new Vector3(dx, dy, 0f), Quaternion.identity);
                    //     }
                    //     else Logger.LogWarning($"Prefab not found: {enemyName}");
                    // }
                    break;
                }

            case "set_flag":
                {
                    string flag = (string)obj["flag"];
                    bool value = obj["value"] != null ? (bool)obj["value"] : false;
                    Logger.LogInfo($"Set flag {flag} = {value}");
                    // Apply to your mod state or to Silksong systems here
                    break;
                }

            default:
                Logger.LogWarning($"Unknown cmd: {cmd}");
                break;
        }
    }

    private async Task StartListener(int port, CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Logger.LogInfo($"Listener started on ws://127.0.0.1:{port}");
        try
        {
            while (!token.IsCancellationRequested)
            {
                var tcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleClient(tcp, token));
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Logger.LogError($"Listener exception: {ex}");
        }
        finally { listener.Stop(); }
    }

    private async Task HandleClient(TcpClient tcpClient, CancellationToken token)
    {
        using (tcpClient)
        using (var stream = tcpClient.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true))
        {
            // Read request start line
            var start = await reader.ReadLineAsync().ConfigureAwait(false);
            if (start == null) return;

            // Read headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
            {
                var parts = line.Split(new[] { ':', 2 });
                if (parts.Length == 2) headers[parts[0].Trim()] = parts[1].Trim();
            }

            if (!headers.TryGetValue("Sec-WebSocket-Key", out var key)) return;
            var accept = ComputeWebSocketAcceptKey(key);
            var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
            var respBytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(respBytes, 0, respBytes.Length, token).ConfigureAwait(false);

            var ws = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
            lock (clientsLock) { clients.Add(ws); }
            try
            {
                var buffer = new byte[8192];
                while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    try
                    {
                        var tokenJson = JToken.Parse(msg);
                        if (tokenJson.Type == JTokenType.Array)
                        {
                            // Process batch atomically on main thread
                            var batch = tokenJson as JArray;
                            EnqueueMain(() =>
                            {
                                foreach (var j in batch)
                                {
                                    if (j is JObject jobj) ExecuteCommand(jobj);
                                }
                            });
                        }
                        else if (tokenJson is JObject jobj)
                        {
                            EnqueueMain(() => ExecuteCommand(jobj));
                        }
                        var ack = new JObject { ["status"] = "queued" };
                        var outBytes = Encoding.UTF8.GetBytes(ack.ToString());
                        await ws.SendAsync(new ArraySegment<byte>(outBytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Parse/queue error: {ex}");
                        var err = new JObject { ["status"] = "error", ["message"] = ex.Message };
                        var outBytes = Encoding.UTF8.GetBytes(err.ToString());
                        await ws.SendAsync(new ArraySegment<byte>(outBytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.LogError($"Client error: {ex}"); }
            finally
            {
                lock (clientsLock) { clients.Remove(ws); }
                try { ws.Abort(); ws.Dispose(); } catch { }
            }
        }
    }

    private static string ComputeWebSocketAcceptKey(string secWebSocketKey)
    {
        var concat = secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(concat));
        return Convert.ToBase64String(hash);
    }
}