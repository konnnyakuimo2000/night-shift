using System.IO;
using NightShift.UI;
using UnityEditor;
using UnityEngine;

namespace NightShift.Editor
{
    /// <summary>同じ譜面からWAVを再生成する。外部サービス・録音・素材ダウンロードは使わない。</summary>
    public static class SoundtrackBaker
    {
        public const string Folder = "Assets/NightShift/Resources/Audio/NightShift";

        [UnityEditor.MenuItem("NightShift/Audio/Rebuild Original Soundtrack")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            foreach (SoundtrackScene scene in System.Enum.GetValues(typeof(SoundtrackScene)))
            {
                string path = Folder + "/" + scene + ".wav";
                float[] samples = SoundtrackLibrary.Render(scene);
                using (var file = new BinaryWriter(File.Create(path)))
                {
                    file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + samples.Length * 2);
                    file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16);
                    file.Write((short)1); file.Write((short)1); file.Write(SoundtrackLibrary.SampleRate);
                    file.Write(SoundtrackLibrary.SampleRate * 2); file.Write((short)2); file.Write((short)16);
                    file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(samples.Length * 2);
                    foreach (float sample in samples) file.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.75f;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("NIGHT SHIFT: BGM 6曲・環境音1本を生成しました。 " + Folder);
        }
    }
}
