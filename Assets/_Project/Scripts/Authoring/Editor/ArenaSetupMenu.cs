using MergeBullet.Authoring;
using MergeBullet.Components;
using MergeBullet.Presentation;
using MergeBullet.UI;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
        const string UiDir = Root + "/UI";
        const string ThemePath = UiDir + "/MergeBulletTheme.tss";
        const string HudLayoutPath = UiDir + "/GameHud.uxml";
        const string PanelSettingsPath = UiDir + "/GamePanelSettings.asset";
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

            EditArenaSubScene((subScene, arena) =>
            {
                Undo.RecordObject(arena, "Assign Max Level Effect");
                arena.MaxLevelEffectPrefab = prefab;
                EditorUtility.SetDirty(arena);
            });
            Debug.Log("Max Level Effect 생성 완료. Play를 눌러 확인하세요.");
        }

        [MenuItem("MergeBullet/Create Player")]
        static void CreatePlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Create Arena SubScene 을 실행하세요.");
                return;
            }

            Material material = CreateMaterial("Player", new Color(0.3f, 0.75f, 1f));
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Bullet.prefab");

            EditArenaSubScene((subScene, arena) =>
            {
                foreach (GameObject root in subScene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<PlayerAuthoring>() == null) continue;
                    Debug.LogWarning("SubScene에 이미 Player가 있습니다.");
                    return;
                }

                GameObject player = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Player", material);
                SceneManager.MoveGameObjectToScene(player, subScene);
                var authoring = player.AddComponent<PlayerAuthoring>();
                authoring.BulletPrefab = bulletPrefab;
                player.transform.localScale = Vector3.one * (authoring.Radius * 2f);
                Undo.RegisterCreatedObjectUndo(player, "Create Player");

                // 이제 총알은 플레이어가 쏘므로 시작 시 뿌리는 총알은 끈다 (스트레스 테스트할 때만 올려서 쓴다)
                Undo.RecordObject(arena, "Disable Initial Bullets");
                arena.BulletCount = 0;
                EditorUtility.SetDirty(arena);
            });
            Debug.Log("Player 생성 완료. Play 후 WASD로 움직여 보세요.");
        }

        [MenuItem("MergeBullet/Create Enemy")]
        static void CreateEnemy()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Create Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);

            // 적 프리팹: 정육면체 + EnemyAuthoring (스폰 시 45도 돌려 마름모로 보인다)
            Material material = CreateMaterial("Enemy", new Color(0.85f, 0.2f, 0.35f));
            GameObject go = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "Enemy", material);
            go.AddComponent<EnemyAuthoring>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/Enemy.prefab");
            Object.DestroyImmediate(go);

            EditArenaSubScene((subScene, arena) =>
            {
                Undo.RecordObject(arena, "Assign Enemy Prefab");
                arena.EnemyPrefab = prefab;
                EditorUtility.SetDirty(arena);
            });
            Debug.Log("Enemy 생성 완료. Play를 눌러 확인하세요.");
        }

        // 러너(작고 빠르고 약함)와 탱커(크고 느리고 단단함)를 만들어 스폰 테이블에 넣는다.
        // 둘 다 시작 가중치 0이라 처음엔 안 나오고, 난이도가 오를수록 섞여 나온다. 색은 모두 빨간 계열(= 적)
        [MenuItem("MergeBullet/Create Enemy Variants")]
        static void CreateEnemyVariants()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Create Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            var gem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/XpGem.prefab");

            GameObject runner = CreateEnemyVariantPrefab("Runner", new Color(1f, 0.4f, 0.45f), gem,
                moveSpeed: 3.5f, radius: 0.25f, maxHealth: 1f, contactDamage: 1f, xp: 1);
            GameObject tank = CreateEnemyVariantPrefab("Tank", new Color(0.55f, 0.08f, 0.15f), gem,
                moveSpeed: 1.2f, radius: 0.6f, maxHealth: 12f, contactDamage: 2f, xp: 4);

            EditArenaSubScene((subScene, arena) =>
            {
                Undo.RecordObject(arena, "Add Enemy Variants");
                SetVariant(arena, runner, startWeight: 0f, endWeight: 1f);
                SetVariant(arena, tank, startWeight: 0f, endWeight: 0.4f);
                EditorUtility.SetDirty(arena);
            });
            Debug.Log("Enemy Variants(Runner, Tank) 생성 완료. Play를 눌러 확인하세요.");
        }

        static GameObject CreateEnemyVariantPrefab(string name, Color color, GameObject gem,
            float moveSpeed, float radius, float maxHealth, float contactDamage, int xp)
        {
            Material material = CreateMaterial(name, color);
            GameObject go = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, name, material);
            var enemy = go.AddComponent<EnemyAuthoring>();
            enemy.MoveSpeed = moveSpeed;
            enemy.Radius = radius;
            enemy.MaxHealth = maxHealth;
            enemy.ContactDamage = contactDamage;
            enemy.XpGemPrefab = gem;
            enemy.XpValue = xp;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // 같은 프리팹이 이미 테이블에 있으면 가중치만 고친다 (메뉴를 다시 실행해도 중복되지 않게)
        static void SetVariant(ArenaAuthoring arena, GameObject prefab, float startWeight, float endWeight)
        {
            EnemyVariant variant = arena.EnemyVariants.Find(v => v.Prefab == prefab);
            if (variant == null)
            {
                variant = new EnemyVariant { Prefab = prefab };
                arena.EnemyVariants.Add(variant);
            }
            variant.StartWeight = startWeight;
            variant.EndWeight = endWeight;
        }

        // 젬 프리팹을 만들고 적 프리팹의 드롭으로 연결한다 (드롭은 적마다 다를 수 있어서 적 프리팹이 갖는다)
        [MenuItem("MergeBullet/Create XP Gem")]
        static void CreateXpGem()
        {
            const string enemyPrefabPath = PrefabsDir + "/Enemy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Create Enemy 를 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);

            Material material = CreateMaterial("XpGem", new Color(0.3f, 1f, 0.75f));
            GameObject go = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, "XpGem", material);
            go.transform.localScale = Vector3.one * 0.22f;
            go.AddComponent<XpGemAuthoring>();
            GameObject gemPrefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/XpGem.prefab");
            Object.DestroyImmediate(go);

            // 프리팹 에셋은 내용을 잠깐 열어서 고친 뒤 다시 저장한다
            GameObject enemy = PrefabUtility.LoadPrefabContents(enemyPrefabPath);
            try
            {
                enemy.GetComponent<EnemyAuthoring>().XpGemPrefab = gemPrefab;
                PrefabUtility.SaveAsPrefabAsset(enemy, enemyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(enemy);
            }
            Debug.Log("XP Gem 생성 완료. Play를 눌러 확인하세요.");
        }

        // 연출용 파티클 프리팹 4종을 만들고, 메인 씬에 EffectPresenter를 두어 연결한다.
        // 프리팹은 "반지름 1" 기준 크기라 EffectPresenter가 대상 크기만큼 늘려서 쓴다 (Scaling Mode: Hierarchy)
        [MenuItem("MergeBullet/Create Effects")]
        static void CreateEffects()
        {
            const string effectsDir = PrefabsDir + "/Effects";
            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureFolder(effectsDir);

            Material material = CreateParticleMaterial();
            var entries = new[]
            {
                EffectEntry(EffectType.Merge, CreateBurstPrefab(effectsDir, "MergeSpark", material,
                    count: 10, lifetime: 0.3f, speed: new Vector2(15f, 25f), size: new Vector2(0.6f, 1f)), volume: 0.25f),
                EffectEntry(EffectType.EnemyKilled, CreateBurstPrefab(effectsDir, "KillDebris", material,
                    count: 16, lifetime: 0.45f, speed: new Vector2(8f, 14f), size: new Vector2(0.3f, 0.5f)), volume: 0.35f),
                EffectEntry(EffectType.Explosion, CreateBurstPrefab(effectsDir, "ExplosionBurst", material,
                    count: 36, lifetime: 0.6f, speed: new Vector2(4f, 8f), size: new Vector2(0.12f, 0.2f)), volume: 0.6f),
                EffectEntry(EffectType.PlayerHit, CreateBurstPrefab(effectsDir, "PlayerHitBurst", material,
                    count: 12, lifetime: 0.35f, speed: new Vector2(8f, 14f), size: new Vector2(0.3f, 0.45f)), volume: 0.6f),
            };

            Scene mainScene = SceneManager.GetActiveScene();
            EffectPresenter presenter = Object.FindAnyObjectByType<EffectPresenter>();
            if (presenter == null)
            {
                var go = new GameObject("Effects");
                SceneManager.MoveGameObjectToScene(go, mainScene);
                Undo.RegisterCreatedObjectUndo(go, "Create Effects");
                presenter = go.AddComponent<EffectPresenter>();
            }

            Undo.RecordObject(presenter, "Setup Effects");
            presenter.Effects = entries;
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            EditorSceneManager.SaveScene(presenter.gameObject.scene);
            Debug.Log("Effects 생성 완료. Play를 눌러 확인하세요.");
        }

        static EffectPresenter.EffectEntry EffectEntry(EffectType type, ParticleSystem prefab, float volume)
            => new EffectPresenter.EffectEntry { Type = type, Prefab = prefab, Volume = volume };

        // URP 기본 파티클 머티리얼(부드러운 원, 반투명)을 복사해서 쓴다
        static Material CreateParticleMaterial()
        {
            string path = MaterialsDir + "/Particle.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            var material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 한 번 터지고 끝나는 파티클. 원(XY 평면)에서 사방으로 퍼지며, 점점 작아지고 투명해진다
        static ParticleSystem CreateBurstPrefab(string dir, string name, Material material,
            int count, float lifetime, Vector2 speed, Vector2 size)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = lifetime;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = count * 2;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            // 회전 없는 Circle 모양은 XY 평면 → 위에서 보는 카메라 기준으로 사방으로 퍼진다
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        // HUD는 MonoBehaviour로 남아 있어야 하므로 SubScene이 아니라 메인 씬에 둔다.
        // 이미 GameHud가 있으면(예전 IMGUI 버전) 새로 만들지 않고 UIDocument만 설정한다.
        [MenuItem("MergeBullet/Create HUD")]
        static void CreateHud()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudLayoutPath);
            if (theme == null || layout == null)
            {
                Debug.LogError($"{ThemePath} 또는 {HudLayoutPath} 를 찾지 못했습니다.");
                return;
            }

            PanelSettings panelSettings = LoadOrCreatePanelSettings(theme);

            Scene mainScene = SceneManager.GetActiveScene();
            GameHud hud = Object.FindAnyObjectByType<GameHud>();
            GameObject go;
            if (hud != null)
            {
                go = hud.gameObject;
            }
            else
            {
                go = new GameObject("GameHud");
                SceneManager.MoveGameObjectToScene(go, mainScene);
                Undo.RegisterCreatedObjectUndo(go, "Create HUD");
            }

            UIDocument document = go.GetComponent<UIDocument>();
            if (document == null) document = Undo.AddComponent<UIDocument>(go);
            Undo.RecordObject(document, "Setup HUD Document");
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            if (hud == null) Undo.AddComponent<GameHud>(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveScene(go.scene);
            Debug.Log("HUD 설정 완료. Play를 눌러 확인하세요.");
        }

        // 기준 해상도 1920x1080에 맞춰 화면 크기에 따라 UI 전체를 확대·축소한다 (PC·모바일 공용)
        static PanelSettings LoadOrCreatePanelSettings(ThemeStyleSheet theme)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        /// <summary>
        /// 아레나 SubScene을 (닫혀 있으면 잠깐 열어서) 수정하고 저장한다.
        /// 이미 열려 있으면 그대로 쓰고 닫지 않는다.
        /// </summary>
        static void EditArenaSubScene(System.Action<Scene, ArenaAuthoring> edit)
        {
            Scene subScene = SceneManager.GetSceneByPath(SubScenePath);
            bool wasLoaded = subScene.isLoaded;
            if (!wasLoaded) subScene = EditorSceneManager.OpenScene(SubScenePath, OpenSceneMode.Additive);

            ArenaAuthoring arena = null;
            foreach (GameObject root in subScene.GetRootGameObjects())
            {
                arena = root.GetComponentInChildren<ArenaAuthoring>();
                if (arena != null) break;
            }

            if (arena == null)
                Debug.LogError($"{SubScenePath} 에서 ArenaAuthoring을 찾지 못했습니다.");
            else
            {
                edit(subScene, arena);
                EditorSceneManager.SaveScene(subScene);
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
            // 기본 하늘색 배경은 총알·적 색과 대비가 약해서 검은색 단색으로 둔다
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
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
