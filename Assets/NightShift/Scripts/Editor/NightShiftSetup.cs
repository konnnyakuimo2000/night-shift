using System.Collections.Generic;
using System.IO;
using NightShift.Core;
using NightShift.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// 初回セットアップ:
    /// ・DAY1 データ（社員・権限・ネットワーク・イベント等）を編集可能な .asset として書き出す
    /// ・メインシーンを作成し Build Settings に登録する
    /// 以後、社員データやイベントはアセットを編集すればよく、コード変更は不要。
    /// </summary>
    [InitializeOnLoad]
    public static class NightShiftSetup
    {
        const string DataRoot = "Assets/NightShift/Resources/NightShiftData";
        const string ScenePath = "Assets/NightShift/Scenes/NightShift.unity";
        const string SessionKey = "NightShift.SetupChecked";

        static NightShiftSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(DataRoot + "/GameDatabase.asset")) CreateDataAssets(false);
            else if (!IsDatabaseValid())
            {
                Debug.LogWarning("[NightShift] GameDatabase の参照が壊れているため、DAY1 データアセットを再生成します。");
                CreateDataAssets(false, true);
            }
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                if (!File.Exists(ScenePath)) CreateScene(false);
            }
        }

        static bool IsDatabaseValid()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DataRoot + "/GameDatabase.asset");
            return db != null && db.IsValid();
        }

        /// <summary>コマンドライン用: Unity -batchmode -executeMethod NightShift.EditorTools.NightShiftSetup.CommandLineSetup -quit</summary>
        public static void CommandLineSetup()
        {
            CreateDataAssets(false);
            CreateScene(false);
        }

        [MenuItem("NightShift/Create DAY1 Data Assets")]
        static void CreateDataAssetsMenu() => CreateDataAssets(true);

        [MenuItem("NightShift/Create Main Scene")]
        static void CreateSceneMenu() => CreateScene(true);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void CreateDataAssets(bool interactive, bool force = false)
        {
            if (File.Exists(DataRoot + "/GameDatabase.asset"))
            {
                if (force) AssetDatabase.DeleteAsset(DataRoot);
                else if (!interactive) return;
                else if (!EditorUtility.DisplayDialog("NIGHT//SHIFT", "DAY1 データアセットは既に存在します。初期データで上書きしますか？\n（編集内容は失われます）", "上書き", "キャンセル"))
                    return;
                else AssetDatabase.DeleteAsset(DataRoot);
            }

            var bundle = DefaultDataFactory.Build();
            EnsureFolder(DataRoot);
            var used = new HashSet<string>();
            foreach (var kv in bundle.objects)
            {
                var obj = kv.Key;
                string folder = string.IsNullOrEmpty(kv.Value) ? DataRoot : DataRoot + "/" + kv.Value;
                EnsureFolder(folder);
                string path = $"{folder}/{obj.name}.asset";
                int n = 1;
                while (used.Contains(path)) path = $"{folder}/{obj.name}_{n++}.asset";
                used.Add(path);
                AssetDatabase.CreateAsset(obj, path);
            }
            // 参照先がすべてアセット化された後に再シリアライズして、相互参照を確定させる
            foreach (var kv in bundle.objects) EditorUtility.SetDirty(kv.Key);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NightShift] DAY1 data assets created under {DataRoot} ({bundle.objects.Count} assets).");
        }

        public static void CreateScene(bool interactive)
        {
            if (File.Exists(ScenePath) && interactive &&
                !EditorUtility.DisplayDialog("NIGHT//SHIFT", "シーンは既に存在します。作り直しますか？", "作り直す", "キャンセル"))
                return;
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/NightShift/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            var c = cam.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.orthographic = true;
            new GameObject("NightShift.GameManager", typeof(GameManager));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[NightShift] Scene created: {ScenePath}");
        }
    }
}
