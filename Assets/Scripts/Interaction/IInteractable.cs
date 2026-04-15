namespace BreathCasino.Core
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        void OnHoverEnter();
        void OnHoverExit();
        void OnClick();
    }
}