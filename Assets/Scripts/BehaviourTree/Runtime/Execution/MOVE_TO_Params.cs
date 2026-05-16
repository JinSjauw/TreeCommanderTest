using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct MOVE_TO_Params
    {
        [SharedVar]
        public Transform target;

        public float arrivalDistance;
    }
}
