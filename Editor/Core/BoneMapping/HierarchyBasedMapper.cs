using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 階層ベースのボーンマッピングを行うクラス
    /// </summary>
    public static class HierarchyBasedMapper
    {
        /// <summary>
        /// 階層ベースのマッピングを実行
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
                
                var result = FindBestHierarchyBasedMatch(avatarBone, availableCostumeBones, avatarBones, costumeBones, mappingData);
                
                if (result.targetBone != null && result.confidence > 0)
                {
                    mappingData.AddOrUpdateMapping(
                        avatarBone.id, 
                        result.targetBone.id, 
                        result.confidence, 
                        MappingMethod.HierarchyBased
                    );
                    
                    mappedCount++;
                    
                    // マッピングされたボーンを利用可能リストから除外
                    availableCostumeBones.Remove(result.targetBone);
                }
            }
            
            return mappedCount;
        }
        
        /// <summary>
        /// 指定されたボーンに最も一致する階層ベースのマッチングを見つける
        /// </summary>
        private static (BoneData targetBone, float confidence) FindBestHierarchyBasedMatch(
            BoneData sourceBone, 
            List<BoneData> targetBones, 
            List<BoneData> allSourceBones, 
            List<BoneData> allTargetBones,
            MappingData mappingData)
        {
            if (sourceBone == null || targetBones == null || targetBones.Count == 0)
                return (null, 0f);
            
            BoneData bestMatch = null;
            float bestConfidence = 0f;
            
            foreach (var targetBone in targetBones)
            {
                float confidence = CalculateHierarchicalSimilarity(sourceBone, targetBone, allSourceBones, allTargetBones, mappingData);
                
                // より良い一致が見つかった場合は更新
                if (confidence > bestConfidence)
                {
                    bestMatch = targetBone;
                    bestConfidence = confidence;
                }
                
                // 非常に高い信頼度が見つかった場合は即時返却
                if (bestConfidence >= 0.9f)
                {
                    break;
                }
            }
            
            return (bestMatch, bestConfidence);
        }
        
        /// <summary>
        /// 2つのボーンの階層的類似度を計算 (0.0～1.0)
        /// </summary>
        private static float CalculateHierarchicalSimilarity(
            BoneData sourceBone, 
            BoneData targetBone, 
            List<BoneData> allSourceBones, 
            List<BoneData> allTargetBones,
            MappingData mappingData)
        {
            float similarity = 0.0f;
            
            // 体の部位が一致する場合はベースの類似度を設定
            if (sourceBone.bodyPart != BodyPart.Other && 
                targetBone.bodyPart != BodyPart.Other && 
                sourceBone.bodyPart == targetBone.bodyPart)
            {
                similarity = 0.3f; // ベースの類似度
            }
            
            // 親の関係を確認
            similarity += CheckParentSimilarity(sourceBone, targetBone, allSourceBones, allTargetBones, mappingData) * 0.4f;
            
            // 子の関係を確認
            similarity += CheckChildrenSimilarity(sourceBone, targetBone, allSourceBones, allTargetBones, mappingData) * 0.3f;
            
            // 兄弟の関係を確認
            similarity += CheckSiblingSimilarity(sourceBone, targetBone, allSourceBones, allTargetBones, mappingData) * 0.2f;
            
            return Mathf.Clamp01(similarity);
        }
        
        /// <summary>
        /// 親ボーンの類似度をチェック
        /// </summary>
        private static float CheckParentSimilarity(
            BoneData sourceBone, 
            BoneData targetBone, 
            List<BoneData> allSourceBones, 
            List<BoneData> allTargetBones,
            MappingData mappingData)
        {
            if (string.IsNullOrEmpty(sourceBone.parentId) || string.IsNullOrEmpty(targetBone.parentId))
            {
                // 両方ともルートボーンであれば高い類似度
                if (string.IsNullOrEmpty(sourceBone.parentId) && string.IsNullOrEmpty(targetBone.parentId))
                {
                    return 0.8f;
                }
                return 0.0f;
            }
            
            // 親ボーンを取得
            var sourceParent = allSourceBones.FirstOrDefault(b => b.id == sourceBone.parentId);
            var targetParent = allTargetBones.FirstOrDefault(b => b.id == targetBone.parentId);
            
            if (sourceParent == null || targetParent == null)
                return 0.0f;
            
            // 親ボーンのマッピング関係をチェック
            BoneData mappedCostumeBone = null;
            float confidence = 0.0f;
            MappingMethod method = MappingMethod.NotMapped;
            bool isManual = false;
            
            bool hasMapping = mappingData.GetCostumeBoneForAvatarBone(
                sourceParent.id, out mappedCostumeBone, out confidence, out method, out isManual);
            
            // 親ボーンがマッピングされていて、マッピング先が一致する場合は高い類似度
            if (hasMapping && mappedCostumeBone != null && mappedCostumeBone.id == targetParent.id)
            {
                return 0.9f;
            }
            
            // 親ボーンの体の部位が一致する場合
            if (sourceParent.bodyPart != BodyPart.Other && 
                targetParent.bodyPart != BodyPart.Other && 
                sourceParent.bodyPart == targetParent.bodyPart)
            {
                return 0.6f;
            }
            
            return 0.1f;
        }
        
        /// <summary>
        /// 子ボーンの類似度をチェック
        /// </summary>
        private static float CheckChildrenSimilarity(
            BoneData sourceBone, 
            BoneData targetBone, 
            List<BoneData> allSourceBones, 
            List<BoneData> allTargetBones,
            MappingData mappingData)
        {
            // 子ボーンがない場合
            if (sourceBone.childrenIds.Count == 0 && targetBone.childrenIds.Count == 0)
            {
                return 0.8f; // 両方とも末端ボーン
            }
            
            if (sourceBone.childrenIds.Count == 0 || targetBone.childrenIds.Count == 0)
            {
                return 0.0f; // 片方だけ末端ボーン
            }
            
            // 子ボーンの数が大きく異なる場合
            if (Mathf.Abs(sourceBone.childrenIds.Count - targetBone.childrenIds.Count) > 2)
            {
                return 0.1f;
            }
            
            // 子ボーンのリストを取得
            var sourceChildren = sourceBone.childrenIds
                .Select(id => allSourceBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null)
                .ToList();
                
            var targetChildren = targetBone.childrenIds
                .Select(id => allTargetBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null)
                .ToList();
            
            // 子ボーンの体の部位を比較
            int matchingBodyParts = 0;
            
            foreach (var sourceChild in sourceChildren)
            {
                if (sourceChild.bodyPart == BodyPart.Other)
                    continue;
                    
                bool hasMatch = targetChildren.Any(c => c.bodyPart == sourceChild.bodyPart);
                
                if (hasMatch)
                {
                    matchingBodyParts++;
                }
            }
            
            float matchRatio = (float)matchingBodyParts / Mathf.Max(sourceChildren.Count, 1);
            
            return Mathf.Clamp01(matchRatio);
        }
        
        /// <summary>
        /// 兄弟ボーンの類似度をチェック
        /// </summary>
        private static float CheckSiblingSimilarity(
            BoneData sourceBone, 
            BoneData targetBone, 
            List<BoneData> allSourceBones, 
            List<BoneData> allTargetBones,
            MappingData mappingData)
        {
            // 親がない場合はルートボーンなので兄弟関係は比較しない
            if (string.IsNullOrEmpty(sourceBone.parentId) || string.IsNullOrEmpty(targetBone.parentId))
            {
                return 0.0f;
            }
            
            // 親ボーンを取得
            var sourceParent = allSourceBones.FirstOrDefault(b => b.id == sourceBone.parentId);
            var targetParent = allTargetBones.FirstOrDefault(b => b.id == targetBone.parentId);
            
            if (sourceParent == null || targetParent == null)
                return 0.0f;
            
            // 兄弟ボーンのリストを取得
            var sourceSiblings = sourceParent.childrenIds
                .Where(id => id != sourceBone.id) // 自分自身を除く
                .Select(id => allSourceBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null)
                .ToList();
                
            var targetSiblings = targetParent.childrenIds
                .Where(id => id != targetBone.id) // 自分自身を除く
                .Select(id => allTargetBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null)
                .ToList();
            
            // 兄弟の数が大きく異なる場合
            if (Mathf.Abs(sourceSiblings.Count - targetSiblings.Count) > 2)
            {
                return 0.2f;
            }
            
            // 兄弟ボーンのマッピング関係を確認
            int matchingSiblings = 0;
            
            foreach (var sourceSibling in sourceSiblings)
            {
                BoneData mappedCostumeBone = null;
                float confidence = 0.0f;
                MappingMethod method = MappingMethod.NotMapped;
                bool isManual = false;
                
                bool hasMapping = mappingData.GetCostumeBoneForAvatarBone(
                    sourceSibling.id, out mappedCostumeBone, out confidence, out method, out isManual);
                    
                if (hasMapping && mappedCostumeBone != null)
                {
                    // マッピング先の兄弟ボーンが正しいか確認
                    bool isCorrectSiblingMapping = targetSiblings.Any(s => s.id == mappedCostumeBone.id);
                    
                    if (isCorrectSiblingMapping)
                    {
                        matchingSiblings++;
                    }
                }
            }
            
            // マッチした兄弟の割合
            float matchRatio = sourceSiblings.Count > 0 ? 
                (float)matchingSiblings / sourceSiblings.Count : 0.0f;
                
            return Mathf.Clamp01(matchRatio);
        }
    }
}
