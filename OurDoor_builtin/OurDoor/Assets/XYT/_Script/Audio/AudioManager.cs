using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioMixer mixer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetBGM(GameManager.Instance.BGMVolume);
        SetSFX(GameManager.Instance.SFXVolume);
        SetVoice(GameManager.Instance.VoiceVolume);
    }
    public void SetBGM(float value)
    {
        mixer.SetFloat("BGMVolume", LinearToDB(value));

        GameManager.Instance.BGMVolume = value;
    }

    public void SetSFX(float value)
    {
        mixer.SetFloat("SFXVolume", LinearToDB(value));

        GameManager.Instance.SFXVolume = value;
    }

    public void SetVoice(float value)
    {
        mixer.SetFloat("VoiceVolume", LinearToDB(value));

        GameManager.Instance.VoiceVolume = value;
    }

    float LinearToDB(float value)
    {
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }
}