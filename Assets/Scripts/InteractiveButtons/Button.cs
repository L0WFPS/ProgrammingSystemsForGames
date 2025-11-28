using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.Events;

public interface IButton
{
    void PressButton();
}
public class Button : MonoBehaviour, IButton
{
    [Header("Events")]
    public UnityEvent onPressed;

    // Call this when the button is pressed physically (your collision / click logic)
    public void PressButton()
    {
        onPressed?.Invoke();
    }
}
