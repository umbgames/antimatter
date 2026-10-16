using UnityEditor;

namespace Antimatter
{
#if UNITY_EDITOR
    public static class MarketplaceSettings
    {
        private const string PrefsPrefix = "Antimatter.Marketplace.";

        public static string FirebaseApiKey
        {
            get => EditorPrefs.GetString(PrefsPrefix + "ApiKey", "AIzaSyC5c3R9MDydDMAiUfZNToXsOhu5ep9gS1I");
            set => EditorPrefs.SetString(PrefsPrefix + "ApiKey", value);
        }

        public static string FirebaseProjectId
        {
            get => EditorPrefs.GetString(PrefsPrefix + "ProjectId", "antimatter-c1602");
            set => EditorPrefs.SetString(PrefsPrefix + "ProjectId", value);
        }

        public static string FirebaseStorageBucket
        {
            get => EditorPrefs.GetString(PrefsPrefix + "StorageBucket", "antimatter-c1602.firebasestorage.app");
            set => EditorPrefs.SetString(PrefsPrefix + "StorageBucket", value);
        }

        public static string CurrentToken
        {
            get => EditorPrefs.GetString(PrefsPrefix + "Token", "");
            set => EditorPrefs.SetString(PrefsPrefix + "Token", value);
        }
        
        public static string CurrentUserId
        {
             get => EditorPrefs.GetString(PrefsPrefix + "UserId", "");
             set => EditorPrefs.SetString(PrefsPrefix + "UserId", value);
        }
        public static string CurrentRefreshToken
        {
            get => EditorPrefs.GetString(PrefsPrefix + "RefreshToken", "");
            set => EditorPrefs.SetString(PrefsPrefix + "RefreshToken", value);
        }

        public static string TokenExpiryTime
        {
             get => EditorPrefs.GetString(PrefsPrefix + "TokenExpiry", "");
             set => EditorPrefs.SetString(PrefsPrefix + "TokenExpiry", value);
        }
    }
#endif
}
