using System;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.IO;
using System.Globalization;

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
                        if (args.Length < 5)
                            Environment.Exit(2);
                        string message = args[1];
                        int voice = Convert.ToInt32(args[2], CultureInfo.InvariantCulture);
                        float speed = Convert.ToSingle(args[3], CultureInfo.InvariantCulture);
                        float volume = Convert.ToSingle(args[4], CultureInfo.InvariantCulture);
                        CreateVoice(message, voice, speed, volume);
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

        private static void CreateVoice(string text, int voice = -1, float speed = 1, float volume = 1)
        {
            var s = new MemoryStream();
           

            if(voice!=-1 && voice < synth.GetInstalledVoices().Count)
                synth.SelectVoice(synth.GetInstalledVoices()[voice].VoiceInfo.Name);
            
            if (speed < 0.1) speed = 0.1f;
            synth.SetOutputToAudioStream(s, new SpeechAudioFormatInfo((int) (48000 / speed), AudioBitsPerSample.Eight, AudioChannel.Mono));
            //synth.SetOutputToDefaultAudioDevice();
            synth.Speak(text);

            if (volume > 1) volume = 1;
            if (volume < 0) volume = 0;

            byte[] result = s.ToArray();
            float temp;
            for (int i = 0; i < result.Length; i++)
            {
                temp = result[i] - 128;
                temp *= volume;
                result[i] = (byte)(temp + 128);
            }

            Console.Write(Convert.ToBase64String(result));
        }
    }
}
