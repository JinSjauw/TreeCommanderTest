using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    [NodeMethod("SendOrder", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class SendOrderMethod : ActionMethod
    {
        [SharedVar(IsHidden = true, AutoVariableName = "AgentOrders")]
        public int ordersSlot;

        [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
        public int orderValue;

        //Debug
        private OrderRegistry orderRegistry;

        protected override void OnInitialize()
        {
            orderRegistry = OrderRegistry.FindInstance();
        }

        public override NodeState Execute(TickContext ctx)
        {
            ordersSlot = orderValue;
            //Debug.Log($"SendOrderMethod: sentOrder: {orderRegistry.orderNames[orderValue]}");

            return NodeState.SUCCESS;
        }
    }
}
