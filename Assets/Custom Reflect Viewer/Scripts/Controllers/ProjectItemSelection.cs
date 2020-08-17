using UnityEngine.UI;

namespace UnityEngine.Reflect.Extensions
{
    /// <summary>
    /// Handles itme selection of the project from the project list
    /// </summary>
    public class ProjectItemSelection : MonoBehaviour
    {
        [Tooltip("The Camera Selection Top Menu component.")]
        [SerializeField] ProjectMenuSelection selectionMenu = default;
        [Tooltip("The Text component that contains the name of the Project.")]
        [SerializeField] Text m_Text = default;
        /// <summary>
        /// The text field which is the name of the project
        /// </summary>
        public Text Text { get => m_Text; set => m_Text = value; }

        /// <summary>
        /// Button pressed on menu item. Send project name to ProjectMenuSelection
        /// </summary>
        public void OnProjectSelected()
        {
            if (selectionMenu != null && m_Text != null)
            {
                selectionMenu.ProjectSelected(m_Text.text);
            }
            else
                Debug.LogWarningFormat("Please fill in Selection Menu nad Text field on {0}", this);
        }
    }
}