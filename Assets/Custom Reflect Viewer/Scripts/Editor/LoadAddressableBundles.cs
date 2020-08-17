using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

class LoadAddressableBundles : MonoBehaviour
{
    static string fileName;
    static string arguments;
    static string platform;
    static string remoteBuildPath;
    // Build fields
    static string iOSBucket = "aaed3245-60b8-4e84-a764-5aee7f07e9ad";
    static string androidBucket = "c2ab66c1-e68a-45a1-9f25-0441d8b0170f";
    static string windowsBucket = "ea1a5db9-fc14-471e-8985-f291c1399862";
    static string webGLBucket = "69562d04-ba26-4bd1-996b-b965db4ca505";
    static string iOSRemoteBuildPath = "ServerData/iOS";
    static string androidRemoteBuildPath = "ServerData/Android";
    static string windowsRemoteBuildPath = "ServerData/StandaloneWindows64";
    static string webGLRemoteBuildPath = "ServerData/WebGL";

    [MenuItem("Window/Asset Management/Load Addressable Bundle")]
    static void LoadAddressableBundle()
    {
        if (CheckRemoteLoadPath())
        {
            UnityEngine.Debug.LogWarningFormat("Your Remote Build Path could not be found. Bundles will NOT be uploaded.");
            return;
        }

        switch(EditorUserBuildSettings.activeBuildTarget)
        {
            case BuildTarget.iOS:
                {
                    if (remoteBuildPath != iOSRemoteBuildPath)
                    {
                        UnityEngine.Debug.LogWarningFormat("Your Remote Build Path {0}, does not match well with the current build target of {1}. Bundles will NOT be uploaded.", remoteBuildPath, EditorUserBuildSettings.activeBuildTarget);
                        return;
                    }
                    fileName = "/bin/bash";
                    arguments = string.Format(" -c \"./ucd entries sync ./{0} --delete --timeout 90 --bucket {1}\"", iOSRemoteBuildPath, iOSBucket);
                    platform = "Update iOS bucket";
                    break;
                }
            case BuildTarget.Android:
                {
                    if (remoteBuildPath != androidRemoteBuildPath)
                    {
                        UnityEngine.Debug.LogWarningFormat("Your Remote Build Path {0}, does not match well with the current build target of {1}. Bundles will NOT be uploaded.", remoteBuildPath, EditorUserBuildSettings.activeBuildTarget);
                        return;
                    }
                    fileName = "cmd.exe";
                    arguments = string.Format("/c ucd entries sync {0} --delete --timeout 90 --bucket {1}", androidRemoteBuildPath, androidBucket);
                    platform = "Update Android bucket";
                    break;
                }
            case BuildTarget.StandaloneWindows64:
                {
                    if (remoteBuildPath != windowsRemoteBuildPath)
                    {
                        UnityEngine.Debug.LogWarningFormat("Your Remote Build Path {0}, does not match well with the current build target of {1}. Bundles will NOT be uploaded.", remoteBuildPath, EditorUserBuildSettings.activeBuildTarget);
                        return;
                    }
                    fileName = "cmd.exe";
                    arguments = string.Format("/c ucd entries sync {0} --delete --timeout 90 --bucket {1}", windowsRemoteBuildPath, windowsBucket);
                    platform = "Update Windows Standalone bucket";
                    break;
                }
            case BuildTarget.WebGL:
                {
                    if (remoteBuildPath != webGLRemoteBuildPath)
                    {
                        UnityEngine.Debug.LogWarningFormat("Your Remote Build Path {0}, does not match well with the current build target of {1}. Bundles will NOT be uploaded.", remoteBuildPath, EditorUserBuildSettings.activeBuildTarget);
                        return;
                    }
                    fileName = "cmd.exe";
                    arguments = string.Format("/c ucd entries sync {0} --delete --timeout 90 --bucket {1}", webGLRemoteBuildPath, webGLBucket);
                    platform = "Update WebGL bucket";
                    break;
                }
            default:
                {
                    UnityEngine.Debug.LogWarningFormat("Your Build Path is not supported. Bundles will NOT be uploaded.");
                    return;
                }
        }

        ExecuteProcessTerminal(platform);
    }

    static bool CheckRemoteLoadPath()
    {
        remoteBuildPath = null;
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            var profileSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.profileSettings;
            var id = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.activeProfileId;
            if (profileSettings != null && !string.IsNullOrEmpty(id))
            {
                remoteBuildPath = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.GetValue(profileSettings, id);
            }
        }
        return remoteBuildPath == null;
    }

    static void ExecuteProcessTerminal(string _platform)
    {
        try
        {
            UnityEngine.Debug.LogFormat("=== Start Executing Addressable Load for {0} ===", _platform);
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Arguments = arguments
            };

            Process myProcess = new Process
            {
                StartInfo = startInfo
            };

            myProcess.Start();
            string output = myProcess.StandardOutput.ReadToEnd();
            UnityEngine.Debug.Log(output);
            myProcess.WaitForExit();
            UnityEngine.Debug.Log("============== End ===============");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log("Exception: " + e);
        }
    }
}