using UnityEngine;
using GoogleMobileAds.Api;
using System;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("AdMob IDs")]
#if UNITY_ANDROID
    private const string rewardedAdId = "ca-app-pub-2867709522189910/8034193836"; //Android
// #elif UNITY_IOS
//     private const string rewardedAdId = "ca-app-pub-3940256099942544/1712485313"; // ✅ Test ID iOS
#else
    private const string rewardedAdId = "unused";
#endif

    private RewardedAd rewardedAd;
    private Action onRewardEarned;
    private Action onAdFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // ✅ Init AdMob
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("✅ AdMob Initialized");
            LoadRewardedAd();
        });
    }

    // ✅ Load quảng cáo trước
    public void LoadRewardedAd()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        var adRequest = new AdRequest();
        RewardedAd.Load(rewardedAdId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"❌ Rewarded ad load failed: {error}");
                return;
            }
            rewardedAd = ad;
            Debug.Log("✅ Rewarded ad loaded");
            RegisterEvents(rewardedAd);
        });
    }

    private void RegisterEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Ad closed → load lại");
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"❌ Ad failed to show: {error}");
            onAdFailed?.Invoke();
            LoadRewardedAd();
        };
    }

    // ✅ Gọi từ GameOverUI
    public void ShowRewardedAd(Action onRewarded, Action onFailed = null)
    {
        onRewardEarned = onRewarded;
        onAdFailed     = onFailed;

        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log($"✅ Reward earned: {reward.Type} x{reward.Amount}");
                onRewardEarned?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("⚠️ Ad chưa sẵn sàng, dùng fallback");
            // ✅ Fallback: vẫn cho chơi tiếp nếu ad chưa load
            onRewarded?.Invoke();
            LoadRewardedAd();
        }
    }

    public bool IsAdReady() => rewardedAd != null && rewardedAd.CanShowAd();
}
