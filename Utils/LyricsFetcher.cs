using SongCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatLyrics.Utils {

    public sealed class Lyrics {
        public string Text { get; }
        public float Time { get; }
        public float? EndTime { get; }

        public Lyrics(string text, float time, float end) {
            Text = text;
            Time = time;
            EndTime = end;
        }
    }

    public static class LyricsFetcher {
        private enum ParserState {
            Number = 0,
            Time = 1,
            Text = 2
        }

        private static void Invalid(List<Lyrics> lyrics) {
            Debug.Log("[Beat Singer] Invalid subtiles file found, cancelling load...");
            lyrics.Clear();
        }
        
        private static void CreateFromSrt(TextReader reader, List<Lyrics> lyrics) {
            ParserState state = ParserState.Number;
            float startTime = 0f;
            float endTime = 0f;

            StringBuilder text = new StringBuilder();
            string line;
            while ((line = reader.ReadLine()) != null) {
                switch (state) {
                    case ParserState.Number:
                        if (string.IsNullOrEmpty(line))
                            continue;
                        if (!int.TryParse(line, out int _))
                            Invalid(lyrics);
                        state = ParserState.Time;
                            break;
                    case ParserState.Time:
                        var match = Regex.Match(line, @"(\d+):(\d+):(\d+,\d+) *--> *(\d+):(\d+):(\d+,\d+)");
                        if (!match.Success) Invalid(lyrics);

                        startTime = int.Parse(match.Groups[1].Value) * 3600f +
                                    int.Parse(match.Groups[2].Value) * 60f +
                                    float.Parse(match.Groups[3].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        endTime = int.Parse(match.Groups[4].Value) * 3600f
                            + int.Parse(match.Groups[5].Value) * 60f
                            + float.Parse(match.Groups[6].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        state = ParserState.Text;
                        break;
                    case ParserState.Text:
                        if (string.IsNullOrEmpty(line)) {
                            lyrics.Add(new Lyrics(text.ToString(), startTime, endTime));
                            text.Length = 0;
                            state = ParserState.Number;
                        } else {
                            text.AppendLine(line);
                        }
                        break;
                    default:
                        throw new Exception();
                }
            }
        }

        public static bool GetLocalLyrics(string songId, List<Lyrics> lyrics) {
            var songDirectory = Loader.CustomLevels.Values.FirstOrDefault(x => x.levelID == songId)?.customLevelPath;
            if (songDirectory == null) return false;
            var srtFile = Path.Combine(songDirectory, "lyrics.srt");
            if (!File.Exists(srtFile)) {
                Debug.Log($"Couldnt find lyrics.srt for {songId}");
                return false;
            }
            Debug.Log($"Found lyrics.srt for {songId}");
            using (FileStream fs = File.OpenRead(srtFile))
            using (var reader = new StreamReader(fs)) {
                CreateFromSrt(reader, lyrics);
                return true;
            }
        }
    }
}
