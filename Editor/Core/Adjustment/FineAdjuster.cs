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
        /// <summary>
        /// 微調整を適用する
        /// </summary>
        public static void ApplyAdjustment(
            GameObject avatarObject,
            GameObject costumeObject,
            AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeObject == null || settings == null)
            {
                Debug.LogError("微調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            // 1. グローバルスケールの適用
            costumeObject.transform.localScale = Vector3.one * settings.globalScale;
            
            // 2. 上半身/下半身の部位ごとの調整
            ApplyBodyPartAdjustments(costumeObject, settings);
            
            Debug.Log("微調整を適用しました。");
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
            
            // 簡易実装: 今回はTransformの名前から推定
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
                else if (name.Contains("left") && (name.Contains("arm") || name.Contains("shoulder")))
                {
                    identifiedPart = BodyPart.LeftUpperArm;
                }
                else if (name.Contains("right") && (name.Contains("arm") || name.Contains("shoulder")))
                {
                    identifiedPart = BodyPart.RightUpperArm;
                }
                else if (name.Contains("left") && (name.Contains("fore") || name.Contains("lower") && name.Contains("arm")))
                {
                    identifiedPart = BodyPart.LeftLowerArm;
                }
                else if (name.Contains("right") && (name.Contains("fore") || name.Contains("lower") && name.Contains("arm")))
                {
                    identifiedPart = BodyPart.RightLowerArm;
                }
                else if (name.Contains("left") && name.Contains("hand"))
                {
                    identifiedPart = BodyPart.LeftHand;
                }
                else if (name.Contains("right") && name.Contains("hand"))
                {
                    identifiedPart = BodyPart.RightHand;
                }
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
                else if (name.Contains("left") && name.Contains("foot"))
                {
                    identifiedPart = BodyPart.LeftFoot;
                }
                else if (name.Contains("right") && name.Contains("foot"))
                {
                    identifiedPart = BodyPart.RightFoot;
                }
                
                result[identifiedPart].Add(renderer);
            }
            
            return result;
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
                        }
                    }
                }
            }
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
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// カスタム部位調整を適用
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
                    if (adjustment.rotation != Vector3.zero)
                    {
                        renderer.transform.rotation = originalRotation * rotation;
                    }
                    
                    // スケールを適用
                    if (scale != Vector3.one)
                    {
                        renderer.transform.localScale = Vector3.Scale(renderer.transform.localScale, scale);
                    }
                    
                    // オフセットを適用
                    if (offset != Vector3.zero)
                    {
                        renderer.transform.position += offset;
                    }
                }
            }
        }
    }
}
