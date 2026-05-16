using System.Runtime.InteropServices;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct CHECK_FLAG_Params
    {
        public bool flagToCheck;
    }
}
