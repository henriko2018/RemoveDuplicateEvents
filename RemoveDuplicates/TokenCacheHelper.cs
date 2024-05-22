using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace RemoveDuplicates;

static class TokenCacheHelper
{
    // computing the root directory is not very simple on Linux and Mac, so a helper is provided
    private static readonly string s_cacheFilePath =
               Path.Combine(MsalCacheHelper.UserRootDirectory, "msal.contoso.cache");

    public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
    public static readonly string? CacheDir = Path.GetDirectoryName(s_cacheFilePath);

    public static readonly string KeyChainServiceName = "Contoso.MyProduct";
    public static readonly string KeyChainAccountName = "MSALCache";

    public static readonly string LinuxKeyRingSchema = "com.contoso.devtools.tokencache";
    public static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
    public static readonly string LinuxKeyRingLabel = "MSAL token cache for all Contoso dev tool apps.";
    public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new("Version", "1");
    public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new("ProductGroup", "MyApps");

    public static async Task AddCacheAsync(IPublicClientApplication app, string clientId)
    {
        // Building StorageCreationProperties
        var storageProperties =
             new StorageCreationPropertiesBuilder(CacheFileName, CacheDir)
             .WithCacheChangedEvent(clientId)
             .WithLinuxKeyring(
                 LinuxKeyRingSchema,
                 LinuxKeyRingCollection,
                 LinuxKeyRingLabel,
                 LinuxKeyRingAttr1,
                 LinuxKeyRingAttr2)
             .WithMacKeyChain(
                 KeyChainServiceName,
                 KeyChainAccountName)
             .Build();

        // This hooks up the cross-platform cache into MSAL
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(app.UserTokenCache);
    }
}
