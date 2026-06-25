using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Compares the agent's AgentReceivedOrder (populated by the binding bridge
    /// from the commander's AgentOrders) against an expected order index.
    /// Returns SUCCESS on match, FAILURE otherwise.
    /// </summary>
    [NodeMethod("CheckOrder", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class CheckOrderMethod : ConditionMethod
    {
        /// <summary>Baked slot offset of the AgentReceivedOrder squad-data variable.
        /// Auto-bound to "AgentReceivedOrder" by convention — not visible in the inspector.</summary>
        [SharedVar(IsHidden = true, AutoVariableName = "AgentReceivedOrder")]
        public int receivedOrderSlot;

        /// <summary>Expected order to check against. Toggle OFF: order search dropdown
        /// picks an order name stored as stringValue, resolved to index at bake time.
        /// Toggle ON: reads from a BB int variable (dynamic per-frame).</summary>
        [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
        public int expectedOrder;

        private OrderRegistry orderRegistry;

        protected override void OnInitialize()
        {
            orderRegistry = OrderRegistry.FindInstance();
        }

        public override NodeState Execute()
        {
            Debug.Log($"CheckOrderMethod: receivedOrder: {orderRegistry.orderNames[receivedOrderSlot]}, expectedOrder: {orderRegistry.orderNames[expectedOrder]}");
            return receivedOrderSlot == expectedOrder ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
