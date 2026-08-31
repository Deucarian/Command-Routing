using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Diagnostics;
using Deucarian.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Deucarian.CommandRouting.Editor
{
    public sealed partial class CommandRoutingEditorWindow
    {


        private void ApplyOutcome(
            string label,
            CommandRouteOutcome outcome,
            bool? expectedSuccess)
        {
            CommandResult result = outcome?.Result ??
                CommandResult.Failure(
                    CommandRoutingErrorCodes.RouteUnavailable,
                    "The command route returned no result.");
            bool matched = !expectedSuccess.HasValue ||
                           result.Succeeded == expectedSuccess.Value;
            string actual = result.Succeeded
                ? "succeeded"
                : "failed" +
                  (string.IsNullOrWhiteSpace(result.ErrorCode)
                      ? string.Empty
                      : " with '" + result.ErrorCode + "'");
            simulatorResult = label + " " + actual + "." +
                              (string.IsNullOrWhiteSpace(result.Message)
                                  ? string.Empty
                                  : " " + result.Message);
            if (expectedSuccess.HasValue && !matched)
            {
                simulatorResult += expectedSuccess.Value
                    ? " Success was expected."
                    : " Failure was expected.";
            }

            simulatorMessageType = matched
                ? MessageType.Info
                : MessageType.Error;
            simulatorResponse = outcome?.Response ?? string.Empty;
            Repaint();
        }

        private void BeginSending()
        {
            sendCancellation?.Cancel();
            sendCancellation?.Dispose();
            sendCancellation = new CancellationTokenSource();
            sending = true;
            simulatorResponse = string.Empty;
            Repaint();
        }

        private void EndSending()
        {
            sendCancellation?.Dispose();
            sendCancellation = null;
            sending = false;
            Repaint();
        }

        private void RefreshSettings()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:CommandRoutingSettings");
            settings =
                guids.Length == 0
                    ? null
                    : AssetDatabase.LoadAssetAtPath<
                        CommandRoutingSettings>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[0]));
            serializedSettings =
                settings == null
                    ? null
                    : new SerializedObject(settings);
        }

        private void CreateSettings()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            settings =
                CreateInstance<CommandRoutingSettings>();
            AssetDatabase.CreateAsset(
                settings,
                AssetDatabase.GenerateUniqueAssetPath(
                    SettingsPath));
            AssetDatabase.SaveAssets();
            RefreshSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
