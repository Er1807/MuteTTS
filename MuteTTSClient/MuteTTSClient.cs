using System;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.IO;

namespace MuteTTSClient
{
    class MuteTTSClient
    {
        static SpeechSynthesizer synth = new SpeechSynthesizer();
        static void Main(string[] args)
        {
            if (args.Length >= 1)
            {
                switch (args[0])
                {
                    case "ListVoices":
                        var voices = synth.GetInstalledVoices();
                        for (int i = 0; i < voices.Count; i++)
                        {
                            Console.WriteLine($"{i} {voices[i].VoiceInfo.Description}");
                        }
                        Environment.Exit(1);
                        break;
                    case "PlayVoice":
                        if (args.Length < 2)
                            Environment.Exit(2);
                        if (args.Length == 2)
                            CreateVoice(args[1]);
                        if (args.Length == 3)
                            CreateVoice(args[1], Convert.ToInt32(args[2]));
                        break;
                    default:
                        Environment.Exit(3);
                        break;
                }
            }
            else
            {
                Environment.Exit(-1);
            }
        }

        private static void CreateVoice(string text, int voice = -1)
        {
            var s = new MemoryStream();
           

            foreach (var item in synth.GetInstalledVoices())
            {
                Console.WriteLine(item.VoiceInfo.Name);
            }

            if(voice!=-1 && voice < synth.GetInstalledVoices().Count)
                synth.SelectVoice(synth.GetInstalledVoices()[voice].VoiceInfo.Name);


            synth.SetOutputToAudioStream(s, new SpeechAudioFormatInfo(48000, AudioBitsPerSample.Eight, AudioChannel.Mono));
            synth.Speak(text);


            Console.Write(Convert.ToBase64String(s.ToArray()));
        }
    }
}
