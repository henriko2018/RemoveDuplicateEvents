using System;
using System.IO;
using Microsoft.Identity.Client;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace RemoveDuplicates
{
    static class TokenCacheHelper
    {

        /// <summary>
        /// Get the user token cache
        /// </summary>
        /// <returns></returns>
        public static TokenCache GetUserCache()
        {
            if (_usertokenCache == null)
            {
                _usertokenCache = new TokenCache();
                _usertokenCache.SetBeforeAccess(BeforeAccessNotification);
                _usertokenCache.SetAfterAccess(AfterAccessNotification);
            }
            return _usertokenCache;
        }

        static TokenCache _usertokenCache;

        /// <summary>
        /// Path to the token cache
        /// </summary>
        public static string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
            "msalcache.txt");

        private static readonly object FileLock = new object();

        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                args.TokenCache.Deserialize(File.Exists(CacheFilePath)
                    ? File.ReadAllBytes(CacheFilePath)
                    : null);
            }
        }

        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.TokenCache.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changesgs in the persistent store
                    Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath));
                    File.WriteAllBytes(CacheFilePath, args.TokenCache.Serialize());
                    // once the write operation takes place restore the HasStateChanged bit to filse
                    args.TokenCache.HasStateChanged = false;
                }
            }
        }
    }
}
