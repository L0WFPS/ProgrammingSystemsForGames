using UnityEngine;

public class FinishLogic : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WinCondition"))
        {
            Debug.Log("win");
            Application.Quit();
        }
    }
}
