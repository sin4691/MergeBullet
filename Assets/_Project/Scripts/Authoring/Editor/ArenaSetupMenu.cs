using MergeBullet.Authoring;
using MergeBullet.Components;
using MergeBullet.Presentation;
using MergeBullet.Systems;
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
    /// 프로젝트를 처음 구성하는 메뉴(MergeBullet/Setup)와 값을 바꾼 뒤 다시 돌리는 메뉴(MergeBullet/Update).
    /// SubScene·프리팹·머티리얼·씬 연결을 코드로 만들어서, 무엇을 어떻게 구성했는지 코드에 남고 다시 만들 수 있다.
    /// 메뉴 목록과 순서는 docs/editor-menus.md.
    /// </summary>
    static class ArenaSetupMenu
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string MeshesDir = Root + "/Meshes";
        const string ScenesDir = Root + "/Scenes";
        const string SubScenePath = ScenesDir + "/ArenaSubScene.unity";
        const string UiDir = Root + "/UI";
        const string ThemePath = UiDir + "/MergeBulletTheme.tss";
        const string HudLayoutPath = UiDir + "/GameHud.uxml";
        const string PanelSettingsPath = UiDir + "/GamePanelSettings.asset";
        const string LobbyLayoutPath = UiDir + "/Lobby.uxml";
        const string GameScenePath = ScenesDir + "/Game.unity";
        const string LobbyScenePath = ScenesDir + "/Lobby.unity";

        [MenuItem("MergeBullet/Setup/Arena SubScene", priority = 1)]
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

            // SubScene 내용 구성: Arena(역할별 authoring) + 벽
            Scene subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var arena = new GameObject("Arena");
            SceneManager.MoveGameObjectToScene(arena, subScene);
            var layout = arena.AddComponent<ArenaLayoutAuthoring>();
            arena.AddComponent<CameraViewAuthoring>();
            arena.AddComponent<BulletRulesAuthoring>().BulletPrefab = bulletPrefab;
            arena.AddComponent<MaxLevelEffectAuthoring>();
            arena.AddComponent<EnemySpawnAuthoring>();
            arena.AddComponent<ProgressionAuthoring>();
            arena.AddComponent<BenchmarkAuthoring>();
            arena.AddComponent<GameSessionAuthoring>();
            BuildLayout(layout, wallMaterial, CreateMaterial("Gate", GateColor));
            EditorSceneManager.SaveScene(subScene, SubScenePath);
            EditorSceneManager.CloseScene(subScene, true);

            // 메인 씬에 SubScene 연결 + 카메라를 탑다운 직교로
            SceneManager.SetActiveScene(mainScene);
            var subSceneGo = new GameObject("ArenaSubScene");
            var subSceneComponent = subSceneGo.AddComponent<SubScene>();
            subSceneComponent.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
            FitCamera(layout.ArenaSize);

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);
            Selection.activeGameObject = subSceneGo;
            Debug.Log("Arena SubScene 생성 완료. Play를 눌러 확인하세요.");
        }

        [MenuItem("MergeBullet/Setup/Max Level Effect", priority = 7)]
        static void CreateMaxLevelEffect()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Arena SubScene 을 실행하세요.");
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

            EditArenaSubScene<MaxLevelEffectAuthoring>((subScene, effect) =>
            {
                Undo.RecordObject(effect, "Assign Max Level Effect");
                effect.MaxLevelEffectPrefab = prefab;
                EditorUtility.SetDirty(effect);
            });
            Debug.Log("Max Level Effect 생성 완료. Play를 눌러 확인하세요.");
        }

        [MenuItem("MergeBullet/Setup/Player", priority = 2)]
        static void CreatePlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Arena SubScene 을 실행하세요.");
                return;
            }

            Material material = CreateMaterial("Player", new Color(0.3f, 0.75f, 1f));
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Bullet.prefab");

            EditArenaSubScene<BulletRulesAuthoring>((subScene, bullets) =>
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
                Undo.RecordObject(bullets, "Disable Initial Bullets");
                bullets.BulletCount = 0;
                EditorUtility.SetDirty(bullets);
            });
            Debug.Log("Player 생성 완료. Play 후 WASD로 움직여 보세요.");
        }

        [MenuItem("MergeBullet/Setup/Enemy", priority = 3)]
        static void CreateEnemy()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);

            // 기본 적 = 사각형 등급 (나머지 등급은 Update/Enemy Tiers가 만든다)
            var gem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/XpGem.prefab");
            GameObject prefab = CreateTierPrefab(Tiers[BasicTierIndex], gem);

            EditArenaSubScene<EnemySpawnAuthoring>((subScene, enemies) =>
            {
                Undo.RecordObject(enemies, "Assign Enemy Prefab");
                enemies.EnemyPrefab = prefab;
                EditorUtility.SetDirty(enemies);
            });
            Debug.Log("Enemy 생성 완료. Play를 눌러 확인하세요.");
        }

        // 적 등급: 변이 많을수록 세다 (삼각형 → 사각형 → 오각형 → 육각형). 색은 모두 빨간 계열(= 적)이고 셀수록 진하다.
        // 이름, 변, 색, 이동 속도, 크기(변까지 거리), 체력, 접촉 피해, 경험치, 등장 가중치(처음 → 후반)
        static readonly (string Name, int Sides, Color Color, float Speed, float Radius, float Health, float Damage, int Xp,
            float StartWeight, float EndWeight)[] Tiers =
        {
            ("Triangle", 3, new Color(1f, 0.5f, 0.55f), 3.4f, 0.22f, 1f, 10f, 1, 1f, 0.6f),   // 작고 빠르고 약한 떼
            ("Enemy", 4, new Color(0.9f, 0.22f, 0.35f), 2f, 0.35f, 3f, 10f, 2, 1f, 1f),       // 기본 적 (Arena의 EnemyPrefab, 가중치 1 고정)
            ("Pentagon", 5, new Color(0.72f, 0.12f, 0.28f), 1.6f, 0.45f, 8f, 15f, 4, 0f, 0.6f), // 중간
            ("Hexagon", 6, new Color(0.5f, 0.06f, 0.2f), 1.1f, 0.6f, 20f, 20f, 8, 0f, 0.4f),   // 크고 느리고 단단함
        };
        const int BasicTierIndex = 1;
        const int BossSides = 8;
        // 등급 도입 전의 적 프리팹. 스폰 테이블에서 빼고 지운다
        static readonly string[] LegacyEnemyPrefabs = { "Runner", "Tank" };

        // 등급별 적 프리팹을 (다시) 만들고 스폰 테이블을 바꾼다. 보스도 정다각형(8각)으로 맞춘다
        [MenuItem("MergeBullet/Update/Enemy Tiers", priority = 102)]
        static void BuildEnemyTiers()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            var gem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/XpGem.prefab");

            var prefabs = new GameObject[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++) prefabs[i] = CreateTierPrefab(Tiers[i], gem);
            UpdateBoss(prefabs[0]);

            EditArenaSubScene<EnemySpawnAuthoring>((subScene, enemies) =>
            {
                Undo.RecordObject(enemies, "Enemy Tiers");
                enemies.EnemyPrefab = prefabs[BasicTierIndex];
                // 예전 Runner·Tank를 빼고 새 등급만 넣는다
                enemies.EnemyVariants.Clear();
                for (int i = 0; i < Tiers.Length; i++)
                    if (i != BasicTierIndex) SetVariant(enemies, prefabs[i], Tiers[i].StartWeight, Tiers[i].EndWeight);
                EditorUtility.SetDirty(enemies);
            });

            // 스폰 테이블에서 뺀 뒤에 지워야 참조가 깨지지 않는다
            foreach (string legacy in LegacyEnemyPrefabs)
            {
                string path = $"{PrefabsDir}/{legacy}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            }
            Debug.Log("적 등급(삼각형·사각형·오각형·육각형) 생성 완료. Play를 눌러 확인하세요.");
        }

        static GameObject CreateTierPrefab(
            (string Name, int Sides, Color Color, float Speed, float Radius, float Health, float Damage, int Xp,
                float StartWeight, float EndWeight) tier, GameObject gem)
            => CreateEnemyVariantPrefab(tier.Name, tier.Color, gem, tier.Speed, tier.Radius, tier.Health, tier.Damage, tier.Xp, tier.Sides);

        // 이미 있는 보스 프리팹의 모양을 정다각형으로 바꾸고, 소환할 부하(가장 약한 등급)를 연결한다 (능력치는 그대로)
        static void UpdateBoss(GameObject summon)
        {
            string path = PrefabsDir + "/Boss.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<EnemyAuthoring>().Sides = BossSides;
                contents.GetComponent<MeshFilter>().sharedMesh = LoadOrCreatePolygonMesh(BossSides);
                BossAuthoring bossAuthoring = contents.GetComponent<BossAuthoring>();
                if (bossAuthoring != null) bossAuthoring.SummonPrefab = summon;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static GameObject CreateEnemyVariantPrefab(string name, Color color, GameObject gem,
            float moveSpeed, float radius, float maxHealth, float contactDamage, int xp, int sides)
        {
            // 정다각형 메시 + 단색 머티리얼. Unity Physics를 쓰지 않으니 콜라이더는 없다
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = LoadOrCreatePolygonMesh(sides);
            go.AddComponent<MeshRenderer>().sharedMaterial = CreateMaterial(name, color);
            var enemy = go.AddComponent<EnemyAuthoring>();
            enemy.MoveSpeed = moveSpeed;
            enemy.Radius = radius;
            enemy.Sides = sides;
            enemy.MaxHealth = maxHealth;
            enemy.ContactDamage = contactDamage;
            enemy.XpGemPrefab = gem;
            enemy.XpValue = xp;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// 변이 sides개인 납작한 정다각형 메시. 중심에서 변까지 거리 0.5라 스폰할 때 2 × 반지름으로 키우면 변까지 거리 = 반지름이 된다
        /// (총알 판정 BulletEnemyCollisionSystem.ClosestOnPolygon과 같은 모양). 정면(+x)에 무엇이 오는지는 EnemyShape.FacingOffset
        /// (홀수 변은 꼭짓점, 짝수 변은 면). 이미 있으면 내용만 다시 써서 GUID(프리팹의 참조)를 유지한다.
        /// </summary>
        static Mesh LoadOrCreatePolygonMesh(int sides)
        {
            EnsureFolder(MeshesDir);
            string path = $"{MeshesDir}/Polygon{sides}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = $"Polygon{sides}" };
            else mesh.Clear();

            float step = 2f * Mathf.PI / sides;
            float offset = EnemyShape.FacingOffset(sides);
            float cornerDistance = 0.5f / Mathf.Cos(Mathf.PI / sides);
            var vertices = new Vector3[sides + 1];
            var normals = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            normals[0] = Vector3.back; // 카메라(-z 쪽)를 향한다
            for (int k = 0; k < sides; k++)
            {
                float angle = (k + 0.5f) * step + offset;
                vertices[k + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * cornerDistance;
                normals[k + 1] = Vector3.back;
                // 카메라에서 볼 때 시계 방향이어야 앞면 (중심 → 다음 꼭짓점 → 이번 꼭짓점)
                triangles[k * 3] = 0;
                triangles[k * 3 + 1] = (k + 1) % sides + 1;
                triangles[k * 3 + 2] = k + 1;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // 같은 프리팹이 이미 테이블에 있으면 가중치만 고친다 (메뉴를 다시 실행해도 중복되지 않게)
        static void SetVariant(EnemySpawnAuthoring enemies, GameObject prefab, float startWeight, float endWeight)
        {
            EnemyVariant variant = enemies.EnemyVariants.Find(v => v.Prefab == prefab);
            if (variant == null)
            {
                variant = new EnemyVariant { Prefab = prefab };
                enemies.EnemyVariants.Add(variant);
            }
            variant.StartWeight = startWeight;
            variant.EndWeight = endWeight;
        }

        // 젬 프리팹을 만들고 적 프리팹의 드롭으로 연결한다 (드롭은 적마다 다를 수 있어서 적 프리팹이 갖는다)
        [MenuItem("MergeBullet/Setup/XP Gem", priority = 5)]
        static void CreateXpGem()
        {
            const string enemyPrefabPath = PrefabsDir + "/Enemy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Enemy 를 실행하세요.");
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
        [MenuItem("MergeBullet/Setup/Effects", priority = 8)]
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
                // 번개는 파티클 대신 선으로 그린다 (Position → Target)
                new EffectPresenter.EffectEntry
                {
                    Type = EffectType.Lightning,
                    Line = CreateLinePrefab(effectsDir, "LightningBolt", material, width: 0.08f),
                    Volume = 0.35f,
                    MaxPerFrame = 20,
                },
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

        [MenuItem("MergeBullet/Setup/Boss Telegraph", priority = 9)]
        static void CreateBossTelegraph()
        {
            EffectPresenter presenter = Object.FindAnyObjectByType<EffectPresenter>();
            if (presenter == null)
            {
                Debug.LogError("게임 씬에 Effects(EffectPresenter)가 없습니다. Game 씬을 열고 MergeBullet/Setup/Effects를 먼저 실행하세요.");
                return;
            }

            // 파티클 머티리얼(정점 색·반투명 지원)을 복사하되 텍스처를 빼서 끝까지 고른 굵기의 선이 되게 한다
            string path = MaterialsDir + "/Telegraph.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(CreateParticleMaterial());
                material.SetTexture("_BaseMap", null);
                AssetDatabase.CreateAsset(material, path);
            }

            BossTelegraph telegraph = presenter.GetComponent<BossTelegraph>();
            if (telegraph == null) telegraph = Undo.AddComponent<BossTelegraph>(presenter.gameObject);
            Undo.RecordObject(telegraph, "Setup Boss Telegraph");
            telegraph.LineMaterial = material;
            EditorUtility.SetDirty(telegraph);
            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            EditorSceneManager.SaveScene(presenter.gameObject.scene);
            Debug.Log("보스 돌진 예고선 설정 완료.");
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

        // 번개 등 선 연출용. 점 위치는 EffectPresenter가 매번 정한다
        static LineRenderer CreateLinePrefab(string dir, string name, Material material, float width)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.enabled = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<LineRenderer>();
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

        // 로비 씬(카메라 + 로비 화면)을 만들고, 빌드 씬 목록을 로비 → 게임 순으로 (첫 화면이 로비)
        [MenuItem("MergeBullet/Setup/Lobby Scene And Build List", priority = 11)]
        static void SetupLobbyScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(ScenesDir);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyScenePath) == null && !CreateLobbyScene()) return;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            Debug.Log("Lobby/Game 씬 설정 완료. Lobby 씬을 열고 Play를 눌러 확인하세요.");
        }

        static bool CreateLobbyScene()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LobbyLayoutPath);
            if (theme == null || layout == null)
            {
                Debug.LogError($"{ThemePath} 또는 {LobbyLayoutPath} 를 찾지 못했습니다.");
                return false;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // 게임 씬과 같은 검은 배경 카메라 (UI만 있지만 카메라가 없으면 화면이 비어 보인다)
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cameraObject.AddComponent<AudioListener>();

            var lobby = new GameObject("Lobby");
            SceneManager.MoveGameObjectToScene(lobby, scene);
            var document = lobby.AddComponent<UIDocument>();
            document.panelSettings = LoadOrCreatePanelSettings(theme);
            document.visualTreeAsset = layout;
            lobby.AddComponent<LobbyScreen>();

            bool saved = EditorSceneManager.SaveScene(scene, LobbyScenePath);
            EditorSceneManager.CloseScene(scene, true);
            return saved;
        }

        // 보스 프리팹(크고 단단한 적 + 돌진 패턴)을 만들고 Arena의 보스 스포너에 연결한다
        [MenuItem("MergeBullet/Setup/Boss", priority = 6)]
        static void CreateBoss()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) == null)
            {
                Debug.LogError("먼저 MergeBullet/Setup/Arena SubScene 을 실행하세요.");
                return;
            }

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            var gem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/XpGem.prefab");

            // 빨간 계열(= 적)이지만 일반 적보다 진하고 크게
            GameObject boss = CreateEnemyVariantPrefab("Boss", new Color(0.75f, 0.05f, 0.25f), gem,
                moveSpeed: 1.4f, radius: 1f, maxHealth: 300f, contactDamage: 30f, xp: 20, sides: BossSides);
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabsDir + "/Boss.prefab");
            try
            {
                if (contents.GetComponent<BossAuthoring>() == null) contents.AddComponent<BossAuthoring>();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabsDir + "/Boss.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            EditArenaSubScene<EnemySpawnAuthoring>((subScene, enemies) =>
            {
                Undo.RecordObject(enemies, "Assign Boss");
                enemies.BossPrefab = boss;
                EditorUtility.SetDirty(enemies);
            });
            Debug.Log("Boss 생성 완료. 생존 시간 Boss Interval(기본 180초)마다 나옵니다.");
        }

        // HUD는 MonoBehaviour로 남아 있어야 하므로 SubScene이 아니라 메인 씬에 둔다.
        // 이미 GameHud가 있으면(예전 IMGUI 버전) 새로 만들지 않고 UIDocument만 설정한다.
        [MenuItem("MergeBullet/Setup/HUD", priority = 10)]
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
        /// 아레나 SubScene을 (닫혀 있으면 잠깐 열어서) 그 안의 T(Arena 오브젝트의 authoring 하나)를 수정하고 저장한다.
        /// 이미 열려 있으면 그대로 쓰고 닫지 않는다.
        /// </summary>
        static void EditArenaSubScene<T>(System.Action<Scene, T> edit) where T : Component
        {
            Scene subScene = SceneManager.GetSceneByPath(SubScenePath);
            bool wasLoaded = subScene.isLoaded;
            if (!wasLoaded) subScene = EditorSceneManager.OpenScene(SubScenePath, OpenSceneMode.Additive);

            T target = null;
            foreach (GameObject root in subScene.GetRootGameObjects())
            {
                target = root.GetComponentInChildren<T>();
                if (target != null) break;
            }

            if (target == null)
                Debug.LogError($"{SubScenePath} 에서 {typeof(T).Name}을 찾지 못했습니다.");
            else
            {
                edit(subScene, target);
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

        // 부서지는 문 색: 벽(회청색)과 구분되는 갈색
        static readonly Color GateColor = new Color(0.6f, 0.45f, 0.3f);

        /// <summary>
        /// 맵 구성: 가운데 맵(Arena Size)을 두께 WallThickness의 벽이 두르고, 각 벽 중앙에 부서지는 문(GateWidth)이 있다.
        /// 벽 바깥에는 한 바퀴 이어진 통로가 있고, 그 바깥 끝을 얇은 테두리 벽이 막는다 (바깥 경계 = OuterHalfSize).
        /// 통로는 네 조각(왼쪽·오른쪽은 모서리까지, 아래·위는 그 사이)으로 나눠 방(Room)으로 등록한다 → 문이 닫혀 있는 동안은
        /// 적이 통로에 나오지 않고, 문 번호와 같은 번호의 조각이 그 문 바로 뒤다. 번호: 0 왼쪽, 1 오른쪽, 2 아래, 3 위.
        /// </summary>
        static void BuildLayout(ArenaLayoutAuthoring arena, Material wall, Material gate)
        {
            Transform parent = arena.transform;
            float halfW = arena.ArenaSize.x * 0.5f, halfH = arena.ArenaSize.y * 0.5f, t = arena.WallThickness;
            float gateHalf = arena.GateWidth * 0.5f;
            Vector2 outer = arena.OuterHalfSize;
            float ringX = halfW + t, ringY = halfH + t;   // 안쪽 벽의 바깥 면
            float passX = outer.x - t, passY = outer.y - t; // 통로의 바깥 끝 (테두리 벽 안쪽 면)

            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                string sideName = i == 0 ? "Left" : "Right";
                string endName = i == 0 ? "Bottom" : "Top";

                // 안쪽 벽 (왼쪽·오른쪽), 가운데에 문
                float wx0 = s < 0f ? -ringX : halfW, wx1 = s < 0f ? -halfW : ringX;
                Block(parent, wall, $"Wall_{sideName}_Lower", wx0, wx1, -halfH, -gateHalf);
                Block(parent, wall, $"Wall_{sideName}_Upper", wx0, wx1, gateHalf, halfH);
                Block(parent, gate, $"Wall_{sideName}_Gate", wx0, wx1, -gateHalf, gateHalf, gateIndex: i);

                // 안쪽 벽 (아래·위, 모서리 포함), 가운데에 문
                float wy0 = s < 0f ? -ringY : halfH, wy1 = s < 0f ? -halfH : ringY;
                Block(parent, wall, $"Wall_{endName}_Left", -ringX, -gateHalf, wy0, wy1);
                Block(parent, wall, $"Wall_{endName}_Right", gateHalf, ringX, wy0, wy1);
                Block(parent, gate, $"Wall_{endName}_Gate", -gateHalf, gateHalf, wy0, wy1, gateIndex: 2 + i);

                // 바깥 테두리 벽 (통로 끝, 맵의 끝이 보이게)
                Block(parent, wall, $"Wall_Outer{sideName}", s < 0f ? -outer.x : passX, s < 0f ? -passX : outer.x, -outer.y, outer.y);
                Block(parent, wall, $"Wall_Outer{endName}", -passX, passX, s < 0f ? -outer.y : passY, s < 0f ? -passY : outer.y);

                // 통로 조각: 왼쪽·오른쪽은 위아래 모서리까지, 아래·위는 그 사이
                Room(parent, $"Room_{sideName}", i,
                    new Vector2(s * (ringX + passX) * 0.5f, 0f), new Vector2(passX - ringX, passY * 2f));
                Room(parent, $"Room_{endName}", 2 + i,
                    new Vector2(0f, s * (ringY + passY) * 0.5f), new Vector2(ringX * 2f, passY - ringY));
            }
        }

        // x0~x1, y0~y1 사각형을 채우는 벽 (문이면 같은 번호의 통로 조각이 뒤에 있다)
        static void Block(Transform parent, Material material, string name, float x0, float x1, float y0, float y1, int gateIndex = -1)
        {
            GameObject block = CreatePrimitiveWithoutCollider(PrimitiveType.Cube, name, material);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            block.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
            var authoring = block.AddComponent<WallBlockAuthoring>();
            authoring.IsGate = gateIndex >= 0;
            authoring.GateIndex = gateIndex;
            authoring.RoomIndex = gateIndex;
        }

        static void Room(Transform parent, string name, int index, Vector2 center, Vector2 size)
        {
            var room = new GameObject(name);
            room.transform.SetParent(parent, false);
            room.transform.localPosition = center;
            var authoring = room.AddComponent<RoomAuthoring>();
            authoring.Index = index;
            authoring.Size = size;
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

        /// <summary>
        /// Arena의 Arena Size(Inspector 값)에 맞춰 벽·바닥 격자를 다시 만들고, 카메라가 플레이어를 따라가게 한다.
        /// 맵 크기를 바꿔 보려면 Arena Size를 고친 뒤 이 메뉴를 다시 실행한다.
        /// </summary>
        [MenuItem("MergeBullet/Update/Arena Walls And Floor", priority = 100)]
        static void RebuildArenaForSize()
        {
            Scene gameScene = SceneManager.GetActiveScene();
            if (gameScene.path != GameScenePath)
            {
                Debug.LogError($"{GameScenePath} 씬을 연 상태에서 실행하세요 (카메라를 설정해야 해서).");
                return;
            }

            Material wallMaterial = CreateMaterial("Wall", new Color(0.35f, 0.4f, 0.5f));
            Material floorMaterial = CreateFloorMaterial();
            Vector2 arenaSize = Vector2.zero;

            Material gateMaterial = CreateMaterial("Gate", GateColor);
            var gem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/XpGem.prefab");

            EditArenaSubScene<ArenaLayoutAuthoring>((scene, arena) =>
            {
                arenaSize = arena.ArenaSize;
                arena.WallGemPrefab = gem;
                EditorUtility.SetDirty(arena);

                // 벽·문·방·바닥을 다시 만든다 (다시 실행해도 겹치지 않게 기존 것은 지운다)
                for (int i = arena.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = arena.transform.GetChild(i).gameObject;
                    if (child.name.StartsWith("Wall_") || child.name.StartsWith("Room_") || child.name == "Floor")
                        Object.DestroyImmediate(child);
                }
                BuildLayout(arena, wallMaterial, gateMaterial);
                // 바닥은 통로까지 포함한 바깥 경계 전체에 깐다
                CreateFloor(arena.transform, arena.OuterHalfSize * 2f, floorMaterial);
            });

            // 카메라는 ECS의 CameraView를 따라간다. 바깥 경계가 테두리 벽이라 그 너머는 보이지 않게(여백 0)
            Camera cam = Camera.main;
            if (cam != null)
            {
                GameCamera follow = cam.GetComponent<GameCamera>();
                if (follow == null) follow = Undo.AddComponent<GameCamera>(cam.gameObject);
                follow.WallMargin = 0f;
                EditorUtility.SetDirty(follow);
            }

            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
            Debug.Log($"아레나 다시 만들기 완료: {arenaSize.x}x{arenaSize.y}. Lobby 씬에서 Play해 확인하세요.");
        }

        // 바닥: 검은 배경에서도 카메라가 움직이는 게 보이도록 격자 무늬. 격자 한 칸 = 2 유닛
        const float FloorCellSize = 2f;

        static void CreateFloor(Transform parent, Vector2 arenaSize, Material material)
        {
            GameObject floor = CreatePrimitiveWithoutCollider(PrimitiveType.Quad, "Floor", material);
            floor.transform.SetParent(parent, false);
            // 총알·적(z = 0)보다 뒤에 (카메라는 z = -10에서 +z 방향을 본다)
            floor.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            floor.transform.localScale = new Vector3(arenaSize.x, arenaSize.y, 1f);
            // 칸 수만큼 텍스처를 반복 (같은 머티리얼을 쓰므로 아레나 크기가 바뀌면 이 메뉴를 다시 실행)
            material.SetTextureScale("_BaseMap", arenaSize / FloorCellSize);
            EditorUtility.SetDirty(material);
        }

        static Material CreateFloorMaterial()
        {
            const string texturePath = MaterialsDir + "/FloorGrid.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                // 칸 테두리에만 옅은 선이 있는 64x64 타일. 반복하면 격자가 된다
                const int size = 64;
                var background = new Color32(14, 16, 22, 255);
                var line = new Color32(34, 38, 50, 255);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, x < 2 || y < 2 ? line : background);
                System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            Material material = CreateMaterial("Floor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
