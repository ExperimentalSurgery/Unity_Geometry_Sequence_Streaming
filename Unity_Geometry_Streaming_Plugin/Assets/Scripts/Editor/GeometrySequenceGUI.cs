using UnityEditor;
using UnityEngine;
using System.IO;
using GeometrySequence.Streaming;
using System.Data;
using System;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;

namespace GeometrySequence.Streaming
{

    [CustomEditor(typeof(GeometrySequencePlayer))]
    [CanEditMultipleObjects]
    public class GeometrySequenceGUI : Editor
    {
        bool showBufferingFoldout;
        bool showDebugFoldout;

        SerializedProperty relativePath;
        SerializedProperty pathToSequence;
        SerializedProperty pathRelation;
        SerializedProperty targetFPS;
        SerializedProperty playAtStart;
        SerializedProperty loopPlay;



        float frameDropShowSeconds = 0.1f;
        float frameDropShowCounter = 0;

        private void OnEnable()
        {
            relativePath = serializedObject.FindProperty("relativePath");
            pathToSequence = serializedObject.FindProperty("pathToSequence");
            pathRelation = serializedObject.FindProperty("pathRelation");
            targetFPS = serializedObject.FindProperty("playbackFPS");
            playAtStart = serializedObject.FindProperty("playAtStart");
            loopPlay = serializedObject.FindProperty("loopPlay");

            GeometrySequencePlayer player = (GeometrySequencePlayer)target;
            player.SetupGeometryStream();

            serializedObject.ApplyModifiedProperties();

            
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            GeometrySequencePlayer player = (GeometrySequencePlayer)target;

            GUILayout.Space(20);

            GUILayout.Label("Set Sequence", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(pathRelation);

            relativePath.stringValue =  GUILayout.TextField(relativePath.stringValue);

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

            if(pathRelation.enumValueIndex != (int)GeometrySequenceStream.PathType.RelativeToStreamingAssets && relativePath.stringValue.Length > 1)
                EditorGUILayout.HelpBox("Files are not placed in the StreamingAsset folder. The playback will work on your PC, but likely not if you build/export the project to other devices.", MessageType.Info);


            GUILayout.Space(20);
            GUILayout.Label("Playback Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Playback FPS:");
            targetFPS.floatValue = EditorGUILayout.FloatField(targetFPS.floatValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Play at Start:");
            playAtStart.boolValue = EditorGUILayout.Toggle(playAtStart.boolValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Loop Playback:");
            loopPlay.boolValue = EditorGUILayout.Toggle(loopPlay.boolValue);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);
            GUILayout.Label("Playback Controls", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("FPS: " + Mathf.RoundToInt(player.GetActualFPS()), GUILayout.Width(60));

                if (player.GetFrameDropped())
                    frameDropShowCounter = 0;

                if (frameDropShowCounter < frameDropShowSeconds)
                {
                    Color originalColor = GUI.contentColor;
                    GUI.contentColor = new Color(1, 0.5f, 0.5f);
                    GUILayout.Label("Frame Dropped!");
                    GUI.contentColor = originalColor;
                }

                    frameDropShowCounter += Time.deltaTime;

                GUILayout.EndHorizontal();
            }

            GUI.enabled = Application.isPlaying;


            GUILayout.BeginHorizontal();
            float desiredFrame = EditorGUILayout.Slider(player.GetCurrentFrameIndex(), 0, player.GetTotalFrames());
            EditorGUILayout.LabelField("/ " + player.GetTotalFrames().ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(desiredFrame, player.GetCurrentFrameIndex()))
                player.GoToFrame((int)desiredFrame);

            GUILayout.BeginHorizontal();

            //Rewinds to first frame
            if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.PrevKey")))
                player.PlayFromStart();

            //Goes a few frames back
            if (GUILayout.Button(EditorGUIUtility.IconContent("Profiler.FirstFrame")))
                player.GoToFrame(player.GetCurrentFrameIndex() - (int)targetFPS.floatValue);


            //Pause
            if (player.IsPlaying())
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton")))
                    player.Pause();
            }

            //Play
            else
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.Play")))
                    player.Play();
            }

            //Goes a few frames forward
            if (GUILayout.Button(EditorGUIUtility.IconContent("Profiler.LastFrame"))) 
                player.GoToFrame(player.GetCurrentFrameIndex() + (int)targetFPS.floatValue);

            GUILayout.EndHorizontal();
            GUI.enabled = true;

            serializedObject.ApplyModifiedProperties();
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }
}