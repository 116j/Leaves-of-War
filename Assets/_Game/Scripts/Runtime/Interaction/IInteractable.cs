namespace Hortensia.Runtime
{
    public interface IInteractable
    {
        string Prompt { get; }

        void Interact();
    }
}
