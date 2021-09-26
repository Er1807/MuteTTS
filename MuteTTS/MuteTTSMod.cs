using ActionMenuApi.Api;
using HarmonyLib;
using MelonLoader;
using MuteTTS;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UIExpansionKit.API;
using UnhollowerBaseLib;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MuteTTSMod), "MuteTTS", "1.0.2", "Eric van Fandenfart")]
[assembly: MelonAdditionalDependencies("ActionMenuApi", "UIExpansionKit")]
[assembly: MelonGame]

namespace MuteTTS
{
    public class MuteTTSMod : MelonMod
    {
        private static MemoryStream stream = new MemoryStream();
        private static AudioSource audiosource = new AudioSource();
        private static bool playing = false;

        private string lastLineRead;
        private string exeLocation;
        private MelonPreferences_Entry<int> useVoiceSetting;

        private MethodInfo UseKeyboardOnlyForText;

        

        public override void OnApplicationStart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MelonLogger.Msg("MuteTTS is only available for Windows");
                return;
            }
            useVoiceSetting = MelonPreferences.CreateCategory("MuteTTS").CreateEntry("UseVoice", -1);
            ExtractExecutable();
            VRCActionMenuPage.AddButton(ActionMenuPage.Main, "TTS", () => CreateTextPopup());
            //VRCActionMenuPage.AddButton(ActionMenuPage.Main, "ListTTS", () => LogAvailableVoices());
            LogAvailableVoices();

            UseKeyboardOnlyForText = typeof(VRCInputManager).GetMethods().First(mi => mi.Name.StartsWith("Method_Public_Static_Void_Boolean_PDM") && mi.GetParameters().Count() == 1);

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
                if (useVoiceSetting.Value == -1)
                    startInfo.Arguments = $"PlayVoice \"{msg}\"";
                else
                    startInfo.Arguments = $"PlayVoice \"{msg}\" {useVoiceSetting.Value}";


                using (Process exeProcess = Process.Start(startInfo))
                {
                    ConsumeReader(exeProcess.StandardOutput, false);
                    exeProcess.WaitForExit();
                    byte[] buffer = Convert.FromBase64String(lastLineRead);
                    stream = new MemoryStream();
                    stream.Write(buffer, 0, buffer.Length);
                    stream.Position = 0;

                    if (audiosource == null)
                        audiosource = CreateAudioSource();
                    audiosource.clip = CreateAudioClipFromStream(buffer);
                    audiosource.Play();
                    playing = true;

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
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.FileName = exeLocation;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            return startInfo;
        }

        private void CreateTextPopup()
        {
            UseKeyboardOnlyForText.Invoke(null, new object[] { true });
            VRCInputManager.Method_Public_Static_Void_Boolean_PDM_0(true);
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
                    return;
                }

                for (int i = 0; i < data.Count; i++)
                {
                    if (i < read)
                        data[i] = ((float)buffer[i] - 128) / 128;
                    else
                        data[i] = 0;
                }

            }
        }
    }
}
