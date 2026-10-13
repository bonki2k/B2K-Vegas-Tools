using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ScriptPortal.Vegas;

public class EntryPoint
{
    // 3 Frames bei 60 FPS = 50 ms, unabhaengig von der Projekt-Framerate.
    private const double OffsetSeconds = 3.0 / 60.0;

    public void FromVegas(Vegas vegas)
    {
        // Von unten nach oben arbeiten, damit neue Spuren die Indizes nicht verschieben.
        for (int i = vegas.Project.Tracks.Count - 1; i >= 0; i--)
        {
            AudioTrack originalTrack = vegas.Project.Tracks[i] as AudioTrack;
            if (originalTrack == null) continue;

            List<AudioEvent> selected = new List<AudioEvent>();
            foreach (TrackEvent ev in originalTrack.Events)
            {
                AudioEvent audio = ev as AudioEvent;
                if (audio != null && audio.Selected && audio.ActiveTake != null)
                    selected.Add(audio);
            }
            if (selected.Count == 0) continue;

            AudioTrack wavTrack = null;
            foreach (AudioEvent audio in selected)
            {
                Take take = audio.ActiveTake;
                string source = take.MediaPath;
                if (!File.Exists(source)) continue;

                // In FFmpeg die gleiche Audiospur wie im VEGAS-Take auswaehlen.
                int audioStream = 0;
                foreach (MediaStream stream in take.Media.Streams)
                {
                    if (stream.MediaType != MediaType.Audio) continue;
                    if (stream.Equals(take.MediaStream)) break;
                    audioStream++;
                }

                double seek = Math.Max(0.0, take.Offset.ToMilliseconds() / 1000.0);
                double rate = audio.PlaybackRate;
                if (rate <= 0.0) rate = 1.0;
                double duration = audio.Length.ToMilliseconds() / 1000.0 * rate;
                if (duration <= 0.0) continue;

                string folder = Path.Combine(Path.GetDirectoryName(source), "B2K_WAV");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder,
                    Path.GetFileNameWithoutExtension(source) + "_a" + (audioStream + 1) + "_" +
                    Guid.NewGuid().ToString("N").Substring(0, 8) + ".wav");

                string fmtSeek = seek.ToString("0.######", CultureInfo.InvariantCulture);
                string fmtDuration = duration.ToString("0.######", CultureInfo.InvariantCulture);
                string args = "-hide_banner -loglevel error -nostdin -y " +
                    (audio.Loop ? "-stream_loop -1 " : "") +
                    "-ss " + fmtSeek + " -i \"" + source + "\" " +
                    "-map 0:a:" + audioStream + " -vn -t " + fmtDuration +
                    " -ar 48000 -c:a pcm_s16le \"" + file + "\"";

                ProcessStartInfo psi = new ProcessStartInfo("ffmpeg.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0 || !File.Exists(file))
                        throw new Exception("FFmpeg-Konvertierung fehlgeschlagen: " + error);
                }

                if (wavTrack == null)
                {
                    // VEGAS Tracks ist keine normale List<T>: Insert() wirft NotSupportedException.
                    // Die gewuenschte Spurposition wird stattdessen beim Erstellen gesetzt.
                    wavTrack = new AudioTrack(vegas.Project, originalTrack.Index + 1, "B2K WAV");
                    vegas.Project.Tracks.Add(wavTrack);
                }

                Media media = Media.CreateInstance(vegas.Project, file);
                AudioEvent newEvent = wavTrack.AddAudioEvent(
                    audio.Start + Timecode.FromSeconds(OffsetSeconds), audio.Length);
                newEvent.AddTake(media.GetAudioStreamByIndex(0));
                newEvent.PlaybackRate = rate;
                newEvent.Loop = false;
            }
        }
    }
}
