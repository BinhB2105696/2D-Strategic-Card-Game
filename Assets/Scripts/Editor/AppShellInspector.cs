using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(DeckBuilderAppController))]
public sealed class AppShellInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Edit Mode previews: edit the actual child objects in Hierarchy, then save the scene. Play Mode changes are temporary. Preview cards are replaced by saved deck data at runtime.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            DeckBuilderAppController controller = (DeckBuilderAppController)target;
            if (!controller.HasAuthoredUi)
            {
                if (GUILayout.Button("Prepare Editable UI")) AppShellSceneTool.PrepareEditableUi();
                return;
            }
            foreach (string page in new[] { "MainMenu", "GuestEntry", "DeckBuilder", "Profile", "Settings", "CardDetail", "Filter", "Message" })
            {
                if (!GUILayout.Button("Preview " + page)) continue;
                Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Preview App Page");
                controller.PreviewPage(page);
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
    }
}
