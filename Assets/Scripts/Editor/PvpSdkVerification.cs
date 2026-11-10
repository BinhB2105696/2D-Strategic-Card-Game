using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

// Verification only: never upgrades the Editor, changes settings, starts networking
// or signs in. The dependency plan is checked before any package installation.
[InitializeOnLoad]
public static class PvpSdkVerification
{
    [Serializable] private sealed class Pin { public string name, version; }
    [Serializable] private sealed class Plan
    {
        public string targetUnity, projectVersionHash;
        public bool metadataCompatibilityVerified;
        public Pin[] pins, preserve;
    }
    [Serializable] private sealed class Report
    {
        public string state, message, unity, utc;
        public bool sdkVerified, existingPackagesPreserved, projectVersionUnchanged;
        public Pin[] packages;
    }
    private static readonly string Control = Path.GetFullPath("Tools/Simulation/Control/PvpSdk");
    private static double stableSince, nextPoll;
    static PvpSdkVerification() { EditorApplication.update += Tick; }

    [MenuItem("Card Game/Networking/Verify Pinned PvP SDKs")]
    public static void Request()
    {
        Directory.CreateDirectory(Control);
        File.WriteAllText(Path.Combine(Control, "verify.request"), "verify");
    }

    private static void Tick()
    {
        if (!File.Exists(Path.Combine(Control, "verify.request"))) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { stableSince = 0; return; }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (stableSince == 0) stableSince = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - stableSince < 5) return;
        try
        {
            Plan plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Control, "plan.json")));
            if (!plan.metadataCompatibilityVerified || Application.unityVersion != plan.targetUnity)
                throw new InvalidOperationException("Editor/version compatibility guard failed.");
            string versionHash;
            using (SHA256 hash = SHA256.Create()) versionHash = BitConverter.ToString(hash.ComputeHash(
                File.ReadAllBytes("ProjectSettings/ProjectVersion.txt"))).Replace("-", "").ToLowerInvariant();
            if (versionHash != plan.projectVersionHash) throw new InvalidOperationException("ProjectVersion.txt changed; stop installation review.");
            if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Unity reports script compilation errors.");
            var registered = PackageInfo.GetAllRegisteredPackages().ToDictionary(package => package.name);
            foreach (Pin pin in plan.pins)
            {
                if (!registered.TryGetValue(pin.name, out PackageInfo package) || package.version != pin.version)
                { Write("waiting-packages", "Waiting for pinned package: " + pin.name); return; }
            }
            foreach (Pin previous in plan.preserve)
                if (!registered.TryGetValue(previous.name, out PackageInfo package) || package.version != previous.version)
                    throw new InvalidOperationException("Existing package version changed/missing: " + previous.name);
            RequireMethod("Unity.Netcode.Runtime", "Unity.Netcode.NetworkManager", "StartHost");
            RequireMethod("Unity.Netcode.Runtime", "Unity.Netcode.NetworkManager", "StartClient");
            RequireMethod("Unity.Netcode.Runtime", "Unity.Netcode.Transports.UTP.UnityTransport", "SetRelayServerData");
            RequireMethod("Unity.Services.Core", "Unity.Services.Core.UnityServices", "InitializeAsync");
            Type auth = Assembly.Load("Unity.Services.Authentication").GetType("Unity.Services.Authentication.AuthenticationService", true)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).PropertyType;
            if (!auth.GetMethods().Any(method => method.Name == "SignInAnonymouslyAsync")) throw new MissingMethodException(auth.FullName, "SignInAnonymouslyAsync");
            Type relay = Assembly.Load("Unity.Services.Relay").GetType("Unity.Services.Relay.RelayService", true)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).PropertyType;
            foreach (string method in new[] { "CreateAllocationAsync", "GetJoinCodeAsync", "JoinAllocationAsync" })
                if (!relay.GetMethods().Any(candidate => candidate.Name == method)) throw new MissingMethodException(relay.FullName, method);
            Write("verified", "Pinned SDKs loaded; scripts compiled; original package versions and Editor version unchanged.", plan.pins);
            File.Delete(Path.Combine(Control, "verify.request"));
            Debug.Log("PvP SDK verification PASS. See Tools/Simulation/Control/PvpSdk/status.json");
        }
        catch (Exception exception)
        {
            Write("failed", exception.ToString());
            File.Delete(Path.Combine(Control, "verify.request"));
            Debug.LogException(exception);
        }
    }

    private static void RequireMethod(string assembly, string type, string method)
    {
        Type resolved = Assembly.Load(assembly).GetType(type, true);
        if (!resolved.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Any(candidate => candidate.Name == method))
            throw new MissingMethodException(type, method);
    }

    private static void Write(string state, string message, Pin[] pins = null)
    {
        var report = new Report { state = state, message = message, unity = Application.unityVersion,
            utc = DateTime.UtcNow.ToString("o"), sdkVerified = state == "verified",
            existingPackagesPreserved = state == "verified", projectVersionUnchanged = state == "verified", packages = pins };
        string path = Path.Combine(Control, "status.json");
        File.WriteAllText(path + ".tmp", JsonUtility.ToJson(report, true));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
    }
}
