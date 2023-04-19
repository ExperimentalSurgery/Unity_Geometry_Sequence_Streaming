using UnityEditor;
using UnityEngine;
using System.IO;
using GeometrySequence.Streaming;

namespace GeometrySequence.Streaming
{

    [CustomEditor(typeof(GeometrySequenceStream))]
    [CanEditMultipleObjects]
    public class GeometrySequenceGUI : Editor
    {
        bool showBufferingFoldout;
        bool showDebugFoldout;
        SerializedProperty pointcloudMaterial;
        SerializedProperty meshMaterial;
        SerializedProperty meshUnlitMaterial;

        float frameDropShowSeconds = 0.1f;
        float frameDropShowCounter = 0;

        private void OnEnable()
        {
            pointcloudMaterial = serializedObject.FindProperty("pointCloudMaterial");
            meshMaterial = serializedObject.FindProperty("meshMaterial");
            meshUnlitMaterial = serializedObject.FindProperty("meshMaterialTextured");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            GeometrySequenceStream stream = (GeometrySequenceStream)target;

            GUILayout.Space(20);

            GUILayout.Label("Set Sequence", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Sequence Folder"))
            {
                string path = EditorUtility.OpenFolderPanel("Open a folder that contains a Geometry Sequence", stream.pathToSequence, "");

                if (path != null)
                {
                    if (Directory.Exists(path))
                    {
                        if (Directory.GetFiles(path, "*.ply").Length > 0)
                        {
                            stream.pathToSequence = path;
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

            if (stream.pathToSequence != null)
            {
                if (stream.pathToSequence.Length > 0)
                {
                    GUILayout.Label("Sequence Path: " + stream.pathToSequence, EditorStyles.wordWrappedLabel);
                }
            }

            GUILayout.Space(20);
            GUILayout.Label("Playback Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Playback FPS:");
            stream.playbackFPS = EditorGUILayout.IntField(stream.playbackFPS);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Play:");
            stream.autoplay = EditorGUILayout.Toggle(stream.autoplay);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Loop Playback:");
            stream.loopPlay = EditorGUILayout.Toggle(stream.loopPlay);
            EditorGUILayout.EndHorizontal();

            showBufferingFoldout = EditorGUILayout.Foldout(showBufferingFoldout, "Buffering Settings");
            if (showBufferingFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Frame Buffer Size:");
                stream.bufferSize = EditorGUILayout.IntField(stream.bufferSize);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Use all available threads:");
                stream.useAllThreads = EditorGUILayout.Toggle(stream.useAllThreads);
                EditorGUILayout.EndHorizontal();

                if (!stream.useAllThreads)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Maximum number of threads:");
                    stream.threadCount = EditorGUILayout.IntField(stream.threadCount);
                    GUILayout.EndHorizontal();
                }

            }

            //showDebugFoldout = EditorGUILayout.Foldout(showDebugFoldout, "Debug Settings");
            //if (showDebugFoldout)
            //{
            //}

            GUILayout.Space(20);
            GUILayout.Label("Materials", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(pointcloudMaterial);
            EditorGUILayout.PropertyField(meshMaterial);
            EditorGUILayout.PropertyField(meshUnlitMaterial);

            GUILayout.Space(20);
            GUILayout.Label("Playback Controls", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("FPS: " + Mathf.RoundToInt(stream.GetCurrentFPS()), GUILayout.Width(60));

                if (stream.GetFrameDropped())
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
            float desiredFrame = EditorGUILayout.Slider(stream.GetCurrentFrameIndex(), 0, stream.GetTotalFrames());
            EditorGUILayout.LabelField("/ " + stream.GetTotalFrames().ToString(), GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(desiredFrame - stream.GetCurrentFrameIndex()) > 3)
                stream.SkipToFrame((int)desiredFrame);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.PrevKey")))
                stream.PlayFromStart();

            if (GUILayout.Button(EditorGUIUtility.IconContent("Profiler.FirstFrame")))
                stream.SkipToFrame(stream.GetCurrentFrameIndex() - stream.playbackFPS);


            if (stream.IsPlaying())
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton")))
                    stream.Pause();
            }

            else
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.Play")))
                    stream.Play();
            }


            if (GUILayout.Button(EditorGUIUtility.IconContent("Profiler.LastFrame")))
                stream.SkipToFrame(stream.GetCurrentFrameIndex() + stream.playbackFPS);

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