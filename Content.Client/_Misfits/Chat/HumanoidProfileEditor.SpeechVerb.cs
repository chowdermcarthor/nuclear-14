// #Misfits Add - vocal style preference picker in character creation

using System.Linq;
using Content.Shared.Speech;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<SpeechVerbPrototype> _speechVerbList = new();

    private void InitializeSpeechVerb()
    {
        _speechVerbList = _prototypeManager
            .EnumeratePrototypes<SpeechVerbPrototype>()
            .Where(p => p.VocalStyleSelectable)
            .OrderBy(p => Loc.GetString(p.Name))
            .ToList();

        SpeechVerbButton.OnItemSelected += args =>
        {
            SpeechVerbButton.SelectId(args.Id);
            SetSpeechVerbPreference(_speechVerbList[args.Id].ID);
        };
    }

    private void UpdateSpeechVerbControls()
    {
        if (Profile is null)
            return;

        SpeechVerbButton.Clear();
        for (var i = 0; i < _speechVerbList.Count; i++)
        {
            var verb = _speechVerbList[i];
            SpeechVerbButton.AddItem(Loc.GetString(verb.Name), i);
        }

        var currentId = _speechVerbList.FindIndex(x => x.ID == Profile.SpeechVerbPreference);
        if (currentId < 0)
            currentId = _speechVerbList.FindIndex(x => x.ID == "Default");
        if (currentId >= 0)
            SpeechVerbButton.SelectId(currentId);
    }

    private void SetSpeechVerbPreference(string verbId)
    {
        Profile = Profile?.WithSpeechVerbPreference(verbId);
        IsDirty = true;
    }
}
