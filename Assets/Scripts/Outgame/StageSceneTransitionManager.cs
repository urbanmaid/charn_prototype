using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 아웃게임에서 선택한 스테이지를 유지한 채 게임플레이 전용 씬으로 전환한다.
/// 게임플레이 씬에서는 StageSceneInitializer가 선택된 메타데이터를 읽어 스테이지를 생성한다.
/// </summary>
public class StageSceneTransitionManager : MonoBehaviour
{
    [Tooltip("Build Settings에 등록된 게임플레이 전용 씬의 이름입니다.")]
    [SerializeField] private string gameplaySceneName;
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    /// <summary>
    /// 마지막으로 선택된 StageMetaData를 사용해 게임플레이 씬을 불러온다.
    /// </summary>
    public void LoadSelectedStageScene()
    {
        GameplayDiagnostics.Step("TRANSITION requested scene=" + gameplaySceneName);
        if (StageSelectionManager.SelectedStageMetaData == null)
        {
            Debug.LogWarning("선택된 스테이지가 없어 게임플레이 씬으로 전환할 수 없습니다.", this);
            return;
        }

        if (!StageSelectionManager.SelectedStageMetaData.CanEnterGameplay())
        {
            Debug.LogWarning(
                $"'{StageSelectionManager.SelectedStageMetaData.name}'의 맵 또는 시작 지점 설정이 완료되지 않아 게임플레이 씬으로 전환할 수 없습니다.",
                StageSelectionManager.SelectedStageMetaData);
            ShowWIPMessege();
            return;
        }

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogWarning("StageSceneTransitionManager에 게임플레이 씬 이름이 지정되지 않았습니다.", this);
            ShowWIPMessege();
            return;
        }

        var saves = SaveDataManager.Instance;
        if (saves != null && !string.IsNullOrWhiteSpace(saves.Data?.saveId) && !saves.TryBindSelectedSlot())
        {
            Debug.LogError("선택한 저장 슬롯을 연결하지 못했습니다. 새 게임으로 진입하지 않습니다.", this);
            return;
        }
        GameplayDiagnostics.Step("LOAD SCENE BEGIN " + gameplaySceneName + " available=" + Application.CanStreamedLevelBeLoaded(gameplaySceneName));
        SceneManager.LoadScene(gameplaySceneName, loadSceneMode);
        GameplayDiagnostics.Step("LOAD SCENE call returned (not necessarily ready)");
    }
    
    private void ShowWIPMessege()
    {
        if(MenuSceneManager.Instance != null)
        {
            MenuSceneManager.Instance.ShowWIPPage();
        } 
    }
}
