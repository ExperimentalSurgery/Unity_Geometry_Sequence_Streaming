using System;
using System.IO;
using UnityEngine;


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
            UnityEngine.Debug.LogError("Pointcloud path is not valid!");
            return;
        }

        if (plyFilePaths.Length == 0)
            UnityEngine.Debug.LogError("Could not find any ply files!");
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

                byte[] byteBuffer = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                reader.Close();

                float x, y, z;
                byte r, g, b, a;
                int bufferPosition = 0;

                for (int i = 0; i < numberOfPointsInFrame; i++)
                {
                    if (doubleUsed)
                    {
                        x = (float)BitConverter.ToDouble(byteBuffer, bufferPosition);
                        y = (float)BitConverter.ToDouble(byteBuffer, bufferPosition + sizeof(double));
                        z = (float)BitConverter.ToDouble(byteBuffer, bufferPosition + 2 * sizeof(double));

                        bufferPosition += 3 * sizeof(double);
                    }

                    else
                    {
                        x = BitConverter.ToSingle(byteBuffer, bufferPosition);
                        y = BitConverter.ToSingle(byteBuffer, bufferPosition + sizeof(float));
                        z = BitConverter.ToSingle(byteBuffer, bufferPosition + 2 * sizeof(float));

                        bufferPosition += 3 * sizeof(float);
                    }

                    frameBuffer[nFramesRead].frameData.frameVertices[i].x = x;
                    frameBuffer[nFramesRead].frameData.frameVertices[i].y = y;
                    frameBuffer[nFramesRead].frameData.frameVertices[i].z = -z; // Invert Z Axis, to match Unitys Coordinate System

                    r = byteBuffer[bufferPosition];
                    g = byteBuffer[bufferPosition + 1];
                    b = byteBuffer[bufferPosition + 2];

                    bufferPosition += 3;

                    if (alphaUsed)
                    {
                        a = byteBuffer[bufferPosition];
                        bufferPosition++;
                    }

                    frameBuffer[nFramesRead].frameData.frameColors[i].r = r / 256f;
                    frameBuffer[nFramesRead].frameData.frameColors[i].g = g / 256f;
                    frameBuffer[nFramesRead].frameData.frameColors[i].b = b / 256f; 
                }

                frameBuffer[nFramesRead].frameDataIsAvailable = true;

                nFramesRead++;

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

