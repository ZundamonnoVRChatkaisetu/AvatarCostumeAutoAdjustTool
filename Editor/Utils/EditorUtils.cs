using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// Unityエディタ関連のユーティリティ機能を提供するクラス
    /// </summary>
    public static class EditorUtils
    {
        /// <summary>
        /// GameObjectをヒエラルキーで選択
        /// </summary>
        public static void SelectInHierarchy(GameObject gameObject)
        {
            if (gameObject == null) return;
            
            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
        }

        /// <summary>
        /// アクティブなオブジェクトがヒューマノイドアバターかどうかを確認
        /// </summary>
        public static bool IsHumanoidAvatar(GameObject gameObject)
        {
            if (gameObject == null) return false;
            
            var animator = gameObject.GetComponent<Animator>();
            return animator != null && animator.avatar != null && animator.avatar.isHuman;
        }

        /// <summary>
        /// オブジェクトからすべてのボーンを取得
        /// </summary>
        public static Transform[] GetAllBones(GameObject gameObject)
        {
            if (gameObject == null) return new Transform[0];
            
            return gameObject.GetComponentsInChildren<Transform>()
                .Where(t => t != gameObject.transform)
                .ToArray();
        }

        /// <summary>
        /// 指定されたパスのディレクトリが存在しない場合は作成
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 指定されたタイプのアセットを作成
        /// </summary>
        public static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            
            return asset;
        }

        /// <summary>
        /// 指定されたボーンタイプに基づいて色を取得
        /// </summary>
        public static Color GetBoneTypeColor(string boneType)
        {
            switch (boneType.ToLower())
            {
                case "hips":
                case "spine":
                case "chest":
                    return new Color(0.9f, 0.5f, 0.5f); // 赤っぽい色
                
                case "head":
                case "neck":
                case "jaw":
                case "eye":
                    return new Color(0.9f, 0.9f, 0.5f); // 黄色っぽい色
                
                case "shoulder":
                case "upperarm":
                case "lowerarm":
                case "hand":
                case "thumb":
                case "index":
                case "middle":
                case "ring":
                case "pinky":
                    return new Color(0.5f, 0.9f, 0.5f); // 緑っぽい色
                
                case "upperleg":
                case "lowerleg":
                case "foot":
                case "toes":
                    return new Color(0.5f, 0.5f, 0.9f); // 青っぽい色
                
                default:
                    return new Color(0.7f, 0.7f, 0.7f); // グレー
            }
        }

        /// <summary>
        /// ボックスGUIスタイルを取得
        /// </summary>
        public static GUIStyle GetBoxStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakeColorTexture(color);
            return style;
        }

        /// <summary>
        /// 指定された色のテクスチャを作成
        /// </summary>
        public static Texture2D MakeColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// アバターのスキンメッシュレンダラーを取得
        /// </summary>
        public static SkinnedMeshRenderer[] GetAvatarSkinnedMeshRenderers(GameObject avatar)
        {
            if (avatar == null) return new SkinnedMeshRenderer[0];
            
            return avatar.GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// アバターのメッシュサイズを取得
        /// </summary>
        public static Bounds GetAvatarBounds(GameObject avatar)
        {
            if (avatar == null) return new Bounds(Vector3.zero, Vector3.zero);
            
            var renderers = avatar.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            
            return bounds;
        }

        /// <summary>
        /// ハンドルの描画色を一時的に変更
        /// </summary>
        public static void WithHandlesColor(Color color, System.Action action)
        {
            Color originalColor = Handles.color;
            Handles.color = color;
            action?.Invoke();
            Handles.color = originalColor;
        }

        /// <summary>
        /// シーンビューに3Dテキストを表示
        /// </summary>
        public static void Draw3DLabel(Vector3 position, string text, Color color)
        {
            WithHandlesColor(color, () => Handles.Label(position, text));
        }

        /// <summary>
        /// シーンビューに線を描画
        /// </summary>
        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            WithHandlesColor(color, () => Handles.DrawLine(start, end));
        }

        /// <summary>
        /// シーンビューに矢印を描画
        /// </summary>
        public static void DrawArrow(Vector3 start, Vector3 end, float size, Color color)
        {
            WithHandlesColor(color, () =>
            {
                Handles.DrawLine(start, end);
                
                Vector3 direction = (end - start).normalized;
                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                if (right.magnitude < 0.01f)
                {
                    right = Vector3.Cross(direction, Vector3.forward).normalized;
                }
                
                Vector3 up = Vector3.Cross(right, direction).normalized;
                
                Vector3 arrowHead1 = end - direction * size + right * size * 0.5f;
                Vector3 arrowHead2 = end - direction * size - right * size * 0.5f;
                
                Handles.DrawLine(end, arrowHead1);
                Handles.DrawLine(end, arrowHead2);
            });
        }

        /// <summary>
        /// ボーン間の接続関係を描画
        /// </summary>
        public static void DrawBoneConnections(Transform root)
        {
            if (root == null) return;
            
            var allBones = root.GetComponentsInChildren<Transform>();
            foreach (var bone in allBones)
            {
                if (bone.parent != null && bone.parent != root)
                {
                    DrawLine(bone.parent.position, bone.position, Color.white);
                }
            }
        }

        /// <summary>
        /// アバターのヒューマノイドボーンを描画
        /// </summary>
        public static void DrawHumanoidBones(Animator animator)
        {
            if (animator == null || !animator.avatar || !animator.avatar.isHuman) return;
            
            // 体幹のボーン描画
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Hips, HumanBodyBones.Spine);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Spine, HumanBodyBones.Chest);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Chest, HumanBodyBones.UpperChest);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.UpperChest, HumanBodyBones.Neck);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Neck, HumanBodyBones.Head);
            
            // 左腕のボーン描画
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            
            // 右腕のボーン描画
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            
            // 左脚のボーン描画
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            
            // 右脚のボーン描画
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
            
            // 腰からの接続
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg);
            
            // 肩からの接続
            DrawHumanoidBoneConnection(animator, HumanBodyBones.UpperChest, HumanBodyBones.LeftShoulder);
            DrawHumanoidBoneConnection(animator, HumanBodyBones.UpperChest, HumanBodyBones.RightShoulder);
        }

        /// <summary>
        /// ヒューマノイドの2つのボーン間の接続を描画
        /// </summary>
        private static void DrawHumanoidBoneConnection(Animator animator, HumanBodyBones bone1, HumanBodyBones bone2)
        {
            Transform transform1 = animator.GetBoneTransform(bone1);
            Transform transform2 = animator.GetBoneTransform(bone2);
            
            if (transform1 != null && transform2 != null)
            {
                DrawLine(transform1.position, transform2.position, new Color(0.7f, 1f, 0.7f));
            }
        }
    }
}
