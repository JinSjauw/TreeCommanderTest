using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct WAIT_Params
    {
        public float waitTime;

        [SharedVar]
        public float timer;
    }
}
