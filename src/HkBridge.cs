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
using Newtonsoft.Json.Linq;

namespace Silksong.Bridge;

[BepInPlugin("com.gabeparra.silksong.bridge","Silksong Bridge","0.1.0")]
public class HkBridge : BaseUnityPlugin
{
    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly object clientsLock = new object();
    private CancellationTokenSource cts;
    private readonly int port = 9000;

    void Awake()
    {
        Logger.LogInfo($"Silksong Bridge starting on TCP port {port}");
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
        // NOTE: Using TCP-based JSON command interface for .NET Framework 4.7.2 compatibility
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Logger.LogInfo($"Listener started on TCP port {port} (listening on all interfaces)");
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
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            try
            {
                while (!token.IsCancellationRequested && tcpClient.Connected)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line)) break;

                    try
                    {
                        var tokenJson = JToken.Parse(line);
                        if (tokenJson.Type == JTokenType.Array)
                        {
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
                        await writer.WriteLineAsync(ack.ToString()).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Parse/queue error: {ex}");
                        var err = new JObject { ["status"] = "error", ["message"] = ex.Message };
                        await writer.WriteLineAsync(err.ToString()).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.LogError($"Client error: {ex}"); }
        }
    }
}