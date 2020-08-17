using System.Collections.Generic;

namespace UnityEngine.Reflect.Extensions
{
    /// <summary>
    /// The Metadata behavior for loading saved models through the Addressables system
    /// </summary>
    public class SavedModelsMetadataBehavior : IManageMetadata
    {
        ReflectMetadataManager reflectMetadataManager;

        public SavedModelsMetadataBehavior(ReflectMetadataManager manager)
        {
            if (manager != null)
                reflectMetadataManager = manager;
            else
                Debug.LogError("Fatal Error: The Reflect Metadata Manager cannot be null.");
        }

        #region IManageMetadata implementation
        public void OnEnabled()
        {
            AddressablesManager.Instance.ModelLoaded += StartSearch;
        }

        public void OnDisabled()
        {
            AddressablesManager.Instance.ModelLoaded -= StartSearch;
        }

        public void OnStarted()
        {
            // Combine dictionaries
            foreach (KeyValuePair<IObserveMetadata, MetadataSearch> kvp in reflectMetadataManager.NotifySyncObjectDictionary)
            {
                if (!reflectMetadataManager.NotifyRootDictionary.ContainsKey(kvp.Key))
                {
                    reflectMetadataManager.NotifyRootDictionary.Add(kvp.Key, kvp.Value);
                }
            }
        }

        public void StartSearch()
        {
            // Search metadata
            SearchMetadata();
        }
        #endregion

        void SearchMetadata()
        {
            //Make a copy of the notifyRootDictionary so we can remove entries and no duplicate notifications are sent
            reflectMetadataManager.NotifyRootCopy = new Dictionary<IObserveMetadata, MetadataSearch>(reflectMetadataManager.NotifyRootDictionary);

            foreach (KeyValuePair<IObserveMetadata, MetadataSearch> kvp in reflectMetadataManager.NotifyRootDictionary)
            {
                if (kvp.Key is IObserveReflectRoot key)
                    key.NotifyBeforeSearch();
            }

            if (reflectMetadataManager.ReflectRoot != null)
                reflectMetadataManager.SearchReflectRoot(reflectMetadataManager.ReflectRoot);

            foreach (KeyValuePair<IObserveMetadata, MetadataSearch> kvp in reflectMetadataManager.NotifyRootDictionary)
            {
                if (kvp.Key is IObserveReflectRoot key)
                    key.NotifyAfterSearch();
            }
        }
    }
}