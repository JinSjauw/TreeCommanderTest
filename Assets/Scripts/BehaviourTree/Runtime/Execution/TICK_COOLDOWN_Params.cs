using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct TICK_COOLDOWN_Params
    {
        public float delay;

        [SharedVar]
        public float cooldownToTick;
    }
}
