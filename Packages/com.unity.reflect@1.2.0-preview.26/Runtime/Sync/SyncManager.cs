﻿// Prevent warnings for field not assigned to
#pragma warning disable 0649

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using Unity.Reflect.Utils;

namespace UnityEngine.Reflect
{
    public sealed class SyncManager : MonoBehaviour, ISyncTask, IProgressTask, ILogReceiver
    {
        [SerializeField]
        ProjectManager m_ProjectManager;

        [SerializeField]
        SyncTopMenu m_SyncMenu;

        [SerializeField]
        ProgressBar m_ProgressBar;

        [SerializeField]
        Transform m_SyncRoot;

        [SerializeField]
        Shader[] m_Shaders;

        public Transform syncRoot => m_SyncRoot;

        public delegate void EventHandler(SyncInstance instance);
        public event EventHandler onInstanceAdded;

        public event Action<Exception> onError;

        Transform m_SyncInstancesRoot;

        public IReadOnlyDictionary<string, SyncInstance> syncInstances => m_SyncInstances;

        readonly Dictionary<string, SyncInstance> m_SyncInstances = new Dictionary<string, SyncInstance>();

        readonly ServiceObserver m_Observer = new ServiceObserver(null);

        Project m_SelectedProject = Project.Empty;

        Coroutine m_ApplyChangesCoroutine;

        public SyncManager()
        {
            m_Observer.onManifestsUpdated += OnManifestsUpdated;
            m_Observer.onSyncEnabled += () => onSyncEnabled?.Invoke();
            m_Observer.onSyncDisabled += () => onSyncDisabled?.Invoke();
            m_Observer.onSyncStarted += () => onSyncStarted?.Invoke();
            m_Observer.onSyncStopped += () => onSyncStopped?.Invoke();
        }

        public void LogReceived(Unity.Reflect.Utils.Logger.Level level, string msg)
        {
            switch (level)
            {
                case Unity.Reflect.Utils.Logger.Level.Debug:
                case Unity.Reflect.Utils.Logger.Level.Info:
                    Debug.Log(msg);
                    break;

                case Unity.Reflect.Utils.Logger.Level.Warn:
                    Debug.LogWarning(msg);
                    break;

                case Unity.Reflect.Utils.Logger.Level.Error:
                case Unity.Reflect.Utils.Logger.Level.Fatal:
                    Debug.LogError(msg);
                    break;

                default:
                    Debug.Log(msg);
                    break;
            }
        }

        void OnDestroy()
        {
            Debug.Log("Releasing observer...");
            UnbindObserver();
        }

        public IEnumerator Open(Project project)
        {
            if (IsProjectOpened(project))
            {
                Debug.LogWarning($"Project is already opened '{m_SelectedProject.name}'");
                yield break;
            }

            Close();
            yield return null;

            m_SelectedProject = project;

            m_ProgressBar.Register(this);

            ResetSyncRoot();

            const string kOpening = "Opening";

            progressChanged?.Invoke(0.0f, kOpening);

            try
            {
                var sessions = m_ProjectManager.LoadProjectManifests(project);

                foreach (var session in sessions)
                {
                    m_SyncInstances.TryGetValue(session.sourceId, out var syncInstance);

                    if (syncInstance == null)
                    {
                        var folder = m_ProjectManager.GetSourceProjectFolder(project, session.sourceId);

                        m_SyncInstances[session.sourceId] = syncInstance = new SyncInstance(m_SyncInstancesRoot, folder, session.sourceId);
                        syncInstance.onPrefabChanged += OnPrefabChanged;
                        onInstanceAdded?.Invoke(syncInstance);

                        syncInstance.ApplyModifications(session.manifest);
                    }
                }
            }
            catch (Exception ex)
            {
                taskCompleted?.Invoke();
                m_ProgressBar.UnRegister(this);
                m_SelectedProject = Project.Empty;
                m_SyncInstances.Clear();

                onError?.Invoke(ex);
                throw;
            }

            m_SyncMenu.Register(this);
            BindObserver();
            yield return null;
            
            taskCompleted?.Invoke();

            m_ProgressBar.UnRegister(this);

            RecenterSyncRoot();

            onProjectOpened?.Invoke();

            yield return DoApplyPrefabChanges();
        }

        void OnPrefabChanged(SyncInstance instance, SyncPrefab prefab)
        {
            ApplyPrefabChanges();
        }

        void RecenterSyncRoot()
        {
            var renderers = m_SyncInstancesRoot.GetComponentsInChildren<Renderer>();

            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; ++i)
            {
                var b = renderers[i].bounds;

                if (i == 0)
                {
                    bounds = b;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            var center = bounds.center - bounds.size.y * 0.5f * Vector3.up; // Middle-Bottom

            var offset = m_SyncInstancesRoot.position - center;

            m_SyncInstancesRoot.position = offset;
            m_SyncRoot.position = -offset;
        }

        void ResetSyncRoot()
        {
            foreach (Transform child in m_SyncRoot)
            {
                Destroy(child.gameObject);
            }

            m_SyncRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_SyncRoot.localScale = Vector3.one;

            m_SyncInstancesRoot = new GameObject("Instances").transform;
            m_SyncInstancesRoot.parent = m_SyncRoot;
        }

        public bool IsProjectOpened(Project project) => m_SelectedProject.serverProjectId == project.serverProjectId;

        public void Close()
        {
            UnbindObserver();
            
            if (m_SelectedProject != Project.Empty)
            {
                ResetSyncRoot();

                m_SyncInstances.Clear();

                m_SelectedProject = Project.Empty;

                m_SyncMenu.UnRegister(this);

                onProjectClosed?.Invoke();
            }
        }

        public void StartSync()
        {
            m_Observer.StartSync();
        }

        public void StopSync()
        {
            m_Observer.StopSync();
        }        
        
        public void ApplyPrefabChanges()
        {
            if (m_ApplyChangesCoroutine != null)
            {
                StopCoroutine(m_ApplyChangesCoroutine);
            }

            m_ApplyChangesCoroutine = StartCoroutine(DoApplyPrefabChanges());
        }

        IEnumerator DoApplyPrefabChanges()
        {
            foreach (var instance in m_SyncInstances)
            {
                yield return instance.Value.ApplyPrefabChanges();
            }
        }

        void OnManifestsUpdated(string projectId, IPlayerClient playerClient, bool allManifests, string[] sourceIds)
        {
            StartCoroutine(ManifestUpdatedInternal(projectId, playerClient, allManifests, sourceIds));
        }

        void OnProjectUpdated(Project project)
        {
            if (!IsProjectOpened(project))
            {
                return;
            }

            Debug.Log($"OnProjectUpdated '{project.serverProjectId}', Channel exists: {m_ProjectManager.IsProjectAvailableOnline(project)}");

            m_SelectedProject = project;
            BindObserver();
        }

        IEnumerator ManifestUpdatedInternal(string projectId, IPlayerClient playerClient, bool allManifests, string[] sourceIds)
        {
            if(!m_SelectedProject.projectId.Equals(projectId))
            {
                yield break;
            }

            onSyncUpdateBegin?.Invoke();

            Task<ManifestAsset[]> loadManifestsTask;
            if (allManifests)
            {
                loadManifestsTask = Task.Run(async () =>
                {
                    var result = await playerClient.GetManifestsAsync();
                    return result.ToArray();
                });
            }
            else
            {
                loadManifestsTask = Task.WhenAll(sourceIds.Select(playerClient.GetManifestAsync));
            }

            while (!loadManifestsTask.IsCompleted)
            {
                yield return null;
            }

            if (loadManifestsTask.IsFaulted)
            {
                onError?.Invoke(loadManifestsTask.Exception);
                throw loadManifestsTask.Exception;
            }

            var manifests = loadManifestsTask.Result.Select(m => m.Manifest);
            foreach (var manifest in manifests)
            {
                var sessionId = manifest.SourceId;
                
                if (!m_SyncInstances.TryGetValue(sessionId, out var syncInstance))
                {
                    var folder = m_ProjectManager.GetSourceProjectFolder(m_SelectedProject, sessionId);
                    m_SyncInstances[sessionId] = syncInstance = new SyncInstance(m_SyncInstancesRoot, folder, sessionId);
                }

                yield return m_ProjectManager.DownloadSourceProjectLocally(m_SelectedProject, sessionId, syncInstance.Manifest, manifest, playerClient, null);

                try
                {
                    var hasChanged = syncInstance.ApplyModifications(manifest);
                    onSyncUpdateEnd?.Invoke(hasChanged);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    throw;
                }                
            }
        }

		void Awake()
        {
            Unity.Reflect.Utils.Logger.AddReceiver(this);
            m_ProjectManager.onProjectChanged += OnProjectUpdated;

            if (m_SyncRoot == null)
            {
                Debug.LogWarning("SyncRoot is null");
            }
        }

        // iOS/Android specific MonoBehaviour callback
        // when app is either sent to background or revived from background
        void OnApplicationPause(bool isPaused)
        {
            Debug.Log($"OnApplicationPause: {isPaused}");
            if (isPaused)
            {
                UnbindObserver();
            }
            else
            {
                BindObserver();
            }
        }

        void BindObserver()
        {
            if (m_ProjectManager.IsProjectAvailableOnline(m_SelectedProject))
            {
                m_Observer.BindProject(m_SelectedProject);
            }
            else
            {
                UnbindObserver();
            }
        }
        
        void UnbindObserver()
        {
            m_Observer.BindProject(Project.Empty);
        }        

        void Update()
        {
            m_Observer.ProcessPendingEvents();
        }

        public event Action onSyncEnabled;
        public event Action onSyncDisabled;
        public event Action onSyncUpdateBegin;
        public event Action<bool> onSyncUpdateEnd;
        public event Action onSyncStarted;
        public event Action onSyncStopped;

        public event Action onProjectOpened;
        public event Action onProjectClosed;

        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;
    }
}