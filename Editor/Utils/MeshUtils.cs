using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// メッシュ操作に関するユーティリティ機能を提供するクラス
    /// </summary>
    public static class MeshUtils
    {
        /// <summary>
        /// スキンメッシュレンダラーのバインドポーズを更新
        /// </summary>
        public static void UpdateBindPoses(SkinnedMeshRenderer skinnedMesh, Transform[] newBones)
        {
            if (skinnedMesh == null || newBones == null || newBones.Length == 0) return;
            
            Mesh sharedMesh = skinnedMesh.sharedMesh;
            if (sharedMesh == null) return;
            
            // メッシュをインスタンス化（元のアセットを変更しないため）
            Mesh instancedMesh = Object.Instantiate(sharedMesh);
            
            // 新しいバインドポーズ配列
            Matrix4x4[] bindPoses = new Matrix4x4[newBones.Length];
            
            // ルートボーンの逆行列
            Matrix4x4 rootInverse = Matrix4x4.identity;
            if (skinnedMesh.rootBone != null)
            {
                rootInverse = skinnedMesh.rootBone.worldToLocalMatrix;
            }
            
            // 各ボーンのバインドポーズを設定
            for (int i = 0; i < newBones.Length; i++)
            {
                if (newBones[i] != null)
                {
                    // ボーンのワールド空間からメッシュのローカル空間への変換行列
                    bindPoses[i] = rootInverse * newBones[i].localToWorldMatrix;
                }
                else
                {
                    bindPoses[i] = Matrix4x4.identity;
                }
            }
            
            // メッシュにバインドポーズを設定
            instancedMesh.bindposes = bindPoses;
            
            // スキンメッシュレンダラーに新しいメッシュを設定
            skinnedMesh.sharedMesh = instancedMesh;
        }

        /// <summary>
        /// スキンメッシュレンダラーのボーンウェイトを調整
        /// </summary>
        public static void AdjustBoneWeights(SkinnedMeshRenderer skinnedMesh, Dictionary<Transform, Transform> boneMapping)
        {
            if (skinnedMesh == null || boneMapping == null || boneMapping.Count == 0) return;
            
            Mesh sharedMesh = skinnedMesh.sharedMesh;
            if (sharedMesh == null) return;
            
            // 現在のボーン配列
            Transform[] currentBones = skinnedMesh.bones;
            
            // 新しいボーン配列
            Transform[] newBones = new Transform[currentBones.Length];
            
            // ボーンのマッピングを適用
            for (int i = 0; i < currentBones.Length; i++)
            {
                if (boneMapping.TryGetValue(currentBones[i], out Transform mappedBone))
                {
                    newBones[i] = mappedBone;
                }
                else
                {
                    newBones[i] = currentBones[i];
                }
            }
            
            // スキンメッシュのボーンを更新
            skinnedMesh.bones = newBones;
            
            // バインドポーズを更新
            UpdateBindPoses(skinnedMesh, newBones);
        }

        /// <summary>
        /// メッシュのバウンディングボックスを取得
        /// </summary>
        public static Bounds GetMeshBounds(SkinnedMeshRenderer skinnedMesh)
        {
            if (skinnedMesh == null || skinnedMesh.sharedMesh == null)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }
            
            return skinnedMesh.sharedMesh.bounds;
        }

        /// <summary>
        /// メッシュを体の部位に分類
        /// </summary>
        public static Dictionary<string, List<int>> ClassifyMeshByBodyPart(Mesh mesh, Transform[] bones, BodyPartReferenceData bodyPartData)
        {
            if (mesh == null || bones == null || bodyPartData == null) return null;
            
            Dictionary<string, List<int>> bodyPartVertices = new Dictionary<string, List<int>>();
            BoneWeight[] boneWeights = mesh.boneWeights;
            
            // 部位ごとの頂点リストを初期化
            foreach (var bodyPart in bodyPartData.BodyParts)
            {
                bodyPartVertices[bodyPart.Name] = new List<int>();
            }
            
            // ボーン名と部位のマッピングを作成
            Dictionary<string, string> boneToBodyPart = new Dictionary<string, string>();
            foreach (var bodyPart in bodyPartData.BodyParts)
            {
                foreach (string boneName in bodyPart.RelatedBones)
                {
                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (bones[i] != null && bones[i].name.ToLower().Contains(boneName.ToLower()))
                        {
                            boneToBodyPart[bones[i].name] = bodyPart.Name;
                        }
                    }
                }
            }
            
            // 各頂点を最も影響の強いボーンに基づいて分類
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                // 最も影響の強いボーンを特定
                int mostInfluentialBoneIndex = GetMostInfluentialBoneIndex(boneWeights[i]);
                
                if (mostInfluentialBoneIndex >= 0 && mostInfluentialBoneIndex < bones.Length && bones[mostInfluentialBoneIndex] != null)
                {
                    string boneName = bones[mostInfluentialBoneIndex].name;
                    
                    // ボーンの部位を特定
                    if (boneToBodyPart.TryGetValue(boneName, out string bodyPartName))
                    {
                        bodyPartVertices[bodyPartName].Add(i);
                    }
                    else
                    {
                        // デフォルトの部位（体の中心）に分類
                        if (bodyPartVertices.ContainsKey("Chest"))
                        {
                            bodyPartVertices["Chest"].Add(i);
                        }
                    }
                }
            }
            
            return bodyPartVertices;
        }

        /// <summary>
        /// 最も影響の強いボーンのインデックスを取得
        /// </summary>
        private static int GetMostInfluentialBoneIndex(BoneWeight boneWeight)
        {
            float[] weights = new float[4] 
            { 
                boneWeight.weight0, 
                boneWeight.weight1, 
                boneWeight.weight2, 
                boneWeight.weight3 
            };
            
            int[] indices = new int[4] 
            { 
                boneWeight.boneIndex0, 
                boneWeight.boneIndex1, 
                boneWeight.boneIndex2, 
                boneWeight.boneIndex3 
            };
            
            float maxWeight = weights[0];
            int maxIndex = 0;
            
            for (int i = 1; i < 4; i++)
            {
                if (weights[i] > maxWeight)
                {
                    maxWeight = weights[i];
                    maxIndex = i;
                }
            }
            
            return indices[maxIndex];
        }

        /// <summary>
        /// 部位ごとにメッシュを変形
        /// </summary>
        public static void DeformMeshByBodyParts(Mesh mesh, Dictionary<string, List<int>> bodyPartVertices, Dictionary<string, Vector3> scaleFactors)
        {
            if (mesh == null || bodyPartVertices == null || scaleFactors == null) return;
            
            // 頂点データを取得
            Vector3[] vertices = mesh.vertices;
            
            // 部位ごとに変形
            foreach (var bodyPart in bodyPartVertices)
            {
                string partName = bodyPart.Key;
                List<int> vertexIndices = bodyPart.Value;
                
                if (scaleFactors.TryGetValue(partName, out Vector3 scaleFactor))
                {
                    // この部位の頂点の中心を計算
                    Vector3 center = Vector3.zero;
                    foreach (int index in vertexIndices)
                    {
                        center += vertices[index];
                    }
                    
                    if (vertexIndices.Count > 0)
                    {
                        center /= vertexIndices.Count;
                        
                        // 中心からのオフセットに基づいて各頂点をスケール
                        foreach (int index in vertexIndices)
                        {
                            Vector3 offset = vertices[index] - center;
                            offset.x *= scaleFactor.x;
                            offset.y *= scaleFactor.y;
                            offset.z *= scaleFactor.z;
                            vertices[index] = center + offset;
                        }
                    }
                }
            }
            
            // 変更した頂点をメッシュに適用
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        /// <summary>
        /// メッシュの複製を作成
        /// </summary>
        public static Mesh DuplicateMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null) return null;
            
            Mesh newMesh = new Mesh();
            newMesh.vertices = sourceMesh.vertices;
            newMesh.triangles = sourceMesh.triangles;
            newMesh.uv = sourceMesh.uv;
            newMesh.normals = sourceMesh.normals;
            newMesh.colors = sourceMesh.colors;
            newMesh.tangents = sourceMesh.tangents;
            
            // ボーンウェイト
            if (sourceMesh.boneWeights != null && sourceMesh.boneWeights.Length > 0)
            {
                newMesh.boneWeights = sourceMesh.boneWeights;
            }
            
            // バインドポーズ
            if (sourceMesh.bindposes != null && sourceMesh.bindposes.Length > 0)
            {
                newMesh.bindposes = sourceMesh.bindposes.ToArray();
            }
            
            // サブメッシュ
            newMesh.subMeshCount = sourceMesh.subMeshCount;
            for (int i = 0; i < sourceMesh.subMeshCount; i++)
            {
                newMesh.SetTriangles(sourceMesh.GetTriangles(i), i);
            }
            
            newMesh.RecalculateBounds();
            
            return newMesh;
        }

        /// <summary>
        /// スキンメッシュに新しいボーン配列を設定
        /// </summary>
        public static void SetNewBones(SkinnedMeshRenderer skinnedMesh, Transform[] newBones, Transform rootBone)
        {
            if (skinnedMesh == null || newBones == null) return;
            
            // ルートボーンの設定
            skinnedMesh.rootBone = rootBone;
            
            // ボーン配列の設定
            skinnedMesh.bones = newBones;
            
            // バインドポーズの更新
            UpdateBindPoses(skinnedMesh, newBones);
        }

        /// <summary>
        /// スキンメッシュの複製を作成
        /// </summary>
        public static SkinnedMeshRenderer DuplicateSkinnedMesh(SkinnedMeshRenderer source, Transform parent)
        {
            if (source == null || parent == null) return null;
            
            // 新しいGameObjectを作成
            GameObject newObject = new GameObject(source.gameObject.name + "_Copy");
            newObject.transform.SetParent(parent);
            newObject.transform.localPosition = source.transform.localPosition;
            newObject.transform.localRotation = source.transform.localRotation;
            newObject.transform.localScale = source.transform.localScale;
            
            // スキンメッシュレンダラーを追加
            SkinnedMeshRenderer newSkinned = newObject.AddComponent<SkinnedMeshRenderer>();
            
            // メッシュの複製
            Mesh newMesh = DuplicateMesh(source.sharedMesh);
            
            // マテリアルの設定
            newSkinned.sharedMaterials = source.sharedMaterials;
            
            // メッシュの設定
            newSkinned.sharedMesh = newMesh;
            
            // ボーンとルートボーンの設定
            newSkinned.bones = source.bones;
            newSkinned.rootBone = source.rootBone;
            
            // その他のプロパティの設定
            newSkinned.quality = source.quality;
            newSkinned.updateWhenOffscreen = source.updateWhenOffscreen;
            newSkinned.skinnedMotionVectors = source.skinnedMotionVectors;
            
            return newSkinned;
        }

        /// <summary>
        /// メッシュをスケール
        /// </summary>
        public static void ScaleMesh(Mesh mesh, Vector3 scaleFactor)
        {
            if (mesh == null) return;
            
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].x *= scaleFactor.x;
                vertices[i].y *= scaleFactor.y;
                vertices[i].z *= scaleFactor.z;
            }
            
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        /// <summary>
        /// メッシュを対象の大きさに合わせて自動スケーリング
        /// </summary>
        public static Vector3 AutoScaleMeshToTarget(Mesh sourceMesh, Bounds targetBounds)
        {
            if (sourceMesh == null) return Vector3.one;
            
            Bounds sourceBounds = sourceMesh.bounds;
            
            // ソースが0サイズの場合は処理しない
            if (sourceBounds.size.x <= 0 || sourceBounds.size.y <= 0 || sourceBounds.size.z <= 0)
            {
                return Vector3.one;
            }
            
            // スケール係数を計算
            Vector3 scaleFactor = new Vector3(
                targetBounds.size.x / sourceBounds.size.x,
                targetBounds.size.y / sourceBounds.size.y,
                targetBounds.size.z / sourceBounds.size.z
            );
            
            // メッシュをスケール
            Vector3[] vertices = sourceMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].x *= scaleFactor.x;
                vertices[i].y *= scaleFactor.y;
                vertices[i].z *= scaleFactor.z;
                
                // 中心位置を合わせる
                vertices[i] += (targetBounds.center - sourceBounds.center);
            }
            
            sourceMesh.vertices = vertices;
            sourceMesh.RecalculateBounds();
            sourceMesh.RecalculateNormals();
            sourceMesh.RecalculateTangents();
            
            return scaleFactor;
        }
    }
}
