using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Editor;

public class GraphViewStabilityTests
{
    private static BehaviourNodeView MakeView(BehaviourTreeEditorGraphView view, BehaviourNode node, string guid)
    {
        node.guid = guid;
        var nodeView = new BehaviourNodeView(node) { GraphView = view };
        view.AddElement(nodeView);
        return nodeView;
    }

    [Test]
    public void BuildWarningTooltip_FromList_RendersAllMessages()
    {
        var warnings = new List<NodeWarning>
        {
            new NodeWarning { Type = NodeWarningType.VariableNotFound, Message = "Variable 'Foo' not found in Blackboard." },
            new NodeWarning { Type = NodeWarningType.VariableNotAssigned, Message = "Variable not assigned for 'Bar'." },
        };

        string tooltip = NodeWarningEvaluator.BuildWarningTooltip(warnings);

        StringAssert.Contains("Foo", tooltip);
        StringAssert.Contains("Bar", tooltip);
        Assert.AreEqual("No warnings", NodeWarningEvaluator.BuildWarningTooltip(new List<NodeWarning>()));
    }

    [Test]
    public void GraphEditorTheme_Instance_IsNeverNull()
    {
        Assert.NotNull(GraphEditorTheme.instance);
    }

    [Test]
    public void Dispose_DestroysSearchProvider_AndIsIdempotent()
    {
        int before = Resources.FindObjectsOfTypeAll<NodeSearchProvider>().Length;

        var view = new BehaviourTreeEditorGraphView();
        view.EnsureSearchWindow();
        Assert.AreEqual(before + 1, Resources.FindObjectsOfTypeAll<NodeSearchProvider>().Length);

        view.Dispose();
        Assert.AreEqual(before, Resources.FindObjectsOfTypeAll<NodeSearchProvider>().Length);
        Assert.DoesNotThrow(() => view.Dispose());
    }

    [Test]
    [Timeout(5000)]
    public void GetCompatiblePorts_PreExistingCycle_CompletesWithoutHanging()
    {
        var view = new BehaviourTreeEditorGraphView();
        try
        {
            var nodeA = ScriptableObject.CreateInstance<CompositeNode>();
            var nodeB = ScriptableObject.CreateInstance<CompositeNode>();
            var nodeC = ScriptableObject.CreateInstance<CompositeNode>();

            BehaviourNodeView viewA = MakeView(view, nodeA, "guid-a");
            BehaviourNodeView viewB = MakeView(view, nodeB, "guid-b");
            BehaviourNodeView viewC = MakeView(view, nodeC, "guid-c");

            // Hand-wire a corrupt cycle in the visual graph: A -> B -> A
            view.AddElement(viewA.output.ConnectTo(viewB.input));
            view.AddElement(viewB.output.ConnectTo(viewA.input));

            // Without a visited-set guard in WouldCreateCycle this call never returns.
            var compatible = view.GetCompatiblePorts(viewC.output, null);

            Assert.NotNull(compatible);
        }
        finally
        {
            view.Dispose();
        }
    }
}
