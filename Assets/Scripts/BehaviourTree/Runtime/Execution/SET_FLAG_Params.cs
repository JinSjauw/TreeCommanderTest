using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct SET_FLAG_Params
    {
        public bool valueToSet;

        [SharedVar]
        public bool flagToSet;
    }
}
