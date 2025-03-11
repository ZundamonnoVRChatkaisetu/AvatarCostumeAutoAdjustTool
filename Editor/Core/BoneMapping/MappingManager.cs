using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ボーンマッピングの全体管理を行うクラス
    /// </summary>
    public static class MappingManager
    {
        /// <summary>
        /// 完全自動マッピングを実行（全方式を組み合わせて最適化）
        /// </summary>
        /// <returns>マッピングされたボーンの数</returns>
        public static int PerformFullMapping(
            MappingData mappingData, 
            List<BoneData> avatarBones, 
            List<BoneData> costumeBones,
            float confidenceThreshold = 0.5f)
        {
            if (mappingData == null || avatarBones == null || costumeBones == null)
                return 0;
            
            int totalMappedCount = 0;
            
            // 1. 名前ベースのマッピング（最も信頼性が高い）
            int nameMappedCount = NameBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            totalMappedCount += nameMappedCount;
            
            Debug.Log($"名前ベースマッピング: {nameMappedCount} ボーンをマッピングしました。");
            
            // 2. 階層ベースのマッピング（名前ベースでマッピングできなかったものに適用）
            int hierarchyMappedCount = HierarchyBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            totalMappedCount += hierarchyMappedCount;
            
            Debug.Log($"階層ベースマッピング: {hierarchyMappedCount} ボーンをマッピングしました。");
            
            // 3. 位置ベースのマッピング（最後の手段）
            int positionMappedCount = PositionBasedMapper.PerformMapping(mappingData, avatarBones, costumeBones);
            totalMappedCount += positionMappedCount;
            
            Debug.Log($"位置ベースマッピング: {positionMappedCount} ボーンをマッピングしました。");
            
            // 4. 信頼度が低いマッピングを除外
            int removedCount = RemoveLowConfidenceMappings(mappingData, avatarBones, confidenceThreshold);
            
            if (removedCount > 0)
            {
                Debug.Log($"信頼度が低いマッピング {removedCount} 件を除外しました（しきい値: {confidenceThreshold}）。");
                totalMappedCount -= removedCount;
            }
            
            // 5. 重要なボーンに対してフォールバックマッピングを適用
            int fallbackCount = ApplyFallbackMappings(mappingData, avatarBones, costumeBones);
            
            if (fallbackCount > 0)
            {
                Debug.Log($"フォールバックマッピング: {fallbackCount} ボーンに適用しました。");
                totalMappedCount += fallbackCount;
            }
            
            return totalMappedCount;
        }
        
        /// <summary>
        /// 信頼度が低いマッピングを削除
        /// </summary>
        private static int RemoveLowConfidenceMappings(
            MappingData mappingData, 
            List<BoneData> avatarBones, 
            float confidenceThreshold)
        {
            int removedCount = 0;
            List<string> bonesWithLowConfidence = new List<string>();
            
            // 信頼度が低いボーンを特定
            foreach (var bone in avatarBones)
            {
                BoneData costumeBone;
                float confidence;
                MappingMethod method;
                bool isManual;
                
                bool hasMapping = mappingData.GetCostumeBoneForAvatarBone(
                    bone.id, out costumeBone, out confidence, out method, out isManual);
                
                // 手動マッピングは常に保持
                if (hasMapping && !isManual && confidence < confidenceThreshold)
                {
                    bonesWithLowConfidence.Add(bone.id);
                }
            }
            
            // マッピングを削除
            foreach (var boneId in bonesWithLowConfidence)
            {
                mappingData.RemoveMapping(boneId);
                removedCount++;
            }
            
            return removedCount;
        }
        
        /// <summary>
        /// 重要なボーンに対してフォールバックマッピングを適用
        /// </summary>
        private static int ApplyFallbackMappings(
            MappingData mappingData, 
            List<BoneData> avatarBones, 
            List<BoneData> costumeBones)
        {
            int fallbackCount = 0;
            
            // 重要なボーンリスト（体の主要部位）
            List<BodyPart> essentialBodyParts = new List<BodyPart>
            {
                BodyPart.Head,
                BodyPart.Neck,
                BodyPart.Chest,
                BodyPart.Spine,
                BodyPart.Hips,
                BodyPart.LeftShoulder,
                BodyPart.LeftUpperArm,
                BodyPart.LeftLowerArm,
                BodyPart.LeftHand,
                BodyPart.RightShoulder,
                BodyPart.RightUpperArm,
                BodyPart.RightLowerArm,
                BodyPart.RightHand,
                BodyPart.LeftUpperLeg,
                BodyPart.LeftLowerLeg,
                BodyPart.LeftFoot,
                BodyPart.RightUpperLeg,
                BodyPart.RightLowerLeg,
                BodyPart.RightFoot
            };
            
            // 重要なアバターボーンで未マッピングのものを特定
            var essentialUnmappedBones = avatarBones
                .Where(b => essentialBodyParts.Contains(b.bodyPart))
                .Where(b => 
                {
                    BoneData costumeBone;
                    float confidence;
                    MappingMethod method;
                    bool isManual;
                    return !mappingData.GetCostumeBoneForAvatarBone(b.id, out costumeBone, out confidence, out method, out isManual);
                })
                .ToList();
            
            // 未使用の衣装ボーン
            var usedCostumeBoneIds = new HashSet<string>();
            foreach (var bone in avatarBones)
            {
                BoneData costumeBone;
                float confidence;
                MappingMethod method;
                bool isManual;
                
                if (mappingData.GetCostumeBoneForAvatarBone(bone.id, out costumeBone, out confidence, out method, out isManual))
                {
                    usedCostumeBoneIds.Add(costumeBone.id);
                }
            }
            
            var availableCostumeBones = costumeBones
                .Where(b => !usedCostumeBoneIds.Contains(b.id) && !mappingData.IsCostumeBoneExcluded(b.id))
                .ToList();
            
            // 各重要ボーンに対してフォールバックマッピングを適用
            foreach (var avatarBone in essentialUnmappedBones)
            {
                if (mappingData.IsAvatarBoneExcluded(avatarBone.id))
                    continue;
                
                // 同じ体の部位の未使用衣装ボーンを探す
                var sameBodyPartBones = availableCostumeBones
                    .Where(b => b.bodyPart == avatarBone.bodyPart)
                    .ToList();
                
                if (sameBodyPartBones.Count > 0)
                {
                    // 最も信頼度が高そうなボーンを選択
                    var bestMatch = sameBodyPartBones.First(); // 単純に最初のものを選択
                    
                    // フォールバックマッピングを適用（低い信頼度）
                    mappingData.AddOrUpdateMapping(
                        avatarBone.id,
                        bestMatch.id,
                        0.3f, // 低い信頼度で設定
                        MappingMethod.HierarchyBased // フォールバックとして階層ベースを使用
                    );
                    
                    fallbackCount++;
                    
                    // 使用したボーンを利用可能リストから削除
                    availableCostumeBones.Remove(bestMatch);
                }
                else
                {
                    // 体の部位が異なっても同様の機能を持つボーンを探す
                    BoneData fallbackBone = FindFallbackBone(avatarBone, availableCostumeBones);
                    
                    if (fallbackBone != null)
                    {
                        // フォールバックマッピングを適用（非常に低い信頼度）
                        mappingData.AddOrUpdateMapping(
                            avatarBone.id,
                            fallbackBone.id,
                            0.1f, // 非常に低い信頼度
                            MappingMethod.PositionBased // 最終手段
                        );
                        
                        fallbackCount++;
                        
                        // 使用したボーンを利用可能リストから削除
                        availableCostumeBones.Remove(fallbackBone);
                    }
                }
            }
            
            return fallbackCount;
        }
        
        /// <summary>
        /// フォールバック用のボーンを探す（最終手段）
        /// </summary>
        private static BoneData FindFallbackBone(BoneData avatarBone, List<BoneData> availableBones)
        {
            // 体の部位が同じグループに属するボーンを探す
            BodyPart targetPart = avatarBone.bodyPart;
            List<BodyPart> similarParts = new List<BodyPart>();
            
            // 上半身グループ
            if (targetPart == BodyPart.Head || targetPart == BodyPart.Neck || 
                targetPart == BodyPart.Chest || targetPart == BodyPart.UpperChest || 
                targetPart == BodyPart.Spine)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.Head, BodyPart.Neck, BodyPart.Chest, 
                    BodyPart.UpperChest, BodyPart.Spine 
                });
            }
            // 左腕グループ
            else if (targetPart == BodyPart.LeftShoulder || targetPart == BodyPart.LeftUpperArm || 
                     targetPart == BodyPart.LeftLowerArm || targetPart == BodyPart.LeftHand)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.LeftShoulder, BodyPart.LeftUpperArm, 
                    BodyPart.LeftLowerArm, BodyPart.LeftHand 
                });
            }
            // 右腕グループ
            else if (targetPart == BodyPart.RightShoulder || targetPart == BodyPart.RightUpperArm || 
                     targetPart == BodyPart.RightLowerArm || targetPart == BodyPart.RightHand)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.RightShoulder, BodyPart.RightUpperArm, 
                    BodyPart.RightLowerArm, BodyPart.RightHand 
                });
            }
            // 左脚グループ
            else if (targetPart == BodyPart.LeftUpperLeg || targetPart == BodyPart.LeftLowerLeg || 
                     targetPart == BodyPart.LeftFoot || targetPart == BodyPart.LeftToes)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.LeftUpperLeg, BodyPart.LeftLowerLeg, 
                    BodyPart.LeftFoot, BodyPart.LeftToes 
                });
            }
            // 右脚グループ
            else if (targetPart == BodyPart.RightUpperLeg || targetPart == BodyPart.RightLowerLeg || 
                     targetPart == BodyPart.RightFoot || targetPart == BodyPart.RightToes)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.RightUpperLeg, BodyPart.RightLowerLeg, 
                    BodyPart.RightFoot, BodyPart.RightToes 
                });
            }
            // 指グループ
            else if (targetPart == BodyPart.LeftThumb || targetPart == BodyPart.LeftIndex || 
                     targetPart == BodyPart.LeftMiddle || targetPart == BodyPart.LeftRing || 
                     targetPart == BodyPart.LeftPinky)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.LeftThumb, BodyPart.LeftIndex, BodyPart.LeftMiddle, 
                    BodyPart.LeftRing, BodyPart.LeftPinky, BodyPart.LeftHand 
                });
            }
            // 右指グループ
            else if (targetPart == BodyPart.RightThumb || targetPart == BodyPart.RightIndex || 
                     targetPart == BodyPart.RightMiddle || targetPart == BodyPart.RightRing || 
                     targetPart == BodyPart.RightPinky)
            {
                similarParts.AddRange(new[] { 
                    BodyPart.RightThumb, BodyPart.RightIndex, BodyPart.RightMiddle, 
                    BodyPart.RightRing, BodyPart.RightPinky, BodyPart.RightHand 
                });
            }
            
            // 類似した体の部位を持つボーンを探す
            var candidateBones = availableBones
                .Where(b => similarParts.Contains(b.bodyPart))
                .ToList();
            
            if (candidateBones.Count > 0)
            {
                // 単純に最初のものを選択
                return candidateBones.First();
            }
            
            // 同グループのボーンが見つからない場合、最終手段として任意のボーンを返す
            return availableBones.Count > 0 ? availableBones.First() : null;
        }
    }
}
