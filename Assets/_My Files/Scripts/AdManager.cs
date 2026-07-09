using System;
using UnityEngine;
// using Unity.Services.LevelPlay;

namespace SlideAndMatch
{
    public class AdManager : MonoBehaviour
    {

        // [Header("LevelPlay Settings")]
        // [SerializeField] private string androidAppKey = "demo_android_app_key";
        // [SerializeField] private string rewardedAdUnitId = "rewarded_ad_unit_id";
        // [SerializeField] private bool useMockFallback = true;

        // [Header("Ad Toggle")]
        // public bool adsEnabled = true;

        // private LevelPlayRewardedAd rewardedAd;
        // private Action onRewardCallback;
        // private bool isInitialized = false;
        // private static AdManager instance;
        // public static AdManager Instance
        // {
        //     get
        //     {
        //         if (instance == null)
        //         {
        //             instance = FindAnyObjectByType<AdManager>();
        //             if (instance == null)
        //             {
        //                 GameObject go = new GameObject("AdManager");
        //                 instance = go.AddComponent<AdManager>();
        //             }
        //         }
        //         return instance;
        //     }
        // }

        // void Awake()
        // {
        //     if (instance != null && instance != this)
        //     {
        //         Destroy(gameObject);
        //         return;
        //     }
        //     instance = this;
        //     DontDestroyOnLoad(gameObject);
        // }

        // void Start()
        // {
        //     InitializeSDK();
        // }

        // private void InitializeSDK()
        // {
        //     try
        //     {
        //         Debug.Log("[AdManager] Initializing LevelPlay SDK...");
        //         LevelPlay.OnInitSuccess += SdkInitializationCompletedEvent;
        //         LevelPlay.OnInitFailed += SdkInitializationFailedEvent;
        //         LevelPlay.Init(androidAppKey);
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.LogError($"[AdManager] Exception initializing LevelPlay SDK: {ex.Message}");
        //     }
        // }

        // private void SdkInitializationCompletedEvent(LevelPlayConfiguration config)
        // {
        //     Debug.Log("[AdManager] LevelPlay SDK Initialized Successfully!");
        //     isInitialized = true;

        //     try
        //     {
        //         // Create rewarded ad object
        //         rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);

        //         // Register event listeners
        //         rewardedAd.OnAdLoaded += RewardedOnAdLoadedEvent;
        //         rewardedAd.OnAdLoadFailed += RewardedOnAdLoadFailedEvent;
        //         rewardedAd.OnAdRewarded += RewardedOnAdRewardedEvent;
        //         rewardedAd.OnAdClosed += RewardedOnAdClosedEvent;

        //         // Pre-load the first ad
        //         rewardedAd.LoadAd();
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.LogError($"[AdManager] Exception creating LevelPlayRewardedAd: {ex.Message}");
        //     }
        // }

        // private void SdkInitializationFailedEvent(LevelPlayInitError error)
        // {
        //     Debug.LogError($"[AdManager] LevelPlay SDK failed to initialize: {error.ErrorMessage}");
        // }

        // private void RewardedOnAdLoadedEvent(LevelPlayAdInfo adInfo)
        // {
        //     Debug.Log("[AdManager] Rewarded Ad Loaded successfully.");
        // }

        // private void RewardedOnAdLoadFailedEvent(LevelPlayAdError error)
        // {
        //     Debug.LogWarning($"[AdManager] Rewarded Ad failed to load: {error.ErrorMessage}");
        // }

        // private void RewardedOnAdRewardedEvent(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        // {
        //     Debug.Log("[AdManager] Rewarded Ad completed! Granting reward...");
        //     if (onRewardCallback != null)
        //     {
        //         onRewardCallback.Invoke();
        //         onRewardCallback = null;
        //     }
        // }

        // private void RewardedOnAdClosedEvent(LevelPlayAdInfo adInfo)
        // {
        //     Debug.Log("[AdManager] Rewarded Ad closed. Loading next ad...");
        //     // Load next ad for future use
        //     if (rewardedAd != null)
        //     {
        //         rewardedAd.LoadAd();
        //     }
        // }

        // /// <summary>
        // /// Attempts to show a real rewarded ad. Falls back to mock ad if unavailable.
        // /// </summary>
        // public void ShowRewardedAd(Action onRewardGranted)
        // {
        //     onRewardCallback = onRewardGranted;

        //     if (!adsEnabled)
        //     {
        //         Debug.Log("[AdManager] Ads are disabled via adsEnabled toggle. Granting reward directly.");
        //         if (onRewardCallback != null)
        //         {
        //             onRewardCallback.Invoke();
        //             onRewardCallback = null;
        //         }
        //         return;
        //     }

        //     // Check if LevelPlay ad is ready
        //     if (isInitialized && rewardedAd != null && rewardedAd.IsAdReady())
        //     {
        //         Debug.Log("[AdManager] Showing real LevelPlay rewarded ad...");
        //         rewardedAd.ShowAd();
        //     }
        //     else
        //     {
        //         Debug.Log("[AdManager] Real ad not ready. Using mock ad fallback...");
        //         if (useMockFallback)
        //         {
        //             UIManager ui = FindAnyObjectByType<UIManager>();
        //             if (ui != null)
        //             {
        //                 ui.StartMockAdSequence(() =>
        //                 {
        //                     if (onRewardCallback != null)
        //                     {
        //                         onRewardCallback.Invoke();
        //                         onRewardCallback = null;
        //                     }
        //                 });
        //             }
        //             else
        //             {
        //                 // Direct reward if no UI manager found (failsafe)
        //                 Debug.LogWarning("[AdManager] UIManager not found. Granting reward directly.");
        //                 if (onRewardCallback != null)
        //                 {
        //                     onRewardCallback.Invoke();
        //                     onRewardCallback = null;
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             Debug.LogWarning("[AdManager] Mock fallback disabled and real ad not ready. No reward granted.");
        //             onRewardCallback = null;
        //         }
        //     }
        // }
    }
}
