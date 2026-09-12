#if UNITY_EDITOR

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ExternalDependencyInstaller : EditorWindow
{
    private const string DownloadDirectory =
        "Packages/Downloaded";

    private const string MediaPipeVersion = "0.16.3";

    private const string MediaPipeUrl =
        "https://github.com/homuler/MediaPipeUnityPlugin/releases/download/" +
        "v0.16.3/com.github.homuler.mediapipe-0.16.3.tgz";

    private const string MediaPipeFileName =
        "com.github.homuler.mediapipe-0.16.3.tgz";

    private const string MediaPipePackageName =
        "com.github.homuler.mediapipe";

    private const string SonyVersion = "2.6.0.02120";

    private const string SonyUrl =
        "https://xyn.sony.net/hubfs/developer/SRD/download/old/" +
        "srdisplay-unity-plugin_v2.6.0.02120.unitypackage";

    private const string SonyFileName =
        "srdisplay-unity-plugin_v2.6.0.02120.unitypackage";


    private static bool _running;

    
    static ExternalDependencyInstaller()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode == false)
        {
            EditorApplication.delayCall += Initialize;
        }
    }


    private static void Initialize()
    {
        if (_running)
            return;

        _running = true;
        _ = InstallDependencies();
    }


    private static async Task InstallDependencies()
    {
        try
        {
            await InstallMediaPipe();
            await InstallSony();
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[Dependencies] Installation failed:\n{e}"
            );
        }
        finally
        {
            _running = false;
        }
    }


    // ---------------------------------------------------------------------
    // MediaPipe
    // ---------------------------------------------------------------------

    private static async Task InstallMediaPipe()
    {
        string packagePath =
            Path.Combine(
                DownloadDirectory,
                MediaPipeFileName
            );

        string absolutePath =
            Path.GetFullPath(packagePath);

        // Already downloaded.
        if (File.Exists(absolutePath))
        {
            Debug.Log(
                $"[Dependencies] MediaPipe {MediaPipeVersion} is already downloaded."
            );

            return;
        }

        Debug.Log(
            $"[Dependencies] Downloading MediaPipe {MediaPipeVersion}..."
        );

        Directory.CreateDirectory(
            Path.GetFullPath(DownloadDirectory)
        );

        await DownloadFile(
            MediaPipeUrl,
            absolutePath
        );

        Debug.Log(
            $"[Dependencies] MediaPipe {MediaPipeVersion} downloaded."
        );
        
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------------
    // Sony SRDisplay
    // ---------------------------------------------------------------------

    private static async Task InstallSony()
    {
        string packagePath =
            Path.Combine(
                DownloadDirectory,
                SonyFileName
            );

        string absolutePath =
            Path.GetFullPath(packagePath);

        // Check whether the package appears to already be imported.
        if (IsSonyInstalled())
        {
            Debug.Log(
                $"[Dependencies] Sony SRDisplay {SonyVersion} is already installed."
            );

            return;
        }

        if (!File.Exists(absolutePath))
        {
            Debug.Log(
                $"[Dependencies] Downloading Sony SRDisplay {SonyVersion}..."
            );

            Directory.CreateDirectory(
                Path.GetFullPath(DownloadDirectory)
            );

            await DownloadFile(
                SonyUrl,
                absolutePath
            );

            Debug.Log(
                $"[Dependencies] Sony SRDisplay {SonyVersion} downloaded."
            );
        }

        Debug.Log(
            "[Dependencies] Importing Sony SRDisplay..."
        );

        AssetDatabase.ImportPackage(
            packagePath,
            false
        );

        Debug.Log(
            $"[Dependencies] Sony SRDisplay {SonyVersion} imported."
        );
    }


    private static bool IsSonyInstalled()
    {
        // Change this path if the Sony package uses a different root folder.
        return Directory.Exists(
            Path.Combine(
                Application.dataPath,
                "SRDisplayUnityPlugin"
            )
        );
    }


    // ---------------------------------------------------------------------
    // HTTP download
    // ---------------------------------------------------------------------

    private static async Task DownloadFile(
        string url,
        string destination)
    {
        string temporaryFile =
            destination + ".download";

        try
        {
            using var client = new HttpClient();

            client.Timeout =
                TimeSpan.FromMinutes(10);

            using HttpResponseMessage response =
                await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead
                );

            response.EnsureSuccessStatusCode();

            await using Stream input =
                await response.Content.ReadAsStreamAsync();

            await using FileStream output =
                new FileStream(
                    temporaryFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                );

            await input.CopyToAsync(output);

            output.Close();

            // Atomic-ish replacement:
            // only make the file visible once the download completed.
            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(
                temporaryFile,
                destination
            );
        }
        catch
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);

            throw;
        }
    }
}

#endif