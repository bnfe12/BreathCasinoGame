namespace BreathCasino.Core
{
    public interface IHoldInteractable : IInteractable
    {
        bool CanHold { get; }
        float HoldDuration { get; }
        void OnHoldStart();
        void OnHoldProgress(float progress01);
        void OnHoldCancel();
        void OnHoldComplete();
    }
}
