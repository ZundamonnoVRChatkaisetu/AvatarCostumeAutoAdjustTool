using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ボーン情報、マッピング情報、調整設定などのデータを管理するクラス
    /// </summary>
    public class DataManager
    {
        // シングルトンインスタンス
        private static DataManager _instance;
        public static DataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DataManager();
                }
                return _instance;
            }
        }

        // デフォルトの保存ディレクトリ
        private const string DEFAULT_SAVE_DIRECTORY = "AvatarCostumeAdjustTool";
        private const string PRESETS_DIRECTORY = "Presets";
        private const string MAPPINGS_DIRECTORY = "Mappings";

        // 初期化フラグ
        private bool _initialized = false;

        // ボーン命名パターンデータ
        private List<BoneNamingPattern> _boneNamingPatterns;
        public List<BoneNamingPattern> BoneNamingPatterns 
        { 
            get 
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _boneNamingPatterns; 
            }
        }

        // 身体部位参照データ
        private BodyPartReferenceData _bodyPartReferenceData;
        public BodyPartReferenceData BodyPartReferenceData 
        { 
            get 
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _bodyPartReferenceData; 
            }
        }

        // 保存済みマッピングデータのキャッシュ
        private Dictionary<string, MappingData> _savedMappings;
        public Dictionary<string, MappingData> SavedMappings
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _savedMappings;
            }
        }

        // 保存済み調整設定のキャッシュ
        private Dictionary<string, AdjustmentSettings> _savedAdjustmentSettings;
        public Dictionary<string, AdjustmentSettings> SavedAdjustmentSettings
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _savedAdjustmentSettings;
            }
        }

        /// <summary>
        /// データマネージャーを初期化し、必要なデータを読み込む
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            // 必要なディレクトリを作成
            CreateDirectories();

            // ボーン命名パターンデータを読み込み
            LoadBoneNamingPatterns();

            // 身体部位参照データを読み込み
            LoadBodyPartReferenceData();

            // 保存済みマッピングデータを読み込み
            LoadSavedMappings();

            // 保存済み調整設定を読み込み
            LoadSavedAdjustmentSettings();

            _initialized = true;
        }

        /// <summary>
        /// 必要なディレクトリ構造を作成
        /// </summary>
        private void CreateDirectories()
        {
            string rootPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY);
            string presetsPath = Path.Combine(rootPath, PRESETS_DIRECTORY);
            string mappingsPath = Path.Combine(rootPath, MAPPINGS_DIRECTORY);
            
            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);
            
            if (!Directory.Exists(presetsPath))
                Directory.CreateDirectory(presetsPath);
            
            if (!Directory.Exists(mappingsPath))
                Directory.CreateDirectory(mappingsPath);
        }

        /// <summary>
        /// ボーン命名パターンデータを読み込み
        /// </summary>
        private void LoadBoneNamingPatterns()
        {
            try
            {
                _boneNamingPatterns = JsonUtils.LoadFromResources<BoneNamingPatternList>("BoneNamingPatterns").Patterns;
                Debug.Log($"ボーン命名パターンを読み込みました: {_boneNamingPatterns.Count}件");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ボーン命名パターンの読み込みに失敗しました: {e.Message}");
                _boneNamingPatterns = new List<BoneNamingPattern>();
            }
        }

        /// <summary>
        /// 身体部位参照データを読み込み
        /// </summary>
        private void LoadBodyPartReferenceData()
        {
            try
            {
                _bodyPartReferenceData = JsonUtils.LoadFromResources<BodyPartReferenceData>("BodyPartReferences");
                Debug.Log("身体部位参照データを読み込みました");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"身体部位参照データの読み込みに失敗しました: {e.Message}");
                _bodyPartReferenceData = new BodyPartReferenceData();
            }
        }

        /// <summary>
        /// 保存済みのマッピングデータを読み込み
        /// </summary>
        private void LoadSavedMappings()
        {
            _savedMappings = new Dictionary<string, MappingData>();
            string mappingsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, MAPPINGS_DIRECTORY);
            
            if (Directory.Exists(mappingsPath))
            {
                string[] files = Directory.GetFiles(mappingsPath, "*.json");
                foreach (string file in files)
                {
                    try
                    {
                        MappingData mappingData = JsonUtils.LoadFromJson<MappingData>(file);
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        _savedMappings[fileName] = mappingData;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"マッピングデータの読み込みに失敗しました ({file}): {e.Message}");
                    }
                }
            }
            
            Debug.Log($"保存済みマッピングデータを読み込みました: {_savedMappings.Count}件");
        }

        /// <summary>
        /// 保存済みの調整設定を読み込み
        /// </summary>
        private void LoadSavedAdjustmentSettings()
        {
            _savedAdjustmentSettings = new Dictionary<string, AdjustmentSettings>();
            string presetsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, PRESETS_DIRECTORY);
            
            if (Directory.Exists(presetsPath))
            {
                string[] files = Directory.GetFiles(presetsPath, "*.json");
                foreach (string file in files)
                {
                    try
                    {
                        AdjustmentSettings settings = JsonUtils.LoadFromJson<AdjustmentSettings>(file);
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        _savedAdjustmentSettings[fileName] = settings;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"調整設定の読み込みに失敗しました ({file}): {e.Message}");
                    }
                }
            }
            
            Debug.Log($"保存済み調整設定を読み込みました: {_savedAdjustmentSettings.Count}件");
        }

        /// <summary>
        /// マッピングデータを保存
        /// </summary>
        public void SaveMapping(string name, MappingData mappingData)
        {
            string mappingsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, MAPPINGS_DIRECTORY);
            string filePath = Path.Combine(mappingsPath, $"{name}.json");
            
            JsonUtils.SaveToJson(filePath, mappingData);
            _savedMappings[name] = mappingData;
            
            Debug.Log($"マッピングデータを保存しました: {name}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 調整設定を保存
        /// </summary>
        public void SaveAdjustmentSettings(string name, AdjustmentSettings settings)
        {
            string presetsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, PRESETS_DIRECTORY);
            string filePath = Path.Combine(presetsPath, $"{name}.json");
            
            JsonUtils.SaveToJson(filePath, settings);
            _savedAdjustmentSettings[name] = settings;
            
            Debug.Log($"調整設定を保存しました: {name}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// マッピングデータを削除
        /// </summary>
        public bool DeleteMapping(string name)
        {
            string mappingsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, MAPPINGS_DIRECTORY);
            string filePath = Path.Combine(mappingsPath, $"{name}.json");
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _savedMappings.Remove(name);
                Debug.Log($"マッピングデータを削除しました: {name}");
                AssetDatabase.Refresh();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 調整設定を削除
        /// </summary>
        public bool DeleteAdjustmentSettings(string name)
        {
            string presetsPath = Path.Combine(Application.dataPath, DEFAULT_SAVE_DIRECTORY, PRESETS_DIRECTORY);
            string filePath = Path.Combine(presetsPath, $"{name}.json");
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _savedAdjustmentSettings.Remove(name);
                Debug.Log($"調整設定を削除しました: {name}");
                AssetDatabase.Refresh();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// マッピングデータのリストを取得
        /// </summary>
        public List<string> GetMappingNameList()
        {
            return _savedMappings.Keys.ToList();
        }

        /// <summary>
        /// 調整設定のリストを取得
        /// </summary>
        public List<string> GetAdjustmentSettingsNameList()
        {
            return _savedAdjustmentSettings.Keys.ToList();
        }

        /// <summary>
        /// データのリロード
        /// </summary>
        public void ReloadData()
        {
            _initialized = false;
            Initialize();
        }
    }

    /// <summary>
    /// ボーン命名パターンのリストコンテナ（JSONから読み込むため）
    /// </summary>
    [System.Serializable]
    public class BoneNamingPatternList
    {
        public List<BoneNamingPattern> Patterns;
    }

    /// <summary>
    /// ボーン命名パターン情報
    /// </summary>
    [System.Serializable]
    public class BoneNamingPattern
    {
        public string Type;              // ボーンの種類（例: Head, Neck, Spine）
        public List<string> Patterns;    // 名前のパターンリスト
        public List<string> Prefixes;    // 左右などの接頭辞パターン
        public List<string> Suffixes;    // 左右などの接尾辞パターン
        public bool IsPaired;            // 左右のペアがあるか（手、足など）
    }

    /// <summary>
    /// 身体部位参照データ
    /// </summary>
    [System.Serializable]
    public class BodyPartReferenceData
    {
        public List<BodyPartReference> BodyParts = new List<BodyPartReference>();
    }

    /// <summary>
    /// 身体部位参照データクラス
    /// </summary>
    [System.Serializable]
    public class BodyPartReference
    {
        public string Name;                      // 部位名（例: Head, Chest, LeftArm）
        public string[] RelatedBones;            // 関連するボーン名
        public Vector3 DefaultPosition;          // 標準的な相対位置
        public Vector3 DefaultSize;              // 標準的なサイズ
        public float DefaultRotation;            // 標準的な回転
        public List<Vector3> ReferencePoints;    // 参照点のリスト
    }
}
