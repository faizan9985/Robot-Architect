using Oculus.Voice.Dictation;
using UnityEngine;
namespace RemixTable.Voice
{
    // MIC button: listens for one spoken command with the Meta Voice SDK (dictation) and runs it through the
    // toolbox, e.g. "mute the drums", "solo vocals", "pitch mode", "loop", "undo", "start recording".
    // Dictation needs a Wit.ai app: create one at wit.ai, then Meta > Voice SDK > Get Started, paste its
    // client access token and assign the generated configuration to the Dictation object's App Dictation
    // Experience. Until then the MIC button reports that voice is not set up.
    public sealed class RemixVoiceInput : MonoBehaviour, IRemixToggle
    {
        public RemixToolbox toolbox;
        [Tooltip("Kept inactive in the scene until voice is used, so an unconfigured SDK stays silent.")]
        public AppDictationExperience dictation;
        bool listening;
        public string LastHeard { get; private set; }
        public bool Available => dictation != null && dictation.RuntimeConfiguration != null && dictation.RuntimeConfiguration.witConfiguration != null;
        public bool IsOn => listening && dictation != null && dictation.isActiveAndEnabled && (dictation.Active || dictation.IsRequestActive);
        public void SetOn(bool on)
        {
            if (!Available) return;
            if (on)
            {
                if (!dictation.gameObject.activeSelf) dictation.gameObject.SetActive(true);
                Subscribe();
                listening = true;
                dictation.Activate();
            }
            else
            {
                listening = false;
                dictation.Deactivate();
            }
        }
        bool subscribed;
        void Subscribe()
        {
            if (subscribed || dictation == null) return;
            dictation.DictationEvents.OnFullTranscription.AddListener(Heard);
            dictation.DictationEvents.OnStoppedListening.AddListener(Stopped);
            subscribed = true;
        }
        void Heard(string text)
        {
            LastHeard = text;
            if (toolbox != null) toolbox.RunPhrase(text);
        }
        void Stopped() => listening = false;
        void OnDisable()
        {
            if (!subscribed || dictation == null) return;
            dictation.DictationEvents.OnFullTranscription.RemoveListener(Heard);
            dictation.DictationEvents.OnStoppedListening.RemoveListener(Stopped);
            subscribed = false; listening = false;
        }
    }
}
