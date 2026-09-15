using System;
using UnityEngine;

namespace Deucarian.CommandRouting.Samples.DefinitionWorkflow
{
    /// <summary>Small caller example. The configured scene hosts own services and resource lifetimes.</summary>
    public sealed class CommandRoutingWorkflow : MonoBehaviour
    {
        [SerializeField] private CommandHost host;
        [SerializeField] private CommandKey<string> command = SampleCommands.SetMessage;
        [SerializeField] private SampleCommandTrigger trigger;
        private string status = "Ready. Choose an action below.";
        public string Status => status;
        public async void Execute() { var result = await host.ExecuteAsync(command, "Hello from C#"); status = result.Succeeded ? "Message handled successfully." : "Command rejected."; }
        public void ExecuteComponent() { trigger.Execute(); status = "Executed through the typed component."; }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, Math.Min(540, Screen.width - 48), Screen.height - 48), GUI.skin.box);
            GUILayout.Label("Command-Routing — definition workflow");
            GUILayout.Label("The payload contract is declared once. The startup component registers its real handler; no assembly scanning or domain state assets are needed.");
            GUILayout.Space(12);
            if (GUILayout.Button("Execute typed command", GUILayout.Height(32))) { try { Execute(); } catch (Exception error) { status = error.Message; } }
            if (GUILayout.Button("Execute from component", GUILayout.Height(32))) { try { ExecuteComponent(); } catch (Exception error) { status = error.Message; } }
            GUILayout.Space(12);
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
