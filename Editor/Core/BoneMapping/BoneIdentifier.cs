using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// ボーン識別を行うユーティリティクラス
    /// </summary>
    public static class BoneIdentifier
    {
        // キャッシュ
        private static Dictionary<int, List<BoneData>> analyzedAvatars = new Dictionary<int, List<BoneData>>();
        private static Dictionary<int, List<BoneData>> analyzedCostumes = new Dictionary<int, List<BoneData>>();
        
        /// <summary>
        /// アバターのボーン構造を解析
        /// </summary>
        public static List<BoneData> AnalyzeAvatarBones(GameObject avatarObject)
        {
            if (avatarObject == null) return new List<BoneData>();
            
            int avatarId = avatarObject.GetInstanceID();
            
            // キャッシュがあれば使用
            if (analyzedAvatars.TryGetValue(avatarId, out var cachedBones))
            {
                // アバターが変更されていないかチェック
                if (IsAvatarCacheValid(avatarObject, cachedBones))
                {
                    return cachedBones;
                }
            }
            
            // 新しく解析
            var bones = AnalyzeBones(avatarObject, true);
            
            // キャッシュに保存
            analyzedAvatars[avatarId] = bones;
            
            return bones;
        }
        
        /// <summary>
        /// 衣装のボーン構造を解析
        /// </summary>
        public static List<BoneData> AnalyzeCostumeBones(GameObject costumeObject)
        {
            if (costumeObject == null) return new List<BoneData>();
            
            int costumeId = costumeObject.GetInstanceID();
            
            // キャッシュがあれば使用
            if (analyzedCostumes.TryGetValue(costumeId, out var cachedBones))
            {
                // 衣装が変更されていないかチェック
                if (IsCostumeCacheValid(costumeObject, cachedBones))
                {
                    return cachedBones;
                }
            }
            
            // 新しく解析
            var bones = AnalyzeBones(costumeObject, false);
            
            // キャッシュに保存
            analyzedCostumes[costumeId] = bones;
            
            return bones;
        }
        
        /// <summary>
        /// アバターのキャッシュが有効かチェック
        /// </summary>
        private static bool IsAvatarCacheValid(GameObject avatarObject, List<BoneData> cachedBones)
        {
            // 簡易チェック: Transform数が同じかどうか
            int transformCount = CountTransforms(avatarObject.transform);
            int cachedBoneCount = cachedBones.Count;
            
            if (transformCount != cachedBoneCount)
            {
                return false;
            }
            
            // ルートボーンのチェック
            Animator animator = avatarObject.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    var rootBone = cachedBones.FirstOrDefault(b => b.isRoot);
                    if (rootBone == null || rootBone.name != hips.name)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 衣装のキャッシュが有効かチェック
        /// </summary>
        private static bool IsCostumeCacheValid(GameObject costumeObject, List<BoneData> cachedBones)
        {
            // 簡易チェック: Transform数が同じかどうか
            int transformCount = CountTransforms(costumeObject.transform);
            int cachedBoneCount = cachedBones.Count;
            
            if (transformCount != cachedBoneCount)
            {
                return false;
            }
            
            // メッシュ構造のチェック
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            int rendererCount = renderers.Length;
            
            if (rendererCount == 0)
            {
                return true; // メッシュがない場合は単純なボーン構造として扱う
            }
            
            // ルートボーンのチェック（最初のレンダラーのみ）
            if (renderers[0].rootBone != null)
            {
                var rootBone = cachedBones.FirstOrDefault(b => b.isRoot);
                if (rootBone == null || rootBone.name != renderers[0].rootBone.name)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Transformの総数をカウント
        /// </summary>
        private static int CountTransforms(Transform root)
        {
            int count = 1; // 自分自身
            
            for (int i = 0; i < root.childCount; i++)
            {
                count += CountTransforms(root.GetChild(i));
            }
            
            return count;
        }
        
        /// <summary>
        /// ボーン構造を解析して情報リストを作成
        /// </summary>
        private static List<BoneData> AnalyzeBones(GameObject targetObject, bool isAvatar)
        {
            List<BoneData> result = new List<BoneData>();
            Dictionary<Transform, BoneData> transformToBone = new Dictionary<Transform, BoneData>();
            
            // ヒューマノイドボーン情報の取得（アバターの場合）
            Dictionary<string, BoneData> humanoidBones = new Dictionary<string, BoneData>();
            
            if (isAvatar)
            {
                Animator animator = targetObject.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    humanoidBones = BoneData.GetHumanoidBones(animator);
                }
            }
            
            // スキンメッシュ関連のボーン収集
            CollectSkinnedMeshBones(targetObject, result, transformToBone);
            
            // 残りのTransformを収集
            CollectRemainingTransforms(targetObject.transform, null, result, transformToBone, isAvatar);
            
            // 親子関係の設定
            SetupParentChildRelationships(result, transformToBone);
            
            // 体の部位の推定
            EstimateBodyParts(result, humanoidBones);
            
            // ヒューマノイド情報の統合
            if (humanoidBones.Count > 0)
            {
                MergeHumanoidBoneInfo(result, humanoidBones, transformToBone);
            }
            
            return result;
        }
        
        /// <summary>
        /// スキンメッシュレンダラーからボーン情報を収集
        /// </summary>
        private static void CollectSkinnedMeshBones(GameObject targetObject, List<BoneData> boneList, Dictionary<Transform, BoneData> transformToBone)
        {
            SkinnedMeshRenderer[] renderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                // ルートボーン
                if (renderer.rootBone != null)
                {
                    if (!transformToBone.ContainsKey(renderer.rootBone))
                    {
                        BoneData boneData = new BoneData(renderer.rootBone);
                        boneData.isRoot = true;
                        boneList.Add(boneData);
                        transformToBone[renderer.rootBone] = boneData;
                    }
                }
                
                // ボーン配列
                if (renderer.bones != null)
                {
                    foreach (var bone in renderer.bones)
                    {
                        if (bone != null && !transformToBone.ContainsKey(bone))
                        {
                            BoneData boneData = new BoneData(bone);
                            boneList.Add(boneData);
                            transformToBone[bone] = boneData;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 残りのTransformをボーン情報として収集
        /// </summary>
        private static void CollectRemainingTransforms(Transform current, string parentId, List<BoneData> boneList, Dictionary<Transform, BoneData> transformToBone, bool isAvatar)
        {
            // すでに処理済みならスキップ
            if (transformToBone.ContainsKey(current))
            {
                // 親IDの設定だけ行う
                if (parentId != null)
                {
                    transformToBone[current].parentId = parentId;
                }
                
                // 子の処理
                string currentId = transformToBone[current].id;
                for (int i = 0; i < current.childCount; i++)
                {
                    CollectRemainingTransforms(current.GetChild(i), currentId, boneList, transformToBone, isAvatar);
                }
                
                return;
            }
            
            // Mesh, SkinnedMeshRenderer, Collider, Renderer を持つオブジェクトはスキップ（ボーンではない）
            if (!isAvatar && 
                (current.GetComponent<MeshFilter>() != null || 
                 current.GetComponent<SkinnedMeshRenderer>() != null ||
                 current.GetComponent<Collider>() != null || 
                 (current.GetComponent<Renderer>() != null && !(current.GetComponent<Renderer>() is SkinnedMeshRenderer))))
            {
                // ボーンではないオブジェクトの子も処理
                for (int i = 0; i < current.childCount; i++)
                {
                    CollectRemainingTransforms(current.GetChild(i), parentId, boneList, transformToBone, isAvatar);
                }
                
                return;
            }
            
            // 新しいボーンデータを作成
            BoneData boneData = new BoneData(current, parentId);
            boneList.Add(boneData);
            transformToBone[current] = boneData;
            
            // 子の処理
            for (int i = 0; i < current.childCount; i++)
            {
                CollectRemainingTransforms(current.GetChild(i), boneData.id, boneList, transformToBone, isAvatar);
            }
        }
        
        /// <summary>
        /// 親子関係の設定
        /// </summary>
        private static void SetupParentChildRelationships(List<BoneData> boneList, Dictionary<Transform, BoneData> transformToBone)
        {
            // 親子関係の設定
            foreach (var bone in boneList)
            {
                if (!string.IsNullOrEmpty(bone.parentId))
                {
                    var parent = boneList.FirstOrDefault(b => b.id == bone.parentId);
                    if (parent != null && !parent.childrenIds.Contains(bone.id))
                    {
                        parent.childrenIds.Add(bone.id);
                    }
                }
            }
        }
        
        /// <summary>
        /// 体の部位の推定
        /// </summary>
        private static void EstimateBodyParts(List<BoneData> boneList, Dictionary<string, BoneData> humanoidBones)
        {
            foreach (var bone in boneList)
            {
                // まだ体の部位が未設定の場合
                if (bone.bodyPart == BodyPart.Other)
                {
                    // ヒューマノイドボーンとの照合
                    bool foundInHumanoid = false;
                    
                    foreach (var humanoid in humanoidBones.Values)
                    {
                        if (bone.IsSameBone(humanoid))
                        {
                            bone.bodyPart = humanoid.bodyPart;
                            bone.isHumanoid = true;
                            bone.humanoidName = humanoid.humanoidName;
                            foundInHumanoid = true;
                            break;
                        }
                    }
                    
                    // ヒューマノイドボーンで見つからなかった場合は名前から推定
                    if (!foundInHumanoid)
                    {
                        bone.bodyPart = BoneData.EstimateBodyPart(bone.name);
                    }
                }
            }
        }
        
        /// <summary>
        /// ヒューマノイドボーン情報の統合
        /// </summary>
        private static void MergeHumanoidBoneInfo(List<BoneData> boneList, Dictionary<string, BoneData> humanoidBones, Dictionary<Transform, BoneData> transformToBone)
        {
            foreach (var humanoidEntry in humanoidBones)
            {
                string humanoidName = humanoidEntry.Key;
                BoneData humanoidBone = humanoidEntry.Value;
                
                if (humanoidBone.transform == null) continue;
                
                // 対応するボーンを探す
                if (transformToBone.TryGetValue(humanoidBone.transform, out BoneData existingBone))
                {
                    // ヒューマノイド情報を統合
                    existingBone.isHumanoid = true;
                    existingBone.humanoidName = humanoidName;
                    existingBone.bodyPart = humanoidBone.bodyPart;
                }
            }
        }
    }
}
