using Codice.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityBuilderAction.Input;
using UnityBuilderAction.Reporting;
using UnityBuilderAction.Versioning;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Nicked from here https://github.com/game-ci/unity-builder/tree/main
// and adapted to accept HoloLens specific parameters

namespace UnityBuilderAction
{
  static class Builder
  {
    public static void BuildProject()
    {
      // Gather values from args
      var options = ArgumentsParser.GetValidatedOptions();

      // Gather values from project
      var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(s => s.path).ToArray();
      
      // Get all buildOptions from options
      BuildOptions buildOptions = BuildOptions.None;
      foreach (string buildOptionString in Enum.GetNames(typeof(BuildOptions))) {
        if (options.ContainsKey(buildOptionString)) {
          BuildOptions buildOptionEnum = (BuildOptions) Enum.Parse(typeof(BuildOptions), buildOptionString);
          buildOptions |= buildOptionEnum;
        }
      }

#if UNITY_2021_2_OR_NEWER
      // Determine subtarget
      StandaloneBuildSubtarget buildSubtarget;
      if (!options.TryGetValue("standaloneBuildSubtarget", out var subtargetValue) || !Enum.TryParse(subtargetValue, out buildSubtarget)) {
        buildSubtarget = default;
      }
#endif

      // Define BuildPlayer Options
      var buildPlayerOptions = new BuildPlayerOptions {
        scenes = scenes,
        locationPathName = options["customBuildPath"],
        target = (BuildTarget) Enum.Parse(typeof(BuildTarget), options["buildTarget"]),
        options = buildOptions,
#if UNITY_2021_2_OR_NEWER
        subtarget = (int) buildSubtarget
#endif
      };

      // Set version for this build
      VersionApplicator.SetVersion(options["buildVersion"]);
      
      // Apply Android settings
      if (buildPlayerOptions.target == BuildTarget.Android)
      {
        VersionApplicator.SetAndroidVersionCode(options["androidVersionCode"]);
        AndroidSettings.Apply(options);
      }

      CheckHoloLensOptions(buildPlayerOptions, options);

      
      // Execute default AddressableAsset content build, if the package is installed.
      // Version defines would be the best solution here, but Unity 2018 doesn't support that,
      // so we fall back to using reflection instead.
      var addressableAssetSettingsType = Type.GetType(
        "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings,Unity.Addressables.Editor");
      if (addressableAssetSettingsType != null)
      {
        // ReSharper disable once PossibleNullReferenceException, used from try-catch
        try
        {
          addressableAssetSettingsType.GetMethod("CleanPlayerContent", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] {null});
          addressableAssetSettingsType.GetMethod("BuildPlayerContent", new Type[0]).Invoke(null, new object[0]);
        }
        catch (Exception e)
        {
          Debug.LogError($"Failed to run default addressables build:\n{e}");
        }
      }

      // Perform build
      BuildReport buildReport = BuildPipeline.BuildPlayer(buildPlayerOptions);

      // Summary
      BuildSummary summary = buildReport.summary;
      StdOutReporter.ReportSummary(summary);

      // Result
      BuildResult result = summary.result;
      StdOutReporter.ExitWithResult(result);
    }

    private static void CheckHoloLensOptions( BuildPlayerOptions buildPlayerOptions, Dictionary<string,string> options)
    {
        if (buildPlayerOptions.target == BuildTarget.WSAPlayer)
        {
            string outValue;
            if (options.TryGetValue("wsaArchitecture", out outValue))
            {
                EditorUserBuildSettings.wsaArchitecture = outValue;
            }
            if (options.TryGetValue("wsaUWPBuildType", out outValue))
            {
                EditorUserBuildSettings.wsaUWPBuildType = 
                    (WSAUWPBuildType)Enum.Parse(typeof(WSAUWPBuildType), outValue);
            }
            if (options.TryGetValue("wsaSubtarget", out outValue))
            {
                EditorUserBuildSettings.wsaSubtarget = 
                    (WSASubtarget)Enum.Parse(typeof(WSASubtarget), outValue);
            }
            if (options.TryGetValue("wsaMinUWPSDK", out outValue))
            {
                EditorUserBuildSettings.wsaMinUWPSDK = outValue;
            }
            if (options.TryGetValue("wsaUWPSDK", out outValue))
            {
                EditorUserBuildSettings.wsaUWPSDK = outValue;
            }
            if (options.TryGetValue("wsaUWPVisualStudioVersion", out outValue))
            {
                EditorUserBuildSettings.wsaUWPVisualStudioVersion = outValue;
            }
        }
    }
  }
}
