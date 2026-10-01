using SectorCleanse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Full-screen "choose your name" overlay. Shown on top of the menu until the
    /// player has claimed a valid, unique name; hides itself afterwards.
    /// The input field only accepts letters and digits and caps the length, and the
    /// profile re-validates everything (and checks for duplicates) on confirm.
    /// </summary>
    public class NameEntryView : MonoBehaviour
    {
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private InputField input;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text errorLabel;

        private void Awake()
        {
            if (!profile) profile = FindAnyObjectByType<PlayerProfile>();
            if (input)
            {
                input.characterLimit = PlayerProfile.MaxLength;
                input.contentType = InputField.ContentType.Alphanumeric;
            }
        }

        private void OnEnable()
        {
            if (confirmButton) confirmButton.onClick.AddListener(Confirm);
            if (input) input.onValueChanged.AddListener(ClearError);
            if (errorLabel) errorLabel.text = "";
        }

        private void OnDisable()
        {
            if (confirmButton) confirmButton.onClick.RemoveListener(Confirm);
            if (input) input.onValueChanged.RemoveListener(ClearError);
        }

        private void Start() => UpdateVisibility();

        /// <summary>
        /// Show the panel if no name is set, hide it otherwise. The menu calls this
        /// whenever it opens or the name changes (a disabled panel can't listen itself).
        /// </summary>
        public void UpdateVisibility()
        {
            if (profile) gameObject.SetActive(!profile.HasName);
        }

        private void ClearError(string _)
        {
            if (errorLabel) errorLabel.text = "";
        }

        private void Confirm()
        {
            if (!profile || !input) return;

            profile.TryClaimName(input.text, (ok, error) =>
            {
                if (ok) gameObject.SetActive(false);
                else if (errorLabel) errorLabel.text = error;
            });
        }
    }
}
