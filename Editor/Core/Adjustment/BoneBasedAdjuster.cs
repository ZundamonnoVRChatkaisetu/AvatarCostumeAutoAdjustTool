using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ボーンベースの衣装調整を行うクラス
    /// </summary>
    public static class BoneBasedAdjuster
    {
        /// <summary>
        /// ボーンベースの調整を適用
        /// </summary>
        public static void ApplyAdjustment(
            GameObject avatarObject,
            GameObject costumeObject,
            MappingData mappingData,
            AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeObject == null || 
                mappingData == null || settings == null)
            {
                Debug.LogError("ボーンベース調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            // アバターと衣装のボーン情報を収集
            var avatarBones = BoneIdentifier.AnalyzeAvatarBones(avatarObject);
            var costumeBones = BoneIdentifier.AnalyzeCostumeBones(costumeObject);
            
            // ボーン構成の違いを分析
            bool hasDifferentBoneStructure = AnalyzeBoneStructureDifferences(avatarBones, costumeBones, mappingData);
            
            // 1. スケルトン構造の調整
            AdjustSkeletonStructure(avatarObject, costumeObject, avatarBones, costumeBones, mappingData, settings);
            
            // 2. スキンメッシュの調整
            if (hasDifferentBoneStructure)
            {
                // 異なるボーン構造の場合は特殊な処理を適用
                Debug.Log("異なるボーン構造を検出したため、適応処理を行います。");
                BoneStructureAdapter.AdaptToDifferentBoneStructure(
                    avatarObject, costumeObject, mappingData, avatarBones, costumeBones);
            }
            else
            {
                // 通常のスキンメッシュ調整
                AdjustSkinnedMeshes(avatarObject, costumeObject, avatarBones, costumeBones, mappingData);
            }
            
            // 3. グローバル調整の適用
            ApplyGlobalAdjustments(costumeObject, settings);
            
            Debug.Log("ボーンベースの調整を適用しました。");
        }
        
        /// <summary>
        /// ボーン構造の違いを分析
        /// </summary>
        private static bool AnalyzeBoneStructureDifferences(
            List<BoneData> avatarBones, 
            List<BoneData> costumeBones, 
            MappingData mappingData)
        {
            if (avatarBones == null || costumeBones == null || mappingData == null)
                return false;
                
            // 重要なボーンのリスト
            List<BodyPart> importantParts = new List<BodyPart>
            {
                BodyPart.Head,
                BodyPart.Neck,
                BodyPart.Chest,
                BodyPart.Spine,
                BodyPart.Hips,
                BodyPart.LeftShoulder,
                BodyPart.RightShoulder,
                BodyPart.LeftUpperArm,
                BodyPart.RightUpperArm,
                BodyPart.LeftLowerArm,
                BodyPart.RightLowerArm,
                BodyPart.LeftUpperLeg,
                BodyPart.RightUpperLeg,
                BodyPart.LeftLowerLeg,
                BodyPart.RightLowerLeg
            };
            
            // 重要なパーツの存在の違いをチェック
            HashSet<BodyPart> avatarParts = new HashSet<BodyPart>(
                avatarBones.Where(b => b.bodyPart != BodyPart.Unknown && b.bodyPart != BodyPart.Other)
                           .Select(b => b.bodyPart));
                           
            HashSet<BodyPart> costumeParts = new HashSet<BodyPart>(
                costumeBones.Where(b => b.bodyPart != BodyPart.Unknown && b.bodyPart != BodyPart.Other)
                            .Select(b => b.bodyPart));
            
            // 重要なパーツの存在差をチェック
            foreach (var part in importantParts)
            {
                if (avatarParts.Contains(part) != costumeParts.Contains(part))
                {
                    return true; // 構造の違いを検出
                }
            }
            
            // ボーン階層の違いをチェック
            return HasHierarchyDifference(avatarBones, costumeBones, mappingData);
        }
        
        /// <summary>
        /// ボーン階層の違いを検出
        /// </summary>
        private static bool HasHierarchyDifference(
            List<BoneData> avatarBones, 
            List<BoneData> costumeBones, 
            MappingData mappingData)
        {
            // マッピングされたボーンの親子関係を比較
            foreach (var avatarBone in avatarBones)
            {
                BoneData costumeBone;
                float confidence;
                MappingMethod method;
                bool isManual;
                
                if (mappingData.GetCostumeBoneForAvatarBone(
                    avatarBone.id, out costumeBone, out confidence, out method, out isManual))
                {
                    // アバターと衣装の両方で親ボーンが存在する場合
                    if (avatarBone.parentId != null && costumeBone.parentId != null)
                    {
                        // 両方の親ボーンをマッピングから検索
                        BoneData avatarParentBone = avatarBones.FirstOrDefault(b => b.id == avatarBone.parentId);
                        BoneData costumeParentBone = costumeBones.FirstOrDefault(b => b.id == costumeBone.parentId);
                        
                        if (avatarParentBone != null && costumeParentBone != null)
                        {
                            // 親ボーン同士がマッピングされているかチェック
                            BoneData mappedParentCostumeBone;
                            float parentConfidence;
                            MappingMethod parentMethod;
                            bool parentIsManual;
                            
                            if (mappingData.GetCostumeBoneForAvatarBone(
                                avatarParentBone.id, out mappedParentCostumeBone, out parentConfidence, out parentMethod, out parentIsManual))
                            {
                                // 親同士のマッピングが一致しない場合は階層の違いあり
                                if (mappedParentCostumeBone.id != costumeParentBone.id)
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                // 親がマッピングされていない場合も階層の違いと判断
                                return true;
                            }
                        }
                    }
                    // 一方に親がなく、もう一方に親がある場合も階層の違い
                    else if (avatarBone.parentId != null || costumeBone.parentId != null)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// スケルトン構造の調整
        /// </summary>
        private static void AdjustSkeletonStructure(
            GameObject avatarObject,
            GameObject costumeObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData,
            AdjustmentSettings settings)
        {
            // ボーン参照の辞書を作成
            Dictionary<string, Transform> avatarBoneDict = new Dictionary<string, Transform>();
            Dictionary<string, Transform> costumeBoneDict = new Dictionary<string, Transform>();
            
            foreach (var bone in avatarBones)
            {
                if (bone.transform != null)
                {
                    avatarBoneDict[bone.id] = bone.transform;
                }
            }
            
            foreach (var bone in costumeBones)
            {
                if (bone.transform != null)
                {
                    costumeBoneDict[bone.id] = bone.transform;
                }
            }
            
            // 衣装のルートボーンを特定
            Transform costumeRoot = costumeObject.transform;
            BoneData costumeRootBone = costumeBones.FirstOrDefault(b => b.isRoot && b.transform == costumeRoot);
            
            if (costumeRootBone != null)
            {
                // ルートボーンに対応するアバターボーンを探す
                BoneData avatarRootBone = null;
                float confidence = 0f;
                MappingMethod method = MappingMethod.NotMapped;
                bool isManuallyMapped = false;
                
                mappingData.GetAvatarBoneForCostumeBone(costumeRootBone.id, out avatarRootBone, out confidence, out method, out isManuallyMapped);
                
                // ルートボーンがマッピングされていない場合はヒップボーンを試す
                if (avatarRootBone == null)
                {
                    avatarRootBone = avatarBones.FirstOrDefault(b => b.bodyPart == BodyPart.Hips);
                }
                
                // ルートの位置合わせ
                if (avatarRootBone != null && avatarRootBone.transform != null)
                {
                    // ルートボーンの位置とスケールのみ調整（回転はアバターの姿勢に依存するため）
                    costumeRoot.position = avatarRootBone.transform.position;
                }
            }
            
            // 各ボーンのマッピングに基づいて位置を調整
            foreach (var costumeBone in costumeBones)
            {
                // ルートボーンは既に調整済み
                if (costumeBone.isRoot && costumeBone.transform == costumeRoot)
                {
                    continue;
                }
                
                // 対応するアバターボーンを取得
                BoneData avatarBone = null;
                float confidence = 0f;
                MappingMethod method = MappingMethod.NotMapped;
                bool isManuallyMapped = false;
                
                bool hasMapping = mappingData.GetAvatarBoneForCostumeBone(
                    costumeBone.id, out avatarBone, out confidence, out method, out isManuallyMapped);
                
                if (hasMapping && avatarBone != null && avatarBone.transform != null && costumeBone.transform != null)
                {
                    // 親子関係が維持されるように相対的に位置合わせ
                    if (costumeBone.transform.parent != null)
                    {
                        // 親ボーンに対する相対位置を維持
                        Transform costumeParent = costumeBone.transform.parent;
                        
                        // 親ボーンがアバターに対応付けられているかチェック
                        BoneData costumeParentBone = costumeBones.FirstOrDefault(b => b.transform == costumeParent);
                        
                        if (costumeParentBone != null)
                        {
                            BoneData avatarParentBone = null;
                            float parentConfidence = 0f;
                            MappingMethod parentMethod = MappingMethod.NotMapped;
                            bool parentIsManuallyMapped = false;
                            
                            bool hasParentMapping = mappingData.GetAvatarBoneForCostumeBone(
                                costumeParentBone.id, out avatarParentBone, out parentConfidence, out parentMethod, out parentIsManuallyMapped);
                            
                            if (hasParentMapping && avatarParentBone != null && avatarParentBone.transform != null)
                            {
                                // 親ボーンが対応付けられている場合、親子関係を維持しつつ調整
                                // 相対的な位置関係を維持するために親からの相対位置を計算
                                Vector3 localPosInParent = costumeBone.transform.localPosition;
                                Quaternion localRotInParent = costumeBone.transform.localRotation;
                                Vector3 localScaleInParent = costumeBone.transform.localScale;
                                
                                // アバターのボーン位置に移動し、相対的な関係を維持
                                costumeBone.transform.position = avatarBone.transform.position;
                                
                                // スケールと回転の調整（必要に応じて）
                                if (settings != null && settings.adjustRotation)
                                {
                                    costumeBone.transform.rotation = avatarBone.transform.rotation;
                                }
                                
                                if (settings != null && settings.adjustScale)
                                {
                                    // アバターと衣装のスケール比を計算
                                    float scaleRatio = avatarBone.transform.lossyScale.magnitude / 
                                                      costumeBone.transform.lossyScale.magnitude;
                                    costumeBone.transform.localScale = localScaleInParent * scaleRatio;
                                }
                            }
                            else
                            {
                                // 親が対応付けられていない場合は直接位置合わせ
                                costumeBone.transform.position = avatarBone.transform.position;
                            }
                        }
                        else
                        {
                            // 親ボーンが識別できない場合は直接位置合わせ
                            costumeBone.transform.position = avatarBone.transform.position;
                        }
                    }
                    else
                    {
                        // 親がない場合は直接位置合わせ
                        costumeBone.transform.position = avatarBone.transform.position;
                    }
                }
            }
        }
        
        /// <summary>
        /// スキンメッシュの調整
        /// </summary>
        private static void AdjustSkinnedMeshes(
            GameObject avatarObject,
            GameObject costumeObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData)
        {
            // 衣装内のすべてのスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                AdjustSkinnedMeshRenderer(renderer, avatarObject, avatarBones, costumeBones, mappingData);
            }
        }
        
        /// <summary>
        /// スキンメッシュレンダラーの調整
        /// </summary>
        private static void AdjustSkinnedMeshRenderer(
            SkinnedMeshRenderer renderer,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                return;
            }
            
            // 元のボーン配列を取得
            Transform[] bones = renderer.bones;
            Transform rootBone = renderer.rootBone;
            
            if (bones == null || bones.Length == 0)
            {
                return;
            }
            
            // 新しいボーン配列
            Transform[] newBones = new Transform[bones.Length];
            Transform newRootBone = null;
            
            // ルートボーンの調整
            if (rootBone != null)
            {
                // 対応するボーンデータを探す
                BoneData costumeRootBoneData = costumeBones.FirstOrDefault(b => b.transform == rootBone);
                
                if (costumeRootBoneData != null)
                {
                    // マッピングからアバターボーンを取得
                    BoneData avatarRootBoneData = null;
                    float confidence = 0f;
                    MappingMethod method = MappingMethod.NotMapped;
                    bool isManuallyMapped = false;
                    
                    bool hasMapping = mappingData.GetAvatarBoneForCostumeBone(
                        costumeRootBoneData.id, out avatarRootBoneData, out confidence, out method, out isManuallyMapped);
                    
                    if (hasMapping && avatarRootBoneData != null && avatarRootBoneData.transform != null)
                    {
                        newRootBone = avatarRootBoneData.transform;
                    }
                    else
                    {
                        // マッピングがない場合はヒップボーンを試す
                        var hipBone = avatarBones.FirstOrDefault(b => b.bodyPart == BodyPart.Hips);
                        if (hipBone != null && hipBone.transform != null)
                        {
                            newRootBone = hipBone.transform;
                        }
                        else
                        {
                            // それでも見つからない場合はアバターのルートを使用
                            newRootBone = avatarObject.transform;
                        }
                    }
                }
                else
                {
                    // 衣装のルートボーンがボーンデータに見つからない場合
                    newRootBone = avatarObject.transform;
                }
            }
            else
            {
                // ルートボーンがない場合はアバターのルートを使用
                newRootBone = avatarObject.transform;
            }
            
            // 各ボーンの調整
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                
                if (bone == null)
                {
                    newBones[i] = null;
                    continue;
                }
                
                // 対応するボーンデータを探す
                BoneData costumeBoneData = costumeBones.FirstOrDefault(b => b.transform == bone);
                
                if (costumeBoneData != null)
                {
                    // マッピングからアバターボーンを取得
                    BoneData avatarBoneData = null;
                    float confidence = 0f;
                    MappingMethod method = MappingMethod.NotMapped;
                    bool isManuallyMapped = false;
                    
                    bool hasMapping = mappingData.GetAvatarBoneForCostumeBone(
                        costumeBoneData.id, out avatarBoneData, out confidence, out method, out isManuallyMapped);
                    
                    if (hasMapping && avatarBoneData != null && avatarBoneData.transform != null)
                    {
                        newBones[i] = avatarBoneData.transform;
                    }
                    else
                    {
                        // マッピングがない場合は元のボーンを使用
                        newBones[i] = bone;
                    }
                }
                else
                {
                    // ボーンデータに見つからない場合は元のボーンを使用
                    newBones[i] = bone;
                }
            }
            
            // 変更を適用
            renderer.rootBone = newRootBone;
            renderer.bones = newBones;
            
            // NOTE: バインドポーズの更新は現在BoneStructureAdapter.csで行います
        }
        
        /// <summary>
        /// グローバル調整を適用
        /// </summary>
        private static void ApplyGlobalAdjustments(GameObject costumeObject, AdjustmentSettings settings)
        {
            if (costumeObject == null || settings == null)
            {
                return;
            }
            
            // グローバルスケールの適用
            costumeObject.transform.localScale = Vector3.one * settings.globalScale;
            
            // 体の部位別調整の適用
            ApplyBodyPartAdjustments(costumeObject, settings);
        }

        /// <summary>
        /// 体の部位別調整を適用
        /// </summary>
        private static void ApplyBodyPartAdjustments(GameObject costumeObject, AdjustmentSettings settings)
        {
            if (settings.bodyPartAdjustments == null || settings.bodyPartAdjustments.Count == 0)
                return;

            // 衣装内のすべてのスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null)
                    continue;
                
                // 各部位調整を適用
                foreach (var kvp in settings.bodyPartAdjustments)
                {
                    // 部位に対応するボーンを特定
                    Transform targetBone = FindBoneForBodyPart(renderer, kvp.Key);
                    BodyPartAdjustment adjustment = kvp.Value;
                    
                    if (targetBone != null)
                    {
                        // スケール調整
                        if (adjustment.adjustScale)
                        {
                            targetBone.localScale = Vector3.Scale(targetBone.localScale, adjustment.scaleMultiplier);
                        }
                        
                        // オフセット調整
                        if (adjustment.adjustPosition)
                        {
                            targetBone.position += adjustment.positionOffset;
                        }
                        
                        // 回転調整
                        if (adjustment.adjustRotation)
                        {
                            targetBone.rotation *= Quaternion.Euler(adjustment.rotationOffset);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 指定された体の部位に対応するボーンを検索
        /// </summary>
        private static Transform FindBoneForBodyPart(SkinnedMeshRenderer renderer, BodyPart bodyPart)
        {
            // この実装はプロジェクトの具体的なボーン命名に依存します
            // 実際のプロジェクトでは必要に応じて拡張してください
            string[] possibleBoneNames = GetPossibleBoneNamesForBodyPart(bodyPart);
            
            if (possibleBoneNames.Length == 0)
                return null;
                
            foreach (var boneName in possibleBoneNames)
            {
                foreach (var bone in renderer.bones)
                {
                    if (bone != null && bone.name.ToLowerInvariant().Contains(boneName.ToLowerInvariant()))
                    {
                        return bone;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 体の部位に対応する可能性のあるボーン名を取得
        /// </summary>
        private static string[] GetPossibleBoneNamesForBodyPart(BodyPart bodyPart)
        {
            switch (bodyPart)
            {
                case BodyPart.Head:
                    return new[] { "head", "face", "skull" };
                case BodyPart.Neck:
                    return new[] { "neck" };
                case BodyPart.Chest:
                    return new[] { "chest", "upper_chest", "thorax", "upper_torso" };
                case BodyPart.Spine:
                    return new[] { "spine", "back", "backbone" };
                case BodyPart.Hips:
                    return new[] { "hips", "pelvis", "hip" };
                case BodyPart.LeftShoulder:
                    return new[] { "l_shoulder", "left_shoulder", "l_clavicle", "left_clavicle" };
                case BodyPart.RightShoulder:
                    return new[] { "r_shoulder", "right_shoulder", "r_clavicle", "right_clavicle" };
                case BodyPart.LeftUpperArm:
                    return new[] { "l_upper_arm", "left_upper_arm", "l_arm", "left_arm" };
                case BodyPart.RightUpperArm:
                    return new[] { "r_upper_arm", "right_upper_arm", "r_arm", "right_arm" };
                case BodyPart.LeftLowerArm:
                    return new[] { "l_lower_arm", "left_lower_arm", "l_forearm", "left_forearm" };
                case BodyPart.RightLowerArm:
                    return new[] { "r_lower_arm", "right_lower_arm", "r_forearm", "right_forearm" };
                case BodyPart.LeftHand:
                    return new[] { "l_hand", "left_hand" };
                case BodyPart.RightHand:
                    return new[] { "r_hand", "right_hand" };
                case BodyPart.LeftUpperLeg:
                    return new[] { "l_upper_leg", "left_upper_leg", "l_thigh", "left_thigh" };
                case BodyPart.RightUpperLeg:
                    return new[] { "r_upper_leg", "right_upper_leg", "r_thigh", "right_thigh" };
                case BodyPart.LeftLowerLeg:
                    return new[] { "l_lower_leg", "left_lower_leg", "l_calf", "left_calf", "l_shin", "left_shin" };
                case BodyPart.RightLowerLeg:
                    return new[] { "r_lower_leg", "right_lower_leg", "r_calf", "right_calf", "r_shin", "right_shin" };
                case BodyPart.LeftFoot:
                    return new[] { "l_foot", "left_foot" };
                case BodyPart.RightFoot:
                    return new[] { "r_foot", "right_foot" };
                case BodyPart.Unknown:
                case BodyPart.Other:
                default:
                    return new string[0];
            }
        }
    }
}
