using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 体の部位ごとの調整を行うクラス
    /// </summary>
    public static class BodyPartAdjuster
    {
        /// <summary>
        /// 指定された体の部位に調整を適用
        /// </summary>
        public static void ApplyAdjustment(
            GameObject avatarObject,
            GameObject costumeObject,
            BodyPart bodyPart,
            AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeObject == null || settings == null)
            {
                Debug.LogError("部位調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            if (!settings.bodyPartAdjustments.ContainsKey(bodyPart))
            {
                Debug.LogError($"指定された体の部位 {bodyPart} の調整設定が見つかりません。");
                return;
            }
            
            // 1. 体の部位ごとのレンダラーを特定
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers = IdentifyBodyPartRenderers(costumeObject);
            
            // 2. 部位別調整を適用
            ApplyBodyPartAdjustment(bodyPartRenderers, bodyPart, settings.bodyPartAdjustments[bodyPart]);
            
            Debug.Log($"体の部位 {bodyPart} に調整を適用しました。");
        }
        
        /// <summary>
        /// レンダラーを体の部位ごとに分類
        /// </summary>
        private static Dictionary<BodyPart, List<Renderer>> IdentifyBodyPartRenderers(GameObject costumeObject)
        {
            Dictionary<BodyPart, List<Renderer>> result = new Dictionary<BodyPart, List<Renderer>>();
            
            // 部位ごとのリストを初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                result[part] = new List<Renderer>();
            }
            
            // 複数の方法でメッシュを分類
            
            // 1. メッシュの名前から推定
            ClassifyRenderersByName(costumeObject, result);
            
            // 2. スキンメッシュの場合はボーンから推定
            ClassifyRenderersByBones(costumeObject, result);
            
            // 3. 位置から推定（残りのレンダラー）
            ClassifyRemainingRenderersByPosition(costumeObject, result);
            
            return result;
        }
        
        /// <summary>
        /// メッシュ名から体の部位を推定して分類
        /// </summary>
        private static void ClassifyRenderersByName(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            Renderer[] renderers = costumeObject.GetComponentsInChildren<Renderer>();
            
            foreach (var renderer in renderers)
            {
                string name = renderer.name.ToLower();
                BodyPart identifiedPart = BodyPart.Other;
                
                // 名前から部位を推定
                if (name.Contains("head") || name.Contains("face") || name.Contains("hair"))
                {
                    identifiedPart = BodyPart.Head;
                }
                else if (name.Contains("neck"))
                {
                    identifiedPart = BodyPart.Neck;
                }
                else if (name.Contains("chest") && name.Contains("upper"))
                {
                    identifiedPart = BodyPart.UpperChest;
                }
                else if (name.Contains("chest") || name.Contains("breast") || name.Contains("torso"))
                {
                    identifiedPart = BodyPart.Chest;
                }
                else if (name.Contains("spine") || name.Contains("back"))
                {
                    identifiedPart = BodyPart.Spine;
                }
                else if (name.Contains("hip") || name.Contains("pelvis"))
                {
                    identifiedPart = BodyPart.Hips;
                }
                else if (name.Contains("left") && name.Contains("shoulder"))
                {
                    identifiedPart = BodyPart.LeftShoulder;
                }
                else if (name.Contains("right") && name.Contains("shoulder"))
                {
                    identifiedPart = BodyPart.RightShoulder;
                }
                else if (name.Contains("left") && (name.Contains("upper") && name.Contains("arm")))
                {
                    identifiedPart = BodyPart.LeftUpperArm;
                }
                else if (name.Contains("right") && (name.Contains("upper") && name.Contains("arm")))
                {
                    identifiedPart = BodyPart.RightUpperArm;
                }
                else if (name.Contains("left") && (name.Contains("lower") && name.Contains("arm") || name.Contains("forearm")))
                {
                    identifiedPart = BodyPart.LeftLowerArm;
                }
                else if (name.Contains("right") && (name.Contains("lower") && name.Contains("arm") || name.Contains("forearm")))
                {
                    identifiedPart = BodyPart.RightLowerArm;
                }
                else if (name.Contains("left") && name.Contains("hand") && !name.Contains("finger") && !name.Contains("thumb"))
                {
                    identifiedPart = BodyPart.LeftHand;
                }
                else if (name.Contains("right") && name.Contains("hand") && !name.Contains("finger") && !name.Contains("thumb"))
                {
                    identifiedPart = BodyPart.RightHand;
                }
                
                // 指の部位判定
                else if (name.Contains("left") && name.Contains("thumb"))
                {
                    identifiedPart = BodyPart.LeftThumb;
                }
                else if (name.Contains("right") && name.Contains("thumb"))
                {
                    identifiedPart = BodyPart.RightThumb;
                }
                else if (name.Contains("left") && name.Contains("index"))
                {
                    identifiedPart = BodyPart.LeftIndex;
                }
                else if (name.Contains("right") && name.Contains("index"))
                {
                    identifiedPart = BodyPart.RightIndex;
                }
                else if (name.Contains("left") && name.Contains("middle"))
                {
                    identifiedPart = BodyPart.LeftMiddle;
                }
                else if (name.Contains("right") && name.Contains("middle"))
                {
                    identifiedPart = BodyPart.RightMiddle;
                }
                else if (name.Contains("left") && name.Contains("ring"))
                {
                    identifiedPart = BodyPart.LeftRing;
                }
                else if (name.Contains("right") && name.Contains("ring"))
                {
                    identifiedPart = BodyPart.RightRing;
                }
                else if (name.Contains("left") && (name.Contains("pinky") || name.Contains("little")))
                {
                    identifiedPart = BodyPart.LeftPinky;
                }
                else if (name.Contains("right") && (name.Contains("pinky") || name.Contains("little")))
                {
                    identifiedPart = BodyPart.RightPinky;
                }
                
                // 脚の部位判定
                else if (name.Contains("left") && (name.Contains("thigh") || name.Contains("upper") && name.Contains("leg")))
                {
                    identifiedPart = BodyPart.LeftUpperLeg;
                }
                else if (name.Contains("right") && (name.Contains("thigh") || name.Contains("upper") && name.Contains("leg")))
                {
                    identifiedPart = BodyPart.RightUpperLeg;
                }
                else if (name.Contains("left") && (name.Contains("calf") || name.Contains("lower") && name.Contains("leg")))
                {
                    identifiedPart = BodyPart.LeftLowerLeg;
                }
                else if (name.Contains("right") && (name.Contains("calf") || name.Contains("lower") && name.Contains("leg")))
                {
                    identifiedPart = BodyPart.RightLowerLeg;
                }
                else if (name.Contains("left") && name.Contains("foot") && !name.Contains("toe"))
                {
                    identifiedPart = BodyPart.LeftFoot;
                }
                else if (name.Contains("right") && name.Contains("foot") && !name.Contains("toe"))
                {
                    identifiedPart = BodyPart.RightFoot;
                }
                else if (name.Contains("left") && name.Contains("toe"))
                {
                    identifiedPart = BodyPart.LeftToes;
                }
                else if (name.Contains("right") && name.Contains("toe"))
                {
                    identifiedPart = BodyPart.RightToes;
                }
                
                result[identifiedPart].Add(renderer);
            }
        }
        
        /// <summary>
        /// スキンメッシュのボーン情報から体の部位を推定して分類
        /// </summary>
        private static void ClassifyRenderersByBones(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            SkinnedMeshRenderer[] skinnedRenderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in skinnedRenderers)
            {
                // すでに名前ベースで分類されている場合はスキップ
                if (result.Any(kvp => kvp.Value.Contains(renderer) && kvp.Key != BodyPart.Other))
                {
                    continue;
                }
                
                // ボーン情報がない場合はスキップ
                if (renderer.bones == null || renderer.bones.Length == 0)
                {
                    continue;
                }
                
                // ボーン名から部位を推定
                Dictionary<BodyPart, int> partCounts = new Dictionary<BodyPart, int>();
                
                foreach (var bone in renderer.bones)
                {
                    if (bone != null)
                    {
                        BodyPart bonePart = EstimateBodyPartFromBoneName(bone.name);
                        
                        if (!partCounts.ContainsKey(bonePart))
                        {
                            partCounts[bonePart] = 0;
                        }
                        
                        partCounts[bonePart]++;
                    }
                }
                
                // 最も多く関連付けられている部位を選択（Other以外）
                var sortedParts = partCounts
                    .Where(kvp => kvp.Key != BodyPart.Other)
                    .OrderByDescending(kvp => kvp.Value);
                
                if (sortedParts.Any())
                {
                    BodyPart primaryPart = sortedParts.First().Key;
                    
                    // Other から移動
                    result[BodyPart.Other].Remove(renderer);
                    result[primaryPart].Add(renderer);
                }
            }
        }
        
        /// <summary>
        /// 残りのレンダラーを位置ベースで分類
        /// </summary>
        private static void ClassifyRemainingRenderersByPosition(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            // 基準となる部位の位置を取得
            Dictionary<BodyPart, Vector3> partPositions = new Dictionary<BodyPart, Vector3>();
            
            foreach (var kvp in result)
            {
                if (kvp.Key != BodyPart.Other && kvp.Value.Count > 0)
                {
                    // その部位のレンダラーの平均位置を計算
                    Vector3 sum = Vector3.zero;
                    int count = 0;
                    
                    foreach (var renderer in kvp.Value)
                    {
                        if (renderer != null)
                        {
                            sum += renderer.bounds.center;
                            count++;
                        }
                    }
                    
                    if (count > 0)
                    {
                        partPositions[kvp.Key] = sum / count;
                    }
                }
            }
            
            // Other に分類されたレンダラーを位置ベースで再分類
            var otherRenderers = new List<Renderer>(result[BodyPart.Other]);
            
            foreach (var renderer in otherRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                
                // 最も近い部位を探す
                BodyPart closestPart = BodyPart.Other;
                float minDistance = float.MaxValue;
                
                foreach (var kvp in partPositions)
                {
                    float distance = Vector3.Distance(renderer.bounds.center, kvp.Value);
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPart = kvp.Key;
                    }
                }
                
                // 十分に近い場合のみ再分類
                if (closestPart != BodyPart.Other && minDistance < 0.5f) // 閾値は要調整
                {
                    result[BodyPart.Other].Remove(renderer);
                    result[closestPart].Add(renderer);
                }
            }
        }
        
        /// <summary>
        /// ボーン名から体の部位を推定
        /// </summary>
        private static BodyPart EstimateBodyPartFromBoneName(string boneName)
        {
            // BoneData.EstimateBodyPartと同じロジックを使用
            return BoneData.EstimateBodyPart(boneName);
        }
        
        /// <summary>
        /// 部位別調整を適用
        /// </summary>
        private static void ApplyBodyPartAdjustment(
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers,
            BodyPart bodyPart,
            BodyPartAdjustment adjustment)
        {
            if (!bodyPartRenderers.ContainsKey(bodyPart) || bodyPartRenderers[bodyPart].Count == 0)
            {
                Debug.LogWarning($"体の部位 {bodyPart} に対応するメッシュが見つかりません。");
                return;
            }
            
            // スケールの適用
            Vector3 scale = adjustment.GetScaleVector();
            
            // オフセットの適用
            Vector3 offset = adjustment.GetOffsetVector();
            
            // 回転の適用
            Quaternion rotation = Quaternion.Euler(adjustment.rotation);
            
            // 部位の中心位置を計算（回転の基準点）
            Vector3 center = CalculatePartCenter(bodyPartRenderers[bodyPart]);
            
            // 部位のレンダラーに適用
            foreach (var renderer in bodyPartRenderers[bodyPart])
            {
                if (renderer == null)
                {
                    continue;
                }
                
                Transform rendererTransform = renderer.transform;
                
                // スケール適用
                if (scale != Vector3.one)
                {
                    rendererTransform.localScale = Vector3.Scale(rendererTransform.localScale, scale);
                }
                
                // 回転適用（部位の中心を基準に）
                if (adjustment.rotation != Vector3.zero)
                {
                    // 現在の位置を保存
                    Vector3 originalPosition = rendererTransform.position;
                    
                    // 中心からの相対位置を計算
                    Vector3 relativePosition = originalPosition - center;
                    
                    // 相対位置を回転
                    Vector3 rotatedPosition = center + (rotation * relativePosition);
                    
                    // 回転後の位置を設定
                    rendererTransform.position = rotatedPosition;
                    
                    // レンダラー自体も回転
                    rendererTransform.rotation = rotation * rendererTransform.rotation;
                }
                
                // オフセット適用
                if (offset != Vector3.zero)
                {
                    rendererTransform.position += offset;
                }
            }
        }
        
        /// <summary>
        /// 部位の中心位置を計算
        /// </summary>
        private static Vector3 CalculatePartCenter(List<Renderer> renderers)
        {
            if (renderers.Count == 0)
            {
                return Vector3.zero;
            }
            
            // すべてのレンダラーのバウンディングボックス中心の平均を計算
            Vector3 sum = Vector3.zero;
            int count = 0;
            
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    sum += renderer.bounds.center;
                    count++;
                }
            }
            
            return (count > 0) ? sum / count : Vector3.zero;
        }
    }
}
