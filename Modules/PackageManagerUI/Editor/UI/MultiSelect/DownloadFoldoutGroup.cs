// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

namespace UnityEditor.PackageManager.UI.Internal
{
    internal class DownloadFoldoutGroup : MultiSelectFoldoutGroup
    {
        public DownloadFoldoutGroup(AssetStoreDownloadManager assetStoreDownloadManager,
                                    AssetStoreCache assetStoreCache,
                                    PackageOperationDispatcher operationDispatcher,
                                    UnityConnectProxy unityConnect,
                                    ApplicationProxy application)
            : base(new DownloadAction(operationDispatcher, assetStoreDownloadManager, assetStoreCache, unityConnect, application),
                   new CancelDownloadAction(operationDispatcher, assetStoreDownloadManager, application))
        {
            mainFoldout.headerTextTemplate = L10n.Tr("Download {0}");
            inProgressFoldout.headerTextTemplate = L10n.Tr("Downloading {0}...");
        }
    }
}
