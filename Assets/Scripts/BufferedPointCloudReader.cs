using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using System.Globalization;

public struct Frame
{
    public int frameNumber;
    //public float time;
    public bool frameDataIsAvailable;
    public FrameData frameData;
}

public struct FrameData
{
    public Vector3[] frameVertices;
    public Color[] frameColors;
}

public class BufferedPointCloudReader
{
    public string folder;
    public int nFrames = 0;
    public int nFramesRead = 0;
    public float timeOffset = 0.0f;
    public Frame[] frameBuffer;
    private String[] configFileLines;
    public string[] plyFilePaths;

    public BufferedPointCloudReader(string folder)
    {
        this.folder = folder;

        try
        {
            plyFilePaths = Directory.GetFiles(folder, "*.ply");
        }

        catch (Exception e)
        {
            Debug.LogError("Pointcloud path is not valid!");
            return;
        }

        if (plyFilePaths.Length == 0)
            Debug.LogError("Could not find any ply files!");
    }

    public void ReadFrame()
    {
        if (nFramesRead < nFrames)
        {

            if (frameBuffer[nFramesRead].frameDataIsAvailable == false)
            {
                BinaryReader reader = new BinaryReader(new FileStream(plyFilePaths[nFramesRead], FileMode.Open));

                bool doubleUsed = false;
                bool alphaUsed = false;

                string line = "";

                while (!line.Contains("element vertex"))
                {
                    line = readHeaderLine(reader);
                }

                string[] lineElements = line.Split(' ');

                int numberOfPointsInFrame = Int32.Parse(lineElements[2]);

                line = readHeaderLine(reader);

                if (line.Contains("double"))
                    doubleUsed = true;
                else if (line.Contains("float"))
                    doubleUsed = false;

                while (!line.Contains("blue"))
                {
                    line = readHeaderLine(reader);
                }

                line = readHeaderLine(reader);

                if (line.Contains("alpha"))
                    alphaUsed = true;

                bool skipToEnd = true;
                
                //Skip to the end of the header
                if (line.Contains("end_header"))
                {
                    skipToEnd = false;
                }

                if (skipToEnd)
                {
                    while (!line.Contains("end_header"))
                    {
                        line = readHeaderLine(reader);
                    }
                }

                frameBuffer[nFramesRead].frameData.frameVertices = new Vector3[numberOfPointsInFrame];
                frameBuffer[nFramesRead].frameData.frameColors = new Color[numberOfPointsInFrame];

                float x, y, z;
                byte r, g, b, a;

                for (int i = 0; i < numberOfPointsInFrame; i++)
                {
                    if (doubleUsed)
                    {
                        x = (float)reader.ReadDouble();
                        y = (float)reader.ReadDouble();
                        z = (float)reader.ReadDouble();
                    }

                    else
                    {
                        x = reader.ReadSingle();
                        y = reader.ReadSingle();
                        z = reader.ReadSingle();
                    }


                    frameBuffer[nFramesRead].frameData.frameVertices[i] = new Vector3(x, y, -z); // invert z axis from kinect data

                    r = reader.ReadByte();
                    g = reader.ReadByte();
                    b = reader.ReadByte();

                    if (alphaUsed)
                        a = reader.ReadByte();

                    frameBuffer[nFramesRead].frameData.frameColors[i] = new Color((float)r / 256f, (float)g / 256f, (float)b / 256f, 1f);
                }

                frameBuffer[nFramesRead].frameDataIsAvailable = true;

                nFramesRead++;

                reader.Close();
            }
        }
    }



    public void ReadConfig()
    {
        //configFileLines = File.ReadAllLines(folder + "_PointCloudConfig.txt");

        nFrames = Directory.GetFiles(folder, "*.ply").Length;
        frameBuffer = new Frame[nFrames];

        //timeOffset = Convert.ToSingle (configFileLines [0]);

        for (int i = 1; i < nFrames; i++) // first line in file stores time offset
        {
            frameBuffer[i].frameDataIsAvailable = false;
            //frameBuffer[i].time = Convert.ToSingle(configFileLines [i]);
        }

    }

    public void WriteConfig()
    {
        configFileLines[0] = Convert.ToString(timeOffset);
        File.WriteAllLines(folder + "_PointCloudConfig.txt", configFileLines);
    }

    private string currentFileName(int number)
    {
        string myString = number.ToString();
        myString = myString.PadLeft(5, '0');
        return myString;
    }


    private string readHeaderLine(BinaryReader reader)
    {
        char currentChar = 'a';
        string s = "";

        while (currentChar != '\r' && currentChar != '\n')
        {
            currentChar = reader.ReadChar();
            s += currentChar;
        }

        return s;
    }

}

