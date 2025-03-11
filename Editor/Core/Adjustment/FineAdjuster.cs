using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 衣装の微調整を行うクラス
    /// </summary>
    public static class FineAdjuster
    {
        // 調整の適用履歴を保持
        private static Stack<AdjustmentSettings> adjustmentHistory = new Stack<AdjustmentSettings>();
        
        /// <summary>
        /// 微調整を適用する
        /// </summary>
        public static void ApplyAdjustment(
            GameObject avatarObject,
            GameObject costumeObject,
            AdjustmentSettings settings)
        {
            if (avatarObject == null || settings == null)
            {
                Debug.LogError("微調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            // 衣装オブジェクトが実際に存在するか確認
            if (costumeObject == null || !costumeObject.activeInHierarchy)
            {
                // AdjustmentManagerから最新の衣装インスタンスを取得
                costumeObject = AdjustmentManager.GetCostumeInstance();
                
                if (costumeObject == null)
                {
                    // アバターの子から衣装インスタンスを探す
                    for (int i = 0; i < avatarObject.transform.childCount; i++)
                    {
                        Transform child = avatarObject.transform.GetChild(i);
                        if (child.name.EndsWith("_Instance"))
                        {
                            costumeObject = child.gameObject;
                            break;
                        }
                    }
                    
                    if (costumeObject == null)
                    {
                        Debug.LogError("衣装オブジェクトが見つかりません。先に「衣装を着せる」を実行してください。");
                        return;
                    }
                }
            }

            // 調整履歴に現在の設定を保存
            adjustmentHistory.Push(settings.Clone());

            // 1. グローバルスケールの適用
            costumeObject.transform.localScale = Vector3.one * settings.globalScale;
            
            // 2. 上半身/下半身の部位ごとの調整
            ApplyBodyPartAdjustments(costumeObject, settings);
            
            // 3. スキニングメッシュを更新して確実に反映させる
            UpdateSkinnedMeshRenderers(costumeObject);
            
            Debug.Log("微調整を適用しました。");
        }
        
        /// <summary>
        /// スキニングメッシュレンダラーを更新して反映を確実にする
        /// </summary>
        private static void UpdateSkinnedMeshRenderers(GameObject costumeObject)
        {
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.sharedMesh != null)
                {
                    // 現在のボーンとルートボーンを保存
                    Transform[] bones = renderer.bones;
                    Transform rootBone = renderer.rootBone;
                    
                    // 一度リセットして再設定することで強制的に更新
                    renderer.bones = null;
                    renderer.rootBone = null;
                    
                    renderer.bones = bones;
                    renderer.rootBone = rootBone;
                    
                    // レンダラーを再評価
                    if (renderer.sharedMesh != null && renderer.sharedMesh.isReadable)
                    {
                        renderer.sharedMesh.MarkModified();
                    }
                }
            }
        }
        
        /// <summary>
        /// 体の部位ごとの調整を適用
        /// </summary>
        private static void ApplyBodyPartAdjustments(GameObject costumeObject, AdjustmentSettings settings)
        {
            // 衣装のレンダラーから体の部位ごとのメッシュを特定
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers = IdentifyBodyPartRenderers(costumeObject);
            
            // 上半身のオフセット
            Vector3 upperBodyOffset = settings.GetUpperBodyOffset();
            if (upperBodyOffset != Vector3.zero)
            {
                ApplyUpperBodyOffset(costumeObject, bodyPartRenderers, upperBodyOffset);
            }
            
            // 下半身のオフセット
            Vector3 lowerBodyOffset = settings.GetLowerBodyOffset();
            if (lowerBodyOffset != Vector3.zero)
            {
                ApplyLowerBodyOffset(costumeObject, bodyPartRenderers, lowerBodyOffset);
            }
            
            // 腕のスケール調整
            if (settings.leftArmScale != 1.0f)
            {
                ApplyArmScale(costumeObject, bodyPartRenderers, true, settings.leftArmScale);
            }
            
            if (settings.rightArmScale != 1.0f)
            {
                ApplyArmScale(costumeObject, bodyPartRenderers, false, settings.rightArmScale);
            }
            
            // 脚のスケール調整
            if (settings.leftLegScale != 1.0f)
            {
                ApplyLegScale(costumeObject, bodyPartRenderers, true, settings.leftLegScale);
            }
            
            if (settings.rightLegScale != 1.0f)
            {
                ApplyLegScale(costumeObject, bodyPartRenderers, false, settings.rightLegScale);
            }
            
            // 部位別の詳細調整
            foreach (var part in settings.bodyPartAdjustments.Keys)
            {
                var adjustment = settings.bodyPartAdjustments[part];
                
                // カスタム設定が有効な場合のみ適用
                if (adjustment.isEnabled && adjustment.useCustomSettings)
                {
                    ApplyCustomBodyPartAdjustment(costumeObject, bodyPartRenderers, part, adjustment);
                }
            }
        }
        
        /// <summary>
        /// レンダラーを体の部位ごとに分類（改良版）
        /// </summary>
        private static Dictionary<BodyPart, List<Renderer>> IdentifyBodyPartRenderers(GameObject costumeObject)
        {
            Dictionary<BodyPart, List<Renderer>> result = new Dictionary<BodyPart, List<Renderer>>();
            
            // 部位ごとのリストを初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                result[part] = new List<Renderer>();
            }
            
            // 全レンダラーを収集
            Renderer[] renderers = costumeObject.GetComponentsInChildren<Renderer>(true);
            
            // スキンメッシュレンダラーの場合はボーンから部位を推定
            foreach (var renderer in renderers)
            {
                SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedRenderer != null && skinnedRenderer.bones != null && skinnedRenderer.bones.Length > 0)
                {
                    // ルートボーンから推定
                    if (skinnedRenderer.rootBone != null)
                    {
                        string rootName = skinnedRenderer.rootBone.name.ToLower();
                        BodyPart identifiedPart = IdentifyBodyPartFromName(rootName);
                        
                        if (identifiedPart != BodyPart.Unknown)
                        {
                            result[identifiedPart].Add(renderer);
                            continue;
                        }
                    }
                    
                    // 最も影響の大きいボーンから推定
                    if (skinnedRenderer.sharedMesh != null && skinnedRenderer.sharedMesh.isReadable)
                    {
                        BoneWeight[] weights = skinnedRenderer.sharedMesh.boneWeights;
                        if (weights.Length > 0)
                        {
                            // ボーンウェイトの合計を計算
                            Dictionary<int, float> boneInfluence = new Dictionary<int, float>();
                            foreach (var weight in weights)
                            {
                                AddBoneWeight(boneInfluence, weight.boneIndex0, weight.weight0);
                                AddBoneWeight(boneInfluence, weight.boneIndex1, weight.weight1);
                                AddBoneWeight(boneInfluence, weight.boneIndex2, weight.weight2);
                                AddBoneWeight(boneInfluence, weight.boneIndex3, weight.weight3);
                            }
                            
                            // 最も影響の大きいボーンを特定
                            int maxInfluenceBoneIndex = -1;
                            float maxInfluence = 0f;
                            
                            foreach (var kvp in boneInfluence)
                            {
                                if (kvp.Value > maxInfluence)
                                {
                                    maxInfluence = kvp.Value;
                                    maxInfluenceBoneIndex = kvp.Key;
                                }
                            }
                            
                            if (maxInfluenceBoneIndex >= 0 && maxInfluenceBoneIndex < skinnedRenderer.bones.Length)
                            {
                                Transform bone = skinnedRenderer.bones[maxInfluenceBoneIndex];
                                if (bone != null)
                                {
                                    string boneName = bone.name.ToLower();
                                    BodyPart identifiedPart = IdentifyBodyPartFromName(boneName);
                                    
                                    if (identifiedPart != BodyPart.Unknown)
                                    {
                                        result[identifiedPart].Add(renderer);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 名前から部位を推定（レンダラーの名前）
                string name = renderer.name.ToLower();
                BodyPart identifiedPart = IdentifyBodyPartFromName(name);
                
                if (identifiedPart != BodyPart.Unknown)
                {
                    result[identifiedPart].Add(renderer);
                    continue;
                }
                
                // 親オブジェクトの名前からも推定
                if (renderer.transform.parent != null)
                {
                    string parentName = renderer.transform.parent.name.ToLower();
                    identifiedPart = IdentifyBodyPartFromName(parentName);
                    
                    if (identifiedPart != BodyPart.Unknown)
                    {
                        result[identifiedPart].Add(renderer);
                        continue;
                    }
                }
                
                // 分類できなかった場合はOtherに
                result[BodyPart.Other].Add(renderer);
            }
            
            return result;
        }
        
        /// <summary>
        /// ボーンウェイトを追加するヘルパーメソッド
        /// </summary>
        private static void AddBoneWeight(Dictionary<int, float> boneInfluence, int boneIndex, float weight)
        {
            if (weight <= 0f) return;
            
            if (!boneInfluence.ContainsKey(boneIndex))
            {
                boneInfluence[boneIndex] = weight;
            }
            else
            {
                boneInfluence[boneIndex] += weight;
            }
        }
        
        /// <summary>
        /// 名前から体の部位を特定
        /// </summary>
        private static BodyPart IdentifyBodyPartFromName(string name)
        {
            // 頭部
            if (name.Contains("head") || name.Contains("face") || name.Contains("hair") || 
                name.Contains("skull") || name.Contains("cranium"))
            {
                return BodyPart.Head;
            }
            // 首
            else if (name.Contains("neck"))
            {
                return BodyPart.Neck;
            }
            // 胸部
            else if (name.Contains("chest") || name.Contains("breast") || name.Contains("thorax") || 
                     name.Contains("pectoral") || name.Contains("bust"))
            {
                return BodyPart.Chest;
            }
            // 上胸部
            else if (name.Contains("upperchest") || name.Contains("upper_chest"))
            {
                return BodyPart.UpperChest;
            }
            // 背骨
            else if (name.Contains("spine") || name.Contains("back") || name.Contains("column"))
            {
                return BodyPart.Spine;
            }
            // 腰
            else if (name.Contains("hip") || name.Contains("pelvis") || name.Contains("waist"))
            {
                return BodyPart.Hips;
            }
            // 左肩
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     name.Contains("shoulder"))
            {
                return BodyPart.LeftShoulder;
            }
            // 右肩
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     name.Contains("shoulder"))
            {
                return BodyPart.RightShoulder;
            }
            // 左上腕
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     (name.Contains("arm") || name.Contains("upper") && name.Contains("arm")))
            {
                return BodyPart.LeftUpperArm;
            }
            // 右上腕
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     (name.Contains("arm") || name.Contains("upper") && name.Contains("arm")))
            {
                return BodyPart.RightUpperArm;
            }
            // 左下腕
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     (name.Contains("fore") || name.Contains("lower") && name.Contains("arm")))
            {
                return BodyPart.LeftLowerArm;
            }
            // 右下腕
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     (name.Contains("fore") || name.Contains("lower") && name.Contains("arm")))
            {
                return BodyPart.RightLowerArm;
            }
            // 左手
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     name.Contains("hand"))
            {
                return BodyPart.LeftHand;
            }
            // 右手
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     name.Contains("hand"))
            {
                return BodyPart.RightHand;
            }
            // 左上脚
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     (name.Contains("thigh") || name.Contains("upper") && (name.Contains("leg") || name.Contains("thigh"))))
            {
                return BodyPart.LeftUpperLeg;
            }
            // 右上脚
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     (name.Contains("thigh") || name.Contains("upper") && (name.Contains("leg") || name.Contains("thigh"))))
            {
                return BodyPart.RightUpperLeg;
            }
            // 左下脚
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     (name.Contains("calf") || name.Contains("shin") || name.Contains("lower") && name.Contains("leg")))
            {
                return BodyPart.LeftLowerLeg;
            }
            // 右下脚
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     (name.Contains("calf") || name.Contains("shin") || name.Contains("lower") && name.Contains("leg")))
            {
                return BodyPart.RightLowerLeg;
            }
            // 左足
            else if ((name.Contains("left") || name.Contains("l_") || name.StartsWith("l.") || name.EndsWith(".l") || name.EndsWith("_l")) && 
                     name.Contains("foot"))
            {
                return BodyPart.LeftFoot;
            }
            // 右足
            else if ((name.Contains("right") || name.Contains("r_") || name.StartsWith("r.") || name.EndsWith(".r") || name.EndsWith("_r")) && 
                     name.Contains("foot"))
            {
                return BodyPart.RightFoot;
            }
            
            // デフォルト
            return BodyPart.Unknown;
        }
        
        /// <summary>
        /// 上半身へのオフセットを適用
        /// </summary>
        private static void ApplyUpperBodyOffset(
            GameObject costumeObject, 
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers, 
            Vector3 offset)
        {
            // 上半身に関連する部位のリスト
            BodyPart[] upperBodyParts = new BodyPart[]
            {
                BodyPart.Head,
                BodyPart.Neck,
                BodyPart.Chest,
                BodyPart.UpperChest,
                BodyPart.Spine,
                BodyPart.LeftShoulder,
                BodyPart.LeftUpperArm,
                BodyPart.LeftLowerArm,
                BodyPart.LeftHand,
                BodyPart.RightShoulder,
                BodyPart.RightUpperArm,
                BodyPart.RightLowerArm,
                BodyPart.RightHand,
                BodyPart.LeftThumb,
                BodyPart.LeftIndex,
                BodyPart.LeftMiddle,
                BodyPart.LeftRing,
                BodyPart.LeftPinky,
                BodyPart.RightThumb,
                BodyPart.RightIndex,
                BodyPart.RightMiddle,
                BodyPart.RightRing,
                BodyPart.RightPinky
            };
            
            // 各部位に対してオフセットを適用
            foreach (var part in upperBodyParts)
            {
                if (bodyPartRenderers.ContainsKey(part))
                {
                    foreach (var renderer in bodyPartRenderers[part])
                    {
                        if (renderer.transform != null)
                        {
                            renderer.transform.position += offset;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 下半身へのオフセットを適用
        /// </summary>
        private static void ApplyLowerBodyOffset(
            GameObject costumeObject, 
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers, 
            Vector3 offset)
        {
            // 下半身に関連する部位のリスト
            BodyPart[] lowerBodyParts = new BodyPart[]
            {
                BodyPart.Hips,
                BodyPart.LeftUpperLeg,
                BodyPart.LeftLowerLeg,
                BodyPart.LeftFoot,
                BodyPart.LeftToes,
                BodyPart.RightUpperLeg,
                BodyPart.RightLowerLeg,
                BodyPart.RightFoot,
                BodyPart.RightToes
            };
            
            // 各部位に対してオフセットを適用
            foreach (var part in lowerBodyParts)
            {
                if (bodyPartRenderers.ContainsKey(part))
                {
                    foreach (var renderer in bodyPartRenderers[part])
                    {
                        if (renderer.transform != null)
                        {
                            renderer.transform.position += offset;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 腕のスケール調整を適用
        /// </summary>
        private static void ApplyArmScale(
            GameObject costumeObject, 
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers,
            bool isLeft,
            float scale)
        {
            // 腕に関連する部位のリスト
            BodyPart[] armParts;
            
            if (isLeft)
            {
                armParts = new BodyPart[]
                {
                    BodyPart.LeftShoulder,
                    BodyPart.LeftUpperArm,
                    BodyPart.LeftLowerArm,
                    BodyPart.LeftHand,
                    BodyPart.LeftThumb,
                    BodyPart.LeftIndex,
                    BodyPart.LeftMiddle,
                    BodyPart.LeftRing,
                    BodyPart.LeftPinky
                };
            }
            else
            {
                armParts = new BodyPart[]
                {
                    BodyPart.RightShoulder,
                    BodyPart.RightUpperArm,
                    BodyPart.RightLowerArm,
                    BodyPart.RightHand,
                    BodyPart.RightThumb,
                    BodyPart.RightIndex,
                    BodyPart.RightMiddle,
                    BodyPart.RightRing,
                    BodyPart.RightPinky
                };
            }
            
            // 各部位に対してスケールを適用
            foreach (var part in armParts)
            {
                if (bodyPartRenderers.ContainsKey(part))
                {
                    foreach (var renderer in bodyPartRenderers[part])
                    {
                        if (renderer.transform != null)
                        {
                            renderer.transform.localScale *= scale;
                            
                            // スキンメッシュレンダラーの場合、ボーンも調整
                            SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                            if (skinnedRenderer != null && skinnedRenderer.bones != null)
                            {
                                foreach (var bone in skinnedRenderer.bones)
                                {
                                    if (bone != null && IsArmBone(bone.name, isLeft))
                                    {
                                        bone.localScale = bone.localScale * scale;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// ボーン名が腕のボーンかどうかを判定
        /// </summary>
        private static bool IsArmBone(string boneName, bool isLeft)
        {
            string lowerName = boneName.ToLower();
            
            bool isLeftBone = lowerName.Contains("left") || lowerName.Contains("l_") || 
                             lowerName.StartsWith("l.") || lowerName.EndsWith(".l") || lowerName.EndsWith("_l");
                             
            bool isRightBone = lowerName.Contains("right") || lowerName.Contains("r_") || 
                              lowerName.StartsWith("r.") || lowerName.EndsWith(".r") || lowerName.EndsWith("_r");
                              
            bool isArmRelated = lowerName.Contains("arm") || lowerName.Contains("hand") || 
                               lowerName.Contains("shoulder") || lowerName.Contains("forearm") || 
                               lowerName.Contains("elbow") || lowerName.Contains("wrist") || 
                               lowerName.Contains("finger") || lowerName.Contains("thumb");
                               
            return isArmRelated && ((isLeft && isLeftBone) || (!isLeft && isRightBone));
        }
        
        /// <summary>
        /// 脚のスケール調整を適用
        /// </summary>
        private static void ApplyLegScale(
            GameObject costumeObject, 
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers,
            bool isLeft,
            float scale)
        {
            // 脚に関連する部位のリスト
            BodyPart[] legParts;
            
            if (isLeft)
            {
                legParts = new BodyPart[]
                {
                    BodyPart.LeftUpperLeg,
                    BodyPart.LeftLowerLeg,
                    BodyPart.LeftFoot,
                    BodyPart.LeftToes
                };
            }
            else
            {
                legParts = new BodyPart[]
                {
                    BodyPart.RightUpperLeg,
                    BodyPart.RightLowerLeg,
                    BodyPart.RightFoot,
                    BodyPart.RightToes
                };
            }
            
            // 各部位に対してスケールを適用
            foreach (var part in legParts)
            {
                if (bodyPartRenderers.ContainsKey(part))
                {
                    foreach (var renderer in bodyPartRenderers[part])
                    {
                        if (renderer.transform != null)
                        {
                            renderer.transform.localScale *= scale;
                            
                            // スキンメッシュレンダラーの場合、ボーンも調整
                            SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                            if (skinnedRenderer != null && skinnedRenderer.bones != null)
                            {
                                foreach (var bone in skinnedRenderer.bones)
                                {
                                    if (bone != null && IsLegBone(bone.name, isLeft))
                                    {
                                        bone.localScale = bone.localScale * scale;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// ボーン名が脚のボーンかどうかを判定
        /// </summary>
        private static bool IsLegBone(string boneName, bool isLeft)
        {
            string lowerName = boneName.ToLower();
            
            bool isLeftBone = lowerName.Contains("left") || lowerName.Contains("l_") || 
                             lowerName.StartsWith("l.") || lowerName.EndsWith(".l") || lowerName.EndsWith("_l");
                             
            bool isRightBone = lowerName.Contains("right") || lowerName.Contains("r_") || 
                              lowerName.StartsWith("r.") || lowerName.EndsWith(".r") || lowerName.EndsWith("_r");
                              
            bool isLegRelated = lowerName.Contains("leg") || lowerName.Contains("thigh") || 
                              lowerName.Contains("calf") || lowerName.Contains("shin") || 
                              lowerName.Contains("knee") || lowerName.Contains("ankle") || 
                              lowerName.Contains("foot") || lowerName.Contains("toe");
                              
            return isLegRelated && ((isLeft && isLeftBone) || (!isLeft && isRightBone));
        }
        
        /// <summary>
        /// カスタム部位調整を適用（改良版）
        /// </summary>
        private static void ApplyCustomBodyPartAdjustment(
            GameObject costumeObject,
            Dictionary<BodyPart, List<Renderer>> bodyPartRenderers,
            BodyPart bodyPart,
            BodyPartAdjustment adjustment)
        {
            if (!bodyPartRenderers.ContainsKey(bodyPart) || bodyPartRenderers[bodyPart].Count == 0)
            {
                return;
            }
            
            // スケールの適用
            Vector3 scale = adjustment.GetScaleVector();
            
            // オフセットの適用
            Vector3 offset = adjustment.GetOffsetVector();
            
            // 回転の適用
            Quaternion rotation = Quaternion.Euler(adjustment.rotation);
            
            // 部位のレンダラーに適用
            foreach (var renderer in bodyPartRenderers[bodyPart])
            {
                if (renderer.transform != null)
                {
                    // 元の回転と位置を保存
                    Quaternion originalRotation = renderer.transform.rotation;
                    Vector3 originalPosition = renderer.transform.position;
                    
                    // 回転を適用
                    if (adjustment.adjustRotation && adjustment.rotation != Vector3.zero)
                    {
                        renderer.transform.rotation = originalRotation * rotation;
                    }
                    
                    // スケールを適用
                    if (adjustment.adjustScale && scale != Vector3.one)
                    {
                        renderer.transform.localScale = Vector3.Scale(renderer.transform.localScale, scale);
                        
                        // スキンメッシュレンダラーの場合、ボーンも調整
                        SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                        if (skinnedRenderer != null && skinnedRenderer.bones != null)
                        {
                            foreach (var bone in skinnedRenderer.bones)
                            {
                                if (bone != null && IsBoneForBodyPart(bone.name, bodyPart))
                                {
                                    bone.localScale = Vector3.Scale(bone.localScale, scale);
                                }
                            }
                        }
                    }
                    
                    // オフセットを適用
                    if (adjustment.adjustPosition && offset != Vector3.zero)
                    {
                        renderer.transform.position += offset;
                    }
                }
            }
        }
        
        /// <summary>
        /// ボーン名が指定された体の部位に関連するかを判定
        /// </summary>
        private static bool IsBoneForBodyPart(string boneName, BodyPart bodyPart)
        {
            string name = boneName.ToLower();
            BodyPart identifiedPart = IdentifyBodyPartFromName(name);
            return identifiedPart == bodyPart;
        }
    }
}