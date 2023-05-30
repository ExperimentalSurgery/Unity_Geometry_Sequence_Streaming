using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GeometrySequence.Streaming
{
    [CustomEditor(typeof(GeometrySequenceClip))]
    [CanEditMultipleObjects]
    public class GeometrySequenceClipGUI : Editor
    {
        SerializedProperty absolutePathToSequence;
        SerializedProperty pathRelation;
        SerializedProperty relativePath;

        GeometrySequenceStream.PathType enumforDisplay; 

        void OnEnable()
        {
            pathRelation = serializedObject.FindProperty("pathRelation");
            relativePath = serializedObject.FindProperty("relativePath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label("Set Sequence", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(pathRelation);

            relativePath.stringValue = GUILayout.TextField(relativePath.stringValue);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Open Sequence"))
            {
                string path = EditorUtility.OpenFolderPanel("Open a folder that contains a Geometry Sequence", relativePath.stringValue, "");

                if (path != null)
                {
                    if (Directory.Exists(path))
                    {
                        if (Directory.GetFiles(path, "*.ply").Length > 0)
                        {
                            if (path.Contains("StreamingAssets"))
                            {
                                relativePath.stringValue = Path.GetRelativePath(Application.streamingAssetsPath, path);
                                pathRelation.enumValueFlag = (int)GeometrySequenceStream.PathType.RelativeToStreamingAssets;
                            }

                            else if (path.Contains(Application.dataPath))
                            {
                                relativePath.stringValue = Path.GetRelativePath(Application.dataPath, path);
                                pathRelation.enumValueFlag = (int)GeometrySequenceStream.PathType.RelativeToDataPath;
                            }

                            else
                            {
                                relativePath.stringValue = path;
                                pathRelation.enumValueFlag = (int)GeometrySequenceStream.PathType.AbsolutePath;
                            }

                        }

                        else
                        {
                            EditorUtility.DisplayDialog("Folder not valid", "Could not find any sequence file in the choosen folder!" +
                                                        " Pick another folder, or convert your Geometry Sequence into the correct format with the included converter.",
                                                        "Got it!");
                        }
                    }
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                relativePath.stringValue = "";
            }

            GUILayout.EndHorizontal();

            if (pathRelation.enumValueIndex != (int)GeometrySequenceStream.PathType.RelativeToStreamingAssets && relativePath.stringValue.Length > 1)
                EditorGUILayout.HelpBox("Files are not placed in the StreamingAsset folder. The playback will work on your PC, but likely not if you build/export the project to other devices.", MessageType.Info);



            serializedObject.ApplyModifiedProperties();
        }
    }
}



