using UnityEngine;

public interface IDoor
{
    bool IsOpen { get; }

    void OpenDoor();
    void OpenDoor(Transform opener);

    void CloseDoor();
    void ToggleDoor();
}
