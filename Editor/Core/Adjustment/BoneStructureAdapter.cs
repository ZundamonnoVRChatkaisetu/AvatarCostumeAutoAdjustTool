using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// 異なるボーン構成に対応するためのアダプタークラス
    /// </summary>
    public static class BoneStructureAdapter
    {
        /// <summary>
        /// 異なるボーン構造のアバターと衣装に対して適応処理を行う
        /// </summary>
        public static void AdaptToDifferentBoneStructure(
            GameObject avatarObject,
            GameObject costumeObject,
            MappingData mappingData,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones)
        {
            if (avatarObject == null || costumeObject == null || mappingData == null)
            {
                Debug.LogError("ボーン構造適応処理に必要なオブジェクトが不足しています。");
                return;
            }

            // 衣装内のすべてのスキンメッシュレンダラーを取得
            SkinnedMeshRenderer[] renderers = costumeObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            int successCount = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null)
                    continue;

                try 
                {
                    AdaptSkinnedMeshToNewBoneStructure(renderer, avatarObject, avatarBones, costumeBones, mappingData);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"スキンメッシュ '{renderer.name}' の適応処理中にエラーが発生しました: {ex.Message}");
                }
            }

            Debug.Log($"ボーン構造適応処理が完了しました。{successCount}/{renderers.Length} のスキンメッシュを処理しました。");
        }

        /// <summary>
        /// スキンメッシュを新しいボーン構造に適応させる
        /// </summary>
        private static void AdaptSkinnedMeshToNewBoneStructure(
            SkinnedMeshRenderer renderer,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData)
        {
            Mesh sharedMesh = renderer.sharedMesh;
            if (sharedMesh == null)
                return;

            // 元のボーン配列とバインドポーズを取得
            Transform[] originalBones = renderer.bones;
            Matrix4x4[] originalBindPoses = sharedMesh.bindposes;

            if (originalBones == null || originalBones.Length == 0 || 
                originalBindPoses == null || originalBindPoses.Length == 0)
            {
                return;
            }

            // ボーンマッピングの改善: ボーンパス情報も使用
            Dictionary<string, Transform> avatarBonesByPath = CreateBonePathDictionary(avatarObject);
            Dictionary<string, Transform> costumeBonesByPath = CreateBonePathDictionary(renderer.gameObject);

            // 新しいボーン配列を作成
            Transform[] newBones = new Transform[originalBones.Length];
            Matrix4x4[] newBindPoses = new Matrix4x4[originalBindPoses.Length];

            // ボーンの変換マッピングを作成（インデックスベース）
            Dictionary<int, int> boneIndexMap = new Dictionary<int, int>();
            Dictionary<int, BoneTransformInfo> boneTransformMap = new Dictionary<int, BoneTransformInfo>();
            Dictionary<Transform, Transform> directBoneMapping = new Dictionary<Transform, Transform>();

            // マッピング情報をディクショナリに変換して高速アクセスできるようにする
            Dictionary<string, string> boneIdMapping = CreateBoneIdMappingDictionary(mappingData);

            // ルートボーンの処理
            Transform newRootBone = FindAppropriateAvatarBone(
                renderer.rootBone, 
                avatarObject, 
                avatarBones, 
                costumeBones, 
                mappingData,
                boneIdMapping,
                avatarBonesByPath,
                costumeBonesByPath);

            if (newRootBone != null)
            {
                renderer.rootBone = newRootBone;
            }
            else if (avatarObject != null)
            {
                // ルートボーンが見つからない場合はアバターのルートを使用
                renderer.rootBone = avatarObject.transform;
            }

            // 各ボーンの処理
            bool hasStructuralDifferences = false;
            List<int> missingBoneIndices = new List<int>();

            for (int i = 0; i < originalBones.Length; i++)
            {
                Transform originalBone = originalBones[i];
                if (originalBone == null)
                {
                    newBones[i] = null;
                    newBindPoses[i] = originalBindPoses[i];
                    missingBoneIndices.Add(i);
                    continue;
                }

                // 対応するアバターボーンを検索
                Transform newBone = FindAppropriateAvatarBone(
                    originalBone, 
                    avatarObject, 
                    avatarBones, 
                    costumeBones, 
                    mappingData,
                    boneIdMapping,
                    avatarBonesByPath,
                    costumeBonesByPath);

                if (newBone != null)
                {
                    newBones[i] = newBone;
                    directBoneMapping[originalBone] = newBone;

                    // 元のボーンと新しいボーンの階層構造が異なる場合はフラグを立てる
                    if (IsStructurallyDifferent(originalBone, newBone))
                    {
                        hasStructuralDifferences = true;
                    }

                    // バインドポーズの計算に必要な情報を保存
                    boneTransformMap[i] = new BoneTransformInfo
                    {
                        OriginalBone = originalBone,
                        NewBone = newBone,
                        OriginalBindPose = originalBindPoses[i]
                    };
                }
                else
                {
                    // マッピングが見つからない場合
                    Debug.LogWarning($"ボーン '{originalBone.name}' に対応するアバターボーンが見つかりませんでした。");
                    missingBoneIndices.Add(i);
                    
                    // 階層的に最も近い親ボーンを探す
                    Transform parentBone = originalBone.parent;
                    Transform fallbackBone = null;
                    
                    while (parentBone != null && fallbackBone == null)
                    {
                        if (directBoneMapping.TryGetValue(parentBone, out Transform mappedParent))
                        {
                            fallbackBone = mappedParent;
                            break;
                        }
                        parentBone = parentBone.parent;
                    }
                    
                    if (fallbackBone == null)
                    {
                        // それでも見つからない場合はルートを使用
                        fallbackBone = renderer.rootBone ?? avatarObject.transform;
                    }
                    
                    newBones[i] = fallbackBone;
                    hasStructuralDifferences = true;
                    
                    boneTransformMap[i] = new BoneTransformInfo
                    {
                        OriginalBone = originalBone,
                        NewBone = fallbackBone,
                        OriginalBindPose = originalBindPoses[i]
                    };
                }
            }

            // 階層構造が異なる場合はバインドポーズを再計算
            if (hasStructuralDifferences)
            {
                RecalculateBindPoses(ref newBindPoses, boneTransformMap, renderer);
            }
            else
            {
                // 階層構造が同じ場合は元のバインドポーズを使用
                newBindPoses = originalBindPoses;
            }

            // スキンウェイトの再計算が必要かどうかを判断
            bool needsWeightRedistribution = hasStructuralDifferences || missingBoneIndices.Count > 0;

            // スキンウェイトの再分配が必要な場合
            if (needsWeightRedistribution && sharedMesh.isReadable)
            {
                RedistributeSkinnedMeshWeights(sharedMesh, originalBones, newBones, missingBoneIndices);
            }
            else if (needsWeightRedistribution && !sharedMesh.isReadable)
            {
                Debug.LogWarning($"メッシュ '{sharedMesh.name}' は読み取り可能ではないため、スキンウェイトの再分配ができません。" +
                               "プロジェクト設定でメッシュの Read/Write Enabled オプションを有効にしてください。");
            }

            // 変更を適用
            renderer.bones = newBones;
            
            // 新しいバインドポーズをメッシュに適用
            Mesh meshCopy = UnityEngine.Object.Instantiate(sharedMesh);
            meshCopy.name = sharedMesh.name + "_Adapted";
            meshCopy.bindposes = newBindPoses;
            renderer.sharedMesh = meshCopy;

            // 完了メッセージ
            Debug.Log($"スキンメッシュ「{sharedMesh.name}」を新しいボーン構造に適応させました。" + 
                      (hasStructuralDifferences ? "（ボーン階層の違いを検出して調整しました）" : ""));
        }

        /// <summary>
        /// オブジェクト内のすべてのボーンをパスをキーとして辞書化する
        /// </summary>
        private static Dictionary<string, Transform> CreateBonePathDictionary(GameObject obj)
        {
            Dictionary<string, Transform> boneDict = new Dictionary<string, Transform>();
            Transform[] transforms = obj.GetComponentsInChildren<Transform>(true);

            foreach (var transform in transforms)
            {
                string path = GetTransformPath(transform, obj.transform);
                boneDict[path] = transform;

                // 正規化されたボーン名も追加（大文字小文字を区別せず、アンダースコアなどを除去）
                string normalizedName = NormalizeBoneName(transform.name);
                if (!string.IsNullOrEmpty(normalizedName) && !boneDict.ContainsKey(normalizedName))
                {
                    boneDict[normalizedName] = transform;
                }
            }

            return boneDict;
        }

        /// <summary>
        /// マッピングデータからIDをキーとするマッピング辞書を作成
        /// </summary>
        private static Dictionary<string, string> CreateBoneIdMappingDictionary(MappingData mappingData)
        {
            Dictionary<string, string> idMapping = new Dictionary<string, string>();
            
            if (mappingData == null || mappingData.BoneMappings == null)
                return idMapping;

            foreach (var mapping in mappingData.BoneMappings)
            {
                // 衣装ボーンID -> アバターボーンID のマッピング
                idMapping[mapping.CostumeBoneId] = mapping.AvatarBoneId;
            }

            return idMapping;
        }

        /// <summary>
        /// ボーン名を正規化する（大文字小文字を区別せず、アンダースコアなどを除去）
        /// </summary>
        private static string NormalizeBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return "";

            return boneName.ToLowerInvariant()
                .Replace("_", "")
                .Replace(" ", "")
                .Replace("-", "");
        }

        /// <summary>
        /// トランスフォームのパスを取得
        /// </summary>
        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == root)
                return "";

            if (transform.parent == root)
                return transform.name;

            return GetTransformPath(transform.parent, root) + "/" + transform.name;
        }
