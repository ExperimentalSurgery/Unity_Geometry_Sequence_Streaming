# Unity Pointcloud Player


## Overview
This plugin allows you to read and play sequences of animated pointclouds. The pointclouds have to be in the binary .ply format, data is streamed live from disk, with an adjustable buffer. The plugin has no dependencies, and should be usable in all Unity Versions!

## Setup
Either download the whole repository and open it up as Unity project, or download just the package from the Releases Tab. If you download the package, import it into Unity. You need at least a Pointcloud Manager, a Pointcloud Renderer and Pointcloud Player in your scene to get started. Take a look at the example scene on how to set it up!

Drop your numbered binary .ply files into the Data folder. Then in the Pointcloud Manager, change the relative path of "Path to sequence" to point to your folder. Now you can hit the play button, your sequence should start! Use the parameters on the Pointcloud Material, to adjust the look of your pointcloud. Especially the point size may need to be adjusted.


## Example Scene / Data
The example scene demonstrates the setup of all scripts and gameobjects. If you have downloaded the whole Unity Repository, you already downloaded the test data. If you choose to download the package, please download [all the .ply files here](https://github.com/ExperimentalSurgery/Unity_Pointcloud_Player/tree/main/Data/Example) and the put them, relative to your projects root folder, under Data/Example.
Now open the Example Scene and hit play, you should see a very short sequence of an animated pointcloud playing!

## Issues
If you find any issues with this project, please report them in the issue section

