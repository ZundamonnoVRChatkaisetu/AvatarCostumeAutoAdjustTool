using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 位置ベースのボーンマッピングを行うクラス
    /// </summary>
    public static class PositionBasedMapper
    {
        /// <summary>
        /// 位置ベースのマッピングを実行
        /// </summary>
        /// <returns>マッピングされたボーンの数</returns>
        public static int PerformMapping(MappingData mappingData, List<BoneData> avatarBones, List<BoneData> costumeBones)
        {
            if (mappingData == null || avatarBones == null || costumeBones == null)
                return 0;
                
            // 未マッピングのアバターボーンのみを対象にする
            var unmappedAvatarBones = mappingData.GetUnmappedAvatarBoneIds()
                .Select(id => avatarBones.FirstOrDefault(b => b.id == id))
                .Where(b => b != null && b.transform != null) // Transformが必要
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
                .Where(b => !mappedCostumeBoneIds.Contains(b.id) && 
                           !mappingData.IsCostumeBoneExcluded(b.id) &&
                           b.transform != null) // Transformが必要
                .ToList();
            
            int mappedCount = 0;
            
            // マッピング実行
            foreach (var avatarBone in unmappedAvatarBones)
            {
                // 除外リストにあるボーンはスキップ
                if (mappingData.IsAvatarBoneExcluded(avatarBone.id))
                    continue;
                
                var result = FindBestPositionBasedMatch(avatarBone, availableCostumeBones);
                
                if (result.targetBone != null && result.confidence > 0)
                {
                    mappingData.AddOrUpdateMapping(
                        avatarBone.id, 
                        result.targetBone.id, 
                        result.confidence, 
                        MappingMethod.PositionBased
                    );
                    
                    mappedCount++;
                    
                    // マッピングされたボーンを利用可能リストから除外
                    availableCostumeBones.Remove(result.targetBone);
                }
            }
            
            return mappedCount;
        }
        
        /// <summary>
        /// 指定されたボーンに最も一致する位置ベースのマッチングを見つける
        /// </summary>
        private static (BoneData targetBone, float confidence) FindBestPositionBasedMatch(
            BoneData sourceBone, 
            List<BoneData> targetBones)
        {
            if (sourceBone == null || targetBones == null || targetBones.Count == 0 || 
                sourceBone.transform == null)
                return (null, 0f);
            
            // ルートボーン位置の取得
            Transform avatarRoot = FindRootTransform(sourceBone.transform);
            if (avatarRoot == null) return (null, 0f);
            
            // アバターボーンの位置を取得（ルート空間）
            Vector3 avatarRootPosition = avatarRoot.position;
            Vector3 sourceBoneRelativePosition = sourceBone.transform.position - avatarRootPosition;
            
            // マッチング候補
            BoneData bestMatch = null;
            float bestConfidence = 0f;
            float minDistance = float.MaxValue;
            
            // 衣装ボーンの中から最も近い位置のものを探す
            foreach (var targetBone in targetBones)
            {
                if (targetBone.transform == null) continue;
                
                Transform costumeRoot = FindRootTransform(targetBone.transform);
                if (costumeRoot == null) continue;
                
                // 衣装ボーンの位置を取得（ルート空間）
                Vector3 costumeRootPosition = costumeRoot.position;
                Vector3 targetBoneRelativePosition = targetBone.transform.position - costumeRootPosition;
                
                // 正規化されたモデルサイズを考慮した距離計算
                float avatarHeight = EstimateModelHeight(avatarRoot);
                float costumeHeight = EstimateModelHeight(costumeRoot);
                
                // 高さが0以下の場合はスキップ
                if (avatarHeight <= 0f || costumeHeight <= 0f) continue;
                
                // スケールを正規化
                float scaleRatio = avatarHeight / costumeHeight;
                Vector3 scaledTargetPosition = targetBoneRelativePosition * scaleRatio;
                
                // 距離計算
                float distance = Vector3.Distance(sourceBoneRelativePosition, scaledTargetPosition);
                
                // 体の部位を考慮
                float bodyPartFactor = 1.0f;
                if (sourceBone.bodyPart != BodyPart.Other && 
                    targetBone.bodyPart != BodyPart.Other)
                {
                    // 同じ体の部位なら距離を割り引く
                    if (sourceBone.bodyPart == targetBone.bodyPart)
                    {
                        bodyPartFactor = 0.5f;
                    }
                    // 左右が逆の場合はペナルティ
                    else if (IsSymmetricalBodyPart(sourceBone.bodyPart, targetBone.bodyPart))
                    {
                        bodyPartFactor = 1.5f;
                    }
                }
                
                // 補正された距離
                float adjustedDistance = distance * bodyPartFactor;
                
                // より良い一致が見つかった場合は更新
                if (adjustedDistance < minDistance)
                {
                    minDistance = adjustedDistance;
                    bestMatch = targetBone;
                }
            }
            
            // 信頼度の計算（距離が小さいほど信頼度が高い）
            if (bestMatch != null)
            {
                // モデルの高さを基準とした相対距離
                Transform avatarRoot2 = FindRootTransform(sourceBone.transform);
                float avatarHeight = EstimateModelHeight(avatarRoot2);
                
                // 距離が大きすぎる場合は信頼度を0に
                if (minDistance > avatarHeight * 0.2f) // 高さの20%以上離れていれば信頼度は低い
                {
                    bestConfidence = 0f;
                }
                else
                {
                    // 信頼度の計算（距離が0なら1.0、モデル高さの20%で0.0）
                    bestConfidence = Mathf.Clamp01(1.0f - (minDistance / (avatarHeight * 0.2f)));
                    
                    // 体の部位が一致する場合は信頼度を上げる
                    if (sourceBone.bodyPart != BodyPart.Other && 
                        bestMatch.bodyPart != BodyPart.Other && 
                        sourceBone.bodyPart == bestMatch.bodyPart)
                    {
                        bestConfidence = Mathf.Min(1.0f, bestConfidence + 0.2f);
                    }
                }
            }
            
            return (bestMatch, bestConfidence);
        }
        
        /// <summary>
        /// ルートTransformを取得（Animatorの親か最上位Transform）
        /// </summary>
        private static Transform FindRootTransform(Transform boneTransform)
        {
            Transform current = boneTransform;
            
            while (current.parent != null)
            {
                if (current.GetComponent<Animator>() != null)
                {
                    return current;
                }
                
                // 親がいなければそこがルート
                if (current.parent == null)
                {
                    return current;
                }
                
                current = current.parent;
            }
            
            return current;
        }
        
        /// <summary>
        /// モデルのおおよその高さを推定
        /// </summary>
        private static float EstimateModelHeight(Transform root)
        {
            if (root == null) return 0f;
            
            // モデル全体のバウンズを計算
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            
            // レンダラーを使用してバウンズを計算
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            
            // スキンメッシュレンダラーがない場合はコライダーで試行
            if (renderers.Length == 0)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            
            // どちらもない場合はTransformの階層で推定
            if (renderers.Length == 0 && root.GetComponentsInChildren<Collider>().Length == 0)
            {
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                
                Transform[] transforms = root.GetComponentsInChildren<Transform>();
                foreach (Transform t in transforms)
                {
                    min = Vector3.Min(min, t.position);
                    max = Vector3.Max(max, t.position);
                }
                
                bounds.SetMinMax(min, max);
            }
            
            // 高さを返す
            return bounds.size.y;
        }
        
        /// <summary>
        /// 対称的な体の部位かどうかをチェック（左右が逆）
        /// </summary>
        private static bool IsSymmetricalBodyPart(BodyPart part1, BodyPart part2)
        {
            switch (part1)
            {
                case BodyPart.LeftShoulder: return part2 == BodyPart.RightShoulder;
                case BodyPart.LeftUpperArm: return part2 == BodyPart.RightUpperArm;
                case BodyPart.LeftLowerArm: return part2 == BodyPart.RightLowerArm;
                case BodyPart.LeftHand: return part2 == BodyPart.RightHand;
                case BodyPart.LeftThumb: return part2 == BodyPart.RightThumb;
                case BodyPart.LeftIndex: return part2 == BodyPart.RightIndex;
                case BodyPart.LeftMiddle: return part2 == BodyPart.RightMiddle;
                case BodyPart.LeftRing: return part2 == BodyPart.RightRing;
                case BodyPart.LeftPinky: return part2 == BodyPart.RightPinky;
                case BodyPart.LeftUpperLeg: return part2 == BodyPart.RightUpperLeg;
                case BodyPart.LeftLowerLeg: return part2 == BodyPart.RightLowerLeg;
                case BodyPart.LeftFoot: return part2 == BodyPart.RightFoot;
                case BodyPart.LeftToes: return part2 == BodyPart.RightToes;
                
                case BodyPart.RightShoulder: return part2 == BodyPart.LeftShoulder;
                case BodyPart.RightUpperArm: return part2 == BodyPart.LeftUpperArm;
                case BodyPart.RightLowerArm: return part2 == BodyPart.LeftLowerArm;
                case BodyPart.RightHand: return part2 == BodyPart.LeftHand;
                case BodyPart.RightThumb: return part2 == BodyPart.LeftThumb;
                case BodyPart.RightIndex: return part2 == BodyPart.LeftIndex;
                case BodyPart.RightMiddle: return part2 == BodyPart.LeftMiddle;
                case BodyPart.RightRing: return part2 == BodyPart.LeftRing;
                case BodyPart.RightPinky: return part2 == BodyPart.LeftPinky;
                case BodyPart.RightUpperLeg: return part2 == BodyPart.LeftUpperLeg;
                case BodyPart.RightLowerLeg: return part2 == BodyPart.LeftLowerLeg;
                case BodyPart.RightFoot: return part2 == BodyPart.LeftFoot;
                case BodyPart.RightToes: return part2 == BodyPart.LeftToes;
                
                default: return false;
            }
        }
    }
}
