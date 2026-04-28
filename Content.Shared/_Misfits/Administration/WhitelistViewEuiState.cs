// #Misfits Change - Shared state and messages for the Whitelist Viewer EUI
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration;

[Serializable, NetSerializable]
public sealed class WhitelistViewEuiState : EuiStateBase
{
    public List<string> WhitelistedCkeys;

    public WhitelistViewEuiState(List<string> whitelistedCkeys)
    {
        WhitelistedCkeys = whitelistedCkeys;
    }
}

[Serializable, NetSerializable]
public sealed class RequestWhitelistViewMessage : EuiMessageBase
{
}
