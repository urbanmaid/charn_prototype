using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// MainGameplay에서 URP 후처리가 실제로 실행되도록 카메라와 Volume을 연결한다.
/// 구형 PostProcessLayer가 카메라에 남아 있어도 URP는 UniversalAdditionalCameraData를 사용한다.
/// </summary>
public static class URPPostProcessingInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        if (SceneManager.GetActiveScene().name != "MainGameplay")
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogWarning("URPPostProcessingInitializer: MainGameplay 카메라를 찾지 못했습니다.");
            return;
        }

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;

        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int includedVolumeCount = 0;

        foreach (Volume volume in volumes)
        {
            if (volume == null || !volume.enabled || volume.profile == null)
            {
                continue;
            }

            cameraData.volumeLayerMask |= 1 << volume.gameObject.layer;
            includedVolumeCount++;
        }

        if (includedVolumeCount == 0)
        {
            Debug.LogWarning("URPPostProcessingInitializer: 활성화된 URP Volume 또는 VolumeProfile을 찾지 못했습니다.");
        }
    }
}
