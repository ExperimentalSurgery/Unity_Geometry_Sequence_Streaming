using GeometrySequence.Streaming;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeometrySequenceAPIExample : MonoBehaviour
{
    string sequencePath = "Examples\\ExampleData\\Twisty_Box";
    GeometrySequencePlayer player;

    int loopsPlayed = 0;

    // Start is called before the first frame update
    void Start()
    {
        //Get our player.
        player = GetComponent<GeometrySequencePlayer>();

        //First to load our sequence. In this case the path is inside of the Assets folder (in the Editor, this is also the Data Path),
        //so we set it as relative to our data path. We also set our desired target playback framerate here
        player.LoadSequence(sequencePath, GeometrySequencePlayer.PathType.RelativeToDataPath, 30);

        //Disable automatic looping and automatic playback.
        player.SetLoopPlay(false);
        player.SetAutoStart(false);

        //Now we start the playback from the start. In this case, you could also just set the autostart variable to true,
        //inside of the LoadSequence function
        player.PlayFromStart();
    }

    // Update is called once per frame
    void Update()
    {
        //Get the total length ins seconds of our sequence
        float totalTime = player.GetTotalTime();

        //Check how much of our sequence has played
        float currentTime = player.GetCurrentTime();

        //If half of our sequence has played, we want to start the sequence from the beginning again for three times
        if (currentTime > totalTime / 2 && loopsPlayed < 3)
        {
            player.GoToTime(0);
            loopsPlayed++;
        }

    }
}
