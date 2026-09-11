using Deucarian.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.CommandRouting.Editor
{
    public sealed class CommandRoutingEditorWindow : EditorWindow
    {
        private CommandRoutingWorkspace view;
        private CommandRoutingTestSession session;
        private double nextRefresh;
        public static void Open() => DeucarianEditorWindowPages.ShowStandalone<CommandRoutingEditorWindow>(
            "Command routing", new Vector2(560, 480));
        public static IDeucarianEditorPage CreatePage() =>
            DeucarianEditorWindowPages.Create<CommandRoutingEditorWindow>((window, root) => window.Build(root),
                activate: (window, _) => window.Activate(), deactivate: window => window.session?.Deactivate(),
                update: window => window.Tick());
        private void OnEnable() { session = new CommandRoutingTestSession(); }
        private void OnDisable() { view?.Dispose(); view = null; session?.Dispose(); session = null; }
        public void CreateGUI() => Build(rootVisualElement);
        private void Build(VisualElement root)
        { view?.Dispose(); view = new CommandRoutingWorkspace(root, session); }
        private void Activate() { session?.Activate(); view?.Refresh(); }
        private void OnInspectorUpdate() => Tick();
        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .25;
            session?.RefreshCatalogSources();
            view?.Refresh();
        }
    }
}
