using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// メッシュベースの衣装調整を行うクラス
    /// </summary>
    public static class MeshBasedAdjuster
    {
        /// <summary>
        /// メッシュベースの調整を適用
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
                Debug.LogError("メッシュベース調整に必要なオブジェクトが不足しています。");
                return;
            }
            
            // アバターと衣装のボーン情報を収集
            var avatarBones = BoneIdentifier.AnalyzeAvatarBones(avatarObject);
            var costumeBones = BoneIdentifier.AnalyzeCostumeBones(costumeObject);
            
            // 1. アバターと衣装のメッシュ情報を収集
            var avatarMeshes = CollectMeshes(avatarObject);
            var costumeMeshes = CollectMeshes(costumeObject);
            
            if (avatarMeshes.Count == 0 || costumeMeshes.Count == 0)
            {
                Debug.LogError("メッシュ情報の収集に失敗しました。");
                return;
            }
            
            // 2. メッシュの部位分類
            Dictionary<BodyPart, List<MeshInfo>> avatarBodyPartMeshes = ClassifyMeshesByBodyPart(avatarMeshes, avatarBones);
            Dictionary<BodyPart, List<MeshInfo>> costumeBodyPartMeshes = ClassifyMeshesByBodyPart(costumeMeshes, costumeBones);
            
            // 3. 部位ごとのメッシュ変形
            foreach (var bodyPart in costumeBodyPartMeshes.Keys)
            {
                // アバターに対応する部位のメッシュがある場合
                if (avatarBodyPartMeshes.ContainsKey(bodyPart) && 
                    avatarBodyPartMeshes[bodyPart].Count > 0 &&
                    costumeBodyPartMeshes[bodyPart].Count > 0)
                {
                    DeformMeshes(
                        avatarBodyPartMeshes[bodyPart],
                        costumeBodyPartMeshes[bodyPart],
                        bodyPart,
                        settings
                    );
                }
            }
            
            // 4. グローバル調整の適用
            ApplyGlobalAdjustments(costumeObject, settings);
            
            Debug.Log("メッシュベースの調整を適用しました。");
        }
        
        /// <summary>
        /// メッシュ情報を収集
        /// </summary>
        private static List<MeshInfo> CollectMeshes(GameObject targetObject)
        {
            List<MeshInfo> result = new List<MeshInfo>();
            
            // スキンメッシュレンダラーを収集
            SkinnedMeshRenderer[] skinnedRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.sharedMesh != null)
                {
                    result.Add(new MeshInfo
                    {
                        transform = renderer.transform,
                        mesh = renderer.sharedMesh,
                        renderer = renderer,
                        isSkinned = true
                    });
                }
            }
            
            // 通常メッシュレンダラーを収集
            MeshRenderer[] meshRenderers = targetObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    result.Add(new MeshInfo
                    {
                        transform = renderer.transform,
                        mesh = filter.sharedMesh,
                        meshFilter = filter,
                        renderer = renderer,
                        isSkinned = false
                    });
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// メッシュを体の部位ごとに分類
        /// </summary>
        private static Dictionary<BodyPart, List<MeshInfo>> ClassifyMeshesByBodyPart(
            List<MeshInfo> meshes, 
            List<BoneData> bones)
        {
            Dictionary<BodyPart, List<MeshInfo>> result = new Dictionary<BodyPart, List<MeshInfo>>();
            
            // 部位ごとのリストを初期化
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                result[part] = new List<MeshInfo>();
            }
            
            // スキンメッシュの場合は関連するボーンから部位を推定
            foreach (var meshInfo in meshes)
            {
                if (meshInfo.isSkinned && meshInfo.renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    // ウェイトが最も高いボーンから部位を推定
                    Dictionary<BodyPart, float> partWeights = EstimateBodyPartWeights(skinnedRenderer, bones);
                    
                    if (partWeights.Count > 0)
                    {
                        // 最も重みの大きい部位を選択
                        BodyPart primaryPart = partWeights.OrderByDescending(kv => kv.Value).First().Key;
                        
                        // その部位のリストに追加
                        result[primaryPart].Add(meshInfo);
                        meshInfo.primaryBodyPart = primaryPart;
                    }
                    else
                    {
                        // ボーン情報から判別できない場合はOtherに分類
                        result[BodyPart.Other].Add(meshInfo);
                        meshInfo.primaryBodyPart = BodyPart.Other;
                    }
                }
                else
                {
                    // 非スキンメッシュの場合は位置から推定
                    BodyPart estimatedPart = EstimateBodyPartFromPosition(meshInfo.transform.position, bones);
                    result[estimatedPart].Add(meshInfo);
                    meshInfo.primaryBodyPart = estimatedPart;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// スキンメッシュから体の部位の重みを推定
        /// </summary>
        private static Dictionary<BodyPart, float> EstimateBodyPartWeights(
            SkinnedMeshRenderer renderer, 
            List<BoneData> bones)
        {
            Dictionary<BodyPart, float> result = new Dictionary<BodyPart, float>();
            
            if (renderer == null || renderer.bones == null || renderer.bones.Length == 0)
            {
                return result;
            }
            
            // ボーン配列からボーンデータを探す
            Dictionary<Transform, BoneData> transformToBone = new Dictionary<Transform, BoneData>();
            foreach (var bone in bones)
            {
                if (bone.transform != null)
                {
                    transformToBone[bone.transform] = bone;
                }
            }
            
            // 各ボーンの部位をカウント
            Dictionary<BodyPart, int> partCounts = new Dictionary<BodyPart, int>();
            
            foreach (var boneTransform in renderer.bones)
            {
                if (boneTransform != null && transformToBone.TryGetValue(boneTransform, out BoneData boneData))
                {
                    BodyPart part = boneData.bodyPart;
                    
                    if (!partCounts.ContainsKey(part))
                    {
                        partCounts[part] = 0;
                    }
                    
                    partCounts[part]++;
                }
            }
            
            // カウントから重みを計算
            int totalBones = renderer.bones.Length;
            foreach (var kvp in partCounts)
            {
                result[kvp.Key] = (float)kvp.Value / totalBones;
            }
            
            return result;
        }
        
        /// <summary>
        /// 位置から体の部位を推定
        /// </summary>
        private static BodyPart EstimateBodyPartFromPosition(Vector3 position, List<BoneData> bones)
        {
            // 指定された位置に最も近いボーンを見つける
            BoneData closestBone = null;
            float minDistance = float.MaxValue;
            
            foreach (var bone in bones)
            {
                if (bone.transform != null)
                {
                    float distance = Vector3.Distance(position, bone.transform.position);
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestBone = bone;
                    }
                }
            }
            
            return closestBone != null ? closestBone.bodyPart : BodyPart.Other;
        }
        
        /// <summary>
        /// メッシュの変形処理
        /// </summary>
        private static void DeformMeshes(
            List<MeshInfo> avatarMeshes,
            List<MeshInfo> costumeMeshes,
            BodyPart bodyPart,
            AdjustmentSettings settings)
        {
            // メッシュベースの変形はより高度なアルゴリズムが必要
            // このサンプルでは簡単なスケーリングと位置合わせのみを行う
            
            // アバターメッシュのバウンディングボックスを計算
            Bounds avatarBounds = CalculateCombinedBounds(avatarMeshes);
            
            // 衣装メッシュのバウンディングボックスを計算
            Bounds costumeBounds = CalculateCombinedBounds(costumeMeshes);
            
            // 部位に応じた調整設定を取得
            BodyPartAdjustment partAdjustment = settings.GetBodyPartAdjustment(bodyPart);
            
            // 各衣装メッシュに適用
            foreach (var costumeMesh in costumeMeshes)
            {
                if (costumeMesh.transform != null)
                {
                    // 1. スケール調整
                    Vector3 scaleRatio = Vector3.one;
                    
                    // アバターと衣装のサイズ比を計算
                    if (avatarBounds.size.x > 0 && costumeBounds.size.x > 0)
                    {
                        scaleRatio.x = avatarBounds.size.x / costumeBounds.size.x;
                    }
                    
                    if (avatarBounds.size.y > 0 && costumeBounds.size.y > 0)
                    {
                        scaleRatio.y = avatarBounds.size.y / costumeBounds.size.y;
                    }
                    
                    if (avatarBounds.size.z > 0 && costumeBounds.size.z > 0)
                    {
                        scaleRatio.z = avatarBounds.size.z / costumeBounds.size.z;
                    }
                    
                    // 部位調整設定を適用
                    scaleRatio.x *= partAdjustment.scaleX;
                    scaleRatio.y *= partAdjustment.scaleY;
                    scaleRatio.z *= partAdjustment.scaleZ;
                    
                    costumeMesh.transform.localScale = Vector3.Scale(costumeMesh.transform.localScale, scaleRatio);
                    
                    // 2. 位置調整
                    Vector3 positionOffset = avatarBounds.center - costumeBounds.center;
                    
                    // 部位調整設定を適用
                    positionOffset.x += partAdjustment.offsetX;
                    positionOffset.y += partAdjustment.offsetY;
                    positionOffset.z += partAdjustment.offsetZ;
                    
                    costumeMesh.transform.position += positionOffset;
                }
            }
        }
        
        /// <summary>
        /// メッシュリストの結合バウンディングボックスを計算
        /// </summary>
        private static Bounds CalculateCombinedBounds(List<MeshInfo> meshes)
        {
            if (meshes.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }
            
            Bounds result = new Bounds(meshes[0].transform.position, Vector3.zero);
            
            foreach (var meshInfo in meshes)
            {
                if (meshInfo.mesh != null)
                {
                    // メッシュのバウンディングボックスをワールド空間に変換
                    Bounds meshBounds = meshInfo.mesh.bounds;
                    
                    // ワールド空間での変換行列
                    Matrix4x4 matrix = meshInfo.transform.localToWorldMatrix;
                    
                    // 変換された8つの頂点を求める
                    Vector3[] points = new Vector3[8];
                    Vector3 min = meshBounds.min;
                    Vector3 max = meshBounds.max;
                    
                    points[0] = matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z));
                    points[1] = matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z));
                    points[2] = matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z));
                    points[3] = matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z));
                    points[4] = matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z));
                    points[5] = matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z));
                    points[6] = matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z));
                    points[7] = matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, max.z));
                    
                    // 結果のバウンディングボックスを拡張
                    foreach (var point in points)
                    {
                        result.Encapsulate(point);
                    }
                }
            }
            
            return result;
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
            // 上半身のオフセット
            Vector3 upperBodyOffset = settings.GetUpperBodyOffset();
            
            // 下半身のオフセット
            Vector3 lowerBodyOffset = settings.GetLowerBodyOffset();
            
            // 体の部位ごとに適用（将来の実装）
        }
    }
    
    /// <summary>
    /// メッシュ情報を保持するクラス
    /// </summary>
    public class MeshInfo
    {
        public Transform transform;
        public Mesh mesh;
        public Renderer renderer;
        public MeshFilter meshFilter;
        public bool isSkinned;
        public BodyPart primaryBodyPart = BodyPart.Other;
    }
}
