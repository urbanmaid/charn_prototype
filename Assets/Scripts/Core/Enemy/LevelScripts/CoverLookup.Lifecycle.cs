using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnemyAI
{
    public partial class CoverLookup
    {
        private sealed class Snapshot
        {
            public GameObject[] objects;
            public readonly List<Vector3[]> points = new List<Vector3[]>();
            public readonly List<int> hashes = new List<int>();
        }

        private sealed class SceneCatalog
        {
            public Scene scene;
            public StageSceneInitializer stage;
            public bool dynamicStage;
            public System.Action changed;
            public readonly Dictionary<int, Snapshot> masks = new Dictionary<int, Snapshot>();
            public readonly HashSet<StateController> users = new HashSet<StateController>();
        }

        private readonly Dictionary<int, SceneCatalog> catalogs = new Dictionary<int, SceneCatalog>();
        public int CollectionCount { get; private set; }

        private void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;
        private void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

        private SceneCatalog GetCatalog(Scene scene)
        {
            if (catalogs.TryGetValue(scene.handle, out SceneCatalog catalog)) return catalog;
            catalog = new SceneCatalog { scene = scene };
            // Discover once per scene, including an initializer whose Awake has not run.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var stage = root.GetComponentInChildren<StageSceneInitializer>(true);
                if (stage == null) continue;
                catalog.stage = stage;
                catalog.dynamicStage = true;
                break;
            }
            if (catalog.stage != null)
            {
                SceneCatalog captured = catalog;
                catalog.changed = () => Invalidate(captured);
                catalog.stage.CoverGeometryChanged += catalog.changed;
            }
            catalogs.Add(scene.handle, catalog);
            return catalog;
        }

        private static bool StageReady(SceneCatalog catalog) =>
            !catalog.dynamicStage || (catalog.stage != null && catalog.stage.IsStageInitialized);

        public void Register(StateController controller) =>
            GetCatalog(controller.gameObject.scene).users.Add(controller);

        public void Unregister(StateController controller)
        {
            foreach (SceneCatalog catalog in catalogs.Values) catalog.users.Remove(controller);
        }

        public void RegisterDynamicCover(GameObject cover, Vector3[] spots)
        {
            if (!cover) return;
            Invalidate(GetCatalog(cover.scene));
        }

        public void UnregisterDynamicCover(GameObject cover)
        {
            if (!cover || !catalogs.TryGetValue(cover.scene.handle, out SceneCatalog catalog)) return;
            Invalidate(catalog);
        }

        private void Invalidate(SceneCatalog catalog)
        {
            catalog.masks.Clear();
            covers = null;
            allCoverSpots = null;
            coverHashCodes = null;
            foreach (StateController controller in catalog.users)
                if (controller != null) controller.InvalidateCoverGeometry();
        }

        private Snapshot GetSnapshot(SceneCatalog catalog, int mask)
        {
            if (catalog.masks.TryGetValue(mask, out Snapshot snapshot)) return snapshot;
            // Transform changes during stage spawning must be visible to collider probes.
            Physics.SyncTransforms();
            snapshot = new Snapshot { objects = GetObjectsInLayerMask(mask, catalog.scene) };
            foreach (GameObject cover in snapshot.objects)
            {
                snapshot.points.Add(GetSpots(cover, mask));
                snapshot.hashes.Add(cover.GetHashCode());
            }
            catalog.masks.Add(mask, snapshot);
            CollectionCount++;
            return snapshot;
        }

        private void Activate(Snapshot snapshot)
        {
            covers = snapshot.objects;
            allCoverSpots = snapshot.points;
            coverHashCodes = snapshot.hashes;
        }

        private bool Prepare(StateController controller)
        {
            SceneCatalog catalog = GetCatalog(controller.gameObject.scene);
            catalog.users.Add(controller);
            // Agent registration is the readiness check for the currently baked maps.
            // Do not freeze an empty list while navigation or stage spawning is pending.
            if (!StageReady(catalog) || controller.nav == null ||
                !controller.nav.enabled || !controller.nav.isOnNavMesh) return false;
            Activate(GetSnapshot(catalog, controller.generalStats.coverMask));
            return true;
        }

        private static void Detach(SceneCatalog catalog)
        {
            if (catalog.stage != null) catalog.stage.CoverGeometryChanged -= catalog.changed;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!catalogs.TryGetValue(scene.handle, out SceneCatalog catalog)) return;
            Detach(catalog);
            catalogs.Remove(scene.handle);
        }

        private void OnDestroy()
        {
            foreach (SceneCatalog catalog in catalogs.Values) Detach(catalog);
            catalogs.Clear();
        }
    }
}
