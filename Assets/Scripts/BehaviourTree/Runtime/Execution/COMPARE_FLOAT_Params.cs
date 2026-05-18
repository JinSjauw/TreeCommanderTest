using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct COMPARE_FLOAT_Params
    {
        [SharedVar]
        public float value;

        public float threshold;

        public int operation;
    }
}
