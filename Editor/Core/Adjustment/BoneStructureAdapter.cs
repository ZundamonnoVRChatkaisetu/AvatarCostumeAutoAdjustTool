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

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null)
                    continue;

                AdaptSkinnedMeshToNewBoneStructure(renderer, avatarObject, avatarBones, costumeBones, mappingData);
            }
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

            // 新しいボーン配列を作成
            Transform[] newBones = new Transform[originalBones.Length];
            Matrix4x4[] newBindPoses = new Matrix4x4[originalBindPoses.Length];

            // ボーンの変換マッピングを作成（インデックスベース）
            Dictionary<int, int> boneIndexMap = new Dictionary<int, int>();
            Dictionary<int, BoneTransformInfo> boneTransformMap = new Dictionary<int, BoneTransformInfo>();

            // ルートボーンの処理
            Transform newRootBone = FindAppropriateAvatarBone(renderer.rootBone, avatarObject, avatarBones, costumeBones, mappingData);
            if (newRootBone != null)
            {
                renderer.rootBone = newRootBone;
            }

            // 各ボーンの処理
            bool hasStructuralDifferences = false;

            for (int i = 0; i < originalBones.Length; i++)
            {
                Transform originalBone = originalBones[i];
                if (originalBone == null)
                {
                    newBones[i] = null;
                    newBindPoses[i] = originalBindPoses[i];
                    continue;
                }

                // 対応するアバターボーンを検索
                Transform newBone = FindAppropriateAvatarBone(originalBone, avatarObject, avatarBones, costumeBones, mappingData);

                if (newBone != null)
                {
                    newBones[i] = newBone;

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
                    // マッピングが見つからない場合は元のボーンを使用
                    newBones[i] = originalBone;
                    newBindPoses[i] = originalBindPoses[i];
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
            bool needsWeightRedistribution = hasStructuralDifferences;

            // スキンウェイトの再分配が必要な場合
            if (needsWeightRedistribution)
            {
                RedistributeSkinnedMeshWeights(sharedMesh, originalBones, newBones);
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
        /// 対応するアバターのボーンを検索する
        /// </summary>
        private static Transform FindAppropriateAvatarBone(
            Transform costumeBone,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones,
            MappingData mappingData)
        {
            if (costumeBone == null)
                return null;

            // 対応するボーンデータを探す
            BoneData costumeBoneData = costumeBones.FirstOrDefault(b => b.transform == costumeBone);

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
                    return avatarBoneData.transform;
                }
            }

            // 代替マッピング探索方法
            // 1. 名前による直接検索
            string boneName = costumeBone.name;
            Transform directMatch = FindBoneByName(avatarObject, boneName);
            if (directMatch != null)
                return directMatch;

            // 2. 体のパーツ位置による検索
            Transform positionMatch = FindBoneByPosition(costumeBone, avatarObject, avatarBones, costumeBones);
            if (positionMatch != null)
                return positionMatch;

            // 3. 最後の手段としてヒップボーンまたはルートを返す
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
            string normalizedName = boneName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            foreach (var transform in allTransforms)
            {
                string normalizedTransformName = transform.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                if (normalizedTransformName.Contains(normalizedName) || normalizedName.Contains(normalizedTransformName))
                    return transform;
            }

            return null;
        }

        /// <summary>
        /// 位置に基づいてボーンを検索する
        /// </summary>
        private static Transform FindBoneByPosition(
            Transform costumeBone,
            GameObject avatarObject,
            List<BoneData> avatarBones,
            List<BoneData> costumeBones)
        {
            // 衣装ボーンの位置情報からボディパーツを推測
            Vector3 costumePos = costumeBone.position;
            Vector3 costumeLocalPos = costumeBone.localPosition;

            // コスチュームのボーンからボディパーツを推測する
            BoneData costumeBoneData = costumeBones.FirstOrDefault(b => b.transform == costumeBone);
            BodyPart suggestedBodyPart = costumeBoneData != null ? costumeBoneData.bodyPart : BodyPart.Unknown;

            // 推測したボディパーツに基づいてアバターの対応するボーンを検索
            if (suggestedBodyPart != BodyPart.Unknown)
            {
                var matchingAvatarBones = avatarBones.Where(b => b.bodyPart == suggestedBodyPart).ToList();
                if (matchingAvatarBones.Count > 0)
                {
                    // 単純に最初のマッチを返す（将来的にはより精密な選択ロジックを実装予定）
                    return matchingAvatarBones[0].transform;
                }
            }

            // ボディパーツが不明または一致するボーンが見つからない場合は、位置による最近接ボーンを検索
            float closestDistance = float.MaxValue;
            Transform closestBone = null;

            foreach (var avatarBone in avatarBones)
            {
                if (avatarBone.transform != null)
                {
                    float distance = Vector3.Distance(costumePos, avatarBone.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestBone = avatarBone.transform;
                    }
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
        /// バインドポーズを再計算
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
                    // ボーンのワールド空間からメッシュのローカル空間への変換を計算
                    Matrix4x4 bindPose = Matrix4x4.identity;
                    
                    // レンダラーのトランスフォームが存在する場合は考慮
                    if (renderer.transform != null)
                    {
                        bindPose = renderer.transform.worldToLocalMatrix * info.NewBone.localToWorldMatrix;
                    }
                    else
                    {
                        bindPose = info.NewBone.worldToLocalMatrix;
                    }
                    
                    bindPose = bindPose.inverse;
                    newBindPoses[index] = bindPose;
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
        private static void RedistributeSkinnedMeshWeights(Mesh mesh, Transform[] originalBones, Transform[] newBones)
        {
            if (mesh == null || !mesh.isReadable)
            {
                Debug.LogWarning("メッシュが読み取り可能ではないため、スキンウェイトの再分配ができません。");
                return;
            }

            // ボーンのインデックスマッピングを作成
            Dictionary<int, int> boneIndexMap = new Dictionary<int, int>();
            for (int i = 0; i < originalBones.Length; i++)
            {
                if (originalBones[i] != null && newBones[i] != null)
                {
                    boneIndexMap[i] = i; // 同じインデックスを使用
                }
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
                
                // 各ボーンインデックスの処理
                // インデックス0の処理
                if (boneIndexMap.ContainsKey(oldWeight.boneIndex0) && oldWeight.weight0 > 0)
                {
                    newWeight.boneIndex0 = boneIndexMap[oldWeight.boneIndex0];
                    newWeight.weight0 = oldWeight.weight0;
                    totalWeight += oldWeight.weight0;
                }
                else
                {
                    newWeight.boneIndex0 = 0;
                    newWeight.weight0 = 0;
                }

                // インデックス1の処理
                if (boneIndexMap.ContainsKey(oldWeight.boneIndex1) && oldWeight.weight1 > 0)
                {
                    newWeight.boneIndex1 = boneIndexMap[oldWeight.boneIndex1];
                    newWeight.weight1 = oldWeight.weight1;
                    totalWeight += oldWeight.weight1;
                }
                else
                {
                    newWeight.boneIndex1 = 0;
                    newWeight.weight1 = 0;
                }

                // インデックス2の処理
                if (boneIndexMap.ContainsKey(oldWeight.boneIndex2) && oldWeight.weight2 > 0)
                {
                    newWeight.boneIndex2 = boneIndexMap[oldWeight.boneIndex2];
                    newWeight.weight2 = oldWeight.weight2;
                    totalWeight += oldWeight.weight2;
                }
                else
                {
                    newWeight.boneIndex2 = 0;
                    newWeight.weight2 = 0;
                }

                // インデックス3の処理
                if (boneIndexMap.ContainsKey(oldWeight.boneIndex3) && oldWeight.weight3 > 0)
                {
                    newWeight.boneIndex3 = boneIndexMap[oldWeight.boneIndex3];
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
