# Diagnostic Debug.Logs to Remove

Files with temporary debug logging added during null propagation investigation.

| # | File | Line(s) | Description |
|---|------|---------|-------------|
| 1 | `Assets\BehaviourTree\Runtime\Methods\CheckVariableArray.cs` | ~83-108 | Header log + per-element value/type/match log in `Execute()` |
| 2 | `Assets\BehaviourTree\Runtime\Methods\VariableMethods.cs` | ~111-139 | Variable name resolution + before/after log in `ClearVariable.Execute()` |
| 3 | `Assets\BehaviourTree\Runtime\SquadInstance.cs` | ~183-271 | Two `DetectedEnemies` diagnostic blocks (var lookup + log) in `CopyToBB()` |
| 4 | `Assets\BehaviourTree\Runtime\SquadInstance.cs` | ~257-296 | Two `DetectedEnemies` diagnostic blocks (var lookup + log) in `CopyFromBB()` |
| 5 | `Assets\Scripts\Enemy\EnemyDetectionSystem.cs` | ~57-63 | Per-hit activeInHierarchy log + summary log in `DetectTargets()` |
| 6 | `Assets\BehaviourTree\Runtime\Tests\FormationDataFlowTests.cs` | ~713-842 | Phase A/B/C Debug.Logs in `NullPropagation_AgentClearsDetectedEnemy_ReachesCommander()` |
