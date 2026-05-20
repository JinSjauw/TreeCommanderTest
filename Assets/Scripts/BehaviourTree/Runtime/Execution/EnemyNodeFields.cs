using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_MoveTo_NodeFields
    {
        [SharedVar]
        public Vector3 TargetMovePosition;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SelectPatrolPoint_NodeFields
    {
        [SharedVar]
        public Vector3 TargetMovePosition;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SelectEngagePosition_NodeFields
    {
        [SharedVar]
        public Vector3 TargetMovePosition;
    }
}
