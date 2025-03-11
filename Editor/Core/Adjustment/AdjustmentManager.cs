using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 衣装調整の全体管理を行うクラス
    /// </summary>
    public static class AdjustmentManager
    {
        // 調整中の衣装インスタンス
        private static GameObject costumeInstance;
        
        // 元の衣装データのバックアップ
        private static Dictionary<string, Vector3> originalPositions = new Dictionary<string, Vector3>();
        private static Dictionary<string, Quaternion> originalRotations = new Dictionary<string, Quaternion>();
        private static Dictionary<string, Vector3> originalScales = new Dictionary<string, Vector3>();
        
        // メッシュバックアップ (バインドポーズの復元用)
        private static Dictionary<string, Matrix4x4[]> originalBindPoses = new Dictionary<string, Matrix4x4[]>();
        private static Dictionary<string, BoneWeight[]> originalBoneWeights = new Dictionary<string, BoneWeight[]>();

        /// <summary>
        /// 衣装を適用する
        /// </summary>
        public static void ApplyCostume(
            GameObject avatarObject, 
            GameObject costumeObject, 
            MappingData mappingData, 
            AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeObject == null || mappingData == null || settings == null)
            {
                Debug.LogError("衣装適用に必要なオブジェクトが不足しています。");
                return;
            }
            
            // 既存の衣装インスタンスがあれば削除
            CleanupExistingCostumeInstance(avatarObject);
            
            // 新しい衣装インスタンスを作成
            costumeInstance = GameObject.Instantiate(costumeObject);
            costumeInstance.name = $"{costumeObject.name}_Instance";
            
            // 衣装インスタンスをアバターの子として配置
            costumeInstance.transform.SetParent(avatarObject.transform);
            costumeInstance.transform.localPosition = Vector3.zero;
            costumeInstance.transform.localRotation = Quaternion.identity;
            costumeInstance.transform.localScale = Vector3.one;
            
            // 元の状態をバックアップ
            BackupOriginalCostumeState(costumeInstance);
            
            try
            {
                // 調整方法に応じて処理を分岐
                if (settings.method == "BoneBased")
                {
                    BoneBasedAdjuster.ApplyAdjustment(avatarObject, costumeInstance, mappingData, settings);
                }
                else if (settings.method == "MeshBased")
                {
                    MeshBasedAdjuster.ApplyAdjustment(avatarObject, costumeInstance, mappingData, settings);
                }
                
                // 体の部位別調整の適用（全ての部位に適用）
                ApplyAllBodyPartAdjustments(avatarObject, settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"衣装適用中にエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
                
                // エラー発生時は元の状態に戻す
                RestoreOriginalCostumeState(costumeInstance);
                
                EditorUtility.DisplayDialog("エラー", 
                    $"衣装適用中にエラーが発生しました。\n{ex.Message}", "OK");
            }
            
            // エディタの更新を要求
            EditorUtility.SetDirty(avatarObject);
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 微調整を適用する
        /// </summary>
        public static void ApplyFineAdjustment(GameObject avatarObject, AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeInstance == null || settings == null)
            {
                Debug.LogError("微調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            FineAdjuster.ApplyAdjustment(avatarObject, costumeInstance, settings);
            
            // エディタの更新を要求
            EditorUtility.SetDirty(avatarObject);
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// すべての体の部位別調整を適用する
        /// </summary>
        private static void ApplyAllBodyPartAdjustments(GameObject avatarObject, AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeInstance == null || settings == null)
                return;
            
            // すべての有効な部位別調整を適用
            foreach (var kvp in settings.bodyPartAdjustments)
            {
                if (kvp.Value.isEnabled)
                {
                    BodyPartAdjuster.ApplyAdjustment(avatarObject, costumeInstance, kvp.Key, settings);
                }
            }
        }
        
        /// <summary>
        /// 部位別の調整を適用する
        /// </summary>
        public static void ApplyBodyPartAdjustment(GameObject avatarObject, BodyPart bodyPart, AdjustmentSettings settings)
        {
            if (avatarObject == null || costumeInstance == null || settings == null)
            {
                Debug.LogError("部位別調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            if (!settings.bodyPartAdjustments.ContainsKey(bodyPart))
            {
                Debug.LogError($"指定された体の部位 {bodyPart} の調整設定が見つかりません。");
                return;
            }
            
            BodyPartAdjuster.ApplyAdjustment(avatarObject, costumeInstance, bodyPart, settings);
            
            // エディタの更新を要求
            EditorUtility.SetDirty(avatarObject);
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 調整をリセットする
        /// </summary>
        public static void ResetAdjustment(GameObject avatarObject)
        {
            if (avatarObject == null || costumeInstance == null)
            {
                Debug.LogError("リセットに必要なオブジェクトが不足しています。");
                return;
            }
            
            // 元の状態に戻す
            RestoreOriginalCostumeState(costumeInstance);
            
            // エディタの更新を要求
            EditorUtility.SetDirty(avatarObject);
            SceneView.RepaintAll();
        }
        
        /// <summary>
        /// 既存の衣装インスタンスをクリーンアップ
        /// </summary>
        private static void CleanupExistingCostumeInstance(GameObject avatarObject)
        {
            if (costumeInstance != null)
            {
                // 現在のインスタンスを削除
                GameObject.DestroyImmediate(costumeInstance);
                costumeInstance = null;
            }
            
            // バックアップをクリア
            originalPositions.Clear();
            originalRotations.Clear();
            originalScales.Clear();
            originalBindPoses.Clear();
            originalBoneWeights.Clear();
            
            // アバターの子から "_Instance" が付くオブジェクトを検索して削除
            for (int i = avatarObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = avatarObject.transform.GetChild(i);
                if (child.name.EndsWith("_Instance"))
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
        }
        
        /// <summary>
        /// 衣装の元の状態をバックアップ
        /// </summary>
        private static void BackupOriginalCostumeState(GameObject costumeObject)
        {
            if (costumeObject == null) return;
            
            originalPositions.Clear();
            originalRotations.Clear();
            originalScales.Clear();
            originalBindPoses.Clear();
            originalBoneWeights.Clear();
            
            // すべてのTransformをバックアップ
            Transform[] transforms = costumeObject.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                string path = GetRelativePath(costumeObject.transform, t);
                originalPositions[path] = t.localPosition;
                originalRotations[path] = t.localRotation;
                originalScales[path] = t.localScale;
            }
            
            // スキンメッシュのバインドポーズもバックアップ
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.sharedMesh != null)
                {
                    string path = GetRelativePath(costumeObject.transform, renderer.transform);
                    originalBindPoses[path] = renderer.sharedMesh.bindposes.Clone() as Matrix4x4[];
                    
                    // ボーンウェイトもバックアップ（読み取り可能な場合のみ）
                    if (renderer.sharedMesh.isReadable)
                    {
                        originalBoneWeights[path] = renderer.sharedMesh.boneWeights.Clone() as BoneWeight[];
                    }
                }
            }
        }
        
        /// <summary>
        /// 衣装を元の状態に戻す
        /// </summary>
        private static void RestoreOriginalCostumeState(GameObject costumeObject)
        {
            if (costumeObject == null) return;
            
            // すべてのTransformを元に戻す
            Transform[] transforms = costumeObject.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                string path = GetRelativePath(costumeObject.transform, t);
                
                if (originalPositions.ContainsKey(path))
                    t.localPosition = originalPositions[path];
                    
                if (originalRotations.ContainsKey(path))
                    t.localRotation = originalRotations[path];
                    
                if (originalScales.ContainsKey(path))
                    t.localScale = originalScales[path];
            }
            
            // スキンメッシュのバインドポーズも元に戻す
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.sharedMesh != null)
                {
                    string path = GetRelativePath(costumeObject.transform, renderer.transform);
                    
                    if (originalBindPoses.ContainsKey(path))
                    {
                        // メッシュをコピーして元のバインドポーズを設定
                        Mesh originalMesh = renderer.sharedMesh;
                        Mesh restoreMesh = UnityEngine.Object.Instantiate(originalMesh);
                        restoreMesh.name = originalMesh.name + "_Restored";
                        restoreMesh.bindposes = originalBindPoses[path];
                        
                        // ボーンウェイトを元に戻す（読み取り可能な場合のみ）
                        if (restoreMesh.isReadable && originalBoneWeights.ContainsKey(path))
                        {
                            restoreMesh.boneWeights = originalBoneWeights[path];
                        }
                        
                        renderer.sharedMesh = restoreMesh;
                    }
                }
            }
        }
        
        /// <summary>
        /// 相対パスを取得
        /// </summary>
        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";
                
            if (target.parent == null)
                return "";
                
            if (target.parent == root)
                return target.name;
                
            return GetRelativePath(root, target.parent) + "/" + target.name;
        }
        
        /// <summary>
        /// 衣装インスタンスを取得
        /// </summary>
        public static GameObject GetCostumeInstance()
        {
            return costumeInstance;
        }
    }
}
