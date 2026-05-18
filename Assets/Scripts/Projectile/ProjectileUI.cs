using UnityEngine;

public class ProjectileUI : MonoBehaviour
{
    [SerializeField] private GameObject projectileMarkObject;
    [SerializeField] private GameObject[] lockedIndicator;

    public void EnableMark(bool state) 
    {
        projectileMarkObject.SetActive(state);
    }

    public void EnableLockMarks(int index) 
    {
        Debug.Log("Lock mark: " + index);
        if(index < lockedIndicator.Length) 
        {
            lockedIndicator[index].SetActive(true);
        }
    }

    public void DisableLockMark() 
    {
        for (int i = 0; i < lockedIndicator.Length; i++) 
        {
            lockedIndicator[i].SetActive(false);
        }
    }
}
