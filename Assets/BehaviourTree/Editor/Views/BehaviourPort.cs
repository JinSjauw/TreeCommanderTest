using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    public class BehaviourPort : Port
    {
        private VisualElement portCapElement;
        private bool wasPortCapLit;
        private IVisualElementScheduledItem portCapPoll;

        public BehaviourPort(Orientation orientation, Direction direction, Capacity capacity, System.Type type) 
        : base(orientation, direction, capacity, type)
        {
            portName = "";

            LoadTemplate();
            SetupBaseClasses();
        }

        /// <summary>
        /// Wires the EdgeConnector to the base Port's m_EdgeConnector field.
        /// Required because Port.Create&lt;T&gt;() is not used — ports are instantiated
        /// directly via new BehaviourPort(). Without this, EdgeManipulator crashes
        /// with NullReferenceException when tearing off an edge (it accesses
        /// m_ConnectedPort.edgeConnector.edgeDragHelper, but edgeConnector is null).
        /// </summary>
        public void SetEdgeConnector(EdgeConnector edgeConnector)
        {
            m_EdgeConnector = edgeConnector;
        }

        public override void Connect(Edge edge)
        {
            base.Connect(edge);
            UpdateConnectionStateClass();
        }

        public override void Disconnect(Edge edge)
        {
            base.Disconnect(edge);
            UpdateConnectionStateClass();
        }

        private static VisualTreeAsset cachedPortUxml;

        private void LoadTemplate()
        {
            if (cachedPortUxml == null)
                cachedPortUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.BehaviourPortUxml);
            if (cachedPortUxml != null)
                cachedPortUxml.CloneTree(this);
        }

        private void SetupBaseClasses()
        {
            AddToClassList("behaviour-port");

            if (direction == Direction.Input)
            {
                AddToClassList("behaviour-port--input");
            }
            else
            {
                AddToClassList("behaviour-port--output");
            }

            UpdateConnectionStateClass();

            VisualElement connectorElement = this.Q<VisualElement>("connector");
            if (connectorElement != null)
            {
                connectorElement.pickingMode = PickingMode.Position;
            }

            portCapElement = this.Q<VisualElement>("port-cap");

            // Poll portCapLit only while an edge is being dragged (ghost edge blocks mouse events).
            portCapPoll = schedule.Execute(PollPortCapLit).Every(50);
            portCapPoll.Pause();
        }

        public override void OnStartEdgeDragging()
        {
            base.OnStartEdgeDragging();
            portCapPoll.Resume();
        }

        public override void OnStopEdgeDragging()
        {
            base.OnStopEdgeDragging();
            portCapPoll.Pause();
            wasPortCapLit = false;
            UpdateCustomCapHover(false);
        }

        [EventInterest(typeof(MouseEnterEvent), typeof(MouseLeaveEvent), typeof(MouseUpEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);

            if (portCapElement == null)
                return;

            if (evt.eventTypeId == MouseEnterEvent.TypeId())
            {
                UpdateCustomCapHover(true);
            }
            else if (evt.eventTypeId == MouseLeaveEvent.TypeId())
            {
                UpdateCustomCapHover(false);
            }
            else if (evt.eventTypeId == MouseUpEvent.TypeId())
            {
                // Edge drag ended — reset if mouse is outside the port layout
                MouseUpEvent mouseUpEvent = evt as MouseUpEvent;
                if (mouseUpEvent != null && !layout.Contains(mouseUpEvent.localMousePosition))
                {
                    wasPortCapLit = false;
                    UpdateCustomCapHover(false);
                }
            }
        }

        private void PollPortCapLit()
        {
            if (portCapElement == null)
                return;

            if (portCapLit != wasPortCapLit)
            {
                wasPortCapLit = portCapLit;
                UpdateCustomCapHover(portCapLit);
            }
        }

        private void UpdateCustomCapHover(bool hovered)
        {
            if (hovered)
                portCapElement.AddToClassList("behaviour-port__cap--hover");
            else
                portCapElement.RemoveFromClassList("behaviour-port__cap--hover");
        }

        private void UpdateConnectionStateClass()
        {
            RemoveFromClassList("behaviour-port--connected");
            RemoveFromClassList("behaviour-port--disconnected");

            if (connected)
            {
                AddToClassList("behaviour-port--connected");
            }
            else
            {
                AddToClassList("behaviour-port--disconnected");
            }
        }
    }
}
