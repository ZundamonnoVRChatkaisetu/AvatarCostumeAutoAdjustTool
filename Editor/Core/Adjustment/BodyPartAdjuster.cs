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
        // 部位ごとのレンダラー参照のキャッシュ
        private static Dictionary<int, Dictionary<BodyPart, List<Renderer>>> rendererCache = 
            new Dictionary<int, Dictionary<BodyPart, List<Renderer>>>();
            
        // キャッシュのクリーンアップ用のタイムスタンプ
        private static double lastCacheCleanupTime = 0;
        private static readonly double CACHE_CLEANUP_INTERVAL = 300.0; // 5分間隔でクリーンアップ

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
            
            // ボディパーツ調整が無効の場合はスキップ
            var adjustment = settings.bodyPartAdjustments[bodyPart];
            if (!adjustment.isEnabled)
            {
                return;
            }
            
            // 1. キャッシュをチェックしてクリーンアップ
            CleanupCacheIfNeeded();
            
            // 2. 体の部位ごとのレンダラーを特定（キャッシュを利用）
            int costumeId = costumeObject.GetInstanceID();
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers;
            
            if (rendererCache.ContainsKey(costumeId))
            {
                bodyPartRenderers = rendererCache[costumeId];
            }
            else
            {
                bodyPartRenderers = IdentifyBodyPartRenderers(costumeObject);
                rendererCache[costumeId] = bodyPartRenderers;
            }
            
            // 3. 部位別調整を適用
            ApplyBodyPartAdjustment(bodyPartRenderers, bodyPart, adjustment);
            
            // 4. スキンメッシュレンダラーを更新して反映を確実にする
            UpdateSkinnedMeshRenderers(bodyPartRenderers[bodyPart]);
            
            Debug.Log($"体の部位 {bodyPart} に調整を適用しました。");
        }
        
        /// <summary>
        /// スキンメッシュレンダラーを更新して反映を確実にする
        /// </summary>
        private static void UpdateSkinnedMeshRenderers(List<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
                {
                    // 現在のボーンとルートボーンを保存
                    Transform[] bones = skinnedRenderer.bones;
                    Transform rootBone = skinnedRenderer.rootBone;
                    
                    // 一度リセットして再設定することで強制的に更新
                    skinnedRenderer.bones = null;
                    skinnedRenderer.rootBone = null;
                    
                    skinnedRenderer.bones = bones;
                    skinnedRenderer.rootBone = rootBone;
                    
                    // レンダラーを再評価
                    if (skinnedRenderer.sharedMesh.isReadable)
                    {
                        skinnedRenderer.sharedMesh.MarkModified();
                    }
                }
            }
        }
        
        /// <summary>
        /// キャッシュのクリーンアップが必要かチェックして実行
        /// </summary>
        private static void CleanupCacheIfNeeded()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            
            if (currentTime - lastCacheCleanupTime > CACHE_CLEANUP_INTERVAL)
            {
                // 無効なエントリをクリーンアップ
                List<int> keysToRemove = new List<int>();
                
                foreach (var kvp in rendererCache)
                {
                    bool isValid = false;
                    
                    // 各部位の少なくとも1つのレンダラーが有効かチェック
                    foreach (var partKvp in kvp.Value)
                    {
                        foreach (var renderer in partKvp.Value)
                        {
                            if (renderer != null)
                            {
                                isValid = true;
                                break;
                            }
                        }
                        
                        if (isValid) break;
                    }
                    
                    if (!isValid)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                // 無効なエントリを削除
                foreach (int key in keysToRemove)
                {
                    rendererCache.Remove(key);
                }
                
                lastCacheCleanupTime = currentTime;
            }
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
            
            // 4. 親子関係による追加分類 (子オブジェクトは親の部位を継承しやすい)
            ClassifyByParentChildRelationship(costumeObject, result);
            
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
                if (renderer == null) continue;
                
                string name = renderer.name.ToLower();
                BodyPart identifiedPart = IdentifyBodyPartFromName(name);
                
                // 親オブジェクトの名前も考慮
                if (identifiedPart == BodyPart.Other && renderer.transform.parent != null)
                {
                    string parentName = renderer.transform.parent.name.ToLower();
                    identifiedPart = IdentifyBodyPartFromName(parentName);
                }
                
                result[identifiedPart].Add(renderer);
            }
        }
        
        /// <summary>
        /// 名前から体の部位を識別
        /// </summary>
        private static BodyPart IdentifyBodyPartFromName(string name)
        {
            // 頭部関連
            if (name.Contains("head") || name.Contains("face") || name.Contains("hair") || 
                name.Contains("skull") || name.Contains("cranium"))
            {
                return BodyPart.Head;
            }
            else if (name.Contains("neck"))
            {
                return BodyPart.Neck;
            }
            // 胸部関連
            else if (name.Contains("chest") && name.Contains("upper"))
            {
                return BodyPart.UpperChest;
            }
            else if (name.Contains("chest") || name.Contains("breast") || name.Contains("torso") || name.Contains("bust"))
            {
                return BodyPart.Chest;
            }
            else if (name.Contains("spine") || name.Contains("back") || name.Contains("column"))
            {
                return BodyPart.Spine;
            }
            else if (name.Contains("hip") || name.Contains("pelvis") || name.Contains("waist"))
            {
                return BodyPart.Hips;
            }
            
            // 左右の判定パターンを整理
            bool isLeft = IsLeftPart(name);
            bool isRight = IsRightPart(name);
            
            // 肩関連
            if (name.Contains("shoulder"))
            {
                if (isLeft) return BodyPart.LeftShoulder;
                if (isRight) return BodyPart.RightShoulder;
            }
            
            // 腕関連
            if ((name.Contains("upper") && name.Contains("arm")) || (name.Contains("arm") && !name.Contains("fore") && !name.Contains("lower")))
            {
                if (isLeft) return BodyPart.LeftUpperArm;
                if (isRight) return BodyPart.RightUpperArm;
            }
            else if ((name.Contains("lower") && name.Contains("arm")) || name.Contains("forearm") || name.Contains("elbow"))
            {
                if (isLeft) return BodyPart.LeftLowerArm;
                if (isRight) return BodyPart.RightLowerArm;
            }
            else if (name.Contains("hand") && !name.Contains("finger") && !name.Contains("thumb") && !name.Contains("wrist"))
            {
                if (isLeft) return BodyPart.LeftHand;
                if (isRight) return BodyPart.RightHand;
            }
            else if (name.Contains("wrist"))
            {
                if (isLeft) return BodyPart.LeftHand;
                if (isRight) return BodyPart.RightHand;
            }
            
            // 指関連
            else if (name.Contains("thumb"))
            {
                if (isLeft) return BodyPart.LeftThumb;
                if (isRight) return BodyPart.RightThumb;
            }
            else if (name.Contains("index"))
            {
                if (isLeft) return BodyPart.LeftIndex;
                if (isRight) return BodyPart.RightIndex;
            }
            else if (name.Contains("middle"))
            {
                if (isLeft) return BodyPart.LeftMiddle;
                if (isRight) return BodyPart.RightMiddle;
            }
            else if (name.Contains("ring") && (name.Contains("finger") || !name.Contains("ear")))
            {
                if (isLeft) return BodyPart.LeftRing;
                if (isRight) return BodyPart.RightRing;
            }
            else if (name.Contains("pinky") || name.Contains("little"))
            {
                if (isLeft) return BodyPart.LeftPinky;
                if (isRight) return BodyPart.RightPinky;
            }
            else if (name.Contains("finger"))
            {
                if (isLeft) return BodyPart.LeftHand;
                if (isRight) return BodyPart.RightHand;
            }
            
            // 脚関連
            else if ((name.Contains("thigh") || (name.Contains("upper") && name.Contains("leg"))))
            {
                if (isLeft) return BodyPart.LeftUpperLeg;
                if (isRight) return BodyPart.RightUpperLeg;
            }
            else if (name.Contains("calf") || name.Contains("shin") || (name.Contains("lower") && name.Contains("leg")) || name.Contains("knee"))
            {
                if (isLeft) return BodyPart.LeftLowerLeg;
                if (isRight) return BodyPart.RightLowerLeg;
            }
            else if (name.Contains("ankle"))
            {
                if (isLeft) return BodyPart.LeftFoot;
                if (isRight) return BodyPart.RightFoot;
            }
            else if (name.Contains("foot") && !name.Contains("toe"))
            {
                if (isLeft) return BodyPart.LeftFoot;
                if (isRight) return BodyPart.RightFoot;
            }
            else if (name.Contains("toe"))
            {
                if (isLeft) return BodyPart.LeftToes;
                if (isRight) return BodyPart.RightToes;
            }
            
            // その他の名前でも左右の腕/脚を検出
            else if (name.Contains("arm") || name.Contains("sleeve"))
            {
                if (isLeft) return BodyPart.LeftUpperArm;
                if (isRight) return BodyPart.RightUpperArm;
            }
            else if (name.Contains("leg") || name.Contains("thigh") || name.Contains("pants"))
            {
                if (isLeft) return BodyPart.LeftUpperLeg;
                if (isRight) return BodyPart.RightUpperLeg;
            }
            
            return BodyPart.Other;
        }
        
        /// <summary>
        /// 左側の部位かどうかを判定
        /// </summary>
        private static bool IsLeftPart(string name)
        {
            return name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || 
                   name.EndsWith(".l") || name.EndsWith("_l") || name.EndsWith("_left") ||
                   name.StartsWith("left") || name == "l" || name.Contains("_l_");
        }
        
        /// <summary>
        /// 右側の部位かどうかを判定
        /// </summary>
        private static bool IsRightPart(string name)
        {
            return name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || 
                   name.EndsWith(".r") || name.EndsWith("_r") || name.EndsWith("_right") ||
                   name.StartsWith("right") || name == "r" || name.Contains("_r_");
        }
        
        /// <summary>
        /// スキンメッシュのボーン情報から体の部位を推定して分類
        /// </summary>
        private static void ClassifyRenderersByBones(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            SkinnedMeshRenderer[] skinnedRenderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer == null) continue;
                
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
                Dictionary<BodyPart, float> partWeights = new Dictionary<BodyPart, float>();
                
                // 各ボーンの重要度を計算
                CalculateBonesInfluence(renderer, partWeights);
                
                // 最も影響の大きい部位を選択（Other以外）
                var sortedParts = partWeights
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
        /// 各ボーンがスキンメッシュに与える影響度を計算
        /// </summary>
        private static void CalculateBonesInfluence(SkinnedMeshRenderer renderer, Dictionary<BodyPart, float> partWeights)
        {
            // 1. ボーンの部位を特定
            Dictionary<int, BodyPart> boneIndexToPart = new Dictionary<int, BodyPart>();
            for (int i = 0; i < renderer.bones.Length; i++)
            {
                if (renderer.bones[i] != null)
                {
                    boneIndexToPart[i] = EstimateBodyPartFromBoneName(renderer.bones[i].name);
                }
            }
            
            // 2. メッシュが読み取り可能な場合はウェイトを考慮
            if (renderer.sharedMesh != null && renderer.sharedMesh.isReadable)
            {
                BoneWeight[] weights = renderer.sharedMesh.boneWeights;
                
                foreach (var weight in weights)
                {
                    // 各ボーンのウェイトに基づいて部位の影響度を加算
                    AddBonePartWeight(partWeights, boneIndexToPart, weight.boneIndex0, weight.weight0);
                    AddBonePartWeight(partWeights, boneIndexToPart, weight.boneIndex1, weight.weight1);
                    AddBonePartWeight(partWeights, boneIndexToPart, weight.boneIndex2, weight.weight2);
                    AddBonePartWeight(partWeights, boneIndexToPart, weight.boneIndex3, weight.weight3);
                }
            }
            else
            {
                // 3. 読み取り不可の場合はボーンの数のみで判断
                foreach (int boneIndex in boneIndexToPart.Keys)
                {
                    BodyPart part = boneIndexToPart[boneIndex];
                    
                    if (!partWeights.ContainsKey(part))
                    {
                        partWeights[part] = 0f;
                    }
                    
                    partWeights[part] += 1.0f;
                }
            }
        }
        
        /// <summary>
        /// ボーン部位のウェイトを追加するヘルパーメソッド
        /// </summary>
        private static void AddBonePartWeight(
            Dictionary<BodyPart, float> partWeights, 
            Dictionary<int, BodyPart> boneIndexToPart, 
            int boneIndex, 
            float weight)
        {
            if (weight <= 0f || !boneIndexToPart.ContainsKey(boneIndex))
                return;
            
            BodyPart part = boneIndexToPart[boneIndex];
            
            if (!partWeights.ContainsKey(part))
            {
                partWeights[part] = 0f;
            }
            
            partWeights[part] += weight;
        }
        
        /// <summary>
        /// 残りのレンダラーを位置ベースで分類
        /// </summary>
        private static void ClassifyRemainingRenderersByPosition(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            // 基準となる部位の位置を取得
            Dictionary<BodyPart, Vector3> partPositions = new Dictionary<BodyPart, Vector3>();
            
            // 基準となる部位のサイズも考慮
            Dictionary<BodyPart, float> partSizes = new Dictionary<BodyPart, float>();
            
            foreach (var kvp in result)
            {
                if (kvp.Key != BodyPart.Other && kvp.Value.Count > 0)
                {
                    // その部位のレンダラーの平均位置を計算
                    Vector3 sum = Vector3.zero;
                    float totalSize = 0f;
                    int count = 0;
                    
                    foreach (var renderer in kvp.Value)
                    {
                        if (renderer != null)
                        {
                            sum += renderer.bounds.center;
                            totalSize += renderer.bounds.size.magnitude;
                            count++;
                        }
                    }
                    
                    if (count > 0)
                    {
                        partPositions[kvp.Key] = sum / count;
                        partSizes[kvp.Key] = totalSize / count;
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
                
                // 最も近い部位を探す（部位のサイズも考慮）
                BodyPart closestPart = BodyPart.Other;
                float minNormalizedDistance = float.MaxValue;
                
                foreach (var kvp in partPositions)
                {
                    float distance = Vector3.Distance(renderer.bounds.center, kvp.Value);
                    float partSize = partSizes[kvp.Key];
                    
                    // サイズで正規化した距離（大きい部位ほど広い範囲をカバー）
                    float normalizedDistance = distance / (partSize + 0.001f);
                    
                    if (normalizedDistance < minNormalizedDistance)
                    {
                        minNormalizedDistance = normalizedDistance;
                        closestPart = kvp.Key;
                    }
                }
                
                // 十分に近い場合のみ再分類
                if (closestPart != BodyPart.Other && minNormalizedDistance < 2.0f)
                {
                    result[BodyPart.Other].Remove(renderer);
                    result[closestPart].Add(renderer);
                }
            }
        }
        
        /// <summary>
        /// 親子関係による追加分類
        /// </summary>
        private static void ClassifyByParentChildRelationship(GameObject costumeObject, Dictionary<BodyPart, List<Renderer>> result)
        {
            // 子オブジェクトが親のボディパーツを継承するパターンを処理
            // Otherに分類されたレンダラーのうち、親が特定の部位に分類されている場合、その部位を継承
            
            var otherRenderers = new List<Renderer>(result[BodyPart.Other]);
            
            foreach (var renderer in otherRenderers)
            {
                if (renderer == null || renderer.transform.parent == null)
                {
                    continue;
                }
                
                // 親オブジェクトのレンダラーを探す
                Renderer parentRenderer = renderer.transform.parent.GetComponent<Renderer>();
                if (parentRenderer == null)
                {
                    continue;
                }
                
                // 親レンダラーの部位を特定
                BodyPart parentPart = BodyPart.Other;
                
                foreach (var kvp in result)
                {
                    if (kvp.Value.Contains(parentRenderer) && kvp.Key != BodyPart.Other)
                    {
                        parentPart = kvp.Key;
                        break;
                    }
                }
                
                // 親が特定の部位に分類されていれば子も同じ部位に
                if (parentPart != BodyPart.Other)
                {
                    result[BodyPart.Other].Remove(renderer);
                    result[parentPart].Add(renderer);
                }
            }
            
            // 兄弟関係も考慮（同じ親を持つレンダラーは同じ部位に分類されやすい）
            var stillOtherRenderers = new List<Renderer>(result[BodyPart.Other]);
            
            foreach (var renderer in stillOtherRenderers)
            {
                if (renderer == null || renderer.transform.parent == null)
                {
                    continue;
                }
                
                // 同じ親を持つ兄弟レンダラーを探す
                foreach (Transform siblingTransform in renderer.transform.parent)
                {
                    if (siblingTransform == renderer.transform)
                        continue;
                    
                    Renderer siblingRenderer = siblingTransform.GetComponent<Renderer>();
                    if (siblingRenderer == null)
                        continue;
                    
                    // 兄弟の部位を特定
                    BodyPart siblingPart = BodyPart.Other;
                    
                    foreach (var kvp in result)
                    {
                        if (kvp.Value.Contains(siblingRenderer) && kvp.Key != BodyPart.Other)
                        {
                            siblingPart = kvp.Key;
                            break;
                        }
                    }
                    
                    // 兄弟が特定の部位に分類されていれば同じ部位に
                    if (siblingPart != BodyPart.Other)
                    {
                        result[BodyPart.Other].Remove(renderer);
                        result[siblingPart].Add(renderer);
                        break;
                    }
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
            
            // 適用するかどうかをチェック
            if (!adjustment.isEnabled)
            {
                return;
            }
            
            // スケールの適用
            Vector3 scale = adjustment.GetScaleVector();
            bool applyScale = adjustment.adjustScale && scale != Vector3.one;
            
            // オフセットの適用
            Vector3 offset = adjustment.GetOffsetVector();
            bool applyOffset = adjustment.adjustPosition && offset != Vector3.zero;
            
            // 回転の適用
            Quaternion rotation = Quaternion.Euler(adjustment.rotation);
            bool applyRotation = adjustment.adjustRotation && adjustment.rotation != Vector3.zero;
            
            // 何も適用するものがなければ早期リターン
            if (!applyScale && !applyOffset && !applyRotation)
            {
                return;
            }
            
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
                if (applyScale)
                {
                    rendererTransform.localScale = Vector3.Scale(rendererTransform.localScale, scale);
                    
                    // スキンメッシュレンダラーの場合、関連するボーンのスケールも調整
                    SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                    if (skinnedRenderer != null && skinnedRenderer.bones != null)
                    {
                        foreach (var bone in skinnedRenderer.bones)
                        {
                            if (bone != null && bone.gameObject.activeInHierarchy)
                            {
                                // ボーン名から部位を推定
                                BodyPart bonePart = EstimateBodyPartFromBoneName(bone.name);
                                
                                // 同じ部位のボーンのみスケール調整
                                if (bonePart == bodyPart)
                                {
                                    bone.localScale = Vector3.Scale(bone.localScale, scale);
                                }
                            }
                        }
                    }
                }
                
                // 回転適用（部位の中心を基準に）
                if (applyRotation)
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
                if (applyOffset)
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
            if (renderers == null || renderers.Count == 0)
            {
                return Vector3.zero;
            }
            
            // 有効なレンダラーの数を確認
            int validCount = renderers.Count(r => r != null);
            if (validCount == 0)
            {
                return Vector3.zero;
            }
            
            // バウンディングボックスを統合する方法で中心を計算
            Bounds combinedBounds = new Bounds();
            bool isFirst = true;
            
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    if (isFirst)
                    {
                        combinedBounds = renderer.bounds;
                        isFirst = false;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            
            return combinedBounds.center;
        }
    }
}