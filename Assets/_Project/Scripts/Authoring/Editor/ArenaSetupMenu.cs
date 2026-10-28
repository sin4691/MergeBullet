using MergeBullet.Authoring;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeBullet.Editor
{
    /// <summary>
    /// 메뉴 한 번으로 아레나 SubScene, 총알 프리팹, 머티리얼을 만들고 현재 씬에 연결한다.
    /// SubScene은 에디터에서만 만들 수 있어서 코드로 자동화했다.
    /// </summary>
    static class ArenaSetupMenu
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string SubScenePath = ScenesDir + "/ArenaSubScene.unity";
        const float WallThickness = 0.2f;

        [MenuItem("MergeBullet/Create Arena SubScene")]
        static void CreateArenaSubScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) != null)
            {
                Debug.LogWarning($"{SubScenePath} 가 이미 있습니다. 다시 만들려면 먼저 삭제하세요.");
                return;
            }

            Scene mainScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(mainScene.path))
            {
                Debug.LogError("현재 씬을 먼저 저장하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureFolder(ScenesDir);

            Material bulletMaterial = CreateMaterial("Bullet", new Color(1f, 0.85f, 0.2f));
            Material wallMaterial = CreateMaterial("Wall", new Color(0.35f, 0.4f, 0.5f));
            GameObject bulletPrefab = CreateBulletPrefab(bulletMaterial);

            // SubScene 내용 구성: Arena(authoring) + 벽 4개
            Scene subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var arena = new GameObject("Arena");
            SceneManager.MoveGameObjectToScene(arena, subScene);
            var authoring = arena.AddComponent<ArenaAuthoring>();
            authoring.BulletPrefab = bulletPrefab;
            BuildWalls(arena.transform, authoring.ArenaSize, wallMaterial);
            EditorSceneManager.SaveScene(subScene, SubScenePath);
            EditorSceneManager.CloseScene(subScene, true);

            // 메인 씬에 SubScene 연결 + 카메라를 탑다운 직교로
            SceneManager.SetActiveScene(mainScene);
            var subSceneGo = new GameObject("ArenaSubScene");
            var subSceneComponent = subSceneGo.AddComponent<SubScene>();
            subSceneComponent.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
            FitCamera(authoring.ArenaSize);

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);
            Selection.activeGameObject = subSceneGo;
            Debug.Log("Arena SubScene 생성 완료. Play를 눌러 확인하세요.");
        }

        [MenuItem("MergeBullet/Create Max Level Effect")]
        static void CreateMaxLevelEffect()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Create Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);

            // 폭발 프리팹: 구 + ExplosionAuthoring
            Material material = CreateMaterial("Explosion", new Color(1f, 0.95f, 0.8f));
            GameObject go = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, "Explosion", material);
            go.AddComponent<ExplosionAuthoring>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/Explosion.prefab");
            Object.DestroyImmediate(go);

            // SubScene의 ArenaAuthoring에 연결. SubScene이 이미 열려 있으면 그대로 쓰고 닫지 않는다
            Scene subScene = SceneManager.GetSceneByPath(SubScenePath);
            bool wasLoaded = subScene.isLoaded;
            if (!wasLoaded) subScene = EditorSceneManager.OpenScene(SubScenePath, OpenSceneMode.Additive);

            ArenaAuthoring authoring = null;
            foreach (GameObject root in subScene.GetRootGameObjects())
            {
                authoring = root.GetComponentInChildren<ArenaAuthoring>();
                if (authoring != null) break;
            }

            if (authoring == null)
            {
                Debug.LogError($"{SubScenePath} 에서 ArenaAuthoring을 찾지 못했습니다.");
            }
            else
            {
                Undo.RecordObject(authoring, "Assign Max Level Effect");
                authoring.MaxLevelEffectPrefab = prefab;
                EditorUtility.SetDirty(authoring);
                EditorSceneManager.SaveScene(subScene);
                Debug.Log("Max Level Effect 생성 완료. Play를 눌러 확인하세요.");
            }

            if (!wasLoaded) EditorSceneManager.CloseScene(subScene, true);
        }

        static GameObject CreateBulletPrefab(Material material)
        {
            GameObject go = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, "Bullet", material);
            go.AddComponent<BulletAuthoring>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/Bullet.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildWalls(Transform parent, Vector2 arenaSize, Material material)
        {
            float w = arenaSize.x, h = arenaSize.y, t = WallThickness;

            // 벽 안쪽 면이 정확히 아레나 경계에 오도록 바깥으로 t/2 밀어 둔다
            CreateWall(parent, "Wall_Top", new Vector2(0f, (h + t) * 0.5f), new Vector2(w + t * 2f, t), material);
            CreateWall(parent, "Wall_Bottom", new Vector2(0f, -(h + t) * 0.5f), new Vector2(w + t * 2f, t), material);
            CreateWall(parent, "Wall_Left", new Vector2(-(w + t) * 0.5f, 0f), new Vector2(t, h), material);
            CreateWall(parent, "Wall_Right", new Vector2((w + t) * 0.5f, 0f), new Vector2(t, h), material);
        }

        static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size, Material material)
        {
            GameObject wall = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, name, material);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        // Unity Physics를 쓰지 않으므로 기본 Collider는 제거한다 (충돌은 시스템에서 직접 계산)
        static GameObject CreatePrimitiveWithoutCollider(PrimitiveType type, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void FitCamera(Vector2 arenaSize)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Undo.RecordObject(cam, "Fit Camera");
            Undo.RecordObject(cam.transform, "Fit Camera");
            cam.orthographic = true;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            const float margin = 1f;
            const float aspect = 16f / 9f; // 에디터에선 Game 뷰 비율을 알 수 없어 기준 비율로 맞춘다
            cam.orthographicSize = Mathf.Max(arenaSize.y * 0.5f + margin, (arenaSize.x * 0.5f + margin) / aspect);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
