using System;
using System.Diagnostics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(NetworkManager))]
public class NetworkStarter : MonoBehaviour
{
    [Header("Network Settings")] [Tooltip("The tunnel host")]
    public string hostUrl = "cinnamon-croissant.eu.cc";

    [Tooltip("Standard HTTPS/WSS port for the WebGL client")]
    public ushort webClientPort = 443;

    [Tooltip("The local port the PC host will listen on")]
    public ushort hostListenPort = 7777;

    [Tooltip("Cloudflare tunnel token")] public string cloudflareToken =
        "eyJhIjoiNzIxY2NmOTU4MmU4MmFmYzViNjQzNTJiYWE5MGI5ODUiLCJ0IjoiMWRhYWM5OTEtNzZiMS00MzM5LWI3NDAtZmZiYzRlMmFkOGExIiwicyI6IlltWXhObVl5TlRRdE56ZzJOaTAwTkdFMExUaGlNVGN0TVRFeU1UWXhOV1EzTkRSbU5EVm1PRFpqWm1RdFpUWXpPQzAwT1RZeExUaGxaRFV0WW1Rek1qRmhZVGcwWmpCaSJ9";

    private Process tunnelProcess;

    private void Start()
    {
        var networkManager = NetworkManager.Singleton;
        var transport = networkManager.GetComponent<UnityTransport>();
        transport.UseWebSockets = true;

        // Fetch the role selected in the Multiplayer Play Mode tools or Build Profile
        var isServer = NetworkManager.Singleton.IsServer;

        // WebGL requires WebSockets. We enforce this for both Host and Client.
        transport.UseWebSockets = true;

        if (!isServer)
        {
            Debug.Log($"[Client] Connecting to {hostUrl}:{webClientPort}");

            // Pass the domain and port. WebGL will naturally resolve the domain 
            // and upgrade to WSS (Secure WebSockets) on port 443.
            transport.SetConnectionData(hostUrl, webClientPort);

            networkManager.StartClient();
        }
        else
        {
            Debug.Log($"[Host] Listening on local port {hostListenPort}");

            // The host listens locally. Cloudflare forwards internet traffic here.
            // "0.0.0.0" allows the server to listen on all available network interfaces.
            transport.SetConnectionData("127.0.0.1", hostListenPort, "0.0.0.0");

            networkManager.StartHost();

            LaunchTunnel();
        }
    }

    private void OnApplicationQuit()
    {
        // Clean up the background process when the game closes
        if (tunnelProcess != null && !tunnelProcess.HasExited)
        {
            Debug.Log("[Host] Shutting down Cloudflare tunnel...");
            tunnelProcess.Kill();
            tunnelProcess.Dispose();
        }
    }

    private void LaunchTunnel()
    {
        try
        {
            tunnelProcess = new Process();
            tunnelProcess.StartInfo.FileName = "cloudflared";

            // Constructs: tunnel --url http://localhost:7777 --hostname cinnamon-croissant.eu.cc
            tunnelProcess.StartInfo.Arguments =
                $"tunnel --no-autoupdate run --token {cloudflareToken}";

            // Run invisibly in the background
            tunnelProcess.StartInfo.UseShellExecute = false;
            tunnelProcess.StartInfo.CreateNoWindow = true;

            // Redirect output to Unity Console
            tunnelProcess.StartInfo.RedirectStandardOutput = true;
            tunnelProcess.StartInfo.RedirectStandardError = true;

            tunnelProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Debug.Log($"[Cloudflared] {args.Data}");
            };
            tunnelProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Debug.LogWarning($"[Cloudflared] {args.Data}");
            };

            tunnelProcess.Start();
            tunnelProcess.BeginOutputReadLine();
            tunnelProcess.BeginErrorReadLine();

            Debug.Log("[Host] Cloudflare tunnel launched successfully in the background.");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[Host] Failed to start Cloudflare tunnel. Ensure it is installed and in your PATH. Error: {e.Message}");
        }
    }
}
