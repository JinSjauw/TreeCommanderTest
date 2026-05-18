namespace BehaviourTree.Core
{
    public enum MethodID 
    {
        NONE = 0,
        [MethodCategory(BehaviourNodeType.DECORATOR)]INVERTER = 1,
        [MethodCategory(BehaviourNodeType.DECORATOR)]REPEATER = 2,
        [MethodCategory(BehaviourNodeType.ACTION)]MOVE_TO = 3,
        [MethodCategory(BehaviourNodeType.ACTION)]SET_NEXT_PATROL_POINT = 4,
        [MethodCategory(BehaviourNodeType.ACTION)]AIM = 5,
        [MethodCategory(BehaviourNodeType.ACTION)]ATTACK = 6,
        [MethodCategory(BehaviourNodeType.ACTION)]TICK_COOLDOWN = 7,
        [MethodCategory(BehaviourNodeType.ACTION)]WAIT = 8,
        [MethodCategory(BehaviourNodeType.CONDITION)]CHECK_FLAG = 9,
        [MethodCategory(BehaviourNodeType.CONDITION)]TARGET_IN_RANGE = 10,
        [MethodCategory(BehaviourNodeType.ACTION)]SET_FLAG = 11,
        [MethodCategory(BehaviourNodeType.CONDITION)]COMPARE_FLOAT = 12,
        [MethodCategory(BehaviourNodeType.CONDITION)]HAS_TRAJECTORY = 13,
        [MethodCategory(BehaviourNodeType.CONDITION)]IS_ON_TARGET = 14,
        [MethodCategory(BehaviourNodeType.CONDITION)]IS_RELOADING = 15,
        [MethodCategory(BehaviourNodeType.CONDITION)]HAS_DIRECT_LOS = 16,
        [MethodCategory(BehaviourNodeType.ACTION)]SET_NEXT_ATTACK_POINT = 17,
        [MethodCategory(BehaviourNodeType.ACTION)]DETECT_TARGET = 18,
        [MethodCategory(BehaviourNodeType.CONDITION)]HAS_TARGET = 19,
        [MethodCategory(BehaviourNodeType.CONDITION)]ARRIVED_AT_DESTINATION = 20,
        [MethodCategory(BehaviourNodeType.ACTION)]SELECT_AIM_TARGET = 21,
        [MethodCategory(BehaviourNodeType.CONDITION)]HAS_AIM_TARGET = 22,
    }
}
