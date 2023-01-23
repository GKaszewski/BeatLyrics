# BeatLyrics
Adds lyrics to Beat Saber. It's a fork of BeatSinger by Grégoire Geis

[BeatSinger](https://github.com/71/BeatSinger)

## Installation
This mod depends on [SongCore](https://github.com/Kylemc1413/SongCore), you need it for this mod to function properly. If you have SongCore installed drop the BeatLyrics.dll
into Plugins folder.

## How to use this mod
This mod uses srt files to get the lyrics data. For each level you need to put an srt file into level's folder.
For example:
1. Go to Beat Saber_Data/CustomLevels
2. Go to your chosen level directory
3. Put `lyrics.srt` file there. (**IMPORTANT** the lyrics file must be named **lyrics.srt** otherwise it will not load.)
4. Open Beat saber, choose your level and enjoy the lyrics!

### Example srt file
```
0
00:00:00,000 --> 00:00:00,000
by RentAnAdviser.com
1
00:00:00,200 --> 00:00:02,900
When the days are cold
2
00:00:03,000 --> 00:00:05,600
And the cards all fold
3
00:00:05,700 --> 00:00:08,200
And the saints we see
4
00:00:08,300 --> 00:00:11,000
Are all made of gold
```

## Tools
In near future I am going to create a tool that lets you generate srt file for now you can use already available online tools.
Like this one: [LrcGenerator](https://lrcgenerator.com/)

