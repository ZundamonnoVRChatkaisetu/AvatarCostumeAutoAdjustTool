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
            
            // 調整方法に応じて処理を分岐
            if (settings.method == "BoneBased")
            {
                BoneBasedAdjuster.ApplyAdjustment(avatarObject, costumeInstance, mappingData, settings);
            }
            else if (settings.method == "MeshBased")
            {
                MeshBasedAdjuster.ApplyAdjustment(avatarObject, costumeInstance, mappingData, settings);
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
            
            // すべてのTransformをバックアップ
            Transform[] transforms = costumeObject.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                string path = GetRelativePath(costumeObject.transform, t);
                originalPositions[path] = t.localPosition;
                originalRotations[path] = t.localRotation;
                originalScales[path] = t.localScale;
            }
            
            // スキンメッシュのバインドポーズもバックアップ（必要に応じて）
            // ここに実装予定
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
            
            // スキンメッシュのバインドポーズも元に戻す（必要に応じて）
            // ここに実装予定
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
    }
}
