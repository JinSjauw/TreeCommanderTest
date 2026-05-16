using System.Collections.Generic;
using UnityEngine;

public class PatrolPointManager : MonoBehaviour
{
    [SerializeField] private List<Transform> patrolPoints;

    public List<Transform> GetPatrolPoints() 
    {
        return patrolPoints;
    }
}
