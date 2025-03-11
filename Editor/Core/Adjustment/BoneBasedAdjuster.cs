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
            
            // 1. スケルトン構造の調整
            AdjustSkeletonStructure(avatarObject, costumeObject, avatarBones, costumeBones, mappingData);
            
            // 2. スキンメッシュの調整
            AdjustSkinnedMeshes(avatarObject, costumeObject, avatarBones, costumeBones, mappingData);
            
            // 3. グローバル調整の適用
            ApplyGlobalAdjustments(costumeObject, settings);
            
            Debug.Log("ボーンベースの調整を適用しました。");
        }
        
        /// <summary>
        /// スケルトン構造の調整
        /// </summary>
        private static void AdjustSkeletonStructure(
            GameObject avatarObject,
            GameObject costumeObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData)
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
                                // 相対的な位置関係を維持
                                Vector3 localPosInParent = costumeBone.localPosition;
                                costumeBone.transform.position = avatarBone.transform.position;
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
            
            // バインドポーズの更新は現在サポートしていない
            // 今後の機能追加で必要に応じて実装予定
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
            
            // TODO: 上半身/下半身のオフセットや腕/脚のスケール調整も実装予定
        }
    }
}
