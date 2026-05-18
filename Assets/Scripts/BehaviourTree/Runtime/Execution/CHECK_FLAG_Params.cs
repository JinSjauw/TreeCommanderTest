using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct CHECK_FLAG_Params
    {
        [SharedVar]
        public bool flagToCheck;
    }
}
