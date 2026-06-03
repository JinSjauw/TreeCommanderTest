using UnityEngine;

public class CorpseSpawn : MonoBehaviour
{
    [SerializeField] private int jointsToDisconnect = 3;

    private CharacterJoint[] allJoints;

    private void OnEnable()
    {
        if(jointsToDisconnect <= 0)
        {
            return; 
        }
        allJoints = GetComponentsInChildren<CharacterJoint>();

        int count = Mathf.Min(jointsToDisconnect, allJoints.Length);
        for (int i = 0; i < count; i++)
        {
            int swapIndex = Random.Range(i, allJoints.Length);
            CharacterJoint temp = allJoints[i];
            allJoints[i] = allJoints[swapIndex];
            allJoints[swapIndex] = temp;

            Destroy(allJoints[i]);
        }
    }
}
