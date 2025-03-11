using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 名前ベースのボーンマッピングを行うクラス
    /// </summary>
    public static class NameBasedMapper
    {
        /// <summary>
        /// 名前ベースのマッピングを実行
        /// </summary>
        /// <returns>マッピングされたボーンの数</returns>
        public static int PerformMapping(MappingData mappingData, List<BoneData> avatarBones, List<BoneData> costumeBones)
        {
            if (mappingData == null || avatarBones == null || costumeBones == null)
                return 0;
                
            // 未マッピングのアバターボーンのみを対象にする
            var unmappedAvatarBones = mappingData.GetUnmappedAvatarBoneIds()
                .Select(id => avatarBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null)
                .ToList();
                
            // 既にマッピングされた衣装ボーンは除外
            var mappedCostumeBoneIds = new HashSet<string>();
            foreach (var avatarBone in avatarBones)
            {
                BoneData costumeBone;
                float confidence;
                MappingMethod method;
                bool isManual;
                
                if (mappingData.GetCostumeBoneForAvatarBone(avatarBone.id, out costumeBone, out confidence, out method, out isManual))
                {
                    mappedCostumeBoneIds.Add(costumeBone.id);
                }
            }
            
            // 未マッピングの衣装ボーン
            var availableCostumeBones = costumeBones
                .Where(b => !mappedCostumeBoneIds.Contains(b.id) && !mappingData.IsCostumeBoneExcluded(b.id))
                .ToList();
            
            int mappedCount = 0;
            
            // マッピング実行
            foreach (var avatarBone in unmappedAvatarBones)
            {
                // 除外リストにあるボーンはスキップ
                if (mappingData.IsAvatarBoneExcluded(avatarBone.id))
                    continue;
                
                var result = FindBestNameBasedMatch(avatarBone, availableCostumeBones);
                
                if (result.targetBone != null && result.confidence > 0)
                {
                    mappingData.AddOrUpdateMapping(
                        avatarBone.id, 
                        result.targetBone.id, 
                        result.confidence, 
                        MappingMethod.NameBased
                    );
                    
                    mappedCount++;
                    
                    // マッピングされたボーンを利用可能リストから除外
                    availableCostumeBones.Remove(result.targetBone);
                }
            }
            
            return mappedCount;
        }
        
        /// <summary>
        /// 指定されたボーンに最も一致する名前ベースのマッチングを見つける
        /// </summary>
        private static (BoneData targetBone, float confidence) FindBestNameBasedMatch(BoneData sourceBone, List<BoneData> targetBones)
        {
            if (sourceBone == null || targetBones == null || targetBones.Count == 0)
                return (null, 0f);
            
            BoneData bestMatch = null;
            float bestConfidence = 0f;
            
            string sourceName = NormalizeBoneName(sourceBone.name);
            
            foreach (var targetBone in targetBones)
            {
                string targetName = NormalizeBoneName(targetBone.name);
                float confidence = CalculateNameSimilarity(sourceName, targetName);
                
                // ヒューマノイドボーンの場合は追加のチェック
                if (sourceBone.isHumanoid && targetBone.isHumanoid)
                {
                    // 同じヒューマノイドボーンの場合は高い信頼度
                    if (sourceBone.humanoidName == targetBone.humanoidName)
                    {
                        confidence = Mathf.Max(confidence, 0.95f);
                    }
                }
                
                // 体の部位が一致する場合は信頼度を上げる
                if (sourceBone.bodyPart != BodyPart.Other && 
                    targetBone.bodyPart != BodyPart.Other && 
                    sourceBone.bodyPart == targetBone.bodyPart)
                {
                    confidence = Mathf.Min(1.0f, confidence + 0.2f);
                }
                
                // より良い一致が見つかった場合は更新
                if (confidence > bestConfidence)
                {
                    bestMatch = targetBone;
                    bestConfidence = confidence;
                }
                
                // 完全一致が見つかった場合は即時返却
                if (bestConfidence >= 0.999f)
                {
                    break;
                }
            }
            
            return (bestMatch, bestConfidence);
        }
        
        /// <summary>
        /// ボーン名を正規化
        /// </summary>
        private static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            
            // 小文字に変換
            string result = name.ToLower();
            
            // 数字を削除
            result = Regex.Replace(result, @"\d+", "");
            
            // アンダースコア、ドット、ハイフンを削除
            result = result.Replace("_", "").Replace(".", "").Replace("-", "");
            
            // 左右の識別子を標準化
            result = ReplaceLeftRightIdentifiers(result);
            
            // "bone"という単語を削除
            result = result.Replace("bone", "");
            
            return result;
        }
        
        /// <summary>
        /// 左右の識別子を標準化
        /// </summary>
        private static string ReplaceLeftRightIdentifiers(string name)
        {
            // 左側の識別子
            name = Regex.Replace(name, @"^l\.", "left");
            name = Regex.Replace(name, @"^l_", "left");
            name = Regex.Replace(name, @"\.l$", "left");
            name = Regex.Replace(name, @"_l$", "left");
            
            // 右側の識別子
            name = Regex.Replace(name, @"^r\.", "right");
            name = Regex.Replace(name, @"^r_", "right");
            name = Regex.Replace(name, @"\.r$", "right");
            name = Regex.Replace(name, @"_r$", "right");
            
            return name;
        }
        
        /// <summary>
        /// 2つの名前の類似度を計算 (0.0～1.0)
        /// </summary>
        private static float CalculateNameSimilarity(string name1, string name2)
        {
            if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
                return 0f;
            
            // 完全一致
            if (name1 == name2)
                return 1.0f;
            
            // 文字列長の差が大きすぎる場合
            int maxLength = Mathf.Max(name1.Length, name2.Length);
            int minLength = Mathf.Min(name1.Length, name2.Length);
            
            if (minLength < maxLength * 0.5f)
                return 0.1f;
            
            // Levenshtein距離（編集距離）の計算
            int distance = LevenshteinDistance(name1, name2);
            
            // 距離から類似度を計算 (0.0～1.0)
            float similarity = 1.0f - (float)distance / maxLength;
            
            return similarity;
        }
        
        /// <summary>
        /// Levenshtein距離（編集距離）を計算
        /// </summary>
        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];
            
            // 初期化
            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;
                
            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;
                
            // 距離計算
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    
                    matrix[i, j] = Mathf.Min(
                        matrix[i - 1, j] + 1,      // 削除
                        matrix[i, j - 1] + 1,      // 挿入
                        matrix[i - 1, j - 1] + cost // 置換
                    );
                }
            }
            
            return matrix[s1.Length, s2.Length];
        }
    }
}
