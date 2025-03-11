using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// マッピング方法を表す列挙型
    /// </summary>
    public enum MappingMethod
    {
        NotMapped,      // マッピングなし
        NameBased,      // 名前ベースのマッピング
        HierarchyBased, // 階層ベースのマッピング
        PositionBased,  // 位置ベースのマッピング
        Manual          // 手動でのマッピング
    }
    
    /// <summary>
    /// ボーンマッピング情報を表すクラス
    /// </summary>
    [Serializable]
    public class BoneMapping
    {
        public string avatarBoneId;        // アバターボーンのID
        public string costumeBoneId;       // 衣装ボーンのID
        public float confidence;           // マッピングの信頼度 (0.0～1.0)
        public MappingMethod method;       // マッピング方法
        public bool isManuallyMapped;      // 手動でマッピングされたかどうか
        
        // パスプロパティの追加（PreviewManager用）
        public string AvatarBonePath { get; set; }
        public string CostumeBonePath { get; set; }
        
        // 保存用のコンストラクタ
        public BoneMapping() { }
        
        // マッピング作成用のコンストラクタ
        public BoneMapping(string avatarBoneId, string costumeBoneId, float confidence, MappingMethod method, bool isManual = false)
        {
            this.avatarBoneId = avatarBoneId;
            this.costumeBoneId = costumeBoneId;
            this.confidence = Mathf.Clamp01(confidence);
            this.method = method;
            this.isManuallyMapped = isManual;
        }
    }
    
    /// <summary>
    /// マッピング情報を管理するクラス
    /// </summary>
    [Serializable]
    public class MappingData
    {
        // マッピングリスト
        private List<BoneMapping> mappings = new List<BoneMapping>();
        
        // PreviewManager用のプロパティ
        public List<BoneMapping> BoneMappings { get { return mappings; } }
        
        // 除外ボーンリスト
        private HashSet<string> excludedAvatarBoneIds = new HashSet<string>();
        private HashSet<string> excludedCostumeBoneIds = new HashSet<string>();
        
        // ボーン参照
        [NonSerialized]
        private Dictionary<string, BoneData> avatarBones = new Dictionary<string, BoneData>();
        [NonSerialized]
        private Dictionary<string, BoneData> costumeBones = new Dictionary<string, BoneData>();
        
        /// <summary>
        /// デフォルトコンストラクタ（シリアライズ用）
        /// </summary>
        public MappingData() { }
        
        /// <summary>
        /// ボーンリストを指定してマッピングデータを作成
        /// </summary>
        public MappingData(List<BoneData> avatarBones, List<BoneData> costumeBones)
        {
            UpdateBoneReferences(avatarBones, costumeBones);
        }
        
        /// <summary>
        /// ボーン参照を更新
        /// </summary>
        public void UpdateBoneReferences(List<BoneData> avatarBones, List<BoneData> costumeBones)
        {
            this.avatarBones.Clear();
            this.costumeBones.Clear();
            
            foreach (var bone in avatarBones)
            {
                this.avatarBones[bone.id] = bone;
            }
            
            foreach (var bone in costumeBones)
            {
                this.costumeBones[bone.id] = bone;
            }
            
            // 無効なマッピングを削除
            CleanupInvalidMappings();
        }
        
        /// <summary>
        /// 無効なマッピングを削除
        /// </summary>
        private void CleanupInvalidMappings()
        {
            mappings.RemoveAll(m => 
                !avatarBones.ContainsKey(m.avatarBoneId) || 
                !costumeBones.ContainsKey(m.costumeBoneId));
            
            // 無効な除外ボーンIDも削除
            excludedAvatarBoneIds.RemoveWhere(id => !avatarBones.ContainsKey(id));
            excludedCostumeBoneIds.RemoveWhere(id => !costumeBones.ContainsKey(id));
        }
        
        #region マッピング管理
        
        /// <summary>
        /// マッピングを追加または更新
        /// </summary>
        public void AddOrUpdateMapping(string avatarBoneId, string costumeBoneId, float confidence, MappingMethod method)
        {
            // 除外リストに入っているかチェック
            if (excludedAvatarBoneIds.Contains(avatarBoneId) || excludedCostumeBoneIds.Contains(costumeBoneId))
            {
                return;
            }
            
            // 既存のマッピングを探す
            int existingIndex = mappings.FindIndex(m => m.avatarBoneId == avatarBoneId);
            
            if (existingIndex >= 0)
            {
                BoneMapping existing = mappings[existingIndex];
                
                // 手動マッピングは自動マッピングで上書きしない
                if (existing.isManuallyMapped && !method.Equals(MappingMethod.Manual))
                {
                    return;
                }
                
                // 信頼度が高い場合のみ更新
                if (confidence > existing.confidence || method.Equals(MappingMethod.Manual))
                {
                    existing.costumeBoneId = costumeBoneId;
                    existing.confidence = confidence;
                    existing.method = method;
                    existing.isManuallyMapped = method.Equals(MappingMethod.Manual);
                }
            }
            else
            {
                // 新しいマッピングを追加
                mappings.Add(new BoneMapping(avatarBoneId, costumeBoneId, confidence, method));
            }
            
            // 他のアバターボーンから同じ衣装ボーンへのマッピングを削除（1対1関係を保証）
            // ただし、手動マッピングの場合は例外
            if (method.Equals(MappingMethod.Manual))
            {
                int conflictIndex = mappings.FindIndex(m => 
                    m.costumeBoneId == costumeBoneId && 
                    m.avatarBoneId != avatarBoneId);
                
                if (conflictIndex >= 0)
                {
                    mappings.RemoveAt(conflictIndex);
                }
            }
        }
        
        /// <summary>
        /// 手動マッピングを設定
        /// </summary>
        public void SetManualMapping(string avatarBoneId, string costumeBoneId)
        {
            AddOrUpdateMapping(avatarBoneId, costumeBoneId, 1.0f, MappingMethod.Manual);
        }
        
        /// <summary>
        /// マッピングを削除
        /// </summary>
        public void RemoveMapping(string avatarBoneId)
        {
            mappings.RemoveAll(m => m.avatarBoneId == avatarBoneId);
        }
        
        /// <summary>
        /// 衣装ボーンIDによるマッピング削除
        /// </summary>
        public void RemoveMappingByCostumeBone(string costumeBoneId)
        {
            mappings.RemoveAll(m => m.costumeBoneId == costumeBoneId);
        }
        
        /// <summary>
        /// すべてのマッピングをクリア
        /// </summary>
        public void ClearAllMappings()
        {
            mappings.Clear();
            excludedAvatarBoneIds.Clear();
            excludedCostumeBoneIds.Clear();
        }
        
        /// <summary>
        /// アバターボーンを除外リストに追加
        /// </summary>
        public void ExcludeAvatarBone(string avatarBoneId)
        {
            excludedAvatarBoneIds.Add(avatarBoneId);
            RemoveMapping(avatarBoneId);
        }
        
        /// <summary>
        /// 衣装ボーンを除外リストに追加
        /// </summary>
        public void ExcludeCostumeBone(string costumeBoneId)
        {
            excludedCostumeBoneIds.Add(costumeBoneId);
            RemoveMappingByCostumeBone(costumeBoneId);
        }
        
        /// <summary>
        /// アバターボーンの除外を解除
        /// </summary>
        public void IncludeAvatarBone(string avatarBoneId)
        {
            excludedAvatarBoneIds.Remove(avatarBoneId);
        }
        
        /// <summary>
        /// 衣装ボーンの除外を解除
        /// </summary>
        public void IncludeCostumeBone(string costumeBoneId)
        {
            excludedCostumeBoneIds.Remove(costumeBoneId);
        }
        
        /// <summary>
        /// アバターボーンが除外されているかを確認
        /// </summary>
        public bool IsAvatarBoneExcluded(string avatarBoneId)
        {
            return excludedAvatarBoneIds.Contains(avatarBoneId);
        }
        
        /// <summary>
        /// 衣装ボーンが除外されているかを確認
        /// </summary>
        public bool IsCostumeBoneExcluded(string costumeBoneId)
        {
            return excludedCostumeBoneIds.Contains(costumeBoneId);
        }
        
        #endregion
        
        #region マッピング取得
        
        /// <summary>
        /// アバターボーンIDに対応する衣装ボーンを取得
        /// </summary>
        public bool GetCostumeBoneForAvatarBone(string avatarBoneId, out BoneData costumeBone, out float confidence, out MappingMethod method, out bool isManuallyMapped)
        {
            var mapping = mappings.FirstOrDefault(m => m.avatarBoneId == avatarBoneId);
            
            if (mapping != null && costumeBones.ContainsKey(mapping.costumeBoneId))
            {
                costumeBone = costumeBones[mapping.costumeBoneId];
                confidence = mapping.confidence;
                method = mapping.method;
                isManuallyMapped = mapping.isManuallyMapped;
                return true;
            }
            
            costumeBone = null;
            confidence = 0f;
            method = MappingMethod.NotMapped;
            isManuallyMapped = false;
            return false;
        }
        
        /// <summary>
        /// 衣装ボーンIDに対応するアバターボーンを取得
        /// </summary>
        public bool GetAvatarBoneForCostumeBone(string costumeBoneId, out BoneData avatarBone, out float confidence, out MappingMethod method, out bool isManuallyMapped)
        {
            var mapping = mappings.FirstOrDefault(m => m.costumeBoneId == costumeBoneId);
            
            if (mapping != null && avatarBones.ContainsKey(mapping.avatarBoneId))
            {
                avatarBone = avatarBones[mapping.avatarBoneId];
                confidence = mapping.confidence;
                method = mapping.method;
                isManuallyMapped = mapping.isManuallyMapped;
                return true;
            }
            
            avatarBone = null;
            confidence = 0f;
            method = MappingMethod.NotMapped;
            isManuallyMapped = false;
            return false;
        }
        
        /// <summary>
        /// マッピングされたボーンの数を取得
        /// </summary>
        public int GetMappedBoneCount()
        {
            return mappings.Count;
        }
        
        /// <summary>
        /// 未マッピングのアバターボーンのIDリストを取得
        /// </summary>
        public List<string> GetUnmappedAvatarBoneIds()
        {
            var mappedIds = new HashSet<string>(mappings.Select(m => m.avatarBoneId));
            return avatarBones.Keys.Where(id => !mappedIds.Contains(id) && !excludedAvatarBoneIds.Contains(id)).ToList();
        }
        
        /// <summary>
        /// 未マッピングの衣装ボーンのIDリストを取得
        /// </summary>
        public List<string> GetUnmappedCostumeBoneIds()
        {
            var mappedIds = new HashSet<string>(mappings.Select(m => m.costumeBoneId));
            return costumeBones.Keys.Where(id => !mappedIds.Contains(id) && !excludedCostumeBoneIds.Contains(id)).ToList();
        }
        
        /// <summary>
        /// 体の部位ごとのマッピング状態を取得
        /// </summary>
        public Dictionary<BodyPart, float> GetBodyPartMappingStatus()
        {
            var result = new Dictionary<BodyPart, float>();
            var bodyPartCounts = new Dictionary<BodyPart, int>();
            var bodyPartMappedCounts = new Dictionary<BodyPart, int>();
            
            // 体の部位ごとのボーン数をカウント
            foreach (var bone in avatarBones.Values)
            {
                if (!bodyPartCounts.ContainsKey(bone.bodyPart))
                {
                    bodyPartCounts[bone.bodyPart] = 0;
                    bodyPartMappedCounts[bone.bodyPart] = 0;
                }
                
                bodyPartCounts[bone.bodyPart]++;
                
                // マッピングされているかチェック
                if (mappings.Any(m => m.avatarBoneId == bone.id))
                {
                    bodyPartMappedCounts[bone.bodyPart]++;
                }
            }
            
            // 体の部位ごとのマッピング率を計算
            foreach (var part in bodyPartCounts.Keys)
            {
                if (bodyPartCounts[part] > 0)
                {
                    result[part] = (float)bodyPartMappedCounts[part] / bodyPartCounts[part];
                }
                else
                {
                    result[part] = 0f;
                }
            }
            
            return result;
        }
        
        #endregion
        
        #region ファイル操作
        
        /// <summary>
        /// マッピングデータをJSONファイルに保存
        /// </summary>
        public void SaveToFile(string filePath)
        {
            // 保存用データの作成
            var saveData = new MappingDataSaveFormat
            {
                mappings = this.mappings,
                excludedAvatarBoneIds = this.excludedAvatarBoneIds.ToList(),
                excludedCostumeBoneIds = this.excludedCostumeBoneIds.ToList()
            };
            
            // JSONに変換して保存
            string jsonData = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(filePath, jsonData);
        }
        
        /// <summary>
        /// マッピングデータをJSONファイルから読み込み
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("マッピングデータファイルが見つかりません。", filePath);
            }
            
            string jsonData = File.ReadAllText(filePath);
            var loadedData = JsonUtility.FromJson<MappingDataSaveFormat>(jsonData);
            
            if (loadedData == null)
            {
                throw new Exception("マッピングデータの読み込みに失敗しました。");
            }
            
            // データの適用
            this.mappings = loadedData.mappings;
            this.excludedAvatarBoneIds = new HashSet<string>(loadedData.excludedAvatarBoneIds);
            this.excludedCostumeBoneIds = new HashSet<string>(loadedData.excludedCostumeBoneIds);
            
            // 無効なマッピングの削除
            CleanupInvalidMappings();
        }
        
        #endregion
    }
    
    /// <summary>
    /// マッピングデータの保存用フォーマット
    /// </summary>
    [Serializable]
    public class MappingDataSaveFormat
    {
        public List<BoneMapping> mappings = new List<BoneMapping>();
        public List<string> excludedAvatarBoneIds = new List<string>();
        public List<string> excludedCostumeBoneIds = new List<string>();
    }
}
