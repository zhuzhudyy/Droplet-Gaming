using UnityEditor;
using UnityEngine;
namespace DropletPrototype.Editor
{
    [CustomEditor(typeof(SolarSystemBackdrop))]
    public sealed class SolarLayoutInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var b=(SolarSystemBackdrop)target;
            EditorGUILayout.HelpBox("Macro positions: AU relative to the Sun. Local arena: metres. Celestial proxies preserve physical angular size; Earth is normally subpixel. Edit the authority JSON and re-export through Blender.",MessageType.Info);
            if(GUILayout.Button("Locate Sun proxy"))Focus(b.sunProxy);
            if(GUILayout.Button("Locate Earth proxy (physical size)"))Focus(b.earthProxy);
            if(GUILayout.Button("Apply bound observer mapping")){Undo.RecordObjects(new Object[]{b.sunProxy,b.earthProxy,b.skyProxy},"Apply celestial mapping");b.ReloadLayout();b.ApplyMapping(b.observer);}
        }
        static void Focus(Transform t){if(t==null)return;Selection.activeGameObject=t.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(t.position,Vector3.one*Mathf.Max(5,t.localScale.x*3)),false);}
        [DrawGizmo(GizmoType.Selected|GizmoType.NonSelected)]
        static void Draw(SolarSystemBackdrop b,GizmoType type)
        {
            if(b.Layout==null)return;var d=b.Layout;Vector3 center=new Vector3(d.localOrigin[0],d.localOrigin[1],d.localOrigin[2]);
            if((type&GizmoType.Selected)!=0)
            {
                Gizmos.color=new Color(.25f,.6f,1,.4f);Gizmos.DrawWireSphere(center,d.boundaryRadius);Gizmos.color=new Color(1,.65f,.15f,.4f);Gizmos.DrawWireSphere(center,d.warningRadius);
                Handles.Label(center+Vector3.right*d.boundaryRadius,"Flight boundary "+d.boundaryRadius+" m");
                if(b.sunProxy!=null)Handles.Label(b.sunProxy.position,"Sun / physical angular size");if(b.earthProxy!=null)Handles.Label(b.earthProxy.position,"Earth / subpixel at this distance");
            }
        }
    }
}
