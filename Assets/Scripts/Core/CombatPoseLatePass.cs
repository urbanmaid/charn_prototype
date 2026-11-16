using UnityEngine;

// Separate from the early Update restoration so existing aiming/IK finishes first.
[DefaultExecutionOrder(500)]
public sealed class CombatPoseLatePass : MonoBehaviour
{
    public CombatBodyReaction reaction;
    public CombatMovementAccent movement;
    public CombatCameraFeedback cameraFeedback;
    private void LateUpdate()
    {
        if (reaction && reaction.isActiveAndEnabled) reaction.ApplyPose();
        if (movement && movement.isActiveAndEnabled) movement.ApplyPose();
        if (cameraFeedback && cameraFeedback.isActiveAndEnabled) cameraFeedback.ApplyHandPose();
    }
}
