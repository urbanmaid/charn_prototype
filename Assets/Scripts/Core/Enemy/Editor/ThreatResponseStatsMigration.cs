using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EnemyAI
{
    // 선택한 개체 하나만 옮긴다. 모든 씬/프리팹을 자동 저장하거나 기본값으로 일괄 덮어쓰지 않는다.
    public static class ThreatResponseStatsMigration
    {
        public static readonly string[] SettingNames = typeof(ThreatResponseStats)
            .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray();

        public static ThreatResponseStats CreateAndAssign(StateController controller, string assetPath)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("에셋 생성 및 연결은 Play Mode 밖에서 실행하세요.");
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("저장 경로가 필요합니다.");
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Split('/').Any(part => part == ".." || part == ".") ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/')))
                throw new ArgumentException("Assets 안의 유효한 폴더와 .asset 경로를 지정하세요.");
            if (File.Exists(assetPath) || AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidOperationException("기존 에셋을 덮어쓰지 않습니다. 새 파일 이름을 선택하세요.");

            // 기본값이 아니라 현재 유효값 16개를 복사한다. 기존 에셋이 있으면 그 값으로 독립 사본을 만든다.
            var asset = controller.CreateThreatResponseStatsSnapshot();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                Undo.RecordObject(controller, "Assign ThreatResponseStats");
                controller.threatResponseStats = asset;
                if (PrefabUtility.IsPartOfPrefabInstance(controller))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                EditorUtility.SetDirty(controller);
                if (controller.gameObject.scene.IsValid() && controller.gameObject.scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                AssetDatabase.SaveAssetIfDirty(asset);
                // 연결은 Undo 대상이다. 생성한 에셋 파일과 씬 저장 여부는 사용자가 관리한다.
                return asset;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }
    }
}
