using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.Reflect.Extensions
{
    /// <summary>
    /// Manager for loading Addressables
    /// </summary>
    [DisallowMultipleComponent]
    public class AddressablesManager : MonoBehaviour, IProgressTask
    {
        [Tooltip("The label in the Addressables Group for the logo that will be automatically displayed on startup.")]
        [SerializeField] string oneLogoLabel = "OneLogo";
        [Tooltip("The label in the Addressables Group for the models that will be loaded.")]
        [SerializeField] string modelLabel = "Model";
        /// <summary>
        /// Event for getting the sprites that were loaded
        /// </summary>
        public System.Action SpritesAdded;
        /// <summary>
        /// Event for a sprite has loaded
        /// </summary>
        public System.Action<Sprite> SpriteLoaded;
        /// <summary>
        /// Event for notifying model list is ready
        /// </summary>
        public System.Action ModelListReady;
        /// <summary>
        /// Event for notifying model list is ready
        /// </summary>
        public System.Action ModelLoaded;
        List<Sprite> loadedSprites;
        /// <summary>
        /// The list of sprites loaded from the Addressables
        /// </summary>
        public List<Sprite> LoadedSprites { get { return loadedSprites; } }
        Dictionary<IResourceLocation, string> availableProjects;
        /// <summary>
        /// The available projects list
        /// </summary>
        public Dictionary<IResourceLocation, string> AvailableProjects { get => availableProjects; }
        static AddressablesManager _instance;
        /// <summary>
        /// The Addressables Manager
        /// </summary>
        /// <value>The singleton instance of the addressables manager</value>
        public static AddressablesManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<AddressablesManager>();

                return _instance;
            }
            set => _instance = value;
        }

        AsyncOperationHandle<GameObject> progressHandle;
        bool isLoading;

        #region IProgressTask implementation
        public event Action<float, string> progressChanged;
        public event Action taskCompleted;
        public void Cancel()
        { }
        #endregion
        void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(this);
            else
                Instance = this;
        }

        void Update()
        {
            if (isLoading)
            {
                // For the Reflect progress bar
                progressChanged?.Invoke(progressHandle.PercentComplete, "Loading "+(progressHandle.PercentComplete * 100).ToString("F0")+"%");
            }
        }

        /// <summary>
        /// Load the Sprite with the OneLogo label
        /// </summary>
        public void LoadOneLogoSprite()
        {
            if (!string.IsNullOrEmpty(oneLogoLabel))
                Addressables.LoadAssetAsync<Sprite>(oneLogoLabel).Completed += SpriteLoadedCheck;
            else
                Debug.LogWarning("Trying to load starting sprite logo but the one logo label is blank.");
        }

        /// <summary>
        /// Load the models available with matching model label for our project list
        /// </summary>
        public void LoadAvailableModelList()
        {
            if (!string.IsNullOrEmpty(modelLabel))
                Addressables.LoadResourceLocationsAsync(modelLabel, typeof(GameObject)).Completed += ModelListLoadCheck;
            else
                Debug.LogWarning("Trying to load available model list but the model label is blank.");
        }

        void ModelListLoadCheck(AsyncOperationHandle<IList<IResourceLocation>> obj)
        {
            availableProjects = new Dictionary<IResourceLocation, string>();
            if (obj.Result != null)
            {
                foreach (var location in obj.Result)
                {
                    if (!string.IsNullOrEmpty(location.InternalId))
                    {
                        var _name = System.IO.Path.GetFileNameWithoutExtension(location.InternalId);
                        if (string.IsNullOrEmpty(_name))
                            _name = location.InternalId;
                        if (availableProjects.ContainsKey(location))
                            availableProjects.Remove(location);
                        availableProjects.Add(location, _name);
                    }
                }
            }
            else
            {
                Debug.Log("Location loaded but result is null.");
                return;
            }

            if (obj.Result.Count > 0)
            {
                // List is ready
                ModelListReady?.Invoke();
            }
        }

        /// <summary>
        /// Load the model
        /// </summary>
        /// <param name="modelName">Model name from the project list which is also the identifier in the available projects</param>
        /// <param name="root">The root game object where the model will be instantiated</param>
        public void LoadModel(string modelName, Transform root)
        {
            if (!string.IsNullOrEmpty(modelName))
            {
                var matches = availableProjects.Where(x => x.Value == modelName).ToList();
                if (matches != null && matches.Count > 0)
                {
                    if (matches.Count > 1)
                        Debug.LogWarningFormat("Multiple projects are available for the selected name {0}, using the first found.");
                    progressHandle = Addressables.InstantiateAsync(matches[0].Key, root);
                    progressHandle.Completed += ModelLoadedCheck;
                    isLoading = true;
                    return;
                }
            }
            Debug.LogErrorFormat("No downloaded projects were found for {0}", modelName);
        }

        void ModelLoadedCheck(AsyncOperationHandle<GameObject> obj)
        {
            switch (obj.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    if (obj.Result != null)
                        Debug.Log("Found model: " + obj.Result.name);
                    else
                        Debug.Log("Model loaded but result is null.");
                    break;
                case AsyncOperationStatus.Failed:
                    Debug.Log("Model load failed.");
                    break;
                default:
                    break;
            }

            isLoading = false;
            if (obj.IsDone)
            {
                // For the Reflect progress bar
                progressChanged?.Invoke(1, "Loading Complete");
                taskCompleted?.Invoke();
                // Model is loaded
                ModelLoaded?.Invoke();
            }
        }

        void SpriteLoadedCheck(AsyncOperationHandle<Sprite> obj)
        {
            switch (obj.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    SpriteLoaded?.Invoke(obj.Result);
                    break;
                case AsyncOperationStatus.Failed:
                    Debug.Log("Single Sprite load failed.");
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Load the Addressables sprites with the sent label
        /// </summary>
        /// <param name="logoLabel">Label for Addressable sprites</param>
        public void LoadSpritesWithLabel(string logoLabel)
        {
            if (!string.IsNullOrEmpty(logoLabel))
            {
                loadedSprites = new List<Sprite>();
                Addressables.LoadAssetsAsync<Sprite>(logoLabel, null).Completed += SpritesLoadedCheck;
            }
        }

        void SpritesLoadedCheck(AsyncOperationHandle<IList<Sprite>> objects)
        {
            switch (objects.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    BuildSpriteList(objects.Result);
                    break;
                case AsyncOperationStatus.Failed:
                    Debug.Log("Sprite List load failed.");
                    break;
                default:
                    break;
            }
        }

        void BuildSpriteList(IList<Sprite> sprites)
        {
            if (sprites != null && sprites.Count > 0)
            {
                foreach (var _sprite in sprites)
                {
                    Debug.Log("Found sprite " + _sprite.name);
                    if (!loadedSprites.Contains(_sprite))
                        loadedSprites.Add(_sprite);
                }
                SpritesAdded?.Invoke();
            }
        }
    }
}