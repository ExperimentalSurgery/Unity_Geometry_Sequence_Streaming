
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class PointCloudManager : MonoBehaviour
{
	public int currentFrameIndex = 0;
	//private int nextFrameIndex = 1;
	public float timeOffset = 0f;
	public float fps = 30f;
	private float playNextFrameTime;
	private float nextFrameOffsetTime = 0f;

    public Material pointCloudMaterial;

	public GameObject pointCloudRendererGameObject;
	public GameObject thisPointCloudRendererGameObject;
	private PointCloudRenderer pointCloudRenderer;

	public bool playStream = false;
	public bool restartStream = false;
	public bool streamEnded = false;

	public PointCloudPlayer pcPlayer;
	private BufferedPointCloudReader reader;

    void Start()
    {
		thisPointCloudRendererGameObject = GameObject.Instantiate(pointCloudRendererGameObject);
		thisPointCloudRendererGameObject.transform.SetParent(transform);
		thisPointCloudRendererGameObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
		thisPointCloudRendererGameObject.transform.localRotation = Quaternion.identity;
		thisPointCloudRendererGameObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

		pointCloudRenderer = thisPointCloudRendererGameObject.GetComponent<PointCloudRenderer> ();
    }

	public void setReader (BufferedPointCloudReader _reader) {
		reader = _reader;
		timeOffset = _reader.timeOffset;
	}

    void Update()
    {
		if (restartStream) {
			currentFrameIndex = 0;
			restartStream = false;
			streamEnded = false;
		}

		if (playStream && !streamEnded) 
		{

			// Render new pointCloudFrame only if enough time has passed
			//if (pcPlayer.millisElapsedInCurrentPlaySequence >= currentFrameOffsetTime) {
			if (pcPlayer.millisElapsedInCurrentPlaySequence >= playNextFrameTime) {

				// render frames only if point data is available
				if (reader.frameBuffer[currentFrameIndex].frameDataIsAvailable) {
					// renderer sends vertices and colors to GPU

					pointCloudRenderer.UpdateMesh (reader.frameBuffer [currentFrameIndex].frameData.frameVertices, reader.frameBuffer [currentFrameIndex].frameData.frameColors, Matrix4x4.identity);

                    //reader.frameBuffer[currentFrameIndex].frameData.frameVertices = null;
                    //reader.frameBuffer[currentFrameIndex].frameData.frameColors = null;
                    //reader.frameBuffer[currentFrameIndex].frameDataIsAvailable = false;
				}

				currentFrameIndex++;
				playNextFrameTime += 1000 / fps;

			}
			if (currentFrameIndex > reader.nFrames - 1) {
				currentFrameIndex = 0;
				playNextFrameTime = 0;
				pcPlayer.millisElapsedInCurrentPlaySequence = 0;
				//Debug.Log ("Mesh cleared");
				//playStream = false;
				//streamEnded = true;
			}
		} 
    }
}

