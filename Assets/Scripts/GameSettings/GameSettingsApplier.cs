using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using BuiltinShadowResolution = UnityEngine.ShadowResolution;
using BuiltinShadowQuality = UnityEngine.ShadowQuality;

public class GameSettingsApplier : MonoBehaviour {
    public static GameSettingsApplier Instance;

    [Header("Post Process")]
    public Volume postProcessVolume;

    [Header("Bloom / MotionBlur Intensity When On")]
    [Range(0f, 10f)]
    public float bloomIntensityWhenOn = 3f;

    [Range(0f, 1f)]
    public float motionBlurIntensityWhenOn = 0.4f;

    [Header("Crosshair")]
    public GameObject crosshairRoot;

    [Header("Language (0=KR,1=EN,2=JP,3=ZH-CN)")]
    public Locale[] languageLocales;

    [Header("Background Audio")]
    public bool playInBackgroundDefault = true;

    private Bloom _bloom;
    private MotionBlur _motionBlur;

    private Light[] _sceneLights;

    private BuiltinShadowQuality _originalShadowQuality;
    private BuiltinShadowResolution _originalShadowResolution;

    private LightShadows[] _originalLightShadows;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _originalShadowQuality = QualitySettings.shadows;
        _originalShadowResolution = QualitySettings.shadowResolution;

        CachePostProcessComponents();
        CacheSceneLights(true);
    }

    private void Start() {
        ApplyAllGraphicsSettings();
    }

    private void CachePostProcessComponents() {
        if (postProcessVolume == null) {
            return;
        }

        if (postProcessVolume.profile == null) {
            return;
        }

        postProcessVolume.profile.TryGet(out _bloom);
        postProcessVolume.profile.TryGet(out _motionBlur);

        if (_bloom != null && bloomIntensityWhenOn <= 0f) {
            bloomIntensityWhenOn = _bloom.intensity.value;
        }

        if (_motionBlur != null && motionBlurIntensityWhenOn <= 0f) {
            motionBlurIntensityWhenOn = _motionBlur.intensity.value;
        }
    }

    private void CacheSceneLights(bool captureOriginal) {
        _sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        if (!captureOriginal) {
            return;
        }

        if (_sceneLights == null || _sceneLights.Length == 0) {
            _originalLightShadows = null;
            return;
        }

        _originalLightShadows = new LightShadows[_sceneLights.Length];
        for (int i = 0; i < _sceneLights.Length; i++) {
            _originalLightShadows[i] = _sceneLights[i].shadows;
        }
    }

    public void ApplyAllGraphicsSettings() {
        ApplyWindowModeAndResolution();
        ApplyVSync();
        ApplyFrameLimit();
        ApplyMotionBlur();
        ApplyBloom();
        ApplyShadowQuality();
        ApplyBrightness();
        ApplyCrosshair();
        ApplyLanguage();
        ApplyPlayInBackground();
    }

    public void ApplyWindowModeAndResolution() {
        if (SettingsManager.Instance == null) {
            return;
        }

        int windowModeIndex = SettingsManager.Instance.GetInt("WindowMode", 0);
        int resolutionIndex = SettingsManager.Instance.GetInt("Resolution", 0);

        Resolution baseRes = GetBaseResolution(resolutionIndex);
        FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;

        if (windowModeIndex == 0) {
            mode = FullScreenMode.ExclusiveFullScreen;
        } else if (windowModeIndex == 1) {
            mode = FullScreenMode.FullScreenWindow;
        } else {
            mode = FullScreenMode.Windowed;
        }

        if (mode == FullScreenMode.Windowed) {
            int screenW = Display.main.systemWidth;
            int screenH = Display.main.systemHeight;

            float scaleX = (float)screenW / baseRes.width;
            float scaleY = (float)screenH / baseRes.height;
            float scale = Mathf.Min(1.0f, Mathf.Min(scaleX, scaleY));

            int targetW = Mathf.RoundToInt(baseRes.width * scale);
            int targetH = Mathf.RoundToInt(baseRes.height * scale);

            if (targetW < 1) {
                targetW = 1;
            }

            if (targetH < 1) {
                targetH = 1;
            }

            Screen.SetResolution(targetW, targetH, mode);
        } else {
            if (baseRes.width <= 0 || baseRes.height <= 0) {
                Resolution cur = Screen.currentResolution;
                Screen.SetResolution(cur.width, cur.height, mode);
            } else {
                Screen.SetResolution(baseRes.width, baseRes.height, mode);
            }
        }
    }

    private Resolution GetBaseResolution(int index) {
        Resolution r = new Resolution();

        if (index == 0) {
            r.width = 1920;
            r.height = 1080;
        } else if (index == 1) {
            r.width = 1900;
            r.height = 900;
        } else if (index == 2) {
            r.width = 1280;
            r.height = 960;
        } else if (index == 3) {
            r.width = 1280;
            r.height = 960;
        } else {
            r.width = 1920;
            r.height = 1080;
        }

        return r;
    }

    public void ApplyVSync() {
        int vsyncIndex = SettingsManager.Instance.GetInt("VSync", 0);
        vsyncIndex = Mathf.Clamp(vsyncIndex, 0, 2);

        QualitySettings.vSyncCount = vsyncIndex;
    }

    public void ApplyFrameLimit() {
        int frameIndex = SettingsManager.Instance.GetInt("FrameLimit", 0);
        frameIndex = Mathf.Clamp(frameIndex, 0, 4);

        int target;
        if (frameIndex == 0) {
            target = -1;
        } else if (frameIndex == 1) {
            target = 30;
        } else if (frameIndex == 2) {
            target = 60;
        } else if (frameIndex == 3) {
            target = 120;
        } else {
            target = 144;
        }

        Application.targetFrameRate = target;
    }

    public void ApplyMotionBlur() {
        int val = SettingsManager.Instance.GetInt("MotionBlur", 0);
        bool enabled = (val == 1);

        if (_motionBlur != null) {
            _motionBlur.active = true;
            _motionBlur.intensity.overrideState = true;

            if (enabled) {
                _motionBlur.intensity.value = motionBlurIntensityWhenOn;
            } else {
                _motionBlur.intensity.value = 0f;
            }
        }
    }

    public void ApplyBloom() {
        int val = SettingsManager.Instance.GetInt("Bloom", 0);
        bool enabled = (val == 1);

        if (_bloom != null) {
            _bloom.active = true;
            _bloom.intensity.overrideState = true;

            if (enabled) {
                _bloom.intensity.value = bloomIntensityWhenOn;
            } else {
                _bloom.intensity.value = 0f;
            }
        }
    }

    public void ApplyShadowQuality() {
        int index = SettingsManager.Instance.GetInt("ShadowQuality", 3);
        index = Mathf.Clamp(index, 0, 3);

        if (_sceneLights == null) {
            CacheSceneLights(_originalLightShadows == null);
        }

        if (index == 0) {
            QualitySettings.shadows = BuiltinShadowQuality.Disable;
        } else if (index == 1) {
            QualitySettings.shadows = BuiltinShadowQuality.HardOnly;
        } else if (index == 2) {
            QualitySettings.shadows = BuiltinShadowQuality.All;
        } else {
            QualitySettings.shadows = _originalShadowQuality;
            QualitySettings.shadowResolution = _originalShadowResolution;
        }

        if (_sceneLights != null && _sceneLights.Length > 0) {
            for (int i = 0; i < _sceneLights.Length; i++) {
                Light light = _sceneLights[i];
                if (light == null) {
                    continue;
                }

                if (index == 0) {
                    light.shadows = LightShadows.None;
                } else if (index == 1) {
                    light.shadows = LightShadows.Hard;
                } else if (index == 2) {
                    light.shadows = LightShadows.Soft;
                } else {
                    if (_originalLightShadows != null && i < _originalLightShadows.Length) {
                        light.shadows = _originalLightShadows[i];
                    }
                }
            }
        }
    }

    public void ApplyBrightness() {
        float b = SettingsManager.Instance.GetFloat("Brightness", 1.0f);
        b = Mathf.Clamp(b, 0.5f, 1.5f);

        float intensity = 0.3f * b + 0.2f;

        RenderSettings.ambientIntensity = intensity;
        RenderSettings.reflectionIntensity = intensity;
    }

    public void ApplyCrosshair() {
        int value = SettingsManager.Instance.GetInt("Crosshair", 0);
        bool visible = (value == 0);

        if (crosshairRoot != null) {
            crosshairRoot.SetActive(visible);
        }
    }

    public void ApplyLanguage() {
        int index = SettingsManager.Instance.GetInt("Language", 0);
        if (index < 0) {
            index = 0;
        }

        if (languageLocales != null && languageLocales.Length > 0) {
            if (index >= languageLocales.Length) {
                index = Mathf.Clamp(index, 0, languageLocales.Length - 1);
            }

            Locale target = languageLocales[index];
            if (target != null) {
                LocalizationSettings.SelectedLocale = target;
                return;
            }
        }

        if (LocalizationSettings.AvailableLocales != null &&
            LocalizationSettings.AvailableLocales.Locales != null) {

            var list = LocalizationSettings.AvailableLocales.Locales;
            if (index >= 0 && index < list.Count) {
                var target = list[index];
                LocalizationSettings.SelectedLocale = target;
                return;
            }
        }
    }

    public void ApplyPlayInBackground() {
        int value = SettingsManager.Instance.GetInt("PlayInBackground", playInBackgroundDefault ? 0 : 1);
        bool run = (value == 0);

        Application.runInBackground = run;
    }
}