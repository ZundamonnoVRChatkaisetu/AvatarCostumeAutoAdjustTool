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
                idMapping[mapping.costumeBoneId] = mapping.avatarBoneId;
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

        /// <summary>
        /// 対応するアバターのボーンを検索する（改善版）
        /// </summary>
        private static Transform FindAppropriateAvatarBone(
            Transform costumeBone,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData,
            Dictionary<string, string> boneIdMapping,
            Dictionary<string, Transform> avatarBonesByPath,
            Dictionary<string, Transform> costumeBonesByPath)
        {
            if (costumeBone == null)
                return null;

            // 対応するボーンデータを探す
            BoneData costumeBoneData = costumeBones.FirstOrDefault(b => b.transform == costumeBone);

            if (costumeBoneData != null)
            {
                // 1. マッピングからアバターボーンを取得
                if (boneIdMapping.TryGetValue(costumeBoneData.id, out string avatarBoneId))
                {
                    BoneData avatarBoneData = avatarBones.FirstOrDefault(b => b.id == avatarBoneId);
                    if (avatarBoneData != null && avatarBoneData.transform != null)
                    {
                        return avatarBoneData.transform;
                    }
                }

                // 2. 体のパーツによるマッピング
                if (costumeBoneData.bodyPart != BodyPart.Unknown && costumeBoneData.bodyPart != BodyPart.Other)
                {
                    var matchingAvatarBones = avatarBones.Where(b => b.bodyPart == costumeBoneData.bodyPart).ToList();
                    if (matchingAvatarBones.Count > 0)
                    {
                        return matchingAvatarBones[0].transform;
                    }
                }
            }

            // 3. 名前ベースの検索
            string boneName = costumeBone.name;
            Transform nameMatch = FindBoneByName(avatarObject, boneName);
            if (nameMatch != null)
                return nameMatch;

            // 4. 正規化された名前での検索
            string normalizedName = NormalizeBoneName(boneName);
            if (!string.IsNullOrEmpty(normalizedName) && avatarBonesByPath.TryGetValue(normalizedName, out Transform normalizedMatch))
            {
                return normalizedMatch;
            }

            // 5. 位置による最近接ボーンを検索
            Transform positionMatch = FindBoneByPosition(costumeBone, avatarObject, avatarBones, costumeBones);
            if (positionMatch != null)
                return positionMatch;

            // 6. 最後の手段としてヒップボーンまたはルートを返す
            var hipBone = avatarBones.FirstOrDefault(b => b.bodyPart == BodyPart.Hips);
            if (hipBone != null && hipBone.transform != null)
                return hipBone.transform;

            // それでも見つからない場合はアバターのルートを返す
            return avatarObject.transform;
        }

        /// <summary>
        /// 名前でボーンを検索する
        /// </summary>
        private static Transform FindBoneByName(GameObject root, string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return null;

            // 完全一致検索
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>();
            foreach (var transform in allTransforms)
            {
                if (transform.name == boneName)
                    return transform;
            }

            // 部分一致検索（LeftArm → left_arm, leftarm, LEFT_ARM などのバリエーションに対応）
            string normalizedName = NormalizeBoneName(boneName);
            foreach (var transform in allTransforms)
            {
                string normalizedTransformName = NormalizeBoneName(transform.name);
                
                // 完全一致
                if (normalizedTransformName == normalizedName)
                    return transform;
                
                // 部分一致（より正確な一致を優先）
                if (normalizedTransformName.Contains(normalizedName) || normalizedName.Contains(normalizedTransformName))
                {
                    // 左右の違いを考慮する
                    bool isLeftInSource = normalizedName.Contains("left") || normalizedName.Contains("l");
                    bool isRightInSource = normalizedName.Contains("right") || normalizedName.Contains("r");
                    bool isLeftInTarget = normalizedTransformName.Contains("left") || normalizedTransformName.Contains("l");
                    bool isRightInTarget = normalizedTransformName.Contains("right") || normalizedTransformName.Contains("r");
                    
                    // 左右が一致している場合のみ採用
                    if ((isLeftInSource && isLeftInTarget) || 
                        (isRightInSource && isRightInTarget) || 
                        (!isLeftInSource && !isRightInSource && !isLeftInTarget && !isRightInTarget))
                    {
                        return transform;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 位置に基づいてボーンを検索する（改善版）
        /// </summary>
        private static Transform FindBoneByPosition(
            Transform costumeBone,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones)
        {
            if (costumeBone == null || avatarObject == null)
                return null;
                
            // 衣装ボーンの位置情報からボディパーツを推測
            Vector3 costumePos = costumeBone.position;

            // コスチュームのボーンからボディパーツを推測する
            BoneData costumeBoneData = costumeBones.FirstOrDefault(b => b.transform == costumeBone);
            BodyPart suggestedBodyPart = costumeBoneData != null ? costumeBoneData.bodyPart : BodyPart.Unknown;

            // 推測したボディパーツに基づいてアバターの対応するボーンを検索
            if (suggestedBodyPart != BodyPart.Unknown)
            {
                var matchingAvatarBones = avatarBones.Where(b => b.bodyPart == suggestedBodyPart).ToList();
                if (matchingAvatarBones.Count > 0)
                {
                    // 位置が最も近いボーンを選択
                    return matchingAvatarBones
                        .OrderBy(b => Vector3.Distance(costumePos, b.transform.position))
                        .First().transform;
                }
            }

            // ボディパーツが不明または一致するボーンが見つからない場合は、位置による最近接ボーンを検索
            Transform[] avatarTransforms = avatarObject.GetComponentsInChildren<Transform>();
            float closestDistance = float.MaxValue;
            Transform closestBone = null;

            foreach (var transform in avatarTransforms)
            {
                float distance = Vector3.Distance(costumePos, transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBone = transform;
                }
            }

            return closestBone;
        }

        /// <summary>
        /// 2つのボーンの階層構造が異なるかどうかをチェック
        /// </summary>
        private static bool IsStructurallyDifferent(Transform originalBone, Transform newBone)
        {
            if (originalBone == null || newBone == null)
                return false;

            // 親の数が異なる場合は構造が異なる
            int originalDepth = 0;
            int newDepth = 0;
            
            Transform originalParent = originalBone.parent;
            while (originalParent != null)
            {
                originalDepth++;
                originalParent = originalParent.parent;
            }
            
            Transform newParent = newBone.parent;
            while (newParent != null)
            {
                newDepth++;
                newParent = newParent.parent;
            }
            
            // 階層の深さが異なる場合は構造が異なると判断
            if (Mathf.Abs(originalDepth - newDepth) > 1)
                return true;
                
            return false;
        }

        /// <summary>
        /// バインドポーズを再計算（改善版）
        /// </summary>
        private static void RecalculateBindPoses(
            ref Matrix4x4[] newBindPoses,
            Dictionary<int, BoneTransformInfo> boneTransformMap,
            SkinnedMeshRenderer renderer)
        {
            // 新しいボーン構造に基づいてバインドポーズを再計算
            foreach (var kvp in boneTransformMap)
            {
                int index = kvp.Key;
                BoneTransformInfo info = kvp.Value;

                if (info.NewBone != null)
                {
                    try
                    {
                        // メッシュのルートに対する新しいボーンのワールド行列
                        Matrix4x4 worldToLocalMatrix;
                        
                        // レンダラーのトランスフォームが存在する場合は考慮
                        if (renderer.transform != null)
                        {
                            // ワールド空間からメッシュローカル空間への変換行列
                            worldToLocalMatrix = renderer.transform.worldToLocalMatrix;
                        }
                        else
                        {
                            worldToLocalMatrix = Matrix4x4.identity;
                        }
                        
                        // 新しいバインドポーズ = メッシュローカル空間から見たボーンの逆行列
                        Matrix4x4 bindPose = worldToLocalMatrix * info.NewBone.localToWorldMatrix;
                        bindPose = bindPose.inverse;
                        
                        newBindPoses[index] = bindPose;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"バインドポーズの計算中にエラーが発生しました: {ex.Message}");
                        // エラーの場合は元のバインドポーズを使用
                        newBindPoses[index] = info.OriginalBindPose;
                    }
                }
                else
                {
                    // 対応するボーンがない場合は元のバインドポーズを使用
                    newBindPoses[index] = info.OriginalBindPose;
                }
            }
        }

        /// <summary>
        /// スキンメッシュのウェイトを再分配
        /// </summary>
        private static void RedistributeSkinnedMeshWeights(
            Mesh mesh, 
            Transform[] originalBones, 
            Transform[] newBones, 
            List<int> missingBoneIndices)
        {
            if (mesh == null || !mesh.isReadable)
            {
                Debug.LogWarning("メッシュが読み取り可能ではないため、スキンウェイトの再分配ができません。");
                return;
            }

            // ウェイトデータが少ない場合の対処
            if (mesh.boneWeights.Length == 0)
                return;

            // ウェイトデータの複製
            BoneWeight[] oldWeights = mesh.boneWeights;
            BoneWeight[] newWeights = new BoneWeight[oldWeights.Length];

            // 各頂点のウェイトを再分配
            for (int i = 0; i < oldWeights.Length; i++)
            {
                BoneWeight oldWeight = oldWeights[i];
                BoneWeight newWeight = new BoneWeight();

                // 重みの合計
                float totalWeight = 0;
                
                // 新しいウェイト値を設定
                if (!missingBoneIndices.Contains(oldWeight.boneIndex0) && oldWeight.weight0 > 0)
                {
                    newWeight.boneIndex0 = oldWeight.boneIndex0;
                    newWeight.weight0 = oldWeight.weight0;
                    totalWeight += oldWeight.weight0;
                }
                else
                {
                    newWeight.boneIndex0 = 0;
                    newWeight.weight0 = 0;
                }

                if (!missingBoneIndices.Contains(oldWeight.boneIndex1) && oldWeight.weight1 > 0)
                {
                    newWeight.boneIndex1 = oldWeight.boneIndex1;
                    newWeight.weight1 = oldWeight.weight1;
                    totalWeight += oldWeight.weight1;
                }
                else
                {
                    newWeight.boneIndex1 = 0;
                    newWeight.weight1 = 0;
                }

                if (!missingBoneIndices.Contains(oldWeight.boneIndex2) && oldWeight.weight2 > 0)
                {
                    newWeight.boneIndex2 = oldWeight.boneIndex2;
                    newWeight.weight2 = oldWeight.weight2;
                    totalWeight += oldWeight.weight2;
                }
                else
                {
                    newWeight.boneIndex2 = 0;
                    newWeight.weight2 = 0;
                }

                if (!missingBoneIndices.Contains(oldWeight.boneIndex3) && oldWeight.weight3 > 0)
                {
                    newWeight.boneIndex3 = oldWeight.boneIndex3;
                    newWeight.weight3 = oldWeight.weight3;
                    totalWeight += oldWeight.weight3;
                }
                else
                {
                    newWeight.boneIndex3 = 0;
                    newWeight.weight3 = 0;
                }

                // ウェイトの正規化（合計が0でない場合）
                if (totalWeight > 0)
                {
                    newWeight.weight0 /= totalWeight;
                    newWeight.weight1 /= totalWeight;
                    newWeight.weight2 /= totalWeight;
                    newWeight.weight3 /= totalWeight;
                }
                else
                {
                    // ウェイトの合計が0の場合、最初のボーンに全てのウェイトを割り当て
                    newWeight.boneIndex0 = 0;
                    newWeight.weight0 = 1;
                    newWeight.boneIndex1 = 0;
                    newWeight.weight1 = 0;
                    newWeight.boneIndex2 = 0;
                    newWeight.weight2 = 0;
                    newWeight.boneIndex3 = 0;
                    newWeight.weight3 = 0;
                }

                newWeights[i] = newWeight;
            }

            // 新しいウェイトをメッシュに設定
            mesh.boneWeights = newWeights;
        }

        /// <summary>
        /// ボーン変換情報を保持する構造体
        /// </summary>
        private struct BoneTransformInfo
        {
            public Transform OriginalBone;
            public Transform NewBone;
            public Matrix4x4 OriginalBindPose;
        }
    }
}