using UnityEditor;

namespace Antimatter
{
    public static class MarketplaceSettings
    {
        private const string PrefsPrefix = "Antimatter.Marketplace.";

        public static string FirebaseApiKey
        {
            get => EditorPrefs.GetString(PrefsPrefix + "ApiKey", "AIzaSyC33NT9b32yw5aU4q0oopO8PV2sPP5NaOg");
            set => EditorPrefs.SetString(PrefsPrefix + "ApiKey", value);
        }

        public static string FirebaseProjectId
        {
            get => EditorPrefs.GetString(PrefsPrefix + "ProjectId", "intrepid-charge-414316");
            set => EditorPrefs.SetString(PrefsPrefix + "ProjectId", value);
        }

        public static string FirebaseStorageBucket
        {
            get => EditorPrefs.GetString(PrefsPrefix + "StorageBucket", "intrepid-charge-414316.firebasestorage.app");
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
    }
}
