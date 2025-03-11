using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AvatarCostumeAdjustTool
{
    /// <summary>
    /// エディタでのリアルタイムプレビュー機能を管理するクラス
    /// </summary>
    public class PreviewManager
    {
        // シングルトンインスタンス
        private static PreviewManager _instance;
        public static PreviewManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PreviewManager();
                }
                return _instance;
            }
        }

        // プレビュー対象のアバター
        private GameObject _previewAvatar;
        public GameObject PreviewAvatar
        {
            get { return _previewAvatar; }
            set 
            { 
                _previewAvatar = value;
                if (_previewAvatar != null)
                {
                    // アバターの初期状態を記録
                    _originalAvatarTransform = new TransformData(_previewAvatar.transform);
                    _originalAvatarSkinnedMeshes = GetSkinnedMeshesData(_previewAvatar);
                }
            }
        }

        // プレビュー対象の衣装
        private GameObject _previewCostume;
        public GameObject PreviewCostume
        {
            get { return _previewCostume; }
            set 
            { 
                _previewCostume = value;
                if (_previewCostume != null)
                {
                    // 衣装の初期状態を記録
                    _originalCostumeTransform = new TransformData(_previewCostume.transform);
                    _originalCostumeSkinnedMeshes = GetSkinnedMeshesData(_previewCostume);
                }
            }
        }

        // 適用されたプレビュー衣装のインスタンス
        private GameObject _appliedCostumeInstance;

        // エディタのリアルタイム更新用のデリゲート
        private System.Action<SceneView> _sceneViewUpdateCallback;

        // 初期状態のトランスフォーム情報
        private TransformData _originalAvatarTransform;
        private TransformData _originalCostumeTransform;

        // 初期状態のスキンメッシュデータ
        private List<SkinnedMeshData> _originalAvatarSkinnedMeshes;
        private List<SkinnedMeshData> _originalCostumeSkinnedMeshes;

        // アニメーション用
        private float _currentRotation = 0f;
        private bool _autoRotate = false;
        public bool AutoRotate
        {
            get { return _autoRotate; }
            set { _autoRotate = value; }
        }

        // プレビュー設定
        private bool _showBones = false;
        public bool ShowBones
        {
            get { return _showBones; }
            set { _showBones = value; }
        }

        private bool _showWireframe = false;
        public bool ShowWireframe
        {
            get { return _showWireframe; }
            set { _showWireframe = value; }
        }

        private float _previewOpacity = 0.5f;
        public float PreviewOpacity
        {
            get { return _previewOpacity; }
            set { _previewOpacity = value; }
        }

        private bool _previewEnabled = true;
        public bool PreviewEnabled
        {
            get { return _previewEnabled; }
            set { _previewEnabled = value; }
        }

        private Color _wireframeColor = new Color(0f, 0.7f, 1f, 0.5f);
        public Color WireframeColor
        {
            get { return _wireframeColor; }
            set { _wireframeColor = value; }
        }

        // 背景色
        private Color _backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set { _backgroundColor = value; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        private PreviewManager()
        {
            // コールバックの初期化
            _sceneViewUpdateCallback = OnSceneViewUpdate;
        }

        /// <summary>
        /// プレビューの表示/非表示を設定（静的メソッド）
        /// </summary>
        public static void SetPreviewEnabled(bool enabled)
        {
            Instance.PreviewEnabled = enabled;
        }

        /// <summary>
        /// プレビューの透明度を設定（静的メソッド）
        /// </summary>
        public static void SetPreviewOpacity(float opacity)
        {
            Instance.PreviewOpacity = opacity;
        }

        /// <summary>
        /// ワイヤーフレームモードを設定（静的メソッド）
        /// </summary>
        public static void SetWireframeMode(bool enabled)
        {
            Instance.ShowWireframe = enabled;
        }

        /// <summary>
        /// プレビューの開始
        /// </summary>
        public void StartPreview()
        {
            if (_previewAvatar == null)
            {
                Debug.LogWarning("プレビュー対象のアバターが設定されていません。");
                return;
            }

            // シーンビューのイベントに登録
            SceneView.duringSceneGui += _sceneViewUpdateCallback;

            // シーンビューのカメラ位置調整
            FocusSceneViewOnAvatar();

            Debug.Log("プレビューを開始しました。");
        }

        /// <summary>
        /// プレビューの終了
        /// </summary>
        public void StopPreview()
        {
            // シーンビューのイベントから解除
            SceneView.duringSceneGui -= _sceneViewUpdateCallback;

            // 衣装インスタンスの削除
            if (_appliedCostumeInstance != null)
            {
                Object.DestroyImmediate(_appliedCostumeInstance);
                _appliedCostumeInstance = null;
            }

            Debug.Log("プレビューを終了しました。");
        }

        /// <summary>
        /// 衣装をアバターに適用
        /// </summary>
        public void ApplyCostumeToAvatar(MappingData mappingData, AdjustmentSettings adjustmentSettings)
        {
            if (_previewAvatar == null || _previewCostume == null)
            {
                Debug.LogWarning("アバターまたは衣装が設定されていません。");
                return;
            }

            // 既存のインスタンスを削除
            if (_appliedCostumeInstance != null)
            {
                Object.DestroyImmediate(_appliedCostumeInstance);
            }

            // 衣装のインスタンスを作成
            _appliedCostumeInstance = Object.Instantiate(_previewCostume);
            _appliedCostumeInstance.name = _previewCostume.name + " (Applied)";
            _appliedCostumeInstance.transform.SetParent(_previewAvatar.transform);

            // ボーンマッピングを適用
            ApplyBoneMapping(_appliedCostumeInstance, _previewAvatar, mappingData);

            // 微調整設定を適用
            ApplyAdjustmentSettings(_appliedCostumeInstance, adjustmentSettings);

            Debug.Log("衣装をアバターに適用しました。");
        }

        /// <summary>
        /// ボーンマッピングを適用
        /// </summary>
        private void ApplyBoneMapping(GameObject costume, GameObject avatar, MappingData mappingData)
        {
            if (mappingData == null || mappingData.BoneMappings == null || mappingData.BoneMappings.Count == 0)
            {
                Debug.LogWarning("ボーンマッピングデータが無効です。");
                return;
            }

            // アバターと衣装のボーン取得
            Dictionary<string, Transform> avatarBones = GetAllBones(avatar);
            Dictionary<string, Transform> costumeBones = GetAllBones(costume);

            // ボーンマッピング情報からディクショナリを作成
            Dictionary<Transform, Transform> boneMapping = new Dictionary<Transform, Transform>();
            foreach (var mapping in mappingData.BoneMappings)
            {
                if (avatarBones.TryGetValue(mapping.AvatarBonePath, out Transform avatarBone) &&
                    costumeBones.TryGetValue(mapping.CostumeBonePath, out Transform costumeBone))
                {
                    boneMapping[costumeBone] = avatarBone;
                }
            }

            // スキンメッシュレンダラーの更新
            SkinnedMeshRenderer[] skinnedMeshes = costume.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMesh in skinnedMeshes)
            {
                MeshUtils.AdjustBoneWeights(skinnedMesh, boneMapping);
            }
        }

        /// <summary>
        /// 微調整設定を適用
        /// </summary>
        private void ApplyAdjustmentSettings(GameObject costume, AdjustmentSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning("調整設定が無効です。");
                return;
            }

            // 全体スケール
            costume.transform.localScale = Vector3.one * settings.GlobalScale;

            // 位置オフセット
            costume.transform.localPosition = settings.PositionOffset;

            // 回転オフセット
            costume.transform.localRotation = Quaternion.Euler(settings.RotationOffset);

            // 部位ごとの調整（簡易実装）
            // 完全実装では、スキンメッシュの部位ごとの変形が必要
            if (settings.BodyPartAdjustments != null && settings.BodyPartAdjustments.Count > 0)
            {
                // スキンメッシュごとに処理
                SkinnedMeshRenderer[] skinnedMeshes = costume.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skinnedMesh in skinnedMeshes)
                {
                    if (skinnedMesh.sharedMesh != null)
                    {
                        // メッシュのコピーを作成
                        Mesh meshCopy = MeshUtils.DuplicateMesh(skinnedMesh.sharedMesh);

                        // 体の部位ごとの頂点分類を取得
                        BodyPartReferenceData bodyPartData = DataManager.Instance.BodyPartReferenceData;
                        Dictionary<string, List<int>> bodyPartVertices = MeshUtils.ClassifyMeshByBodyPart(
                            meshCopy, skinnedMesh.bones, bodyPartData);

                        // 部位ごとのスケール係数を設定
                        Dictionary<string, Vector3> scaleFactors = new Dictionary<string, Vector3>();
                        foreach (var adjustment in settings.BodyPartAdjustments)
                        {
                            scaleFactors[adjustment.BodyPart] = adjustment.Scale;
                        }

                        // メッシュを変形
                        MeshUtils.DeformMeshByBodyParts(meshCopy, bodyPartVertices, scaleFactors);

                        // 変更したメッシュをレンダラーに設定
                        skinnedMesh.sharedMesh = meshCopy;
                    }
                }
            }
        }

        /// <summary>
        /// シーンビューの更新イベント
        /// </summary>
        private void OnSceneViewUpdate(SceneView sceneView)
        {
            if (_previewAvatar == null || !_previewEnabled)
                return;

            // 背景色の設定
            sceneView.sceneViewState.showSkybox = false;
            Handles.DrawSolidRectangleWithOutline(
                new Rect(0, 0, sceneView.position.width, sceneView.position.height),
                _backgroundColor,
                Color.clear);

            // 自動回転
            if (_autoRotate)
            {
                _currentRotation += Time.deltaTime * 20f;
                _previewAvatar.transform.rotation = Quaternion.Euler(0, _currentRotation, 0);
            }

            // ボーンの描画
            if (_showBones && _previewAvatar != null)
            {
                var animator = _previewAvatar.GetComponent<Animator>();
                if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                {
                    EditorUtils.DrawHumanoidBones(animator);
                }
                else
                {
                    EditorUtils.DrawBoneConnections(_previewAvatar.transform);
                }
            }

            // ワイヤーフレームの描画
            if (_showWireframe)
            {
                // 透明度を適用した色を使用
                Color wireColor = new Color(_wireframeColor.r, _wireframeColor.g, _wireframeColor.b, _previewOpacity);
                
                DrawWireframe(_previewAvatar, wireColor);
                if (_appliedCostumeInstance != null)
                {
                    Color costumeWireColor = new Color(wireColor.r, wireColor.g, wireColor.b, wireColor.a * 0.6f);
                    DrawWireframe(_appliedCostumeInstance, costumeWireColor);
                }
            }

            // シーンビューの表示設定
            sceneView.sceneLighting = true;
            sceneView.sceneViewState.showFog = false;
            sceneView.sceneViewState.showSkybox = false;
            sceneView.sceneViewState.showFlares = false;
            sceneView.sceneViewState.showImageEffects = true;
            sceneView.sceneViewState.showParticleSystems = false;

            // シーンビューを再描画
            sceneView.Repaint();
        }

        /// <summary>
        /// ワイヤーフレームの描画
        /// </summary>
        private void DrawWireframe(GameObject obj, Color color)
        {
            if (obj == null)
                return;

            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    EditorUtils.WithHandlesColor(color, () =>
                    {
                        Handles.matrix = renderer.transform.localToWorldMatrix;
                        EditorUtils.DrawWireMesh(meshFilter.sharedMesh, Matrix4x4.identity, color);
                    });
                }
                else if (renderer is SkinnedMeshRenderer skinnedMesh && skinnedMesh.sharedMesh != null)
                {
                    EditorUtils.WithHandlesColor(color, () =>
                    {
                        Handles.matrix = renderer.transform.localToWorldMatrix;
                        EditorUtils.DrawWireMesh(skinnedMesh.sharedMesh, Matrix4x4.identity, color);
                    });
                }
            }

            // 行列をリセット
            Handles.matrix = Matrix4x4.identity;
        }

        /// <summary>
        /// シーンビューのカメラをアバターにフォーカス
        /// </summary>
        private void FocusSceneViewOnAvatar()
        {
            if (_previewAvatar == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            // アバターのバウンディングボックスを計算
            Bounds bounds = EditorUtils.GetAvatarBounds(_previewAvatar);

            // バウンディングボックスの中心にフォーカス
            sceneView.Frame(bounds, false);

            // カメラ位置をアバターの前に設定
            Vector3 cameraPosition = bounds.center + new Vector3(0, 0, -2f * bounds.size.magnitude);
            sceneView.LookAt(bounds.center, Quaternion.LookRotation(bounds.center - cameraPosition), bounds.size.magnitude);
            
            // FOVを調整して全体が見えるようにする
            sceneView.size = bounds.size.magnitude * 0.8f;
        }

        /// <summary>
        /// オブジェクトからすべてのボーンを取得
        /// </summary>
        private Dictionary<string, Transform> GetAllBones(GameObject obj)
        {
            Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            Transform[] transforms = obj.GetComponentsInChildren<Transform>(true);

            foreach (var transform in transforms)
            {
                string path = GetTransformPath(transform, obj.transform);
                bones[path] = transform;
            }

            return bones;
        }

        /// <summary>
        /// トランスフォームのパスを取得
        /// </summary>
        private string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == root)
                return "";

            if (transform.parent == root)
                return transform.name;

            return GetTransformPath(transform.parent, root) + "/" + transform.name;
        }

        /// <summary>
        /// スキンメッシュデータを取得
        /// </summary>
        private List<SkinnedMeshData> GetSkinnedMeshesData(GameObject obj)
        {
            List<SkinnedMeshData> result = new List<SkinnedMeshData>();
            SkinnedMeshRenderer[] renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var renderer in renderers)
            {
                SkinnedMeshData data = new SkinnedMeshData
                {
                    Path = GetTransformPath(renderer.transform, obj.transform),
                    Mesh = renderer.sharedMesh,
                    Materials = renderer.sharedMaterials.ToArray(),
                    RootBone = renderer.rootBone,
                    Bones = renderer.bones.ToArray(),
                    BoundingVolume = renderer.localBounds
                };

                result.Add(data);
            }

            return result;
        }

        /// <summary>
        /// アバターを元の状態に戻す
        /// </summary>
        public void RestoreOriginalAvatar()
        {
            if (_previewAvatar != null && _originalAvatarTransform != null)
            {
                _originalAvatarTransform.ApplyTo(_previewAvatar.transform);
                RestoreOriginalSkinnedMeshes(_previewAvatar, _originalAvatarSkinnedMeshes);
            }
        }

        /// <summary>
        /// 衣装を元の状態に戻す
        /// </summary>
        public void RestoreOriginalCostume()
        {
            if (_previewCostume != null && _originalCostumeTransform != null)
            {
                _originalCostumeTransform.ApplyTo(_previewCostume.transform);
                RestoreOriginalSkinnedMeshes(_previewCostume, _originalCostumeSkinnedMeshes);
            }
        }

        /// <summary>
        /// スキンメッシュレンダラーを元の状態に戻す
        /// </summary>
        private void RestoreOriginalSkinnedMeshes(GameObject obj, List<SkinnedMeshData> originalData)
        {
            if (originalData == null)
                return;

            Dictionary<string, Transform> pathToTransform = new Dictionary<string, Transform>();
            Transform[] transforms = obj.GetComponentsInChildren<Transform>(true);
            
            foreach (var transform in transforms)
            {
                string path = GetTransformPath(transform, obj.transform);
                pathToTransform[path] = transform;
            }

            foreach (var data in originalData)
            {
                if (pathToTransform.TryGetValue(data.Path, out Transform transform))
                {
                    SkinnedMeshRenderer renderer = transform.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMesh = data.Mesh;
                        renderer.sharedMaterials = data.Materials;
                        renderer.rootBone = data.RootBone;
                        renderer.bones = data.Bones;
                        renderer.localBounds = data.BoundingVolume;
                    }
                }
            }
        }
    }

    /// <summary>
    /// トランスフォームのデータ保存用クラス
    /// </summary>
    public class TransformData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public TransformData(Transform transform)
        {
            Position = transform.position;
            Rotation = transform.rotation;
            Scale = transform.localScale;
        }

        public void ApplyTo(Transform transform)
        {
            transform.position = Position;
            transform.rotation = Rotation;
            transform.localScale = Scale;
        }
    }

    /// <summary>
    /// スキンメッシュデータ保存用クラス
    /// </summary>
    public class SkinnedMeshData
    {
        public string Path;
        public Mesh Mesh;
        public Material[] Materials;
        public Transform RootBone;
        public Transform[] Bones;
        public Bounds BoundingVolume;
    }
}