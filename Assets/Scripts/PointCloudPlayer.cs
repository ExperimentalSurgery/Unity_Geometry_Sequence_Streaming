using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading;

public class PointCloudPlayer : MonoBehaviour {

	public string pathToSequence;

	public PointCloudManager pcManager;

	private BufferedPointCloudReader bpcReader;

	private Thread readerThread = null;
	public int threadCounter = 0;
	public bool runReaderThread = true;

	public float millisElapsedInCurrentPlaySequence = 0f;

	public int numberOfFramesBufferedBeforePlay = 200;
	public bool loopPlay = true;
	private int upperBufferSize = 100;
	private int lowerBufferSize = 10;
	private bool buffering = true;

	public MeshRenderer bufferingText;

	void Start () {

		string[] args = System.Environment.GetCommandLineArgs ();

		for (int i = 0; i < args.Length; i++) {
			if (args [i] == "-folderInput" && i + 1 < args.Length) {
				pathToSequence = args [i + 1];
			}
			if (args [i] == "-bufferSize" && i + 1 < args.Length) {
				numberOfFramesBufferedBeforePlay = Convert.ToInt32(args [i + 1]);
			}
		}

		upperBufferSize = lowerBufferSize + numberOfFramesBufferedBeforePlay;

		SetupReaderAndPCManager ();
	}

	void SetupReaderAndPCManager () {

		bpcReader = new BufferedPointCloudReader(pathToSequence + "/");

		bpcReader.ReadConfig();

		pcManager.setReader(bpcReader);

		readerThread = new Thread(ReaderThreadRunner);

		readerThread.Start();
	}

	void OnApplicationQuit () {
		runReaderThread = false;

		bpcReader.timeOffset = pcManager.timeOffset;

		//bpcReader.WriteConfig ();
	}

	public void HandleUserInput () {
		if (Input.GetKeyDown (KeyCode.Escape)) {
			Application.Quit();
		}
	}

	void Update () {

		HandleUserInput ();

		int BufferCount = bpcReader.nFramesRead - pcManager.currentFrameIndex;

		bool allFramesRead = bpcReader.nFrames == bpcReader.nFramesRead;

		if (allFramesRead) {
			pcManager.playStream = true;
			bufferingText.enabled = false;
			buffering = false;
		} else if (buffering && upperBufferSize <= BufferCount) {
			pcManager.playStream = true;
			bufferingText.enabled = false;
			buffering = false;
		} else if (!buffering && (lowerBufferSize >= BufferCount)) {
			pcManager.playStream = false;
			bufferingText.enabled = true;
			buffering = true;
		}

		if (!buffering) {
			millisElapsedInCurrentPlaySequence += Time.deltaTime * 1000;
		}

		if (pcManager.streamEnded) {
			millisElapsedInCurrentPlaySequence = 0f;

			if (loopPlay) {
				runReaderThread = true;
				pcManager.restartStream = true;
				threadCounter = 0;

				SetupReaderAndPCManager ();
			}

		}
	}

	void ReaderThreadRunner ()
	{
		while (runReaderThread)
		{
			threadCounter++;
			bpcReader.ReadFrame();

			if (bpcReader.nFramesRead == bpcReader.nFrames){
				runReaderThread = false;
			}
		}
	}
}
