using ActionMenuApi.Api;
using HarmonyLib;
using MelonLoader;
using MuteTTS;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UIExpansionKit.API;
using UnhollowerBaseLib;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MuteTTSMod), "MuteTTS", "1.0.5", "Eric van Fandenfart")]
[assembly: MelonAdditionalDependencies("ActionMenuApi", "UIExpansionKit")]
[assembly: MelonGame]

namespace MuteTTS
{
    public class MuteTTSMod : MelonMod
    {
        private static MemoryStream stream = new MemoryStream();
        private static AudioSource audiosource = null;
        private static bool playing = false;
        private static bool lastMuteValue;
        private string lastLineRead;
        private string exeLocation;
        private MelonPreferences_Entry<int> useVoiceSetting;
        private static MelonPreferences_Entry<bool> blockMic;
        private static MelonPreferences_Entry<float> TTSSpeed;
        private static MelonPreferences_Entry<float> TTSVolume;

        private MethodInfo UseKeyboardOnlyForText;


        public override void OnApplicationStart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MelonLogger.Msg("MuteTTS is only available for Windows");
                return;
            }

            MelonPreferences_Category category = MelonPreferences.CreateCategory("MuteTTS");
            useVoiceSetting = category.CreateEntry("UseVoice", -1);

            blockMic = category.CreateEntry("BlockMic", false, description: "VRC will no longer be able to send your Voice. Only TTS is available");
            TTSVolume = category.CreateEntry("TTS Volume", 1f, description: "Value between 0 and 1");
            TTSSpeed = category.CreateEntry("TTS Speed", 1f);

            ExtractExecutable();
            
            VRCActionMenuPage.AddButton(ActionMenuPage.Main, "TTS", () => CreateTextPopup());
            LogAvailableVoices();
            UseKeyboardOnlyForText = typeof(VRCInputManager).GetMethods().First(mi => mi.Name.StartsWith("Method_Public_Static_Void_Boolean_0") && mi.GetParameters().Count() == 1);

            HarmonyInstance.Patch(typeof(AudioClip).GetMethod("GetData", BindingFlags.Instance | BindingFlags.Public), postfix: new HarmonyMethod(typeof(MuteTTSMod).GetMethod("Get", BindingFlags.Static | BindingFlags.Public)));
        }

        private void ExtractExecutable()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string path = "Executables";
                exeLocation = Path.GetFullPath(path + "/MuteTTSClient.exe");

                Directory.CreateDirectory(path);
                using (var stream = assembly.GetManifestResourceStream("MuteTTS.MuteTTSClient.exe"))
                {
                    using (FileStream file = new FileStream(exeLocation, FileMode.Create))
                        stream.CopyTo(file);
                }
                MelonLogger.Msg($"Extracted file to {exeLocation}");
            }
            catch (Exception)
            {
                MelonLogger.Msg("Couldnt extract exe file. Maybe file is in use and its not an error");
            }
        }

        private void LogAvailableVoices()
        {
            ProcessStartInfo startInfo = CreateDefaultStartInfo();

            startInfo.Arguments = $"ListVoices";
            MelonLogger.Msg("Available Voices:");
            using (Process exeProcess = Process.Start(startInfo))
            {
                ConsumeReader(exeProcess.StandardOutput, true);
                exeProcess.WaitForExit();
            }
        }

        private async void ConsumeReader(TextReader reader, bool justPrint)
        {
            string text;

            while ((text = await reader.ReadLineAsync()) != null)
            {
                if (justPrint)
                    MelonLogger.Msg(text);
                else
                    lastLineRead = text;
            }
        }

        private void GetVoice(string msg = "Hello World")
        {
            Task.Run(()=>{
                ProcessStartInfo startInfo = CreateDefaultStartInfo();

                msg = msg.Replace("\\", "").Replace("\"", "");

                startInfo.Arguments = $"PlayVoice \"{msg}\" {useVoiceSetting.Value} {Convert.ToString(TTSSpeed.Value,CultureInfo.InvariantCulture)} {Convert.ToString(TTSVolume.Value,CultureInfo.InvariantCulture)}";

                MelonLogger.Msg($"Calling excutable with parameters {startInfo.Arguments.Replace(msg, "***")}");
                
                using (Process exeProcess = Process.Start(startInfo))
                {
                    ConsumeReader(exeProcess.StandardOutput, false);
                    exeProcess.WaitForExit();
                    byte[] buffer = Convert.FromBase64String(lastLineRead);
                    MelonLogger.Msg($"Recieved {buffer.Length} bytes from excutable to play");
                    stream = new MemoryStream();
                    stream.Write(buffer, 0, buffer.Length);
                    stream.Position = 0;

                    if (audiosource == null)
                        audiosource = CreateAudioSource();
                    audiosource.clip = CreateAudioClipFromStream(buffer);
                    audiosource.Play();
                    playing = true;
                    lastMuteValue = DefaultTalkController.field_Private_Static_Boolean_0;
                    DefaultTalkController.field_Private_Static_Boolean_0 = false;//Unmute
                }
            });
        }

        private AudioClip CreateAudioClipFromStream(byte[] buffer)
        {
            AudioClip myClip = AudioClip.Create("Test", buffer.Length, 1, 48000, false);
            Il2CppStructArray<float> t = new Il2CppStructArray<float>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                t[i] = ((float)buffer[i] - 128) / 128;

            }
            myClip.SetData(t, 0);
            return myClip;
        }

        private ProcessStartInfo CreateDefaultStartInfo()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = exeLocation,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return startInfo;
        }

        private void CreateTextPopup()
        {
            UseKeyboardOnlyForText.Invoke(null, new object[] { true });

            BuiltinUiUtils.ShowInputPopup("MuteTTS", "", InputField.InputType.Standard, false, "Send", (message, _, _2) =>
            {
                UseKeyboardOnlyForText.Invoke(null, new object[] { false });
                GetVoice(message);
            },()=> { UseKeyboardOnlyForText.Invoke(null, new object[] { false }); });

        }

        public AudioSource CreateAudioSource()
        {
            GameObject obj = new GameObject("MuteTTS");
            GameObject.DontDestroyOnLoad(obj);

            AudioSource audioSource = obj.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;

            return audioSource;
        }


        public static void Get(AudioClip __instance, ref Il2CppStructArray<float> data, int offsetSamples)
        {
            if (__instance?.name != "Microphone") return;
            //MelonLogger.Msg(data.Length); //== 960
            //MelonLogger.Msg(offsetSamples); -> inc by 960
            //MelonLogger.Msg(__instance.frequency); //== 48000
            //MelonLogger.Msg(__instance.length); //== 240000
            //MelonLogger.Msg(__instance.samples); //==5
            //MelonLogger.Msg(__instance.channels); //== 1

            if (playing)
            {
                byte[] buffer = new byte[data.Count];
                int read = stream.Read(buffer, 0, data.Count);

                if (read == 0)
                {
                    playing = false;
                    DefaultTalkController.field_Private_Static_Boolean_0 = lastMuteValue;//Restore mute state
                    return;
                }

                for (int i = 0; i < data.Count; i++)
                {
                    if (i < read)
                        data[i] = ((float)buffer[i] - 128) / 128;
                    else
                        data[i] = 0;
                }

            }else if (blockMic.Value)
            {
                for (int i = 0; i < data.Count; i++)
                    data[i] = 0;
            }
            
        }
    }
}
