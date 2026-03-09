using Content.Shared.MedicalScanner;
using Content.Shared.DeltaV.MedicalRecords; // #Misfits Change
using Content.Shared._Shitmed.Targeting;
using JetBrains.Annotations;

namespace Content.Client.HealthAnalyzer.UI
{
    // #Misfits Change
    [UsedImplicitly]
    public sealed class HealthAnalyzerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private HealthAnalyzerWindow? _window;

        public HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = new HealthAnalyzerWindow
            {
                Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName,
            };
            _window.OnClose += Close;
            _window.OnBodyPartSelected += SendBodyPartMessage;
            _window.OnTriageStatusChanged += SendTriageStatusMessage;
            _window.OnClaimPatient += SendTriageClaimMessage;
            _window.OpenCentered();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            if (message is not HealthAnalyzerScannedUserMessage cast)
                return;

            _window.Populate(cast);
        }

        private void SendBodyPartMessage(TargetBodyPart? part, EntityUid target) => SendMessage(new HealthAnalyzerPartMessage(EntMan.GetNetEntity(target), part ?? null));

        private void SendTriageStatusMessage(TriageStatus status) => SendMessage(new HealthAnalyzerTriageStatusMessage(status));

        private void SendTriageClaimMessage() => SendMessage(new HealthAnalyzerTriageClaimMessage());

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_window != null)
            {
                _window.OnClose -= Close;
                _window.OnBodyPartSelected -= SendBodyPartMessage;
                _window.OnTriageStatusChanged -= SendTriageStatusMessage;
                _window.OnClaimPatient -= SendTriageClaimMessage;
                _window.Close();
            }

            _window = null;
        }
    }
}
