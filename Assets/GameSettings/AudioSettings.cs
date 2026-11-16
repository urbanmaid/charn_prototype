using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioSettings : MonoBehaviour
{
    public AudioMixer mixer;
    public GameObject AudioSettingsUI;
    public List<MonoBehaviour> playerControllers = new List<MonoBehaviour>(); // 플레이어 이동 스크립트
    public ControlsTutorial controlsTutorial;

    private readonly Dictionary<MonoBehaviour, bool> savedControllerStates = new Dictionary<MonoBehaviour, bool>();

    void OnEnable() => GameplayCursor.SetUIOpen(this, AudioSettingsUI != null && AudioSettingsUI.activeInHierarchy);
    void OnDisable()
    {
        GameplayCursor.SetUIOpen(this, false);
        RestoreControllers();
    }

    void Update()
    {
        if (AudioSettingsUI != null && Input.GetKeyDown(KeyCode.F12))
        {
            if (AudioSettingsUI.activeSelf)
                CloseUI();
            else
                OpenUI();
        }        
    }
    public void OpenUI()
    {
        if (AudioSettingsUI == null) return;
        AudioSettingsUI.SetActive(true);
        GameplayCursor.SetUIOpen(this, true);

        // 모든 플레이어 컨트롤러 비활성화
        foreach (var controller in playerControllers)
        {
            if (controller != null && controller != this)
            {
                if (!savedControllerStates.ContainsKey(controller))
                    savedControllerStates.Add(controller, controller.enabled);
                controller.enabled = false;
            }
        }
        // 게임 일시정지 (선택)
        //Time.timeScale = 0f;
    }

    public void CloseUI()
    {
        if (AudioSettingsUI != null) AudioSettingsUI.SetActive(false);
        GameplayCursor.SetUIOpen(this, false);
        RestoreControllers();
    }

    private void RestoreControllers()
    {
        // 열기 전에 꺼져 있던 컨트롤러를 임의로 켜지 않는다.
        foreach (var pair in savedControllerStates)
        {
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        }
        savedControllerStates.Clear();
    }
    public void SetMaster(float value)
    {
        SetVolume("MasterVolume", value);
    }

    public void SetBGM(float value)
    {
        SetVolume("BGMVolume", value);
    }

    public void SetBackground(float value)
    {
        SetVolume("BGVolume", value);
    }

    public void SetVoice(float value)
    {
        SetVolume("VoiceVolume", value);
    }

    void SetVolume(string name, float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1f);
        mixer.SetFloat(name, Mathf.Log10(value) * 20);
    }
}
