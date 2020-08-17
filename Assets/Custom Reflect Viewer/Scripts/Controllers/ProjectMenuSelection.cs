using System.Collections.Generic;
using System.Collections;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.Reflect.Extensions
{
    /// <summary>
    /// Project Menu Selection for the saved projects behavior using the Addressable system
    /// </summary>
    public class ProjectMenuSelection : MonoBehaviour
    {
        [Tooltip("Reflect Projects Menu.")]
        [SerializeField] GameObject reflectProjectsMenu = default;
        [Tooltip("Addressables Saved Projects Menu.")]
        [SerializeField] GameObject savedProjectsMenu = default;
        [Tooltip("Projects Top Menu.")]
        [SerializeField] ProjectsTopMenu topMenu = default;
        [Tooltip("Project Item Template.")]
        [SerializeField] GameObject itemTemplate = default;
        [Tooltip("The root object to instaniate the model.")]
        [SerializeField] Transform modelRoot = default;
        [Tooltip("The Progress Bar for project loading.")]
        [SerializeField] ProgressBar progressBar = default;

        void OnEnable()
        {
            AddressablesManager.Instance.ModelListReady += MakeProjectList;
            progressBar.Register(AddressablesManager.Instance);
        }

        void OnDisable()
        {
            AddressablesManager.Instance.ModelListReady -= MakeProjectList;
            progressBar.UnRegister(AddressablesManager.Instance);
        }

        void MakeProjectList()
        {
            if (modelRoot == default)
            {
                Debug.LogErrorFormat("A root transform to load model is required on {0}", this);
                return;
            }
            if (itemTemplate == null)
            {
                Debug.LogErrorFormat("A valid Item menu template is required on {0}", this);
                return;
            }

            if (reflectProjectsMenu != null)
                reflectProjectsMenu.SetActive(false);
            if (savedProjectsMenu != null)
            savedProjectsMenu.SetActive(true);

            foreach (KeyValuePair<IResourceLocation, string> kvp in AddressablesManager.Instance.AvailableProjects)
            {
                GameObject obj = Instantiate(itemTemplate, itemTemplate.transform.parent);
                obj.SetActive(true);
                ProjectItemSelection item = obj.GetComponent<ProjectItemSelection>();
                if (item != null && item.Text != null)
                {
                    item.Text.text = kvp.Value;
                }
                else
                    Debug.LogWarningFormat("A Selection Menu and Text field is required on {0}", itemTemplate);
            }
        }

        /// <summary>
        /// Menu item selected so remove any existing model and load new model
        /// </summary>
        /// <param name="projectName">Project name</param>
        public void ProjectSelected(string projectName)
        {
            if (!string.IsNullOrEmpty(projectName))
            {
                if (modelRoot.childCount > 0)
                {
                    List<Transform> objectsToDestroy = new List<Transform>();
                    foreach (Transform child in modelRoot)
                    {
                        objectsToDestroy.Add(child);
                    }
                    foreach (Transform childTransform in objectsToDestroy)
                    {
                        Destroy(childTransform.gameObject);
                    }
                }

                // Closes menu
                if (topMenu != null)
                    topMenu.OnOpen();
                // Give time for the model to be destroyed
                StartCoroutine(WaitForModelRemoval(projectName));
            }
        }

        IEnumerator WaitForModelRemoval(string projectName)
        {
            yield return new WaitForSeconds(0.5f);
            AddressablesManager.Instance.LoadModel(projectName, modelRoot);
        }
    }
}